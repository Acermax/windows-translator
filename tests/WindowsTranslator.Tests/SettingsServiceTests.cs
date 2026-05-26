using System.Text.Json;
using WindowsTranslator.Core.Settings;

namespace WindowsTranslator.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_CreatesDefaultSettings_WhenFileDoesNotExist()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(settingsPath);

        var settings = await service.LoadAsync();

        Assert.True(File.Exists(settingsPath));
        Assert.Equal("http://localhost:8000/v1/chat/completions", settings.Translation.Endpoint);
        Assert.Equal("Z", settings.Hotkey.Key);
        Assert.True(settings.Hotkey.Ctrl);
        Assert.True(settings.Hotkey.Shift);
        Assert.Equal("UIAutomation", settings.Capture.Method);
        Assert.Equal(50, settings.Capture.FocusSettleDelayMs);
        Assert.Equal(1_500, settings.Capture.MaxAutomationElements);
        Assert.False(settings.Capture.UseClipboardFallback);
        Assert.Equal(160, settings.Capture.ClipboardFallbackDelayMs);
        Assert.Equal(0.6, settings.Translation.Temperature);
        Assert.Equal(0.95, settings.Translation.TopP);
        Assert.Equal(20, settings.Translation.TopK);
        Assert.Equal(0.0, settings.Translation.MinP);
        Assert.Equal(0.0, settings.Translation.PresencePenalty);
        Assert.Equal(1.0, settings.Translation.RepetitionPenalty);
        Assert.False(settings.Translation.IncludeReasoning);
        Assert.False(settings.Translation.EnableThinking);
    }

    [Fact]
    public async Task SaveAsync_NormalizesAndPersistsSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(settingsPath);
        var settings = AppSettings.CreateDefault();
        settings.Translation.Endpoint = "  http://localhost:8001/v1/chat/completions  ";
        settings.Translation.Model = "  test-model  ";
        settings.Translation.Temperature = 5;
        settings.Translation.TopP = 2;
        settings.Translation.TopK = -1;
        settings.Translation.MinP = -1;
        settings.Translation.PresencePenalty = 5;
        settings.Translation.RepetitionPenalty = 0;
        settings.Translation.IncludeReasoning = true;
        settings.Translation.EnableThinking = true;
        settings.Translation.MaxOutputTokens = 40_000;
        settings.Hotkey.Key = " z ";
        settings.Capture.Method = "  UIAutomation  ";
        settings.Capture.FocusSettleDelayMs = -1;
        settings.Capture.MaxAutomationElements = 5;
        settings.Capture.UseClipboardFallback = true;
        settings.Capture.ClipboardFallbackDelayMs = 10_000;

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("http://localhost:8001/v1/chat/completions", loaded.Translation.Endpoint);
        Assert.Equal("test-model", loaded.Translation.Model);
        Assert.Equal(2, loaded.Translation.Temperature);
        Assert.Equal(1, loaded.Translation.TopP);
        Assert.Equal(0, loaded.Translation.TopK);
        Assert.Equal(0, loaded.Translation.MinP);
        Assert.Equal(2, loaded.Translation.PresencePenalty);
        Assert.Equal(0.01, loaded.Translation.RepetitionPenalty);
        Assert.True(loaded.Translation.IncludeReasoning);
        Assert.True(loaded.Translation.EnableThinking);
        Assert.Equal(32_768, loaded.Translation.MaxOutputTokens);
        Assert.Equal("Z", loaded.Hotkey.Key);
        Assert.Equal("UIAutomation", loaded.Capture.Method);
        Assert.Equal(0, loaded.Capture.FocusSettleDelayMs);
        Assert.Equal(10, loaded.Capture.MaxAutomationElements);
        Assert.True(loaded.Capture.UseClipboardFallback);
        Assert.Equal(2_000, loaded.Capture.ClipboardFallbackDelayMs);
    }

    [Fact]
    public async Task LoadAsync_Throws_WhenSettingsJsonIsInvalid()
    {
        var settingsPath = CreateTempSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await File.WriteAllTextAsync(settingsPath, "{not-json");
        var service = new SettingsService(settingsPath);

        await Assert.ThrowsAsync<JsonException>(() => service.LoadAsync());
    }

    private static string CreateTempSettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), "WindowsTranslator.Tests", Guid.NewGuid().ToString("N"), "settings.json");
    }
}
