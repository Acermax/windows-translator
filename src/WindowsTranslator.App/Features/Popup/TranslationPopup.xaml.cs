using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowsTranslator.Core.Translation;

namespace WindowsTranslator.App.Features.Popup;

public partial class TranslationPopup : Window
{
    public TranslationPopup()
    {
        InitializeComponent();
    }

    public event EventHandler? OptionsRequested;

    public void SetLoading(string sourceText)
    {
        StatusTextBlock.Text = "Traduciendo...";
        OriginalTextBlock.Text = BuildPreview(sourceText);
        TranslationTextBox.Text = string.Empty;
        CopyButton.IsEnabled = false;
    }

    public void SetResult(TranslationResult result)
    {
        StatusTextBlock.Text = $"Traduccion lista ({result.Duration.TotalSeconds:0.0}s)";
        TranslationTextBox.Text = result.Text;
        CopyButton.IsEnabled = true;
    }

    public void SetError(string message)
    {
        StatusTextBlock.Text = "Error";
        OriginalTextBlock.Text = string.Empty;
        TranslationTextBox.Text = message;
        CopyButton.IsEnabled = false;
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
