using System.Text.Json;

namespace SprocketShellSelector;

internal sealed record ShellProfile(string Id, string Label, DartSettings Settings);

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
            CheckFields(item, Fields);
            var id = item.GetProperty("id").GetString() ?? "";
            var label = item.GetProperty("label").GetString() ?? "";
            if (id.Length is < 1 or > 40 || id.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')) || !ids.Add(id))
                throw new FormatException($"Invalid, duplicate or reserved profile id: {id}.");
            if (string.IsNullOrWhiteSpace(label) || label.Length > 80 || label.Any(char.IsControl) || !labels.Add(label))
                throw new FormatException($"Invalid or duplicate profile label for {id}.");
            double Number(string name) => item.GetProperty(name).GetDouble();
            var settings = new DartSettings(Number("penetratorDiameterFactor"), Number("penetratorLengthInCalibres"),
                Number("penetratorDensity"), Number("velocityEfficiency"), Number("maximumVelocityFactor"),
                Number("maximumVelocity"), Number("penetrationQuality"), Number("fragmentDamageMultiplier"),
                Number("velocityMultiplier"));
            _ = ShellBallistics.Calculate(120, 800, 1800, settings);
            result.Add(new(id, label, settings));
        }
        return result;
    }

    private static void CheckFields(JsonElement item, HashSet<string> allowed)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new FormatException("Expected a JSON object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
                throw new FormatException($"Unknown or duplicate shell setting: {property.Name}.");
        if (seen.Count != allowed.Count) throw new FormatException("Missing required shell profile settings.");
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
        return requested == Vanilla || profiles.Any(p => p.Id == requested) ? requested : Vanilla;
    }
}

