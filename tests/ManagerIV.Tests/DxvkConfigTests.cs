using System;
using System.IO;
using System.Linq;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class DxvkConfigTests : IDisposable
{
    private readonly string _tempDir;

    public DxvkConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DxvkConfigTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void TestDxvkConfigReadWrite()
    {
        string confPath = Path.Combine(_tempDir, "dxvk.conf");
        string originalContent = @"# d3d9.forceAspectRatio = """"
d3d9.maxFrameLatency = 1
# d3d9.presentInterval = 1
dxvk.hud = fps
d3d9.samplerAnisotropy = 16
";
        File.WriteAllText(confPath, originalContent);

        // Load config
        var config = DxvkConfig.Load(confPath);

        // Assert loaded values
        Assert.Equal("", config.ForceAspectRatio);
        Assert.Equal(1, config.D3d9MaxFrameLatency);
        Assert.Equal(1, config.D3d9PresentInterval);
        Assert.True(config.PresentIntervalEnabled);
        Assert.Equal("fps", config.Hud);
        Assert.Equal(16, config.D3d9SamplerAnisotropy);

        // Modify values
        config.ForceAspectRatio = "16:9";
        config.D3d9MaxFrameLatency = 2;
        config.PresentIntervalEnabled = false; // D3d9PresentInterval -> 0
        config.Hud = "fps,compiler";
        config.D3d9SamplerAnisotropy = 8;
        config.DxgiEnableHDR = false;

        // Save config
        DxvkConfig.Save(confPath, config);

        // Load again & verify
        var reloaded = DxvkConfig.Load(confPath);
        Assert.Equal("16:9", reloaded.ForceAspectRatio);
        Assert.Equal(2, reloaded.D3d9MaxFrameLatency);
        Assert.Equal(0, reloaded.D3d9PresentInterval);
        Assert.False(reloaded.PresentIntervalEnabled);
        Assert.Equal("fps,compiler", reloaded.Hud);
        Assert.Equal(8, reloaded.D3d9SamplerAnisotropy);
        Assert.False(reloaded.DxgiEnableHDR);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }
}
