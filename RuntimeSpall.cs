using BepInEx;
using HarmonyLib;
using Sprocket.DamageModelling;
namespace SprocketShellSelector;
internal sealed class ShellImpactContext
{
    internal string ProfileId = "";
    internal float GunDiameter;
    internal bool LiveImpact;
    internal bool ExplosionPending;
    internal UnityEngine.Vector3 ExplosionPosition;
}
[HarmonyPatch]
internal static class RuntimeSpall
{
    internal static SpallSettings Settings = new();
    [ThreadStatic] internal static ShellImpactContext? Impact;
    [ThreadStatic] private static UnityEngine.Vector3? apheAxis;
    internal static void Configure()
    {
        var path=Path.Combine(Paths.ConfigPath,"sprocket.shellselector.spall.json");
        var previous=Path.Combine(Paths.ConfigPath,"nl.roan.sprocket.shellselector.spall.json");
        if(!File.Exists(path) && File.Exists(previous))
            File.WriteAllText(path,SpallBalance.ToJson(SpallBalance.Parse(File.ReadAllText(previous))));
        if(!File.Exists(path)) File.WriteAllText(path,SpallBalance.ToJson(new()));
        Settings=SpallBalance.Parse(File.ReadAllText(path));
        Plugin.ModLog.LogInfo("[Spall] APFSDS remaining-penetration/cone and APHE amplified native AP spall enabled. "+path);
    }
    private sealed record BurstState(int CountBefore,bool Custom,bool Aphe,UnityEngine.Vector3 ExplosionPosition=default,UnityEngine.Vector3? PreviousAxis=null);
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.Penetrate))]
    private static void ExperimentalNormalization(ref PenetratorInfo penetrator)
    {
        if(Impact?.ProfileId!="apfsds" || !Settings.ApfsdsDisableClassicNormalization)return;
        penetrator=new PenetratorInfo(penetrator.Mass,penetrator.Diameter,penetrator.Density,0,
            penetrator.PenetratorConstant,penetrator.Position,penetrator.Velocity);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static void Burst(PenetrationSimulation simulation,ref CompoundStructure.SpallSpawnBurst o,out BurstState __state)
    {
        __state=new(simulation.FragmentCount,false,false,PreviousAxis:apheAxis);
        try
        {
            var context=Impact;
            if(context==null || context.ProfileId is not ("apfsds" or "aphe")) return;
            if(o.parentIndex<0 || o.parentIndex>=simulation.FragmentCount) return;
            var parent=simulation.fragments[o.parentIndex];
            if((parent.flags & FragmentFlag.OriginalPenetrator)==0 || parent.materialIndex<0) return;
            var s=Settings;
            var aphe=context.ProfileId=="aphe";
            if(aphe)
            {
                // Keep native AP origin, direction, spread, material occupancy, speed and profile.
                // The same native loop evaluates both the original projectile and its fragments.
                o.volume*=(float)s.ApheSpallMultiplier;
                o.crossectionalArea*=(float)s.ApheSpallMultiplier;
                apheAxis=o.planeNormal.sqrMagnitude>.5f?o.planeNormal.normalized:o.direction.normalized;
                __state=__state with {Custom=true,Aphe=true,
                    ExplosionPosition=context.LiveImpact?simulation.SimToWorldSpacePoint(o.spawnPoint):default};
            }
            else
            {
                var original=simulation.Penetrator;
                var initialPen=PenetrationUtils.ComputePenetration(original.Diameter*1000,original.Mass,original.Velocity.magnitude,original.PenetratorConstant);
                var remainingPen=parent.GetBasePenetration(simulation.projectileProfiles[parent.projectileProfileIndex].penetratorConstant)*1000;
                if(!float.IsFinite(initialPen) || initialPen<=0 || !float.IsFinite(remainingPen)) return;
                var remainingFraction=Math.Clamp(remainingPen/initialPen,0,1);
                var ratio=SpallBalance.RemainingPenetrationRatio(remainingFraction,s);
                o.volume=(float)(SpallBalance.ReferenceApVolume(context.GunDiameter,context.GunDiameter)*ratio);
                o.crossectionalArea=(float)(Math.PI*context.GunDiameter*context.GunDiameter*.25*ratio);
                var energyFraction=original.Mass>0 && original.Velocity.sqrMagnitude>0
                    ? parent.mass*parent.speed*parent.speed/(original.Mass*original.Velocity.sqrMagnitude):0;
                var cone=SpallBalance.ConeFactor(energyFraction,s);
                o.spread*=(float)cone;
                __state=__state with {Custom=true};
                Plugin.ModLog.LogInfo($"[Spall] APFSDS remaining={remainingPen:0.0}/{initialPen:0.0}mm fraction={remainingFraction:0.000} massAndCountRatioToAP={ratio:0.000} coneFactor={cone:0.000}");
            }
        }
        catch(Exception ex){Plugin.ModLog.LogError("[Spall] Burst tuning failed: "+ex);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static void EndBurst(BurstState __state) => apheAxis=__state.PreviousAxis;
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.GetFragmentDirection))]
    private static bool ConeDirection(ref UnityEngine.Vector3 __result)
    {
        if(apheAxis is not {} axis)return true;
        var helper=Math.Abs(axis.y)<.9f?UnityEngine.Vector3.up:UnityEngine.Vector3.right;
        var tangent=UnityEngine.Vector3.Cross(helper,axis).normalized;
        var bitangent=UnityEngine.Vector3.Cross(axis,tangent);
        var sample=SpallBalance.Cone(System.Random.Shared.NextDouble(),System.Random.Shared.NextDouble(),Settings.ApheConeHalfAngleDegrees);
        __result=axis*sample.Z+tangent*sample.X+bitangent*sample.Y;
        return false;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static void BurstDone(PenetrationSimulation simulation,BurstState __state)
    {
        if(!__state.Custom)return;
        var count=simulation.FragmentCount-__state.CountBefore;
        Plugin.ModLog.LogInfo($"[Spall] {(__state.Aphe?"APHE native AP":"APFSDS")} spawned={count}"+
            (__state.Aphe?$" volumeAndCountInputMultiplier={Settings.ApheSpallMultiplier:0.0}; native directions/continuation preserved":""));
        if(__state.Aphe && count>0 && Impact is {LiveImpact:true,ExplosionPending:false} context)
        {
            context.ExplosionPosition=__state.ExplosionPosition;
            context.ExplosionPending=true;
        }
    }
}
