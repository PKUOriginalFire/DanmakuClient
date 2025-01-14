using System.Globalization;
using System.Windows;
using System.Windows.Input;
using DanmakuClient.Models;
using DanmakuClient.Services;

namespace DanmakuClient;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow {
    private readonly AppSettings _settings;

    public MainWindow() {
        InitializeComponent();

        // 加载设置
        _settings = SettingsService.LoadSettings();
        LoadSettingsToUi();
    }

    private void LoadSettingsToUi() {
        UrlTextBox.Text = _settings.LastUrl;
        TopPaddingBox.Text = (_settings.Padding.Top * 100).ToString(CultureInfo.InvariantCulture);
        BottomPaddingBox.Text = (_settings.Padding.Bottom * 100).ToString(CultureInfo.InvariantCulture);
        LeftPaddingBox.Text = (_settings.Padding.Left * 100).ToString(CultureInfo.InvariantCulture);
        RightPaddingBox.Text = (_settings.Padding.Right * 100).ToString(CultureInfo.InvariantCulture);
    }

    private void SaveCurrentSettings() {
        _settings.LastUrl = UrlTextBox.Text.Trim();
        if (TryParseAllPaddings(out var padding)) {
            _settings.Padding.Left = padding.Left;
            _settings.Padding.Top = padding.Top;
            _settings.Padding.Right = padding.Right;
            _settings.Padding.Bottom = padding.Bottom;
        }

        SettingsService.SaveSettings(_settings);
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) {
        var url = UrlTextBox.Text.Trim();

        // 检查 URL 是否有效
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)) {
            MessageBox.Show("请输入有效的 URL！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 获取并验证所有边距值
        if (!TryParseAllPaddings(out var padding)) {
            MessageBox.Show("请输入有效的边距值！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // 保存当前设置
        SaveCurrentSettings();

        // 打开 WebView2 窗口并加载指定 URL
        var overlayWindow = new OverlayWindow(url, padding);
        overlayWindow.Show();

        // 关闭当前窗口
        Close();
    }

    private bool TryParseAllPaddings(out Thickness padding) {
        padding = new Thickness();

        if (!TryParsePercentage(LeftPaddingBox.Text, out var left) ||
            !TryParsePercentage(TopPaddingBox.Text, out var top) ||
            !TryParsePercentage(RightPaddingBox.Text, out var right) ||
            !TryParsePercentage(BottomPaddingBox.Text, out var bottom))
            return false;

        // 存储为小数形式的百分比值
        padding = new Thickness(left / 100, top / 100, right / 100, bottom / 100);
        return true;
    }

    private static bool TryParsePercentage(string input, out double percentage) {
        percentage = 0;
        if (!double.TryParse(input, out var value)) return false;

        // 验证百分比值是否在有效范围内（0-100）
        if (value is < 0 or > 100) return false;

        percentage = value;
        return true;
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.ButtonState == MouseButtonState.Pressed) {
            DragMove();
        }
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e) {
        Close();
    }
}