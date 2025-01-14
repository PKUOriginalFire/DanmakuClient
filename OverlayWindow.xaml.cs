using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DanmakuClient.Services;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;

namespace DanmakuClient;

public partial class OverlayWindow {
    // WinAPI 调用
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x80000;
    private const int WsExTransparent = 0x20;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly string _url;
    private Thickness _padding;

    public OverlayWindow(string url, Thickness padding) {
        _url = url;
        _padding = padding;
        _notifyIcon = new Forms.NotifyIcon(); // 在构造函数开始时初始化
        InitializeComponent();
        InitializeNotifyIcon();

        // 初始化 WebView2 并加载远程 URL
        InitializeWebView2(url).ConfigureAwait(false);

        // 设置窗口鼠标点击穿透
        Loaded += (_, _) => EnableMouseClickThrough();
    }

    private async Task InitializeWebView2(string url) {
        // 确保 WebView2 环境初始化
        await WebView.EnsureCoreWebView2Async();

        if (WebView.CoreWebView2 != null) {
            // 设置 WebView2 背景为透明
            WebView.DefaultBackgroundColor = Color.Transparent;

            // 注册加载完成事件，注入 CSS 样式
            WebView.CoreWebView2.NavigationCompleted +=
                async (_, e) => await WebView_NavigationCompleted(e);

            // 加载指定 URL
            WebView.Source = new Uri(url);
        }
    }

    private async Task WebView_NavigationCompleted(CoreWebView2NavigationCompletedEventArgs e) {
        if (e.IsSuccess) {
            // 注入 JavaScript 修改网页背景为透明并设置边距
            var script = $@"
                (function() {{
                    document.body.style.backgroundColor = 'transparent';
                    document.body.style.overflow = 'hidden';
                    document.body.style.margin = '0';
                    document.body.style.padding = '0';
                    
                    // 移除可能存在的旧容器
                    const oldContainer = document.querySelector('div[data-role=""danmaku-container""]');
                    if (oldContainer) {{
                        while (oldContainer.firstChild) {{
                            document.body.appendChild(oldContainer.firstChild);
                        }}
                        oldContainer.remove();
                    }}
                    
                    const container = document.createElement('div');
                    container.setAttribute('data-role', 'danmaku-container');
                    container.style.position = 'fixed';
                    container.style.top = '{_padding.Top * 100}%';
                    container.style.left = '{_padding.Left * 100}%';
                    container.style.right = '{_padding.Right * 100}%';
                    container.style.bottom = '{_padding.Bottom * 100}%';
                    
                    while (document.body.firstChild) {{
                        container.appendChild(document.body.firstChild);
                    }}
                    document.body.appendChild(container);
                }})();";
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }

    private void EnableMouseClickThrough() {
        var hwnd = new WindowInteropHelper(this).Handle;

        var exStyle = GetWindowLongPtr(hwnd, GwlExStyle);
        if (exStyle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

        var result = SetWindowLongPtr(hwnd, GwlExStyle,
            new IntPtr(exStyle.ToInt64() | WsExLayered | WsExTransparent));

        if (result == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private void InitializeNotifyIcon() {
        _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
            Application.ResourceAssembly.Location); // 明确指定 Windows.Application
        _notifyIcon.Visible = true;
        _notifyIcon.Text = "弹幕显示窗口";

        var contextMenu = new Forms.ContextMenuStrip();

        contextMenu.Items.Add("刷新页面 (重载配置)", null, (_, _) => {
            try {
                // 重新加载配置
                var settings = SettingsService.LoadSettings();
                _padding = new Thickness(
                    settings.Padding.Left,
                    settings.Padding.Top,
                    settings.Padding.Right,
                    settings.Padding.Bottom
                );

                if (WebView.CoreWebView2 != null) {
                    // 如果配置中的URL和当前URL不同，则直接导航到新URL
                    if (settings.LastUrl != _url)
                        WebView.Source = new Uri(settings.LastUrl);
                    else
                        // 使用 Task.Run 来执行异步操作
                        Task.Run(() => {
                            try {
                                WebView.CoreWebView2.ExecuteScriptAsync("location.reload();").Wait();
                            }
                            catch (Exception ex) {
                                Application.Current.Dispatcher.Invoke(() => {
                                    MessageBox.Show($"刷新页面失败：{ex.Message}", "错误",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                            }
                        });
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"刷新页面失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        contextMenu.Items.Add("窗口设置...", null, (_, _) => {
            Application.Current.Dispatcher.Invoke(() => { // 明确指定 Windows.Application
                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            });
        });

        contextMenu.Items.Add("编辑配置文件...", null, (_, _) => {
            try {
                var settingsPath = SettingsService.SettingsPath;
                if (File.Exists(settingsPath)) {
                    var startInfo = new ProcessStartInfo {
                        FileName = settingsPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                else {
                    MessageBox.Show("配置文件不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"配置设置文件失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        contextMenu.Items.Add("-"); // 添加分隔线

        contextMenu.Items.Add("退出程序", null, (_, _) => {
            Application.Current.Dispatcher.Invoke(() => { // 明确指定 Windows.Application
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Application.Current.Shutdown();
            });
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    // 添加析构函数确保清理托盘图标
    protected override void OnClosed(EventArgs e) {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnClosed(e);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}