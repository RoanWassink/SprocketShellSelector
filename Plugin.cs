using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine.Events;
namespace SprocketShellSelector;
[BepInPlugin("nl.roan.sprocket.shellselector", "Sprocket Shell Selector", "0.12.4")]
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
        Log.LogInfo("Sprocket Shell Selector v0.12.4 loaded; component VUID armour responses and date-driven eras enabled.");
        var armourHarmony=new Harmony("nl.roan.sprocket.shellselector.armourresponses");
        try{if(RuntimeArmourResponses.Configure())armourHarmony.PatchAll(typeof(RuntimeArmourResponses));}
        catch(Exception ex){armourHarmony.UnpatchSelf();Log.LogWarning("[Armour response] Optional adapter disabled: "+ex.Message);}
        var atgmHarmony=new Harmony("nl.roan.sprocket.shellselector.atgm");
        try
        {
            RuntimeAtgm.Configure(Config);
            if(RuntimeAtgm.Enabled){atgmHarmony.PatchAll(typeof(RuntimeAtgm));RuntimeAtgm.Ready=true;Log.LogInfo("[ATGM] Experimental native flight/player-ray/scope hooks loaded.");}
        }
        catch(Exception ex){atgmHarmony.UnpatchSelf();RuntimeAtgm.Ready=false;Log.LogError("[ATGM] Optional flight guidance unavailable; other shell hooks remain active: "+ex);}
        var audioHarmony=new Harmony("nl.roan.sprocket.shellselector.atgmaudio");
        try
        {
            RuntimeLaunchAudio.Configure(Config);
            if(RuntimeLaunchAudio.Enabled){RuntimeLaunchAudio.PreloadClips();audioHarmony.PatchAll(typeof(RuntimeLaunchAudio));Log.LogInfo("[ATGM audio] Supplied TOW launch sound enabled for launcher and gun-launched missiles.");}
        }
        catch(Exception ex){audioHarmony.UnpatchSelf();Log.LogWarning("[ATGM audio] Optional launch audio unavailable; native sounds retained: "+ex.Message);}
        var aiHarmony=new Harmony("nl.roan.sprocket.shellselector.atgmai");
        try{aiHarmony.PatchAll(typeof(RuntimeAtgmAi));Log.LogInfo("[ATGM AI] Scoped native aiming hooks loaded.");}
        catch(Exception ex){aiHarmony.UnpatchSelf();Log.LogWarning("[ATGM AI] Optional AI guidance unavailable: "+ex.Message);}
        var trailHarmony=new Harmony("nl.roan.sprocket.shellselector.atgmtrail");
        try{RuntimeAtgmTrail.Configure(Config);trailHarmony.PatchAll(typeof(RuntimeAtgmTrail));Log.LogInfo("[ATGM trail] Experimental motor flame/smoke hooks loaded.");}
        catch(Exception ex){trailHarmony.UnpatchSelf();Log.LogWarning("[ATGM trail] Optional visuals unavailable: "+ex.Message);}
        var simulatorHarmony = new Harmony("nl.roan.sprocket.shellselector.simulator");
        try
        {
            RuntimeArmourSimulator.Configure();
            simulatorHarmony.PatchAll(typeof(RuntimeShellEra));
            simulatorHarmony.PatchAll(typeof(RuntimeArmourSimulator));
            Log.LogInfo("[Armour Simulator] Patches loaded.");
        }
        catch (Exception ex)
        {
            simulatorHarmony.UnpatchSelf();
            Log.LogError($"[Armour Simulator] Disabled; shell and spall/APHE patches remain active: {ex}");
        }
        var effectsHarmony=new Harmony("nl.roan.sprocket.shellselector.effects");
        var scrollHarmony=new Harmony("nl.roan.sprocket.shellselector.simulatorscroll");
        try{scrollHarmony.PatchAll(typeof(RuntimeSimulatorScroll));Log.LogInfo("[Simulator scroll] Native shell list scrollbar hook loaded.");}
        catch(Exception ex){scrollHarmony.UnpatchSelf();Log.LogError($"[Simulator scroll] Optional scrollbar disabled: {ex}");}
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
