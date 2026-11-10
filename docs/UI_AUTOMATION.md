# Driving the GUI (for tests and for AI agents)

The app can be operated from a script or an agent over a loopback JSON line protocol. It is **off unless asked for**,
and it drives the real window: every command presses an actual control, so an automated run exercises the same code
path a user does and every edit still lands in `.mod-history` through the Core APIs.

This exists for two reasons. A GUI-only feature is otherwise untestable without a person clicking it, and an assistant
helping a user should be able to press the buttons instead of describing where they are.

## Starting it

```powershell
# From the folder holding ModdingGUI.exe (the Debug exe is what the user runs):
.\ModdingGUI.exe --ui-server            # listens on 127.0.0.1:4571
.\ModdingGUI.exe --ui-server 5000       # explicit port
.\ModdingGUI.exe --ui-script smoke.txt  # run a command file once the window is open
.\ModdingGUI.exe --ui-script smoke.txt --ui-exit   # ...then close
```

While it listens it writes `%TEMP%\gladius-ui-driver.json` with the port and pid, and prints
`[ui] listening on 127.0.0.1:<port>`. The listener is bound to loopback only and is stopped when the window closes.

## Talking to it

One JSON request per line, one JSON response per line. `scripts/ui-drive.ps1` wraps that:

```powershell
cd ModdingGUI
.\scripts\ui-drive.ps1 '{"cmd":"ping"}'
.\scripts\ui-drive.ps1 '{"cmd":"snapshot"}' | ConvertFrom-Json
.\scripts\ui-drive.ps1 -File .\scripts\team-builder-smoke.txt
```

`snapshot` is how you find a control's name; `items` is how you read a list past snapshot's 40-item cut.

## Commands

| Command | Fields | What it does |
|---|---|---|
| `ping` | — | Liveness, plus the selected tab |
| `snapshot` | — | Every named control (type, text, bounds, enabled/visible, list items, checked, tab pages), the selected tab, and the last 40 log lines |
| `items` | `name`, `offset?`, `count?` | Every item of a list or combo box with its index |
| `click` | `name` | Presses a button, toggles a checkbox, selects a radio button |
| `set` | `name`, `text?` / `checked?` / `index?` / `item?` | Sets text, a checkbox, a list or combo index, a combo item by substring, or a tab page of a `TabControl` |
| `selectTab` | `name` | Switches the main tab strip |
| `selectNode` | `name`, `text` | Selects a tree node by (partial) text |
| `clickMenuItem` | `name` | Presses a menu item, checking it first when it is a toggle |
| `screenshot` | `path?` | Renders the window to a PNG (`DrawToBitmap`); `%TEMP%` is expanded |
| `log` | `count?` | The app log tail, with level and source |
| `openTeamBuilder` | `project` | Selects the project (relative paths resolve against the app) and opens the Team Builder tab |
| `quit` | — | Closes the window |

`wait` is not a server command: it is handled by `ui-drive.ps1` (client-side sleep, so the app's UI thread stays free
to paint) and by `--ui-script` (same idea). Use it after anything asynchronous — opening Team Builder loads classes on
a worker thread.

Every response is `{"ok":true,"result":{…}}` or `{"ok":false,"error":"…"}`.

## Behaviour under automation

- **Dialogs do not appear.** A modal would deadlock the driver, which owns the UI thread. `UiConfirm` logs the
  question and answers **yes**, so the transcript shows the decision:

  ```
  [automation] Job points: 'Agent Hero' now spends 58 JP on granted skills but is awarded 55. … → yes
  Success: Raised 'Agent Hero' JOBPOINTS to 58 so its granted skills stay legal.
  ```

  Legacy `MessageBox.Show` calls (settings, missing files) are untouched: avoid commands that trigger them, or they
  will hang the run.
- **Everything runs on the UI thread**, so `snapshot` and `screenshot` are consistent with what the user would see.
- **`screenshot` uses `DrawToBitmap`**, which needs no foreground window and works on a locked desktop. WebView2 and
  some themed controls may render imperfectly; the layout is reliable.

## Adding a command

Two files:

1. `frmMain_partials/UiAutomation.cs` — add a `case` to the `ExecuteUiCommand` switch and a method. Get controls by
   name through `FindControl`, and give anything you add to the UI a `Name` so it is addressable.
2. `scripts/ui-drive.ps1` needs no change: it forwards whatever line it is given.

Keep commands to *driving* the window. A command that reaches into Core directly would be a second code path into the
game data, which `PLAN.md` §0 forbids; press the button instead.

## Relationship to the MCP server (Phase 8)

This is not the MCP server. It is the same idea applied to the shell: a small, opt-in, loopback-only surface used to
test the GUI and to let an agent operate it for a user. Phase 8 exposes the Core APIs over stdio for clients such as
Claude Desktop; the driver exposes the *window*. The two are complementary and share nothing but `IAppLog`.
