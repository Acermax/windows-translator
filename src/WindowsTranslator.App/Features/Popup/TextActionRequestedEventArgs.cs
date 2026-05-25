using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Popup;

public sealed class TextActionRequestedEventArgs(TextProcessingAction action) : EventArgs
{
    public TextProcessingAction Action { get; } = action;
}
