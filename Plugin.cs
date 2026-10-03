using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine.Events;
namespace SprocketShellSelector;
[BepInPlugin("nl.roan.sprocket.shellselector", "Sprocket Shell Selector", "0.9.4")]
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
            RuntimeSpall.Configure();
            harmony.PatchAll(typeof(RuntimeShellSelection));
            harmony.PatchAll(typeof(RuntimeSpall));
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf();
            Log.LogError($"Shell selector disabled: {ex}");
            return;
        }
        Log.LogInfo("Sprocket Shell Selector v0.9.4 loaded; shell and spall/APHE patches loaded.");
        var simulatorHarmony = new Harmony("nl.roan.sprocket.shellselector.simulator");
        try
        {
            RuntimeArmourSimulator.Configure();
            simulatorHarmony.PatchAll(typeof(RuntimeArmourSimulator));
            Log.LogInfo("[Armour Simulator] Patches loaded.");
        }
        catch (Exception ex)
        {
            simulatorHarmony.UnpatchSelf();
            Log.LogError($"[Armour Simulator] Disabled; shell and spall/APHE patches remain active: {ex}");
        }
        var effectsHarmony=new Harmony("nl.roan.sprocket.shellselector.effects");
        try
        {
            effectsHarmony.PatchAll(typeof(RuntimeShellEffects));
            Log.LogInfo("[APHE Effect] Optional native explosion visual hook loaded.");
        }
        catch(Exception ex)
        {
            effectsHarmony.UnpatchSelf();
            Log.LogError($"[APHE Effect] Visual hook disabled; shell behaviour remains active: {ex}");
        }
    }
}
internal static class Ui
{
    internal static UnityAction<int> IntCallback(Action<int> action) => DelegateSupport.ConvertDelegate<UnityAction<int>>(action)!;
    internal static Il2CppSystem.Action<bool> BoolCallback(Action<bool> action) => DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(action)!;
}
