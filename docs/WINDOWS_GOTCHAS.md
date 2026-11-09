# Windows environment gotchas

Things that break the tool while the code is correct. Each entry records the symptom, the cause, and the check, so a
future session does not spend rounds guessing.

## 1. A Low-integrity folder label silently sandboxes the built app

**Symptom.** "Open containing folder" and "Open ISO location" fail with `Access is denied`. The log shows a path that
exists, spelled correctly:

```
Could not open 'E:\…\gold_test' through the shell: the shell refused to open '…' (0x80070005)
Could not open '…' via ShellExecute: … Access is denied
explorer.exe - Application Error: … (0xC0000142)
```

**Cause.** The folder tree carries a *mandatory integrity label* of **Low**. Windows derives a new process's integrity
from the **image file's** label, so launching `ModdingGUI.exe` from a Low-labeled folder produces a Low-integrity
process even when the user double-clicks it from a normal desktop. A Low process may not drive the medium-integrity
shell (UIPI), so every shell route is refused at once, and a spawned `explorer.exe` cannot initialise (`0xC0000142`).

Workspaces created by an agent session arrive with this label; folders the user made normally do not. That is why the
problem looked like "the AI locks my folders down".

**Check.**

```
icacls "E:\path\to\app\folder" | findstr Mandatory
```

`Mandatory Label\Low Mandatory Level` is the problem; `Medium` is normal.

The app reports the same fact itself: `frmMain_Load` logs

```
Running as: elevated=False, integrity=Medium, inJob=False, interactive=True, session=1
```

and the open-folder dialog names the Low case outright.

**Fix.** Raise the label on the project root and everything under it:

```
icacls "E:\path\to\project" /setintegritylevel (OI)(CI)Medium /T /C
```

`(OI)(CI)` makes new files inherit Medium, so a rebuild does not reintroduce the problem. Rebuild the app afterwards so
the executable itself is relabeled, then confirm:

```
icacls "…\ModdingGUI.exe" | findstr Mandatory      # expect Medium
```

**Do not** "fix" this by adding fallbacks in the code: from a Low process no route can succeed. ShellExecute, the shell
namespace API, and spawning `explorer.exe` are all refused, and the diagnostics added in `Helpers/ProcessContext` exist
to say so immediately rather than one feature at a time.

## 2. Never spawn `explorer.exe` from this app

`explorer.exe` started with `UseShellExecute = false` inherits this process's environment and dies at load with
`0xC0000142` (DLL initialisation failed), leaving an Application Error dialog. Folder opening goes through
`Helpers/ShellFolderOpener`, which asks the running shell via `SHOpenFolderAndSelectItems` and creates no process.

## 3. The shell namespace rejects forward slashes

`SHParseDisplayName` returns `E_INVALIDARG` (`0x80070057`) for `E:/dir/sub`. The tool's path fields are normalised to
forward slashes for the Python toolchain, so anything handing a path to a Windows API, to `explorer.exe`, or to `cmd`
must convert first:

```csharp
ShellFolderOpener.NormaliseForShell(path)   // absolute, backslashes, no trailing separator, drive root preserved
```

This cost three separate bugs (class/skill loading, folder opening, ShellExecute fallback) before the boundary was
named.

## 4. "An error occurred trying to start process 'X' … Access is denied" usually means X is a folder

`Process.Start` on a directory with `UseShellExecute = false` produces exactly that message. It is not a permissions
problem. Check whether the path is a directory before suspecting ACLs.
