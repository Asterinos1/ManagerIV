using System;
using System.IO;
using System.Linq;

namespace ManagerIV.Core;

/// <summary>
/// Model class wrapping all settings within FusionFix's INI file.
/// Inherits ViewModelBase to enable seamless two-way binding in XAML views.
/// </summary>
public class FusionFixConfig : ViewModelBase
{
    // ==========================================
    // Backing Fields and Defaults
    // ==========================================

    // [MAIN]
    private int _recoilFix = 1;
    private int _aimingZoomFix = 1;
    private int _skipIntro = 1;
    private int _skipMenu = 0;
    private int _mouseFix = 1;
    private string _walkKey = "0x12";

    // [USERPROFILE]
    private string _customUserProfilePath = "";

    // [CAMERASENSITIVITY]
    private double _mouseLookSensitivityRangeMin = 0.1;
    private double _mouseLookSensitivityRangeMax = 2.0;
    private double _gamepadLookSensitivityRangeMin = 0.1;
    private double _gamepadLookSensitivityRangeMax = 2.0;
    private double _mouseAimSensitivityRangeMin = 0.1;
    private double _mouseAimSensitivityRangeMax = 2.0;
    private double _gamepadAimSensitivityRangeMin = 0.1;
    private double _gamepadAimSensitivityRangeMax = 2.0;

    // [SHADOWS]
    private int _extraDynamicShadows = 2;
    private double _cascadeBlendSize = 0.1;
    private int _highResolutionShadows = 0;
    private int _forceShadowFilter = 0;

    // [FRAMELIMIT]
    private int _frameLimitType = 2;
    private int _fpsLimit = -2;
    private int _cutsceneFpsLimit = 0;
    private int _loadingFpsLimit = 30;
    private int _unlockFramerateDuringLoadscreens = 1;
    private int _minigamesFpsLimit = 30;
    private string _minigamesList = "pool_game, air_hockey, arm_wrestling, tenpinbowl, darts, drinking";

    // [MISC]
    private int _defaultCameraAngleInTLaD = 1;
    private int _pedDeathAnimFixFromTBoGT = 1;
    private int _disableCameraCenteringInCover = 1;
    private int _extraInfo = 1;
    private double _overrideTreeAlpha = 0.0;
    private int _consoleCarReflectionsAndDirt = 1;
    private int _alwaysDisplayHealthOnReticle = 1;
    private int _smoothShorelines = 1;
    private int _smoothLightVolumes = 1;
    private int _noBloomColorShift = 1;
    private int _menuEnteringDelay = 0;
    private int _menuExitingDelay = 0;
    private int _menuAccessDelayOnStartup = 0;
    private int _radarZoomDelay = 3000;
    private int _deathMusic = 0;
    private double _drunkDrivingHandlingFixIntensity = 0.65;
    private double _drunkDrivingCamFixIntensity = 1.0;

    // [FOG]
    private double _volFogFarClip = 4500.0;
    private int _extendedTimecycEditing = 0;

    // [BudgetedIV]
    private int _vehicleBudget = 0;
    private int _pedBudget = 0;
    private int _extendedLimits = 0;

    // [EPISODICCONTENT]
    private int _episodicVehicles = 0;
    private int _episodicWeapons = 0;
    private int _explosiveAnnihilator = 0;
    private int _otherEpisodicChecks = 0;
    private int _tBoGTHelicopterHeightLimit = 0;
    private int _tBoGTPoliceWeapons = 0;
    private int _removeSCOSignatureCheck = 0;

    // [SUNSHAFTS]
    private double _sunShaftsDensity = 0.9;
    private double _sunShaftsDecay = 0.95;

    // [POSTFX]
    private int _enablePreAlphaDepth = 1;
    private int _ambientOcclusionBlurPasses = 1;
    private int _ambientOcclusionSamples = 9;
    private double _ambientOcclusionLogMaxOffset = 3.0;
    private int _ambientOcclusionMaxMipLevel = 5;
    private double _ambientOcclusionFarClip = 150.0;
    private double _ambientOcclusionRadius = 1.125;
    private double _ambientOcclusionBias = 0.03;
    private double _ambientOcclusionIntensity = 0.4;
    private double _ambientOcclusionBlurRadius = 2.0;

    // [SHADOWFILTERSHARP]
    private double _sharpShadowSoftness = 1.5;
    private double _sharpShadowBias = 5.0;

    // [SHADOWFILTERSOFT]
    private double _softShadowSoftness = 3.0;
    private double _softShadowBias = 8.0;

    // [SHADOWFILTERCHSS]
    private double _chssShadowSoftness = 1.5;
    private double _chssShadowBias = 5.0;
    private double _chssMaxSoftness = 20.0;

    // [PROJECT2DFX]
    private double _coronaRadiusMultiplier = 1.0;
    private double _coronaAlphaMultiplier = 1.0;
    private int _slightlyIncreaseRadiusWithDistance = 1;
    private int _disableDefaultLodLights = 1;

    // [TURNINDICATORS]
    private int _manualTurnIndicators = 0;
    private string _leftIndicatorKey = "0xDB";
    private string _rightIndicatorKey = "0xDD";

    // [EXPERIMENTAL]
    private int _reflectionMSAAQuality = 0;


    // ==========================================
    // Public Properties
    // ==========================================

    // [MAIN]
    public int RecoilFix
    {
        get => _recoilFix;
        set { if (SetProperty(ref _recoilFix, value)) OnPropertyChanged(nameof(RecoilFixEnabled)); }
    }
    public int AimingZoomFix
    {
        get => _aimingZoomFix;
        set => SetProperty(ref _aimingZoomFix, value);
    }
    public int SkipIntro
    {
        get => _skipIntro;
        set { if (SetProperty(ref _skipIntro, value)) OnPropertyChanged(nameof(SkipIntroEnabled)); }
    }
    public int SkipMenu
    {
        get => _skipMenu;
        set { if (SetProperty(ref _skipMenu, value)) OnPropertyChanged(nameof(SkipMenuEnabled)); }
    }
    public int MouseFix
    {
        get => _mouseFix;
        set { if (SetProperty(ref _mouseFix, value)) OnPropertyChanged(nameof(MouseFixEnabled)); }
    }
    public string WalkKey
    {
        get => _walkKey;
        set => SetProperty(ref _walkKey, value ?? "0x12");
    }

    // [USERPROFILE]
    public string CustomUserProfilePath
    {
        get => _customUserProfilePath;
        set => SetProperty(ref _customUserProfilePath, value ?? "");
    }

    // [CAMERASENSITIVITY]
    public double MouseLookSensitivityRangeMin
    {
        get => _mouseLookSensitivityRangeMin;
        set => SetProperty(ref _mouseLookSensitivityRangeMin, value);
    }
    public double MouseLookSensitivityRangeMax
    {
        get => _mouseLookSensitivityRangeMax;
        set => SetProperty(ref _mouseLookSensitivityRangeMax, value);
    }
    public double GamepadLookSensitivityRangeMin
    {
        get => _gamepadLookSensitivityRangeMin;
        set => SetProperty(ref _gamepadLookSensitivityRangeMin, value);
    }
    public double GamepadLookSensitivityRangeMax
    {
        get => _gamepadLookSensitivityRangeMax;
        set => SetProperty(ref _gamepadLookSensitivityRangeMax, value);
    }
    public double MouseAimSensitivityRangeMin
    {
        get => _mouseAimSensitivityRangeMin;
        set => SetProperty(ref _mouseAimSensitivityRangeMin, value);
    }
    public double MouseAimSensitivityRangeMax
    {
        get => _mouseAimSensitivityRangeMax;
        set => SetProperty(ref _mouseAimSensitivityRangeMax, value);
    }
    public double GamepadAimSensitivityRangeMin
    {
        get => _gamepadAimSensitivityRangeMin;
        set => SetProperty(ref _gamepadAimSensitivityRangeMin, value);
    }
    public double GamepadAimSensitivityRangeMax
    {
        get => _gamepadAimSensitivityRangeMax;
        set => SetProperty(ref _gamepadAimSensitivityRangeMax, value);
    }

    // [SHADOWS]
    public int ExtraDynamicShadows
    {
        get => _extraDynamicShadows;
        set => SetProperty(ref _extraDynamicShadows, value);
    }
    public double CascadeBlendSize
    {
        get => _cascadeBlendSize;
        set => SetProperty(ref _cascadeBlendSize, value);
    }
    public int HighResolutionShadows
    {
        get => _highResolutionShadows;
        set { if (SetProperty(ref _highResolutionShadows, value)) OnPropertyChanged(nameof(HighResolutionShadowsEnabled)); }
    }
    public int ForceShadowFilter
    {
        get => _forceShadowFilter;
        set => SetProperty(ref _forceShadowFilter, value);
    }

    // [FRAMELIMIT]
    public int FrameLimitType
    {
        get => _frameLimitType;
        set => SetProperty(ref _frameLimitType, value);
    }
    public int FpsLimit
    {
        get => _fpsLimit;
        set => SetProperty(ref _fpsLimit, value);
    }
    public int CutsceneFpsLimit
    {
        get => _cutsceneFpsLimit;
        set => SetProperty(ref _cutsceneFpsLimit, value);
    }
    public int LoadingFpsLimit
    {
        get => _loadingFpsLimit;
        set => SetProperty(ref _loadingFpsLimit, value);
    }
    public int UnlockFramerateDuringLoadscreens
    {
        get => _unlockFramerateDuringLoadscreens;
        set { if (SetProperty(ref _unlockFramerateDuringLoadscreens, value)) OnPropertyChanged(nameof(UnlockFramerateDuringLoadscreensEnabled)); }
    }
    public int MinigamesFpsLimit
    {
        get => _minigamesFpsLimit;
        set => SetProperty(ref _minigamesFpsLimit, value);
    }
    public string MinigamesList
    {
        get => _minigamesList;
        set => SetProperty(ref _minigamesList, value ?? "");
    }

    // [MISC]
    public int DefaultCameraAngleInTLaD
    {
        get => _defaultCameraAngleInTLaD;
        set { if (SetProperty(ref _defaultCameraAngleInTLaD, value)) OnPropertyChanged(nameof(DefaultCameraAngleInTLaDEnabled)); }
    }
    public int PedDeathAnimFixFromTBoGT
    {
        get => _pedDeathAnimFixFromTBoGT;
        set { if (SetProperty(ref _pedDeathAnimFixFromTBoGT, value)) OnPropertyChanged(nameof(PedDeathAnimFixFromTBoGTEnabled)); }
    }
    public int DisableCameraCenteringInCover
    {
        get => _disableCameraCenteringInCover;
        set { if (SetProperty(ref _disableCameraCenteringInCover, value)) OnPropertyChanged(nameof(DisableCameraCenteringInCoverEnabled)); }
    }
    public int ExtraInfo
    {
        get => _extraInfo;
        set { if (SetProperty(ref _extraInfo, value)) OnPropertyChanged(nameof(ExtraInfoEnabled)); }
    }
    public double OverrideTreeAlpha
    {
        get => _overrideTreeAlpha;
        set => SetProperty(ref _overrideTreeAlpha, value);
    }
    public int ConsoleCarReflectionsAndDirt
    {
        get => _consoleCarReflectionsAndDirt;
        set { if (SetProperty(ref _consoleCarReflectionsAndDirt, value)) OnPropertyChanged(nameof(ConsoleCarReflectionsAndDirtEnabled)); }
    }
    public int AlwaysDisplayHealthOnReticle
    {
        get => _alwaysDisplayHealthOnReticle;
        set { if (SetProperty(ref _alwaysDisplayHealthOnReticle, value)) OnPropertyChanged(nameof(AlwaysDisplayHealthOnReticleEnabled)); }
    }
    public int SmoothShorelines
    {
        get => _smoothShorelines;
        set { if (SetProperty(ref _smoothShorelines, value)) OnPropertyChanged(nameof(SmoothShorelinesEnabled)); }
    }
    public int SmoothLightVolumes
    {
        get => _smoothLightVolumes;
        set { if (SetProperty(ref _smoothLightVolumes, value)) OnPropertyChanged(nameof(SmoothLightVolumesEnabled)); }
    }
    public int NoBloomColorShift
    {
        get => _noBloomColorShift;
        set { if (SetProperty(ref _noBloomColorShift, value)) OnPropertyChanged(nameof(NoBloomColorShiftEnabled)); }
    }
    public int MenuEnteringDelay
    {
        get => _menuEnteringDelay;
        set => SetProperty(ref _menuEnteringDelay, value);
    }
    public int MenuExitingDelay
    {
        get => _menuExitingDelay;
        set => SetProperty(ref _menuExitingDelay, value);
    }
    public int MenuAccessDelayOnStartup
    {
        get => _menuAccessDelayOnStartup;
        set => SetProperty(ref _menuAccessDelayOnStartup, value);
    }
    public int RadarZoomDelay
    {
        get => _radarZoomDelay;
        set => SetProperty(ref _radarZoomDelay, value);
    }
    public int DeathMusic
    {
        get => _deathMusic;
        set { if (SetProperty(ref _deathMusic, value)) OnPropertyChanged(nameof(DeathMusicEnabled)); }
    }
    public double DrunkDrivingHandlingFixIntensity
    {
        get => _drunkDrivingHandlingFixIntensity;
        set => SetProperty(ref _drunkDrivingHandlingFixIntensity, value);
    }
    public double DrunkDrivingCamFixIntensity
    {
        get => _drunkDrivingCamFixIntensity;
        set => SetProperty(ref _drunkDrivingCamFixIntensity, value);
    }

    // [FOG]
    public double VolFogFarClip
    {
        get => _volFogFarClip;
        set => SetProperty(ref _volFogFarClip, value);
    }
    public int ExtendedTimecycEditing
    {
        get => _extendedTimecycEditing;
        set { if (SetProperty(ref _extendedTimecycEditing, value)) OnPropertyChanged(nameof(ExtendedTimecycEditingEnabled)); }
    }

    // [BudgetedIV]
    public int VehicleBudget
    {
        get => _vehicleBudget;
        set => SetProperty(ref _vehicleBudget, value);
    }
    public int PedBudget
    {
        get => _pedBudget;
        set => SetProperty(ref _pedBudget, value);
    }
    public int ExtendedLimits
    {
        get => _extendedLimits;
        set { if (SetProperty(ref _extendedLimits, value)) OnPropertyChanged(nameof(ExtendedLimitsEnabled)); }
    }

    // [EPISODICCONTENT]
    public int EpisodicVehicles
    {
        get => _episodicVehicles;
        set { if (SetProperty(ref _episodicVehicles, value)) OnPropertyChanged(nameof(EpisodicVehiclesEnabled)); }
    }
    public int EpisodicWeapons
    {
        get => _episodicWeapons;
        set { if (SetProperty(ref _episodicWeapons, value)) OnPropertyChanged(nameof(EpisodicWeaponsEnabled)); }
    }
    public int ExplosiveAnnihilator
    {
        get => _explosiveAnnihilator;
        set { if (SetProperty(ref _explosiveAnnihilator, value)) OnPropertyChanged(nameof(ExplosiveAnnihilatorEnabled)); }
    }
    public int OtherEpisodicChecks
    {
        get => _otherEpisodicChecks;
        set { if (SetProperty(ref _otherEpisodicChecks, value)) OnPropertyChanged(nameof(OtherEpisodicChecksEnabled)); }
    }
    public int TBoGTHelicopterHeightLimit
    {
        get => _tBoGTHelicopterHeightLimit;
        set { if (SetProperty(ref _tBoGTHelicopterHeightLimit, value)) OnPropertyChanged(nameof(TBoGTHelicopterHeightLimitEnabled)); }
    }
    public int TBoGTPoliceWeapons
    {
        get => _tBoGTPoliceWeapons;
        set { if (SetProperty(ref _tBoGTPoliceWeapons, value)) OnPropertyChanged(nameof(TBoGTPoliceWeaponsEnabled)); }
    }
    public int RemoveSCOSignatureCheck
    {
        get => _removeSCOSignatureCheck;
        set { if (SetProperty(ref _removeSCOSignatureCheck, value)) OnPropertyChanged(nameof(RemoveSCOSignatureCheckEnabled)); }
    }

    // [SUNSHAFTS]
    public double SunShaftsDensity
    {
        get => _sunShaftsDensity;
        set => SetProperty(ref _sunShaftsDensity, value);
    }
    public double SunShaftsDecay
    {
        get => _sunShaftsDecay;
        set => SetProperty(ref _sunShaftsDecay, value);
    }

    // [POSTFX]
    public int EnablePreAlphaDepth
    {
        get => _enablePreAlphaDepth;
        set { if (SetProperty(ref _enablePreAlphaDepth, value)) OnPropertyChanged(nameof(EnablePreAlphaDepthEnabled)); }
    }
    public int AmbientOcclusionBlurPasses
    {
        get => _ambientOcclusionBlurPasses;
        set => SetProperty(ref _ambientOcclusionBlurPasses, value);
    }
    public int AmbientOcclusionSamples
    {
        get => _ambientOcclusionSamples;
        set => SetProperty(ref _ambientOcclusionSamples, value);
    }
    public double AmbientOcclusionLogMaxOffset
    {
        get => _ambientOcclusionLogMaxOffset;
        set => SetProperty(ref _ambientOcclusionLogMaxOffset, value);
    }
    public int AmbientOcclusionMaxMipLevel
    {
        get => _ambientOcclusionMaxMipLevel;
        set => SetProperty(ref _ambientOcclusionMaxMipLevel, value);
    }
    public double AmbientOcclusionFarClip
    {
        get => _ambientOcclusionFarClip;
        set => SetProperty(ref _ambientOcclusionFarClip, value);
    }
    public double AmbientOcclusionRadius
    {
        get => _ambientOcclusionRadius;
        set => SetProperty(ref _ambientOcclusionRadius, value);
    }
    public double AmbientOcclusionBias
    {
        get => _ambientOcclusionBias;
        set => SetProperty(ref _ambientOcclusionBias, value);
    }
    public double AmbientOcclusionIntensity
    {
        get => _ambientOcclusionIntensity;
        set => SetProperty(ref _ambientOcclusionIntensity, value);
    }
    public double AmbientOcclusionBlurRadius
    {
        get => _ambientOcclusionBlurRadius;
        set => SetProperty(ref _ambientOcclusionBlurRadius, value);
    }

    // [SHADOWFILTERSHARP]
    public double SharpShadowSoftness
    {
        get => _sharpShadowSoftness;
        set => SetProperty(ref _sharpShadowSoftness, value);
    }
    public double SharpShadowBias
    {
        get => _sharpShadowBias;
        set => SetProperty(ref _sharpShadowBias, value);
    }

    // [SHADOWFILTERSOFT]
    public double SoftShadowSoftness
    {
        get => _softShadowSoftness;
        set => SetProperty(ref _softShadowSoftness, value);
    }
    public double SoftShadowBias
    {
        get => _softShadowBias;
        set => SetProperty(ref _softShadowBias, value);
    }

    // [SHADOWFILTERCHSS]
    public double ChssShadowSoftness
    {
        get => _chssShadowSoftness;
        set => SetProperty(ref _chssShadowSoftness, value);
    }
    public double ChssShadowBias
    {
        get => _chssShadowBias;
        set => SetProperty(ref _chssShadowBias, value);
    }
    public double ChssMaxSoftness
    {
        get => _chssMaxSoftness;
        set => SetProperty(ref _chssMaxSoftness, value);
    }

    // [PROJECT2DFX]
    public double CoronaRadiusMultiplier
    {
        get => _coronaRadiusMultiplier;
        set => SetProperty(ref _coronaRadiusMultiplier, value);
    }
    public double CoronaAlphaMultiplier
    {
        get => _coronaAlphaMultiplier;
        set => SetProperty(ref _coronaAlphaMultiplier, value);
    }
    public int SlightlyIncreaseRadiusWithDistance
    {
        get => _slightlyIncreaseRadiusWithDistance;
        set { if (SetProperty(ref _slightlyIncreaseRadiusWithDistance, value)) OnPropertyChanged(nameof(SlightlyIncreaseRadiusWithDistanceEnabled)); }
    }
    public int DisableDefaultLodLights
    {
        get => _disableDefaultLodLights;
        set { if (SetProperty(ref _disableDefaultLodLights, value)) OnPropertyChanged(nameof(DisableDefaultLodLightsEnabled)); }
    }

    // [TURNINDICATORS]
    public int ManualTurnIndicators
    {
        get => _manualTurnIndicators;
        set { if (SetProperty(ref _manualTurnIndicators, value)) OnPropertyChanged(nameof(ManualTurnIndicatorsEnabled)); }
    }
    public string LeftIndicatorKey
    {
        get => _leftIndicatorKey;
        set => SetProperty(ref _leftIndicatorKey, value ?? "0xDB");
    }
    public string RightIndicatorKey
    {
        get => _rightIndicatorKey;
        set => SetProperty(ref _rightIndicatorKey, value ?? "0xDD");
    }

    // [EXPERIMENTAL]
    public int ReflectionMSAAQuality
    {
        get => _reflectionMSAAQuality;
        set => SetProperty(ref _reflectionMSAAQuality, value);
    }

    // ==========================================
    // Boolean Helpers for UI Checkboxes
    // ==========================================
    public bool RecoilFixEnabled
    {
        get => RecoilFix == 1;
        set => RecoilFix = value ? 1 : 0;
    }
    public bool SkipIntroEnabled
    {
        get => SkipIntro == 1;
        set => SkipIntro = value ? 1 : 0;
    }
    public bool SkipMenuEnabled
    {
        get => SkipMenu == 1;
        set => SkipMenu = value ? 1 : 0;
    }
    public bool MouseFixEnabled
    {
        get => MouseFix == 1;
        set => MouseFix = value ? 1 : 0;
    }
    public bool HighResolutionShadowsEnabled
    {
        get => HighResolutionShadows == 1;
        set => HighResolutionShadows = value ? 1 : 0;
    }
    public bool UnlockFramerateDuringLoadscreensEnabled
    {
        get => UnlockFramerateDuringLoadscreens == 1;
        set => UnlockFramerateDuringLoadscreens = value ? 1 : 0;
    }
    public bool DefaultCameraAngleInTLaDEnabled
    {
        get => DefaultCameraAngleInTLaD == 1;
        set => DefaultCameraAngleInTLaD = value ? 1 : 0;
    }
    public bool PedDeathAnimFixFromTBoGTEnabled
    {
        get => PedDeathAnimFixFromTBoGT == 1;
        set => PedDeathAnimFixFromTBoGT = value ? 1 : 0;
    }
    public bool DisableCameraCenteringInCoverEnabled
    {
        get => DisableCameraCenteringInCover == 1;
        set => DisableCameraCenteringInCover = value ? 1 : 0;
    }
    public bool ExtraInfoEnabled
    {
        get => ExtraInfo == 1;
        set => ExtraInfo = value ? 1 : 0;
    }
    public bool ConsoleCarReflectionsAndDirtEnabled
    {
        get => ConsoleCarReflectionsAndDirt == 1;
        set => ConsoleCarReflectionsAndDirt = value ? 1 : 0;
    }
    public bool AlwaysDisplayHealthOnReticleEnabled
    {
        get => AlwaysDisplayHealthOnReticle == 1;
        set => AlwaysDisplayHealthOnReticle = value ? 1 : 0;
    }
    public bool SmoothShorelinesEnabled
    {
        get => SmoothShorelines == 1;
        set => SmoothShorelines = value ? 1 : 0;
    }
    public bool SmoothLightVolumesEnabled
    {
        get => SmoothLightVolumes == 1;
        set => SmoothLightVolumes = value ? 1 : 0;
    }
    public bool NoBloomColorShiftEnabled
    {
        get => NoBloomColorShift == 1;
        set => NoBloomColorShift = value ? 1 : 0;
    }
    public bool DeathMusicEnabled
    {
        get => DeathMusic == 1;
        set => DeathMusic = value ? 1 : 0;
    }
    public bool ExtendedTimecycEditingEnabled
    {
        get => ExtendedTimecycEditing == 1;
        set => ExtendedTimecycEditing = value ? 1 : 0;
    }
    public bool ExtendedLimitsEnabled
    {
        get => ExtendedLimits == 1;
        set => ExtendedLimits = value ? 1 : 0;
    }
    public bool EpisodicVehiclesEnabled
    {
        get => EpisodicVehicles == 1;
        set => EpisodicVehicles = value ? 1 : 0;
    }
    public bool EpisodicWeaponsEnabled
    {
        get => EpisodicWeapons == 1;
        set => EpisodicWeapons = value ? 1 : 0;
    }
    public bool ExplosiveAnnihilatorEnabled
    {
        get => ExplosiveAnnihilator == 1;
        set => ExplosiveAnnihilator = value ? 1 : 0;
    }
    public bool OtherEpisodicChecksEnabled
    {
        get => OtherEpisodicChecks == 1;
        set => OtherEpisodicChecks = value ? 1 : 0;
    }
    public bool TBoGTHelicopterHeightLimitEnabled
    {
        get => TBoGTHelicopterHeightLimit == 1;
        set => TBoGTHelicopterHeightLimit = value ? 1 : 0;
    }
    public bool TBoGTPoliceWeaponsEnabled
    {
        get => TBoGTPoliceWeapons == 1;
        set => TBoGTPoliceWeapons = value ? 1 : 0;
    }
    public bool RemoveSCOSignatureCheckEnabled
    {
        get => RemoveSCOSignatureCheck == 1;
        set => RemoveSCOSignatureCheck = value ? 1 : 0;
    }
    public bool EnablePreAlphaDepthEnabled
    {
        get => EnablePreAlphaDepth == 1;
        set => EnablePreAlphaDepth = value ? 1 : 0;
    }
    public bool SlightlyIncreaseRadiusWithDistanceEnabled
    {
        get => SlightlyIncreaseRadiusWithDistance == 1;
        set => SlightlyIncreaseRadiusWithDistance = value ? 1 : 0;
    }
    public bool DisableDefaultLodLightsEnabled
    {
        get => DisableDefaultLodLights == 1;
        set => DisableDefaultLodLights = value ? 1 : 0;
    }
    public bool ManualTurnIndicatorsEnabled
    {
        get => ManualTurnIndicators == 1;
        set => ManualTurnIndicators = value ? 1 : 0;
    }

    // ==========================================
    // Parser Helpers
    // ==========================================
    private static void ParseRange(string rangeStr, out double min, out double max)
    {
        min = 0.1;
        max = 2.0;
        if (string.IsNullOrEmpty(rangeStr)) return;
        var parts = rangeStr.Split(',');
        if (parts.Length >= 2)
        {
            double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out min);
            double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out max);
        }
    }

    // ==========================================
    // Load and Save Logic
    // ==========================================
    public static FusionFixConfig Load(string iniPath)
    {
        var config = new FusionFixConfig();
        if (!File.Exists(iniPath)) return config;

        try
        {
            var lines = File.ReadAllLines(iniPath);
            string section = "";
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;
                if (trimmed.StartsWith("//")) continue;

                if (trimmed.StartsWith("["))
                {
                    int closeBracket = trimmed.IndexOf(']');
                    if (closeBracket > 0)
                    {
                        section = trimmed.Substring(1, closeBracket - 1).Trim().ToUpperInvariant();
                        continue;
                    }
                }

                var parts = trimmed.Split('=', 2);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim().ToLowerInvariant();
                string valAndComment = parts[1];
                
                string valueStr = valAndComment;
                int commentIdx = valAndComment.IndexOf("//");
                if (commentIdx >= 0)
                {
                    valueStr = valAndComment.Substring(0, commentIdx);
                }
                commentIdx = valueStr.IndexOf(";");
                if (commentIdx >= 0)
                {
                    valueStr = valueStr.Substring(0, commentIdx);
                }
                valueStr = valueStr.Trim();

                switch (section)
                {
                    case "MAIN":
                        if (key == "recoilfix" && int.TryParse(valueStr, out int rf)) config.RecoilFix = rf;
                        if (key == "aimingzoomfix" && int.TryParse(valueStr, out int azf)) config.AimingZoomFix = azf;
                        if (key == "skipintro" && int.TryParse(valueStr, out int si)) config.SkipIntro = si;
                        if (key == "skipmenu" && int.TryParse(valueStr, out int sm)) config.SkipMenu = sm;
                        if (key == "mousefix" && int.TryParse(valueStr, out int mf)) config.MouseFix = mf;
                        if (key == "walkkey") config.WalkKey = valueStr;
                        break;
                    case "USERPROFILE":
                        if (key == "customuserprofilepath") config.CustomUserProfilePath = valueStr;
                        break;
                    case "CAMERASENSITIVITY":
                        if (key == "mouselooksensitivityrange")
                        {
                            ParseRange(valueStr, out double min, out double max);
                            config.MouseLookSensitivityRangeMin = min;
                            config.MouseLookSensitivityRangeMax = max;
                        }
                        else if (key == "gamepadlooksensitivityrange")
                        {
                            ParseRange(valueStr, out double min, out double max);
                            config.GamepadLookSensitivityRangeMin = min;
                            config.GamepadLookSensitivityRangeMax = max;
                        }
                        else if (key == "mouseaimsensitivityrange")
                        {
                            ParseRange(valueStr, out double min, out double max);
                            config.MouseAimSensitivityRangeMin = min;
                            config.MouseAimSensitivityRangeMax = max;
                        }
                        else if (key == "gamepadaimsensitivityrange")
                        {
                            ParseRange(valueStr, out double min, out double max);
                            config.GamepadAimSensitivityRangeMin = min;
                            config.GamepadAimSensitivityRangeMax = max;
                        }
                        break;
                    case "SHADOWS":
                        if (key == "extradynamicshadows" && int.TryParse(valueStr, out int eds)) config.ExtraDynamicShadows = eds;
                        if (key == "cascadeblendsize" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cbs)) config.CascadeBlendSize = cbs;
                        if (key == "highresolutionshadows" && int.TryParse(valueStr, out int hrs)) config.HighResolutionShadows = hrs;
                        if (key == "forceshadowfilter" && int.TryParse(valueStr, out int fsf)) config.ForceShadowFilter = fsf;
                        break;
                    case "FRAMELIMIT":
                        if (key == "framelimittype" && int.TryParse(valueStr, out int flt)) config.FrameLimitType = flt;
                        if (key == "fpslimit" && int.TryParse(valueStr, out int fl)) config.FpsLimit = fl;
                        if (key == "cutscenefpslimit" && int.TryParse(valueStr, out int cfl)) config.CutsceneFpsLimit = cfl;
                        if (key == "loadingfpslimit" && int.TryParse(valueStr, out int lfl)) config.LoadingFpsLimit = lfl;
                        if (key == "unlockframerateduringloadscreens" && int.TryParse(valueStr, out int ufdl)) config.UnlockFramerateDuringLoadscreens = ufdl;
                        if (key == "minigamesfpslimit" && int.TryParse(valueStr, out int mfl)) config.MinigamesFpsLimit = mfl;
                        if (key == "minigameslist") config.MinigamesList = valueStr;
                        break;
                    case "MISC":
                        if (key == "defaultcameraangleintlad" && int.TryParse(valueStr, out int dc)) config.DefaultCameraAngleInTLaD = dc;
                        if (key == "peddeathanimfixfromtbogt" && int.TryParse(valueStr, out int pd)) config.PedDeathAnimFixFromTBoGT = pd;
                        if (key == "disablecameracenteringincover" && int.TryParse(valueStr, out int dcc)) config.DisableCameraCenteringInCover = dcc;
                        if (key == "extrainfo" && int.TryParse(valueStr, out int ei)) config.ExtraInfo = ei;
                        if (key == "overridetreealpha" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ota)) config.OverrideTreeAlpha = ota;
                        if (key == "consolecarreflectionsanddirt" && int.TryParse(valueStr, out int ccrd)) config.ConsoleCarReflectionsAndDirt = ccrd;
                        if (key == "alwaysdisplayhealthonreticle" && int.TryParse(valueStr, out int adhr)) config.AlwaysDisplayHealthOnReticle = adhr;
                        if (key == "smoothshorelines" && int.TryParse(valueStr, out int ss)) config.SmoothShorelines = ss;
                        if (key == "smoothlightvolumes" && int.TryParse(valueStr, out int slv)) config.SmoothLightVolumes = slv;
                        if (key == "nobloomcolorshift" && int.TryParse(valueStr, out int nbcs)) config.NoBloomColorShift = nbcs;
                        if (key == "menuenteringdelay" && int.TryParse(valueStr, out int med)) config.MenuEnteringDelay = med;
                        if (key == "menuexitingdelay" && int.TryParse(valueStr, out int mxd)) config.MenuExitingDelay = mxd;
                        if (key == "menuaccessdelayonstartup" && int.TryParse(valueStr, out int mads)) config.MenuAccessDelayOnStartup = mads;
                        if (key == "radarzoomdelay" && int.TryParse(valueStr, out int rzd)) config.RadarZoomDelay = rzd;
                        if (key == "deathmusic" && int.TryParse(valueStr, out int dm)) config.DeathMusic = dm;
                        if (key == "drunkdrivinghandlingfixintensity" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ddhi)) config.DrunkDrivingHandlingFixIntensity = ddhi;
                        if (key == "drunkdrivingcamfixintensity" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ddci)) config.DrunkDrivingCamFixIntensity = ddci;
                        break;
                    case "FOG":
                        if (key == "volfogfarclip" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vffc)) config.VolFogFarClip = vffc;
                        if (key == "extendedtimecycediting" && int.TryParse(valueStr, out int ete)) config.ExtendedTimecycEditing = ete;
                        break;
                    case "BUDGETEDIV":
                        if (key == "vehiclebudget" && int.TryParse(valueStr, out int vb)) config.VehicleBudget = vb;
                        if (key == "pedbudget" && int.TryParse(valueStr, out int pb)) config.PedBudget = pb;
                        if (key == "extendedlimits" && int.TryParse(valueStr, out int el)) config.ExtendedLimits = el;
                        break;
                    case "EPISODICCONTENT":
                        if (key == "episodicvehicles" && int.TryParse(valueStr, out int ev)) config.EpisodicVehicles = ev;
                        if (key == "episodicweapons" && int.TryParse(valueStr, out int ew)) config.EpisodicWeapons = ew;
                        if (key == "explosiveannihilator" && int.TryParse(valueStr, out int ea)) config.ExplosiveAnnihilator = ea;
                        if (key == "otherepisodicchecks" && int.TryParse(valueStr, out int oec)) config.OtherEpisodicChecks = oec;
                        if (key == "tbogthelicopterheightlimit" && int.TryParse(valueStr, out int thhl)) config.TBoGTHelicopterHeightLimit = thhl;
                        if (key == "tbogtpoliceweapons" && int.TryParse(valueStr, out int tpw)) config.TBoGTPoliceWeapons = tpw;
                        if (key == "removescosignaturecheck" && int.TryParse(valueStr, out int rssc)) config.RemoveSCOSignatureCheck = rssc;
                        break;
                    case "SUNSHAFTS":
                        if (key == "sunshaftsdensity" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ssd)) config.SunShaftsDensity = ssd;
                        if (key == "sunshaftsdecay" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ssc)) config.SunShaftsDecay = ssc;
                        break;
                    case "POSTFX":
                        if (key == "enableprealphadepth" && int.TryParse(valueStr, out int epad)) config.EnablePreAlphaDepth = epad;
                        if (key == "ambientocclusionblurpasses" && int.TryParse(valueStr, out int aobp)) config.AmbientOcclusionBlurPasses = aobp;
                        if (key == "ambientocclusionsamples" && int.TryParse(valueStr, out int aos)) config.AmbientOcclusionSamples = aos;
                        if (key == "ambientocclusionlogmaxoffset" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double aolmo)) config.AmbientOcclusionLogMaxOffset = aolmo;
                        if (key == "ambientocclusionmaxmiplevel" && int.TryParse(valueStr, out int aomml)) config.AmbientOcclusionMaxMipLevel = aomml;
                        if (key == "ambientocclusionfarclip" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double aofc)) config.AmbientOcclusionFarClip = aofc;
                        if (key == "ambientocclusionradius" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double aor)) config.AmbientOcclusionRadius = aor;
                        if (key == "ambientocclusionbias" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double aob)) config.AmbientOcclusionBias = aob;
                        if (key == "ambientocclusionintensity" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double aoi)) config.AmbientOcclusionIntensity = aoi;
                        if (key == "ambientocclusionblurradius" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double aobr)) config.AmbientOcclusionBlurRadius = aobr;
                        break;
                    case "SHADOWFILTERSHARP":
                        if (key == "shadowsoftness" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sss)) config.SharpShadowSoftness = sss;
                        if (key == "shadowbias" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ssb)) config.SharpShadowBias = ssb;
                        break;
                    case "SHADOWFILTERSOFT":
                        if (key == "shadowsoftness" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double soss)) config.SoftShadowSoftness = soss;
                        if (key == "shadowbias" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sosb)) config.SoftShadowBias = sosb;
                        break;
                    case "SHADOWFILTERCHSS":
                        if (key == "shadowsoftness" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double css)) config.ChssShadowSoftness = css;
                        if (key == "shadowbias" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double csb)) config.ChssShadowBias = csb;
                        if (key == "maxsoftness" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cms)) config.ChssMaxSoftness = cms;
                        break;
                    case "PROJECT2DFX":
                        if (key == "coronaradiusmultiplier" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double crm)) config.CoronaRadiusMultiplier = crm;
                        if (key == "coronaalphamultiplier" && double.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cam)) config.CoronaAlphaMultiplier = cam;
                        if (key == "slightlyincreaseradiuswithdistance" && int.TryParse(valueStr, out int sird)) config.SlightlyIncreaseRadiusWithDistance = sird;
                        if (key == "disabledefaultlodlights" && int.TryParse(valueStr, out int ddll)) config.DisableDefaultLodLights = ddll;
                        break;
                    case "TURNINDICATORS":
                        if (key == "manualturnindicators" && int.TryParse(valueStr, out int mti)) config.ManualTurnIndicators = mti;
                        if (key == "leftindicatorkey") config.LeftIndicatorKey = valueStr;
                        if (key == "rightindicatorkey") config.RightIndicatorKey = valueStr;
                        break;
                    case "EXPERIMENTAL":
                        if (key == "reflectionmsaaquality" && int.TryParse(valueStr, out int rmq)) config.ReflectionMSAAQuality = rmq;
                        break;
                }
            }
        }
        catch { }

        return config;
    }

    public static void Save(string iniPath, FusionFixConfig config)
    {
        if (!File.Exists(iniPath)) return;

        try
        {
            var lines = File.ReadAllLines(iniPath).ToList();
            var writtenKeys = new HashSet<(string section, string key)>();
            string section = "";
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";")) continue;

                if (trimmed.StartsWith("["))
                {
                    int closeBracket = trimmed.IndexOf(']');
                    if (closeBracket > 0)
                    {
                        section = trimmed.Substring(1, closeBracket - 1).Trim().ToUpperInvariant();
                        continue;
                    }
                }

                var parts = trimmed.Split('=', 2);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim().ToLowerInvariant();
                string valAndComment = parts[1];

                string comment = "";
                int commentIdx = valAndComment.IndexOf("//");
                if (commentIdx >= 0)
                {
                    comment = valAndComment.Substring(commentIdx);
                }
                else
                {
                    commentIdx = valAndComment.IndexOf(";");
                    if (commentIdx >= 0)
                    {
                        comment = valAndComment.Substring(commentIdx);
                    }
                }

                string? newValue = null;
                switch (section)
                {
                    case "MAIN":
                        if (key == "recoilfix") { newValue = config.RecoilFix.ToString(); writtenKeys.Add(("MAIN", "recoilfix")); }
                        if (key == "aimingzoomfix") { newValue = config.AimingZoomFix.ToString(); writtenKeys.Add(("MAIN", "aimingzoomfix")); }
                        if (key == "skipintro") { newValue = config.SkipIntro.ToString(); writtenKeys.Add(("MAIN", "skipintro")); }
                        if (key == "skipmenu") { newValue = config.SkipMenu.ToString(); writtenKeys.Add(("MAIN", "skipmenu")); }
                        if (key == "mousefix") { newValue = config.MouseFix.ToString(); writtenKeys.Add(("MAIN", "mousefix")); }
                        if (key == "walkkey") { newValue = config.WalkKey; writtenKeys.Add(("MAIN", "walkkey")); }
                        break;
                    case "USERPROFILE":
                        if (key == "customuserprofilepath") { newValue = config.CustomUserProfilePath; writtenKeys.Add(("USERPROFILE", "customuserprofilepath")); }
                        break;
                    case "CAMERASENSITIVITY":
                        if (key == "mouselooksensitivityrange") newValue = $"{config.MouseLookSensitivityRangeMin.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}, {config.MouseLookSensitivityRangeMax.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";
                        if (key == "gamepadlooksensitivityrange") newValue = $"{config.GamepadLookSensitivityRangeMin.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}, {config.GamepadLookSensitivityRangeMax.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";
                        if (key == "mouseaimsensitivityrange") newValue = $"{config.MouseAimSensitivityRangeMin.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}, {config.MouseAimSensitivityRangeMax.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";
                        if (key == "gamepadaimsensitivityrange") newValue = $"{config.GamepadAimSensitivityRangeMin.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}, {config.GamepadAimSensitivityRangeMax.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";
                        break;
                    case "SHADOWS":
                        if (key == "extradynamicshadows") newValue = config.ExtraDynamicShadows.ToString();
                        if (key == "cascadeblendsize") newValue = config.CascadeBlendSize.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "highresolutionshadows") newValue = config.HighResolutionShadows.ToString();
                        if (key == "forceshadowfilter") newValue = config.ForceShadowFilter.ToString();
                        break;
                    case "FRAMELIMIT":
                        if (key == "framelimittype") newValue = config.FrameLimitType.ToString();
                        if (key == "fpslimit") newValue = config.FpsLimit.ToString();
                        if (key == "cutscenefpslimit") newValue = config.CutsceneFpsLimit.ToString();
                        if (key == "loadingfpslimit") newValue = config.LoadingFpsLimit.ToString();
                        if (key == "unlockframerateduringloadscreens") newValue = config.UnlockFramerateDuringLoadscreens.ToString();
                        if (key == "minigamesfpslimit") newValue = config.MinigamesFpsLimit.ToString();
                        if (key == "minigameslist") newValue = config.MinigamesList;
                        break;
                    case "MISC":
                        if (key == "defaultcameraangleintlad") newValue = config.DefaultCameraAngleInTLaD.ToString();
                        if (key == "peddeathanimfixfromtbogt") newValue = config.PedDeathAnimFixFromTBoGT.ToString();
                        if (key == "disablecameracenteringincover") newValue = config.DisableCameraCenteringInCover.ToString();
                        if (key == "extrainfo") newValue = config.ExtraInfo.ToString();
                        if (key == "overridetreealpha") newValue = config.OverrideTreeAlpha.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "consolecarreflectionsanddirt") newValue = config.ConsoleCarReflectionsAndDirt.ToString();
                        if (key == "alwaysdisplayhealthonreticle") newValue = config.AlwaysDisplayHealthOnReticle.ToString();
                        if (key == "smoothshorelines") newValue = config.SmoothShorelines.ToString();
                        if (key == "smoothlightvolumes") newValue = config.SmoothLightVolumes.ToString();
                        if (key == "nobloomcolorshift") newValue = config.NoBloomColorShift.ToString();
                        if (key == "menuenteringdelay") newValue = config.MenuEnteringDelay.ToString();
                        if (key == "menuexitingdelay") newValue = config.MenuExitingDelay.ToString();
                        if (key == "menuaccessdelayonstartup") newValue = config.MenuAccessDelayOnStartup.ToString();
                        if (key == "radarzoomdelay") newValue = config.RadarZoomDelay.ToString();
                        if (key == "deathmusic") newValue = config.DeathMusic.ToString();
                        if (key == "drunkdrivinghandlingfixintensity") newValue = config.DrunkDrivingHandlingFixIntensity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "drunkdrivingcamfixintensity") newValue = config.DrunkDrivingCamFixIntensity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "FOG":
                        if (key == "volfogfarclip") newValue = config.VolFogFarClip.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "extendedtimecycediting") newValue = config.ExtendedTimecycEditing.ToString();
                        break;
                    case "BUDGETEDIV":
                        if (key == "vehiclebudget") newValue = config.VehicleBudget.ToString();
                        if (key == "pedbudget") newValue = config.PedBudget.ToString();
                        if (key == "extendedlimits") newValue = config.ExtendedLimits.ToString();
                        break;
                    case "EPISODICCONTENT":
                        if (key == "episodicvehicles") newValue = config.EpisodicVehicles.ToString();
                        if (key == "episodicweapons") newValue = config.EpisodicWeapons.ToString();
                        if (key == "explosiveannihilator") newValue = config.ExplosiveAnnihilator.ToString();
                        if (key == "otherepisodicchecks") newValue = config.OtherEpisodicChecks.ToString();
                        if (key == "tbogthelicopterheightlimit") newValue = config.TBoGTHelicopterHeightLimit.ToString();
                        if (key == "tbogtpoliceweapons") newValue = config.TBoGTPoliceWeapons.ToString();
                        if (key == "removescosignaturecheck") newValue = config.RemoveSCOSignatureCheck.ToString();
                        break;
                    case "SUNSHAFTS":
                        if (key == "sunshaftsdensity") newValue = config.SunShaftsDensity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "sunshaftsdecay") newValue = config.SunShaftsDecay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "POSTFX":
                        if (key == "enableprealphadepth") newValue = config.EnablePreAlphaDepth.ToString();
                        if (key == "ambientocclusionblurpasses") newValue = config.AmbientOcclusionBlurPasses.ToString();
                        if (key == "ambientocclusionsamples") newValue = config.AmbientOcclusionSamples.ToString();
                        if (key == "ambientocclusionlogmaxoffset") newValue = config.AmbientOcclusionLogMaxOffset.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "ambientocclusionmaxmiplevel") newValue = config.AmbientOcclusionMaxMipLevel.ToString();
                        if (key == "ambientocclusionfarclip") newValue = config.AmbientOcclusionFarClip.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "ambientocclusionradius") newValue = config.AmbientOcclusionRadius.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "ambientocclusionbias") newValue = config.AmbientOcclusionBias.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "ambientocclusionintensity") newValue = config.AmbientOcclusionIntensity.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "ambientocclusionblurradius") newValue = config.AmbientOcclusionBlurRadius.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "SHADOWFILTERSHARP":
                        if (key == "shadowsoftness") newValue = config.SharpShadowSoftness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "shadowbias") newValue = config.SharpShadowBias.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "SHADOWFILTERSOFT":
                        if (key == "shadowsoftness") newValue = config.SoftShadowSoftness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "shadowbias") newValue = config.SoftShadowBias.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "SHADOWFILTERCHSS":
                        if (key == "shadowsoftness") newValue = config.ChssShadowSoftness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "shadowbias") newValue = config.ChssShadowBias.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "maxsoftness") newValue = config.ChssMaxSoftness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "PROJECT2DFX":
                        if (key == "coronaradiusmultiplier") newValue = config.CoronaRadiusMultiplier.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "coronaalphamultiplier") newValue = config.CoronaAlphaMultiplier.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "slightlyincreaseradiuswithdistance") newValue = config.SlightlyIncreaseRadiusWithDistance.ToString();
                        if (key == "disabledefaultlodlights") newValue = config.DisableDefaultLodLights.ToString();
                        break;
                    case "TURNINDICATORS":
                        if (key == "manualturnindicators") newValue = config.ManualTurnIndicators.ToString();
                        if (key == "leftindicatorkey") newValue = config.LeftIndicatorKey;
                        if (key == "rightindicatorkey") newValue = config.RightIndicatorKey;
                        break;
                    case "EXPERIMENTAL":
                        if (key == "reflectionmsaaquality") newValue = config.ReflectionMSAAQuality.ToString();
                        break;
                }

                if (newValue != null)
                {
                    string spacing = " ";
                    if (!string.IsNullOrEmpty(comment))
                    {
                        spacing = "                           ";
                    }

                    int keyIdx = line.IndexOf(parts[0]);
                    string indent = keyIdx >= 0 ? line.Substring(0, keyIdx) : "";
                    lines[i] = $"{indent}{parts[0].Trim()} = {newValue}{spacing}{comment}".TrimEnd();
                }
            }

            // Insert missing keys
            var missingKeys = new List<(string Section, string Key, string Value)>();
            if (!writtenKeys.Contains(("MAIN", "skipintro"))) missingKeys.Add(("MAIN", "skipintro", config.SkipIntro.ToString()));
            if (!writtenKeys.Contains(("MAIN", "skipmenu"))) missingKeys.Add(("MAIN", "skipmenu", config.SkipMenu.ToString()));
            if (!writtenKeys.Contains(("MAIN", "mousefix"))) missingKeys.Add(("MAIN", "mousefix", config.MouseFix.ToString()));
            if (!writtenKeys.Contains(("MAIN", "walkkey"))) missingKeys.Add(("MAIN", "walkkey", config.WalkKey));
            if (!writtenKeys.Contains(("USERPROFILE", "customuserprofilepath"))) missingKeys.Add(("USERPROFILE", "customuserprofilepath", config.CustomUserProfilePath));

            var grouped = missingKeys.GroupBy(k => k.Section).ToList();
            foreach (var group in grouped)
            {
                string targetSection = group.Key;
                int sectionHeaderIdx = -1;
                int nextSectionHeaderIdx = -1;
                for (int j = 0; j < lines.Count; j++)
                {
                    var trimmedLine = lines[j].Trim();
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var secName = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim().ToUpperInvariant();
                        if (secName == targetSection)
                        {
                            sectionHeaderIdx = j;
                        }
                        else if (sectionHeaderIdx != -1 && nextSectionHeaderIdx == -1)
                        {
                            nextSectionHeaderIdx = j;
                            break;
                        }
                    }
                }

                if (sectionHeaderIdx != -1)
                {
                    int insertIdx = nextSectionHeaderIdx != -1 ? nextSectionHeaderIdx : lines.Count;
                    foreach (var item in group)
                    {
                        lines.Insert(insertIdx, $"{item.Key} = {item.Value}");
                        insertIdx++;
                        if (nextSectionHeaderIdx != -1) nextSectionHeaderIdx++;
                    }
                }
                else
                {
                    lines.Add("");
                    lines.Add($"[{targetSection}]");
                    foreach (var item in group)
                    {
                        lines.Add($"{item.Key} = {item.Value}");
                    }
                }
            }

            File.WriteAllLines(iniPath, lines);
        }
        catch { }
    }
}
