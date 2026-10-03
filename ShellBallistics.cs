namespace SprocketShellSelector;

// Gameplay defaults; this is a subcalibre AP approximation, not a long-rod model.
internal readonly record struct DartSettings(double DiameterRatio = .22, double LengthInCalibres = 5,
    double Density = 17500, double VelocityEfficiency = .85, double MaxVelocityFactor = 2.2,
    double MaxVelocity = 2200, double PenetrationQuality = .45, double DamageMultiplier = .5,
    double VelocityMultiplier = 1)
{
    public DartSettings() : this(.22, 5, 17500, .85, 2.2, 2200, .45, .5) { }
}

internal readonly record struct DartBallistics(float Diameter, float Length, float Mass,
    float Velocity, ushort PenetratorConstant, float DamageMultiplier);

internal static class ShellBallistics
{
    internal static DartBallistics Calculate(double calibreMm, double vanillaVelocity,
        ushort vanillaK, DartSettings settings)
    {
        if (!double.IsFinite(calibreMm) || calibreMm <= 0 || calibreMm > ushort.MaxValue ||
            !double.IsFinite(vanillaVelocity) || vanillaVelocity <= 0 || vanillaK == 0)
            throw new ArgumentOutOfRangeException(nameof(calibreMm), "Invalid cannon ballistics.");
        static void Range(double value, double min, double max)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid APFSDS balance setting.");
        }
        Range(settings.DiameterRatio, .05, .9);
        Range(settings.LengthInCalibres, .5, 10);
        Range(settings.Density, 1000, 25000);
        Range(settings.VelocityEfficiency, .1, 2);
        Range(settings.VelocityMultiplier, .1, 4);
        Range(settings.MaxVelocityFactor, 1, 4);
        Range(settings.MaxVelocity, 100, 5000);
        Range(settings.PenetrationQuality, .1, 4);
        Range(settings.DamageMultiplier, .01, 1);
        var diameter = calibreMm * .001 * settings.DiameterRatio;
        var length = calibreMm * .001 * settings.LengthInCalibres;
        var mass = Math.PI * diameter * diameter * .25 * length * settings.Density;
        var vanillaMass = 1.59e-5 * calibreMm * calibreMm * calibreMm;
        var velocityFactor = Math.Clamp(Math.Sqrt(vanillaMass / mass) * settings.VelocityEfficiency * settings.VelocityMultiplier,
            1, settings.MaxVelocityFactor);
        var velocity = Math.Min(vanillaVelocity * velocityFactor, settings.MaxVelocity);
        var k = Math.Clamp(Math.Round(vanillaK / Math.Pow(settings.PenetrationQuality, 1 / 1.43)), 1, ushort.MaxValue);
        return new((float)diameter, (float)length, (float)mass, (float)velocity, (ushort)k,
            (float)settings.DamageMultiplier);
    }
}
