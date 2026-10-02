using BepInEx;
using HarmonyLib;
using Sprocket.DamageModelling;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace SprocketShellSelector;
internal sealed class ShellImpactContext
{
    internal string ProfileId = "";
    internal float GunDiameter;
    internal bool ApheBurstMade;
    internal readonly ApheFuse Fuse = new();
}
[HarmonyPatch]
internal static class RuntimeSpall
{
    internal static SpallSettings Settings = new();
    [ThreadStatic] internal static ShellImpactContext? Impact;
    [ThreadStatic] private static bool sphericalBurst;
    [ThreadStatic] private static bool emittingAphe;
    [ThreadStatic] private static CompoundStructure? activeStructure;
    internal static void Configure()
    {
        var path=Path.Combine(Paths.ConfigPath,"sprocket.shellselector.spall.json");
        var previous=Path.Combine(Paths.ConfigPath,"nl.roan.sprocket.shellselector.spall.json");
        if(!File.Exists(path) && File.Exists(previous))
        {
            var old=SpallBalance.Parse(File.ReadAllText(previous));
            // Upgrade only the untouched previous experiment's default burst.
            if(old.ApheFragmentCount==24 && old.ApheFragmentMass==.6 && old.ApheFragmentSpeed==450)
                old=old with {ApheFragmentCount=96,ApheFragmentMass=2.4,ApheFragmentSpeed=650};
            File.WriteAllText(path,SpallBalance.ToJson(old));
        }
        if(!File.Exists(path)) File.WriteAllText(path,SpallBalance.ToJson(new()));
        Settings=SpallBalance.Parse(File.ReadAllText(path));
        Plugin.ModLog.LogInfo("[Spall] APFSDS remaining-penetration/cone and APHE spherical burst enabled. "+path);
    }
    private sealed record BurstState(bool PreviousSphere, int CountBefore, bool Custom, int ParentIndex, bool Aphe);
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.Penetrate))]
    private static void BeginPenetrate(CompoundStructure __instance,out CompoundStructure? __state)
    {
        __state=activeStructure;
        activeStructure=__instance;
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.Penetrate))]
    private static void EndPenetrate(CompoundStructure? __state) => activeStructure=__state;
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static bool Burst(PenetrationSimulation simulation, ref CompoundStructure.SpallSpawnBurst o, out BurstState __state)
    {
        __state=new(sphericalBurst,simulation.FragmentCount,false,-1,false);
        if(emittingAphe) return true;
        try
        {
            var context=Impact;
            if(context == null || context.ProfileId is not ("apfsds" or "aphe")) return true;
            if(o.parentIndex < 0 || o.parentIndex >= simulation.FragmentCount) return true;
            var parent=simulation.fragments[o.parentIndex];
            if((parent.flags & FragmentFlag.OriginalPenetrator)==0 || parent.materialIndex < 0) return true;
            var s=Settings;
            var aphe=context.ProfileId=="aphe";
            if(aphe)
            {
                if(context.ApheBurstMade) return false;
                if((parent.flags & (FragmentFlag.Embedded|FragmentFlag.Deflected|FragmentFlag.Killed))!=0) return true;
                var rhaMm=Math.Max(0,parent.travelDistance)*1000*simulation.materials[parent.materialIndex].rhaFactor;
                if(!context.Fuse.Traverse(simulation.Pointer,o.parentIndex,rhaMm,s)) return true; // Normal AP spall and continuation until armed.
                if(!(o.density>0) || !(o.spallFactor>0)) return true;
                var direction=parent.direction.normalized;
                var exit=parent.EndPoint;
                var delay=(float)(parent.speed*s.ApheFuseDelayMilliseconds*.001);
                // Same simulation-space raycast as the native fragment route. Do not jump through an inner plate.
                if(delay>0 && activeStructure!=null && activeStructure.Raycast(exit+direction*.001f,direction,delay,out StructureIntersection hit))
                    delay=Math.Max(0,hit.distance-.001f);
                o.spawnPoint=exit+direction*(.001f+delay);
                o.speed=(float)s.ApheFragmentSpeed;
                o.planeNormal=Vector3.zero;
                o.depthLookup=null; // Rebuild material occupancy at the delayed interior origin.
                sphericalBurst=true;
                emittingAphe=true;
                var first=simulation.FragmentCount;
                try
                {
                    for(var left=s.ApheFragmentCount;left>0;left-=32)
                    {
                        var batch=Math.Min(32,left);
                        o.crossectionalArea=(float)(batch*o.spallFactor);
                        o.volume=(float)(s.ApheFragmentMass*batch/s.ApheFragmentCount/o.density);
                        CompoundStructure.CreateSpallBurst(simulation,ref o);
                    }
                }
                finally {emittingAphe=false;}
                if(simulation.FragmentCount==first) return false;
                __state=__state with {Custom=true,ParentIndex=o.parentIndex,Aphe=true};
                context.ApheBurstMade=true;
                Plugin.ModLog.LogInfo($"[APHE Fuse] armedRha={context.Fuse.AccumulatedRhaMm:0.0}mm delayDistance={delay:0.000}m burstFragments={simulation.FragmentCount-first}");
                return false;
            }
            else
            {
                __state=__state with {Custom=true,ParentIndex=o.parentIndex};
                var original=simulation.Penetrator;
                var initialPen=PenetrationUtils.ComputePenetration(original.Diameter*1000,original.Mass,original.Velocity.magnitude,original.PenetratorConstant);
                var remainingPen=parent.GetBasePenetration(simulation.projectileProfiles[parent.projectileProfileIndex].penetratorConstant)*1000;
                if(!float.IsFinite(initialPen) || initialPen<=0 || !float.IsFinite(remainingPen)) return true;
                var remainingFraction=Math.Clamp(remainingPen/initialPen,0,1);
                var ratio=SpallBalance.RemainingPenetrationRatio(remainingFraction,s);
                // Full-calibre AP reference capacity; no plate-thickness anchors or narrow sweet spot.
                o.volume=(float)(SpallBalance.ReferenceApVolume(context.GunDiameter,context.GunDiameter)*ratio);
                o.crossectionalArea=(float)(Math.PI*context.GunDiameter*context.GunDiameter*.25*ratio);
                var energyFraction=original.Mass>0 && original.Velocity.sqrMagnitude>0
                    ? parent.mass*parent.speed*parent.speed/(original.Mass*original.Velocity.sqrMagnitude):0;
                var cone=SpallBalance.ConeFactor(energyFraction,s);
                o.spread *= (float)cone;
                Plugin.ModLog.LogInfo($"[Spall] APFSDS remaining={remainingPen:0.0}/{initialPen:0.0}mm fraction={remainingFraction:0.000} massAndCountRatioToAP={ratio:0.000} coneFactor={cone:0.000}");
            }
        }
        catch(Exception ex) { Plugin.ModLog.LogError("[Spall] Burst tuning failed: "+ex); }
        return true;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static void BurstDone(PenetrationSimulation simulation, BurstState __state)
    {
        if(!__state.Custom) return;
        var count=simulation.FragmentCount-__state.CountBefore;
        if (__state.Aphe && count == 0 && Impact != null) Impact.ApheBurstMade = false;
        if(__state.Aphe && count>0)
        {
            var parent=simulation.fragments[__state.ParentIndex];
            parent.speed=0;
            parent.flags |= FragmentFlag.Killed;
            simulation.fragments[__state.ParentIndex]=parent;
            // These native profiles are per simulation, not shared vanilla ammunition data.
            var index=simulation.fragments[__state.CountBefore].projectileProfileIndex;
            var profile=simulation.projectileProfiles[index];
            var oldProfiles=simulation.projectileProfiles;
            if(oldProfiles.Length>=256) throw new InvalidOperationException("No spare APHE fragment profile slot.");
            var profiles=new Il2CppStructArray<ProjectileProfile>(oldProfiles.Length+1);
            for(var i=0;i<oldProfiles.Length;i++) profiles[i]=oldProfiles[i];
            profile.penetratorConstant=(ushort)Settings.ApheFragmentK;
            profiles[oldProfiles.Length]=profile;
            simulation.projectileProfiles=profiles;
            for(var i=0;i<count;i++)
            {
                var fragment=simulation.fragments[__state.CountBefore+i];
                var point=SpallBalance.Sphere((i*.6180339887498949)%1,(i+.5)/count);
                fragment.direction=new Vector3(point.X,point.Y,point.Z);
                fragment.projectileProfileIndex=(byte)oldProfiles.Length;
                simulation.fragments[__state.CountBefore+i]=fragment;
            }
            // Fragment array access boxes native value types. Write the stopped parent back
            // before the native caller computes its next penetration/continuation.
            Plugin.ModLog.LogInfo("[Spall] APHE original penetrator stopped at burst; speed=0.");
        }
        Plugin.ModLog.LogInfo($"[Spall] {(__state.Aphe?"APHE":"APFSDS")} spawned={count} parent={__state.ParentIndex}");
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static void EndBurst(BurstState __state) => sphericalBurst=__state.PreviousSphere;
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static bool Simulate(CompoundStructure __instance,PenetrationSimulation sim, short index,out CompoundStructure? __state)
    {
        __state=activeStructure;
        activeStructure=__instance;
        if(Impact is not {ProfileId: "aphe", ApheBurstMade: true} || index<0 || index>=sim.FragmentCount) return true;
        var fragment=sim.fragments[index];
        if((fragment.flags & FragmentFlag.OriginalPenetrator)==0) return true;
        // The APHE approximation has broken up: stop the intact projectile continuation.
        fragment.flags |= FragmentFlag.Killed;
        fragment.speed=0;
        sim.fragments[index]=fragment;
        return false;
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static void EndSimulate(CompoundStructure? __state) => activeStructure=__state;
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.GetFragmentDirection))]
    private static bool Direction(ref Vector3 __result)
    {
        if(!sphericalBurst) return true;
        // Uniform sphere; replaces only APHE fragment direction calls inside the scoped burst.
        var point=SpallBalance.Sphere(System.Random.Shared.NextDouble(),System.Random.Shared.NextDouble());
        __result=new Vector3(point.X,point.Y,point.Z);
        return false;
    }
}



