# ModdingGUI

A Windows-based graphical interface for modding tools that simplifies the unpacking, editing, and repacking of ISO files. ModdingGUI is designed to help less technical users manage the unpacking and repacking processes with ease.

## Features

- **Unpack ISO Files**: Select an ISO file, specify a target directory, and unpack game files for modding.
- **Pack Modified Files**: After making edits, repack the files back into an ISO format for deployment.
- **Automated Batch File Creation**: Automatically generates and runs batch files to handle complex modding workflows.
- **Process Output Logging**: Displays real-time logs in the interface for both unpacking and packing operations, with colored status messages to indicate success, errors, and informational messages.
- **User-Friendly Interface**: Easy-to-use UI elements, including file and folder selectors, provide a streamlined experience for users with any technical background.

## Prerequisites

- **Python 3**: Required for running the underlying modding scripts. The application will check for Python 3 installation on startup. Make sure Python is added to your system PATH.
- **Windows OS**: This application is designed for Windows environments.

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

## Contributing

We welcome contributions! Please submit issues for any bugs or feature requests, and create pull requests with descriptive changes. Be sure to follow the existing code structure and naming conventions for consistency.

