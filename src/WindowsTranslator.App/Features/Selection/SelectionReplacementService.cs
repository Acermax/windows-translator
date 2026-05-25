using WindowsTranslator.App.Infrastructure.Windows;

namespace WindowsTranslator.App.Features.Selection;

internal sealed class SelectionReplacementService
{
    public async Task ReplaceSelectionAsync(
        IntPtr targetWindowHandle,
        string replacementText,
        CancellationToken cancellationToken)
    {
        if (targetWindowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Original window is no longer available.");
        }

        if (string.IsNullOrWhiteSpace(replacementText))
        {
            throw new InvalidOperationException("There is no replacement text.");
        }

        System.Windows.Clipboard.SetText(replacementText);

        if (!NativeMethods.SetForegroundWindow(targetWindowHandle))
        {
            throw new InvalidOperationException("Could not focus the original window.");
        }

        await Task.Delay(120, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        SendCtrlV();
    }

    private static void SendCtrlV()
    {
        NativeMethods.keybd_event(NativeMethods.VirtualKey.Control, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VirtualKey.V, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VirtualKey.V, 0, NativeMethods.KeyEvent.KeyUp, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VirtualKey.Control, 0, NativeMethods.KeyEvent.KeyUp, UIntPtr.Zero);
    }
}
