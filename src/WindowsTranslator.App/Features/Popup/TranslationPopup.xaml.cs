using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Popup;

public partial class TranslationPopup : Window
{
    private bool _hasSourceText;

    public TranslationPopup()
    {
        InitializeComponent();
    }

    public event EventHandler? OptionsRequested;
    public event EventHandler<TextActionRequestedEventArgs>? TextActionRequested;
    public event EventHandler? ReplaceRequested;

    public void SetLoading(string sourceText, TextProcessingAction action)
    {
        _hasSourceText = true;
        StatusTextBlock.Text = $"{GetActionLabel(action)}...";
        OriginalTextBlock.Text = BuildPreview(sourceText);
        TranslationTextBox.Text = string.Empty;
        CopyButton.IsEnabled = false;
        ReplaceButton.IsEnabled = false;
        SetActionButtonsEnabled(false);
    }

    public void SetResult(TranslationResult result, TextProcessingAction action)
    {
        StatusTextBlock.Text = $"{GetActionLabel(action)} listo ({result.Duration.TotalSeconds:0.0}s)";
        TranslationTextBox.Text = result.Text;
        CopyButton.IsEnabled = true;
        ReplaceButton.IsEnabled = true;
        SetActionButtonsEnabled(true);
    }

    public void SetError(string message, bool canRunActions)
    {
        _hasSourceText = canRunActions;
        StatusTextBlock.Text = "Error";
        if (!canRunActions)
        {
            OriginalTextBlock.Text = string.Empty;
        }

        TranslationTextBox.Text = message;
        CopyButton.IsEnabled = false;
        ReplaceButton.IsEnabled = false;
        SetActionButtonsEnabled(canRunActions);
    }

    private static string BuildPreview(string text)
    {
        var compact = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= 240 ? compact : compact[..240] + "...";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TranslationTextBox.Text))
        {
            System.Windows.Clipboard.SetText(TranslationTextBox.Text);
        }
    }

    private void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        RequestTextAction(TextProcessingAction.Translate);
    }

    private void ReverseButton_Click(object sender, RoutedEventArgs e)
    {
        RequestTextAction(TextProcessingAction.TranslateToEnglish);
    }

    private void GrammarButton_Click(object sender, RoutedEventArgs e)
    {
        RequestTextAction(TextProcessingAction.CorrectGrammar);
    }

    private void ImproveButton_Click(object sender, RoutedEventArgs e)
    {
        RequestTextAction(TextProcessingAction.ImproveWriting);
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        ReplaceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        OptionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HeaderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse button state changed during event dispatch.
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
        }
    }

    private void RequestTextAction(TextProcessingAction action)
    {
        if (!_hasSourceText)
        {
            return;
        }

        TextActionRequested?.Invoke(this, new TextActionRequestedEventArgs(action));
    }

    private void SetActionButtonsEnabled(bool isEnabled)
    {
        TranslateButton.IsEnabled = isEnabled;
        ReverseButton.IsEnabled = isEnabled;
        GrammarButton.IsEnabled = isEnabled;
        ImproveButton.IsEnabled = isEnabled;
    }

    private static string GetActionLabel(TextProcessingAction action)
    {
        return action switch
        {
            TextProcessingAction.Translate => "Traduccion",
            TextProcessingAction.TranslateToEnglish => "A ingles",
            TextProcessingAction.CorrectGrammar => "Gramatica",
            TextProcessingAction.ImproveWriting => "Mejora",
            _ => "Texto"
        };
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
