namespace SprocketShellSelector;

// Gameplay scaling around a 100 mm reference gun; not real explosive/jet physics.
internal static class ShellBalance
{
    internal static double Ratio(double calibreMm)
    {
        if(!double.IsFinite(calibreMm)||calibreMm<=0) throw new ArgumentOutOfRangeException(nameof(calibreMm));
        return calibreMm/100;
    }
    internal static double ChemicalPenetration(ShellProfile p,double calibreMm) => Math.Clamp(p.ChemicalPenetrationMm*Ratio(calibreMm),1,2000);
    internal static double BlastPower(ShellProfile p,double calibreMm) => Math.Clamp(p.NativeExplosivePower*Math.Pow(Ratio(calibreMm),3),.1,500);
    internal static double SecondPlateBudget(ShellProfile p,double calibreMm,double remainingMm) => Math.Min(remainingMm,ChemicalPenetration(p,calibreMm)*p.SecondPlatePenetrationFactor);
    internal static int FragmentTarget(string behavior,double calibreMm) => (int)Math.Clamp(Math.Round((behavior=="heat"?40:48)*Math.Pow(Ratio(calibreMm),1.5)),4,32);
    internal static double FragmentVolume(string behavior,double calibreMm)
    {
        var diameter=calibreMm*.001;
        return SpallBalance.ReferenceApVolume(diameter,diameter)*(behavior=="heat"?4:behavior=="hesh"?16:10);
    }
    internal static double RodCone(DartSettings s) => Math.Clamp(1+(5-s.LengthInCalibres)*.06,.7,1.25);
    internal static DartSettings BallisticSettings(ShellProfile p) => p.Behavior=="apfsds"
        ? p.Settings with {PenetrationQuality=Math.Clamp(p.Settings.PenetrationQuality*Math.Sqrt(p.Settings.LengthInCalibres/5),.1,4)} : p.Settings;
    internal static double FragmentSpeed(string behavior,double calibreMm) => (behavior=="heat"?1100:behavior=="hesh"?700:900)*Math.Clamp(Math.Pow(Ratio(calibreMm),.15),.7,1.3);
    // Payload energy is available even when the native shell barely perforates.
    // This applies to spawned fragments only; never restore the chemical parent budget.
    internal static double PayloadFragmentSpeed(string behavior,double calibreMm,double nativeSpeed)
    {
        var target=FragmentSpeed(behavior,calibreMm);
        return double.IsFinite(nativeSpeed)?Math.Clamp(nativeSpeed,target,target*1.25):target;
    }
    internal static double Visual(double scale,double calibreMm) => scale*Math.Clamp(Math.Pow(Ratio(calibreMm),1d/3),.6,2);
}

// Only a solid -> air -> solid transition counts as spaced armour.
internal sealed class ChemicalLayers
{
    private bool seenSolid, seenGap;
    internal bool Degraded { get; private set; }
    internal bool Observe(bool solid)
    {
        if(!solid){if(seenSolid)seenGap=true;return false;}
        if(seenSolid && seenGap)Degraded=true;
        seenSolid=true;
        return Degraded;
    }
}
