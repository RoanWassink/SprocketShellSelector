using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AmmunitionStorage;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.CrewSystems;
using Sprocket.Vehicles.Weapons;
using Sprocket.WeaponFramework;
namespace SprocketShellSelector;

[HarmonyPatch]
internal static class RuntimeAtgmInitialRound
{
    internal static bool Ready;
    private sealed record Round(IVehicleBehaviour Instance,Cannon Cannon,LoadTask Task,AmmoRackBehaviour Supply,string Profile);
    private static readonly AtgmInitialRoundLedger ledger=new();
    private static readonly Dictionary<IntPtr,Round> rounds=new();
    private static readonly HashSet<IntPtr> initializing=new();
    private static readonly HashSet<IntPtr> initialAssemblyTasks=new();
    private static readonly HashSet<(IntPtr Instance,int Vuid)> assemblyEntries=new();
    private static readonly Dictionary<(IntPtr Instance,int Vuid),(IProjectileTypeRegister Register,Il2CppSystem.Guid Guid)> registrations=new();
    private static int diagnosticCount;
    private static void Report(string text){if(diagnosticCount++<32)Plugin.ModLog.LogInfo("[ATGM initial] "+text);}
    private static readonly Dictionary<IntPtr,(float Mass,float Material,float Assembly)> addedMass=new();
    private static readonly HashSet<string> warnings=new();
    private static void Guard(string area,Action action)
    {try{action();}catch(Exception ex){if(warnings.Add(area+ex.Message))Plugin.ModLog.LogWarning("[ATGM initial] "+area+": "+ex.Message);}}
    internal static bool IsInitializing(LoadTask task)=>Ready&&initializing.Contains(task.Pointer);
    internal static bool Holds(LoadTask task)=>Ready&&rounds.TryGetValue(task.Pointer,out var r)&&
        task.rack?.Pointer==r.Supply.Pointer&&task.State==LoadState.Loaded&&task.currentRequest!=LoadRequest.Consumed;

    [HarmonyPrefix,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.LoadInstantly)),HarmonyPriority(Priority.First)]
    private static void Prepare(LoadTask __instance)
    {
        if(!Ready||initializing.Contains(__instance.Pointer)||!initialAssemblyTasks.Contains(__instance.Pointer))return;
        Guard("Prepare",()=>
        {
            var cannon=__instance.target?.TryCast<WeaponBehaviour>()?.mount?.TryCast<Cannon>();
            if(cannon==null||!RuntimeAtgmLauncher.IsLauncher(cannon)||!RuntimeAtgmLauncher.CanLaunch(cannon))return;
            var instance=cannon.Vehicle.Behaviour;var shell=cannon.ShellBlueprint;
            if(instance==null||shell?.GeneratedProjectileBlueprint==null||shell.GeneratedProjectileBlueprint.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<ShellBlueprint>>().Count==0){Report($"SKIP cannon={cannon.VUID.Value} reason=instance-or-native-blueprint instance={instance?.Pointer} state={__instance.State}");return;}
            var register=IProjectileTypeRegister.Instance;
            if(register==null){Report($"SKIP cannon={cannon.VUID.Value} reason=native-register-unavailable state={__instance.State}");return;}
            // Claim before creating supply: failure never grants another round in this instance.
            if(!ledger.Claim(instance.Pointer.ToInt64(),cannon.VUID.Value,true,true,true))return;
            // Same native AP-base protocol as the accepted rack-independent carousel.
            // Generated GUID/functions belong to this exact cannon blueprint; Shell Selector
            // still replaces the projectile at firing. Never manufacture a numeric type ID.
            var projectile=shell.GeneratedProjectileBlueprint[0];var guid=projectile.ProjectileTypeGuid;
            if(!register.IsRegistered(guid))register.Create($"{shell.Diameter} mm ATGM initial chamber",guid,
                MathF.Pow(shell.Diameter,3)*1.58999992e-5f,shell.Diameter*.001f,shell.Diameter*.003f,1f,10f,projectile.FunctionDefinitions);
            if(!register.IsRegistered(guid)||!register.TryGet(guid,out var native)||native==null||native.ID.Value==ProjectileTypeID.Invalid.Value)
            {Report($"SKIP cannon={cannon.VUID.Value} reason=native-registration-readback state={__instance.State}; initial claim retained, no retry");return;}
            register.AddUser(guid);
            registrations[(instance.Pointer,cannon.VUID.Value)]=(register,guid);
            var type=register.GetID(guid);
            if(type.Value!=native.ID.Value)throw new InvalidOperationException("Native registration ID readback mismatch.");
            var size=ProjectileSizeID.FromPropellantLength(shell.Diameter,shell.PropellantLength);
            var supply=new AmmoRackBehaviour("ATGM initial chamber "+cannon.VUID.Value,native.ID,size,1);
            supply.LoadAmmo(1);
            if(supply.AmountStored!=1||supply.Capacity!=1)throw new InvalidOperationException("Native one-round source rejected initial fill.");
            var request=__instance.Request;request.TypeID=native.ID;request.SizeID=size;request.Amount=1;request.Destination=cannon.LoadPosition;
            __instance.Request=request;__instance.rack=supply.Cast<IAmmoSource>();
            __instance.currentLoadInfo=new ShellLoadInfo(shell.ProjectileMass,shell.PropellantMass,shell.Length*.001f);
            rounds[__instance.Pointer]=new(instance,cannon,__instance,supply,RuntimeShellSelection.CannonProfile(cannon)!.Id);
            initializing.Add(__instance.Pointer);
        });
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(LoadTask),nameof(LoadTask.LoadInstantly))]
    private static void Loaded(LoadTask __instance,Exception? __exception)
    {
        if(!initializing.Remove(__instance.Pointer)||!rounds.TryGetValue(__instance.Pointer,out var r))return;
        Guard("Readback",()=>{
            if(__exception==null&&(__instance.State!=LoadState.Loaded||r.Supply.AmountStored!=0))Plugin.ModLog.LogWarning("[ATGM initial] Native initial transfer incomplete; no retry or readiness bypass. Inspect CHAMBER readback.");
            Plugin.ModLog.LogInfo($"[ATGM initial] CHAMBER cannon={r.Cannon.VUID.Value} instance={r.Instance.Pointer} profile={r.Profile} state={__instance.State} loadedType={r.Cannon.Behaviour?.LoadedProjectileTypeID.Value} source={r.Supply.AmountStored}/{r.Supply.Capacity} expected=Loaded/source0 nativeException={__exception?.Message ?? "none"}; one initial plus finite reserve; gunner/native CanFire retained." );});
    }
    [HarmonyPrefix,HarmonyPatch(typeof(global::VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage),nameof(global::VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage.Execute)),HarmonyPriority(Priority.First)]
    private static void InitialAssembly(global::VehicleDesigner.AmmunitionStorage.WeaponLoadControllerAssemblyStage __instance)
    {
        if(!Ready)return;
        Guard("Native controller assembly",()=>
        {
            Report($"ASSEMBLY sources={__instance.sources?.Length ?? 0}");
            if(__instance.sources==null)return;
            foreach(var source in __instance.sources)
            {
                // Resolve the native weapon's real mount, not the source wrapper's type.
                var c=source.Weapon?.mount?.TryCast<Cannon>()??source.TryCast<Cannon>();
                if(c==null||!RuntimeAtgmLauncher.IsLauncher(c))continue;
                var weapon=c.Behaviour;var task=weapon?.LoadTask?.TryCast<LoadTask>();var instance=c.Vehicle.Behaviour;
                var allowed=RuntimeAtgmLauncher.CanLaunch(c);
                var first=instance!=null&&assemblyEntries.Add((instance.Pointer,c.VUID.Value));
                Report($"CANDIDATE cannon={c.VUID.Value} sourceType={source.Cast<Il2CppSystem.Object>().GetType().FullName} profile={RuntimeShellSelection.CannonProfile(c)?.Id ?? "unavailable"} allowed={allowed} instance={instance?.Pointer} task={task?.Pointer} state={task?.State} loadedType={weapon?.LoadedProjectileTypeID.Value} lastFire={weapon?.LastFireTime} firstAssembly={first}");
                if(!allowed||task==null||weapon==null||!AtgmInitialAssemblyRules.EmptyFirstAssembly(first,(int)task.State,weapon.LoadedProjectileTypeID.Value,weapon.LastFireTime)){Report($"SKIP cannon={c.VUID.Value} reason=first-empty-native-assembly-eligibility");continue;}
                initialAssemblyTasks.Add(task.Pointer);
                try{task.LoadInstantly();}finally{initialAssemblyTasks.Remove(task.Pointer);}
            }
        });
    }
    [HarmonyPrefix,HarmonyPatch(typeof(VehicleBehaviour),nameof(VehicleBehaviour.Release))]
    private static void End(VehicleBehaviour __instance)
    {
        ledger.End(__instance.Pointer.ToInt64());
        foreach(var key in registrations.Keys.Where(k=>k.Instance==__instance.Pointer).ToArray())
        {var r=registrations[key];registrations.Remove(key);Guard("Release registration",()=>r.Register.RemoveUser(r.Guid));}
        assemblyEntries.RemoveWhere(k=>k.Instance==__instance.Pointer);
        foreach(var r in rounds.Values.Where(r=>r.Instance.Pointer==__instance.Pointer).ToArray()){initializing.Remove(r.Task.Pointer);rounds.Remove(r.Task.Pointer);}
    }
    private static int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list)=>list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
    private static bool OwnBreech(CannonBreech breech)
    {
        var items=breech.Vehicle.ObjectReader.Items;
        for(int i=0;i<Count(items);i++){var parts=items[i].Components;for(int j=0;j<Count(parts);j++)if(parts[j].TryCast<Cannon>() is {} c&&c.Vehicle.Pointer==breech.Vehicle.Pointer&&c.VUID.Value==breech.parentCannonVuid.Value&&RuntimeAtgmLauncher.IsLauncher(c)&&RuntimeAtgmLauncher.CanLaunch(c))return true;}
        return false;
    }
    [HarmonyPrefix,HarmonyPatch(typeof(AllOperablesValidCriterion),nameof(AllOperablesValidCriterion.Validate)),HarmonyPriority(Priority.First)]
    private static void Validate(AllOperablesValidCriterion __instance,out Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>? __state)
    {
        __state=null;if(!Ready)return;
        try
        {
            var original=__instance.operables;var kept=new Il2CppSystem.Collections.Generic.List<VehicleOperable>();bool changed=false;
            foreach(var op in Il2CppSystem.Linq.Enumerable.ToArray<VehicleOperable>(original))
            {var breech=op.Component?.TryCast<CannonBreech>();if(breech!=null&&breech.loaderOperable?.Pointer==op.Pointer&&OwnBreech(breech))changed=true;else kept.Add(op);}
            if(changed){__state=original;__instance.operables=kept.Cast<Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>>();}
        }
        catch(Exception ex){if(warnings.Add("Validation"+ex.Message))Plugin.ModLog.LogWarning("[ATGM initial] Loader validation: "+ex.Message);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(AllOperablesValidCriterion),nameof(AllOperablesValidCriterion.Validate))]
    private static void Restore(AllOperablesValidCriterion __instance,Il2CppSystem.Collections.Generic.IEnumerable<VehicleOperable>? __state)
    {if(__state!=null)__instance.operables=__state;}

    [HarmonyPrefix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Build))]
    private static void BeforeBuild(Cannon __instance)
    {if(RuntimeAtgmLauncher.IsLauncher(__instance))Guard("Remove previous ammunition resource",()=>{if(addedMass.Remove(__instance.Pointer,out var r))
        {__instance.SetMass(Math.Max(0,__instance.GetMass(MassType.Ammunition)-r.Mass),MassType.Ammunition);
         __instance.SetCost(Math.Max(0,__instance.GetCost(MassType.Ammunition,CostType.Material)-r.Material),MassType.Ammunition,CostType.Material);
         __instance.SetCost(Math.Max(0,__instance.GetCost(MassType.Ammunition,CostType.Assembly)-r.Assembly),MassType.Ammunition,CostType.Assembly);}});}
    [HarmonyPostfix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Build))]
    private static void AfterBuild(Cannon __instance)
    {
        if(!Ready||!RuntimeAtgmLauncher.IsLauncher(__instance))return;
        Guard("Initial round resource",()=>
        {
            if(!AtgmLauncherModel.Allows(RuntimeShellSelection.CannonProfile(__instance)))return;
            var mass=__instance.ShellBlueprint?.Mass ?? 0;if(!float.IsFinite(mass)||mass<=0)return;
            var shell=__instance.ShellBlueprint!;
            var cost=AtgmInitialRoundResources.Cost(shell.Diameter,shell.PropellantLength,mass);
            __instance.SetMass(__instance.GetMass(MassType.Ammunition)+mass,MassType.Ammunition);
            __instance.SetCost(__instance.GetCost(MassType.Ammunition,CostType.Material)+cost.Material,MassType.Ammunition,CostType.Material);
            __instance.SetCost(__instance.GetCost(MassType.Ammunition,CostType.Assembly)+cost.Assembly,MassType.Ammunition,CostType.Assembly);
            addedMass[__instance.Pointer]=(mass,cost.Material,cost.Assembly);
            __instance.cachedMass=__instance.GetMass(MassType.Everything);__instance.RecalculateCenterOfMass();
        });
    }
    [HarmonyPrefix,HarmonyPatch(typeof(Cannon),nameof(Cannon.Release))]
    private static void Released(Cannon __instance)=>addedMass.Remove(__instance.Pointer);
}
