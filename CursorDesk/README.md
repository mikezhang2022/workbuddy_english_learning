# CursorDesk

Windows desktop client for the [Cursor Cloud Agent API](https://api.cursor.com). This P0 MVP is **cloud Q&A only**: you type a prompt, pick a model, and the app creates an agent run, polls until it finishes, then shows `result`. Coding-task / repo workflows come later.

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

- **Validate Key** — `GET /v1/me`. On success, the status bar shows account email/name. HTTP 401 shows `invalid key`.
- **Model** combo — filled from `GET /v1/models` (`items[].id`). Falls back to `default`.
- **Prompt** — multi-line input.
- **Send** — creates an agent, polls the run, then shows the answer. Disabled if `key.txt` is missing.
- **Answer** — read-only result (and `url` when the API returns one).
- **Session history** — local SQLite list of prompt/result/model/time. Double-click a row to reload that Q&A.
- **Status** — account email plus `idle` / `working` / `done` / `error`.

If `key.txt` is missing at startup, Send is disabled and a clear error is shown.

## API endpoints used

Base URL: `https://api.cursor.com`

| Method | Path | Role |
|--------|------|------|
| GET | `/v1/me` | Validate key; show `userEmail` / name |
| GET | `/v1/models` | Populate model dropdown from `items[].id` |
| POST | `/v1/agents` | Start a Q&A agent (201). Body is exactly `{ "prompt": { "text": "..." }, "model": { "id": "...", "params": [] }, "autoCreatePR": true }`. No `repos` field. `model.params` **must** be `[]`. |
| GET | `/v1/agents/{agentId}/runs/{runId}` | Poll every 2–3s until `status` is `FINISHED`, then display `result` |

There are **no** `/threads/` or `/messages/` calls (those routes 404).

Errors: 401 → `API key invalid`; 429 → `Rate limited, wait Ns` (uses `Retry-After` when present); network failures show a friendly message without crashing the UI.

Local history is stored with `System.Data.SQLite` in `cursordesk.sessions.db` next to the exe (table `Sessions`: Id, Model, Prompt, Result, CreatedAt).
