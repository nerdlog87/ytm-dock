using System.Runtime.InteropServices;
using System.Windows.Input;

namespace YtmDock.Core;

/// <summary>
/// 전역 핫키 — user32 RegisterHotKey. 다른 앱이 포커스를 가져도 동작한다.
/// 콤보 문자열 형식: "Ctrl+Alt+Space", "Ctrl+Alt+Right", "Win+Shift+M" …
/// 이미 다른 프로그램이 쓰는 조합이면 Register 가 false 를 돌려준다 (앱은 계속 돈다).
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    public const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0xD0C0;

    public HotkeyManager(IntPtr hwnd) => _hwnd = hwnd;

    /// <returns>등록 성공 여부. 실패 시 reason 에 사람이 읽을 메시지.</returns>
    public bool Register(string? combo, Action action, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(combo)) { reason = "비어 있음"; return false; }
        if (!TryParse(combo, out var mods, out var vk)) { reason = $"'{combo}' 형식을 해석할 수 없음"; return false; }

        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods | MOD_NOREPEAT, vk))
        {
            reason = $"'{combo}' 은(는) 다른 프로그램이 이미 사용 중";
            return false;
        }
        _actions[id] = action;
        return true;
    }

    /// <summary>WndProc 에서 WM_HOTKEY 를 받으면 호출. 처리했으면 true.</summary>
    public bool Handle(IntPtr wParam)
    {
        if (_actions.TryGetValue(wParam.ToInt32(), out var a)) { a(); return true; }
        return false;
    }

    private static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        foreach (var raw in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= MOD_CONTROL; continue;
                case "alt":               mods |= MOD_ALT;     continue;
                case "shift":             mods |= MOD_SHIFT;   continue;
                case "win" or "windows":  mods |= MOD_WIN;     continue;
            }
            // 키 이름은 WPF Key 열거형 그대로 (Space, Right, Left, M, F9, MediaPlayPause …)
            if (!Enum.TryParse<Key>(raw, ignoreCase: true, out var key)) return false;
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        }
        return vk != 0;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }
}
