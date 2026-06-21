using ManagerIV.Core;

namespace ManagerIV.ViewModels;

public class BackendStatusViewModel : ViewModelBase
{
    private bool _asiLoaderInstalled;
    private bool _fusionFixInstalled;
    private bool _dxvkInstalled;
    private bool _scriptHookInstalled;

    public BackendStatusViewModel()
    {
    }

    public BackendStatusViewModel(bool asiLoader, bool fusionFix, bool dxvk, bool scriptHook)
    {
        _asiLoaderInstalled = asiLoader;
        _fusionFixInstalled = fusionFix;
        _dxvkInstalled = dxvk;
        _scriptHookInstalled = scriptHook;
    }

    public bool AsiLoaderInstalled => _asiLoaderInstalled;
    public string AsiLoaderText => _asiLoaderInstalled ? "Installed" : "Missing";
    public string AsiLoaderBrush => _asiLoaderInstalled ? "#FF107C41" : "#FF8A0A0A"; // Green vs Red

    public bool FusionFixInstalled => _fusionFixInstalled;
    public string FusionFixText => _fusionFixInstalled ? "Installed" : "Missing";
    public string FusionFixBrush => _fusionFixInstalled ? "#FF107C41" : "#FF8A0A0A";

    public bool DxvkInstalled => _dxvkInstalled;
    public string DxvkText => _dxvkInstalled ? "Installed" : "Missing";
    public string DxvkBrush => _dxvkInstalled ? "#FF107C41" : "#FF8A0A0A";

    public bool ScriptHookInstalled => _scriptHookInstalled;
    public string ScriptHookText => _scriptHookInstalled ? "Installed" : "Missing";
    public string ScriptHookBrush => _scriptHookInstalled ? "#FF107C41" : "#FF8A0A0A";
}
