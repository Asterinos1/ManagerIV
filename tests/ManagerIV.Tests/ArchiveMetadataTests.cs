using System.IO;
using System.IO.Compression;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class ArchiveMetadataTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _zipFilePath;
    private readonly string _zipSlipFilePath;
    private readonly string _extractionDir;
    private readonly ArchiveHandler _archiveHandler;
    private readonly MetadataService _metadataService;

    public ArchiveMetadataTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "GtaIVArchiveTests_" + Guid.NewGuid().ToString("N"));
        _zipFilePath = Path.Combine(_testBaseDir, "Better_Handling_v1.0.8_final.zip");
        _zipSlipFilePath = Path.Combine(_testBaseDir, "ZipSlip_Attack.zip");
        _extractionDir = Path.Combine(_testBaseDir, "Extracted");

        Directory.CreateDirectory(_testBaseDir);

        _archiveHandler = new ArchiveHandler();
        _metadataService = new MetadataService();

        CreateValidTestZip();
        CreateZipSlipZip();
    }

    private void CreateValidTestZip()
    {
        using var fs = new FileStream(_zipFilePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        // Add nested .asi file
        var asiEntry = zip.CreateEntry("plugins/better_handling.asi");
        using (var writer = new StreamWriter(asiEntry.Open()))
        {
            writer.Write("[HandlingData]\nVehicle=Turismo\nMass=1400");
        }

        // Add readme with CE keywords
        var readmeEntry = zip.CreateEntry("readme.txt");
        using (var writer = new StreamWriter(readmeEntry.Open()))
        {
            writer.Write("This is a handling mod for GTA IV Complete Edition.\nRequires FusionOverloader and FusionFix.");
        }
    }

    private void CreateZipSlipZip()
    {
        using var fs = new FileStream(_zipSlipFilePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        // Add a malicious entry with path traversal sequence
        var maliciousEntry = zip.CreateEntry("../malicious.txt");
        using (var writer = new StreamWriter(maliciousEntry.Open()))
        {
            writer.Write("Malicious file content.");
        }
    }

    private void CreateNestedTestZip(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var imgEntry = zip.CreateEntry("MyVehiclesMod/pc/models/cdimages/vehicles.img");
        using (var writer = new StreamWriter(imgEntry.Open()))
        {
            writer.Write("dummy img data");
        }

        var readmeEntry = zip.CreateEntry("MyVehiclesMod/readme.txt");
        using (var writer = new StreamWriter(readmeEntry.Open()))
        {
            writer.Write("readme contents");
        }
    }

    private void CreateUpdateNestedTestZip(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var datEntry = zip.CreateEntry("ReleaseFiles/SubDir/update/pc/data/scripts.rpf/custom.dat");
        using (var writer = new StreamWriter(datEntry.Open()))
        {
            writer.Write("dummy dat data");
        }

        var docEntry = zip.CreateEntry("ReleaseFiles/SubDir/readme.txt");
        using (var writer = new StreamWriter(docEntry.Open()))
        {
            writer.Write("instructions");
        }
    }

    private void CreateUpdateVirtualPathZip(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var standardEntry = zip.CreateEntry("ReleaseFiles/update/pc/data/default.dat");
        using (var writer = new StreamWriter(standardEntry.Open()))
        {
            writer.Write("standard data");
        }

        var commonEntry = zip.CreateEntry("ReleaseFiles/update/common/data/handling.dat");
        using (var writer = new StreamWriter(commonEntry.Open()))
        {
            writer.Write("handling data");
        }

        var customEntry = zip.CreateEntry("ReleaseFiles/update/LibertyAlive/config.ini");
        using (var writer = new StreamWriter(customEntry.Open()))
        {
            writer.Write("custom config");
        }

        var imgFolderEntry = zip.CreateEntry("ReleaseFiles/update/LibertyAlive/LibertyAlive.img/custom.dat");
        using (var writer = new StreamWriter(imgFolderEntry.Open()))
        {
            writer.Write("img folder data");
        }
    }

    private void CreateImgFolderZip(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var entry = zip.CreateEntry("LibertyAlive.img/custom.dat");
        using (var writer = new StreamWriter(entry.Open()))
        {
            writer.Write("dummy dat data");
        }
    }

    [Fact]
    public async Task TestSafeExtractionAndMetadataParsing()
    {
        // Act: Extract
        await _archiveHandler.ExtractAsync(_zipFilePath, _extractionDir);

        // Assert: Extraction outputs exist and path structure matches
        string extractedAsi = Path.Combine(_extractionDir, "plugins", "better_handling.asi");
        string extractedReadme = Path.Combine(_extractionDir, "readme.txt");

        Assert.True(File.Exists(extractedAsi), "Extracted ASI plugin should exist.");
        Assert.True(File.Exists(extractedReadme), "Extracted readme file should exist.");

        // Act: Metadata Scan
        var metadata = _metadataService.ScanExtractedDirectory(_extractionDir, Path.GetFileName(_zipFilePath));

        // Assert: Filename parsing
        Assert.Equal("Better Handling", metadata.Name);
        Assert.Equal("1.0.8", metadata.Version);

        // Assert: Compatibility Guessing
        Assert.Equal("CE-compatible", metadata.Compatibility);
        Assert.Contains("ASI Loader", metadata.LoaderRequirements);

        // Assert: Manifest correctness
        Assert.Contains("plugins/better_handling.asi", metadata.FileManifest);
        Assert.Contains("readme.txt", metadata.FileManifest);
    }

    [Fact]
    public async Task TestZipSlipProtection()
    {
        // Act & Assert: Verify that zip-slip attempts throw an InvalidOperationException
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _archiveHandler.ExtractAsync(_zipSlipFilePath, _extractionDir);
        });

        Assert.Contains("Zip-Slip attack", exception.Message);
        
        // Assert: Verify no files were extracted outside the extraction folder
        string parentDir = Path.GetDirectoryName(_extractionDir)!;
        string maliciousFileOut = Path.Combine(parentDir, "malicious.txt");
        Assert.False(File.Exists(maliciousFileOut), "Zip-Slip file should NOT be created outside the target extraction root.");
    }

    [Theory]
    [InlineData("Better_Handling_v1.0.8.zip", "Better Handling", "1.0.8")]
    [InlineData("console-visuals-1.3.rar", "Console Visuals", "1.3")]
    [InlineData("ModLoader2.2.7z", "ModLoader", "2.2")]
    [InlineData("Ultimate_Asi_Loader_v2.0.1_release.zip", "Ultimate Asi Loader", "2.0.1")]
    [InlineData("fix_handling_final_v3.4.5_build123a.zip", "Fix Handling", "3.4.5")]
    [InlineData("Better_Handling-v1.0.8-fix.zip", "Better Handling", "1.0.8")]
    [InlineData("LibertyCity-v2.0-beta.zip", "LibertyCity", "2.0")]
    [InlineData("MyMod-V1.0.8-Description.zip", "MyMod", "1.0.8")]
    [InlineData("ScriptHook-v1.0-.zip", "ScriptHook", "1.0")]
    public void TestFilenameParsingEdgeCases(string fileName, string expectedName, string expectedVersion)
    {
        var (parsedName, parsedVersion) = _metadataService.ParseFilename(fileName);

        Assert.Equal(expectedName, parsedName);
        Assert.Equal(expectedVersion, parsedVersion);
    }

    [Fact]
    public async Task TestModRootPromotion()
    {
        // Arrange
        string nestedZipPath = Path.Combine(_testBaseDir, "nested_mod.zip");
        string extractionDir = Path.Combine(_testBaseDir, "NestedExtracted");
        CreateNestedTestZip(nestedZipPath);

        // Act
        await _archiveHandler.ExtractAsync(nestedZipPath, extractionDir);
        _archiveHandler.PromoteModRoot(extractionDir);

        // Assert
        string nestedFolder = Path.Combine(extractionDir, "MyVehiclesMod");
        string promotedImg = Path.Combine(extractionDir, "pc", "models", "cdimages", "vehicles.img");
        string promotedReadme = Path.Combine(extractionDir, "readme.txt");

        Assert.False(Directory.Exists(nestedFolder), "The original nested folder should be removed.");
        Assert.True(File.Exists(promotedImg), "The image file should be promoted to the root.");
        Assert.True(File.Exists(promotedReadme), "The readme file should be promoted to the root.");
    }

    [Fact]
    public async Task TestModRootPromotionWithUpdateFolder()
    {
        // Arrange
        string nestedZipPath = Path.Combine(_testBaseDir, "update_nested_mod.zip");
        string extractionDir = Path.Combine(_testBaseDir, "UpdateNestedExtracted");
        CreateUpdateNestedTestZip(nestedZipPath);

        // Act
        await _archiveHandler.ExtractAsync(nestedZipPath, extractionDir);
        _archiveHandler.PromoteModRoot(extractionDir);

        // Assert
        string nestedFolder = Path.Combine(extractionDir, "ReleaseFiles");
        string promotedDat = Path.Combine(extractionDir, "pc", "data", "scripts.rpf", "custom.dat");

        Assert.False(Directory.Exists(nestedFolder), "The original nested folders should be cleaned up.");
        Assert.True(File.Exists(promotedDat), "The files inside the nested 'update' folder should be promoted to the root.");
    }

    [Fact]
    public async Task TestModRootPromotionPreservesUpdateVirtualPathHierarchy()
    {
        // Arrange
        string nestedZipPath = Path.Combine(_testBaseDir, "update_virtual_path_mod.zip");
        string extractionDir = Path.Combine(_testBaseDir, "UpdateVirtualPathExtracted");
        CreateUpdateVirtualPathZip(nestedZipPath);

        // Act
        await _archiveHandler.ExtractAsync(nestedZipPath, extractionDir);
        _archiveHandler.PromoteModRoot(extractionDir);

        // Assert
        string promotedDat = Path.Combine(extractionDir, "LibertyAlive", "LibertyAlive.img", "custom.dat");
        string promotedPcFile = Path.Combine(extractionDir, "pc", "data", "default.dat");
        string promotedCommonFile = Path.Combine(extractionDir, "common", "data", "handling.dat");
        string promotedCustomFile = Path.Combine(extractionDir, "LibertyAlive", "config.ini");

        Assert.True(File.Exists(promotedDat), "The custom virtual path below update/ should be preserved exactly.");
        Assert.True(File.Exists(promotedPcFile), "Standard folders below update/ should be elevated with custom folders.");
        Assert.True(File.Exists(promotedCommonFile), "Common folders below update/ should remain in the same staged mod root.");
        Assert.True(File.Exists(promotedCustomFile), "Custom folders below update/ should remain unified with standard folders.");
        Assert.False(Directory.Exists(Path.Combine(extractionDir, "update")), "The update wrapper itself should be stripped.");
    }

    [Fact]
    public async Task TestModRootPromotionDoesNotFlattenImgDirectories()
    {
        // Arrange
        string nestedZipPath = Path.Combine(_testBaseDir, "img_folder_mod.zip");
        string extractionDir = Path.Combine(_testBaseDir, "ImgFolderExtracted");
        CreateImgFolderZip(nestedZipPath);

        // Act
        await _archiveHandler.ExtractAsync(nestedZipPath, extractionDir);
        _archiveHandler.PromoteModRoot(extractionDir);

        // Assert
        string preservedImgFolderFile = Path.Combine(extractionDir, "LibertyAlive.img", "custom.dat");

        Assert.True(File.Exists(preservedImgFolderFile), ".img-suffixed directories should remain normal folders.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in test teardown
        }
    }
}
