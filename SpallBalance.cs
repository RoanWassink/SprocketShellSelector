using System.Text.Json;
namespace SprocketShellSelector;
internal sealed class ApheFuse
{
    private readonly HashSet<(IntPtr,int)> segments = new();
    internal double AccumulatedRhaMm {get;private set;}
    internal bool Traverse(IntPtr simulation,int segment,double rhaMm,SpallSettings settings)
    {
        if(!double.IsFinite(rhaMm) || rhaMm<0) throw new ArgumentOutOfRangeException(nameof(rhaMm));
        if(segments.Add((simulation,segment))) AccumulatedRhaMm+=rhaMm;
        return SpallBalance.FuseArmed(AccumulatedRhaMm,settings);
    }
}
internal sealed record SpallSettings(double ConeMultiplier = .3, double ThinCalibres = .15,
    double MiddleCalibres = .75, double ThickCalibres = 1.5, double ThinRatio = .08,
    double MiddleRatio = 1, double ThickRatio = 1.25, double CurveExponent = 3,
    int ApheFragmentCount = 96, double ApheFragmentMass = 2.4, double ApheFragmentSpeed = 650,
    int ApheFragmentK = 8000, double RemainingPenetrationExponent = 1,
    double ApheFuseRhaMm = 25, double ApheFuseDelayMilliseconds = .5,
    double ConeEnergyWidening = .15);
internal static class SpallBalance
{
    internal static void Validate(SpallSettings s)
    {
        void Range(double x, double min, double max) { if (!double.IsFinite(x) || x < min || x > max) throw new FormatException("Invalid spall setting."); }
        Range(s.ConeMultiplier,.01,1); Range(s.ThinCalibres,.01,4); Range(s.MiddleCalibres,.02,8); Range(s.ThickCalibres,.03,12);
        if (!(s.ThinCalibres < s.MiddleCalibres && s.MiddleCalibres < s.ThickCalibres)) throw new FormatException("Spall thickness anchors must increase.");
        Range(s.ThinRatio,.001,1); Range(s.MiddleRatio,.01,3); Range(s.ThickRatio,.01,5); Range(s.CurveExponent,1,8);
        if (!(s.ThinRatio <= s.MiddleRatio && s.MiddleRatio <= s.ThickRatio)) throw new FormatException("Spall ratios must increase.");
        Range(s.ApheFragmentCount,1,256); Range(s.ApheFragmentMass,.001,10); Range(s.ApheFragmentSpeed,10,1500); Range(s.ApheFragmentK,4000,65535);
        Range(s.RemainingPenetrationExponent,.25,4);
        Range(s.ApheFuseRhaMm,1,200); Range(s.ApheFuseDelayMilliseconds,0,5); Range(s.ConeEnergyWidening,0,.5);
    }
    internal static double ConeFactor(double remainingEnergyFraction, SpallSettings s)
    {
        if(!double.IsFinite(remainingEnergyFraction) || remainingEnergyFraction<0) throw new ArgumentOutOfRangeException(nameof(remainingEnergyFraction));
        return s.ConeMultiplier*(1+s.ConeEnergyWidening*Math.Clamp(remainingEnergyFraction,0,1));
    }
    internal static bool FuseArmed(double accumulatedRhaMm, SpallSettings s) => accumulatedRhaMm>=s.ApheFuseRhaMm;
    internal static double RemainingPenetrationRatio(double remainingFraction, SpallSettings s)
    {
        if (!double.IsFinite(remainingFraction) || remainingFraction < 0) throw new ArgumentOutOfRangeException(nameof(remainingFraction));
        return s.ThinRatio + (s.ThickRatio-s.ThinRatio)*Math.Pow(1-Math.Clamp(remainingFraction,0,1),s.RemainingPenetrationExponent);
    }
    internal static double ThicknessRatio(double pathCalibres, SpallSettings s)
    {
        if (!double.IsFinite(pathCalibres) || pathCalibres < 0) throw new ArgumentOutOfRangeException(nameof(pathCalibres));
        if (pathCalibres <= s.ThinCalibres) return s.ThinRatio;
        if (pathCalibres >= s.ThickCalibres) return s.ThickRatio;
        if (pathCalibres <= s.MiddleCalibres)
            return s.ThinRatio + (s.MiddleRatio-s.ThinRatio)*Math.Pow((pathCalibres-s.ThinCalibres)/(s.MiddleCalibres-s.ThinCalibres),s.CurveExponent);
        return s.MiddleRatio + (s.ThickRatio-s.MiddleRatio)*(1-Math.Pow(1-(pathCalibres-s.MiddleCalibres)/(s.ThickCalibres-s.MiddleCalibres),s.CurveExponent));
    }
    // Confirmed native AP volume: clamp(path / projectileDiameter - .04, 0, .26) * diameter * area.
    internal static double ReferenceApVolume(double gunDiameter, double materialPath)
    {
        if (!double.IsFinite(gunDiameter) || gunDiameter <= 0 || !double.IsFinite(materialPath) || materialPath < 0)
            throw new ArgumentOutOfRangeException(nameof(gunDiameter));
        return Math.Clamp(materialPath/gunDiameter-.04,0,.26)*gunDiameter*Math.PI*gunDiameter*gunDiameter*.25;
    }
    internal static (float X,float Y,float Z) Sphere(double azimuthUnit,double polarUnit)
    {
        if(!double.IsFinite(azimuthUnit) || !double.IsFinite(polarUnit) || azimuthUnit<0 || azimuthUnit>1 || polarUnit<0 || polarUnit>1)
            throw new ArgumentOutOfRangeException(nameof(azimuthUnit));
        var z=polarUnit*2-1;
        var phi=azimuthUnit*Math.PI*2;
        var radius=Math.Sqrt(Math.Max(0,1-z*z));
        return ((float)(radius*Math.Cos(phi)),(float)(radius*Math.Sin(phi)),(float)z);
    }
    internal static string ToJson(SpallSettings s) => JsonSerializer.Serialize(s,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true});
    internal static SpallSettings Parse(string json)
    {
        using var doc=JsonDocument.Parse(json);
        var root=doc.RootElement;
        var expected=new HashSet<string>(StringComparer.Ordinal){"coneMultiplier","thinCalibres","middleCalibres","thickCalibres","thinRatio","middleRatio","thickRatio","curveExponent","apheFragmentCount","apheFragmentMass","apheFragmentSpeed","apheFragmentK"};
        if(root.ValueKind != JsonValueKind.Object) throw new FormatException("Expected spall settings object.");
        var optional=new HashSet<string>(StringComparer.Ordinal){"remainingPenetrationExponent","apheFuseRhaMm","apheFuseDelayMilliseconds","coneEnergyWidening"};
        foreach(var p in root.EnumerateObject()) if(!expected.Remove(p.Name) && !optional.Remove(p.Name)) throw new FormatException("Unknown or duplicate spall setting: "+p.Name);
        if(expected.Count != 0) throw new FormatException("Missing spall settings.");
        var s=JsonSerializer.Deserialize<SpallSettings>(json,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})!;
        Validate(s); return s;
    }
}

