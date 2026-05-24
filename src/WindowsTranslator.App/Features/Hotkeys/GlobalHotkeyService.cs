using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using WindowsTranslator.App.Infrastructure.Windows;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.App.Features.Hotkeys;

internal sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x5452;
    private HwndSource? _source;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public void Register(HotkeySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        Unregister();

        _source = new HwndSource(new HwndSourceParameters("WindowsTranslatorHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        });
        _source.AddHook(WndProc);

        var modifiers = HotkeyConverter.ToModifiers(settings);
        var virtualKey = HotkeyConverter.ToVirtualKey(settings);
        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifiers, virtualKey))
        {
            var error = new Win32Exception(MarshalGetLastWin32Error());
            DisposeSource();
            throw new InvalidOperationException($"Could not register hotkey {HotkeyDisplayFormatter.Format(settings)}: {error.Message}", error);
        }

        _registered = true;
    }

    public void Dispose()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (_registered && _source is not null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        DisposeSource();
    }

    private void DisposeSource()
    {
        if (_source is null)
        {
            return;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    private static int MarshalGetLastWin32Error() => System.Runtime.InteropServices.Marshal.GetLastWin32Error();

    private static class HotkeyConverter
    {
        private const uint Alt = 0x0001;
        private const uint Control = 0x0002;
        private const uint Shift = 0x0004;
        private const uint Win = 0x0008;
        private const uint NoRepeat = 0x4000;

        public static uint ToModifiers(HotkeySettings settings)
        {
            uint modifiers = NoRepeat;
            if (settings.Alt)
            {
                modifiers |= Alt;
            }

            if (settings.Ctrl)
            {
                modifiers |= Control;
            }

            if (settings.Shift)
            {
                modifiers |= Shift;
            }

            if (settings.Win)
            {
                modifiers |= Win;
            }

            return modifiers;
        }

        public static uint ToVirtualKey(HotkeySettings settings)
        {
            if (!Enum.TryParse<Key>(settings.Key, ignoreCase: true, out var key) || key == Key.None)
            {
                throw new InvalidOperationException($"Invalid hotkey key: {settings.Key}");
            }

            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (virtualKey == 0)
            {
                throw new InvalidOperationException($"Invalid hotkey key: {settings.Key}");
            }

            return (uint)virtualKey;
        }
    }
}
