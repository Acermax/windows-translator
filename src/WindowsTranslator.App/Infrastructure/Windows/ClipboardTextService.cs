using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace WindowsTranslator.App.Infrastructure.Windows;

internal static class ClipboardTextService
{
    private static HwndSource? _ownerSource;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(80),
        TimeSpan.FromMilliseconds(160),
        TimeSpan.FromMilliseconds(320)
    ];

    public static async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("There is no text to copy.");
        }

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var error = TrySetText(text);
            if (error is null)
            {
                return;
            }

            if (attempt == RetryDelays.Length)
            {
                throw new InvalidOperationException(BuildClipboardBusyMessage(error.NativeErrorCode), error);
            }

            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }
    }

    public static async Task<string?> CopySelectedTextAsync(
        TimeSpan copyDelay,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        var previousText = TryGetTextRaw();

        try
        {
            SendCtrlC();
            await Task.Delay(copyDelay, cancellationToken);

            for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = TryGetText(maxCharacters);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }

            return null;
        }
        finally
        {
            await TryRestoreTextAsync(previousText);
        }
    }

    private static Win32Exception? TrySetText(string text)
    {
        var ownerWindowHandle = GetOwnerWindowHandle();
        if (!NativeMethods.OpenClipboard(ownerWindowHandle))
        {
            return new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            SetUnicodeText(text);
            return null;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static async Task TryRestoreTextAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                await SetTextAsync(text, CancellationToken.None);
                return;
            }
            catch (InvalidOperationException)
            {
            }

            if (attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt]);
            }
        }
    }

    private static string? TryGetTextRaw()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)
                ? System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText)
                : null;
        }
        catch (ExternalException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ThreadStateException)
        {
            return null;
        }
    }

    private static string? TryGetText(int maxCharacters)
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText))
            {
                return null;
            }

            var text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text.Length <= maxCharacters ? text : text[..maxCharacters];
        }
        catch (ExternalException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ThreadStateException)
        {
            return null;
        }
    }

    private static void SendCtrlC()
    {
        NativeMethods.keybd_event(NativeMethods.VirtualKey.Control, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VirtualKey.C, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VirtualKey.C, 0, NativeMethods.KeyEvent.KeyUp, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VirtualKey.Control, 0, NativeMethods.KeyEvent.KeyUp, UIntPtr.Zero);
    }

    private static IntPtr GetOwnerWindowHandle()
    {
        _ownerSource ??= new HwndSource(new HwndSourceParameters("WindowsTranslatorClipboardOwner")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        });

        return _ownerSource.Handle;
    }

    private static void SetUnicodeText(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        var memoryHandle = NativeMethods.GlobalAlloc(
            NativeMethods.GmemMoveable | NativeMethods.GmemZeroInit,
            new UIntPtr((uint)bytes.Length));
        if (memoryHandle == IntPtr.Zero)
        {
            throw CreateWin32InvalidOperation("No se pudo reservar memoria para el portapapeles.");
        }

        try
        {
            var memoryPointer = NativeMethods.GlobalLock(memoryHandle);
            if (memoryPointer == IntPtr.Zero)
            {
                throw CreateWin32InvalidOperation("No se pudo bloquear la memoria del portapapeles.");
            }

            try
            {
                Marshal.Copy(bytes, 0, memoryPointer, bytes.Length);
            }
            finally
            {
                NativeMethods.GlobalUnlock(memoryHandle);
            }

            if (!NativeMethods.EmptyClipboard())
            {
                throw CreateWin32InvalidOperation("No se pudo vaciar el portapapeles.");
            }

            if (NativeMethods.SetClipboardData(NativeMethods.CfUnicodeText, memoryHandle) == IntPtr.Zero)
            {
                throw CreateWin32InvalidOperation("No se pudo escribir texto Unicode en el portapapeles.");
            }

            memoryHandle = IntPtr.Zero;
        }
        finally
        {
            if (memoryHandle != IntPtr.Zero)
            {
                NativeMethods.GlobalFree(memoryHandle);
            }
        }
    }

    private static InvalidOperationException CreateWin32InvalidOperation(string message)
    {
        var error = new Win32Exception(Marshal.GetLastWin32Error());
        return new InvalidOperationException($"{message} {error.Message}", error);
    }

    private static string BuildClipboardBusyMessage(int nativeErrorCode)
    {
        var owner = TryGetOpenClipboardOwner();
        var error = new Win32Exception(nativeErrorCode);
        if (owner is null)
        {
            return $"El portapapeles esta ocupado, pero Windows no informa que proceso lo tiene abierto. Win32: {nativeErrorCode} ({error.Message}).";
        }

        return $"El portapapeles esta ocupado por {owner}. Win32: {nativeErrorCode} ({error.Message}).";
    }

    private static string? TryGetOpenClipboardOwner()
    {
        var windowHandle = NativeMethods.GetOpenClipboardWindow();
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        var processName = GetProcessName(processId);
        var windowTitle = GetWindowTitle(windowHandle);

        return (processName, windowTitle) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{processName} (PID {processId}, ventana \"{windowTitle}\")",
            ({ Length: > 0 }, _) => $"{processName} (PID {processId})",
            (_, { Length: > 0 }) => $"la ventana \"{windowTitle}\"",
            _ => $"una ventana sin titulo (handle 0x{windowHandle.ToInt64():X})"
        };
    }

    private static string? GetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? GetWindowTitle(IntPtr windowHandle)
    {
        var title = new StringBuilder(256);
        return NativeMethods.GetWindowText(windowHandle, title, title.Capacity) > 0
            ? title.ToString()
            : null;
    }
}
