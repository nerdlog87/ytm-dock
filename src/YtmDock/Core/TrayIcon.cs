using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YtmDock.Core;

/// <summary>
/// 트레이 아이콘 + 컨텍스트 메뉴. WPF 에는 트레이가 없어서 WinForms 의 NotifyIcon 을 쓴다 (csproj UseWindowsForms).
/// 아이콘 파일 없이 코드로 그린다 — 빨간 원 + 흰 재생 삼각형.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _miRunAtLogin;
    private readonly ToolStripMenuItem _miLock;
    private readonly ToolStripMenuItem _miAlarms;
    private readonly ToolStripMenuItem _miPeek;

    public event Action? ToggleVisibleRequested;
    public event Action? ToggleModeRequested;
    public event Action<bool>? RunAtLoginChanged;
    public event Action<bool>? LockChanged;
    public event Action<bool>? AlarmsChanged;
    public event Action<bool>? PeekChanged;
    public event Action? AlarmSettingsRequested;
    public event Action? OpenSettingsRequested;
    public event Action? HotkeySettingsRequested;
    public event Action? ExitRequested;

    public TrayIcon(bool runAtLogin, bool locked, bool alarms, bool peek)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("보이기 / 숨기기", null, (_, _) => ToggleVisibleRequested?.Invoke());
        menu.Items.Add("미니 ↔ 전체 전환", null, (_, _) => ToggleModeRequested?.Invoke());
        _miLock = new ToolStripMenuItem("위치 고정") { CheckOnClick = true, Checked = locked };
        _miLock.CheckedChanged += (_, _) => LockChanged?.Invoke(_miLock.Checked);
        menu.Items.Add(_miLock);
        _miPeek = new ToolStripMenuItem("곡 바뀔 때 잠깐 보여주기 (숨김 상태에서)") { CheckOnClick = true, Checked = peek };
        _miPeek.CheckedChanged += (_, _) => PeekChanged?.Invoke(_miPeek.Checked);
        menu.Items.Add(_miPeek);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("핫키 설정…", null, (_, _) => HotkeySettingsRequested?.Invoke());
        _miRunAtLogin = new ToolStripMenuItem("로그인 시 자동 실행") { CheckOnClick = true, Checked = runAtLogin };
        _miRunAtLogin.CheckedChanged += (_, _) => RunAtLoginChanged?.Invoke(_miRunAtLogin.Checked);
        menu.Items.Add(_miRunAtLogin);
        menu.Items.Add(new ToolStripSeparator());

        _miAlarms = new ToolStripMenuItem("알람 자동 재생") { CheckOnClick = true, Checked = alarms };
        _miAlarms.CheckedChanged += (_, _) => AlarmsChanged?.Invoke(_miAlarms.Checked);
        menu.Items.Add(_miAlarms);
        menu.Items.Add("알람 설정…", null, (_, _) => AlarmSettingsRequested?.Invoke());
        menu.Items.Add("설정 파일 열기 (settings.json)", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon() ?? DrawIcon(),
            Text = "YTM Dock",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => ToggleVisibleRequested?.Invoke();
    }

    /// <summary>알람 설정 창에서 전체 스위치를 바꿨을 때 메뉴 체크 표시를 맞춘다.</summary>
    public bool AlarmsChecked { set { if (_miAlarms.Checked != value) _miAlarms.Checked = value; } }

    public void ShowBalloon(string text) => _icon.ShowBalloonTip(2500, "YTM Dock", text, ToolTipIcon.Info);

    /// <summary>exe 와 같은 Assets/icon.ico 를 리소스에서 읽는다. 실패하면 코드로 그린 아이콘.</summary>
    private static Icon? LoadIcon()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.Ordinal));
            if (name is null) return null;
            using var st = asm.GetManifestResourceStream(name)!;
            // 트레이는 DPI 에 따라 16/20/24px 를 쓴다. 32 를 고정으로 주면 셸이 축소해 뭉개진다 → 시스템 작은 아이콘 크기로
            var sz = SystemInformation.SmallIconSize;
            return new Icon(st, sz.Width, sz.Height);
        }
        catch { return null; }
    }

    private static Icon DrawIcon()
    {
        const int s = 32;
        using var bmp = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var red = new SolidBrush(Color.FromArgb(255, 0, 0));
            g.FillEllipse(red, 1, 1, s - 2, s - 2);
            var tri = new[] { new PointF(12, 8), new PointF(25, 16), new PointF(12, 24) };
            g.FillPolygon(Brushes.White, tri);
        }
        // Icon.FromHandle 은 핸들을 복사하지 않으므로 Clone 으로 독립 사본을 만든다
        var h = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(h).Clone(); }
        finally { DestroyIcon(h); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
