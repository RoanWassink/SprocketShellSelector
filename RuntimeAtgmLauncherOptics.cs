using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.AttachedBehaviours;
using Sprocket.WeaponFramework;
using UnityEngine;
namespace SprocketShellSelector;

[HarmonyPatch]
internal static class RuntimeAtgmLauncherOptics
{
    private static int reports;
    private static readonly HashSet<string> warnings=new();
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list)=>list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
    [HarmonyPostfix,HarmonyPatch(typeof(ScopeController),nameof(ScopeController.ActivateSight))]
    private static void Activated(ScopeController __instance,IGunnerSight __0)
    {
        try
        {
            var optical=__0?.TryCast<GunnerSight>();
            if(optical?.Vehicle==null||!RuntimeAtgm.IsPlayerVehicle(optical.Vehicle.Pointer))return;
            Cannon? own=null;int matches=0;bool ordinary=false;
            var items=optical.Vehicle.ObjectReader.Items;
            for(int i=0;i<Count(items);i++)
            {
                var parts=items[i].Components;
                for(int j=0;j<Count(parts);j++)
                {
                    var c=parts[j].TryCast<Cannon>();
                    if(c==null||!c.TryGetAssignedSight(out var assigned)||assigned?.Pointer!=optical.Pointer)continue;
                    if(RuntimeAtgmLauncher.IsLauncher(c)){matches++;own=c;}else ordinary=true;
                }
            }
            bool active=__instance.activeSight?.Pointer==optical.Pointer;
            bool weapon=own?.Behaviour?.Sight?.Pointer==optical.Pointer;
            if(!AtgmOpticalScope.Allows(true,active,matches,ordinary,weapon,own!=null&&RuntimeAtgmLauncher.CanLaunch(own)))
            {
                if(reports++<16)Plugin.ModLog.LogInfo($"[ATGM optics] SKIP sight={optical.VUID.Value} active={active} ownAssociations={matches} ordinaryShared={ordinary} nativeWeaponSight={weapon}");
                return;
            }
            var parent=__0!.SightParent;var camera=__instance.controller;var linked=__0.LinkedWeapon;
            if(parent==null||camera==null||linked==null)return;
            var angles=parent.rotation.eulerAngles;
            if(!float.IsFinite(angles.x)||!float.IsFinite(angles.y)||!float.IsFinite(angles.z))return;
            var before=camera.EulerAngles;var nativeParent=camera.Parent;
            // Native ActivateSight already owns the optical parent and eyepiece.
            // Call its existing angle setter once, using the real sight's world rotation.
            camera.EulerAngles=angles;
            if(reports++<16)Plugin.ModLog.LogInfo($"[ATGM optics] INIT launcher={own!.VUID.Value} sight={optical.VUID.Value} before={before} optical={angles} after={camera.EulerAngles} opticsForward={parent.forward} tubeForward={own.Barrel?.gameObject.transform.forward} parentUnchanged={camera.Parent?.Pointer==nativeParent?.Pointer} linkedUnchanged={__0.LinkedWeapon?.Pointer==linked.Pointer}");
        }
        catch(Exception ex){if(warnings.Add(ex.Message))Plugin.ModLog.LogWarning("[ATGM optics] Initialization skipped: "+ex.Message);}
    }
}
