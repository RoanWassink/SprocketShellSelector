namespace SprocketShellSelector;
internal static class ShellPayload
{
 internal static void Validate(ShellProfile p)
 {
  if(p.Behavior is not ("ap" or "apfsds" or "aphe" or "he" or "heat" or "hesh" or "atgm" or "atgm_gun"))
   throw new FormatException($"Profile '{p.Id}': behavior is '{p.Behavior}'; expected ap, apfsds, aphe, he, heat, hesh, atgm or atgm_gun.");
  void Range(string field,double n,double min,double max)
  {if(!double.IsFinite(n)||n<min||n>max)throw new FormatException(ShellBallistics.RangeError(p.Id,field,n,min,max));}
  Range("chemicalPenetrationMm",p.ChemicalPenetrationMm,0,2000);
  Range("nativeExplosivePower",p.NativeExplosivePower,0,500);
  Range("spallMultiplier",p.SpallMultiplier,1,12);
  Range("coneHalfAngleDegrees",p.ConeHalfAngleDegrees,1,90);
  Range("explosionScale",p.ExplosionScale,.1,3);
  Range("secondPlatePenetrationFactor",p.SecondPlatePenetrationFactor,.01,1);
  Range("airGapLossPerCalibre",p.AirGapLossPerCalibre,0,100);
  Range("referenceCalibreMm",p.ReferenceCalibreMm,10,500);
  AtgmGuidance.Validate(p);
  p.Wire?.Validate(p.Id);
  if(p.Flight is {} f)
  {
   if(f.Propulsion is not ("ballistic" or "rocket")||f.Guidance is not ("none" or "sight" or "keyboard")||f.Carrier is not ("fullBore" or "sabot" or "launcher" or "gunLaunch"))throw new FormatException($"Profile '{p.Id}': invalid flight module.");
  }
  if(p.NativeTechnologyIds is {} technologies && (technologies.Count>32||technologies.Distinct(StringComparer.Ordinal).Count()!=technologies.Count||technologies.Any(t=>string.IsNullOrWhiteSpace(t)||t.Length>128||t.Any(char.IsControl))))throw new FormatException($"Profile '{p.Id}': invalid nativeTechnologyIds.");
  if(ShellBalance.FlightEnabled(p) && p.Atgm is null)throw new FormatException($"Profile '{p.Id}': flight module requires flight settings.");
  if(p.Behavior is "heat" or "hesh" or "atgm" or "atgm_gun" && p.ChemicalPenetrationMm<=0)
   throw new FormatException($"Profile '{p.Id}': chemicalPenetrationMm is 0; expected >0–2000 for {p.Behavior}.");
  if(p.Behavior=="he" && p.NativeExplosivePower<=0)
   throw new FormatException($"Profile '{p.Id}': nativeExplosivePower is 0; expected >0–500 for he.");
 }
}
