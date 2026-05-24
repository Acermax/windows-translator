# Manual Test Checklist

- Start vLLM with an OpenAI-compatible `/v1/chat/completions` endpoint.
- Run the app and confirm the tray icon appears.
- Open settings and set the vLLM endpoint and served model name.
- Select text in Notepad and press `Ctrl+Shift+Z`; confirm a popup appears near the cursor.
- Confirm the clipboard content is not read, overwritten, or restored automatically by the app.
- Try text in a browser, PDF viewer, and code editor.
- Stop vLLM and confirm the popup shows a readable endpoint error.
- Press the hotkey twice quickly and confirm the newer request wins.
