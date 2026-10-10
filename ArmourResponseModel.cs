namespace SprocketShellSelector;

// Gameplay calibration, additional to native passive resistance. No trajectory rotation.
internal sealed class ArmourShotState
{
    internal double Disturbance;
    internal double AdditionalRetention=1;
    internal readonly HashSet<string> Applied=new(StringComparer.Ordinal);
    internal readonly HashSet<string> Preconditioned=new(StringComparer.Ordinal);
    internal double? LastSteelExitMm;
}
internal readonly record struct EraCell(long Spawn,int Element,int Surface,int X,int Y);
internal sealed class EraCells
{
    private readonly HashSet<EraCell> spent=new();
    internal bool Consume(EraCell cell){lock(spent)return spent.Add(cell);}
    internal void RemoveSpawn(long spawn){lock(spent)spent.RemoveWhere(c=>c.Spawn==spawn);}
    internal void RemoveElement(long spawn,int element){lock(spent)spent.RemoveWhere(c=>c.Spawn==spawn&&c.Element==element);}
    internal bool Contains(EraCell cell){lock(spent)return spent.Contains(cell);}
    internal int Count {get{lock(spent)return spent.Count;}}
}
internal static class ArmourResponseModel
{
    internal static bool ReactiveThreat(ArmourResponse response,string behavior)=>
        response.Kind=="lightEra"?behavior=="heat":response.Kind=="heavyEra"&&behavior is "heat" or "apfsds";
    internal static double AngularWeight(ArmourResponse response,string behavior,double angle)=>
        Weight(response.Kind=="heavyEra"&&behavior=="apfsds"?response.Geometry.KineticAngularCurve??Array.Empty<CurvePoint>():response.Geometry.AngularCurve,angle);
    internal static float ResidualSpeed(float nativeSpeed,float parentSpeedRatio)=>
        float.IsFinite(nativeSpeed)&&nativeSpeed>=0&&float.IsFinite(parentSpeedRatio)?nativeSpeed*Math.Clamp(parentSpeedRatio,0,1):nativeSpeed;
    internal static double Weight(IReadOnlyList<CurvePoint> curve,double input)
    {
        if(!double.IsFinite(input)||curve.Count==0||input<curve[0].Input||input>curve[^1].Input)return 0;
        for(var i=1;i<curve.Count;i++)if(input<=curve[i].Input)
            return curve[i-1].Weight+(curve[i].Weight-curve[i-1].Weight)*(input-curve[i-1].Input)/(curve[i].Input-curve[i-1].Input);
        return curve[^1].Weight;
    }
    internal static bool PerforatedSteel(ArmourShotState shot,RodPreconditioning p,string material,double thickness,double angle,double exitMm,string hitKey)
    {
        if(!p.SteelIds.Contains(material)||!double.IsFinite(thickness)||!double.IsFinite(angle)||!double.IsFinite(exitMm)||
            thickness<p.MinimumThickness||angle<p.MinimumAngle||angle>p.MaximumAngle||p.FullAngle<=p.MinimumAngle||!shot.Preconditioned.Add(hitKey))return false;
        shot.Disturbance=Math.Min(p.TotalCap,shot.Disturbance+p.PerLayerCap*Math.Clamp((angle-p.MinimumAngle)/(p.FullAngle-p.MinimumAngle),0,1)*Math.Clamp(thickness/p.FullThickness,0,1));
        shot.LastSteelExitMm=exitMm;
        return true;
    }
    internal static double Apply(ArmourResponseCatalogue catalogue,ArmourResponse response,ArmourShotState shot,
        string hitKey,string behavior,bool original,bool coldwar,double thickness,double angle,double entryMm,bool intactCell)
    {
        if(!catalogue.Enabled||!original||!coldwar||!double.IsFinite(thickness)||!double.IsFinite(angle)||
            thickness<response.Geometry.MinNormalThicknessMm||thickness>response.Geometry.MaxNormalThicknessMm)return 1;
        if(!shot.Applied.Add(hitKey))return 1;
        var weight=AngularWeight(response,behavior,angle);
        if(response.Geometry.Mode=="resolvedLayers")
        {
            if(shot.LastSteelExitMm is not {} exit)return 1;
            var gap=entryMm-exit;
            if(gap<response.Geometry.MinMeasuredGapMm||gap>response.Geometry.MaxMeasuredGapMm)return 1;
            weight*=Weight(response.Geometry.MeasuredGapCurve,gap);
        }
        double retention;
        if(response.Kind=="heavyEra"&&!intactCell)return 1;
        if(behavior=="heat")retention=response.Kind=="lightEra"&&!intactCell?1:response.Calibration.HeatRetention;
        else if(behavior=="apfsds")retention=response.Kind=="lightEra"?1:
            response.Calibration.IntactRodRetention-shot.Disturbance*(response.Calibration.IntactRodRetention-response.Calibration.DisturbedRodRetention);
        else return 1;
        var cap=behavior=="heat"?catalogue.MaximumAdditionalHeatLoss:catalogue.MaximumAdditionalKineticLoss;
        var next=Math.Max(1-cap,shot.AdditionalRetention*(1-weight*(1-retention)));
        var factor=next/shot.AdditionalRetention;
        shot.AdditionalRetention=next;
        return factor;
    }
}
