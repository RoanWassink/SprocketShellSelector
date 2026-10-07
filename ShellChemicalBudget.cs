namespace SprocketShellSelector;
internal static class ShellChemicalBudget
{
    private static readonly Lazy<ShellProfile> Stock=new(()=>ShellProfiles.Parse(ReleaseProfiles.Defaults()).Single(p=>p.Id=="heat"));
    internal static bool IsStock(ShellProfile p)=>p==Stock.Value;
    internal static double Resolve(ShellProfile p,double calibreMm,double? nativeFactor,bool available=true)
    {
        if(!available)return 0;
        if(!IsStock(p))return ShellBalance.ChemicalPenetration(p,calibreMm);
        return nativeFactor is {} factor&&double.IsFinite(factor)&&factor>0&&factor<=20 ? Math.Clamp(calibreMm*factor,1,2000):0;
    }
    internal static string Description(ShellProfile p,double? nativeFactor)=>IsStock(p)
        ? nativeFactor is {} factor&&double.IsFinite(factor)&&factor>0&&factor<=20 ? FormattableString.Invariant($"Native dated HEAT technology: {factor:0.00} x calibre (gameplay).") : "Native HEAT technology has no valid penetrationPerCalibre; profile unavailable."
        : "Custom chemical profile: configured penetration retained; stock technology calibration exempt.";
}
