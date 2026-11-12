# ModdingGUI

A Windows-based graphical interface for modding tools that simplifies the unpacking, editing, and repacking of ISO files. ModdingGUI is designed to help less technical users manage the unpacking and repacking processes with ease.

## Features

- **Unpack / pack ISO files** into a modifiable project (`<name>_ISO` + `<name>_BEC`), with a live log and a `.mod-history` backup of every write.
- **Item, skill and class editors** driven by catalogs, so values are chosen rather than typed.
- **Encounter editor**: pick an encounter by region, town and league, then edit each unit row's 21 slots with guarded controls, real searchable unit picking, an attribute picker and a validator.
- **Series editor**: a league's run of battles in play order, including the `TYPE` that decides whether units carry their health between stages, plus cloning a whole run into a new league.
- **Grid builder**: paint start markers and passability on an arena's `.grd`, with a map overlay.
- **Team builder**: build a unit from its class, gear and skills, and export a roster.
- **Randomizer**, **patching** (xdelta), and **disc-image conversion** (ISO/RVZ/WIA/GCZ).
- **Bundled toolchain**: the pack/unpack/script-compile tools ship as `tools/v009/tools_v009.exe`, so Python is *optional*.

Every edit goes through `ModdingGUI.Core` (`IItemsApi`, `ISkillsApi`, `IEncountersApi`, `ILeaguesApi`, `IGridsApi`, ...), which is also what an MCP server and the UI driver talk to. See `docs/AGENTS.md` for the architecture rules and `docs/OPEN_ITEMS.md` for what is still unfinished.

## Prerequisites

- **Windows** (the shell is WinForms).
- **Python 3** is optional: the bundled toolchain covers unpack, pack and script compile. A `tools/` toolbelt and Python are used when present.
## Installation

### Option 1: For End Users

1. **Download the Latest EXE**: Download the latest executable from the [Releases](https://github.com/Gladius-Community-Devs/ModdingGUI/releases) page.

2. **Ensure Proper Folder Setup**: The `tools` folder is included with the release. Make sure that the `tools` folder and the downloaded executable (`.exe`) are in the same directory.

3. **Execute the EXE**: Run the executable to launch the ModdingGUI application.

4. Windows might catch the program, you can select More Info > Run Anyways in order to execute the exe

### Option 2: For Developers

1. **Clone the Repository**: Clone this repository to your local machine.
   ```bash
   git clone https://github.com/Gladius-Community-Devs/ModdingGUI.git
   ```

2. **Install Dependencies**: Ensure Python 3 is installed on your system and added to your PATH.
   - You can download Python from [python.org](https://www.python.org/).

3. **Configure Tool Scripts**: Place your Python modding tools in the `tools` directory within the application root. Required Python scripts include:
   - `ngciso-tool.py`
   - `bec-tool-all.py`
   - `Gladius_Units_IDX_Unpack.py`
   - `Tok_Num_Update.py`
   - `tok-tool.py`
   - `Update_Strings_Bin.py`
   - `Gladius_Units_IDX_Repack.py`

4. **Build and Run**: Build the project in Visual Studio or another C# IDE, and run `frmMain` as the main form.

## Usage

1. **Unpacking an ISO**:
   - Open the application and click the "Select ISO" button to choose an ISO file.
   - Enter a folder name where you’d like the unpacked files to be stored.
   - Click **Unpack** to initiate the unpacking process. Logs will display the progress and status of each step.

2. **Editing Files**:
   - Once the files are unpacked, you can navigate to the unpacked folder to make modifications as needed.

3. **Repacking Files**:
   - Enter or select the modified folder, then click **Pack** to repack the files back into ISO format.
   - The log will display the status of each packing step, including errors and completion messages.

4. **Saving Batch Files** (optional):
   - The app can optionally save the generated batch files for further inspection or reuse.
   - Check the `Save Batch File` option under the **File** menu if you want to keep these files after execution.

## Directory Structure

The app expects a specific directory structure:
- `tools/`: Contains the Python scripts used for unpacking and repacking.
- `GeneratedBatch_<GUID>.bat`: Temporarily generated batch files for unpacking and packing operations. These files are deleted after execution unless saved by user preference.

## Logging

ModdingGUI logs outputs from each stage of the process in real time:
- **Green**: Successful operations
- **Red**: Errors encountered during processing
- **Blue**: Informational messages

This color-coded logging helps users identify the status of each action immediately.

## Troubleshooting

- **Python 3 Not Found**: Ensure Python 3 is installed and added to your PATH. You can verify by running `python --version` in the command prompt.
- **Missing Tools**: Confirm that all required Python scripts are available in the `tools/` directory.
- **Error During Execution**: Errors during unpacking or packing are logged in red. Check the log messages for details on the failure point.
- **`MSB3821: Couldn't process file Form1.resx due to its being in the Internet or Restricted zone`**: two unrelated causes, in order of likelihood.
  1. The `.resx` is not a real resource file. VS's `GenerateResource` (MSBuild 18) rejects one that contains markup it cannot process — a `.resx` holding the Visual Studio *template documentation* (prose inside `<root>`, `<resheader>` without `<value>`, `[base64 mime encoded …]` placeholders) trips this even though the file carries no zone mark at all, while older MSBuild versions silently accepted it. Replace it with a minimal valid file (see `SkillEditor\SkillEditorView.resx`).
  2. A file really does have the mark of the web, typically because the repository arrived as a zip download. Clear it and rebuild:

     ```powershell
     Get-ChildItem -Recurse -File | Unblock-File
     ```

     This is a per-machine file attribute, not a project setting, so every fresh copy of the repo may need it once.
- **`MSB3270` architecture mismatch**: the app is `win-x64` (it publishes a self-contained single-file exe), so `ModdingGUI.Tests` also sets `PlatformTarget` to `x64`. Give any new project that references the app the same platform target.

## Contributing

We welcome contributions! Please submit issues for any bugs or feature requests, and create pull requests with descriptive changes. Be sure to follow the existing code structure and naming conventions for consistency.

