# ManagerIV — GTA IV Mod Loader

A modern, transaction-safe, Fluent UI-driven mod manager for **GTA IV: Complete Edition (Windows 11)**, written in **C# / .NET 8 / WPF**. 

The manager orchestrates existing community loaders (FusionFix, FusionOverloader, Ultimate ASI Loader, and ScriptHook) and dynamically links files without ever copying them directly into or mutating the vanilla game directory.

---

## Key Features

* **Zero Game Directory Mutation:** Uses NTFS **directory junctions** (cross-drive, no elevation) and **hard links** (same-volume, no duplication) to deploy files. Your vanilla game files are never modified, and Steam file verification will never wipe your mods.
* **Transaction Journal & Safety Rollbacks:** All operations are recorded sequentially in a journal. If an error occurs mid-deployment (e.g. locked files or directory errors), the manager automatically rolls back all changes, restoring the system to its prior stable state.
* **Safe & Batch Archive Import:** Drag-and-drop or select multiple `.zip`, `.rar`, and `.7z` archives simultaneously. Ingests mods sequentially to prevent race conditions and automatically flattens nested folder paths via mod root promotion, featuring Zip-Slip path-traversal protection.
* **Structure & Size Validation:** Checks mod contents against engine file size limits (134 MB for `.img`, 2 GB for `.rpf`), flags unsupported directory-based archives, and guards reserved paths (e.g., preventing overwrites of `update/GTAIV.EFLC.FusionFix`). Blocks deployment on structural errors.
* **Save Profile Manager:** Backup, create, rename, switch, and delete individual GTA IV save files and Rockstar Games profile directories.
* **FusionFix Configuration Editor:** An inline, tabbed settings editor to parse, modify, and save options inside `GTAIV.EFLC.FusionFix.ini`. Includes tooltips and validation warnings for graphics, gameplay, cutscene FPS limits, and vehicle/pedestrian budgets.
* **Automated Tool Deployments:** Queries GitHub API via Octokit.NET to fetch and install/uninstall backend loaders and hooks (FusionFix, Ultimate ASI Loader, DXVK) into the game folder, tracking all files in a profile-specific JSON manifest for a 100% clean uninstall.
* **Vanilla Game Reset:** Instantly restores the game directory to its original clean state by deleting staging directories (`update`, `plugins`, `scripts`), removing installed backend tools/manifests, and removing all mod links.
* **Update Watchdog:** Monitors `GTAIV.exe` size and SHA-256 hash. If Steam updates the game executable under the hood, the app displays a clear warning page to prevent broken setups.
* **Fluent Design & Theme Persistence:** Styled with the native `WPF-UI` library for a premium Fluent theme interface (Light/Dark) that persists across restarts.

---

## Architecture

The system is split into three clean layers separated by platform interfaces:

```
Management Layer (WPF MVVM, Profile Manager, Conflict Detector, Validator)
        │
        ▼ IBackendAdapter
Staging-to-Game Bridge (Junction & Link deployment engine)
        │
        ▼ 
Loading / Injection Layer (FusionFix, ASI Loader, ScriptHook — delegated)
```

---

## Tech Stack

* **Runtime:** .NET 8 / C#
* **UI Framework:** WPF + WPF-UI (Fluent styles)
* **Libraries:**
  * **Octokit.NET:** Resolves and queries tool releases from GitHub APIs respecting rate limits.
  * **SharpCompress:** Safe archive extraction handler.
* **Testing:** xUnit

---

## How to Build and Run

### Prerequisites
* .NET 8 SDK (or Visual Studio 2022)

### Running the Application
To compile and launch the GUI application, run:
```bash
dotnet run --project src/ManagerIV/ManagerIV.csproj
```

### Running Unit Tests
To execute the suite of 58 unit and integration tests (which cover path resolution, conflict detection, transaction rollback, folder validation, watchdog alerts, save profiles, and zip-slip protection):
```bash
dotnet test ManagerIV.sln
```

---

## License

This project is open-source. Backend tools downloaded by the manager (e.g., FusionFix) are governed by their respective licenses (e.g., GPL-3.0).
