namespace DanmakuClient.Models;

public class AppSettings {
    public string LastUrl { get; set; } = "https://dm.wybxc.cc/1234";
    public PaddingSettings Padding { get; set; } = new();
}

public class PaddingSettings {
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
}