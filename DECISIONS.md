# Decisions Log — GTA IV Mod Loader

This file logs key design decisions and corrections made to the codebase to align it with project specifications and resolve bugs.

## [Stage 4 & 5] Priority Sort Direction Correction

**Original design:** In `ConflictDetector.DetectConflicts()`, the winner of a file-level conflict was determined as the last item in ascending priority order (`modIds[^1]`). This meant the mod with the highest priority number (lowest precedence) would override mods with lower priority numbers.
**What was implemented:** The winner is now determined as `modIds[0]` (the first item in priority order).
**Reason:** The spec states that folders that sort earlier alphabetically (e.g. `001_` before `002_`) win. Therefore, the mod with Priority 1 (lowest number) gets the highest actual precedence and wins. This correction ensures that conflict resolution logic aligns with Fusion Overloader's alphabetical merging engine.

## [Stage 5] Staged Mod Subgame Folder Handling & IsLooseUpdateFile Correction

**Original design:** `IsLooseUpdateFile()` checked `!path.EndsWith(".img") && !path.Contains(".img/")` to determine if a file should be deployed via a junction or hardlinked. Folder-based `.img` archives are not supported, and the `.img/` folder check would incorrectly treat loose files inside subgame folders as loose and hardlink them directly to the base game path (e.g., `update/IV/common/data/handling.dat`).
**What was implemented:** Modified `IsLooseUpdateFile()` to:
1. Treat any path starting with `iv/`, `tlad/`, or `tbogt/` as a non-loose file (returning `false`), forcing the mod to be deployed via a directory junction to prevent invalid hardlink targets.
2. Check only `!path.EndsWith(".img")`. Folder-based `.img` archives are already validated and flagged by the `UpdateFolderValidator`.
**Reason:** Subgame directories (`IV/`, `TLAD/`, `TBoGT/`) inside the mod folder are processed by FusionOverloader via the mod folder junction. Hardlinking files directly to `update/IV/...` is incorrect. This change ensures that any mod targeting specific subgames is deployed as a junction so that files reside in `update/<ModFolderName>/IV/...` where they are correctly resolved.

## [Stage 5] Enforcing Conflict Resolution & Error Blocking on Deployment

**Original design:** `DeployAsync` would silently overwrite loose files in the `update/` directory when there were conflicts, and would ignore mod structure validation errors.
**What was implemented:** Modified `ApplyDeploymentAsync` in `MainViewModel` to validate the structure of all enabled mods and check for loose file conflicts before performing any deployment or teardown. If validation errors or loose file conflicts are found, deployment is aborted with an error message. Also added a safety check in `CompleteEditionAdapter.DeployAsync` to throw if a file attempts to write into the reserved `update/GTAIV.EFLC.FusionFix` folder.
**Reason:** The spec states that there is no automatic priority system for loose files, conflicts must be manually merged, and validation errors should block deployment. Aborting deployment prevents silent file overwrites and protects the user's mod environment.

## [Stage 5] Protection for reserved `update/GTAIV.EFLC.FusionFix/` Directory

**Original design:** Mods could write files directly into the reserved `update/GTAIV.EFLC.FusionFix/` directory.
**What was implemented:**
1. Added validation rule in `UpdateFolderValidator` to flag writing to `GTAIV.EFLC.FusionFix/` (including under subgame directories) as a structural error.
2. Added guards in `CompleteEditionAdapter.DeployAsync` to throw if a junction or hardlink targets the reserved path.
**Reason:** Protects the core FusionFix installation files from being overwritten or modified by other mods.

## [Stage 6] Batch Mod Archive Import Support

**Original design:** Mod archive ingestion was designed to import single mod files at a time. The file picker had `Multiselect = false`, and dropping multiple files on the window resulted in triggering parallel, un-awaited async tasks (`_ = vm.ImportArchiveAsync(file)`) which caused race conditions on UI state properties (like `IsBusy` and `StatusText`).
**What was implemented:**
1. Modified `PromptAndImportArchiveAsync` to set `Multiselect = true` on the `OpenFileDialog`.
2. Created a new batch processing method `ImportArchivesAsync(IEnumerable<string> archivePaths)` that processes archives sequentially in a loop, showing progress, collecting individual errors, and refreshing lists and saving library metadata once after the entire batch is completed.
3. Updated the drag-and-drop handler (`HandleFileDrop`) in `ModLibraryView.xaml.cs` to collect all dropped archive files and invoke the batch method `ImportArchivesAsync`.
4. Refactored `ImportArchiveAsync` to delegate to `ImportArchivesAsync` to prevent code duplication.
5. Added a unit test `TestImportMultipleArchivesAsync` to verify batch archive imports.
**Reason:** Allows users to efficiently batch import dozens of mods simultaneously via both the selection dialog and drag-and-drop, avoiding race conditions and ensuring deterministic sequential execution.


