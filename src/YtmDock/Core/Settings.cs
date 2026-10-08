using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YtmDock.Core;

/// <summary>%AppData%\YtmDock\settings.json 모델. 모든 사용자 설정은 여기 한 곳에.</summary>
public sealed class Settings
{
    public static string Dir  => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YtmDock");
    public static string File => Path.Combine(Dir, "settings.json");

    public WindowSettings  Window  { get; set; } = new();
    public StartupSettings Startup { get; set; } = new();
    public HotkeySettings  Hotkeys { get; set; } = new();
    public MorningSettings Morning { get; set; } = new();
    public PeekSettings    Peek    { get; set; } = new();
    public string StartUrl { get; set; } = "https://music.youtube.com/";

    /// <summary>유튜브 뮤직 마크업이 바뀌어 미니 CSS 가 깨졌을 때, 코드 수정 없이 덮어쓰기 위한 칸.</summary>
    public string? CssOverride { get; set; }

    /// <summary>true 면 F12 개발자 도구 허용 (셀렉터 확인용)</summary>
    public bool Debug { get; set; } = false;

    public sealed class WindowSettings
    {
        /// <summary>null 이면 첫 실행 → 오른쪽 아래에 배치</summary>
        public double? X { get; set; }
        public double? Y { get; set; }
        /// <summary>true 면 드래그로 못 움직인다 (트레이 메뉴 '위치 고정')</summary>
        public bool Locked { get; set; } = false;
    }

    public sealed class StartupSettings
    {
        public bool RunAtLogin { get; set; } = false;
    }

    /// <summary>전역 핫키. 형식 "Ctrl+Alt+Space". null/빈 문자열이면 해당 핫키 없음.</summary>
    public sealed class HotkeySettings
    {
        /// <summary>
        /// 왼손 한 손 조합(엄지 Alt·새끼 Ctrl·검지 Z/X/C = 이전·다음·재생, 왼쪽부터 '뒤로·앞으로·토글'). 처음엔 Ctrl+Alt+Space 였는데 Claude 데스크톱 앱과
        /// 충돌했고, 다음 후보 Ctrl+Alt+P 는 양손이라 불편했다. 미디어 키가 있는 키보드라면
        /// "MediaPlayPause" / "MediaNextTrack" / "MediaPreviousTrack" 로 바꾸는 것을 추천.
        /// </summary>
        public string? Prev          { get; set; } = "Ctrl+Alt+Z";
        public string? Next          { get; set; } = "Ctrl+Alt+X";
        public string? PlayPause     { get; set; } = "Ctrl+Alt+C";
        public string? ToggleVisible { get; set; } = "Ctrl+Alt+M";
        public string? VolumeUp      { get; set; } = "Ctrl+Alt+Up";
        public string? VolumeDown    { get; set; } = "Ctrl+Alt+Down";
        /// <summary>핫키 한 번에 움직이는 슬라이더 칸 수 (0~100 기준)</summary>
        public int VolumeStep { get; set; } = 5;
    }

    /// <summary>숨겨 둔 상태에서 곡이 바뀌면 미니 플레이어를 잠깐 보여줬다 다시 숨긴다.</summary>
    public sealed class PeekSettings
    {
        public bool Enabled { get; set; } = true;
        public int Seconds { get; set; } = 4;
    }

    /// <summary>아침 자동 재생: 시작 시각이 Before 이전(기본 평일만)이면 PlaylistUrl 을 바로 재생.</summary>
    public sealed class MorningSettings
    {
        public bool Enabled { get; set; } = false;
        public bool WeekdaysOnly { get; set; } = true;
        /// <summary>"HH:mm"</summary>
        public string Before { get; set; } = "08:00";
        /// <summary>playlist?list=… 또는 watch?list=… 아무거나. 트레이 메뉴로 지정 가능.</summary>
        public string? PlaylistUrl { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Settings Load()
    {
        try
        {
            if (System.IO.File.Exists(File))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(File), JsonOpts) ?? new Settings();
                // v0.1 빌드 중 바뀐 옛 기본값들은 한 번만 새 기본값으로 교체 (사용자가 직접 정한 값은 건드리지 않음)
                var h = loaded.Hotkeys;
                var wasOldTriple = IsOld(h.PlayPause, "Ctrl+Alt+Space", "Ctrl+Alt+P", "Ctrl+Alt+Z")
                                && IsOld(h.Prev, "Ctrl+Alt+Left", "Ctrl+Alt+X")
                                && IsOld(h.Next, "Ctrl+Alt+Right", "Ctrl+Alt+C");
                if (wasOldTriple) { h.Prev = "Ctrl+Alt+Z"; h.Next = "Ctrl+Alt+X"; h.PlayPause = "Ctrl+Alt+C"; }
                return loaded;
            }
        }
        catch { /* 깨진 설정 파일은 무시하고 기본값으로 */ }
        return new Settings();
    }

    private static bool IsOld(string? v, params string[] olds) =>
        v is not null && olds.Any(o => string.Equals(v, o, StringComparison.OrdinalIgnoreCase));

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* 저장 실패는 치명적이지 않다 */ }
    }
}
