using System.IO;
using System.Text.Json;
using DanmakuClient.Models;

namespace DanmakuClient.Services;

public static class SettingsService {
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() {
        WriteIndented = true
    };

    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DanmakuClient",
        "settings.json"
    );

    public static AppSettings LoadSettings() {
        try {
            if (File.Exists(SettingsPath)) {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception) {
            // 如果读取失败，返回默认设置
        }

        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings) {
        try {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, JsonSerializerOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception) {
            // 保存失败时暂时忽略异常
        }
    }
}