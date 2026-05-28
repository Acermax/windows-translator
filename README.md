# Windows Translator

Small Windows desktop translator for selected text. Select text in another app, press `Ctrl+Shift+Z`, send the text to an OpenAI-compatible `/v1/chat/completions` endpoint, and show the translation in a floating popup.

## Features

- Windows tray app built with .NET 8 and WPF.
- Global hotkey, default `Ctrl+Shift+Z`.
- Selected-text capture through Windows UI Automation, with clipboard fallback for apps that do not expose selected text reliably.
- OpenAI-compatible translation endpoint support, including local vLLM servers.
- Configurable endpoint, model, API key, target language, sampling parameters, and Qwen thinking mode.
- Movable popup with translate, English translation, grammar, improve, copy, replace, close, and settings buttons.

## Text Capture And Clipboard Behavior

Windows does not expose a universal "selected text" API for every application. This app first tries to read the active selection through Windows UI Automation. When that is not available, it can use a clipboard fallback: it simulates `Ctrl+C`, reads the copied Unicode text, and then tries to restore the previous clipboard text.

The clipboard fallback is intended for legacy applications such as older Microsoft Office versions. It is best-effort: if another app owns or changes the clipboard at the same time, restoration may fail or only restore plain text. The fallback can be configured in settings.

When replacing selected text, the app writes the translated/corrected text to the clipboard, focuses the original window, and simulates `Ctrl+V`.

## Requirements

- Windows.
- .NET 8 SDK.
- A running OpenAI-compatible endpoint, for example vLLM at `http://localhost:8000/v1/chat/completions`.

## Commands

Run these from the repository root:

```powershell
dotnet restore WindowsTranslator.sln
dotnet build WindowsTranslator.sln
dotnet test WindowsTranslator.sln
dotnet run --project src\WindowsTranslator.App\WindowsTranslator.App.csproj
```

When launched from a terminal, `Ctrl+C` closes the tray app cleanly. If a build reports `WindowsTranslator.App` is locking files under `bin\Debug`, close the tray app or stop the listed process before rebuilding.

## Configuration

On first run the app creates `%APPDATA%\WindowsTranslator\settings.json` with defaults.

- Set `translation.endpoint` to your OpenAI-compatible chat completions endpoint.
- Set `translation.model` to the model name served by that endpoint.
- Leave `translation.apiKey` empty if your local endpoint does not require bearer auth.
- Configure `translation.enableThinking` and `translation.includeReasoning` for Qwen reasoning models.
- Configure sampling with `temperature`, `topP`, `topK`, `minP`, `presencePenalty`, and `repetitionPenalty`.

Do not commit local settings files containing API keys. `.gitignore` excludes common local config files such as `settings.json`, `appsettings.json`, `.env`, and `*.local.json`.

For Qwen reasoning models served by vLLM, `enableThinking` is sent as `chat_template_kwargs.enable_thinking`. The app discards `message.reasoning` and displays only `message.content`.
