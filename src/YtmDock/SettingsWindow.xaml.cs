using System.Windows;
using System.Windows.Input;
using YtmDock.Core;
// csproj 의 UseWindowsForms(트레이용) 때문에 System.Windows.Forms 가 전역 using 으로 들어와
// Button/TextBox/KeyEventArgs 가 WPF 것과 겹친다 → WPF 쪽으로 못 박는다.
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace YtmDock;

/// <summary>
/// 핫키 설정 창. 칸을 클릭하고 조합을 누르면 "Ctrl+Alt+Z" 형식 문자열로 기록한다.
/// 저장 시 settings.json 에 쓰고, MainWindow 가 핫키를 다시 등록한다.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    public bool Saved { get; private set; }

    public SettingsWindow(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        var h = settings.Hotkeys;
        BoxPrev.Text   = h.Prev          ?? "";
        BoxNext.Text   = h.Next          ?? "";
        BoxPlay.Text   = h.PlayPause     ?? "";
        BoxToggle.Text = h.ToggleVisible ?? "";
        BoxVolUp.Text  = h.VolumeUp      ?? "";
        BoxVolDown.Text= h.VolumeDown    ?? "";
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var box = (TextBox)sender;

        // Alt 조합은 Key.System 으로 들어오고 실제 키는 SystemKey 에 있다
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 수식키만 누른 상태면 아직 기록하지 않는다
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        if (key == Key.Escape) { Keyboard.ClearFocus(); return; }
        if (key is Key.Back or Key.Delete) { box.Text = ""; return; }

        var mods = Keyboard.Modifiers;
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        // 수식키 없는 일반 글자/숫자는 타이핑을 가로채므로 거부. 미디어 키·F키·Pause 등은 단독 허용.
        var standalone = key.ToString().StartsWith("Media", StringComparison.Ordinal)
                      || key is >= Key.F1 and <= Key.F24
                      || key is Key.Pause or Key.Scroll or Key.VolumeMute or Key.BrowserHome;
        if (parts.Count == 0 && !standalone)
        {
            ShowHint("글자·숫자 키는 Ctrl / Alt / Shift / Win 과 함께 눌러 주세요. (타이핑을 가로채지 않도록)");
            return;
        }

        parts.Add(key.ToString());
        box.Text = string.Join("+", parts);
        Hint.Visibility = Visibility.Collapsed;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TextBox box) box.Text = "";
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
    {
        var d = new Settings.HotkeySettings();
        BoxPrev.Text = d.Prev ?? ""; BoxNext.Text = d.Next ?? "";
        BoxPlay.Text = d.PlayPause ?? ""; BoxToggle.Text = d.ToggleVisible ?? "";
        BoxVolUp.Text = d.VolumeUp ?? ""; BoxVolDown.Text = d.VolumeDown ?? "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var values = new[] { BoxPrev.Text, BoxNext.Text, BoxPlay.Text, BoxToggle.Text, BoxVolUp.Text, BoxVolDown.Text }.Where(v => v.Length > 0).ToList();
        if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
        {
            ShowHint("같은 조합이 두 군데에 들어가 있어요.");
            return;
        }

        var h = _settings.Hotkeys;
        h.Prev          = Nullify(BoxPrev.Text);
        h.Next          = Nullify(BoxNext.Text);
        h.PlayPause     = Nullify(BoxPlay.Text);
        h.ToggleVisible = Nullify(BoxToggle.Text);
        h.VolumeUp      = Nullify(BoxVolUp.Text);
        h.VolumeDown    = Nullify(BoxVolDown.Text);
        _settings.Save();
        Saved = true;
        Close();
    }

    private static string? Nullify(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void ShowHint(string text) { Hint.Text = text; Hint.Visibility = Visibility.Visible; }
}
