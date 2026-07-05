using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ManagerIV.Core;

/// <summary>
/// Model class wrapping all settings within DXVK's dxvk.conf configuration file.
/// Uses a dictionary-backed store to scale cleanly with all D3D9/DXGI/DXVK properties.
/// </summary>
public class DxvkConfig : ViewModelBase
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    private string GetVal(string key, string defaultVal) => _values.TryGetValue(key, out var val) ? val : defaultVal;
    private void SetVal(string key, string val, [CallerMemberName] string propName = "")
    {
        if (!_values.TryGetValue(key, out var current) || current != val)
        {
            _values[key] = val;
            OnPropertyChanged(propName);
        }
    }

    private bool GetBool(string key, bool defaultVal) => bool.TryParse(GetVal(key, ""), out bool val) ? val : defaultVal;
    private void SetBool(string key, bool val, [CallerMemberName] string propName = "") => SetVal(key, val.ToString(), propName);

    private int GetInt(string key, int defaultVal) => int.TryParse(GetVal(key, ""), out int val) ? val : defaultVal;
    private void SetInt(string key, int val, [CallerMemberName] string propName = "") => SetVal(key, val.ToString(), propName);

    // dxgi / d3d9 / dxvk properties
    public bool DxgiEnableHDR
    {
        get => GetBool("dxgi.enableHDR", true);
        set => SetBool("dxgi.enableHDR", value);
    }
    public bool DxgiEnableDummyCompositionSwapchain
    {
        get => GetBool("dxgi.enableDummyCompositionSwapchain", false);
        set => SetBool("dxgi.enableDummyCompositionSwapchain", value);
    }
    public bool DxvkAllowFse
    {
        get => GetBool("dxvk.allowFse", false);
        set => SetBool("dxvk.allowFse", value);
    }
    public bool DxgiEnableUe4Workarounds
    {
        get => GetBool("dxgi.enableUe4Workarounds", false);
        set => SetBool("dxgi.enableUe4Workarounds", value);
    }
    public bool D3d9DeferSurfaceCreation
    {
        get => GetBool("d3d9.deferSurfaceCreation", false);
        set => SetBool("d3d9.deferSurfaceCreation", value);
    }
    public int D3d9MaxFrameLatency
    {
        get => GetInt("d3d9.maxFrameLatency", 0);
        set => SetInt("d3d9.maxFrameLatency", value);
    }
    public int D3d9MaxFrameRate
    {
        get => GetInt("d3d9.maxFrameRate", 0);
        set => SetInt("d3d9.maxFrameRate", value);
    }
    public string DxvkLatencySleep
    {
        get => GetVal("dxvk.latencySleep", "Auto");
        set => SetVal("dxvk.latencySleep", value);
    }
    public int DxvkLatencyTolerance
    {
        get => GetInt("dxvk.latencyTolerance", 1000);
        set => SetInt("dxvk.latencyTolerance", value);
    }
    public string DxvkDisableNvLowLatency2
    {
        get => GetVal("dxvk.disableNvLowLatency2", "Auto");
        set => SetVal("dxvk.disableNvLowLatency2", value);
    }
    public string D3d9CustomDeviceId
    {
        get => GetVal("d3d9.customDeviceId", "0000");
        set => SetVal("d3d9.customDeviceId", value);
    }
    public string D3d9CustomVendorId
    {
        get => GetVal("d3d9.customVendorId", "0000");
        set => SetVal("d3d9.customVendorId", value);
    }
    public string D3d9CustomDeviceDesc
    {
        get => GetVal("d3d9.customDeviceDesc", "");
        set => SetVal("d3d9.customDeviceDesc", value);
    }
    public string D3d9HideNvidiaGpu
    {
        get => GetVal("d3d9.hideNvidiaGpu", "Auto");
        set => SetVal("d3d9.hideNvidiaGpu", value);
    }
    public string D3d9HideNvkGpu
    {
        get => GetVal("d3d9.hideNvkGpu", "Auto");
        set => SetVal("d3d9.hideNvkGpu", value);
    }
    public string D3d9HideAmdGpu
    {
        get => GetVal("d3d9.hideAmdGpu", "Auto");
        set => SetVal("d3d9.hideAmdGpu", value);
    }
    public string D3d9HideIntelGpu
    {
        get => GetVal("d3d9.hideIntelGpu", "True");
        set => SetVal("d3d9.hideIntelGpu", value);
    }
    public int D3d9PresentInterval
    {
        get => GetInt("d3d9.presentInterval", -1);
        set
        {
            SetInt("d3d9.presentInterval", value);
            OnPropertyChanged(nameof(PresentIntervalEnabled));
        }
    }
    public bool PresentIntervalEnabled
    {
        get => D3d9PresentInterval > 0;
        set => D3d9PresentInterval = value ? 1 : 0;
    }
    public string DxvkTearFree
    {
        get => GetVal("dxvk.tearFree", "Auto");
        set => SetVal("dxvk.tearFree", value);
    }
    public string DxvkTilerMode
    {
        get => GetVal("dxvk.tilerMode", "Auto");
        set => SetVal("dxvk.tilerMode", value);
    }
    public int D3d9SamplerAnisotropy
    {
        get => GetInt("d3d9.samplerAnisotropy", -1);
        set => SetInt("d3d9.samplerAnisotropy", value);
    }
    public double D3d9SamplerLodBias
    {
        get => double.TryParse(GetVal("d3d9.samplerLodBias", "0.0"), out double v) ? v : 0.0;
        set => SetVal("d3d9.samplerLodBias", value.ToString("F1"));
    }
    public bool D3d9ClampNegativeLodBias
    {
        get => GetBool("d3d9.clampNegativeLodBias", false);
        set => SetBool("d3d9.clampNegativeLodBias", value);
    }
    public bool D3d9ForceSampleRateShading
    {
        get => GetBool("d3d9.forceSampleRateShading", false);
        set => SetBool("d3d9.forceSampleRateShading", value);
    }
    public bool DxvkZeroMappedMemory
    {
        get => GetBool("dxvk.zeroMappedMemory", false);
        set => SetBool("dxvk.zeroMappedMemory", value);
    }
    public int DxvkNumCompilerThreads
    {
        get => GetInt("dxvk.numCompilerThreads", 0);
        set => SetInt("dxvk.numCompilerThreads", value);
    }
    public string DxvkUseRawSsbo
    {
        get => GetVal("dxvk.useRawSsbo", "Auto");
        set => SetVal("dxvk.useRawSsbo", value);
    }
    public string DxvkEnableGraphicsPipelineLibrary
    {
        get => GetVal("dxvk.enableGraphicsPipelineLibrary", "Auto");
        set => SetVal("dxvk.enableGraphicsPipelineLibrary", value);
    }
    public string DxvkEnableDescriptorHeap
    {
        get => GetVal("dxvk.enableDescriptorHeap", "Auto");
        set => SetVal("dxvk.enableDescriptorHeap", value);
    }
    public string DxvkEnableDescriptorBuffer
    {
        get => GetVal("dxvk.enableDescriptorBuffer", "Auto");
        set => SetVal("dxvk.enableDescriptorBuffer", value);
    }
    public bool DxvkEnableUnifiedImageLayouts
    {
        get => GetBool("dxvk.enableUnifiedImageLayouts", true);
        set => SetBool("dxvk.enableUnifiedImageLayouts", value);
    }
    public bool DxvkEnableImplicitResolves
    {
        get => GetBool("dxvk.enableImplicitResolves", true);
        set => SetBool("dxvk.enableImplicitResolves", value);
    }
    public string DxvkTrackPipelineLifetime
    {
        get => GetVal("dxvk.trackPipelineLifetime", "Auto");
        set => SetVal("dxvk.trackPipelineLifetime", value);
    }
    public string DxvkEnableMemoryDefrag
    {
        get => GetVal("dxvk.enableMemoryDefrag", "Auto");
        set => SetVal("dxvk.enableMemoryDefrag", value);
    }
    public string Hud
    {
        get => GetVal("dxvk.hud", "");
        set => SetVal("dxvk.hud", value);
    }
    public int D3d9ShaderModel
    {
        get => GetInt("d3d9.shaderModel", 3);
        set => SetInt("d3d9.shaderModel", value);
    }
    public bool D3d9DpiAware
    {
        get => GetBool("d3d9.dpiAware", true);
        set => SetBool("d3d9.dpiAware", value);
    }
    public bool D3d9LenientClear
    {
        get => GetBool("d3d9.lenientClear", false);
        set => SetBool("d3d9.lenientClear", value);
    }
    public int D3d9MaxAvailableMemory
    {
        get => GetInt("d3d9.maxAvailableMemory", 4096);
        set => SetInt("d3d9.maxAvailableMemory", value);
    }
    public bool D3d9MemoryTrackTest
    {
        get => GetBool("d3d9.memoryTrackTest", false);
        set => SetBool("d3d9.memoryTrackTest", value);
    }
    public string D3d9FloatEmulation
    {
        get => GetVal("d3d9.floatEmulation", "Auto");
        set => SetVal("d3d9.floatEmulation", value);
    }
    public string DxvkLowerSinCos
    {
        get => GetVal("dxvk.lowerSinCos", "Auto");
        set => SetVal("dxvk.lowerSinCos", value);
    }
    public string D3d9DeviceLocalConstantBuffers
    {
        get => GetVal("d3d9.deviceLocalConstantBuffers", "Auto");
        set => SetVal("d3d9.deviceLocalConstantBuffers", value);
    }
    public bool D3d9SupportCubeDepthFormats
    {
        get => GetBool("d3d9.supportCubeDepthFormats", false);
        set => SetBool("d3d9.supportCubeDepthFormats", value);
    }
    public bool D3d9SupportDFFormats
    {
        get => GetBool("d3d9.supportDFFormats", true);
        set => SetBool("d3d9.supportDFFormats", value);
    }
    public bool D3d9UseD32forD24
    {
        get => GetBool("d3d9.useD32forD24", false);
        set => SetBool("d3d9.useD32forD24", value);
    }
    public bool D3d9SupportX4R4G4B4
    {
        get => GetBool("d3d9.supportX4R4G4B4", true);
        set => SetBool("d3d9.supportX4R4G4B4", value);
    }
    public bool D3d9DisableA8RT
    {
        get => GetBool("d3d9.disableA8RT", false);
        set => SetBool("d3d9.disableA8RT", value);
    }
    public bool D3d9ForceSamplerTypeSpecConstants
    {
        get => GetBool("d3d9.forceSamplerTypeSpecConstants", false);
        set => SetBool("d3d9.forceSamplerTypeSpecConstants", value);
    }
    public string ForceAspectRatio
    {
        get => GetVal("d3d9.forceAspectRatio", "16:9");
        set => SetVal("d3d9.forceAspectRatio", value);
    }
    public int D3d9ForceRefreshRate
    {
        get => GetInt("d3d9.forceRefreshRate", 0);
        set => SetInt("d3d9.forceRefreshRate", value);
    }
    public bool D3d9ModeCountCompatibility
    {
        get => GetBool("d3d9.modeCountCompatibility", false);
        set => SetBool("d3d9.modeCountCompatibility", value);
    }
    public bool D3d9EnumerateByDisplays
    {
        get => GetBool("d3d9.enumerateByDisplays", true);
        set => SetBool("d3d9.enumerateByDisplays", value);
    }
    public bool D3d9CachedWriteOnlyBuffers
    {
        get => GetBool("d3d9.cachedWriteOnlyBuffers", false);
        set => SetBool("d3d9.cachedWriteOnlyBuffers", value);
    }
    public bool D3d9SeamlessCubes
    {
        get => GetBool("d3d9.seamlessCubes", false);
        set => SetBool("d3d9.seamlessCubes", value);
    }
    public bool DxvkEnableDebugUtils
    {
        get => GetBool("dxvk.enableDebugUtils", false);
        set => SetBool("dxvk.enableDebugUtils", value);
    }
    public int D3d9TextureMemory
    {
        get => GetInt("d3d9.textureMemory", 100);
        set => SetInt("d3d9.textureMemory", value);
    }
    public bool DxvkHideIntegratedGraphics
    {
        get => GetBool("dxvk.hideIntegratedGraphics", false);
        set => SetBool("dxvk.hideIntegratedGraphics", value);
    }
    public bool D3d9DeviceLossOnFocusLoss
    {
        get => GetBool("d3d9.deviceLossOnFocusLoss", false);
        set => SetBool("d3d9.deviceLossOnFocusLoss", value);
    }
    public bool D3d9CountLosableResources
    {
        get => GetBool("d3d9.countLosableResources", true);
        set => SetBool("d3d9.countLosableResources", value);
    }
    public bool D3d9ExtraFrontbuffer
    {
        get => GetBool("d3d9.extraFrontbuffer", false);
        set => SetBool("d3d9.extraFrontbuffer", value);
    }
    public string D3d9UseFP16
    {
        get => GetVal("d3d9.useFP16", "False");
        set => SetVal("d3d9.useFP16", value);
    }
    public int DxvkMaxMemoryBudget
    {
        get => GetInt("dxvk.maxMemoryBudget", 0);
        set => SetInt("dxvk.maxMemoryBudget", value);
    }

    public static DxvkConfig Load(string path)
    {
        var config = new DxvkConfig();
        if (!File.Exists(path)) return config;

        try
        {
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                bool isCommented = trimmed.StartsWith("#");
                string content = isCommented ? trimmed.Substring(1).Trim() : trimmed;

                var parts = content.Split('=', 2);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();
                string val = parts[1].Trim().Trim('"', '\'');

                config._values[key] = val;
            }
        }
        catch { }

        return config;
    }

    public static void Save(string path, DxvkConfig config)
    {
        try
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var keysWritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                bool isCommented = trimmed.StartsWith("#");
                string content = isCommented ? trimmed.Substring(1).Trim() : trimmed;

                var parts = content.Split('=', 2);
                if (parts.Length >= 2)
                {
                    string key = parts[0].Trim();
                    if (config._values.TryGetValue(key, out var val))
                    {
                        bool needsQuotes = key.EndsWith("AspectRatio", StringComparison.OrdinalIgnoreCase) || 
                                           key.EndsWith("DeviceDesc", StringComparison.OrdinalIgnoreCase);
                        
                        string formattedVal = needsQuotes ? $"\"{val}\"" : val;
                        lines[i] = $"{key} = {formattedVal}";
                        keysWritten.Add(key);
                    }
                }
            }

            foreach (var kvp in config._values)
            {
                if (!keysWritten.Contains(kvp.Key))
                {
                    bool needsQuotes = kvp.Key.EndsWith("AspectRatio", StringComparison.OrdinalIgnoreCase) || 
                                       kvp.Key.EndsWith("DeviceDesc", StringComparison.OrdinalIgnoreCase);
                    
                    string formattedVal = needsQuotes ? $"\"{kvp.Value}\"" : kvp.Value;
                    lines.Add($"{kvp.Key} = {formattedVal}");
                }
            }

            File.WriteAllLines(path, lines);
        }
        catch { }
    }
}
