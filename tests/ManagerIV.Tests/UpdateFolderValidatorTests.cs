using ManagerIV.Core;

namespace ManagerIV.Tests;

public class UpdateFolderValidatorTests
{
    private readonly UpdateFolderValidator _validator;

    public UpdateFolderValidatorTests()
    {
        _validator = new UpdateFolderValidator();
    }

    [Fact]
    public void TestValidModStructureReturnsNoIssues()
    {
        // Arrange: Mod with standard structured paths (compiled .img and loose files)
        var files = new List<ModFile>
        {
            new ModFile("custom_vehicles.img", 1024, null),
            new ModFile("common/data/handling.dat", 2048, null),
            new ModFile("tlad/common/data/handling.dat", 512, null)
        };
        var mod = new StagedMod(
            Id: "test-valid",
            Name: "Valid Mod",
            Version: "1.0",
            Description: "A valid mod structure",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void TestFolderBasedArchiveValidation()
    {
        // Arrange: .img folders are virtual roots handled by Fusion Overloader,
        // while .rpf folders still require compiled archives.
        var files = new List<ModFile>
        {
            new ModFile("pc/models/cdimages/vehicles.img/infernus.dff", 1024, null),
            new ModFile("pc/data/scripts.rpf/custom.dat", 512, null)
        };
        var mod = new StagedMod(
            Id: "test-folder-archive",
            Name: "Folder-based Archive Mod",
            Version: "1.0",
            Description: "Folder-based archives",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert
        Assert.NotEmpty(issues);
        Assert.DoesNotContain(issues, i => i.Severity == "Error" && i.Message.Contains("Folder-based .img archive"));
        Assert.Contains(issues, i => i.Severity == "Error" && i.Message.Contains("Folder-based .rpf archive"));
    }

    [Fact]
    public void TestCustomVirtualPathWithImgFolderReturnsNoIssues()
    {
        // Arrange: UAL virtual path layout under update/<NNN>_<ModName>/
        var files = new List<ModFile>
        {
            new ModFile("LibertyAlive/LibertyAlive.img/infernus.dff", 1024, null),
            new ModFile("LibertyAlive/LibertyAlive.img/infernus.wtd", 2048, null)
        };
        var mod = new StagedMod(
            Id: "test-ual-img-folder",
            Name: "UAL Img Folder Mod",
            Version: "1.0",
            Description: "Fusion Overloader virtual path layout",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void TestCustomVirtualSubdirectoryReturnsNoIssues()
    {
        // Arrange: Non-native folders can be UAL virtual roots.
        var files = new List<ModFile>
        {
            new ModFile("LibertyAlive/common/data/handling.dat", 1024, null)
        };
        var mod = new StagedMod(
            Id: "test-ual-custom-folder",
            Name: "UAL Custom Folder Mod",
            Version: "1.0",
            Description: "Fusion Overloader custom virtual root",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void TestLooseAssetFileWarning()
    {
        // Arrange: Mod placing asset files directly in root (which are ignored by FusionOverloader)
        var files = new List<ModFile>
        {
            new ModFile("infernus.wft", 1024, null),
            new ModFile("infernus.wtd", 2048, null)
        };
        var mod = new StagedMod(
            Id: "test-loose",
            Name: "Loose Asset Mod",
            Version: "1.0",
            Description: "Loose assets outside archives",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "Unspecified"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert: We should have warnings about loose asset files and a final structure error
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Severity == "Warning" && i.Message.Contains("Loose asset file"));
        Assert.Contains(issues, i => i.Severity == "Warning" && i.Message.Contains("No .img/.rpf folders"));
    }

    [Fact]
    public void TestImgBasenameCollisionWarning()
    {
        // Arrange: Mod with duplicate filenames under the same .img folder structure
        var files = new List<ModFile>
        {
            new ModFile("pc/models/cdimages/vehicles.img/subfolder1/infernus.dff", 1024, null),
            new ModFile("pc/models/cdimages/vehicles.img/subfolder2/infernus.dff", 2048, null)
        };
        var mod = new StagedMod(
            Id: "test-collision",
            Name: "Collision Mod",
            Version: "1.0",
            Description: "Duplicate basenames in IMG",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert: Verify basename collision warning is flagged
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Severity == "Warning" && i.Message.Contains("Duplicate filename") && i.Message.Contains("infernus.dff"));
    }

    [Fact]
    public void TestImgFileSizeLimitError()
    {
        // Arrange: File exceeds maximum IMG entry size (134 MB)
        long tooBig = 135 * 1024 * 1024; // 135 MB
        var files = new List<ModFile>
        {
            new ModFile("pc/models/cdimages/vehicles.img/huge_texture.txd", tooBig, null)
        };
        var mod = new StagedMod(
            Id: "test-size",
            Name: "Too Big Mod",
            Version: "1.0",
            Description: "Exceeds IMG entry size limit",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert: Verify error limit is reached
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Severity == "Error" && i.Message.Contains("exceeds the IMG limit"));
    }

    [Fact]
    public void TestRpfFileSizeLimitError()
    {
        // Arrange: File exceeds maximum Rpf size limit (2 GB)
        long tooBig = 3L * 1024 * 1024 * 1024; // 3 GB
        var files = new List<ModFile>
        {
            new ModFile("pc/data/scripts.rpf/huge_file.dat", tooBig, null)
        };
        var mod = new StagedMod(
            Id: "test-rpf-size",
            Name: "Too Big Rpf Mod",
            Version: "1.0",
            Description: "Exceeds RPF entry size limit",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert: Verify error limit is reached
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Severity == "Error" && i.Message.Contains("exceeds the RPF limit"));
    }

    [Fact]
    public void TestSubgameFolderValidation()
    {
        // Arrange: Mod with subgame folders
        var files = new List<ModFile>
        {
            new ModFile("iv/custom_vehicles.img", 1024, null),
            new ModFile("tlad/common/data/handling.dat", 2048, null),
            new ModFile("tbogt/pc/models/cdimages/vehicles.img", 512, null)
        };
        var mod = new StagedMod(
            Id: "test-subgame",
            Name: "Subgame Mod",
            Version: "1.0",
            Description: "A mod with subgame targets",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void TestFusionFixProtection()
    {
        // Arrange: Mod attempting to write to update/GTAIV.EFLC.FusionFix/
        var files = new List<ModFile>
        {
            new ModFile("GTAIV.EFLC.FusionFix/unwanted_file.txt", 1024, null)
        };
        var mod = new StagedMod(
            Id: "test-ff-hack",
            Name: "FusionFix Intruder",
            Version: "1.0",
            Description: "Attempts to write to FusionFix folder",
            LibraryPath: "C:/DummyModPath",
            Files: files,
            IsEnabled: true,
            Compatibility: "CE-compatible"
        );

        // Act
        var issues = _validator.Validate(mod);

        // Assert
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Severity == "Error" && i.Message.Contains("reserved path"));
    }
}
