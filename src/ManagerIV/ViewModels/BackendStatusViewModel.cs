using ManagerIV.Core;

namespace ManagerIV.ViewModels;

public class BackendStatusViewModel : ViewModelBase
{
    private bool _asiLoaderInstalled;
    private bool _overloaderInstalled;
    private bool _fusionFixInstalled;
    private bool _scriptHookInstalled;

    public BackendStatusViewModel()
    {
    }

    public BackendStatusViewModel(bool asiLoader, bool overloader, bool fusionFix, bool scriptHook)
    {
        _asiLoaderInstalled = asiLoader;
        _overloaderInstalled = overloader;
        _fusionFixInstalled = fusionFix;
        _scriptHookInstalled = scriptHook;
    }

    public bool AsiLoaderInstalled => _asiLoaderInstalled;
    public string AsiLoaderText => _asiLoaderInstalled ? "Installed" : "Missing";
    public string AsiLoaderBrush => _asiLoaderInstalled ? "#FF107C41" : "#FF8A0A0A"; // Green vs Red

    public bool OverloaderInstalled => _overloaderInstalled;
    public string OverloaderText => _overloaderInstalled ? "Installed" : "Missing";
    public string OverloaderBrush => _overloaderInstalled ? "#FF107C41" : "#FF8A0A0A";

    public bool FusionFixInstalled => _fusionFixInstalled;
    public string FusionFixText => _fusionFixInstalled ? "Installed" : "Missing";
    public string FusionFixBrush => _fusionFixInstalled ? "#FF107C41" : "#FF8A0A0A";

    public bool ScriptHookInstalled => _scriptHookInstalled;
    public string ScriptHookText => _scriptHookInstalled ? "Installed" : "Missing";
    public string ScriptHookBrush => _scriptHookInstalled ? "#FF107C41" : "#FF8A0A0A";
}
