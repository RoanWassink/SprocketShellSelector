using HarmonyLib;
using Sprocket.ArtificialIntelligence;
using Sprocket.Vehicles.Cannons;
using UnityEngine;
using NativeBallistics = Sprocket.PhysicsRules.Ballistics;
using NVector = System.Numerics.Vector3;
namespace SprocketShellSelector;

// Scope the native intercept override to one identified ATGM gun-layer AI call.
internal static class RuntimeAtgmAi
{
    private sealed record Context(Cannon Cannon,ShellProfile Profile);
    private sealed record Aim(Vector3 Origin,Vector3 Direction,float Time);
    private static readonly Dictionary<IntPtr,Cannon> Cannons=new();
    private static readonly Dictionary<IntPtr,Aim> Aims=new();
    private static readonly HashSet<string> Warnings=new();
    [ThreadStatic] private static Context? active;
    [HarmonyPostfix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Build))]
    private static void Built(Cannon __instance)=>Cannons[__instance.Pointer]=__instance;
    [HarmonyPrefix,HarmonyPatch(typeof(Cannon),nameof(Cannon.DisableBehaviour))]
    private static void Released(Cannon __instance)
    {Cannons.Remove(__instance.Pointer);if(__instance.Behaviour!=null)Aims.Remove(__instance.Behaviour.Pointer);}

    [HarmonyPrefix,HarmonyPatch(typeof(GunLayerAI),nameof(GunLayerAI.Step))]
    private static void BeforeAi(GunLayerAI __instance,out Context? __state)
    {
        __state=active;active=null;
        if(!RuntimeAtgm.Ready || !RuntimeAtgm.Enabled)return;
        try
        {
            var mechanism=__instance.Layer?.ControlTarget;
            if(mechanism==null)return;
            foreach(var cannon in Cannons.Values)
            {
                if(cannon.HorizontalAimingMechanism?.Pointer!=mechanism.Pointer && cannon.VerticalAimingMechanism?.Pointer!=mechanism.Pointer)continue;
                var profile=RuntimeShellSelection.CannonProfile(cannon);
                if(profile==null || !ShellBalance.IsAtgm(profile.Behavior) || profile.Atgm?.GuidanceMode=="none" || cannon.HealthFraction<=0)continue;
                var vehicle=cannon.Vehicle?.Behaviour;
                if(vehicle!=null && RuntimeAtgm.IsPlayerVehicle(vehicle.Pointer))continue;
                active=new(cannon,profile);
                if(Warnings.Add("matched:"+cannon.Pointer))Plugin.ModLog.LogInfo($"[ATGM AI] Matched native gun layer to profile={profile.Id} weapon={cannon.Behaviour?.Pointer}");
                return;
            }
        }
        catch(Exception ex){Warn("AI layer lookup",ex);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(GunLayerAI),nameof(GunLayerAI.Step))]
    private static void AfterAi(Context? __state)=>active=__state;

    [HarmonyPrefix,HarmonyPatch(typeof(NativeBallistics),nameof(NativeBallistics.CalculateInterceptPoint),new[]{typeof(Vector3),typeof(Vector3),typeof(Vector3),typeof(float),typeof(int)})]
    private static bool Intercept(Vector3 __0,Vector3 __1,Vector3 __2,ref Vector3 __result)
    {
        if(active==null)return true;
        try
        {
            static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
            if(!Finite(__0)||!Finite(__1)||!Finite(__2))return true;
            var point=AtgmGuidance.AiIntercept(new NVector(__0.x,__0.y,__0.z),new NVector(__1.x,__1.y,__1.z),new NVector(__2.x,__2.y,__2.z),active.Profile.Atgm!);
            var result=new Vector3(point.X,point.Y,point.Z);
            if(!Finite(result))return true;
            __result=result;
            if(Warnings.Add("intercept:"+active.Cannon.Pointer))Plugin.ModLog.LogInfo($"[ATGM AI] Native intercept replaced without shell drop; profile={active.Profile.Id} target={result}");
            var weapon=active.Cannon.Behaviour;
            var direction=result-__2;
            if(weapon!=null && direction.sqrMagnitude>1)
                Aims[weapon.Pointer]=new(__2,direction.normalized,Time.time);
            return false;
        }
        catch(Exception ex){Warn("AI intercept",ex);return true;}
    }
    internal static bool TryAim(IntPtr weapon,float now,out Vector3 origin,out Vector3 direction)
    {
        origin=default;direction=default;
        if(!Aims.TryGetValue(weapon,out var aim)||!AtgmGuidance.FreshAim(now,aim.Time))return false;
        origin=aim.Origin;direction=aim.Direction;return true;
    }
    // Projectile reset does not destroy live cannon assemblies; DisableBehaviour removes them.
    internal static void Clear(){Aims.Clear();Warnings.Clear();active=null;}
    private static void Warn(string area,Exception ex)
    {if(Warnings.Add(area))Plugin.ModLog.LogWarning($"[ATGM AI] {area}: {ex.Message}");}
}
