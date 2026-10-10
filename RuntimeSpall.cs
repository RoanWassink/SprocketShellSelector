using BepInEx;
using HarmonyLib;
using Sprocket.DamageModelling;
namespace SprocketShellSelector;
internal sealed class ShellImpactContext
{
    internal string ProfileId = "";
    internal ShellProfile? Profile;
    internal string Behavior => Profile is {} profile ? ShellBalance.ImpactBehavior(profile) : ProfileId;
    internal bool ChemicalInitialized;
    internal bool ChemicalVisualPlayed;
    internal readonly HashSet<IntPtr> PayloadBursts = new();
    internal readonly Dictionary<IntPtr,ChemicalLayers> Layers = new();
    internal float GunDiameter;
    internal string? FiringEra;
    internal double ChemicalBudget;
    internal bool LiveImpact;
    internal bool ExplosionPending;
    internal UnityEngine.Vector3 ExplosionPosition;
    internal readonly List<UnityEngine.Vector3> ArmourExplosions=new();
    internal readonly List<PlacedEraActivation> PlacedActivations=new();
    internal bool ArmourEffectPlaying;
}
[HarmonyPatch]
internal static class RuntimeSpall
{
    internal static SpallSettings Settings = new();
    [ThreadStatic] internal static ShellImpactContext? Impact;
    [ThreadStatic] private static UnityEngine.Vector3? apheAxis;
    [ThreadStatic] private static PenetrationSimulation? layerSimulation;
    [ThreadStatic] private static short layerFragment;
    [ThreadStatic] private static CompoundStructure? layerStructure;
    private static int chemicalDiagnosticBudget=80;
    private sealed record LayerScope(PenetrationSimulation? Simulation,short Index,CompoundStructure? Structure);
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static void BeginLayer(CompoundStructure __instance,PenetrationSimulation sim,short index,out LayerScope __state)
    {
        __state=new(layerSimulation,layerFragment,layerStructure);
        layerStructure=__instance;
        layerSimulation=Impact?.Behavior is "heat" or "hesh" ? sim : null;
        layerFragment=index;
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.SimulateFragment))]
    private static void EndLayer(LayerScope __state){layerSimulation=__state.Simulation;layerFragment=__state.Index;layerStructure=__state.Structure;}
    [HarmonyPostfix,HarmonyPatch(typeof(StructureIntersection),nameof(StructureIntersection.GetNextMaterialBlock))]
    private static void MaterialLayer(MaterialBlock __result,
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<StructureIntersection> __0,int __1,
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<MaterialInfo> __5)
    {
        var sim=layerSimulation;var context=Impact;
        if(sim==null || context?.Profile is not {} profile || !__result.Valid || layerFragment<0 || layerFragment>=sim.FragmentCount)return;
        var fragment=sim.fragments[layerFragment];
        if((fragment.flags & FragmentFlag.OriginalPenetrator)==0)return;
        var entry=__result.EnterIntersectionIndex;var exit=__result.ExitIntersectionIndex;
        if(entry<0||exit<0||entry>=__1||exit>=__1||entry>=__0.Length||exit>=__0.Length)return;
        var length=Math.Max(0,(__0[exit].distance-__0[entry].distance)*1000);
        var solid=__result.MaterialIndex>=0;
        if(solid){if(__result.MaterialIndex>=__5.Length)return;length*=Math.Max(0,__5[__result.MaterialIndex].rhaFactor);}
        if(solid&&layerStructure is {} structure&&chemicalDiagnosticBudget>0)
        {
            try
            {
            var first=__0[entry];
            var normal=structure.GetIntersectionNormal(ref first).normalized;
            var cosine=Math.Clamp(Math.Abs(UnityEngine.Vector3.Dot(fragment.direction.normalized,normal)),0,1);
            var losMm=Math.Max(0,(__0[exit].distance-__0[entry].distance)*1000);
            var diagnosticConstant=sim.projectileProfiles[fragment.projectileProfileIndex].penetratorConstant;
            var inputMm=fragment.GetBasePenetration(diagnosticConstant)*1000;
            chemicalDiagnosticBudget--;
            Plugin.ModLog.LogInfo($"[Chemical plate] profile={profile.Id} live={context.LiveImpact} angleFromNormal={Math.Acos(cosine)*180/Math.PI:0.0}deg normalThickness={losMm*cosine:0.0}mm pathThickness={losMm:0.0}mm pathRHA={length:0.0}mm incomingBaseBudget={inputMm:0.0}mm; native armour consumption follows (session trace capped80).");
            }
            catch(Exception ex){chemicalDiagnosticBudget=0;Plugin.ModLog.LogWarning("[Chemical plate] Optional trace disabled: "+ex.Message);}
        }
        if(!context.Layers.TryGetValue(sim.Pointer,out var layers))context.Layers[sim.Pointer]=layers=new();
        var transition=layers.ObserveBlock(solid,length);
        if(transition is not {} gap)return;
        var constant=sim.projectileProfiles[fragment.projectileProfileIndex].penetratorConstant;
        var remaining=fragment.GetBasePenetration(constant)*1000;
        if(!float.IsFinite(remaining)||remaining<=0)return;
        var retention=ShellBalance.SpacedRetention(profile,context.GunDiameter*1000,gap.PlateRhaMm,gap.GapMm);
        var budget=remaining*retention;
        var speed=PenetrationUtils.ComputeRequiredPenetrationSpeed(fragment.diameter*1000,fragment.mass,(float)budget,constant);
        if(!float.IsFinite(speed)||speed<=0)return;
        fragment.speed=Math.Min(fragment.speed,speed);
        sim.fragments[layerFragment]=fragment;
        Plugin.ModLog.LogInfo($"[Chemical layers] {context.Behavior} plate={gap.PlateRhaMm:0.0}mm RHA gap={gap.GapMm:0.0}mm retention={retention:0.000}: {remaining:0.0} -> {budget:0.0}mm");
    }
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
    private sealed record BurstState(int CountBefore,bool Custom,bool Aphe,UnityEngine.Vector3 ExplosionPosition=default,UnityEngine.Vector3? PreviousAxis=null,short ParentIndex=-1);
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.Penetrate))]
    [HarmonyPriority(Priority.Last)]
    private static void ExperimentalNormalization(ref PenetratorInfo penetrator)
    {
        var context=Impact;
        if(context is {LiveImpact:true,ChemicalInitialized:false} && context.Behavior is "heat" or "hesh")
        {
            var p=context.Profile!;
            var diameter=context.Behavior=="heat" ? context.GunDiameter*.05f : penetrator.Diameter;
            var mass=context.Behavior=="heat" ? penetrator.Mass*.05f : penetrator.Mass;
            var budget=context.ChemicalBudget;
            var speed=PenetrationUtils.ComputeRequiredPenetrationSpeed(diameter*1000,mass,(float)budget,penetrator.PenetratorConstant);
            if(!float.IsFinite(speed)||speed<=0)return;
            penetrator=new PenetratorInfo(mass,diameter,penetrator.Density,0,penetrator.PenetratorConstant,penetrator.Position,penetrator.Velocity.normalized*speed);
            context.ChemicalInitialized=true;
            Plugin.ModLog.LogInfo($"[Chemical] profile={p.Id} firingEra={context.FiringEra} equivalent penetrator {budget:0}mm RHA; impact-speed-independent proxy");
            return;
        }
        if(context?.Behavior is "heat" or "hesh")
        {
            penetrator=new PenetratorInfo(penetrator.Mass,penetrator.Diameter,penetrator.Density,0,penetrator.PenetratorConstant,penetrator.Position,penetrator.Velocity);
            return;
        }
        if(context?.Behavior!="apfsds" || !Settings.ApfsdsDisableClassicNormalization)return;
        penetrator=new PenetratorInfo(penetrator.Mass,penetrator.Diameter,penetrator.Density,0,
            penetrator.PenetratorConstant,penetrator.Position,penetrator.Velocity);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static bool Burst(PenetrationSimulation simulation,ref CompoundStructure.SpallSpawnBurst o,out BurstState __state)
    {
        __state=new(simulation.FragmentCount,false,false,PreviousAxis:apheAxis);
        try
        {
            var context=Impact;
            if(context==null || context.Behavior is not ("apfsds" or "aphe" or "heat" or "hesh")) return true;
            if(o.parentIndex<0 || o.parentIndex>=simulation.FragmentCount) return true;
            var parent=simulation.fragments[o.parentIndex];
            if(context.Behavior is "aphe" or "heat" or "hesh" && ShellBalance.SuppressPayloadBurst(context.Behavior,
                (parent.flags & FragmentFlag.OriginalPenetrator)!=0,context.PayloadBursts.Contains(simulation.Pointer)))return false;
            if((parent.flags & FragmentFlag.OriginalPenetrator)==0 || parent.materialIndex<0) return true;
            var s=Settings;
            var aphe=context.Behavior is "aphe" or "heat" or "hesh";
            if(aphe)
            {
                // Keep native AP origin, material occupancy and fragment profile.
                // The same native loop evaluates both the original projectile and its fragments.
                var calibre=context.GunDiameter*1000;
                if(!float.IsFinite(o.spallFactor)||o.spallFactor<=0)return true;
                var tuning=context.Behavior=="aphe" ? s.ApheSpallMultiplier/4 : context.Profile!.SpallMultiplier/(context.Behavior=="hesh"?8:1.5);
                o.volume=(float)(ShellBalance.FragmentVolume(context.Behavior,calibre)*tuning);
                var count=Math.Clamp(ShellBalance.FragmentTarget(context.Behavior,calibre)*tuning,4,32);
                o.crossectionalArea=(float)(o.spallFactor*count);
                o.speed=(float)ShellBalance.PayloadFragmentSpeed(context.Behavior,calibre,o.speed);
                Plugin.ModLog.LogInfo($"[Payload burst] {context.Behavior} targetCount={count:0} fragmentSpeed={o.speed:0}m/s volume={o.volume:0.000000}m3");
                apheAxis=context.Behavior=="heat" ? o.direction.normalized : o.planeNormal.sqrMagnitude>.5f?o.planeNormal.normalized:o.direction.normalized;
                __state=__state with {Custom=true,Aphe=true,ParentIndex=o.parentIndex,
                    ExplosionPosition=context.LiveImpact?simulation.SimToWorldSpacePoint(o.spawnPoint):default};
            }
            else
            {
                var original=simulation.Penetrator;
                var initialPen=PenetrationUtils.ComputePenetration(original.Diameter*1000,original.Mass,original.Velocity.magnitude,original.PenetratorConstant);
                var remainingPen=parent.GetBasePenetration(simulation.projectileProfiles[parent.projectileProfileIndex].penetratorConstant)*1000;
                if(!float.IsFinite(initialPen) || initialPen<=0 || !float.IsFinite(remainingPen)) return true;
                var remainingFraction=Math.Clamp(remainingPen/initialPen,0,1);
                var ratio=SpallBalance.RemainingPenetrationRatio(remainingFraction,s);
                o.volume=(float)(SpallBalance.ReferenceApVolume(context.GunDiameter,context.GunDiameter)*ratio);
                o.crossectionalArea=(float)(Math.PI*context.GunDiameter*context.GunDiameter*.25*ratio);
                var energyFraction=original.Mass>0 && original.Velocity.sqrMagnitude>0
                    ? parent.mass*parent.speed*parent.speed/(original.Mass*original.Velocity.sqrMagnitude):0;
                var cone=SpallBalance.ConeFactor(energyFraction,s);
                o.spread*=(float)(cone*ShellBalance.RodCone(context.Profile!.Settings));
                __state=__state with {Custom=true};
                Plugin.ModLog.LogInfo($"[Spall] APFSDS remaining={remainingPen:0.0}/{initialPen:0.0}mm fraction={remainingFraction:0.000} massAndCountRatioToAP={ratio:0.000} coneFactor={cone:0.000}");
            }
        }
        catch(Exception ex){Plugin.ModLog.LogError("[Spall] Burst tuning failed: "+ex);}
        return true;
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
        var angle=Impact?.Behavior=="aphe" ? Settings.ApheConeHalfAngleDegrees : Impact?.Profile?.ConeHalfAngleDegrees ?? Settings.ApheConeHalfAngleDegrees;
        var sample=SpallBalance.Cone(System.Random.Shared.NextDouble(),System.Random.Shared.NextDouble(),angle);
        __result=axis*sample.Z+tangent*sample.X+bitangent*sample.Y;
        return false;
    }
    [HarmonyPostfix,HarmonyPatch(typeof(CompoundStructure),nameof(CompoundStructure.CreateSpallBurst))]
    private static void BurstDone(PenetrationSimulation simulation,BurstState __state)
    {
        if(!__state.Custom)return;
        var count=simulation.FragmentCount-__state.CountBefore;
        if(count>0 && __state.Aphe && Impact is {} impact) impact.PayloadBursts.Add(simulation.Pointer);
        if(count>0 && Impact?.Behavior is "aphe" && __state.ParentIndex>=0)
        {
            var parent=simulation.fragments[__state.ParentIndex];
            parent.flags |= FragmentFlag.Killed;
            simulation.fragments[__state.ParentIndex]=parent;
        }
        Plugin.ModLog.LogInfo($"[Spall] {Impact?.Behavior} spawned={count}; calibre={(Impact?.GunDiameter??0)*1000:0}mm");
        if(__state.Aphe && count>0 && Impact is {LiveImpact:true,ExplosionPending:false} context)
        {
            context.ExplosionPosition=__state.ExplosionPosition;
            context.ExplosionPending=true;
        }
    }
}
