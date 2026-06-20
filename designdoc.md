# GTA IV Mod Loader — Design Document

**Status:** Draft v0.1
**Target platform:** Windows 11 (Windows 10 as a later target; Linux explicitly out of scope)
**Initial game target:** GTA IV: The Complete Edition (CE), via FusionFix / FusionOverloader
**Distribution:** Open source on GitHub, with prebuilt signed releases

---

## 1. Overview

A GUI-driven mod manager for GTA IV that lets a user install, enable, disable, reorder, and switch between sets of mods **without ever mutating the vanilla game directory**. The tool does not perform mod injection itself; it orchestrates existing, well-maintained community loaders (FusionFix / FusionOverloader, Ultimate ASI Loader, ScriptHook / ScriptHookDotNet) and manages the files those loaders consume.

The guiding principle is a clean separation between *what we build* (the management layer) and *what we delegate* (the loading/injection layer).

---

## 2. Scope

### In scope for v1
- CE detection and support only.
- Deploying asset/data mods through FusionOverloader's `update` folder.
- Deploying `.asi` plugins and `.net`/script mods to `plugins/` and `scripts/`.
- Auto-download / update of backend tools (FusionFix bundle, ASI loader, ScriptHook) via GitHub API with manual-link fallback.
- Archive import (`.zip`, `.7z`, `.rar`) with file detection and guided install.
- Profiles, enable/disable, load-order control, conflict detection, backup/rollback, centralized logging, game-update detection, and mod metadata (compatibility scan + name/version parsing).

### Out of scope for v1 (future)
- Legacy versions (1.0.4.0 / 1.0.7.0 / 1.0.8.0) — handled later by an alternative backend adapter.
- Cross-mod merging *inside* `.img` archives (OpenIV-class asset repacking).
- Linux/Proton.
- Steam Workshop or any in-app mod browser/marketplace.

---

## 3. Goals and Non-Goals

**Goals**
- The game's original files are never modified. "Verify integrity of game files" should never wipe a user's setup.
- Cross-drive: manager + mods on one drive, game on another, with no file duplication and no admin elevation.
- Everything configurable at runtime (paths, backend sources, load order) without recompiling.
- Trustworthy: every state change is reversible.

**Non-Goals**
- We are not a mod injector, an `.img` editor, or a graphics fix. Those belong to the backend tools.
- We do not host or redistribute the backend tools; we fetch them from their official sources.

---

## 4. Architecture

Three layers, with a per-version **backend adapter** as the seam between them.

```
┌─────────────────────────────────────────────┐
│  Management Layer  (this app)                │
│  profiles · enable/disable · conflicts ·     │
│  load order · backup/rollback · logging ·    │
│  metadata · downloader · archive handler     │
└───────────────┬─────────────────────────────┘
                │  IBackendAdapter
┌───────────────▼─────────────────────────────┐
│  Staging-to-Game Bridge                      │
│  junctions / hardlinks into game dirs        │
└───────────────┬─────────────────────────────┘
                │
┌───────────────▼─────────────────────────────┐
│  Injection / Loading Layer  (delegated)      │
│  FusionOverloader · Ultimate ASI Loader ·    │
│  ScriptHook / ScriptHookDotNet               │
└─────────────────────────────────────────────┘
```

### `IBackendAdapter`
The management layer never talks to the game directly. It talks to an adapter that encapsulates everything version-specific:

```
interface IBackendAdapter {
    GameVersionProfile DetectVersion(string gameDir);
    DeployTarget ResolveTarget(ModFile file);      // update/ vs plugins/ vs scripts/
    void Deploy(StagedMod mod, int priority);
    void Undeploy(StagedMod mod);
    LoadOrderModel ReadLoadOrder();
    void WriteLoadOrder(LoadOrderModel order);
    IEnumerable<string> BackendLogPaths();
}
```

v1 ships a single `CompleteEditionAdapter`. A future `LegacyAsiAdapter` slots in with zero changes to the management layer. This is the concrete form of the "switchable backend" idea from the notes.

---

## 5. Core Components

| Component | Responsibility |
|---|---|
| **Profile Manager** | Named, independent mod sets; switch the active one. |
| **Mod Library** | Canonical extracted copy of each imported mod + its metadata, stored once in app data. |
| **Deployment Engine** | Links library mods into the game via the active adapter (see §6). |
| **Load Order Service** | Unified ordered list mapped to the right per-type mechanism (see §7). |
| **Conflict Detector** | File-overlap analysis per profile (see §11). |
| **Backup/Rollback Service** | Snapshots before any mutating operation (see §8). |
| **Update Watchdog** | Detects game version changes (see §9). |
| **Backend Tool Manager** | Downloads/updates FusionFix, ASI loader, ScriptHook (see §12). |
| **Archive Handler** | Extraction + safe file detection from `.zip`/`.7z`/`.rar` (see §10). |
| **Metadata Service** | Compatibility scan + name/version parsing + user overrides (see §10). |
| **Logger** | Centralized app log + aggregated backend logs (see §13). |

---

## 6. Staging Strategy *(key technical decision)*

The notes proposed symlinks. After accounting for the CE-first target and the no-admin / cross-drive constraints, a **directory-junction + hardlink** model is a better fit than symlinks, and it maps cleanly onto how FusionOverloader already works.

### Why not plain symlinks
On Windows the link primitives differ in important ways:

| Primitive | Cross-drive? | Needs admin / Dev Mode? | Files or dirs? |
|---|---|---|---|
| Symbolic link | Yes | **Yes** (admin or Developer Mode) | Both |
| Hard link | No (same volume only) | No | Files only |
| Directory junction | **Yes** (any local volume) | **No** | Dirs only |

Symlinks would force either elevation or a "turn on Developer Mode" step — friction that contradicts the "seamless" goal. Junctions give us cross-drive linking with no elevation, and hardlinks give us free, instant, deduplicated file placement within a volume.

### How it works for CE

FusionOverloader scans subfolders inside the game's `update/` folder and merges them by priority; originals stay vanilla. So we do **not** need our own merged virtual filesystem — FusionOverloader *is* the merge engine. Our job is to populate `update/` with one subfolder per enabled mod and control their priority.

Per-mod deployment:

1. **Asset/data mods** → create a **directory junction** at
   `…/GTAIV/update/<NNN>_<modname>` pointing at the mod's folder in our library.
   The junction crosses drives (library on C:, game on D:) with no admin.
   `<NNN>` is the priority prefix the load-order service controls.
2. **`.asi` plugins** → place/junction into `…/GTAIV/plugins/`.
3. **`.net`/script mods** → place/junction into `…/GTAIV/scripts/`.

Disabling a mod = remove its junction. Switching profiles = tear down the active profile's junctions and lay down the next profile's. The vanilla game files are never touched, so a Steam integrity check has nothing to revert.

When a single volume is involved and a true merge is unavoidable (e.g., for the rare loose-file case outside `update/`), we hardlink files into a staging tree first (instant, no duplication), then junction that tree in.

### Fallbacks
- If the filesystem can't hardlink (non-NTFS, network path), fall back to copy and warn.
- An optional **symlink mode** can be exposed for advanced users who have run the app elevated or enabled Developer Mode, but it is never the default.

> Open item: confirm the exact subfolder layout / `modloader.ini` semantics against the installed FusionOverloader version at runtime, and keep the `update` path and priority mechanism **parameterized** in the adapter config rather than hardcoded.

---

## 7. Load Order Management

Load order is two distinct mechanisms behind one unified UI list:

1. **FusionOverloader asset priority** (for `update/` mods)
   Controlled by the numeric/symbol prefix on each mod subfolder (higher sorts later/higher priority) and/or `modloader.ini`. The Load Order Service owns these prefixes; reordering in the UI rewrites prefixes (and/or `modloader.ini`) atomically.

2. **ASI plugin order** (for `.asi` in `plugins/`)
   Governed by Ultimate ASI Loader. Managed by load sequence / naming where the loader honors it.

The UI presents a single drag-to-reorder list, tagging each row with its type (`asset` / `asi` / `script`). Internally each type is written through its correct mechanism. Order is stored per profile so it travels with the profile and is restored exactly on switch.

---

## 8. Backup & Rollback

Trust depends on reversibility. Before **any** mutating operation (deploy, undeploy, profile switch, backend install, game-update reconciliation):

- Record a **transaction journal** entry: an ordered list of intended filesystem operations.
- Because we link rather than copy, "backup" is cheap: we snapshot the *set of junctions/links and their targets*, plus a hash manifest of any real file we do touch (the few backend-managed files like `dinput8.dll`).
- Operations are applied transactionally: on failure mid-way, the journal is replayed in reverse to restore the prior state.
- A user-visible **Restore Point** is created on profile switches and backend updates, with a one-click revert.
- Real game files we never modify need no backup — that is the whole point of the staging model.

---

## 9. Game-Update Detection (Update Watchdog)

Rockstar's periodic updates routinely break setups by changing the executable underneath the user. We detect this proactively:

- On startup (and optionally a lightweight file watcher), record the game executable's **file version + size + hash** in the active profile.
- On mismatch:
  - Surface a clear warning: *"GTAIV.exe changed (was 1.2.0.x, now 1.2.0.y). Your backend tools and some mods may no longer load."*
  - Offer to (a) re-check/refresh backend tools via the Backend Tool Manager, (b) re-validate compatibility of enabled mods, (c) create a restore point first.
- Recommend the user set Steam to "only update when launched" and document the backup-before-update workflow.

This is detection and guidance, not auto-repair — we never silently re-deploy after a game change.

---

## 10. Mod Import & Metadata

### Import flow
1. User drops a `.zip` / `.7z` / `.rar`.
2. Archive Handler extracts to a temp area with **zip-slip protection** (reject entries that escape the target dir).
3. File-type detection classifies contents (`update/`-bound assets, `.asi`, `.net`/scripts, loose data files, readme/ini).
4. Metadata Service derives a name, version, and compatibility guess (below).
5. User confirms or edits name/description, then the mod is committed to the Library.

### Compatibility scan (advisory)
Scan archive contents — folder/file names plus any `readme`, `.txt`, `.ini`, `.md` — for version tokens and classify:

- **CE** keywords: `Complete Edition`, `CE`, `1.2.0`, `FusionFix`, `FusionOverloader`.
- **Legacy** keywords: `1.0.4.0`, `1.0.7.0`, `1.0.8.0`, `downgrade`, `GFWL`.
- **Type hints**: `.asi`, `ScriptHook`, `.net` → loader requirements.

Result is shown as a non-blocking badge: **CE-compatible**, **Legacy**, **Mixed**, or **Unspecified**. It never prevents installation; it informs. (Heuristic by nature — always overridable.)

### Name / version parsing
When no explicit metadata is given, derive from the filename:

- Strip extension and known noise tokens (`final`, `release`, `fix`, build hashes).
- Normalize separators (`.`, `_`, `-`, space).
- Split a trailing version-like token from the name.

Examples:
- `ModLoader2.2` → name `ModLoader`, version `2.2`
- `Better_Handling_v1.0.8` → name `Better Handling`, version `1.0.8`
- `console-visuals-1.3.rar` → name `Console Visuals`, version `1.3`

The user can always override the parsed name/version and add a free-text description on import. Stored metadata: `name, version, description, source, importDate, compatibility, fileManifest, loaderRequirements`.

---

## 11. Conflict Detection

Per profile, maintain a map of **which mod contributes which target path**. On enable/import, flag overlaps.

- **Replacement conflicts** (two mods ship the same target file): detected directly; the active load order decides the winner, shown explicitly in the UI ("Mod B overrides Mod A at `vehicles/…`").
- **`.img`-internal conflicts**: out of scope for v1's deep handling, but detect *that* a mod targets a shared `.img` and warn that fine-grained merging isn't managed.
- **Merge-needed data files** (`handling.dat`, `weaponinfo.xml`, etc., edited by multiple mods): detect the overlap and warn that these need manual merging — last-writer-wins would silently clobber.

Conflict state and history are stored with the profile so they persist across sessions.

---

## 12. Backend Tool Management

- **Source resolution** is manifest-driven (see §15). Each backend tool entry declares a GitHub repo *and/or* a direct URL.
- **GitHub API**: query latest release; cache release metadata to respect the unauthenticated rate limit (60/hr) and let the user supply a token to raise it. Always offer the manual link as a fallback, since not everything (e.g., ScriptHook historically) lives on GitHub.
- **Integrity**: verify a checksum on every downloaded executable/DLL before use. These are files we link into the game process's load path, so provenance matters.
- **Licensing**: we **download** tools from their official sources rather than bundling them. FusionFix is GPLv3; fetching at runtime avoids redistribution entanglement and keeps tools updatable. Record each tool's license in its manifest.
- **Version tracking**: store installed backend versions per game install; the Update Watchdog can prompt refreshes after a game update.

---

## 13. Logging

Scoped honestly: we cannot hook "everything in the game" without injecting (which we delegate away). Instead the Logger provides:

- **App operation log**: every install/enable/disable/reorder/deploy/rollback, structured and timestamped — the source of truth for rollback.
- **Aggregated backend logs**: tail and present logs the other tools already emit (ASI loader log, ScriptHookDotNet log, FusionFix output, crash dumps), discovered via `IBackendAdapter.BackendLogPaths()`.
- A single searchable, filterable view with export, so a user reporting a bug can hand over one bundle.

---

## 14. Tech Stack Recommendation

Given Windows-only (now), native filesystem work, a modern UI, and strong overlap with the existing .NET-based GTA modding ecosystem:

- **Language / runtime:** **C# / .NET 8**.
- **UI:** **WPF** with a Fluent UI library (e.g., WPF-UI or ModernWpf) for a modern look without WinUI 3's packaging friction. WPF is mature, easy to publish as a single self-contained `.exe`, and well documented.
- **Filesystem links:** P/Invoke for junctions (reparse points) and `CreateHardLinkW`; `Directory.CreateSymbolicLink` for the optional elevated symlink mode.
- **GitHub:** Octokit.NET.
- **Archives:** SharpCompress (MIT) for `.zip`/`.rar` extraction; a 7-Zip binding for `.7z`. Avoid bundling the UnRAR source due to its restrictive license — prefer extraction-only paths and document RAR5 limitations.
- **Config/manifests:** JSON (System.Text.Json) or TOML.
- **Tests:** abstract all filesystem operations behind an interface so the deployment/conflict/rollback logic is unit-testable without a real game install.

Rationale over alternatives: Electron/Tauri add a web layer with weaker native-FS ergonomics for this use case; the GTA tooling community is predominantly .NET, easing contribution and interop with the ScriptHookDotNet world. (If Linux is ever reconsidered, Avalonia — also C# — is the migration path, so keeping platform-specific code behind the adapter/FS interfaces pays off.)

---

## 15. Configuration & Modularity

Nothing version- or source-specific is hardcoded. Backend tools and adapters are described by manifests the user can edit without recompiling:

```jsonc
{
  "tools": [
    {
      "id": "fusionfix",
      "name": "FusionFix (incl. FusionOverloader + DXVK)",
      "github": "ThirteenAG/GTAIV.EFLC.FusionFix",
      "directUrl": "https://…/GTAIV.EFLC.FusionFix.zip",
      "license": "GPL-3.0",
      "supportedVersions": ["CE"],
      "installTo": "<gameDir>"
    },
    {
      "id": "asiloader",
      "name": "Ultimate ASI Loader",
      "github": "ThirteenAG/Ultimate-ASI-Loader",
      "license": "…",
      "installTo": "<gameDir>"
    }
  ],
  "adapters": {
    "CE": {
      "updateFolder": "<gameDir>/update",
      "asiFolder": "<gameDir>/plugins",
      "scriptFolder": "<gameDir>/scripts",
      "priorityMechanism": "folderPrefix"   // or "modloaderIni"
    }
  },
  "paths": { "gameDir": "", "libraryDir": "" }
}
```

User-overridable at runtime: game directory, library directory, backend download URLs, the `update`/`plugins`/`scripts` paths, and the priority mechanism.

---

## 16. Security Considerations

- HTTPS-only downloads; checksum verification before any downloaded binary is used.
- Zip-slip / path-traversal protection on all archive extraction.
- No elevation required by design (junctions + hardlinks) — reduces attack surface and friction.
- Clear provenance display for every backend tool (source URL, version, license).
- Code-sign the released `.exe` to avoid SmartScreen friction and reinforce trust.

---

## 17. Roadmap

**Milestone 1 — Foundation (CE, loose-file mods)**
Version detection, library import (zip/7z/rar) with metadata parsing, profiles, junction/hardlink deployment to `update/`/`plugins/`/`scripts/`, enable/disable, backup/rollback, app logging.

**Milestone 2 — Orchestration & safety**
Backend Tool Manager (GitHub + links + checksums), load-order UI for assets and `.asi`, conflict detection, game-update watchdog, aggregated backend logs.

**Milestone 3 — Polish & release**
Fluent UI pass, configurable manifests surfaced in settings, code signing + GitHub Actions CI for releases, docs.

**Future**
Legacy backend adapter (1.0.7.0/1.0.8.0), deeper `.img` / merge-file handling, possible Avalonia/Linux exploration.

---

## 18. Open Questions

- Exact FusionOverloader subfolder/`modloader.ini` semantics to standardize on (verify against the installed version at runtime).
- EFLC episodes (IV / TLAD / TBoGT) targeting — FusionOverloader supports per-subgame folders; decide how/whether to expose this in v1.
- Whether to manage `.asi` load order beyond presence (loader-dependent).
- Strategy for mods distributed as raw `.img` repacks (warn-and-skip vs. partial support).
