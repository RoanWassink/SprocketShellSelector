using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine.Events;
namespace SprocketShellSelector;
[BepInPlugin("nl.roan.sprocket.shellselector", "Sprocket Shell Selector", "0.1.0")]
[BepInDependency("nl.roan.sprocket.materialselector", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource ModLog = null!;
    public override void Load()
    {
        ModLog = Log;
        var harmony = new Harmony("nl.roan.sprocket.shellselector");
        try
        {
            if (Harmony.HasAnyPatches("nl.roan.sprocket.materialselector.apfsdsbeta"))
                throw new InvalidOperationException("Combined Material Selector shell hooks detected. Close the game and restore Material Selector stable v0.4.0 before using this plugin.");
            RuntimeShellSelection.Configure(Config);
            harmony.PatchAll(typeof(RuntimeShellSelection));
            Log.LogInfo("Sprocket Shell Selector v0.1.0 loaded; shell behavior inherited from combined v0.5.1.");
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf();
            Log.LogError($"Shell selector disabled: {ex}");
        }
    }
}
internal static class Ui
{
    internal static UnityAction<int> IntCallback(Action<int> action) => DelegateSupport.ConvertDelegate<UnityAction<int>>(action)!;
    internal static Il2CppSystem.Action<bool> BoolCallback(Action<bool> action) => DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(action)!;
}
