using Il2CppInterop.Runtime;
using BepInEx.Configuration;
using BepInEx;
using HarmonyLib;
using Sprocket.Blueprints;
using Sprocket.DamageModelling;
using Sprocket.ProjectilePhysicsSystems;
using Sprocket.UI;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Cannons.Editor;
using Sprocket.Vehicles.Weapons.Cannons;
using UnityEngine;

namespace SprocketShellSelector;

internal static class RuntimeShellSelection
{
    private const string LegacySaveKey = "roanApfsdsBeta";
    private const string SaveKey = "roanShellProfile";
    private sealed record Selection(CannonBlueprint Blueprint, string ProfileId);
    private sealed record RegisteredDart(IProjectileTypeRegister Register, IProjectileType Type)
    {
        internal readonly HashSet<IntPtr> Users=new();
        internal readonly string Guid=Type.Guid.ToString();
    }
    private static readonly Dictionary<IntPtr, Selection> Selections = new();
    private static readonly Dictionary<(IntPtr, string, float, float, float, ushort, float,string,string?,double,int), RegisteredDart> Types = new();
    private static readonly Dictionary<string, float> DamageFactors = new();
    private static readonly Dictionary<string, (string Id, float GunDiameter,string? FiringEra,double ChemicalBudget,ShellProfile Profile)> ImpactProfiles = new();
    private static readonly ShellNativeTypeCache<ProjectileTypeID> NativeTypes=new();
    [ThreadStatic] internal static bool LaunchAccepted;
    [HarmonyPrefix,HarmonyPatch(typeof(ProjectileRegister),nameof(ProjectileRegister.Release))]
    private static void ClearEraTypes(ProjectileRegister __instance)
    {
        NativeTypes.Clear(__instance.Pointer);
        foreach(var pair in Types.ToArray())
        {
            if(!pair.Value.Users.Remove(__instance.Pointer)||pair.Value.Users.Count>0)continue;
            Types.Remove(pair.Key);DamageFactors.Remove(pair.Value.Guid);ImpactProfiles.Remove(pair.Value.Guid);
        }
    }
    [ThreadStatic] private static float impactDamageFactor;
    [ThreadStatic] private static int scaledDamageCalls;
    private readonly record struct ImpactState(float PreviousFactor, int PreviousCalls, bool IsDart, ShellImpactContext? PreviousContext);
    private static ConfigEntry<float> diameter = null!, length = null!, density = null!, efficiency = null!,
        maxFactor = null!, maxSpeed = null!, quality = null!, damage = null!;
    private static ConfigEntry<bool> enabled = null!, open = null!, diagnostics = null!;
    private static readonly Il2CppSystem.Collections.Generic.List<string> Labels = new();
    private static IReadOnlyList<ShellProfile> profiles = Array.Empty<ShellProfile>();
    private static int catalogueRevision;

    internal static void Configure(ConfigFile config)
    {
        const string section = "APFSDS Beta";
        enabled = config.Bind(section, "Enabled", true, "Enable the experimental cannon shell selector.");
        open = config.Bind(section, "Section open", true, "Open the Shell profile inspector section.");
        diagnostics = config.Bind(section, "Log shots", true, "Log custom projectile registration, firing and impact.");
        ConfigEntry<float> Setting(string name, float value, float min, float max, string description) =>
            config.Bind(section, name, value, new ConfigDescription("Legacy migration value: ignored once the .shells.json file exists. " + description, new AcceptableValueRange<float>(min, max)));
        diameter = Setting("Diameter ratio", .22f, .05f, .9f, "Penetrator diameter divided by selected cannon calibre.");
        length = Setting("Length in calibres", 5f, .5f, 10f, "Penetrator length divided by selected cannon calibre.");
        density = Setting("Density", 17500f, 1000f, 25000f, "Density used to calculate penetrator mass, kg/m3.");
        efficiency = Setting("Velocity efficiency", .85f, .1f, 2f, "Velocity factor = efficiency * sqrt(vanilla AP mass / dart mass), with caps.");
        maxFactor = Setting("Maximum velocity factor", 2.2f, 1f, 4f, "Maximum multiplier of vanilla muzzle velocity.");
        maxSpeed = Setting("Maximum velocity", 2200f, 100f, 5000f, "Absolute muzzle velocity cap, m/s.");
        quality = Setting("Penetration quality", .45f, .1f, 4f, "AP penetration quality multiplier at equal mass, diameter and speed.");
        damage = Setting("Post penetration damage", .5f, .01f, 1f, "Scale native fragment health damage during APFSDS impacts; does not alter penetration energy.");
        // Read old values once when creating the profile file. From then on JSON
        // is authoritative; existing customized v0.5.0 settings survive migration.
        var path = ShellConfigMigration.EnsureCurrentProfileFile(Paths.ConfigPath,
            new(diameter.Value, length.Value, density.Value, efficiency.Value, maxFactor.Value,
                maxSpeed.Value, quality.Value, damage.Value));
        ReleaseProfiles.Upgrade(path);
        profiles = ShellProfiles.Parse(File.ReadAllText(path));
        if (!profiles.Any(p => p.Id == "aphe")) Plugin.ModLog.LogWarning("[Shell Profiles] APHE missing: configuration has reached the 16-profile limit. Remove one profile and restart to allow automatic repair.");
        Labels.Clear();
        Labels.Add("Vanilla ammunition");
        foreach (var profile in profiles) Labels.Add(profile.Label);
        Plugin.ModLog.LogInfo($"[Shell Profiles] Loaded {profiles.Count} profile(s) from {path}. Restart after editing. Legacy numeric CFG values are only used on first migration.");
    }

    internal static void Refresh(Cannon owner)
    {
        var path=System.IO.Path.Combine(Paths.ConfigPath,"sprocket.shellselector.shells.json");
        var updated=ShellProfiles.Parse(File.ReadAllText(path));
        // A new registration generation avoids reusing HE/ATGM definitions with old payloads.
        // Keep old registrations and immutable impact snapshots until their native register clears.
        catalogueRevision=checked(catalogueRevision+1);
        profiles=updated;Labels.Clear();Labels.Add("Vanilla ammunition");foreach(var p in profiles)Labels.Add(p.Label);
        var items=owner.Vehicle.ObjectReader.Items;
        int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list)=>list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
        for(int i=0;i<Count(items);i++)
        {
            var parts=items[i].Components;
            for(int j=0;j<Count(parts);j++)if(parts[j].TryCast<Cannon>() is {} cannon)cannon.RequestRebuild();
        }
        RuntimeArmourSimulator.Refresh();
        Plugin.ModLog.LogInfo($"[Shell editor] Refreshed {profiles.Count} shells; registration generation {catalogueRevision}.");
    }
    internal static IReadOnlyList<ShellProfile> Profiles => profiles;
    internal static bool Enabled => enabled.Value;
    private static ShellProfile? Profile(CannonBlueprint blueprint) => enabled.Value &&
        Selections.TryGetValue(blueprint.Pointer, out var selection)
        ? profiles.FirstOrDefault(p => p.Id == selection.ProfileId) : null;

    internal static ShellProfile? CannonProfile(Cannon cannon)
    {
        var stored=Profile(cannon.Blueprint);
        if(RuntimeAtgmLauncher.IsLauncher(cannon))
        {
            if(!Enabled||!RuntimeAtgmLauncher.Ready)return null;
            var selectedId=Selections.TryGetValue(cannon.Blueprint.Pointer,out var saved)?saved.ProfileId:null;
            if(AtgmInitialAssemblyRules.NeedsLauncherDefault(true,selectedId))
            {
                stored=profiles.Where(p=>AtgmLauncherModel.Allows(p)&&RuntimeShellEra.Allowed(p,cannon.Vehicle)).OrderBy(p=>p.Atgm!.GuidanceMode=="sight"?0:1).FirstOrDefault();
                if(stored!=null&&!OrdinarySharesBlueprint(cannon))
                {
                    // Existing native blueprint selection/save mechanism; once only.
                    Selections[cannon.Blueprint.Pointer]=new(cannon.Blueprint,stored.Id);
                    cannon.Blueprint.MarkModified();
                    Plugin.ModLog.LogInfo($"[ATGM launcher] DEFAULT cannon={cannon.VUID.Value} profile={stored.Id}; unselected/Vanilla migrated, existing custom choice retained.");
                }
            }
            if(!AtgmLauncherModel.Allows(stored))return null;
        }
        return RuntimeShellEra.Resolve(stored,cannon.Vehicle);
    }

    private static bool OrdinarySharesBlueprint(Cannon own)
    {
        var items=own.Vehicle.ObjectReader.Items;bool foundOwn=false;
        int Count<T>(Il2CppSystem.Collections.Generic.IReadOnlyList<T> list)=>list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<T>>().Count;
        for(int i=0;i<Count(items);i++){var parts=items[i].Components;for(int j=0;j<Count(parts);j++)if(parts[j].TryCast<Cannon>() is {} c){if(c.Pointer==own.Pointer)foundOwn=true;if(!RuntimeAtgmLauncher.IsLauncher(c)&&c.Blueprint.Pointer==own.Blueprint.Pointer)return true;}}
        return !foundOwn; // Wait for native ownership enumeration before persisting; never mutate an ordinary shared blueprint.
    }

    internal static ShellProfile? SelectedProfile(CannonBehaviour weapon) =>
        weapon.mount?.TryCast<Cannon>() is {} cannon ? CannonProfile(cannon) : null;

    private static bool Selected(Cannon cannon) => CannonProfile(cannon) != null;

    private static DartBallistics Calculate(Cannon cannon) =>
        ShellBalance.Calculate(cannon.Blueprint.Caliber, cannon.Blueprint.MuzzleVelocity, cannon.Blueprint.PenetratorConstant,
            CannonProfile(cannon) ?? throw new InvalidOperationException("No custom profile selected."));

    internal static ShellProfile? ProjectileProfile(ProjectileInstance projectile) =>
        ImpactProfiles.TryGetValue(projectile.Definition.Guid.ToString(),out var impact)
            ? impact.Profile : null;

    private static void Guard(string action, Action work)
    {
        try { work(); }
        catch (Exception ex) { Plugin.ModLog.LogError($"[APFSDS Beta] {action}: {ex}"); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CannonEditor), nameof(CannonEditor.OnGUI))]
    private static void Draw(CannonEditor __instance, IGUILayout layout)
    {
        if (!enabled.Value) return;
        Guard("Inspector", () =>
        {
            var cannon = __instance.Component;
            var blueprint = cannon.Blueprint;
            IReadOnlyList<ShellProfile> available=RuntimeShellEra.Available(profiles,cannon.Vehicle).Where(p=>!RuntimeAtgmLauncher.IsLauncher(cannon)||AtgmLauncherModel.Allows(p)).ToArray();
            var labels=RuntimeShellEra.Labels(available);
            if(RuntimeAtgmLauncher.IsLauncher(cannon))labels[0]="Unavailable / ATGM launcher only";
            var ui = layout.TryCast<IGUIElementDrawer>();
            if (ui == null || blueprint == null) return;
            layout.EndAllDropdowns();
            layout.BeginDropdown("Shell profile", open.Value, Ui.BoolCallback(value => open.Value = value));
            try
            {
                ui.Dropdown("Shell type", labels.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(),
                    CannonProfile(cannon) is { } selected ? available.ToList().FindIndex(p => p.Id == selected.Id) + 1 : 0,
                    Ui.IntCallback(index => Guard("Select shell", () =>
                    {
                        if (index < 0 || index > available.Count) return;
                        // Recheck captured profile against the actual owner at callback time.
                        if(index>0&&!RuntimeShellEra.Allowed(available[index-1],cannon.Vehicle))return;
                        // Validate before changing the saved selection.
                        if (index > 0) _ = ShellBalance.Calculate(blueprint.Caliber, blueprint.MuzzleVelocity,
                            blueprint.PenetratorConstant, available[index - 1]);
                        Selections[blueprint.Pointer] = new(blueprint,
                            index == 0 ? ShellProfiles.Vanilla : available[index - 1].Id);
                        Plugin.ModLog.LogInfo($"[Shell Selection] Cannon {blueprint.Caliber}mm selected {CannonProfile(cannon)?.Id ?? ShellProfiles.Vanilla}; simulator selection is independent.");
                        blueprint.MarkModified();
                        cannon.RequestRebuild();
                        __instance.RequestRedraw();
                    })), "Applies to cannons sharing this design. APFSDS overrides loaded AP/APHE shots; rack sizes and loading remain vanilla in this beta.");
                RuntimeDamageFeed.DrawOptions(ui,()=>__instance.RequestRedraw());
                var editorTip = new UITooltip { Header = "Modular shells", Body = "Edit names and modules for the shared catalogue. Supported designs apply on Save; pending combinations save as drafts only." };
                ui.Button("Edit shells", DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>((Action)(() => RuntimeShellEditor.Open(() => { Refresh(cannon); __instance.RequestRedraw(); }))), ref editorTip);
                var importTip = new UITooltip { Header = "Import shell JSON", Body = "Read modular or legacy JSON from BepInEx/config/sprocket.shellselector.modules.import.json. Existing IDs are retained; files change only on Save." };
                ui.Button("Import shell JSON", DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>((Action)(() => RuntimeShellEditor.Import(() => { Refresh(cannon); __instance.RequestRedraw(); }))), ref importTip);
                if(RuntimeShellEditor.LastSaveMessage is {} saveMessage)ui.InfoField(saveMessage,2);
                if(Profile(blueprint) is {} stored && !RuntimeShellEra.Allowed(stored,cannon.Vehicle))
                    ui.InfoField(RuntimeAtgmLauncher.IsLauncher(cannon)
                        ? "Saved shell profile unavailable in this era; firing disabled. Selection is retained."
                        : "Saved shell profile unavailable in this era; native ammunition used. Selection is retained.",2);
                if(RuntimeAtgmLauncher.IsLauncher(cannon)&&CannonProfile(cannon)==null)
                    ui.InfoField("ATGM launcher unavailable: firing disabled. Select an allowed launcher ATGM profile and check native technology availability.",2);
                if (Selected(cannon))
                {
                    var dart = Calculate(cannon);
                    var pen = PenetrationUtils.ComputePenetration(dart.Diameter * 1000, dart.Mass,
                        dart.Velocity, dart.PenetratorConstant);
                    ui.InfoField($"{blueprint.Caliber} mm cannon | {dart.Diameter * 1000:0.0} mm penetrator", 2);
                    ui.InfoField($"{dart.Length * 1000:0} mm long | {dart.Mass:0.00} kg", 2);
                    var selectedProfile=CannonProfile(cannon)!;
                    if(ShellBalance.FlightEnabled(selectedProfile))
                        ui.InfoField($"ATGM: cruise {selectedProfile.Atgm!.FlightSpeed:0} m/s | {selectedProfile.Atgm.Acceleration:0} m/s² | {selectedProfile.Atgm.MaxTurnRate:0} deg/s | guidance {selectedProfile.Atgm.GuidanceMode} | flight hook {(RuntimeAtgm.Ready && RuntimeAtgm.Enabled ? "ready" : "unavailable")}",2);
                    ui.InfoField(selectedProfile.Behavior=="he" ? $"{dart.Velocity:0} m/s | native HE power {ShellBalance.BlastPower(selectedProfile,blueprint.Caliber):0.0}" : selectedProfile.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun" ? $"{dart.Velocity:0} m/s | {RuntimeShellEra.ChemicalBudget(selectedProfile,blueprint.Caliber,cannon.Vehicle):0} mm chemical proxy penetration" : $"{dart.Velocity:0} m/s | {pen:0} mm base RHA penetration", 2);
                    if(selectedProfile.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun")
                        ui.InfoField(RuntimeShellEra.ChemicalDescription(selectedProfile,cannon.Vehicle),2);
                    ui.InfoField(CannonProfile(cannon)!.Behavior == "aphe"
                        ? "APHE: reduced penetration | amplified native AP spall"
                        : CannonProfile(cannon)!.Behavior == "apfsds" ? "APFSDS: narrow cone | spall increases as remaining penetration falls"
                        : selectedProfile.Behavior is "he" or "heat" or "hesh" or "atgm" or "atgm_gun" ? $"{selectedProfile.Behavior.ToUpperInvariant()}: EXPERIMENTAL native blast / equivalent penetrator approximation" : $"Fragment damage: {dart.DamageMultiplier:P0} | no explosive filler", 2);
                }
            }
            finally { layout.EndAllDropdowns(); }
        });
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Cannon), nameof(Cannon.Build))]
    private static void Aim(Cannon __instance)
    {
        if (Selected(__instance))
            Guard("Aiming velocity", () => __instance.muzzleVelocity = (float)(RuntimeAtgm.Ready && RuntimeAtgm.Enabled && CannonProfile(__instance) is {} aimingProfile && ShellBalance.Powered(aimingProfile) ? CannonProfile(__instance)!.Atgm!.FlightSpeed : Calculate(__instance).Velocity));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CannonProperties), nameof(CannonProperties.OnRebuilt))]
    private static void Preview(CannonProperties __instance, VehicleComponent component)
    {
        var cannon = component.TryCast<Cannon>();
        if (cannon == null || !Selected(cannon)) return;
        Guard("Cannon performance preview", () =>
        {
            var dart = Calculate(cannon);
            __instance.muzzleVelocity.Value = dart.Velocity;
            __instance.projectileMass.Value = dart.Mass;
            var profile=CannonProfile(cannon)!;
            __instance.penetration.Value = profile.Behavior=="he" ? 0 : profile.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun" ? (float)RuntimeShellEra.ChemicalBudget(profile,cannon.Blueprint.Caliber,cannon.Vehicle) : PenetrationUtils.ComputePenetration(dart.Diameter * 1000,
                dart.Mass, dart.Velocity, dart.PenetratorConstant);
            __instance.dispersion.Value = ShellProperties.CalculateDispersion(cannon.Blueprint.Caliber,
                CannonProperties.DispersionMeasureDistance, dart.Velocity, cannon.Blueprint.BoreLength);
        });
    }

    // Launch receives the firing CannonBehaviour as source. We replace the actual
    // projectile type, not shared vanilla shell definitions or calibre/charge data.
    [HarmonyPrefix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.Launch))]
    private static bool Launch(ProjectileRegister __instance,Il2CppSystem.Object source, ref ProjectileLaunchState launchState,
        ref ProjectileTypeID projectileTypeID)
    {
        LaunchAccepted=true;
        try
        {
            // A previous custom type may be cached by a caller. Recover its native
            // input before eligibility checks, including unknown source contexts.
            var incoming=projectileTypeID.Value.ToString();
            if(!NativeTypes.Restore(__instance.Pointer,incoming,ref projectileTypeID,source?.Pointer??IntPtr.Zero)&&NativeTypes.Known(__instance.Pointer,incoming))
            {LaunchAccepted=false;RuntimeShellEra.WarnCached(source?.Pointer??IntPtr.Zero);return false;}
            var cannon = source?.TryCast<CannonBehaviour>()?.mount?.TryCast<Cannon>();
            if(RuntimeAtgmLauncher.IsLauncher(cannon)&&!RuntimeAtgmLauncher.CanLaunch(cannon!))
            {LaunchAccepted=false;return false;}
            if (cannon == null || !Selected(cannon)) return true;
            var dart = Calculate(cannon);
            var register = IProjectileTypeRegister.Instance;
            if (register == null) throw new InvalidOperationException("Projectile register is unavailable.");
            var profile = CannonProfile(cannon)!;
            // Distinct prototypes retain their firing-era budget even after the design changes era.
            var firingEra=RuntimeShellEra.Era(cannon.Vehicle);
            var chemicalBudget=RuntimeShellEra.ChemicalBudget(profile,cannon.Blueprint.Caliber,cannon.Vehicle);
            var chemicalEra=profile.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun" ? firingEra : null;
            var key = (register.Pointer, profile.Id, dart.Diameter, dart.Length, dart.Mass, dart.PenetratorConstant, dart.DamageMultiplier,projectileTypeID.Value.ToString(),chemicalEra,chemicalBudget,catalogueRevision);
            if (!Types.TryGetValue(key, out var registered))
            {
                var guid = new Il2CppSystem.Guid(System.Guid.NewGuid().ToString());
                var functions = new Il2CppSystem.Collections.Generic.List<ProjectileFunctionDefinition>();
                if(profile.Behavior=="he") functions.Add(new HighExplosiveProjectileFunctionDefinition(0,(float)ShellBalance.BlastPower(profile,cannon.Blueprint.Caliber)));
                else functions.Add(new ArmouredPiercingProjectileFunctionDefinition(dart.PenetratorConstant));
                // No propellant launch function: inject the balanced muzzle velocity
                // once, preserving inherited vehicle velocity. Native Launch then
                // computes impulse/energy from this definition's real mass/velocity.
                var type = register.Create(profile.Label, guid, dart.Mass, dart.Diameter, dart.Length,
                    ShellBalance.Powered(profile) ? 1f : 10f, (float)(profile.Atgm?.MaximumFlightTime ?? 30), functions.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<ProjectileFunctionDefinition>>());
                registered = new(register, type);
                Types[key] = registered;
                if (diagnostics.Value) Plugin.ModLog.LogInfo($"[APFSDS Beta] Registered {guid}: " +
                    $"diameter={dart.Diameter * 1000:0.0}mm length={dart.Length * 1000:0}mm mass={dart.Mass:0.000}kg K={dart.PenetratorConstant}");
            }
            DamageFactors[registered.Type.Guid.ToString()] = profile.Behavior is "apfsds" or "aphe" or "heat" or "hesh" or "atgm" or "atgm_gun" ? 1f : dart.DamageMultiplier;
            registered.Users.Add(__instance.Pointer);
            ImpactProfiles[registered.Type.Guid.ToString()] = (profile.Id, cannon.Blueprint.Caliber * .001f,firingEra,chemicalBudget,profile);
            if(diagnostics.Value && profile.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun")
                Plugin.ModLog.LogInfo($"[Chemical budget] launch profile={profile.Id} firingEra={firingEra} effective={chemicalBudget:0.0}mm stockPeriod={ShellChemicalBudget.IsStock(profile)}");
            var direction = launchState.Direction;
            if (direction.sqrMagnitude < .00001f) throw new InvalidOperationException("Invalid launch direction.");
            // Native CalculateDispersion returns mm spread at a distance in metres.
            // At one metre, convert to a unit-direction offset for the native random
            // fire-direction helper. Keep gun calibre for the accuracy calculation.
            var spread = ShellProperties.CalculateDispersion(cannon.Blueprint.Caliber, 1f,
                dart.Velocity, cannon.Blueprint.BoreLength) * .001f;
            var fireDirection = DetonationPropellantProjectileFunction.GetFireDirection(
                Quaternion.LookRotation(direction), spread);
            var velocity = launchState.InitialVelocity + fireDirection * dart.Velocity;
            // Commit only after registration/calculation succeed, leaving vanilla
            // input untouched on failures.
            NativeTypes.Remember(__instance.Pointer,registered.Type.ID.Value.ToString(),projectileTypeID,source?.Pointer??IntPtr.Zero);
            projectileTypeID = registered.Type.ID;
            launchState = new ProjectileLaunchState(launchState.Position, launchState.Direction, velocity);
            if (diagnostics.Value) Plugin.ModLog.LogInfo($"[APFSDS Beta] FIRE cannon={cannon.Blueprint.Caliber}mm " +
                $"projectileType={projectileTypeID.Value} muzzle={dart.Velocity:0.0}m/s inherited velocity preserved");
        }
        catch (Exception ex)
        {
            if(RuntimeAtgmLauncher.IsLauncher(source?.TryCast<CannonBehaviour>()?.mount?.TryCast<Cannon>()))
            {LaunchAccepted=false;Plugin.ModLog.LogError("[ATGM launcher] Shot withheld: "+ex.Message);return false;}
            Plugin.ModLog.LogError($"[APFSDS Beta] Shot kept vanilla: {ex}");
        }
        return true;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.Launch))]
    private static void Spawned(ProjectileRegister __instance, Il2CppSystem.Object source,
        ref ProjectileLauncherInfo launcherInfo, ref ProjectileFireInfo fireInfo)
    {
        if (LaunchAccepted)
        {
            try
            {
                var cannon=source?.TryCast<CannonBehaviour>()?.mount?.TryCast<Cannon>();
                if(cannon!=null && __instance.activeIdMap.TryGetValue(fireInfo.ProjectileID,out var index))
                {
                    var projectile=__instance.pool[index];
                    if(ImpactProfiles.TryGetValue(projectile.Definition.Guid.ToString(),out var impact))
                    {
                        var profile=impact.Item5;
                        var missile=ShellBalance.Powered(profile) || ShellBalance.Carrier(profile)!="fullBore";
                        var before=fireInfo.Impulse;
                        var after=(float)ShellRecoil.Restore(before,ShellProperties.GetProjectileMass(cannon.Blueprint.Caliber),
                            cannon.Blueprint.MuzzleVelocity,launcherInfo.BarrelLength,missile);
                        fireInfo=new ProjectileFireInfo(fireInfo.ProjectileID,after,fireInfo.KineticEnergy);
                        if(diagnostics.Value && after!=before)
                            Plugin.ModLog.LogInfo($"[Recoil] profile={profile.Id} projectile={fireInfo.ProjectileID} nativeChargeMinimum={after:0.0} previous={before:0.0}; terminal ballistics retained.");
                    }
                }
            }
            catch(Exception ex){Plugin.ModLog.LogWarning("[Recoil] Native firing output retained: "+ex.Message);}
        }
        if (!diagnostics.Value || !LaunchAccepted) return;
        var id = fireInfo.ProjectileID;
        Guard("Spawn diagnostics", () => LogSpawn(__instance, id));
    }

    private static void LogSpawn(ProjectileRegister register, int id)
    {
        if (!register.activeIdMap.TryGetValue(id, out var index)) return;
        var projectile = register.pool[index];
        if (!DamageFactors.ContainsKey(projectile.Definition.Guid.ToString())) return;
        Plugin.ModLog.LogInfo($"[APFSDS Beta] SPAWN projectile={id} type={projectile.Definition.Name} " +
            $"diameter={projectile.Definition.Diameter * 1000:0.0}mm mass={projectile.Definition.Mass:0.000}kg " +
            $"worldSpeed={projectile.velocity.magnitude:0.0}m/s");
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ArmourPiercingProjectileFunction), nameof(ArmourPiercingProjectileFunction.HitDamageModel))]
    private static void Impact(ref ProjectileInstance projectile, out ImpactState __state)
    {
        __state = new(impactDamageFactor, scaledDamageCalls, false, RuntimeSpall.Impact);
        RuntimeSpall.Impact = null;
        impactDamageFactor = 1f;
        scaledDamageCalls = 0;
        try
        {
            if (DamageFactors.TryGetValue(projectile.Definition.Guid.ToString(), out var factor))
            {
                RuntimeAtgm.EndModularFlight(projectile);
                impactDamageFactor = factor;
                if (ImpactProfiles.TryGetValue(projectile.Definition.Guid.ToString(), out var impactProfile))
                    RuntimeSpall.Impact = new() { ProfileId = impactProfile.Id, Profile = impactProfile.Profile, GunDiameter = impactProfile.GunDiameter, LiveImpact=true,FiringEra=impactProfile.FiringEra,ChemicalBudget=impactProfile.ChemicalBudget };
                __state = __state with { IsDart = true };
                if (diagnostics.Value) Plugin.ModLog.LogInfo($"[APFSDS Beta] IMPACT projectile={projectile.ID} speed={projectile.velocity.magnitude:0.0}m/s damageFactor={factor:0.00}");
            }
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[APFSDS Beta] Impact lookup: {ex}"); }
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(ArmourPiercingProjectileFunction), nameof(ArmourPiercingProjectileFunction.HitDamageModel))]
    private static void EndImpact(ImpactState __state)
    {
        PlacedEra.Flush(RuntimeSpall.Impact);
        if (__state.IsDart && diagnostics.Value)
            Plugin.ModLog.LogInfo($"[APFSDS Beta] IMPACT completed; scaled fragment damage calls={scaledDamageCalls}");
        impactDamageFactor = __state.PreviousFactor;
        scaledDamageCalls = __state.PreviousCalls;
        RuntimeSpall.Impact = __state.PreviousContext;
    }

    [HarmonyPrefix,HarmonyPatch(typeof(ExplosiveProjectileFunction),nameof(ExplosiveProjectileFunction.HitDamageModel))]
    private static void ExplosiveImpact(ref ProjectileInstance projectile,out ImpactState __state) => Impact(ref projectile,out __state);
    [HarmonyFinalizer,HarmonyPatch(typeof(ExplosiveProjectileFunction),nameof(ExplosiveProjectileFunction.HitDamageModel))]
    private static void ExplosiveEnd(ImpactState __state) => EndImpact(__state);
    [HarmonyPrefix,HarmonyPatch(typeof(ExplosiveProjectileFunction),nameof(ExplosiveProjectileFunction.HitPhysicsObject))]
    private static void ExplosivePhysics(ref ProjectileInstance projectile,out ImpactState __state) => Impact(ref projectile,out __state);
    [HarmonyFinalizer,HarmonyPatch(typeof(ExplosiveProjectileFunction),nameof(ExplosiveProjectileFunction.HitPhysicsObject))]
    private static void ExplosivePhysicsEnd(ImpactState __state) => EndImpact(__state);

    // ApplyFragmentHits calls this overload directly; Fragment.Damage is inlined.
    // Only change health damage during the custom impact, not flight or penetration.
    [HarmonyPostfix, HarmonyPatch(typeof(ShellProperties), nameof(ShellProperties.GetDamage), new[] { typeof(float), typeof(float) })]
    private static void Damage(ref float __result)
    {
        if (impactDamageFactor > 0 && impactDamageFactor < 1)
        {
            __result *= impactDamageFactor;
            scaledDamageCalls++;
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CannonBlueprint), nameof(CannonBlueprint.Save))]
    private static void Save(CannonBlueprint __instance, IBlueprintSerializer info)
    {
        var id = Selections.TryGetValue(__instance.Pointer, out var selection) ? selection.ProfileId : ShellProfiles.Vanilla;
        info.AddValue(SaveKey, id);
        // Keep v0.5.0 able to open new saves as well.
        info.AddValue(LegacySaveKey, id == ShellProfiles.LegacyDart);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CannonBlueprint), nameof(CannonBlueprint.Load))]
    private static void Load(CannonBlueprint __instance, IBlueprintDeserializer info)
    {
        Guard("Load profile", () =>
        {
            var hasValue = false;
            var hasLegacyValue = false;
            var copier = info.TryCast<BlueprintCopier>();
            if (copier != null)
            {
                hasValue = copier.TryGetValue(SaveKey, out _);
                hasLegacyValue = copier.TryGetValue(LegacySaveKey, out _);
            }
            else
            {
                var wrapper = info.TryCast<SerializationBlueprintWrapper>();
                if (wrapper != null)
                {
                    var entries = wrapper.info.GetEnumerator();
                    while (entries.MoveNext())
                    {
                        if (entries.Name == SaveKey) hasValue = true;
                        if (entries.Name == LegacySaveKey) hasLegacyValue = true;
                    }
                }
                else Plugin.ModLog.LogWarning("[APFSDS Beta] Unknown blueprint reader; using vanilla profile.");
            }
            var requested = hasValue ? info.GetString(SaveKey) : null;
            var id = ShellProfiles.Resolve(requested, hasLegacyValue && info.GetBoolean(LegacySaveKey), profiles);
            if (requested != null && requested != id)
                Plugin.ModLog.LogWarning($"[Shell Profiles] Missing saved profile '{requested}'; using vanilla ammunition.");
            Selections[__instance.Pointer] = new(__instance, id);
        });
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CannonBlueprint), nameof(CannonBlueprint.Clone))]
    private static void Clone(CannonBlueprint __instance, Il2CppSystem.Object __result)
    {
        var copy = __result.TryCast<CannonBlueprint>();
        if (copy != null && Selections.TryGetValue(__instance.Pointer, out var selection))
            Selections[copy.Pointer] = new(copy, selection.ProfileId);
    }
}





