using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class MetadataServiceFileNameParseTests
{
    private readonly MetadataService _metadataService = new();

    [Theory]
    [InlineData("HD NOOSE Headgear for Niko.rar", "HD NOOSE Headgear for Niko", null, "")]
    [InlineData("1404087540_1403961752_FaF Eclipse v2.rar", "FaF Eclipse", "2", "")]
    [InlineData("1780402774_Mitsubisi_FF_01.zip", "Mitsubisi FF 01", null, "")]
    [InlineData("GCU_Car_Pack_v1.2.2_manual-371-1-2-2-1701171631.rar", "GCU Car Pack", "1.2.2", "")]
    [InlineData("Realistic Handling and Physics v1.1-195-1-1-1671355668.zip", "Realistic Handling and Physics", "1.1", "")]
    [InlineData("GTA IV DXVK 2.8.1 Stable-188-2-8-1-Stable-1772181141.zip", "GTA IV DXVK", "2.8.1", "")]
    [InlineData("Rivers of Blood v 8.1 HD Fusion Fix-263-9-1-1772330775.rar", "Rivers of Blood", "8.1", "")]
    [InlineData("Higher Resolution Miscellaneous Pack v2.0-357-2-0-1735494802.zip", "Higher Resolution Miscellaneous Pack", "2.0", "")]
    [InlineData("Installation through Fusion Overloader.zip", "Installation Through Fusion Overloader", null, "")]
    [InlineData("1738616364_Liberty Alive V2 - BUSTED.rar", "Liberty Alive", "2", "")]
    public void TestParseArchiveFileName(string inputFilename, string expectedDisplayName, string expectedVersion, string expectedTagsCommaSeparated)
    {
        // Act
        var result = _metadataService.ParseArchiveFileName(inputFilename);

        // Assert
        Assert.Equal(expectedDisplayName, result.DisplayName);
        Assert.Equal(expectedVersion, result.Version);

        var expectedTags = string.IsNullOrEmpty(expectedTagsCommaSeparated)
            ? Array.Empty<string>()
            : expectedTagsCommaSeparated.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(expectedTags.Length, result.Tags.Count);
        for (int i = 0; i < expectedTags.Length; i++)
        {
            Assert.Equal(expectedTags[i], result.Tags[i]);
        }
    }

    [Theory]
    [InlineData("hello world", "Hello World")]
    [InlineData("HD NOOSE Headgear for Niko", "HD NOOSE Headgear for Niko")]
    [InlineData("installation through fusion overloader", "Installation Through Fusion Overloader")]
    public void TestTitleCase(string input, string expected)
    {
        // Act
        var result = _metadataService.TitleCase(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
