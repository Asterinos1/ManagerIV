namespace GtaIVModLoader.Core;

/// <summary>
/// Specifies the target location in the game directory where a mod's files should be deployed.
/// </summary>
public enum DeployTarget
{
    /// <summary>
    /// Deployed into the update folder (e.g., update/&lt;NNN&gt;_&lt;modname&gt; for FusionOverloader).
    /// </summary>
    Update,

    /// <summary>
    /// Deployed into the plugins folder (e.g., plugins/ for ASI loaders).
    /// </summary>
    Plugins,

    /// <summary>
    /// Deployed into the scripts folder (e.g., scripts/ for ScriptHook).
    /// </summary>
    Scripts
}
