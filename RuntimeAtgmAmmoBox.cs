using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Runtime.Serialization;
using Sprocket.DamageModelling;
using Sprocket.UI;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.CrewSystems;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
using UnityEngine;
namespace SprocketShellSelector;

[HarmonyPatch]
internal static class RuntimeAtgmAmmoBox
{
    internal static bool Ready;
    private sealed class BoxState
    {
        internal readonly AmmoRack Rack;
        internal bool Enabled=true,Syncing;
        internal int Reference=-1;
        internal string Profile="";
        internal float AddedMass,AddedCost;
        internal BoxState(AmmoRack rack)=>Rack=rack;
    }
    private sealed record Binding(Cannon Cannon,LoadTask Task,LoadContributeTask Contributor);
    private static readonly Dictionary<IntPtr,BoxState> boxes=new();
    private static readonly Dictionary<IntPtr,Binding> loaders=new();
    private static readonly Dictionary<IntPtr,string> reports=new();
    private static readonly Dictionary<IntPtr,bool> feedStates=new();
    private static readonly Dictionary<IntPtr,string> referenceReports=new();
    private static readonly HashSet<string> warnings=new();
    private static int updateFrame=-1;
    private const string EnabledKey="sprocketAtgmAmmoBoxEnabledV1",ReferenceKey="sprocketAtgmAmmoBoxReferenceV1",ProfileKey="sprocketAtgmAmmoBoxProfileV1";
    internal static bool Own(AmmoRack? rack)=>rack?.VehicleObject?.GUID==AtgmAmmoBoxRules.Guid;
    private static BoxState State(AmmoRack rack)
    {if(!boxes.TryGetValue(rack.Pointer,out var state))boxes[rack.Pointer]=state=new(rack);return state;}
    private static void Guard(string area,Action action)
    {try{action();}catch(Exception ex){if(warnings.Add(area))Plugin.ModLog.LogWarning("[ATGM ammo box] "+area+": "+ex.Message);}}
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list)=>list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
    private static List<Cannon> Cannons(AmmoRack rack,bool dedicatedOnly=true)
    {
        var result=new List<Cannon>();var items=rack.Vehicle.ObjectReader.Items;
        for(int i=0;i<Count(items);i++)
        {
            var parts=items[i].Components;
            for(int j=0;j<Count(parts);j++)if(parts[j].TryCast<Cannon>() is {} c&&(!dedicatedOnly||RuntimeAtgmLauncher.IsLauncher(c)))result.Add(c);
        }
        return result;
    }
    private static double Distance(AmmoRack rack,Cannon cannon)=>Vector3.Distance(rack.transform.TransformPoint(rack.LoadLocalPosition),cannon.LoadPosition);
    private static void Sync(BoxState box)
    {
        if(box.Syncing||!box.Enabled||box.Rack.behaviour!=null)return; // Never reconfigure a live finite ledger.
        box.Syncing=true;
        try
        {
            var candidates=Cannons(box.Rack).Where(c=>AtgmLauncherModel.Allows(RuntimeShellSelection.CannonProfile(c)));
            var cannon=box.Reference<0?candidates.Where(c=>Distance(box.Rack,c)<=AtgmAmmoBoxRules.Reach).OrderBy(c=>Distance(box.Rack,c)).ThenBy(c=>c.VUID.Value).FirstOrDefault():candidates.FirstOrDefault(c=>c.VUID.Value==box.Reference);
            if(cannon==null)return;
            var shell=cannon.ShellBlueprint;
            if(shell?.GeneratedProjectileBlueprint==null||Count(shell.GeneratedProjectileBlueprint)==0)return;
            box.Rack.Blueprint.ShellSlotBlueprintID=cannon.ShellSizeBlueprintID;
            box.Rack.Blueprint.ShellTypeGuid=shell.GeneratedProjectileBlueprint[0].ProjectileTypeGuid;
            if(box.Rack.ShellSizeBlueprintID!=cannon.ShellSizeBlueprintID)box.Rack.shellSlotBlueprintSlot.Load(cannon.ShellSizeBlueprintID);
            box.Rack.SyncWithShellSlotBlueprint(box.Rack.Blueprint,shell);
            box.Profile=RuntimeShellSelection.CannonProfile(cannon)!.Id;
        }
        finally{box.Syncing=false;}
    }
    private static bool Eligible(BoxState box,Cannon cannon)
    {
        var rack=box.Rack;
        return Ready&&box.Enabled&&rack.IsInstalled&&rack.HealthFraction>0&&rack.Capacity>0&&cannon.IsInstalled&&
            RuntimeAtgmLauncher.IsLauncher(cannon)&&AtgmLauncherModel.Allows(RuntimeShellSelection.CannonProfile(cannon))&&
            AtgmAmmoBoxRules.Matches(rack.Vehicle.Pointer==cannon.Vehicle.Pointer,box.Profile,RuntimeShellSelection.CannonProfile(cannon)?.Id,
                rack.projectileSizeID.Caliber,cannon.Blueprint.Caliber,rack.projectileSizeID.PropellantLength,cannon.Blueprint.PropellantLength,Distance(rack,cannon));
    }
    private static IEnumerable<BoxState> Nearby(Cannon cannon)=>boxes.Values.Where(b=>Eligible(b,cannon)).OrderBy(b=>Distance(b.Rack,cannon)).ThenBy(b=>b.Rack.VUID.Value);
    private static bool HasDesign(Cannon cannon)=>Nearby(cannon).Any();
    private static BoxState? Source(LoadTask task,Cannon cannon)=>task.rack==null?null:Nearby(cannon).FirstOrDefault(b=>b.Rack.behaviour?.Pointer==task.rack.Pointer);
    private static bool Active(Binding binding)=>Source(binding.Task,binding.Cannon) is {} box&&
        (binding.Task.State==LoadState.Loading||binding.Task.State==LoadState.Loaded&&binding.Task.currentRequest==LoadRequest.Unused||box.Rack.behaviour?.AmountStored>0);
    private static void Timing(LoadTask task,Cannon cannon)
    {
        float seconds=(float)AtgmAmmoBoxRules.Seconds(cannon.Blueprint.Caliber,cannon.Blueprint.PropellantLength);
        task.pickupTime=seconds*.2f;task.totalDistance=seconds*.2f;task.dropoffTime=seconds*.4f;
    }
    private static bool Choose(LoadTask task,Cannon cannon)
    {
        if(RuntimeAtgmInitialRound.IsInitializing(task)||RuntimeAtgmInitialRound.Holds(task))return false;
        if(task.State==LoadState.Loading||task.State==LoadState.Loaded&&task.currentRequest==LoadRequest.Unused)return true;
        foreach(var box in Nearby(cannon))
        {
            var supply=box.Rack.behaviour;
            if(supply==null||supply.AmountStored==0||supply.StoredTypeID.Value==ProjectileTypeID.Invalid.Value)continue;
            var request=task.Request;
            if(request.TypeID.Value==ProjectileTypeID.Invalid.Value||request.SizeID.Caliber==0)
            {request.TypeID=supply.StoredTypeID;request.SizeID=supply.ShellSizeID;request.Destination=cannon.LoadPosition;}
            request.Amount=1;
            if(!request.IsMatch(supply.Cast<IAmmoSource>()))continue;
            task.Request=request;
            if(task.State==LoadState.NeverLoaded)task.currentRequest=LoadRequest.Unused;
            task.rack=supply.Cast<IAmmoSource>();task.currentLoadInfo=box.Rack.LoadInfo;Timing(task,cannon);
            var report=$"box={box.Rack.VUID.Value} nativeStock={supply.AmountStored}/{supply.Capacity} profile={box.Profile}";
            if(!reports.TryGetValue(cannon.Pointer,out var previous)||previous!=report)
            {reports[cannon.Pointer]=report;Plugin.ModLog.LogInfo($"[ATGM ammo box] FEED cannon={cannon.VUID.Value} {report}; native shared stock only.");}
            return true;
        }
        // Native lookup/manual fallback is retained. Only our contributor is gated.
        // No cancel/refund/reset of an in-progress native round.
        task.rack=null;return false;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.UpdateRequest)),HarmonyPriority(Priority.Last)]
    private static void SelectSource(LoadTask __instance)
    {
        var cannon=__instance.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
        if(cannon==null||!RuntimeAtgmLauncher.IsLauncher(cannon)||!loaders.ContainsKey(cannon.Pointer))return;
        Guard("Select source",()=>Choose(__instance,cannon));
    }
    [HarmonyPrefix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.Update))]
    private static void Speed(LoadTask __instance,ref float __0,ref float __1,ref float __2)
    {
        try
        {
        var cannon=__instance.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
        if(cannon==null||!loaders.TryGetValue(cannon.Pointer,out var binding))return;
        if(__instance.State==LoadState.NeverLoaded)Guard("Bootstrap supply",()=>Choose(__instance,cannon));
        if(!Active(binding))return;
        __0=1;__1=1;__2=(float)AtgmAmmoBoxRules.Seconds(cannon.Blueprint.Caliber,cannon.Blueprint.PropellantLength)*.2f;
        }
        catch(Exception ex){Guard("Native cycle speed",()=>throw ex);}
    }
    [HarmonyPostfix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.UpdateRequest))]
    private static void Timings(LoadTask __instance)
    {var c=__instance.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();if(c!=null&&loaders.TryGetValue(c.Pointer,out var b)&&Active(b))Timing(__instance,c);}
    [HarmonyPostfix,HarmonyPatch(typeof(Cannon),nameof(Cannon.EnableBehaviour))]
    private static void Attach(Cannon __instance)
    {
        Guard("Native automatic contributor",()=>
        {
            if(!Ready||!RuntimeAtgmLauncher.IsLauncher(__instance)||!HasDesign(__instance))return;
            var weapon=__instance.Behaviour;var task=weapon?.LoadTask?.TryCast<LoadTask>();if(task==null||weapon==null)return;
            var contributor=loaders.TryGetValue(__instance.Pointer,out var binding)&&binding.Task.Pointer==task.Pointer?binding.Contributor:new LoadContributeTask{efficiency=1,commandEfficiency=1,operateCount=1};
            var old=weapon.LoadTaskContributors;
            if(old==null||!old.Any(c=>c.Pointer==contributor.Pointer))
            {
                var array=new Il2CppReferenceArray<ILoadTaskContributor>((old?.Length??0)+1);
                if(old!=null)for(int i=0;i<old.Length;i++)array[i]=old[i];
                array[array.Length-1]=contributor.Cast<ILoadTaskContributor>();weapon.LoadTaskContributors=array;
            }
            loaders[__instance.Pointer]=new(__instance,task,contributor);Choose(task,__instance);
        });
    }
    [HarmonyPrefix,HarmonyPatch(typeof(global::VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage),nameof(global::VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage.Execute))]
    private static void BeforeController(global::VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage __instance)
    {if(Ready&&boxes.Count>0)Guard("Controller assembly",()=>{foreach(var source in __instance.sources)if(source.TryCast<Cannon>() is {} cannon)Attach(cannon);});}
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleWeaponLoadController),nameof(VehicleWeaponLoadController.Update))]
    private static void Refresh()
    {
        if(!Ready||loaders.Count==0||updateFrame==Time.frameCount)return;updateFrame=Time.frameCount;
        foreach(var binding in loaders.Values.ToArray())Guard("Contributor eligibility",()=>
        {
            if(binding.Task.State!=LoadState.Loading&&!(binding.Task.State==LoadState.Loaded&&binding.Task.currentRequest==LoadRequest.Unused))Choose(binding.Task,binding.Cannon);
            var active=Active(binding);binding.Contributor.efficiency=active?1:0;binding.Contributor.commandEfficiency=active?1:0;binding.Contributor.operateCount=(byte)(active?1:0);
            if(!feedStates.TryGetValue(binding.Cannon.Pointer,out var previous)||previous!=active)
            {feedStates[binding.Cannon.Pointer]=active;Plugin.ModLog.LogInfo($"[ATGM ammo box] {(active?"ACTIVE":"INACTIVE")} cannon={binding.Cannon.VUID.Value} state={binding.Task.State}; automatic contributor eligibility revalidated; native/manual loading retained.");}
        });
    }
    [HarmonyPrefix,HarmonyPatch(typeof(WeaponBehaviour),nameof(WeaponBehaviour.LoaderEnableFraction),MethodType.Getter)]
    private static bool LoaderFraction(WeaponBehaviour __instance,ref float __result)
    {
        try
        {
        var c=__instance.mount?.TryCast<Cannon>();if(c==null||!loaders.TryGetValue(c.Pointer,out var binding))return true;
        if(Active(binding)){__result=1;return false;}
        var manual=__instance.LoadTaskContributors?.Where(x=>x.Pointer!=binding.Contributor.Pointer).ToArray();
        __result=manual==null||manual.Length==0?0:manual.Average(x=>x.EnableFraction);return false;
        }
        catch(Exception ex){Guard("Loader availability",()=>throw ex);return true;}
    }
    [HarmonyPrefix,HarmonyPatch(typeof(AllOperablesValidCriterion),nameof(AllOperablesValidCriterion.Validate))]
    private static void Validate(AllOperablesValidCriterion __instance,out Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>? __state)
    {
        __state=null;if(!Ready||boxes.Count==0)return;
        try
        {
        var original=__instance.operables;var kept=new Il2CppSystem.Collections.Generic.List<VehicleOperable>();bool changed=false;
        foreach(var op in Il2CppSystem.Linq.Enumerable.ToArray<VehicleOperable>(original))
        {
            var breech=op.Component?.TryCast<CannonBreech>();
            if(breech!=null&&breech.loaderOperable?.Pointer==op.Pointer&&boxes.Values.Any(b=>b.Rack.Vehicle.Pointer==breech.Vehicle.Pointer&&Cannons(b.Rack).Any(c=>AtgmAmmoBoxRules.WaivesLoader(c.Vehicle.Pointer==breech.Vehicle.Pointer,RuntimeAtgmLauncher.IsLauncher(c),c.VUID.Value,breech.parentCannonVuid.Value,HasDesign(c)))))changed=true;
            else kept.Add(op);
        }
        if(changed){__state=original;__instance.operables=kept.Cast<Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>>();}
        }
        catch(Exception ex){Guard("Loader validation",()=>throw ex);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(AllOperablesValidCriterion),nameof(AllOperablesValidCriterion.Validate))]
    private static void Restore(AllOperablesValidCriterion __instance,Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>? __state)
    {if(__state!=null)__instance.operables=__state;}
    [HarmonyPrefix,HarmonyPatch(typeof(AmmoRack),nameof(AmmoRack.Build))]
    private static void BeforeRack(AmmoRack __instance)
    {
        if(!Own(__instance))return;Guard("Box design",()=>
        {
            var b=State(__instance);Sync(b);
            __instance.SetMass(Math.Max(0,__instance.GetMass(MassType.Mechanisms)-b.AddedMass),MassType.Mechanisms);
            __instance.SetCost(Math.Max(0,__instance.GetCost(MassType.Mechanisms,CostType.Assembly)-b.AddedCost),MassType.Mechanisms,CostType.Assembly);
            b.AddedMass=b.AddedCost=0;
        });
    }
    [HarmonyPostfix,HarmonyPatch(typeof(AmmoRack),nameof(AmmoRack.Build))]
    private static void AfterRack(AmmoRack __instance)
    {
        if(!Own(__instance))return;Guard("Box mass/model",()=>
        {
            var b=State(__instance);b.AddedMass=(float)AtgmAmmoBoxRules.MechanismMass(__instance.Capacity);b.AddedCost=(float)AtgmAmmoBoxRules.MechanismCost(__instance.Capacity);
            __instance.SetMass(__instance.GetMass(MassType.Mechanisms)+b.AddedMass,MassType.Mechanisms);
            __instance.SetCost(__instance.GetCost(MassType.Mechanisms,CostType.Assembly)+b.AddedCost,MassType.Mechanisms,CostType.Assembly);
            __instance.cachedMass=__instance.GetMass(MassType.Everything);__instance.RecalculateCenterOfMass();
        });
    }
    [HarmonyPostfix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Build))]
    private static void CannonChanged(Cannon __instance)
    {if(RuntimeAtgmLauncher.IsLauncher(__instance))foreach(var b in boxes.Values.Where(b=>b.Rack.Vehicle.Pointer==__instance.Vehicle.Pointer).ToArray())Guard("Sync box ammunition",()=>{var old=b.Profile;var id=b.Rack.ShellSizeBlueprintID;Sync(b);if(old!=b.Profile||id!=b.Rack.ShellSizeBlueprintID)b.Rack.RequestRebuild();});}
    [HarmonyPostfix,HarmonyPatch(typeof(AmmoRackEditor),nameof(AmmoRackEditor.OnGUI))]
    private static void Draw(AmmoRackEditor __instance,IGUILayout __0)
    {
        var rack=__instance.Component;if(!Own(rack))return;
        Guard("Box inspector",()=>
        {
            var state=State(rack);var ui=__0.TryCast<IGUIElementDrawer>();if(ui==null)return;
            __0.EndAllDropdowns();__0.BeginDropdown("ATGM automatic ammo box",true,Ui.BoolCallback(_=>{}));
            try
            {
                void Changed(){rack.RequestRebuild();__instance.RequestRedraw();}
                ui.ToggleField("Automatic feed",state.Enabled,Ui.BoolCallback(v=>{state.Enabled=v;Changed();}),"Finite native stock, no crew loader. Same vehicle, matching profile and ammunition, within 2 m of native loading points.");
                var cannons=Cannons(rack);var labels=new Il2CppSystem.Collections.Generic.List<string>();labels.Add("Auto: nearest launcher within 2 m");
                foreach(var c in cannons)
                {var profile=RuntimeShellSelection.CannonProfile(c);labels.Add(AtgmAmmoBoxRules.ReferenceLabel(c.Blueprint.Name,c.Blueprint.Caliber,c.VUID.Value,profile?.Label,profile?.Atgm?.GuidanceMode));}
                ui.Dropdown("Ammunition reference",labels.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(),cannons.FindIndex(c=>c.VUID.Value==state.Reference)+1,Ui.IntCallback(i=>{if(i<0||i>cannons.Count)return;state.Reference=i==0?-1:cannons[i-1].VUID.Value;Sync(state);Changed();}),"Reference chooses calibre/propellant/profile for this box. All nearby matching dedicated launchers share the same finite stock.");
                var allCannons=Cannons(rack,false);
                var referenceReport=string.Join("; ",allCannons.Select(c=>$"id={c.VUID.Value} part={c.VehicleObject.GUID} profile={RuntimeShellSelection.CannonProfile(c)?.Id??"unavailable"} guidance={RuntimeShellSelection.CannonProfile(c)?.Atgm?.GuidanceMode??"none"} listed={RuntimeAtgmLauncher.IsLauncher(c)}"));
                if(!referenceReports.TryGetValue(rack.Pointer,out var previous)||previous!=referenceReport)
                {referenceReports[rack.Pointer]=referenceReport;Plugin.ModLog.LogInfo($"[ATGM ammo box] REFERENCES box={rack.VUID.Value} selected={state.Reference}: {referenceReport}");}
                foreach(var c in allCannons.Where(c=>!RuntimeAtgmLauncher.IsLauncher(c)&&ShellBalance.IsAtgm(RuntimeShellSelection.CannonProfile(c)?.Behavior)))
                    ui.InfoField($"Cannon {c.VUID.Value} ({RuntimeShellSelection.CannonProfile(c)?.Label}) is not a dedicated ATGM launcher part, so it is not listed as an automatic-box reference.",2);
                ui.InfoField($"Profile: {(state.Profile==""?"place a matching launcher nearby":state.Profile)} | capacity {rack.Capacity} | stock {(rack.behaviour==null?"native design fill":rack.behaviour.AmountStored.ToString())}",2);
                foreach(var c in cannons.Where(c=>Distance(rack,c)<=AtgmAmmoBoxRules.Reach))ui.InfoField($"Launcher {c.VUID.Value}: {Distance(rack,c):0.00} m | {AtgmAmmoBoxRules.Seconds(c.Blueprint.Caliber,c.Blueprint.PropellantLength):0.0} s cycle | {(Eligible(state,c)?"compatible":"profile/ammunition/installation mismatch")}",2);
                ui.InfoField($"Mechanism: {state.AddedMass:0} kg / {state.AddedCost:0} assembly cost. Native rack capacity, fill, ammunition mass and damage remain in use. No automatic replenishment from ordinary racks.",2);
            }
            finally{__0.EndAllDropdowns();}
        });
    }
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleObjectSerialization),nameof(VehicleObjectSerialization.ToBlueprint))]
    private static void Saved(VehicleObject __0,Sprocket.Vehicles.Serialization.VehicleObjectBlueprint __result)
    {var parts=__0.Components;for(int i=0;i<Count(parts);i++)if(parts[i].TryCast<AmmoRack>() is {} r&&Own(r)){var b=State(r);__result.State.AddValue(EnabledKey,b.Enabled);__result.State.AddValue(ReferenceKey,b.Reference);__result.State.AddValue(ProfileKey,b.Profile);}}
    [HarmonyPostfix,HarmonyPatch(typeof(VehicleComponent),nameof(VehicleComponent.LoadData))]
    private static void Loaded(VehicleComponent __instance,SerializationInfo __0)
    {
        var r=__instance.TryCast<AmmoRack>();if(!Own(r))return;var b=State(r!);var keys=new HashSet<string>();var e=__0.GetEnumerator();while(e.MoveNext())keys.Add(e.Name);
        if(keys.Contains(EnabledKey))b.Enabled=__0.GetBoolean(EnabledKey);if(keys.Contains(ReferenceKey))b.Reference=__0.GetInt32(ReferenceKey);if(keys.Contains(ProfileKey))b.Profile=__0.GetString(ProfileKey);
    }
    [HarmonyPrefix,HarmonyPatch(typeof(Cannon),nameof(Cannon.DisableBehaviour))]
    private static void Disabled(Cannon __instance){loaders.Remove(__instance.Pointer);reports.Remove(__instance.Pointer);feedStates.Remove(__instance.Pointer);}
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleComponent),nameof(VehicleComponent.ReleaseInternal))]
    private static void Released(VehicleComponent __instance){boxes.Remove(__instance.Pointer);loaders.Remove(__instance.Pointer);reports.Remove(__instance.Pointer);feedStates.Remove(__instance.Pointer);referenceReports.Remove(__instance.Pointer);}
}

