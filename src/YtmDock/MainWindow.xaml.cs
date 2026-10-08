using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using YtmDock.Core;

namespace YtmDock;

public partial class MainWindow : Window
{
    private readonly Settings _settings;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool _forceClose;
    private HotkeyManager? _hotkeys;
    private bool _morningPending;
    private readonly DispatcherTimer _peekTimer = new();
    private bool _peeking;

    // ── 등장/퇴장 애니메이션 ───────────────────────────────────────────────
    // 창 전체 알파(WS_EX_LAYERED)는 WebView2 가 무시한다 (2026-10-09 실측: Chromium 이 DirectComposition 으로 직접 그려
    // 부모 창 알파를 안 탄다). WPF Opacity 도 AllowsTransparency 가 필요해 불가.
    // → 창을 아래에서 위로 '슬라이드' + 페이지 안 내용은 CSS 로 '페이드'. 진짜 투명도/블러는 v0.2(네이티브 UI) 몫.
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    // Windows 11: DWM 에 창 모서리를 둥글게 해 달라고 요청. 창 내용(WebView2 포함)과 무관하게 OS 가 잘라 준다. Win10 은 무시됨.
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private IntPtr _hwnd;
    private const double SlidePx = 22;                 // 슬라이드 거리
    private const int ShowMs = 420, HideMs = 260;      // 등장/퇴장 시간 (처음 220/180 은 '휙' 느낌이라 늘림)
    private readonly DispatcherTimer _animTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double _animFrom, _animTo; private DateTime _animStart; private TimeSpan _animDur; private Action? _animDone; private bool _animEaseIn;
    private bool _animating;                           // 애니메이션 중 LocationChanged 로 위치 저장되는 것 방지
    private double _restTop;                           // 슬라이드의 기준(정지) Top
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _hover;

    /// <summary>핫키 등록 실패 등 사용자에게 알릴 일 (App 이 트레이 풍선으로 보여 준다)</summary>
    public event Action<string>? HotkeyProblem;

    public string? CurrentUrl => Web.CoreWebView2?.Source;

    // 미니(플레이어 바만) / 전체(원래 유튜브 뮤직) 창 크기
    private const double MiniW = 440, MiniH = 64;
    private const double FullW = 440, FullH = 720;

    // 미니 모드에서 WebView2 의 실제 폭. 유튜브 뮤직이 데스크톱 레이아웃을 유지하는 폭이어야 한다.
    // 1100 에서는 볼륨 버튼/슬라이더가 display:none 이고 1400 에서 나타난다 (2026-10-08 실측).
    private const double VirtualW = 1400;

    /// <summary>
    /// 구글이 "이 브라우저 또는 앱은 안전하지 않을 수 있습니다"로 로그인을 막을 때 true 로 바꾼다.
    /// WebView2 기본 UA 는 이미 Edg/ 토큰을 포함하므로 보통은 필요 없다 — 막힐 때만 켠다.
    /// (const 가 아니라 static readonly 인 이유: const false 면 컴파일러가 뒤의 if 를 '도달 불가'로 보고 CS0162 경고를 낸다)
    /// </summary>
    private static readonly bool SpoofEdgeUserAgent = false;

    private const string EdgeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0";

    private const int WM_NCLBUTTONDOWN   = 0x00A1;
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int WM_SYSCOMMAND      = 0x0112;
    private const int SC_MOVE            = 0xF010;

    /// <summary>위치 고정. 드래그(캡션 끌기)만 막고 더블클릭 토글은 살려 둔다.</summary>
    public bool IsLocked { get; set; }

    private bool _mini = true;

    public MainWindow(Settings settings)
    {
        _settings = settings;
        IsLocked = settings.Window.Locked;
        InitializeComponent();
        ApplyWindowSize();
        RestoreOrPlace();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;

        // 창을 끌 때마다 위치 저장 (드래그 중엔 수십 번 발생하므로 0.6초 디바운스)
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SavePosition(); };
        LocationChanged += (_, _) => { if (_animating) return; _saveTimer.Stop(); _saveTimer.Start(); };

        // 곡 바뀜 '잠깐 보여주기' 타이머: 마우스가 올라가 있으면 조금 더 기다린다
        _peekTimer.Tick += (_, _) =>
        {
            if (_hover) { _peekTimer.Interval = TimeSpan.FromSeconds(2); return; }
            _peekTimer.Stop();
            if (_peeking) { _peeking = false; SlideOutAndHide(); }
        };

        // 슬라이드 애니메이션 (ease-out). Top 만 움직인다.
        _animTimer.Tick += (_, _) =>
        {
            var t = Math.Min(1.0, (DateTime.UtcNow - _animStart).TotalMilliseconds / _animDur.TotalMilliseconds);
            // 등장: ease-out (빠르게 와서 부드럽게 멈춤) / 퇴장: ease-in (천천히 떠나기 시작해 가속하며 사라짐)
            // 퇴장에 ease-out 을 쓰면 첫 프레임에 확 움직인 뒤 질질 끌려 '걸렸다 내려가는' 느낌이 난다.
            var e = _animEaseIn ? t * t * t : 1 - Math.Pow(1 - t, 4);
            Top = _animFrom + (_animTo - _animFrom) * e;
            if (t >= 1.0) { _animTimer.Stop(); _animating = false; var d = _animDone; _animDone = null; d?.Invoke(); }
        };

        // 마우스 호버 감지. WebView2 가 마우스를 전부 가져가서 WPF IsMouseOver 가 안 먹는다 → 커서 좌표로 직접 판정
        _hoverTimer.Tick += (_, _) =>
        {
            if (!IsVisible || _hwnd == IntPtr.Zero) { _hover = false; return; }
            _hover = GetCursorPos(out var pt) && GetWindowRect(_hwnd, out var rc)
                     && pt.X >= rc.Left && pt.X < rc.Right && pt.Y >= rc.Top && pt.Y < rc.Bottom;
        };
        _hoverTimer.Start();

        // X(Alt+F4)는 '숨기기'. 진짜 종료는 트레이 메뉴에서만.
        Closing += (_, e) =>
        {
            if (_forceClose) { SavePosition(); return; }
            e.Cancel = true;
            Hide();
        };
    }

    public void ForceClose() { _forceClose = true; _hotkeys?.Dispose(); Close(); }

    public void ToggleVisible()
    {
        // 잠깐 보여주는 중에 사용자가 직접 토글하면 → 그냥 계속 보이게
        if (_peeking) { _peeking = false; _peekTimer.Stop(); return; }
        if (IsVisible) SlideOutAndHide();
        else ShowAnimated(activate: true);
    }

    /// <summary>아래에서 14px 올라오며 등장 + 내용 페이드 인.</summary>
    private void ShowAnimated(bool activate)
    {
        if (_animating) return;
        _restTop = Top;
        _animating = true;
        Top = _restTop + SlidePx;
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        if (activate) Activate();
        // 내용 페이드 인: 숨길 때 남겨 둔 hidden 클래스를 한 프레임 뒤에 푼다 (같은 프레임에 넣고 빼면 전환이 안 보인다)
        _ = Task.Delay(40).ContinueWith(_ => Dispatcher.Invoke(() => _ = ExecAsync("window.ytmDock?.setShown(true)")));
        Animate(_restTop, ShowMs);
    }

    /// <summary>내용 페이드 아웃 + 아래로 내려가며 숨김. 끝나면 원래 Top 복원.</summary>
    private void SlideOutAndHide()
    {
        if (_animating || !IsVisible) return;
        _restTop = Top;
        _animating = true;
        _ = ExecAsync("window.ytmDock?.setShown(false)");
        Animate(_restTop + SlidePx, HideMs, () =>
        {
            Hide();
            Top = _restTop;   // hidden 클래스는 그대로 둔다 → 다음 등장 때 페이드 인의 출발점이 된다
        }, easeIn: true);
    }

    private void Animate(double toTop, int ms, Action? done = null, bool easeIn = false)
    {
        _animEaseIn = easeIn;
        _animFrom = Top; _animTo = toTop;
        _animStart = DateTime.UtcNow; _animDur = TimeSpan.FromMilliseconds(ms); _animDone = done;
        _animTimer.Stop(); _animTimer.Start();
    }

    private void ApplyRoundedCorners()
    {
        if (_hwnd == IntPtr.Zero) return;
        var pref = DWMWCP_ROUND;
        try { DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); } catch { /* Win10 */ }
    }

    /// <summary>숨김 상태에서 곡이 바뀌었을 때: 포커스를 뺏지 않고 잠깐 보여줬다 숨긴다 (미니 모드일 때만).</summary>
    private void PeekForTrackChange()
    {
        if (!_settings.Peek.Enabled || !_mini) return;
        if (IsVisible && !_peeking) return;          // 이미 보이는 중이면 할 일 없음
        _peeking = true;
        if (!IsVisible) ShowAnimated(activate: false);   // ShowActivated=False 라 포커스 안 뺏음
        _peekTimer.Stop();
        _peekTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.Peek.Seconds, 1, 60));
        _peekTimer.Start();
    }

    /// <summary>
    /// --minimized 로 시작할 때. WebView2 는 창 HWND 가 생겨야 초기화되므로 아예 Show 를 안 하면
    /// 아침 자동 재생이 영영 시작되지 않는다 → 최소화 상태로 한 번 띄워 HWND 를 만들고 바로 숨긴다.
    /// </summary>
    public void StartHidden()
    {
        WindowState = WindowState.Minimized;
        Loaded += (_, _) => { Hide(); WindowState = WindowState.Normal; };
        Show();
    }

    /// <summary>저장된 위치가 있고 아직 화면 안이면 거기로, 아니면 오른쪽 아래로.</summary>
    private void RestoreOrPlace()
    {
        var w = _settings.Window;
        if (w.X is double x && w.Y is double y && IsOnScreen(x, y))
        {
            Left = x; Top = y;
        }
        else PlaceBottomRight();
    }

    private static bool IsOnScreen(double x, double y)
    {
        var va = SystemParameters.VirtualScreenLeft; var vt = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth; var vh = SystemParameters.VirtualScreenHeight;
        return x >= va - 50 && y >= vt - 50 && x < va + vw - 50 && y < vt + vh - 50;
    }

    private void SavePosition()
    {
        if (WindowState != WindowState.Normal || _animating) return;
        _settings.Window.X = Left;
        _settings.Window.Y = Top;
        _settings.Save();
    }

    /// <summary>드래그 영역(app-region: drag)은 '캡션'으로 취급되어 더블클릭이 비클라이언트 메시지로 온다 → 토글로 연결.</summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        _hwnd = source.Handle;
        ApplyRoundedCorners();
        RegisterHotkeys(source.Handle);
        source.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            switch (msg)
            {
                case HotkeyManager.WM_HOTKEY:
                    if (_hotkeys?.Handle(wParam) == true) handled = true;
                    break;

                case WM_NCLBUTTONDBLCLK:
                    ToggleMode();
                    handled = true;   // 기본 동작(최대화 시도) 막기
                    break;

                // app-region: drag 영역은 '캡션'이라 누르면 창 이동이 시작된다 → 고정 중엔 삼킨다
                case WM_NCLBUTTONDOWN when IsLocked:
                    handled = true;
                    break;

                // 키보드/시스템 메뉴 경로의 이동도 차단
                case WM_SYSCOMMAND when IsLocked && ((int)wParam & 0xFFF0) == SC_MOVE:
                    handled = true;
                    break;
            }
            return IntPtr.Zero;
        });
    }

    /// <summary>설정 창에서 저장한 뒤 호출 — 기존 등록을 풀고 새 조합으로 다시 등록.</summary>
    public void ReloadHotkeys()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        RegisterHotkeys(hwnd);
    }

    private void RegisterHotkeys(IntPtr hwnd)
    {
        _hotkeys?.Dispose();
        _hotkeys = new HotkeyManager(hwnd);
        var h = _settings.Hotkeys;
        var failed = new List<string>();
        void Reg(string? combo, Action a) { if (!string.IsNullOrWhiteSpace(combo) && !_hotkeys.Register(combo, a, out var why)) failed.Add(why); }

        Reg(h.PlayPause,     () => _ = ExecAsync("window.ytmDock?.playPause()"));
        Reg(h.Next,          () => _ = ExecAsync("window.ytmDock?.next()"));
        Reg(h.Prev,          () => _ = ExecAsync("window.ytmDock?.prev()"));
        Reg(h.ToggleVisible, ToggleVisible);
        var step = Math.Clamp(h.VolumeStep, 1, 50);
        Reg(h.VolumeUp,   () => _ = ExecAsync($"window.ytmDock?.volume({step})"));
        Reg(h.VolumeDown, () => _ = ExecAsync($"window.ytmDock?.volume(-{step})"));

        if (failed.Count > 0) HotkeyProblem?.Invoke("핫키 등록 실패: " + string.Join(" / ", failed));
    }

    private async Task ExecAsync(string js)
    {
        if (Web.CoreWebView2 is null) return;
        try { await Web.CoreWebView2.ExecuteScriptAsync(js); } catch { /* 페이지 전환 중 등 */ }
    }

    /// <summary>작업 표시줄을 피해서 화면 오른쪽 아래 모서리에 놓는다 (첫 실행).</summary>
    private void PlaceBottomRight()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 16;
        Top = wa.Bottom - Height - 16;
    }

    private void ApplyWindowSize()
    {
        Width = _mini ? MiniW : FullW;
        Height = _mini ? MiniH : FullH;
        Web.Width = _mini ? VirtualW : FullW;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 로그인 쿠키가 남으려면 userDataFolder 를 고정해야 한다.
            var profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YtmDock", "profile");
            Directory.CreateDirectory(profileDir);

            // 사용자 제스처 없는 자동 재생 허용 — 6단계(아침 규칙)의 전제. 처음부터 넣어 둔다.
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required");

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: profileDir,
                options: options);

            await Web.EnsureCoreWebView2Async(env);

            var core = Web.CoreWebView2;
            var s = core.Settings;
            s.AreDefaultContextMenusEnabled = false;
            s.IsStatusBarEnabled = false;
            s.AreDevToolsEnabled = _settings.Debug;   // settings.json 의 "debug": true 면 F12 허용
            s.IsZoomControlEnabled = false;
            if (SpoofEdgeUserAgent) s.UserAgent = EdgeUserAgent;

            // CSS 의 app-region: drag 로 창을 끌 수 있게 한다 (WebView2 1.0.2420+).
            s.IsNonClientRegionSupportEnabled = true;

            // 구글 로그인 등이 새 창을 요구하면 같은 뷰 안에서 연다 (팝업 창 금지).
            core.NewWindowRequested += (_, a) =>
            {
                a.Handled = true;
                core.Navigate(a.Uri);
            };

            // 페이지(재)로드 때마다 CSS + 브리지 스크립트 주입
            var css = _settings.CssOverride ?? ReadResource("Web.ytm-mini.css");
            var js = ReadResource("Web.ytm-bridge.js");
            await core.AddScriptToExecuteOnDocumentCreatedAsync(
                $"window.__YTM_DOCK_CSS__ = {JsonSerializer.Serialize(css)};\n{js}");

            // 리로드 후에도 현재 미니/전체 상태를 유지 + 아침 자동 재생 마무리
            core.NavigationCompleted += async (_, _) =>
            {
                await ApplyModeToPageAsync();
                if (_morningPending)
                {
                    _morningPending = false;
                    // watch?list= 는 보통 알아서 재생되지만, 자동 재생이 막혔으면 재생 버튼을 한 번 눌러 준다
                    await Task.Delay(3000);
                    await ExecAsync("(() => { const v = document.querySelector('video'); if (!v || v.paused) document.querySelector('#play-pause-button')?.click(); })()");
                }
            };

            // 페이지의 ⤢ 버튼 클릭 → 토글
            core.WebMessageReceived += (_, a) =>
            {
                var msg = a.TryGetWebMessageAsString();
                if (msg == "toggle") ToggleMode();
                else if (msg is not null && msg.StartsWith("track:", StringComparison.Ordinal)) PeekForTrackChange();
            };

            var morningUrl = MorningRule.Resolve(_settings.Morning, DateTime.Now);
            _morningPending = morningUrl is not null;
            core.Navigate(morningUrl ?? _settings.StartUrl);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"WebView2 초기화 실패:\n{ex.Message}\n\nWebView2 런타임이 설치되어 있는지 확인하세요.",
                "YTM Dock", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    public void ToggleMode()
    {
        if (_peeking) { _peeking = false; _peekTimer.Stop(); }
        _mini = !_mini;

        // 오른쪽 아래 모서리를 고정한 채 크기만 바꾼다
        var right = Left + Width;
        var bottom = Top + Height;
        ApplyWindowSize();
        Left = right - Width;
        Top = bottom - Height;

        _ = ApplyModeToPageAsync();
    }

    private async Task ApplyModeToPageAsync()
    {
        if (Web.CoreWebView2 is null) return;
        await Web.CoreWebView2.ExecuteScriptAsync(
            $"window.ytmDock && window.ytmDock.applyMini({(_mini ? "true" : "false")});");
    }

    private static string ReadResource(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var full = asm.GetManifestResourceNames()
            .First(n => n.EndsWith(name, StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(full)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
