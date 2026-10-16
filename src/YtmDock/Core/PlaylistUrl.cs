using System.Text.RegularExpressions;

namespace YtmDock.Core;

/// <summary>유튜브 뮤직 재생목록 주소 다루기.</summary>
public static class PlaylistUrl
{
    private static readonly Regex ListRe = new(@"[?&]list=([A-Za-z0-9_-]+)", RegexOptions.Compiled);
    private static readonly Regex BareIdRe = new(@"^[A-Za-z0-9_-]{10,}$", RegexOptions.Compiled);

    /// <summary>
    /// playlist?list=ID (목록 페이지, 재생 안 됨) → watch?list=ID (첫 곡부터 큐 재생).
    /// 공유 링크의 si= 추적 파라미터는 버린다.
    /// </summary>
    public static string ToWatchUrl(string url)
    {
        var id = ExtractListId(url);
        return id is not null ? $"https://music.youtube.com/watch?list={id}" : url;
    }

    /// <summary>주소에서 list= 값을 꺼낸다. 주소 대신 ID 만 붙여 넣어도 받아 준다.</summary>
    public static string? ExtractListId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        var m = ListRe.Match(url);
        if (m.Success) return m.Groups[1].Value;
        return BareIdRe.IsMatch(url) ? url : null;
    }

    /// <summary>저장용 정규형 (si= 등 잡다한 파라미터 제거). 재생목록이 아니면 null.</summary>
    public static string? Normalize(string? url)
    {
        var id = ExtractListId(url);
        return id is null ? null : $"https://music.youtube.com/playlist?list={id}";
    }
}
