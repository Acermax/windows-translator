# Windows Translator

Small Windows desktop translator for selected text. Select text in another app, press `Ctrl+Shift+Z`, send the text to an OpenAI-compatible `/v1/chat/completions` endpoint, and show the translation in a floating popup.

## Features

- Windows tray app built with .NET 8 and WPF.
- Global hotkey, default `Ctrl+Shift+Z`.
- Non-invasive selected-text capture through Windows UI Automation.
- OpenAI-compatible translation endpoint support, including local vLLM servers.
- Configurable endpoint, model, API key, target language, sampling parameters, and Qwen thinking mode.
- Movable popup with copy, close, and settings buttons.

## Important Limitation

Windows does not expose a universal "selected text" API for every application. This app intentionally avoids simulating `Ctrl+C`, synthetic keyboard input, or automatic clipboard reads. If the active app does not expose selected text through Windows UI Automation, translation will not be available for that selection.

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
