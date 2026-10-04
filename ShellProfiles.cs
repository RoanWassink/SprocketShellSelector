using System.Text.Json;

namespace SprocketShellSelector;

internal sealed record ShellProfile(string Id, string Label, DartSettings Settings,
    string Behavior = "ap", double ChemicalPenetrationMm = 0,
    double NativeExplosivePower = 0, double SpallMultiplier = 1,
    double ConeHalfAngleDegrees = 90, double ExplosionScale = 1, double SecondPlatePenetrationFactor = .15,
    double AirGapLossPerCalibre = .35, double ReferenceCalibreMm = 100, AtgmSettings? Atgm = null);

// Managed data only: the same validation runs before UI, previews and native shots.
internal static class ShellProfiles
{
    internal const string Vanilla = "vanilla";
    internal const string LegacyDart = "apfsds";
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "id", "label", "penetratorDiameterFactor", "penetratorLengthInCalibres", "penetratorDensity",
        "velocityEfficiency", "velocityMultiplier", "maximumVelocityFactor", "maximumVelocity",
        "penetrationQuality", "fragmentDamageMultiplier"
    };
    private static readonly HashSet<string> Optional = new(StringComparer.Ordinal)
    { "behavior", "chemicalPenetrationMm", "nativeExplosivePower", "spallMultiplier", "coneHalfAngleDegrees", "explosionScale", "secondPlatePenetrationFactor", "airGapLossPerCalibre", "referenceCalibreMm", "flightSpeed", "maxTurnRate", "maximumFlightTime", "guidanceDelay", "guidanceMode", "launchSpeed", "launchSpeedMode", "launchSpeedMultiplier", "acceleration", "motorDelay", "motorBurnTime", "coastDeceleration" };

    internal static IReadOnlyList<ShellProfile> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        CheckFields(root, new HashSet<string>(StringComparer.Ordinal) { "schemaVersion", "profiles" });
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new FormatException("Unsupported shell profile schemaVersion; expected 1.");
        var profiles = root.GetProperty("profiles");
        if (profiles.ValueKind != JsonValueKind.Array || profiles.GetArrayLength() is < 1 or > 16)
            throw new FormatException("Expected 1 to 16 shell profiles.");
        var result = new List<ShellProfile>();
        var ids = new HashSet<string>(StringComparer.Ordinal) { Vanilla };
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Vanilla ammunition" };
        foreach (var item in profiles.EnumerateArray())
        {
            CheckFields(item, Fields, Optional);
            var id = item.GetProperty("id").GetString() ?? "";
            var label = item.GetProperty("label").GetString() ?? "";
            if (id.Length is < 1 or > 40 || id.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')) || !ids.Add(id))
                throw new FormatException($"Invalid, duplicate or reserved profile id: {id}.");
            if (string.IsNullOrWhiteSpace(label) || label.Length > 80 || label.Any(char.IsControl) || !labels.Add(label))
                throw new FormatException($"Invalid or duplicate profile label for {id}.");
            double ReadNumber(string name, JsonElement value)
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
                    throw new FormatException($"Profile '{id}': {name} is {value.GetRawText()}; expected a numeric value.");
                return number;
            }
            double Number(string name) => ReadNumber(name, item.GetProperty(name));
            var settings = new DartSettings(Number("penetratorDiameterFactor"), Number("penetratorLengthInCalibres"),
                Number("penetratorDensity"), Number("velocityEfficiency"), Number("maximumVelocityFactor"),
                Number("maximumVelocity"), Number("penetrationQuality"), Number("fragmentDamageMultiplier"),
                Number("velocityMultiplier"));
            ShellBallistics.ValidateSettings(settings, id);
            var behavior = item.TryGetProperty("behavior",out var mode) ? mode.GetString() ?? "" : id is "apfsds" or "aphe" ? id : "ap";
            double Option(string key,double fallback) => item.TryGetProperty(key,out var value) ? ReadNumber(key, value) : fallback;
            string TextOption(string key,string fallback) => item.TryGetProperty(key,out var value) ? value.ValueKind==JsonValueKind.String ? value.GetString()! : "<invalid type>" : fallback;
            var profile = new ShellProfile(id,label,settings,behavior,
                Option("chemicalPenetrationMm",0),Option("nativeExplosivePower",0),
                Option("spallMultiplier",1),Option("coneHalfAngleDegrees",90),Option("explosionScale",1),Option("secondPlatePenetrationFactor",behavior=="hesh"?.1:.15),Option("airGapLossPerCalibre",behavior=="hesh"?12:.35),Option("referenceCalibreMm",100),
                ShellBalance.IsAtgm(behavior) ? new AtgmSettings(Option("flightSpeed",200),Option("maxTurnRate",20),Option("maximumFlightTime",25),Option("guidanceDelay",.25),
                    TextOption("guidanceMode","sight"), item.TryGetProperty("launchSpeed",out var launch) ? ReadNumber("launchSpeed",launch) : null,
                    TextOption("launchSpeedMode","fixed"),Option("launchSpeedMultiplier",1),Option("acceleration",0),Option("motorDelay",0),Option("motorBurnTime",0),Option("coastDeceleration",0)) : null);
            ShellPayload.Validate(profile);
            result.Add(profile);
        }
        return result;
    }

    private static void CheckFields(JsonElement item, HashSet<string> allowed, HashSet<string>? optional = null)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new FormatException("Expected a JSON object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
            if ((!allowed.Contains(property.Name) && optional?.Contains(property.Name) != true) || !seen.Add(property.Name))
                throw new FormatException($"Unknown or duplicate shell setting: {property.Name}.");
        if (!allowed.IsSubsetOf(seen)) throw new FormatException("Missing required shell profile settings.");
    }

    internal static string CreateDefault(DartSettings settings) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        profiles = new[] { new
        {
            id = LegacyDart, label = "APFSDS (beta)",
            penetratorDiameterFactor = settings.DiameterRatio,
            penetratorLengthInCalibres = settings.LengthInCalibres,
            penetratorDensity = settings.Density,
            velocityEfficiency = settings.VelocityEfficiency,
            velocityMultiplier = settings.VelocityMultiplier,
            maximumVelocityFactor = settings.MaxVelocityFactor,
            maximumVelocity = settings.MaxVelocity,
            penetrationQuality = settings.PenetrationQuality,
            fragmentDamageMultiplier = settings.DamageMultiplier
        }, new
        {
            id = "aphe", label = "APHE (enhanced spall)",
            penetratorDiameterFactor = .9, penetratorLengthInCalibres = 2d,
            penetratorDensity = 7800d, velocityEfficiency = .1,
            velocityMultiplier = 1d, maximumVelocityFactor = 1d,
            maximumVelocity = 2200d, penetrationQuality = .65,
            fragmentDamageMultiplier = 1d
        } }
    }, new JsonSerializerOptions { WriteIndented = true });

    internal static string Resolve(string? id, bool legacyEnabled, IReadOnlyList<ShellProfile> profiles)
    {
        var requested = id ?? (legacyEnabled ? LegacyDart : Vanilla);
        if(requested==LegacyDart && !profiles.Any(p=>p.Id==LegacyDart) && profiles.Any(p=>p.Id=="apfsds_long"))return "apfsds_long";
        return requested == Vanilla || profiles.Any(p => p.Id == requested) ? requested : Vanilla;
    }
}
