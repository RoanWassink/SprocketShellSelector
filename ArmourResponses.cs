using System.Text.Json;

namespace SprocketShellSelector;

internal sealed record ResponseMaterial(double Density, double RhaFactor, double SpallFactor, double RequestedCostMultiplier);
internal sealed record ResponseCalibration(double HeatRetention, double IntactRodRetention, double DisturbedRodRetention, double DisturbanceContribution);
internal sealed record CurvePoint(double Input, double Weight);
internal sealed record ResponseGeometry(string Mode, double MinNormalThicknessMm, double MaxNormalThicknessMm,
    double MinMeasuredGapMm, double MaxMeasuredGapMm, IReadOnlyList<CurvePoint> AngularCurve, IReadOnlyList<CurvePoint> MeasuredGapCurve,
    IReadOnlyList<CurvePoint>? KineticAngularCurve=null);
internal sealed record ArmourResponse(string ResponseId, string Kind, IReadOnlyList<string> CompatibleMaterialIds,
    string MinimumEra, string HistoricalDate, string Status, ResponseMaterial PassiveMaterial,
    ResponseCalibration Calibration, ResponseGeometry Geometry, double CellPitchM);
internal sealed record ArmourResponseCatalogue(bool Enabled, double MaximumAdditionalKineticLoss,
    double MaximumAdditionalHeatLoss, IReadOnlyList<ArmourResponse> Responses, RodPreconditioning Preconditioning);
internal sealed record RodPreconditioning(IReadOnlyList<string> SteelIds,double MinimumThickness,double MinimumAngle,double MaximumAngle,double FullAngle,double FullThickness,double PerLayerCap,double TotalCap);

// Static configuration only. No shell state, impact hooks, cell ledger or effects.
internal static class ArmourResponses
{
    internal static bool PassiveMatches(ArmourResponse response,float density,float rha,float spall)
    {
        static bool Near(float actual,double expected)=>float.IsFinite(actual)&&Math.Abs(actual-expected)<=Math.Max(1e-7,Math.Abs(expected)*1e-6);
        return Near(density,response.PassiveMaterial.Density)&&Near(rha,response.PassiveMaterial.RhaFactor)&&Near(spall,response.PassiveMaterial.SpallFactor);
    }
    internal static ArmourResponseCatalogue Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Fields(root, "schemaVersion", "enabled", "maximumAdditionalKineticLoss", "maximumAdditionalHeatLoss", "preconditioning", "responses");
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new FormatException("Unsupported response schema.");
        var kinetic = Number(root, "maximumAdditionalKineticLoss", 0, .35);
        var heat = Number(root, "maximumAdditionalHeatLoss", 0, .6);
        var pre = root.GetProperty("preconditioning");
        Fields(pre, "steelMaterialIds", "minSteelThicknessMm", "minObliquityDegrees", "maxObliquityDegrees",
            "angleFullDegrees", "thicknessFullMm", "disturbancePerLayerCap", "cumulativeDisturbanceCap");
        _ = Strings(pre.GetProperty("steelMaterialIds"));
        _ = Number(pre, "minSteelThicknessMm", 3, 1000);
        var angleMin = Number(pre, "minObliquityDegrees", 0, 89);
        var angleMax = Number(pre, "maxObliquityDegrees", angleMin, 89);
        _ = Number(pre, "angleFullDegrees", angleMin, angleMax);
        _ = Number(pre, "thicknessFullMm", 3, 1000);
        var perLayer = Number(pre, "disturbancePerLayerCap", 0, .35);
        _ = Number(pre, "cumulativeDisturbanceCap", perLayer, .6);
        var entries = root.GetProperty("responses");
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() is < 1 or > 64)
            throw new FormatException("Expected 1 to 64 armour responses.");
        var result = new List<ArmourResponse>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var materials = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in entries.EnumerateArray())
        {
            Fields(item, "responseId", "kind", "compatibleMaterialIds", "minimumEra", "historicalDate", "status",
                "passiveMaterial", "calibration", "geometry", "era");
            var id = Text(item, "responseId");
            if (!ValidId(id) || !ids.Add(id)) throw new FormatException("Invalid/duplicate responseId.");
            var kind = Text(item, "kind");
            if (kind is not ("glassTextolite" or "nera" or "lightEra" or "heavyEra" or "passiveComposite")) throw new FormatException("Unknown response kind.");
            var compatible = Strings(item.GetProperty("compatibleMaterialIds"));
            foreach (var material in compatible)
                if (!materials.Add(material) || material is "rha" or "sheetMetal") throw new FormatException("Duplicate or protected material binding.");
            if (Text(item, "minimumEra") != "coldwar") throw new FormatException("Candidate responses are ColdWar-only.");
            var historical = Text(item, "historicalDate");
            if (!DateTime.TryParseExact(historical, "yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _)) throw new FormatException("Invalid metadata date.");
            if (Text(item, "status") != "candidate") throw new FormatException("Unsupported response status.");
            var passive = item.GetProperty("passiveMaterial");
            Fields(passive, "density", "rhaFactor", "spallFactor", "requestedCostMultiplier");
            var physical = new ResponseMaterial(Number(passive, "density", 100, 25000), Number(passive, "rhaFactor", .001, 5),
                Number(passive, "spallFactor", 0, 1), Number(passive, "requestedCostMultiplier", 0, 1e12));
            // Price/mass enforcement belongs to the Material Selector producer.
            var cal = item.GetProperty("calibration");
            Fields(cal, "heatRetention", "intactRodRetention", "disturbedRodRetention", "disturbanceContribution");
            var calibration = new ResponseCalibration(Number(cal, "heatRetention", 1-heat, 1),
                Number(cal, "intactRodRetention", 1-kinetic, 1), Number(cal, "disturbedRodRetention", 1-kinetic, 1),
                Number(cal, "disturbanceContribution", 0, .35));
            var geometry = item.GetProperty("geometry");
            var geometryFields=new[]{"mode", "minNormalThicknessMm", "maxNormalThicknessMm", "minMeasuredGapMm", "maxMeasuredGapMm", "angularCurve", "measuredGapCurve"};
            Fields(geometry,kind=="heavyEra"?geometryFields.Append("kineticAngularCurve").ToArray():geometryFields);
            var mode = Text(geometry, "mode");
            if (mode is not ("declaredCassette" or "resolvedLayers")) throw new FormatException("Unknown geometry mode.");
            var min = Number(geometry, "minNormalThicknessMm", .1, 1000);
            var max = Number(geometry, "maxNormalThicknessMm", min, 1000);
            var minGap = Number(geometry, "minMeasuredGapMm", 0, 1000);
            var maxGap = Number(geometry, "maxMeasuredGapMm", minGap, 1000);
            var angular = Curve(geometry.GetProperty("angularCurve"), "angleDegrees", 89.9, false);
            var kineticAngular=kind=="heavyEra"?Curve(geometry.GetProperty("kineticAngularCurve"),"angleDegrees",89.9,false):null;
            var gap = Curve(geometry.GetProperty("measuredGapCurve"), "distanceMm", 1000, mode == "declaredCassette");
            var era = item.GetProperty("era");
            Fields(era, "cellPitchM");
            var pitch = Number(era, "cellPitchM", 0, 10);
            if (kind is "lightEra" or "heavyEra" ? pitch < .01 : pitch != 0)
                throw new FormatException("Invalid ERA cell pitch.");
            result.Add(new(id, kind, compatible, "coldwar", historical, "candidate", physical, calibration,
                new(mode, min, max, minGap, maxGap, angular, gap,kineticAngular), pitch));
        }
        return new(root.GetProperty("enabled").GetBoolean(), kinetic, heat, result, new(Strings(pre.GetProperty("steelMaterialIds")),
            Number(pre,"minSteelThicknessMm",3,1000),angleMin,angleMax,
            Number(pre,"angleFullDegrees",angleMin+.001,angleMax),Number(pre,"thicknessFullMm",3,1000),perLayer,
            Number(pre,"cumulativeDisturbanceCap",perLayer,.6)));
    }

    private static IReadOnlyList<CurvePoint> Curve(JsonElement array, string field, double max, bool allowEmpty)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 16 ||
            array.GetArrayLength() < (allowEmpty ? 0 : 2)) throw new FormatException("Invalid response curve.");
        var points = new List<CurvePoint>();
        foreach (var point in array.EnumerateArray())
        {
            Fields(point, field, "weight");
            var value = Number(point, field, 0, max);
            if (points.Count > 0 && value <= points[^1].Input) throw new FormatException("Unordered response curve.");
            points.Add(new(value, Number(point, "weight", 0, 1)));
        }
        return points;
    }
    private static IReadOnlyList<string> Strings(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 64) throw new FormatException("Invalid material list.");
        var result = new List<string>();
        foreach (var element in array.EnumerateArray())
        {
            var value = element.GetString() ?? "";
            if (!ValidId(value) || result.Contains(value)) throw new FormatException("Invalid material ID.");
            result.Add(value);
        }
        return result;
    }
    private static bool ValidId(string value) => value.Length is > 0 and <= 80 &&
        value.All(c => c <= 127 && (char.IsLetterOrDigit(c) || c is '_' or '-'));
    private static string Text(JsonElement item, string name)
    {
        var value = item.GetProperty(name).GetString() ?? "";
        if (value.Length is < 1 or > 80 || value.Any(char.IsControl)) throw new FormatException("Invalid response text.");
        return value;
    }
    private static double Number(JsonElement item, string name, double min, double max)
    {
        var value = item.GetProperty(name).GetDouble();
        if (!double.IsFinite(value) || value < min || value > max) throw new FormatException("Out-of-range response value: " + name);
        return value;
    }
    private static void Fields(JsonElement item, params string[] expected)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new FormatException("Expected response object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in item.EnumerateObject())
            if (!expected.Contains(field.Name) || !seen.Add(field.Name)) throw new FormatException("Unknown/duplicate response field: " + field.Name);
        if (seen.Count != expected.Length) throw new FormatException("Missing response fields.");
    }

    internal static double ArealMass(double density, double thicknessMm)
    {
        if (!double.IsFinite(density) || density <= 0 || !double.IsFinite(thicknessMm) || thicknessMm < 0) throw new ArgumentOutOfRangeException();
        return density * thicknessMm * .001;
    }
}
