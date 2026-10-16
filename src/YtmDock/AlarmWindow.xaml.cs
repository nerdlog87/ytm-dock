using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using YtmDock.Core;
// csproj 의 UseWindowsForms(트레이용) 때문에 WinForms 이름이 전역으로 들어와 겹친다 → WPF 쪽으로 못 박는다.
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MediaColor = System.Windows.Media.Color;

namespace YtmDock;

/// <summary>
/// 알람 목록 편집 창. 알람 한 개 = 카드 한 장(켜기 · 시각 · 요일 · 플레이리스트).
/// 편집은 창 안의 사본에서만 하고 '저장' 을 눌러야 settings.json 에 반영된다.
/// </summary>
public partial class AlarmWindow : Window
{
    private static readonly DayOfWeek[] DayOrder =
        { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
    private static readonly string[] DayLabels = { "월", "화", "수", "목", "금", "토", "일" };

    private readonly Settings _settings;
    private readonly Func<string?> _currentUrl;
    private readonly Action<string> _testPlay;
    private readonly List<Row> _rows = new();

    public bool Saved { get; private set; }

    private sealed class Row
    {
        public Settings.Alarm? Source;          // null 이면 새 알람
        public required Border Card;
        public required CheckBox On;
        public required TextBox Time;
        public required ToggleButton[] Days;
        public required TextBox Url;
    }

    public AlarmWindow(Settings settings, Func<string?> currentUrl, Action<string> testPlay)
    {
        _settings = settings;
        _currentUrl = currentUrl;
        _testPlay = testPlay;
        InitializeComponent();

        var cfg = settings.Alarms;
        ChkEnabled.IsChecked = cfg.Enabled;
        BoxCatchUp.Text = cfg.CatchUpMinutes.ToString();
        ChkSkip.IsChecked = cfg.SkipIfPlaying;
        ChkDefer.IsChecked = cfg.DeferWhileLocked;
        foreach (var a in cfg.Items.OrderBy(a => a.Time)) AddRow(a);
        UpdateEmpty();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        // 새 알람은 마지막 알람의 플레이리스트를 물려받는다 (보통 같은 목록을 시간만 달리 쓰니까)
        var lastUrl = _rows.LastOrDefault()?.Url.Text;
        var row = AddRow(null);
        if (!string.IsNullOrWhiteSpace(lastUrl)) row.Url.Text = lastUrl;
        else if (PlaylistUrl.Normalize(_currentUrl()) is { } cur) row.Url.Text = cur;
        UpdateEmpty();
        row.Time.Focus();
        row.Time.SelectAll();
    }

    private Row AddRow(Settings.Alarm? src)
    {
        var a = src ?? new Settings.Alarm();

        var on = new CheckBox { IsChecked = a.Enabled, ToolTip = "이 알람 켜기/끄기" };
        var time = new TextBox { Text = a.Time, Style = (Style)FindResource("TimeBox"), ToolTip = "24시간제 HH:mm (예: 07:30)" };

        var days = new ToggleButton[7];
        var dayPanel = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < 7; i++)
        {
            days[i] = new ToggleButton
            {
                Content = DayLabels[i],
                IsChecked = a.Days.Contains(DayOrder[i]),
                Style = (Style)FindResource("DayToggle"),
            };
            dayPanel.Children.Add(days[i]);
        }

        var del = new Button { Content = "✕", Style = (Style)FindResource("ClearBtn") };
        var test = new Button { Content = "▶ 테스트", Style = (Style)FindResource("SmallBtn"), ToolTip = "지금 이 플레이리스트를 재생해 보기" };

        var top = new DockPanel();
        DockPanel.SetDock(del, Dock.Right);
        top.Children.Add(del);
        top.Children.Add(on);
        top.Children.Add(time);
        top.Children.Add(dayPanel);

        var url = new TextBox { Text = a.PlaylistUrl ?? "", ToolTip = "유튜브 뮤직 재생목록 주소 (playlist?list=… 를 붙여 넣으세요)" };
        var useCurrent = new Button { Content = "지금 열린 목록", Style = (Style)FindResource("SmallBtn"), ToolTip = "YTM Dock 에 지금 열려 있는 재생목록으로 채우기" };
        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(test, Dock.Right);
        DockPanel.SetDock(useCurrent, Dock.Right);
        bottom.Children.Add(test);
        bottom.Children.Add(useCurrent);
        bottom.Children.Add(new TextBlock { Text = "플레이리스트", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 8, 0) });
        bottom.Children.Add(url);

        var card = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromRgb(0x26, 0x26, 0x26)),
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x38)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 8, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Child = new StackPanel { Children = { top, bottom } },
        };

        var row = new Row { Source = src, Card = card, On = on, Time = time, Days = days, Url = url };

        del.Click += (_, _) => { List.Children.Remove(card); _rows.Remove(row); UpdateEmpty(); };
        useCurrent.Click += (_, _) =>
        {
            if (PlaylistUrl.Normalize(_currentUrl()) is { } cur) { url.Text = cur; HideHint(); }
            else ShowHint("지금 YTM Dock 에 재생목록이 열려 있지 않아요. 전체 모드에서 플레이리스트를 연 뒤 다시 눌러 주세요.");
        };
        test.Click += (_, _) =>
        {
            if (PlaylistUrl.Normalize(url.Text) is { } u) { _testPlay(u); HideHint(); }
            else ShowHint("플레이리스트 주소가 올바르지 않아요. 주소에 list= 가 들어 있어야 해요.");
        };

        List.Children.Add(card);
        _rows.Add(row);
        return row;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(BoxCatchUp.Text.Trim(), out var catchUp) || catchUp < 1 || catchUp > 720)
        {
            ShowHint("'놓친 알람' 시간은 1~720 분 사이 숫자로 넣어 주세요.");
            return;
        }

        var now = DateTime.Now;
        var items = new List<Settings.Alarm>();
        for (var i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            var no = i + 1;

            var t = ParseTime(r.Time.Text);
            if (t is null) { ShowHint($"{no}번째 알람의 시각을 07:30 처럼 넣어 주세요."); r.Time.Focus(); return; }

            var days = DayOrder.Where((_, d) => r.Days[d].IsChecked == true).ToList();
            if (days.Count == 0) { ShowHint($"{no}번째 알람에 요일을 하나 이상 골라 주세요."); return; }

            var url = PlaylistUrl.Normalize(r.Url.Text);
            if (url is null) { ShowHint($"{no}번째 알람의 플레이리스트 주소가 올바르지 않아요. 주소에 list= 가 들어 있어야 해요."); r.Url.Focus(); return; }

            // 원본은 건드리지 않고 새 객체로 만든다 (중간 칸에서 검증이 실패해도 원래 설정이 그대로 남도록)
            var src = r.Source;
            var timeText = t.Value.ToString("HH:mm");
            var a = new Settings.Alarm
            {
                Enabled = r.On.IsChecked == true,
                Time = timeText,
                Days = days,
                PlaylistUrl = url,
                LastFired = src?.LastFired,
            };
            if (src is not null) a.Id = src.Id;
            var scheduleChanged = src is null || src.Time != timeText || !src.Days.OrderBy(x => x).SequenceEqual(days.OrderBy(x => x));
            // 새로 만들었거나 시각을 바꿨는데 오늘 그 시각이 이미 지났다면, 저장하자마자 '놓친 알람' 으로 울리지 않게
            if (scheduleChanged) AlarmScheduler.MarkPastToday(a, now);
            items.Add(a);
        }

        var cfg = _settings.Alarms;
        cfg.Enabled = ChkEnabled.IsChecked == true;
        cfg.CatchUpMinutes = catchUp;
        cfg.SkipIfPlaying = ChkSkip.IsChecked == true;
        cfg.DeferWhileLocked = ChkDefer.IsChecked == true;
        cfg.Items = items.OrderBy(a => a.Time).ToList();
        _settings.Save();
        Saved = true;
        Close();
    }

    /// <summary>"7:30", "07:30", "0730", "730" 모두 받는다.</summary>
    private static TimeOnly? ParseTime(string text)
    {
        text = text.Trim();
        if (TimeOnly.TryParseExact(text, new[] { "H:mm", "HH:mm" }, null, System.Globalization.DateTimeStyles.None, out var t)) return t;
        if (text.Length is 3 or 4 && text.All(char.IsDigit))
        {
            var h = int.Parse(text[..^2]);
            var m = int.Parse(text[^2..]);
            if (h < 24 && m < 60) return new TimeOnly(h, m);
        }
        return null;
    }

    private void UpdateEmpty() => EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    private void ShowHint(string text) { Hint.Text = text; Hint.Visibility = Visibility.Visible; }
    private void HideHint() => Hint.Visibility = Visibility.Collapsed;
}
