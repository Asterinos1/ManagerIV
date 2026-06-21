# GTA IV Mod Loader

A modern, transaction-safe, Fluent UI-driven mod manager for **GTA IV: Complete Edition (Windows 11)**, written in **C# / .NET 8 / WPF**. 

The manager orchestrates existing community loaders (FusionFix, FusionOverloader, Ultimate ASI Loader, and ScriptHook) and dynamically links files without ever copying them directly into or mutating the vanilla game directory.

---

## Key Features

* **Zero Game Directory Mutation:** Uses NTFS **directory junctions** (cross-drive, no elevation) and **hard links** (same-volume, no duplication) to deploy files. Your vanilla game files are never modified, and Steam file verification will never wipe your mods.
* **Transaction Journal & Safety Rollbacks:** All operations are recorded sequentially. If an error occurs mid-deployment (e.g. locked files or directory errors), the manager automatically rolls back all changes, restoring the system to its prior stable state.
* **Safe Archive Import:** Drag-and-drop `.zip`, `.rar`, and `.7z` archive files directly into the window. Features built-in **Zip-Slip (path traversal) protection** to prevent security issues.
* **Metadata Extraction & Compatibility Scanner:** Automatically normalizes mod filenames (stripping build hashes, release noise tokens) to deduce name and version. Scans text readmes and configuration files for version keywords to flag compatibility badges (`CE-compatible`, `Legacy`, `Mixed`).
* **Update Watchdog:** Monitors `GTAIV.exe` size and SHA-256 hash. If Steam updates the game executable under the hood, the app displays a clear warning page to prevent broken setups.
* **Fluent Windows 11 Design:** Built using WPF and styled with the native `WPF-UI` library for a premium Fluent theme interface.

---

## Architecture

The system is split into three clean layers separated by platform interfaces:

```
Management Layer (WPF MVVM, Profile Manager, Conflict Detector)
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
dotnet run --project src/GtaIVModLoader/GtaIVModLoader.csproj
```

### Running Unit Tests
To execute the suite of 14 unit and integration tests (which cover path resolution, conflict detection, transaction rollback, and zip-slip protection):
```bash
dotnet test GtaIVModLoader.sln
```

---

## License

This project is open-source. Backend tools downloaded by the manager (e.g., FusionFix) are governed by their respective licenses (e.g., GPL-3.0).
