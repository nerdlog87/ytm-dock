using System.Text.RegularExpressions;

namespace YtmDock.Core;

/// <summary>
/// "평일 아침 08:00 이전에 켰다면 지정 플레이리스트를 바로 재생" 규칙.
/// 시간 비교만 한다. 실제 재생은 MainWindow 가 맡는다.
/// </summary>
public static class MorningRule
{
    /// <returns>지금 자동 재생해야 하면 재생용 URL, 아니면 null</returns>
    public static string? Resolve(Settings.MorningSettings m, DateTime now)
    {
        if (!m.Enabled || string.IsNullOrWhiteSpace(m.PlaylistUrl)) return null;
        if (m.WeekdaysOnly && now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return null;
        if (!TimeOnly.TryParse(m.Before, out var before)) before = new TimeOnly(8, 0);
        if (TimeOnly.FromDateTime(now) >= before) return null;
        return ToWatchUrl(m.PlaylistUrl);
    }

    /// <summary>
    /// playlist?list=ID (목록 페이지, 재생 안 됨) → watch?list=ID (첫 곡부터 큐 재생).
    /// 공유 링크의 si= 추적 파라미터는 버린다.
    /// </summary>
    public static string ToWatchUrl(string url)
    {
        var m = Regex.Match(url, @"[?&]list=([A-Za-z0-9_-]+)");
        return m.Success ? $"https://music.youtube.com/watch?list={m.Groups[1].Value}" : url;
    }

    public static string? ExtractListId(string? url)
    {
        if (url is null) return null;
        var m = Regex.Match(url, @"[?&]list=([A-Za-z0-9_-]+)");
        return m.Success ? m.Groups[1].Value : null;
    }
}
