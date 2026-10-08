using System.IO;
using Microsoft.Win32;

namespace YtmDock.Core;

/// <summary>로그인 시 자동 실행 — HKCU\Software\Microsoft\Windows\CurrentVersion\Run. 관리자 권한 불필요.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "YtmDock";

    // 단일 파일(publish) 앱에서는 Assembly.Location 이 빈 문자열이라(IL3000) ProcessPath → BaseDirectory 순으로 구한다
    private static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "YtmDock.exe");

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string s && s.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
            key.SetValue(ValueName, $"\"{ExePath}\" --minimized");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
