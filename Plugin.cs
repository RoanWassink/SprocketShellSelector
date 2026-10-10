using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine.Events;
namespace SprocketShellSelector;
[BepInPlugin("sprocket.shellselector", "Sprocket Shell Selector", "0.13.0")]
[BepInDependency("sprocket.jsoneditor", ">=0.1.0 <0.2.0")]
public sealed class Plugin : BasePlugin
{
    public override bool Unload(){RuntimeWire.Clear();RuntimeDamageFeed.Clear();return base.Unload();}
    internal static ManualLogSource ModLog = null!;
    public static void RefreshArmourResponses()
    {
        var enabled=RuntimeArmourResponses.Configure();
        const string owner="sprocket.shellselector.armourresponses";
        if(enabled&&!Harmony.HasAnyPatches(owner))new Harmony(owner).PatchAll(typeof(RuntimeArmourResponses));
    }
    public override void Load()
    {
        ModLog = Log;
        try
        {
            RuntimeDamageFeed.Configure(Config);
            new Harmony("sprocket.shellselector.damagefeed").PatchAll(typeof(RuntimeDamageFeed));
            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<DamageFeedBehaviour>();
            AddComponent<DamageFeedBehaviour>();
            RuntimeDamageFeed.Ready=true;
            try{new Harmony("sprocket.shellselector.damagefeed.health").PatchAll(typeof(RuntimeDamageHealth));}
            catch(Exception healthError){new Harmony("sprocket.shellselector.damagefeed.health").UnpatchSelf();Log.LogWarning("[Damage feed] Native health reporting unavailable; penetration HUD retained: "+healthError.Message);}
            Log.LogInfo("[Damage feed] Optional Play HUD hooks loaded; default off, cannon panel toggle.");
        }
        catch(Exception ex){RuntimeDamageFeed.Ready=false;new Harmony("sprocket.shellselector.damagefeed.health").UnpatchSelf();new Harmony("sprocket.shellselector.damagefeed").UnpatchSelf();Log.LogWarning("[Damage feed] Optional feature unavailable: "+ex.Message);}

        var harmony = new Harmony(PluginConfigMigration.PluginId);
        try
        {
            if(PluginConfigMigration.EnsureCurrent(Paths.ConfigPath))
            {
                Config.Reload();
                Log.LogInfo("[Configuration] Previous plugin settings copied to sprocket.shellselector.cfg; original retained for rollback.");
            }
            if (Harmony.HasAnyPatches("sprocket.materialselector.apfsdsbeta"))
                throw new InvalidOperationException("Combined Material Selector shell hooks detected. Close the game and use a compatible Material Selector without bundled shell hooks.");
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
        Log.LogInfo("Sprocket Shell Selector v0.13.0 loaded.");
        var armourHarmony=new Harmony("sprocket.shellselector.armourresponses");
        try{if(RuntimeArmourResponses.Configure())armourHarmony.PatchAll(typeof(RuntimeArmourResponses));}
        catch(Exception ex){armourHarmony.UnpatchSelf();Log.LogWarning("[Armour response] Optional adapter disabled: "+ex.Message);}
        var atgmHarmony=new Harmony("sprocket.shellselector.atgm");
        try
        {
            RuntimeAtgm.Configure(Config);
            if(RuntimeAtgm.Enabled){atgmHarmony.PatchAll(typeof(RuntimeAtgm));RuntimeAtgm.Ready=true;Log.LogInfo("[ATGM] Powered missile flight and guidance loaded.");}
        }
        catch(Exception ex){atgmHarmony.UnpatchSelf();RuntimeAtgm.Ready=false;Log.LogError("[ATGM] Optional flight guidance unavailable; other shell hooks remain active: "+ex);}
        var launcherHarmony=new Harmony("sprocket.shellselector.atgmlauncher");
        try{launcherHarmony.PatchAll(typeof(RuntimeAtgmLauncher));RuntimeAtgmLauncher.Ready=true;Log.LogInfo("[ATGM launcher] Native cannon adapter, ATGM-only gate and optional model/icon hooks loaded.");}
        catch(Exception ex){launcherHarmony.UnpatchSelf();RuntimeAtgmLauncher.Ready=false;Log.LogWarning("[ATGM launcher] Adapter unavailable; dedicated launcher shots withheld: "+ex.Message);}
        var opticsHarmony=new Harmony("sprocket.shellselector.atgmlauncher.optics");
        var optics=Config.Bind("ATGM Launcher Tests","OpticalSightDirection",false,"Initialize the dedicated launcher optical sight direction after native activation. Restart to change; retains native mouse control and physical ejection.");
        try{if(optics.Value){opticsHarmony.PatchAll(typeof(RuntimeAtgmLauncherOptics));Log.LogInfo("[ATGM optics] Dedicated launcher optical sight initialization enabled.");}else Log.LogInfo("[ATGM optics] Disabled; native scope initialization retained.");}
        catch(Exception ex){opticsHarmony.UnpatchSelf();Log.LogWarning("[ATGM optics] Optional initialization unavailable: "+ex.Message);}
        var initialHarmony=new Harmony("sprocket.shellselector.atgmlauncher.initialround");
        var initial=Config.Bind("ATGM Launcher Tests","InitialReadyMissile",true,"One finite native initial chamber round per dedicated launcher per new combat instance. No reserve refill; gunner/readiness retained. Restart to change.");
        try{if(initial.Value){initialHarmony.PatchAll(typeof(RuntimeAtgmInitialRound));RuntimeAtgmInitialRound.Ready=true;Log.LogInfo("[ATGM initial] One finite native initial missile enabled.");}}
        catch(Exception ex){initialHarmony.UnpatchSelf();RuntimeAtgmInitialRound.Ready=false;Log.LogWarning("[ATGM initial] Optional starting missile unavailable: "+ex.Message);}
        var boxHarmony=new Harmony("sprocket.shellselector.atgmammobox");
        var automaticBox=Config.Bind("ATGM Launcher Tests","AutomaticAmmoBox",false,"Enable automatic loading from the dedicated finite ATGM ammunition box. Restart to change. Requires a matching nearby launcher and assigned gunner; ordinary ammunition racks are not automatic reserves.");
        try{if(automaticBox.Value){boxHarmony.PatchAll(typeof(RuntimeAtgmAmmoBox));RuntimeAtgmAmmoBox.Ready=true;Log.LogInfo("[ATGM ammo box] Finite automatic feed enabled.");}else Log.LogInfo("[ATGM ammo box] Native rack and container visuals enabled; automatic feed disabled.");}
        catch(Exception ex){boxHarmony.UnpatchSelf();RuntimeAtgmAmmoBox.Ready=false;Log.LogWarning("[ATGM ammo box] Automatic feed unavailable: "+ex.Message);}
        var boxVisualHarmony=new Harmony("sprocket.shellselector.atgmammobox.visuals");
        try{boxVisualHarmony.PatchAll(typeof(AtgmAmmoBoxVisuals));}
        catch(Exception ex){boxVisualHarmony.UnpatchSelf();Log.LogWarning("[ATGM ammo box] Optional container visuals unavailable: "+ex.Message);}
        Log.LogInfo("[ATGM launcher] Dedicated launcher uses native sight association and physical ejection.");
        var audioHarmony=new Harmony("sprocket.shellselector.atgmaudio");
        try
        {
            RuntimeLaunchAudio.Configure(Config);
            if(RuntimeLaunchAudio.Enabled){RuntimeLaunchAudio.PreloadClips();audioHarmony.PatchAll(typeof(RuntimeLaunchAudio));Log.LogInfo("[ATGM audio] Supplied TOW launch sound enabled for launcher and gun-launched missiles.");}
        }
        catch(Exception ex){audioHarmony.UnpatchSelf();Log.LogWarning("[ATGM audio] Optional launch audio unavailable; native sounds retained: "+ex.Message);}
        var aiHarmony=new Harmony("sprocket.shellselector.atgmai");
        try{aiHarmony.PatchAll(typeof(RuntimeAtgmAi));Log.LogInfo("[ATGM AI] Scoped native aiming hooks loaded.");}
        catch(Exception ex){aiHarmony.UnpatchSelf();Log.LogWarning("[ATGM AI] Optional AI guidance unavailable: "+ex.Message);}
        var trailHarmony=new Harmony("sprocket.shellselector.atgmtrail");
        try{RuntimeAtgmTrail.Configure(Config);trailHarmony.PatchAll(typeof(RuntimeAtgmTrail));Log.LogInfo("[ATGM trail] Motor flame and smoke hooks loaded.");}
        catch(Exception ex){trailHarmony.UnpatchSelf();Log.LogWarning("[ATGM trail] Optional visuals unavailable: "+ex.Message);}
        var simulatorHarmony = new Harmony("sprocket.shellselector.simulator");
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
        var effectsHarmony=new Harmony("sprocket.shellselector.effects");
        var scrollHarmony=new Harmony("sprocket.shellselector.simulatorscroll");
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









