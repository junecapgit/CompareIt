# CompareIt

CompareIt is a lightweight Windows file and folder comparison tool built with .NET 8 WinForms.

It is designed for fast local comparisons with minimal setup and supports self-contained publishing for use on locked-down corporate machines.

## Features

- Side-by-side file comparison
- Character-level highlighting within modified lines
- Line number display for both files
- Light red highlighting for removed lines
- Diff block navigation (F3 / Shift+F3)
- Copy change blocks left-to-right or right-to-left
- In-memory editing session for file changes
- Save-on-demand (changes are not written until Save)
- Undo last change history (up to 5 changes)
- Recursive folder comparison with status grouping

## Tech Stack

- .NET 8
- Windows Forms
- DiffPlex (line diff engine)

## Getting Started

### Prerequisites

- Windows
- .NET 8 SDK

### Build

```powershell
dotnet build
```

### Run

```powershell
dotnet run
```

## Usage

1. Select `File` mode or `Folder` mode.
2. Choose left and right paths.
3. Click `Compare` (or press `F5`).

For file mode:

- Use `F3` / `Shift+F3` to move between diff blocks.
- Use `Copy Left -> Right` or `<- Copy Right` to apply block changes in memory.
- Use `Undo Last` to revert the most recent in-memory operation (up to 5).
- Use `Save` (or `Ctrl+S`) to persist changes to disk.

## Publish (Single EXE)

Create a self-contained single-file executable:

```powershell
dotnet publish -c Release -r win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```

Output path:

`bin\Release\net8.0-windows\win-x64\publish\CompareIt.exe`

## Project Structure

```text
CompareIt/
|-- CompareIt.csproj
|-- CompareIt.sln
|-- Program.cs
|-- MainForm.cs
|-- Core/
|   |-- FileDiffer.cs
|   `-- FolderDiffer.cs
`-- Controls/
    |-- SyncedRichTextBox.cs
    |-- FileCompareControl.cs
    `-- FolderCompareControl.cs
```

## Roadmap

- Improved merge conflict workflow
- Better folder diff filters (extensions, ignore patterns)
- Export comparison results
- Optional syntax-aware diff rendering

## Contributing

Contributions are welcome. Please open an issue first for major changes, then submit a pull request with a clear description of the problem and solution.

## License

No license file has been added yet. Add a LICENSE file to define usage rights.
