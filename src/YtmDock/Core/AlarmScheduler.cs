using System.Windows.Threading;
using Microsoft.Win32;

namespace YtmDock.Core;

/// <summary>
/// 알람 자동 재생의 '언제 울릴까' 담당. 실제 재생은 MainWindow 가 한다.
///
/// v0.1 은 앱이 켜지는 순간 한 번만 "08:00 이전인가?" 를 봤다. 그래서 PC 를 켜 둔 채 아침을 맞거나
/// 절전에서 깨우면 아무 일도 없었다. 이제는 세 군데서 같은 판정(FindDue)을 한다.
///   ① 앱 시작 직후      — PC 를 새로 켰을 때
///   ② 15초마다 타이머   — PC 가 켜져 있을 때 정각에
///   ③ 절전 해제·잠금 해제 — 잠자던 PC 를 깨웠을 때 (타이머도 결국 돌지만, 깨자마자 바로 확인)
/// 놓친 알람은 CatchUpMinutes 안에서만 대신 울리고, 알람마다 하루 한 번만 울린다(LastFired).
/// </summary>
public sealed class AlarmScheduler : IDisposable
{
    private readonly Settings _settings;
    private readonly Dispatcher _ui;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _locked;

    /// <summary>울려야 할 알람. UI 스레드에서 발생.</summary>
    public event Action<Settings.Alarm>? Due;

    public AlarmScheduler(Settings settings)
    {
        _settings = settings;
        _ui = Dispatcher.CurrentDispatcher;
        _timer.Tick += (_, _) => Check();
        // SystemEvents 는 별도 스레드에서 올 수 있다 → UI 스레드로 넘긴다
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public void Start() => _timer.Start();

    /// <summary>지금 울려야 할 알람을 찾아 '울림' 으로 표시하고 돌려준다. 없으면 null.</summary>
    public Settings.Alarm? TakeDue(DateTime now)
    {
        var due = FindDue(_settings.Alarms, now);
        if (due.Count == 0) return null;

        // 동시에 여러 개가 걸리면(예: 07:30 과 08:00 을 둘 다 놓침) 가장 최근 것 하나만 울리고 나머지는 지나간 것으로 처리
        foreach (var (a, day) in due) a.LastFired = day.ToString("yyyy-MM-dd");
        _settings.Save();
        return due.MaxBy(d => d.Day + TimeOnly.Parse(d.Alarm.Time).ToTimeSpan()).Alarm;
    }

    /// <summary>순수 판정 함수. now 기준으로 '시각은 지났고, 늦어도 CatchUpMinutes 안이고, 그날 아직 안 울린' 알람들.</summary>
    public static List<(Settings.Alarm Alarm, DateTime Day)> FindDue(Settings.AlarmSettings cfg, DateTime now)
    {
        var result = new List<(Settings.Alarm, DateTime)>();
        if (!cfg.Enabled) return result;
        var window = TimeSpan.FromMinutes(Math.Max(1, cfg.CatchUpMinutes));

        foreach (var a in cfg.Items)
        {
            if (!a.Enabled || PlaylistUrl.ExtractListId(a.PlaylistUrl) is null) continue;
            if (!TimeOnly.TryParse(a.Time, out var t)) continue;

            // 어제 것도 본다: 23:50 알람을 00:20 에 깨웠을 때
            for (var back = 0; back <= 1; back++)
            {
                var day = now.Date.AddDays(-back);
                if (!a.Days.Contains(day.DayOfWeek)) continue;
                var at = day + t.ToTimeSpan();
                if (at > now || now - at > window) continue;
                if (a.LastFired == day.ToString("yyyy-MM-dd")) continue;
                result.Add((a, day));
                break;
            }
        }
        return result;
    }

    /// <summary>
    /// 알람을 새로 만들거나 시각을 바꿨을 때, 오늘 시각이 이미 지났다면 '오늘 울림' 으로 쳐 둔다.
    /// 안 그러면 08:30 에 08:00 알람을 만들자마자 놓친 알람으로 울려 버린다.
    /// </summary>
    public static void MarkPastToday(Settings.Alarm a, DateTime now)
    {
        a.LastFired = null;
        if (!TimeOnly.TryParse(a.Time, out var t)) return;
        if (a.Days.Contains(now.DayOfWeek) && now.Date + t.ToTimeSpan() <= now)
            a.LastFired = now.ToString("yyyy-MM-dd");
    }

    private void Check()
    {
        if (_settings.Alarms.DeferWhileLocked && _locked) return;   // 잠금 해제 때 다시 본다
        var a = TakeDue(DateTime.Now);
        if (a is not null) Due?.Invoke(a);
    }

    private void CheckLater(int ms) =>
        _ = Task.Delay(ms).ContinueWith(_ => _ui.BeginInvoke(Check));

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        // 깨어난 직후엔 네트워크가 아직 없을 수 있어 몇 초 기다린다
        if (e.Mode == PowerModes.Resume) CheckLater(8000);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                _ui.BeginInvoke(() => _locked = true);
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.SessionLogon:
                _ui.BeginInvoke(() => _locked = false);
                CheckLater(2000);
                break;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        // SystemEvents 는 static 이벤트라 해제하지 않으면 계속 붙잡힌다
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
