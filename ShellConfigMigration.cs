using System.Globalization;
namespace SprocketShellSelector;
// Read legacy files only. The destination's existence is the one-time migration marker.
internal static class ShellConfigMigration
{
    internal static void EnsureProfileFile(string destination, string legacyJson, string legacyCfg, DartSettings fallback)
    {
        if (File.Exists(destination)) return;
        string json;
        if (File.Exists(legacyJson)) json = File.ReadAllText(legacyJson);
        else
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            var inSection = false;
            if (File.Exists(legacyCfg)) foreach (var raw in File.ReadLines(legacyCfg))
            {
                var line = raw.Trim();
                if (line.StartsWith('[')) { inSection = line == "[APFSDS Beta]"; continue; }
                if (!inSection || line.StartsWith('#')) continue;
                var pair = line.Split('=', 2);
                if (pair.Length == 2 && double.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    values[pair[0].Trim()] = number;
            }
            double Get(string key, double value) => values.TryGetValue(key, out var found) ? found : value;
            json = ShellProfiles.CreateDefault(new(Get("Diameter ratio", fallback.DiameterRatio),
                Get("Length in calibres", fallback.LengthInCalibres), Get("Density", fallback.Density),
                Get("Velocity efficiency", fallback.VelocityEfficiency), Get("Maximum velocity factor", fallback.MaxVelocityFactor),
                Get("Maximum velocity", fallback.MaxVelocity), Get("Penetration quality", fallback.PenetrationQuality),
                Get("Post penetration damage", fallback.DamageMultiplier), fallback.VelocityMultiplier));
        }
        _ = ShellProfiles.Parse(json); // Invalid legacy data is never silently replaced.
        using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }
}
