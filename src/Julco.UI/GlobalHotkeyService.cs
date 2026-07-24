using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Julco.UI;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly Dictionary<int, HotkeyDefinition> _hotkeys = new();
    private HwndSource? _source;
    private IntPtr _handle;

    public void Register(Window owner, IEnumerable<HotkeyDefinition> hotkeys, Action<string> reportStatus)
    {
        if (_source is not null)
        {
            return;
        }

        _handle = new WindowInteropHelper(owner).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
        _hotkeys.Clear();
        var failures = new List<string>();

        foreach (var hotkey in hotkeys)
        {
            _hotkeys[hotkey.Id] = hotkey;
            if (!RegisterHotKey(_handle, hotkey.Id, ToNativeModifiers(hotkey.Modifiers), (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key)))
            {
                failures.Add(hotkey.DisplayText);
            }
        }

        reportStatus(failures.Count == 0
            ? "Global shortcuts ready: Ctrl+Alt+Shift+L lens, C capture, Right tab, D DOM, S CSS, I images."
            : $"Some global shortcuts are already in use: {string.Join(", ", failures)}.");
    }

    public void Dispose()
    {
        if (_source is null)
        {
            return;
        }

        foreach (var hotkey in _hotkeys.Values)
        {
            UnregisterHotKey(_handle, hotkey.Id);
        }

        _source.RemoveHook(WndProc);
        _source = null;
        _handle = IntPtr.Zero;
        _hotkeys.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _hotkeys.TryGetValue(wParam.ToInt32(), out var hotkey))
        {
            handled = true;
            hotkey.Action();
        }

        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint value = ModNoRepeat;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            value |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            value |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            value |= ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            value |= ModWin;
        }

        return value;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
