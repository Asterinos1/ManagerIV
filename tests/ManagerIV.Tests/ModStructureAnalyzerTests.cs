using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class ModStructureAnalyzerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly ModStructureAnalyzer _analyzer;
    private readonly ArchiveHandler _archiveHandler;

    public ModStructureAnalyzerTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "GtaIVModStructureTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testBaseDir);
        _analyzer = new ModStructureAnalyzer();
        _archiveHandler = new ArchiveHandler();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBaseDir))
        {
            try { Directory.Delete(_testBaseDir, true); } catch { }
        }
    }

    private string CreateZipArchive(string archiveName, Action<ZipArchive> buildZip)
    {
        string archivePath = Path.Combine(_testBaseDir, archiveName);
        using var fs = new FileStream(archivePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        buildZip(zip);
        return archivePath;
    }

    [Fact]
    public async Task TestFlatAsiAtRoot()
    {
        string zipPath = CreateZipArchive("flat_asi.zip", zip =>
        {
            var entry = zip.CreateEntry("BetterHandling.asi");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("dummy asi data");
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Single(report.DetectedTargets);
        var group = report.DetectedTargets[0];
        Assert.Equal(DeploymentTarget.PluginsFolder, group.Target);
        Assert.Equal("", group.SourcePathPrefix);
        Assert.Contains("BetterHandling.asi", group.Entries);
        Assert.Empty(report.UnresolvedFiles);
        Assert.False(report.IsDualTarget);
    }

    [Fact]
    public async Task TestUpdateFolderStructure()
    {
        string zipPath = CreateZipArchive("update_folder.zip", zip =>
        {
            var entry = zip.CreateEntry("update/pc/models/cdimages/vehicles.img");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("dummy img data");
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Single(report.DetectedTargets);
        var group = report.DetectedTargets[0];
        Assert.Equal(DeploymentTarget.UpdateFolder, group.Target);
        Assert.Equal("update/", group.SourcePathPrefix);
        Assert.Contains("update/pc/models/cdimages/vehicles.img", group.Entries);
        Assert.Empty(report.UnresolvedFiles);
        Assert.Equal(VersionCompatibility.CompleteEditionOnly, report.VersionCompatibility);
        Assert.False(report.IsDualTarget);
    }

    [Fact]
    public async Task TestUpdateFolderVirtualPathStructure()
    {
        string zipPath = CreateZipArchive("update_virtual_path_folder.zip", zip =>
        {
            var entry = zip.CreateEntry("Release/update/LibertyAlive/LibertyAlive.img/infernus.dff");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("dummy model data");
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Single(report.DetectedTargets);
        var group = report.DetectedTargets[0];
        Assert.Equal(DeploymentTarget.UpdateFolder, group.Target);
        Assert.Equal("Release/update/", group.SourcePathPrefix);
        Assert.Contains("Release/update/LibertyAlive/LibertyAlive.img/infernus.dff", group.Entries);
        Assert.Empty(report.UnresolvedFiles);
        Assert.Equal(VersionCompatibility.CompleteEditionOnly, report.VersionCompatibility);
        Assert.False(report.IsDualTarget);
    }

    [Fact]
    public async Task TestDualTargetDetection()
    {
        string zipPath = CreateZipArchive("dual_target.zip", zip =>
        {
            var entry1 = zip.CreateEntry("update/pc/models/cdimages/vehicles.img");
            using (var writer = new StreamWriter(entry1.Open())) { writer.Write("dummy img"); }

            var entry2 = zip.CreateEntry("plugins/BetterHandling.asi");
            using (var writer = new StreamWriter(entry2.Open())) { writer.Write("dummy asi"); }
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.True(report.IsDualTarget);
        Assert.Contains(report.DetectedTargets, g => g.Target == DeploymentTarget.UpdateFolder);
        Assert.Contains(report.DetectedTargets, g => g.Target == DeploymentTarget.PluginsFolder);
    }

    [Fact]
    public async Task TestReadmeDependencyExtraction()
    {
        string zipPath = CreateZipArchive("readme_dep.zip", zip =>
        {
            var entry1 = zip.CreateEntry("BetterHandling.asi");
            using (var writer = new StreamWriter(entry1.Open())) { writer.Write("dummy asi"); }

            var readme = zip.CreateEntry("readme.txt");
            using (var writer = new StreamWriter(readme.Open()))
            {
                writer.Write("Requires Ultimate ASI Loader and FusionFix to run.");
            }
        });

        var toolsContext = new InstalledToolsContext(true, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Contains(report.Dependencies, d => d.Name == "Ultimate ASI Loader" && d.IsInstalled);
        Assert.Contains(report.Dependencies, d => d.Name == "FusionFix" && !d.IsInstalled);
    }

    [Fact]
    public async Task TestConflictHintExtraction()
    {
        string zipPath = CreateZipArchive("readme_conflict.zip", zip =>
        {
            var entry1 = zip.CreateEntry("BetterHandling.asi");
            using (var writer = new StreamWriter(entry1.Open())) { writer.Write("dummy asi"); }

            var readme = zip.CreateEntry("readme.txt");
            using (var writer = new StreamWriter(readme.Open()))
            {
                writer.Write("Installation notes: Do not use with ModX. It conflicts with other vehicles mods. Not compatible with EFLC.");
            }
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Contains(report.ConflictHints, h => h.Contains("Do not use with ModX"));
        Assert.Contains(report.ConflictHints, h => h.Contains("conflicts with other vehicles mods"));
        Assert.Contains(report.ConflictHints, h => h.Contains("Not compatible with EFLC"));
    }

    [Fact]
    public async Task TestUnresolvedFileSurfacing()
    {
        string zipPath = CreateZipArchive("unresolved.zip", zip =>
        {
            var entry = zip.CreateEntry("unrelated_folder/somefile.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("unresolved file content");
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Empty(report.DetectedTargets);
        Assert.Contains("unrelated_folder/somefile.txt", report.UnresolvedFiles);

        // Test extraction skips unresolved files
        string extDir = Path.Combine(_testBaseDir, "unresolved_ext");
        await _archiveHandler.ExtractAsync(zipPath, extDir, report);
        
        Assert.True(Directory.Exists(extDir));
        Assert.Empty(Directory.GetFiles(extDir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task TestLegacyKeywordDetection()
    {
        string zipPath = CreateZipArchive("legacy_readme.zip", zip =>
        {
            var entry1 = zip.CreateEntry("BetterHandling.asi");
            using (var writer = new StreamWriter(entry1.Open())) { writer.Write("dummy asi"); }

            var readme = zip.CreateEntry("readme.txt");
            using (var writer = new StreamWriter(readme.Open()))
            {
                writer.Write("Compatible with legacy game version 1.0.7.0.");
            }
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Equal(VersionCompatibility.LegacyOnly, report.VersionCompatibility);
    }

    [Fact]
    public async Task TestVersionCompatibilityBothKeywords()
    {
        string zipPath = CreateZipArchive("both_readme.zip", zip =>
        {
            var entry1 = zip.CreateEntry("BetterHandling.asi");
            using (var writer = new StreamWriter(entry1.Open())) { writer.Write("dummy asi"); }

            var readme = zip.CreateEntry("readme.txt");
            using (var writer = new StreamWriter(readme.Open()))
            {
                writer.Write("Tested on complete edition (1.2.0.4) and 1.0.8.0 legacy.");
            }
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.Equal(VersionCompatibility.Both, report.VersionCompatibility);
    }

    [Fact]
    public async Task TestArchiveHandlerExtractionWithReport()
    {
        string zipPath = CreateZipArchive("extraction_test.zip", zip =>
        {
            // Update folder group
            var entry1 = zip.CreateEntry("MyMod/update/pc/models/cdimages/vehicles.img");
            using (var writer = new StreamWriter(entry1.Open())) { writer.Write("vehicles data"); }

            // Plugins group
            var entry2 = zip.CreateEntry("MyMod/plugins/BetterHandling.asi");
            using (var writer = new StreamWriter(entry2.Open())) { writer.Write("asi data"); }

            // Unresolved file
            var entry3 = zip.CreateEntry("MyMod/license.txt");
            using (var writer = new StreamWriter(entry3.Open())) { writer.Write("MIT License"); }
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        // Verify analyzer targets
        Assert.True(report.IsDualTarget);
        Assert.Equal(2, report.DetectedTargets.Count);
        
        var updateGroup = report.DetectedTargets.First(g => g.Target == DeploymentTarget.UpdateFolder);
        Assert.Equal("MyMod/update/", updateGroup.SourcePathPrefix);
        
        var pluginsGroup = report.DetectedTargets.First(g => g.Target == DeploymentTarget.PluginsFolder);
        Assert.Equal("MyMod/plugins/", pluginsGroup.SourcePathPrefix);

        // Test extracting only CE
        string extDirCe = Path.Combine(_testBaseDir, "ExtractedCE");
        await _archiveHandler.ExtractAsync(zipPath, extDirCe, report, VersionCompatibility.CompleteEditionOnly);

        Assert.True(File.Exists(Path.Combine(extDirCe, "pc/models/cdimages/vehicles.img")));
        Assert.True(File.Exists(Path.Combine(extDirCe, "BetterHandling.asi")));
        Assert.False(File.Exists(Path.Combine(extDirCe, "license.txt")));

        // Test extracting only Legacy
        string extDirLegacy = Path.Combine(_testBaseDir, "ExtractedLegacy");
        await _archiveHandler.ExtractAsync(zipPath, extDirLegacy, report, VersionCompatibility.LegacyOnly);

        Assert.False(File.Exists(Path.Combine(extDirLegacy, "pc/models/cdimages/vehicles.img")));
        Assert.True(File.Exists(Path.Combine(extDirLegacy, "BetterHandling.asi")));
        Assert.False(File.Exists(Path.Combine(extDirLegacy, "license.txt")));
    }

    [Fact]
    public async Task TestUserZipDualTargetExtraction()
    {
        string zipPath = CreateZipArchive("user_dual_target.zip", zip =>
        {
            var e1 = zip.CreateEntry("file.txt");
            using (var w = new StreamWriter(e1.Open())) { w.Write("text"); }

            var e2 = zip.CreateEntry("file2.txt");
            using (var w = new StreamWriter(e2.Open())) { w.Write("text2"); }

            var e3 = zip.CreateEntry("file3.pdf");
            using (var w = new StreamWriter(e3.Open())) { w.Write("pdf"); }

            var e4 = zip.CreateEntry("For Regular GTAIV/Main Files/common/data/handling.dat");
            using (var w = new StreamWriter(e4.Open())) { w.Write("legacy handling"); }

            var e5 = zip.CreateEntry("For GTA IV w. FusionFix installed/update/common/data/handling.dat");
            using (var w = new StreamWriter(e5.Open())) { w.Write("ce handling"); }
        });

        var toolsContext = new InstalledToolsContext(false, false, false);
        var report = await _analyzer.AnalyzeAsync(zipPath, toolsContext);

        Assert.True(report.IsDualTarget);

        // Extract for Complete Edition
        string extDirCe = Path.Combine(_testBaseDir, "UserExtractedCE");
        await _archiveHandler.ExtractAsync(zipPath, extDirCe, report, VersionCompatibility.CompleteEditionOnly);

        // Verify that only the update folder content is extracted and stripped of the prefix
        Assert.True(File.Exists(Path.Combine(extDirCe, "common/data/handling.dat")));
        Assert.Equal("ce handling", File.ReadAllText(Path.Combine(extDirCe, "common/data/handling.dat")));

        // Verify other files/folders are not extracted
        Assert.False(File.Exists(Path.Combine(extDirCe, "file.txt")));
        Assert.False(File.Exists(Path.Combine(extDirCe, "file2.txt")));
        Assert.False(File.Exists(Path.Combine(extDirCe, "file3.pdf")));
        Assert.False(Directory.Exists(Path.Combine(extDirCe, "For Regular GTAIV")));
    }
}
