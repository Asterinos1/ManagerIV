# AGENTS.md — GTA IV Mod Loader

## Purpose of this file
This file governs how the Antigravity CLI agent behaves across all sessions of the GTA IV Mod Loader project. Read it fully at the start of every session before writing any code.

---

## Project snapshot

A GUI-driven mod manager for **GTA IV: Complete Edition (Windows 11)**, written in **C# / .NET 8 / WPF**. The tool orchestrates existing community loaders (FusionFix / FusionOverloader, Ultimate ASI Loader, ScriptHook) and manages the files those loaders consume. It never performs injection itself.

### Tech stack
| Concern | Choice |
|---|---|
| Language / runtime | C# / .NET 8 |
| UI | WPF + Fluent UI (WPF-UI or ModernWpf) |
| Filesystem links | P/Invoke (`CreateHardLinkW`, reparse points for junctions) |
| GitHub integration | Octokit.NET |
| Archives | SharpCompress (zip/rar) + 7-Zip binding (7z) |
| Config | System.Text.Json (JSON manifests) |
| Tests | xUnit; all FS operations behind interfaces |

---

## Non-negotiable constraint

> **The game's original files must never be mutated.**

This is the single hardcoded rule. Every design decision flows from it. No exception exists, no matter how convenient a shortcut seems. "Verify integrity of game files" in Steam must never wipe a user's mod setup.

Concretely, this means:
- Deployment uses **directory junctions** (cross-drive, no elevation) and **hardlinks** (same-volume, no duplication), never in-place file edits.
- Backend tool files (e.g., `dinput8.dll`) are the **only** files we write into the game directory; they are tracked and rollback-able.
- FusionOverloader's `update/<NNN>_<modname>` subfolder model is the merge engine — we populate it via junctions, we do not repack or patch anything ourselves.

---

## Architecture (reference, not a cage)

Three layers, separated by the `IBackendAdapter` interface:

```
Management Layer  (this app)
        │  IBackendAdapter
Staging-to-Game Bridge  (junctions / hardlinks)
        │
Injection / Loading Layer  (FusionOverloader, ASI Loader, ScriptHook — delegated)
```

The `IBackendAdapter` seam is important for future legacy-version support, but the agent may restructure internals freely as long as the non-mutation constraint holds and the public interface remains swappable.

---

## Session model

The project is built in **6 sequential stages, one stage per session**. Each session is independent — the agent starts with an empty context window and must be primed correctly (see below).

### How to start a session
Provide the agent with:
1. This `AGENTS.md` file.
2. All code produced in previous stages (or a path to it).
3. The stage prompt below for the current stage.

The agent must read `AGENTS.md` and the prior code before writing anything new.

---

## Stage prompts

### Stage 1 — Core domain models and interfaces
Build the foundational types. No UI, no complex logic yet.

Implement:
- `IBackendAdapter` interface.
- Domain records/classes: `ModFile`, `StagedMod`, `LoadOrderModel`, `GameVersionProfile`, `DeployTarget`.
- Configuration models for JSON manifests (Tools, Adapters, Paths).
- `IFileSystemLinker` interface to abstract junctions, hardlinks, and symlinks for testability.

Verification: project compiles; all interfaces and models are strongly typed.

---

### Stage 2 — Filesystem linker and transaction journal
Implement the filesystem manipulation and safety layers.

Implement:
- `NativeFileSystemLinker` implementing `IFileSystemLinker`. P/Invoke for `CreateHardLinkW` and directory junctions (reparse points). Fallback to copy on non-NTFS; optional elevated symlink mode behind a flag.
- `TransactionJournal` — records intended filesystem operations in order.
- `BackupRollbackService` — uses the journal to reverse operations on mid-deployment failure.

Verification: a console harness creates a dummy library dir and a dummy game dir, creates a cross-drive directory junction without elevation, and the `TransactionJournal` successfully rolls it back.

---

### Stage 3 — Archive handler and metadata extraction
Implement archive extraction and metadata parsing.

Implement:
- `ArchiveHandler` using SharpCompress. Zip-slip protection: reject entries with `../` or absolute paths outside the extraction target. Support `.zip`, `.rar`, `.7z`.
- `MetadataService`: scans extracted file structures to guess CE vs Legacy compatibility from keywords in `.txt`/`.ini`/`.md` files or specific extensions (`.asi`).
- Filename parsing: strip noise tokens (`final`, `release`, `fix`, build hashes), normalize separators, split trailing version token from name. Examples: `Better_Handling_v1.0.8` → name `Better Handling`, version `1.0.8`.

Verification: a test `.zip` with nested folders and an `.asi` file extracts safely without path traversal; `MetadataService` infers type and parses name/version correctly.

---

### Stage 4 — Profile and load order management
Implement state management for profiles and load orders.

Implement:
- `ProfileManager`: save/load `Profile` objects to a local JSON file. A Profile contains enabled mods, paths, load order, and conflict state.
- `LoadOrderService`: manages the unified ordered list and maps it to per-type mechanisms (FusionOverloader prefix for assets, naming/sequence for `.asi`).
- `ConflictDetector`: builds a map of target paths → mod IDs. Flags replacement conflicts (same target file) and warns when multiple mods touch `handling.dat`, merge-needed data files, or `.img` files.

Verification: two mock mods both targeting `update/data/handling.dat` are added to a profile; `ConflictDetector` identifies the overlap and assigns the winner by load order priority.

---

### Stage 5 — Backend tool manager and adapters
Implement deployment and network-fetching logic.

Implement:
- `CompleteEditionAdapter` implementing `IBackendAdapter`. Translates load order into the physical `update/<NNN>_<modname>` folder structure. Routes `.asi` → `plugins/`, assets → `update/`, scripts → `scripts/`.
- `BackendToolManager` using Octokit.NET. Queries GitHub releases for FusionFix and Ultimate ASI Loader, respects unauthenticated rate limit (60/hr) with caching, verifies checksums on every downloaded binary, always exposes a manual-link fallback.
- `UpdateWatchdog`: hashes `GTAIV.exe` (file version + size + hash) and compares against the last known state stored in the active profile. Surfaces a clear warning on mismatch; never auto-repairs silently.

Verification: `UpdateWatchdog` flags a mismatch after a dummy executable is modified; `CompleteEditionAdapter.ResolveTarget` routes correctly.

---

### Stage 6 — WPF UI integration
Implement the frontend. Wire ViewModels to the core services from previous stages.

Implement:
- `MainWindow` with a navigation sidebar.
- `ModLibraryView`: drag-and-drop list for load order; rows tagged by type (Asset / ASI / Script).
- `ProfileSwitcherView`: swap between configurations.
- Deployment command bound to `BackupRollbackService` — a Restore Point is created before tearing down and rebuilding junctions.

Verification: drag a mod archive into the window; UI shows parsed metadata; enable the mod, apply, and verify via File Explorer that the correct junction exists in the target game directory.

---

## Deviation policy

The agent is free to deviate from the design document when it has good reason (better library choice, cleaner abstraction, safer implementation, etc.), **except for the non-mutation constraint, which is absolute**.

When deviating, the agent must:
1. Implement the better solution.
2. Append an entry to `DECISIONS.md` in the repo root (create it if it doesn't exist) using this format:

```markdown
## [Stage N] <Short title>

**Original design:** What the doc said to do.
**What was implemented:** What the agent did instead.
**Reason:** Why this is better (technical justification).
```

Do not ask for permission before deviating. Just implement and log.

---

## Code style rules

- Write self-explanatory code. Comments only for genuinely complex cases (e.g., P/Invoke reparse-point structs).
- No hardcoded paths, versions, or tool URLs — everything goes through the JSON manifest.
- All filesystem operations behind `IFileSystemLinker` so deployment/conflict/rollback logic is unit-testable without a real game install.
- Prefer `record` types for immutable domain objects.
- Async all the way down for I/O (GitHub API, archive extraction, file operations).

---

## Out of scope (do not implement)

- Legacy game versions (1.0.4.0 / 1.0.7.0 / 1.0.8.0) — future adapter.
- Cross-mod `.img` repacking or merging.
- Linux / Proton support.
- Steam Workshop or any in-app mod browser.
- Elevation / UAC prompts (the design avoids these by construction).