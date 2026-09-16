# CursorDesk

Windows desktop client for the [Cursor Cloud Agent API](https://api.cursor.com), with an optional **local execution** mode. You type a prompt, pick an execution mode, and the app either:

- **Cloud (Cursor API)** — creates a cloud agent run, streams/polls until it finishes, then shows the result; optionally connects a GitHub repo so generated files are committed to `source/` and a PR is opened.
- **Local (CLI)** — runs **Cursor's own agent CLI** (`cursor-agent`) on your machine with the same models/subscription as the Cursor editor. Supports **Agent / Ask / Plan** modes, streams output live, and works directly in a local folder. No `key.txt` needed.

## Setup: API key

1. Create a Cursor API key in the Cursor dashboard.
2. Put the key in a file named `key.txt` in the **same directory as `CursorDesk.exe`**.
   - One line is enough. A trailing newline is fine.
   - Do **not** commit `key.txt` (it is gitignored).

Example (next to the exe after a Debug build):

```
CursorDesk/CursorDesk.App/bin/Debug/key.txt
```

Auth used at runtime: HTTP header `Authorization: Basic ` + base64(`apiKey` + `":"`).

## Build and run

Requires Visual Studio 2022 with the **.NET desktop development** workload, or the .NET SDK plus .NET Framework 4.8 targeting pack.

```bash
cd CursorDesk
dotnet restore
dotnet build CursorDesk.sln
```

Or open `CursorDesk.sln` in Visual Studio 2022 and press F5.

Target: **.NET Framework 4.8**, Windows Forms. Output exe: `CursorDesk.exe`.

Run the exe on Windows (WinForms does not run on Linux). After building, copy `key.txt` beside the exe if it is not already there.

## UI

- **Validate Key** — `GET /v1/me`. On success, the status bar shows account email/name. HTTP 401 shows `invalid key`. Needs `key.txt`.
- **Mode** combo — `Cloud (Cursor API)` (default) or `Local (CLI)`. Local mode reveals the Cursor CLI settings below (CLI / Mode / Args / Dir); it does **not** need `key.txt`.
- **Model** combo — filled from `GET /v1/models` (`items[].id`). Falls back to `default`. Cloud mode only.
- **Prompt** — multi-line input.
- **Send** — runs in the selected mode and streams the answer into the Answer pane.
- **Answer** — read-only formatted result.
- **Session history** — local SQLite list of prompt/result/model/mode/time. Double-click a row to reload that Q&A and switch back to its mode.
- **Status** — account email plus `idle` / `working` / `done` / `error`.

If `key.txt` is missing at startup, a warning is shown, but you can still switch to **Local (CLI)** mode and send prompts.

## Local execution (Cursor agent / Ask, runs locally)

Switch the **Mode** combo to `Local (CLI)` to run **Cursor's local agent** instead of the cloud API. It uses the same models and subscription as the Cursor editor, but the agent loop runs on your machine — no cloud VM cold start, no sandbox/PR round-trip — so interactive tasks feel much faster.

### Setup (one time)

Install the Cursor CLI (Windows PowerShell):

```powershell
irm 'https://cursor.com/install?win32=true' | iex
```

This installs `cursor-agent` under `%LOCALAPPDATA%\cursor-agent\`. The app auto-detects that path; if it finds a `.ps1` wrapper it runs it via `powershell.exe` automatically.

### Configuration

- **CLI** — `cursor-agent` by default (auto-detected); can be a full path or any command on `PATH`.
- **Mode** — Cursor capability preset: **Agent** (can edit files), **Ask** (read-only Q&A over your code), **Plan** (design first). Selecting one fills the Args template.
- **Args** — command-line template. `{prompt}` is replaced with the prompt as a quoted argument. Presets:
  - Agent: `-p --force --trust {prompt}`
  - Ask: `-p --mode ask --trust {prompt}`
  - Plan: `-p --mode plan --trust {prompt}`
  - Add `--model <name>` to pin a model (see `cursor-agent models`).
- **Dir** — the workspace/working directory the agent operates in (defaults to the app folder).

The app starts `cursor-agent` as a child process, streams stdout/stderr live into the Answer pane (ANSI codes stripped) and saves the session with mode `local`. Local runs ignore the GitHub repo box and don't touch the cloud API. Requires an active Cursor subscription.

## API endpoints used

Base URL: `https://api.cursor.com`

| Method | Path | Role |
|--------|------|------|
| GET | `/v1/me` | Validate key; show `userEmail` / name |
| GET | `/v1/models` | Populate model dropdown from `items[].id` |
| POST | `/v1/agents` | Start a Q&A agent (201). Body is exactly `{ "prompt": { "text": "..." }, "model": { "id": "...", "params": [] }, "autoCreatePR": true }`. No `repos` field. `model.params` **must** be `[]`. |
| GET | `/v1/agents/{agentId}/runs/{runId}` | Poll every 2–3s until `status` is `FINISHED`, then display `result` |

There are **no** `/threads/` or `/messages/` calls (those routes 404).

Local mode uses none of these endpoints.

Errors: 401 → `API key invalid`; 429 → `Rate limited, wait Ns` (uses `Retry-After` when present); network failures show a friendly message without crashing the UI.

Local history is stored with `System.Data.SQLite` in `cursordesk.sessions.db` next to the exe (table `Sessions`: Id, Model, Prompt, Result, CreatedAt).
