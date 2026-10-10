namespace SprocketShellSelector;

internal static class ShellRecoil
{
    // Native ProjectileRegister returns energy / barrel length as firing impulse.
    // Retain its charge baseline without changing the terminal penetrator proxy.
    internal static double Restore(double current,double projectileMass,double muzzleSpeed,double barrelLength,bool missile)
    {
        if(missile || !double.IsFinite(current)||current<0 || !double.IsFinite(projectileMass)||projectileMass<=0 ||
            !double.IsFinite(muzzleSpeed)||muzzleSpeed<=0 || !double.IsFinite(barrelLength)||barrelLength<=0)return current;
        var baseline=.5*projectileMass*muzzleSpeed*muzzleSpeed/barrelLength;
        return double.IsFinite(baseline)&&baseline<=float.MaxValue ? Math.Max(current,baseline) : current;
    }
}
