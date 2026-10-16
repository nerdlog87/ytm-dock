using System.IO;
using System.Text.Encodings.Web;
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
    public AlarmSettings   Alarms  { get; set; } = new();
    /// <summary>v0.1 의 '아침 자동 재생'. 읽을 때 Alarms 로 옮기고 null 로 비운다(파일에서 사라짐).</summary>
    public MorningSettings? Morning { get; set; }
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

    /// <summary>
    /// 알람 자동 재생. 정한 시각에 플레이리스트를 재생한다. 여러 개 등록 가능.
    /// PC 가 켜져 있으면 그 시각에, 꺼져 있거나 잠자고 있었다면 켜거나 깨운 순간(CatchUpMinutes 안일 때만) 울린다.
    /// </summary>
    public sealed class AlarmSettings
    {
        /// <summary>전체 스위치 (트레이 '알람 자동 재생')</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>알람 시각이 지난 뒤 이 시간 안에 PC 를 켜거나 깨우면 놓친 알람을 대신 울린다.</summary>
        public int CatchUpMinutes { get; set; } = 60;
        /// <summary>알람 시각에 이미 YTM Dock 에서 음악이 나오고 있으면 끊지 않는다.</summary>
        public bool SkipIfPlaying { get; set; } = true;
        /// <summary>true 면 화면이 잠겨 있는 동안은 미뤘다가 잠금을 풀 때 울린다 (CatchUpMinutes 안일 때만).</summary>
        public bool DeferWhileLocked { get; set; } = false;
        public List<Alarm> Items { get; set; } = new();
    }

    public sealed class Alarm
    {
        public static readonly DayOfWeek[] Weekdays = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        public static readonly DayOfWeek[] AllDays = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public bool Enabled { get; set; } = true;
        /// <summary>"HH:mm"</summary>
        public string Time { get; set; } = "08:00";
        public List<DayOfWeek> Days { get; set; } = Weekdays.ToList();
        /// <summary>playlist?list=… 또는 watch?list=… 아무거나</summary>
        public string? PlaylistUrl { get; set; }
        /// <summary>마지막으로 울린 '알람 날짜' (yyyy-MM-dd). 하루 한 번만 울리게 하는 장치.</summary>
        public string? LastFired { get; set; }
    }

    /// <summary>v0.1 설정 호환용. 시작 시각이 Before 이전이면 재생하던 규칙 → 이제는 Before 시각의 알람으로 옮긴다.</summary>
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
        Converters = { new JsonStringEnumConverter() },   // 요일을 "Monday" 처럼 사람이 읽을 수 있게
        // 기본 인코더는 "Ctrl+Alt+Z" 의 + 를 \u002B 로 바꿔 써서 손으로 고치기 불편하다
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Settings Load()
    {
        try
        {
            if (System.IO.File.Exists(File))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(File), JsonOpts) ?? new Settings();
                // (v0.1 개발 중엔 여기서 옛 기본 핫키 Ctrl+Alt+Left/Right/Space 를 Z/X/C 로 '바꿔 주는' 코드가 있었다.
                //  그런데 사용자가 일부러 그 조합을 고르면 실행할 때마다 되돌려 버리는 버그였다 → v0.1.1 에서 삭제.
                //  설정 이전은 '한 번만 일어나는 흔적'(예: Morning 이 있으면)이 있을 때만 한다.)

                // v0.1 '아침 자동 재생' → 알람 하나로 이전 ("08:00 이전에 켜면" 이 아니라 "08:00 에" 울리는 것으로 바뀜)
                if (loaded.Morning is { } m)
                {
                    if (loaded.Alarms.Items.Count == 0 && !string.IsNullOrWhiteSpace(m.PlaylistUrl))
                        loaded.Alarms.Items.Add(new Alarm
                        {
                            Enabled = m.Enabled,
                            Time = TimeOnly.TryParse(m.Before, out var t) ? t.ToString("HH:mm") : "08:00",
                            Days = (m.WeekdaysOnly ? Alarm.Weekdays : Alarm.AllDays).ToList(),
                            PlaylistUrl = m.PlaylistUrl,
                        });
                    loaded.Morning = null;
                    loaded.Save();
                }
                return loaded;
            }
        }
        catch { /* 깨진 설정 파일은 무시하고 기본값으로 */ }
        return new Settings();
    }

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
