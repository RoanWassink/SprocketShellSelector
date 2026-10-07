namespace SprocketShellSelector;
// Only read the legacy identity here. Current configuration always wins;
// retain the original file for rollback and all unrecognized/native CFG entries.
internal static class PluginConfigMigration
{
    internal const string PluginId="sprocket.shellselector";
    private const string LegacyPluginId="nl.roan.sprocket.shellselector";
    internal static bool EnsureCurrent(string directory)
    {
        var current=Path.Combine(directory,PluginId+".cfg");
        var previous=Path.Combine(directory,LegacyPluginId+".cfg");
        if(File.Exists(current)||!File.Exists(previous))return false;
        File.Copy(previous,current,overwrite:false);
        return true;
    }
}
