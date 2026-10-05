namespace SprocketShellSelector;

// Only the complete, unchanged embedded stock record receives the pack calibration.
// A copied ID, renamed profile or any custom field is deliberately exempt.
internal static class ShellChemicalBudget
{
    private static readonly Lazy<ShellProfile> Stock = new(() =>
        ShellProfiles.Parse(ReleaseProfiles.Defaults()).Single(p => p.Id == "heat"));
    internal static bool IsStock(ShellProfile profile) => profile == Stock.Value;
    internal static double Resolve(ShellProfile profile, double calibreMm, string? firingEra)
    {
        if (!ShellEraPolicy.Allowed(profile, firingEra)) return 0;
        if (!IsStock(profile)) return ShellBalance.ChemicalPenetration(profile, calibreMm);
        return Math.Clamp(calibreMm * (ShellEraPolicy.Rank(firingEra) == 5 ? 4 : 1.2), 1, 2000);
    }
    internal static string Description(ShellProfile profile, string? firingEra) => IsStock(profile)
        ? $"Stock HEAT period calibration: {(ShellEraPolicy.Rank(firingEra) == 5 ? "ColdWar 4.00" : "WWII 1.20")} x calibre (gameplay)."
        : "Custom chemical profile: configured penetration retained; stock period calibration exempt.";
}
