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
    private sealed record RegisteredDart(IProjectileTypeRegister Register, IProjectileType Type);
    private static readonly Dictionary<IntPtr, Selection> Selections = new();
    private static readonly Dictionary<(IntPtr, string, float, float, float, ushort, float), RegisteredDart> Types = new();
    private static readonly Dictionary<string, float> DamageFactors = new();
    private static readonly Dictionary<string, (string Id, float GunDiameter)> ImpactProfiles = new();
    [ThreadStatic] private static float impactDamageFactor;
    [ThreadStatic] private static int scaledDamageCalls;
    private readonly record struct ImpactState(float PreviousFactor, int PreviousCalls, bool IsDart, ShellImpactContext? PreviousContext);
    private static ConfigEntry<float> diameter = null!, length = null!, density = null!, efficiency = null!,
        maxFactor = null!, maxSpeed = null!, quality = null!, damage = null!;
    private static ConfigEntry<bool> enabled = null!, open = null!, diagnostics = null!;
    private static readonly Il2CppSystem.Collections.Generic.List<string> Labels = new();
    private static IReadOnlyList<ShellProfile> profiles = Array.Empty<ShellProfile>();

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
        profiles = ShellProfiles.Parse(File.ReadAllText(path));
        if (!profiles.Any(p => p.Id == "aphe")) Plugin.ModLog.LogWarning("[Shell Profiles] APHE missing: configuration has reached the 16-profile limit. Remove one profile and restart to allow automatic repair.");
        Labels.Clear();
        Labels.Add("Vanilla ammunition");
        foreach (var profile in profiles) Labels.Add(profile.Label);
        Plugin.ModLog.LogInfo($"[Shell Profiles] Loaded {profiles.Count} profile(s) from {path}. Restart after editing. Legacy numeric CFG values are only used on first migration.");
    }

    internal static IReadOnlyList<ShellProfile> Profiles => profiles;
    internal static bool Enabled => enabled.Value;
    private static ShellProfile? Profile(CannonBlueprint blueprint) => enabled.Value &&
        Selections.TryGetValue(blueprint.Pointer, out var selection)
        ? profiles.FirstOrDefault(p => p.Id == selection.ProfileId) : null;

    private static bool Selected(CannonBlueprint blueprint) => Profile(blueprint) != null;

    private static DartBallistics Calculate(CannonBlueprint blueprint) =>
        ShellBallistics.Calculate(blueprint.Caliber, blueprint.MuzzleVelocity, blueprint.PenetratorConstant,
            Profile(blueprint)?.Settings ?? throw new InvalidOperationException("No custom profile selected."));

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
            var ui = layout.TryCast<IGUIElementDrawer>();
            if (ui == null || blueprint == null) return;
            layout.EndAllDropdowns();
            layout.BeginDropdown("Shell profile (beta)", open.Value, Ui.BoolCallback(value => open.Value = value));
            try
            {
                ui.Dropdown("Shell type", Labels.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(),
                    Profile(blueprint) is { } selected ? profiles.ToList().FindIndex(p => p.Id == selected.Id) + 1 : 0,
                    Ui.IntCallback(index => Guard("Select shell", () =>
                    {
                        if (index < 0 || index > profiles.Count) return;
                        // Validate before changing the saved selection.
                        if (index > 0) _ = ShellBallistics.Calculate(blueprint.Caliber, blueprint.MuzzleVelocity,
                            blueprint.PenetratorConstant, profiles[index - 1].Settings);
                        Selections[blueprint.Pointer] = new(blueprint,
                            index == 0 ? ShellProfiles.Vanilla : profiles[index - 1].Id);
                        blueprint.MarkModified();
                        cannon.RequestRebuild();
                        __instance.RequestRedraw();
                    })), "Applies to cannons sharing this design. APFSDS overrides loaded AP/APHE shots; rack sizes and loading remain vanilla in this beta.");
                if (Selected(blueprint))
                {
                    var dart = Calculate(blueprint);
                    var pen = PenetrationUtils.ComputePenetration(dart.Diameter * 1000, dart.Mass,
                        dart.Velocity, dart.PenetratorConstant);
                    ui.InfoField($"{blueprint.Caliber} mm cannon | {dart.Diameter * 1000:0.0} mm penetrator", 2);
                    ui.InfoField($"{dart.Length * 1000:0} mm long | {dart.Mass:0.00} kg", 2);
                    ui.InfoField($"{dart.Velocity:0} m/s | {pen:0} mm base RHA penetration", 2);
                    ui.InfoField(Profile(blueprint)!.Id == "aphe"
                        ? "APHE: reduced penetration | amplified native AP spall"
                        : Profile(blueprint)!.Id == "apfsds" ? "APFSDS: narrow cone | spall increases as remaining penetration falls"
                        : $"Fragment damage: {dart.DamageMultiplier:P0} | no explosive filler", 2);
                }
            }
            finally { layout.EndAllDropdowns(); }
        });
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Cannon), nameof(Cannon.Build))]
    private static void Aim(Cannon __instance)
    {
        if (Selected(__instance.Blueprint))
            Guard("Aiming velocity", () => __instance.muzzleVelocity = Calculate(__instance.Blueprint).Velocity);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CannonProperties), nameof(CannonProperties.OnRebuilt))]
    private static void Preview(CannonProperties __instance, VehicleComponent component)
    {
        var cannon = component.TryCast<Cannon>();
        if (cannon == null || !Selected(cannon.Blueprint)) return;
        Guard("Cannon performance preview", () =>
        {
            var dart = Calculate(cannon.Blueprint);
            __instance.muzzleVelocity.Value = dart.Velocity;
            __instance.projectileMass.Value = dart.Mass;
            __instance.penetration.Value = PenetrationUtils.ComputePenetration(dart.Diameter * 1000,
                dart.Mass, dart.Velocity, dart.PenetratorConstant);
            __instance.dispersion.Value = ShellProperties.CalculateDispersion(cannon.Blueprint.Caliber,
                CannonProperties.DispersionMeasureDistance, dart.Velocity, cannon.Blueprint.BoreLength);
        });
    }

    // Launch receives the firing CannonBehaviour as source. We replace the actual
    // projectile type, not shared vanilla shell definitions or calibre/charge data.
    [HarmonyPrefix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.Launch))]
    private static void Launch(Il2CppSystem.Object source, ref ProjectileLaunchState launchState,
        ref ProjectileTypeID projectileTypeID)
    {
        try
        {
            var cannon = source?.TryCast<CannonBehaviour>()?.mount?.TryCast<Cannon>();
            if (cannon == null || !Selected(cannon.Blueprint)) return;
            var dart = Calculate(cannon.Blueprint);
            var register = IProjectileTypeRegister.Instance;
            if (register == null) throw new InvalidOperationException("Projectile register is unavailable.");
            var profile = Profile(cannon.Blueprint)!;
            var key = (register.Pointer, profile.Id, dart.Diameter, dart.Length, dart.Mass, dart.PenetratorConstant, dart.DamageMultiplier);
            if (!Types.TryGetValue(key, out var registered))
            {
                var guid = new Il2CppSystem.Guid(System.Guid.NewGuid().ToString());
                var functions = new Il2CppSystem.Collections.Generic.List<ProjectileFunctionDefinition>();
                functions.Add(new ArmouredPiercingProjectileFunctionDefinition(dart.PenetratorConstant));
                // No propellant launch function: inject the balanced muzzle velocity
                // once, preserving inherited vehicle velocity. Native Launch then
                // computes impulse/energy from this definition's real mass/velocity.
                var type = register.Create(profile.Label, guid, dart.Mass, dart.Diameter, dart.Length,
                    10f, 30f, functions.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<ProjectileFunctionDefinition>>());
                registered = new(register, type);
                Types[key] = registered;
                if (diagnostics.Value) Plugin.ModLog.LogInfo($"[APFSDS Beta] Registered {guid}: " +
                    $"diameter={dart.Diameter * 1000:0.0}mm length={dart.Length * 1000:0}mm mass={dart.Mass:0.000}kg K={dart.PenetratorConstant}");
            }
            DamageFactors[registered.Type.Guid.ToString()] = profile.Id is "apfsds" or "aphe" ? 1f : dart.DamageMultiplier;
            ImpactProfiles[registered.Type.Guid.ToString()] = (profile.Id, cannon.Blueprint.Caliber * .001f);
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
            projectileTypeID = registered.Type.ID;
            launchState = new ProjectileLaunchState(launchState.Position, launchState.Direction, velocity);
            if (diagnostics.Value) Plugin.ModLog.LogInfo($"[APFSDS Beta] FIRE cannon={cannon.Blueprint.Caliber}mm " +
                $"projectileType={projectileTypeID.Value} muzzle={dart.Velocity:0.0}m/s inherited velocity preserved");
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[APFSDS Beta] Shot kept vanilla: {ex}"); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.Launch))]
    private static void Spawned(ProjectileRegister __instance, ref ProjectileFireInfo fireInfo)
    {
        if (!diagnostics.Value) return;
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
                impactDamageFactor = factor;
                if (ImpactProfiles.TryGetValue(projectile.Definition.Guid.ToString(), out var impactProfile))
                    RuntimeSpall.Impact = new() { ProfileId = impactProfile.Id, GunDiameter = impactProfile.GunDiameter, LiveImpact=true };
                __state = __state with { IsDart = true };
                if (diagnostics.Value) Plugin.ModLog.LogInfo($"[APFSDS Beta] IMPACT projectile={projectile.ID} speed={projectile.velocity.magnitude:0.0}m/s damageFactor={factor:0.00}");
            }
        }
        catch (Exception ex) { Plugin.ModLog.LogError($"[APFSDS Beta] Impact lookup: {ex}"); }
    }

    [HarmonyFinalizer, HarmonyPatch(typeof(ArmourPiercingProjectileFunction), nameof(ArmourPiercingProjectileFunction.HitDamageModel))]
    private static void EndImpact(ImpactState __state)
    {
        if (__state.IsDart && diagnostics.Value)
            Plugin.ModLog.LogInfo($"[APFSDS Beta] IMPACT completed; scaled fragment damage calls={scaledDamageCalls}");
        impactDamageFactor = __state.PreviousFactor;
        scaledDamageCalls = __state.PreviousCalls;
        RuntimeSpall.Impact = __state.PreviousContext;
    }

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






