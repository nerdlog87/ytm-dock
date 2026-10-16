using System.Windows;
using YtmDock.Core;

namespace YtmDock;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private TrayIcon? _tray;
    private MainWindow? _win;

    public Settings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 단일 인스턴스. 두 번째 실행은 조용히 종료 (트레이에 이미 있으니까).
        _mutex = new Mutex(initiallyOwned: true, "YtmDock_SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        Settings = Settings.Load();

        // 레지스트리의 실제 상태를 설정보다 우선 (사용자가 작업 관리자에서 껐을 수도 있다)
        Settings.Startup.RunAtLogin = StartupManager.IsEnabled();

        _win = new MainWindow(Settings);

        _tray = new TrayIcon(Settings.Startup.RunAtLogin, Settings.Window.Locked, Settings.Alarms.Enabled, Settings.Peek.Enabled);
        _tray.PeekChanged += on => { Settings.Peek.Enabled = on; Settings.Save(); };
        _win.HotkeyProblem += msg => _tray.ShowBalloon(msg);
        _win.AlarmNotice += msg => _tray.ShowBalloon(msg);
        _tray.AlarmsChanged += on => { Settings.Alarms.Enabled = on; Settings.Save();
            if (on && Settings.Alarms.Items.Count == 0)
                _tray.ShowBalloon("아직 알람이 없어요. 트레이 → '알람 설정…'에서 시각과 플레이리스트를 추가해 주세요."); };
        _tray.AlarmSettingsRequested += () =>
        {
            var dlg = new AlarmWindow(Settings, () => _win.CurrentUrl, url => _win.PlayPlaylist(url));
            dlg.ShowDialog();
            if (!dlg.Saved) return;
            _tray.AlarmsChecked = Settings.Alarms.Enabled;
            var on = Settings.Alarms.Items.Count(a => a.Enabled);
            _tray.ShowBalloon(Settings.Alarms.Enabled && on > 0 ? $"알람 {on}개가 켜져 있어요." : "켜진 알람이 없어요.");
        };
        _tray.HotkeySettingsRequested += () =>
        {
            var dlg = new SettingsWindow(Settings);
            dlg.ShowDialog();
            if (dlg.Saved) { _win.ReloadHotkeys(); _tray.ShowBalloon("설정을 적용했어요."); }
        };
        _tray.OpenSettingsRequested += () =>
        {
            Settings.Save();   // 파일이 없으면 기본값으로 먼저 만들어 둔다
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{Settings.File}\"") { UseShellExecute = true }); }
            catch (Exception ex) { _tray.ShowBalloon($"열기 실패: {ex.Message}"); }
        };
        _tray.ToggleVisibleRequested += () => _win.ToggleVisible();
        _tray.ToggleModeRequested    += () => _win.ToggleMode();
        _tray.LockChanged            += on => { _win.IsLocked = on; Settings.Window.Locked = on; Settings.Save(); };
        _tray.RunAtLoginChanged      += on =>
        {
            try
            {
                StartupManager.SetEnabled(on);
                Settings.Startup.RunAtLogin = on;
                Settings.Save();
                _tray.ShowBalloon(on ? "로그인할 때 자동으로 실행됩니다." : "자동 실행을 껐습니다.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"시작 프로그램 등록 실패:\n{ex.Message}", "YTM Dock");
            }
        };
        _tray.ExitRequested += ExitApp;

        var startHidden = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startHidden) _win.Show();
        else
        {
            _win.StartHidden();
            _tray.ShowBalloon("트레이에서 실행 중입니다. 아이콘을 더블클릭하면 플레이어가 나타납니다.");
        }
    }

    public void ExitApp()
    {
        _win?.ForceClose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
