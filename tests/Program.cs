using SprocketShellSelector;
var checks = 0; void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
var defaults = new DartSettings();
var dart120 = ShellBallistics.Calculate(120, 800, 1800, defaults);
Check(Math.Abs(dart120.Diameter - .0264) < 1e-7, "120mm example subcalibre diameter");
Check(Math.Abs(dart120.Length - .6) < 1e-6, "120mm example rod length");
Check(dart120.Mass > 5.74 && dart120.Mass < 5.76, "120mm example cylinder mass");
Check(dart120.Velocity > 1480 && dart120.Velocity < 1490, "120mm example bounded velocity");
Check(dart120.PenetratorConstant > 1800, "quality below one must reduce penetration at equal geometry");
foreach (var calibre in new[] { 10d, 20d, 75d, 90d, 150d, 300d })
{
    var dart = ShellBallistics.Calculate(calibre, 800, 1800, defaults);
    var scale = calibre / 120;
    Check(Math.Abs(dart.Diameter / dart120.Diameter - scale) < 1e-6, "diameter scales with selected calibre");
    Check(Math.Abs(dart.Length / dart120.Length - scale) < 1e-6, "length scales with selected calibre");
    Check(Math.Abs(dart.Mass / dart120.Mass - scale * scale * scale) < 1e-4, "mass scales with volume");
    Check(Math.Abs(dart.Velocity - dart120.Velocity) < .001, "scale preserves uncapped mass-based velocity factor");
}
Check(ShellBallistics.Calculate(120, 2000, 1800, defaults).Velocity == 2200, "absolute speed cap");
Check(ShellBallistics.Calculate(120, 800, 1800, defaults with { MaxVelocityFactor = 1.2 }).Velocity == 960, "relative speed cap");
var heavier = ShellBallistics.Calculate(120, 800, 1800, defaults with { Density = 22000 });
Check(heavier.Mass > dart120.Mass && heavier.Velocity < dart120.Velocity, "heavier rod trades speed for mass");
Check(ShellBallistics.Calculate(120, 800, 1800, defaults with { PenetrationQuality = 1.5 }).PenetratorConstant < dart120.PenetratorConstant,
    "higher quality improves K");
Check(ShellBallistics.Calculate(120, 800, 1800, defaults with { DamageMultiplier = .2 }) with { DamageMultiplier = dart120.DamageMultiplier } == dart120,
    "damage tuning leaves ballistics unchanged");
void Reject(Action work, string message)
{
    var rejected = false;
    try { work(); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, message);
}
foreach (var invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, 70000d })
    Reject(() => ShellBallistics.Calculate(invalid, 800, 1800, defaults), "reject invalid calibre");
foreach (var invalid in new[] { 0d, double.NaN, double.PositiveInfinity })
    Reject(() => ShellBallistics.Calculate(120, invalid, 1800, defaults), "reject invalid velocity");
Reject(() => ShellBallistics.Calculate(120, 800, 0, defaults), "reject zero K");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { DiameterRatio = double.NaN }), "reject NaN setting");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { Density = -1 }), "reject invalid density");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { DamageMultiplier = 2 }), "reject invalid damage scale");
var profileJson = ShellProfiles.CreateDefault(defaults);
var profileList = ShellProfiles.Parse(profileJson);
Check(profileList.Count == 2 && profileList[0].Id == "apfsds", "default profile ID");
Check(profileList[0].Settings == defaults, "JSON preserves complete default settings");
Check(ShellBallistics.Calculate(120, 800, 1800, profileList[0].Settings) == dart120, "profile migration preserves ballistics");
Check(ShellProfiles.Resolve(null, true, profileList) == "apfsds", "old selected save migrates");
Check(ShellProfiles.Resolve(null, false, profileList) == "vanilla", "old unselected save stays vanilla");
Check(ShellProfiles.Resolve("vanilla", true, profileList) == "vanilla", "new selection overrides legacy flag");
Check(ShellProfiles.Resolve("removed", true, profileList) == "vanilla", "missing ID falls back to vanilla");
Check(ShellProfiles.Resolve("apfsds", false, profileList) == "apfsds", "new custom selection overrides legacy flag");
var secondJson = profileJson.Replace("apfsds", "apds").Replace("APFSDS (beta)", "APDS experiment");
var reordered = ShellProfiles.Parse(secondJson);
Check(ShellProfiles.Resolve("apds", false, reordered) == "apds", "selection is by stable ID");
var slower = defaults with { VelocityMultiplier = .8 };
Check(ShellBallistics.Calculate(120, 800, 1800, slower).Velocity < dart120.Velocity, "explicit velocity multiplier works");
Reject(() => ShellBallistics.Calculate(120, 800, 1800, defaults with { VelocityMultiplier = double.NaN }), "invalid velocity multiplier");
void BadJson(string json, string message)
{
    var rejected = false;
    try { ShellProfiles.Parse(json); }
    catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or ArgumentOutOfRangeException or InvalidOperationException)
    { rejected = true; }
    Check(rejected, message);
}
BadJson(profileJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), "unsupported schema");
BadJson(profileJson.Replace("\"apfsds\"", "\"vanilla\""), "reserved ID");
BadJson(profileJson.Replace("\"apfsds\"", "\"AP FSDS\""), "invalid ID");
BadJson(profileJson.Replace("\"penetratorDensity\": 17500", "\"penetratorDensity\": -1"), "invalid profile density");
BadJson(profileJson.Replace("\"penetratorDensity\"", "\"densityTypo\""), "unknown field rejected");
BadJson(profileJson.Replace("\"penetratorDensity\": 17500,", ""), "missing field rejected");
BadJson(profileJson.Replace("\"penetratorDensity\": 17500,", "\"penetratorDensity\": 17500, \"penetratorDensity\": 17500,"), "duplicate field rejected");
var objectJson = System.Text.Json.JsonDocument.Parse(profileJson).RootElement.GetProperty("profiles")[0].GetRawText();
BadJson("{\"schemaVersion\":1,\"profiles\":[" + objectJson + "," + objectJson + "]}", "duplicate profile IDs rejected");
BadJson("{\"schemaVersion\":1,\"profiles\":[]}", "empty profile list rejected");
var twoProfiles = ShellProfiles.Parse("{\"schemaVersion\":1,\"profiles\":[" + objectJson + "," +
    objectJson.Replace("apfsds", "apds").Replace("APFSDS (beta)", "APDS experiment") + "]}");
Check(twoProfiles.Count == 2 && ShellProfiles.Resolve("apds", false, twoProfiles) == "apds", "multiple profiles supported");
Console.WriteLine($"PASS: {checks} shell ballistics and profile checks.");

var temp = Path.Combine(Path.GetTempPath(), "shell-selector-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
try
{
    var legacy = Path.Combine(temp, "legacy.json");
    var cfg = Path.Combine(temp, "legacy.cfg");
    var dest = Path.Combine(temp, "new.json");
    File.WriteAllText(legacy, profileJson);
    File.WriteAllText(cfg, "[APFSDS Beta]\nDensity = 22000\n");
    ShellConfigMigration.EnsureProfileFile(dest, legacy, cfg, defaults);
    Check(File.ReadAllText(dest) == profileJson, "legacy JSON imported exactly");
    File.WriteAllText(legacy, secondJson);
    ShellConfigMigration.EnsureProfileFile(dest, legacy, cfg, defaults);
    Check(File.ReadAllText(dest) == profileJson, "migration runs once");
    Check(File.ReadAllText(legacy) == secondJson, "legacy JSON unchanged");
    var numericDest = Path.Combine(temp, "numeric.json");
    ShellConfigMigration.EnsureProfileFile(numericDest, Path.Combine(temp,"missing.json"), cfg, defaults);
    Check(ShellProfiles.Parse(File.ReadAllText(numericDest))[0].Settings.Density == 22000, "legacy numeric CFG migration");
    Check(File.ReadAllText(cfg) == "[APFSDS Beta]\nDensity = 22000\n", "material CFG unchanged");
    File.WriteAllText(legacy, "{}");
    var invalidDest = Path.Combine(temp, "invalid.json");
    BadJson(File.ReadAllText(legacy), "invalid migration source rejected");
    try { ShellConfigMigration.EnsureProfileFile(invalidDest, legacy, cfg, defaults); } catch (FormatException) { }
    Check(!File.Exists(invalidDest), "invalid import creates no destination");
}
finally { Directory.Delete(temp, true); }
Console.WriteLine($"PASS total: {checks} shell checks including migration.");
var spallSettings = new SpallSettings();
SpallBalance.Validate(spallSettings);
Check(SpallBalance.Parse(SpallBalance.ToJson(spallSettings)) == spallSettings, "complete spall JSON round trip");
Check(SpallBalance.ThicknessRatio(.15, spallSettings) == .08, "thin anchor 8 percent AP volume");
Check(SpallBalance.ThicknessRatio(.75, spallSettings) == 1, "middle anchor same AP volume");
Check(SpallBalance.ThicknessRatio(1.5, spallSettings) == 1.25, "thick anchor 125 percent AP volume");
Check(SpallBalance.ThicknessRatio(10, spallSettings) == 1.25, "bounded thick response");
Check(SpallBalance.ReferenceApVolume(.13,.13) == SpallBalance.ReferenceApVolume(.13,.26), "native AP spall fraction saturates");
Check(SpallBalance.ReferenceApVolume(.13,.004) == 0, "native AP below four percent calibre produces no spall");
Check(SpallBalance.ReferenceApVolume(.13,.1) * SpallBalance.ThicknessRatio(.1/.13,spallSettings) > SpallBalance.ReferenceApVolume(.13,.02) * SpallBalance.ThicknessRatio(.02/.13,spallSettings), "thin mass lower than thick mass");
var previousRatio=0d;
var monotonic=true;
for(var i=0;i<=1000;i++) { var ratio=SpallBalance.ThicknessRatio(i*.003,spallSettings); monotonic &= ratio>=previousRatio; previousRatio=ratio; }
Check(monotonic,"continuous thickness curve monotonic over full sweep");
foreach(var bad in new[] {spallSettings with {ConeMultiplier=0},spallSettings with {ThinCalibres=2},spallSettings with {ApheFragmentCount=257},spallSettings with {ApheFragmentMass=double.NaN},spallSettings with {ApheFragmentK=2000}})
{
    var invalid=false; try {SpallBalance.Validate(bad);} catch(FormatException){invalid=true;}
    Check(invalid,"invalid experimental spall setting rejected");
}
var unknown=false; try {SpallBalance.Parse(SpallBalance.ToJson(spallSettings).Replace("coneMultiplier","coneTypo"));} catch(FormatException){unknown=true;}
Check(unknown,"unknown spall field rejected");
var apheSettings=new DartSettings(.9,2,7800,.1,1,2200,.65,1);
var aphe=ShellBallistics.Calculate(130,861,1800,apheSettings);
var aphePen=Math.Pow(Math.Sqrt(aphe.Mass)*aphe.Velocity/(aphe.PenetratorConstant*Math.Pow(aphe.Diameter*1000*.01,.75)),1.43);
var apPen=Math.Pow(Math.Sqrt(1.59e-5*130*130*130)*861/(1800*Math.Pow(130*.01,.75)),1.43);
Check(aphePen<apPen,"APHE experiment lower native penetration than vanilla AP");
Check(aphe.Velocity==861,"APHE does not gain dart muzzle speed");
Console.WriteLine($"PASS total: {checks} shell, migration and spall checks.");
var sumX=0d;var sumY=0d;var sumZ=0d;var unitSphere=true;
for(var i=0;i<2048;i++) {
    var v=SpallBalance.Sphere((i*.6180339887498949)%1,(i+.5)/2048);
    unitSphere &= Math.Abs(v.X*v.X+v.Y*v.Y+v.Z*v.Z-1)<1e-6;
    sumX+=v.X;sumY+=v.Y;sumZ+=v.Z;
}
Check(unitSphere,"spherical fragments keep normalized directions");
Check(Math.Abs(sumX/2048)<.001 && Math.Abs(sumY/2048)<.001 && Math.Abs(sumZ/2048)<.001,"spherical distribution has no preferred hemisphere");
Check(SpallBalance.Sphere(0,0).Z==-1 && SpallBalance.Sphere(0,1).Z==1,"sphere includes forward and backward directions");
var packageConfig=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../outputs/v0.2.0/config"));
if(Directory.Exists(packageConfig)) {
    var packageProfiles=ShellProfiles.Parse(File.ReadAllText(Path.Combine(packageConfig,"nl.roan.sprocket.shellselector.shells.json")));
    Check(packageProfiles.Any(p=>p.Id=="apfsds") && packageProfiles.Any(p=>p.Id=="aphe"),"test package includes both experiments");
    _=SpallBalance.Parse(File.ReadAllText(Path.Combine(packageConfig,"nl.roan.sprocket.shellselector.spall.json")));
    Check(true,"test package spall JSON validates");
}
Console.WriteLine($"FINAL PASS: {checks} checks.");

var oldQuality=ShellBallistics.Calculate(125,890,1800,new DartSettings() with {PenetrationQuality=.75});
var newQuality=ShellBallistics.Calculate(125,890,1800,new DartSettings());
Check(newQuality with {PenetratorConstant=oldQuality.PenetratorConstant} == oldQuality,"quality adjustment changes only K, not geometry/mass/speed");
var relativePen=Math.Pow((double)oldQuality.PenetratorConstant/newQuality.PenetratorConstant,1.43);
Check(Math.Abs(relativePen-.6)<.001,"quality constant reduces native penetration to sixty percent across calibres");
var calibreInvariant=true;
foreach(var calibre in new[]{20d,75d,125d,200d}) {
 var oldK=ShellBallistics.Calculate(calibre,900,1800,new DartSettings() with{PenetrationQuality=.75}).PenetratorConstant;
 var newK=ShellBallistics.Calculate(calibre,900,1800,new DartSettings()).PenetratorConstant;
 calibreInvariant &= oldK==oldQuality.PenetratorConstant && newK==newQuality.PenetratorConstant;
}
Check(calibreInvariant,"balance constant has no special case for 125mm");
Console.WriteLine($"v0.3.0 PASS: {checks} checks.");
Check(SpallBalance.RemainingPenetrationRatio(1,spallSettings)==spallSettings.ThinRatio,"full remaining penetration produces minimum spall");
Check(SpallBalance.RemainingPenetrationRatio(0,spallSettings)==spallSettings.ThickRatio,"exhausted penetration produces maximum spall ratio");
Check(SpallBalance.RemainingPenetrationRatio(2,spallSettings)==spallSettings.ThinRatio,"remaining fraction above one is bounded");
Check(Math.Abs(SpallBalance.RemainingPenetrationRatio(.5,spallSettings)-.665)<1e-12,"half remaining penetration gives broad linear response");
var priorRemainingRatio=double.PositiveInfinity;
var remainingMonotonic=true;
for(var i=0;i<=1000;i++) {
 var ratio=SpallBalance.RemainingPenetrationRatio(i*.001,spallSettings);
 remainingMonotonic &= ratio<=priorRemainingRatio && ratio>=spallSettings.ThinRatio && ratio<=spallSettings.ThickRatio;
 priorRemainingRatio=ratio;
}
Check(remainingMonotonic,"spall decreases monotonically with remaining penetration across full sweep");
Check(SpallBalance.RemainingPenetrationRatio(.4,spallSettings with{ThinCalibres=.1,MiddleCalibres=1,ThickCalibres=2})==SpallBalance.RemainingPenetrationRatio(.4,spallSettings),"remaining model independent of legacy plate thickness anchors");
foreach(var invalidRemaining in new[]{double.NaN,double.PositiveInfinity,-.1}) {
 var rejected=false;try{SpallBalance.RemainingPenetrationRatio(invalidRemaining,spallSettings);}catch(ArgumentOutOfRangeException){rejected=true;}
 Check(rejected,"invalid remaining penetration rejected");
}
var oldSpallJson=SpallBalance.ToJson(spallSettings).Replace(",\n  \"remainingPenetrationExponent\": 1","");
Check(SpallBalance.Parse(oldSpallJson).RemainingPenetrationExponent==1,"existing spall configuration defaults to linear remaining penetration model");
Console.WriteLine($"v0.4.0 PASS: {checks} checks.");
Check(SpallBalance.ConeFactor(0,spallSettings)==.3,"exhausted kinetic energy retains base cone");
Check(Math.Abs(SpallBalance.ConeFactor(.5,spallSettings)-.3225)<1e-12,"half kinetic energy widens cone by seven point five percent");
Check(Math.Abs(SpallBalance.ConeFactor(1,spallSettings)-.345)<1e-12,"full kinetic energy widens cone by fifteen percent");
Check(SpallBalance.ConeFactor(2,spallSettings)==SpallBalance.ConeFactor(1,spallSettings),"cone widening capped at fifteen percent");
var configTemp=Path.Combine(Path.GetTempPath(),"shell-names-"+Guid.NewGuid());
Directory.CreateDirectory(configTemp);
try {
 var previousFile=Path.Combine(configTemp,"nl.roan.sprocket.shellselector.shells.json");
 File.WriteAllText(previousFile,profileJson);
 var currentFile=ShellConfigMigration.EnsureCurrentProfileFile(configTemp,defaults);
 Check(Path.GetFileName(currentFile)=="sprocket.shellselector.shells.json","current profile filename contains no author name");
 Check(File.ReadAllText(currentFile)==profileJson && File.ReadAllText(previousFile)==profileJson,"renaming preserves existing settings and source backup");
 File.WriteAllText(previousFile,secondJson);
 ShellConfigMigration.EnsureCurrentProfileFile(configTemp,defaults);
 Check(File.ReadAllText(currentFile)==profileJson,"new filename remains authoritative after migration");
} finally {Directory.Delete(configTemp,true);}
Console.WriteLine($"v0.5.0 PASS: {checks} checks.");
foreach(var invalidMultiplier in new[]{0d,13d,double.NaN}){
 var rejected=false;try{SpallBalance.Validate(spallSettings with{ApheSpallMultiplier=invalidMultiplier});}catch(FormatException){rejected=true;}
 Check(rejected,"invalid APHE native spall multiplier rejected");
}
var legacyBurstConfig=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../outputs/v0.5.0/config/sprocket.shellselector.spall.json"));
if(File.Exists(legacyBurstConfig)){
 var legacyBurst=SpallBalance.Parse(File.ReadAllText(legacyBurstConfig));
 Check(legacyBurst.ApheSpallMultiplier==4 && legacyBurst.ApheExplosionEffect,"previous fuse config imports new native AP defaults without reset");
}
Console.WriteLine($"v0.6.0 PASS: {checks} checks.");
foreach(var halfAngle in new[]{30d,60d,90d}){
 var bounded=true;var normalized=true;
 for(var i=0;i<2048;i++){
  var point=SpallBalance.Cone((i*.6180339887498949)%1,(i+.5)/2048,halfAngle);
  bounded &= point.Z>=Math.Cos(halfAngle*Math.PI/180)-1e-6 && point.Z<=1;
  normalized &= Math.Abs(point.X*point.X+point.Y*point.Y+point.Z*point.Z-1)<1e-6;
 }
 Check(bounded,"cone stays within requested forward half-angle");
 Check(normalized,"cone directions remain unit length");
}
Check(Math.Abs(SpallBalance.Cone(0,1,90).Z)<1e-6,"180 degree full cone reaches hemisphere boundary without backwards fragments");
foreach(var bad in new[]{new SpallSettings() with{ApheConeHalfAngleDegrees=91},new SpallSettings() with{ApheExplosionScale=0}}){
 var rejected=false;try{SpallBalance.Validate(bad);}catch(FormatException){rejected=true;}
 Check(rejected,"invalid experimental cone or effect scale rejected");
}
Console.WriteLine($"v0.7.0 EXPERIMENT PASS: {checks} checks.");
var incoming=new System.Numerics.Vector3(1,0,0);
var slopeNormal=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(1,1,0));
var downward=PlateDeflection.Bend(incoming,slopeNormal,8,30);
Check(downward.Y<0,"sloping plate bends shot downward without gravity hardcoding");
Check(System.Numerics.Vector3.Dot(downward,slopeNormal)>0,"deflected shot remains directed into plate");
Check(Math.Abs(downward.Length()-1)<1e-6,"deflection preserves unit direction and thus speed");
Check(System.Numerics.Vector3.Distance(downward,PlateDeflection.Bend(incoming,-slopeNormal,8,30))<1e-6,"plate-normal orientation does not change deflection");
Check(PlateDeflection.Bend(incoming,new(1,-1,0),8,30).Y>0,"mirrored slope produces mirrored bend");
Check(PlateDeflection.Bend(incoming,incoming,8,30)==incoming,"normal incidence stays straight");
Check(PlateDeflection.Bend(incoming,slopeNormal,0,30)==incoming,"zero maximum angle disables bend");
var turn=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,1);
Check(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Transform(downward,turn),PlateDeflection.Bend(System.Numerics.Vector3.Transform(incoming,turn),System.Numerics.Vector3.Transform(slopeNormal,turn),8,30))<1e-6,"deflection rotates with plate and incoming shot");
Console.WriteLine($"v0.8.0 EXPERIMENT PASS: {checks} checks.");
var repairDir = Path.Combine(Path.GetTempPath(), "shell-repair-"+Guid.NewGuid());
Directory.CreateDirectory(repairDir);
try {
 var path=Path.Combine(repairDir,"sprocket.shellselector.shells.json");
 var old="{\"schemaVersion\":1,\"profiles\":["+objectJson+"]}";
 File.WriteAllText(path,old);
 Check(ShellConfigMigration.EnsureApheProfile(path),"missing APHE repaired");
 Check(ShellProfiles.Parse(File.ReadAllText(path)).Count==2,"repair adds APHE");
 Check(ShellProfiles.Parse(File.ReadAllText(path))[0].Settings==defaults,"repair preserves APFSDS values");
 Check(File.ReadAllText(path+".pre-aphe-backup")==old,"repair backs up exact original");
 var repaired=File.ReadAllText(path);
 Check(!ShellConfigMigration.EnsureApheProfile(path)&&File.ReadAllText(path)==repaired,"repair is idempotent");
 File.WriteAllText(path,profileJson.Replace("\"penetrationQuality\": 0.65","\"penetrationQuality\": 0.7"));
 var custom=File.ReadAllText(path);
 Check(!ShellConfigMigration.EnsureApheProfile(path)&&File.ReadAllText(path)==custom,"existing APHE preserved byte for byte");
 File.WriteAllText(path,"{}");
 try {ShellConfigMigration.EnsureApheProfile(path);} catch(FormatException) {}
 Check(File.ReadAllText(path)=="{}","invalid configuration not overwritten");
} finally {Directory.Delete(repairDir,true);}
Console.WriteLine($"v0.8.2 PASS: {checks} checks.");
var copyNode=System.Text.Json.Nodes.JsonNode.Parse(profileJson)!;
copyNode["profiles"]![0]!["id"]="long_rod";
copyNode["profiles"]![0]!["label"]="Long rod";
copyNode["profiles"]![0]!["behavior"]="apfsds";
var copied=ShellProfiles.Parse(copyNode.ToJsonString())[0];
Check(copied.Behavior=="apfsds" && copied.Id=="long_rod","APFSDS behavior independent of id");
Check(profileList[0].Behavior=="apfsds" && profileList[1].Behavior=="aphe","old configurations retain dedicated behaviors");
copyNode["profiles"]![0]!["behavior"]="ap";
Check(ShellProfiles.Parse(copyNode.ToJsonString())[0].Behavior=="ap","explicit AP overrides legacy semantics");
foreach(var behavior in new[]{"heat","hesh"}){
 copyNode["profiles"]![0]!["behavior"]=behavior;
 copyNode["profiles"]![0]!["chemicalPenetrationMm"]=350;
 var p=ShellProfiles.Parse(copyNode.ToJsonString())[0];
 Check(p.Behavior==behavior && p.ChemicalPenetrationMm==350,"chemical payload parses");
}
copyNode["profiles"]![0]!["behavior"]="he";
copyNode["profiles"]![0]!["nativeExplosivePower"]=40;
Check(ShellProfiles.Parse(copyNode.ToJsonString())[0].NativeExplosivePower==40,"native HE power parses");
foreach(var invalid in new[]{copied with{Behavior="unknown"},copied with{Behavior="heat"},copied with{Behavior="he"},copied with{ChemicalPenetrationMm=2001},copied with{NativeExplosivePower=501},copied with{ExplosionScale=double.NaN},copied with{ConeHalfAngleDegrees=91}}){
 var rejected=false;try{ShellPayload.Validate(invalid);}catch(FormatException){rejected=true;}
 Check(rejected,"unsupported payload rejected");
}
Console.WriteLine($"v0.9.0 EXPERIMENT PASS: {checks} checks.");
if(args.Length>0){
 var presets=ShellProfiles.Parse(File.ReadAllText(args[0]));
 Check(presets.Count is 5 or 6 or 7 or 8 or 9,"preset file loads");
 Check(presets.Count(p=>p.Behavior=="apfsds")==2,"two APFSDS variants load");
 Check(presets.Any(p=>p.Behavior=="he")&&presets.Any(p=>p.Behavior=="heat")&&presets.Any(p=>p.Behavior=="hesh"),"all payload examples present");
 Console.WriteLine($"PRESETS PASS: {checks} checks.");
}
var heatPreset=copied with{Behavior="heat",ChemicalPenetrationMm=400,NativeExplosivePower=40};
Check(ShellBalance.ChemicalPenetration(heatPreset,75)==300,"HEAT scales penetration with calibre");
Check(ShellBalance.ChemicalPenetration(heatPreset,125)==500,"125mm chemical capacity");
Check(ShellBalance.BlastPower(heatPreset,100)==40 && Math.Abs(ShellBalance.BlastPower(heatPreset,200)-320)<1e-9,"HE power scales with calibre cubed");
Check(ShellBalance.BlastPower(heatPreset,500)==500,"HE power has upper bound");
Check(ShellBalance.FragmentTarget("heat",75)>=12,"75mm HEAT has useful fragment budget");
Check(ShellBalance.FragmentTarget("hesh",300)==32,"large HESH stays within native fragment cap");
Check(Math.Abs(ShellBalance.FragmentVolume("heat",200)/ShellBalance.FragmentVolume("heat",100)-8)<1e-9,"spall volume scales with calibre cubed");
var shortProfile=copied with{Settings=copied.Settings with{LengthInCalibres=3}};
var longProfile=copied with{Settings=copied.Settings with{LengthInCalibres=6}};
Check(ShellBalance.BallisticSettings(shortProfile).PenetrationQuality<copied.Settings.PenetrationQuality,"short rod penetration efficiency lower");
Check(ShellBalance.BallisticSettings(longProfile).PenetrationQuality>copied.Settings.PenetrationQuality,"long rod penetration efficiency higher");
Check(ShellBalance.RodCone(shortProfile.Settings)>ShellBalance.RodCone(longProfile.Settings),"short rod has wider cone than long rod");
Check(ShellBalance.BallisticSettings(profileList[1])==profileList[1].Settings,"APHE geometry not affected by rod modifier");
Check(ShellBalance.Visual(.65,125)<ShellBalance.Visual(1.5,125),"HE visual larger than APHE at equal calibre");
Check(ShellBalance.FragmentSpeed("heat",100)>ShellBalance.FragmentSpeed("hesh",100),"HEAT concentrated fast fragments HESH slower broad fragments");
Console.WriteLine($"v0.9.1 BALANCE PASS: {checks} checks.");
var layers=new ChemicalLayers();
Check(!layers.Observe(false),"initial air does not consume first plate");
Check(!layers.Observe(true),"first solid keeps initial budget");
Check(!layers.Observe(true),"contiguous solid does not count as spaced plate");
Check(!layers.Observe(false),"air gap arms degradation without changing air simulation");
Check(layers.Observe(true)&&layers.Degraded,"second solid after gap degrades budget");
Check(layers.Observe(true),"degraded budget persists through later solids");
Check(ShellBalance.SecondPlateBudget(heatPreset,100,350)==60,"HEAT second plate capped to 15 percent");
Check(ShellBalance.SecondPlateBudget(heatPreset,100,20)==20,"second plate never restores lost penetration");
Check(ShellBalance.SecondPlateBudget(heatPreset,200,700)==120,"second plate capacity scales with calibre");
Check(ShellBalance.FragmentTarget("aphe",75)>=21 && ShellBalance.FragmentTarget("hesh",100)==32,"stronger APHE HESH fragment budgets");
copyNode["profiles"]![0]!["behavior"]="heat";
copyNode["profiles"]![0]!["secondPlatePenetrationFactor"]=.2;
Check(ShellProfiles.Parse(copyNode.ToJsonString())[0].SecondPlatePenetrationFactor==.2,"second plate factor configurable");
foreach(var factor in new[]{0d,1.1,double.NaN}){
 var rejected=false;try{ShellPayload.Validate(heatPreset with{SecondPlatePenetrationFactor=factor});}catch(FormatException){rejected=true;}
 Check(rejected,"invalid layer factor rejected");
}
Console.WriteLine($"v0.9.2 LAYERS PASS: {checks} checks.");
foreach(var behavior in new[]{"aphe","hesh","heat"}){
 var target=ShellBalance.FragmentSpeed(behavior,100);
 Check(ShellBalance.PayloadFragmentSpeed(behavior,100,0)==target,"barely perforating payload retains fragment energy");
 Check(ShellBalance.PayloadFragmentSpeed(behavior,100,5000)==target*1.25,"payload fragment energy bounded");
 Check(ShellBalance.PayloadFragmentSpeed(behavior,100,double.NaN)==target,"invalid native speed has finite fallback");
 Check(ShellBalance.FragmentTarget(behavior,75)>=26,"75mm payload has dense burst");
}
Check(ShellBalance.FragmentVolume("hesh",100)>ShellBalance.FragmentVolume("aphe",100),"HESH heavier broad burst than APHE");
Check(ShellBalance.FragmentVolume("aphe",100)>ShellBalance.FragmentVolume("heat",100),"APHE broad damage HEAT concentrated damage");
Console.WriteLine($"v0.9.3 PAYLOAD PASS: {checks} checks.");
var releaseDefaults=ShellProfiles.Parse(ReleaseProfiles.Defaults());
Check(releaseDefaults.Count==9 && !releaseDefaults.Any(p=>p.Id=="apfsds"),"test release has nine presets without standard dart");
Check(releaseDefaults.Count(p=>p.Behavior=="apfsds")==2,"release includes long and short rods");
Check(ShellProfiles.Resolve("apfsds",false,releaseDefaults)=="apfsds_long","old dart saves map to long rod");
Check(ShellProfiles.Resolve(null,true,releaseDefaults)=="apfsds_long","legacy selected flag maps to long rod");
var upgradeDir=Path.Combine(Path.GetTempPath(),"shell-release-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(upgradeDir);
try{
 var path=Path.Combine(upgradeDir,"profiles.json");File.WriteAllText(path,profileJson);
 Check(ReleaseProfiles.Upgrade(path),"old config upgraded");
 var upgraded=ShellProfiles.Parse(File.ReadAllText(path));
 Check(upgraded.Count==9 && !upgraded.Any(p=>p.Id=="apfsds"),"stock old dart removed while new presets added");
 Check(upgraded.Single(p=>p.Id=="aphe").Settings==profileList[1].Settings,"existing APHE tuning preserved");
 Check(File.ReadAllText(path+".pre-v094-backup")==profileJson,"upgrade backs up exact config");
 Check(!ReleaseProfiles.Upgrade(path),"release upgrade idempotent");
 File.WriteAllText(path,profileJson.Replace("APFSDS (beta)","My custom dart"));ReleaseProfiles.Upgrade(path);
 Check(ShellProfiles.Parse(File.ReadAllText(path)).Any(p=>p.Id=="apfsds"),"named custom old dart retained");
 File.WriteAllText(path,"{}");try{ReleaseProfiles.Upgrade(path);}catch(Exception){}
 Check(File.ReadAllText(path)=="{}","invalid upgrade input preserved");
}finally{Directory.Delete(upgradeDir,true);}
Console.WriteLine($"v0.9.4 RELEASE PASS: {checks} checks.");
var heatGap=heatPreset with{AirGapLossPerCalibre=.35,SecondPlatePenetrationFactor=.15};
var heshGap=heatGap with{Behavior="hesh",AirGapLossPerCalibre=12,SecondPlatePenetrationFactor=.1};
Check(ShellBalance.SpacedRetention(heatGap,100,10,0)==1,"no gap no added HEAT loss");
Check(ShellBalance.SpacedRetention(heatGap,100,10,5)==1,"tiny gap preserves HEAT jet budget");
Check(ShellBalance.SpacedRetention(heatGap,100,10,100)>.8,"thin skirt and modest gap cannot erase HEAT capacity");
Check(ShellBalance.SpacedRetention(heatGap,100,50,100)<ShellBalance.SpacedRetention(heatGap,100,5,100),"thicker front plate disrupts HEAT more");
Check(ShellBalance.SpacedRetention(heatGap,100,10,500)<ShellBalance.SpacedRetention(heatGap,100,10,100),"longer gap reduces HEAT retention");
Check(ShellBalance.SpacedRetention(heshGap,100,10,50)<.11,"separated HESH strongly decoupled");
Check(ShellBalance.SpacedRetention(heshGap,100,10,0)==1,"contiguous HESH has no added loss");
Check(ShellBalance.SpacedRetention(heshGap,100,10,5)<ShellBalance.SpacedRetention(heatGap,100,10,5),"HESH more sensitive to small real gap");
Check(Math.Abs(ShellBalance.SpacedRetention(heatGap,100,10,100)-ShellBalance.SpacedRetention(heatGap,200,20,200))<1e-10,"relative plate and gap scale with calibre");
Check(ShellBalance.SpacedRetention(heatGap with{AirGapLossPerCalibre=0},100,10,500)==1,"per-shell zero sensitivity disables additional loss");
var distanceLayers=new ChemicalLayers();
Check(distanceLayers.ObserveBlock(false,1000)==null,"initial free flight excluded");
Check(distanceLayers.ObserveBlock(true,4)==null,"first solid no degradation");
Check(distanceLayers.ObserveBlock(true,6)==null,"contiguous solid thickness accumulates");
Check(distanceLayers.ObserveBlock(false,20)==null && distanceLayers.ObserveBlock(false,30)==null,"gap segments accumulate without loss in air");
var measuredGap=distanceLayers.ObserveBlock(true,40);
Check(measuredGap is {} mg && mg.PlateRhaMm==10 && mg.GapMm==50,"next solid reports exact preceding plate and gap once");
Check(distanceLayers.ObserveBlock(true,5)==null,"same solid never repeats previous gap penalty");
distanceLayers.ObserveBlock(false,100);
Check(distanceLayers.ObserveBlock(true,5) is {} nextGap && nextGap.PlateRhaMm==45 && nextGap.GapMm==100,"later gap tracked independently");
foreach(var behavior in new[]{heatGap,heshGap}){
 double last=1;var bounded=true;
 for(var gap=0;gap<2000;gap++){var value=ShellBalance.SpacedRetention(behavior,100,20,gap);bounded &= value<=last && value>=behavior.SecondPlatePenetrationFactor && value<=1;last=value;}
 Check(bounded,"gap curve monotonic bounded and cannot restore penetration");
}
Console.WriteLine($"v0.9.6 DISTANCE PASS: {checks} checks.");
Check(!ShellBalance.SuppressPayloadBurst("heat",true,true),"HEAT original can spall again after a later perforation");
Check(ShellBalance.SuppressPayloadBurst("heat",false,true),"HEAT secondary fragments do not trigger repeated payload bursts");
Check(ShellBalance.SuppressPayloadBurst("hesh",true,true),"HESH retains single payload burst");
Check(ShellBalance.SuppressPayloadBurst("aphe",true,true),"APHE retains single payload burst");
Check(!ShellBalance.SuppressPayloadBurst("heat",true,false),"first HEAT burst allowed");
Console.WriteLine($"v0.9.7 MULTIPLATE PASS: {checks} checks.");

// A valid APFSDS entry must not be blamed for another behavior's invalid settings.
var earlyHeatNode = System.Text.Json.Nodes.JsonNode.Parse(objectJson)!;
earlyHeatNode["id"] = "early_heat";
earlyHeatNode["label"] = "Early HEAT";
earlyHeatNode["behavior"] = "heat";
earlyHeatNode["chemicalPenetrationMm"] = 250;
earlyHeatNode["maximumVelocityFactor"] = .75;
var mixedInvalidJson = "{\"schemaVersion\":1,\"profiles\":[" + objectJson + "," + earlyHeatNode.ToJsonString() + "]}";
var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
try
{
 System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
 try { ShellProfiles.Parse(mixedInvalidJson); Check(false,"invalid HEAT setting rejected"); }
 catch(ArgumentOutOfRangeException ex)
 {
  Check(ex.Message.StartsWith("Profile 'early_heat': maximumVelocityFactor is 0.75; expected 1–4"),"diagnostic identifies HEAT profile, JSON field, value and inclusive limits using invariant numbers");
  Check(ex.ParamName=="maximumVelocityFactor" && !ex.Message.Contains("APFSDS"),"shared validation no longer incorrectly names APFSDS");
 }
} finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
earlyHeatNode["maximumVelocityFactor"] = 1;
Check(ShellProfiles.Parse("{\"schemaVersion\":1,\"profiles\":["+objectJson+","+earlyHeatNode.ToJsonString()+"]}").Count==2,"minimum valid factor still accepts APFSDS and HEAT together");
earlyHeatNode["spallMultiplier"] = 13;
try { ShellProfiles.Parse("{\"schemaVersion\":1,\"profiles\":["+objectJson+","+earlyHeatNode.ToJsonString()+"]}"); Check(false,"invalid payload rejected"); }
catch(FormatException ex) { Check(ex.Message.Contains("Profile 'early_heat': spallMultiplier is 13; expected 1–12"),"payload diagnostic includes profile and field limits"); }
var diagnosticsDir=Path.Combine(Path.GetTempPath(),"shell-diagnostics-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(diagnosticsDir);
try
{
 var path=Path.Combine(diagnosticsDir,"sprocket.shellselector.shells.json");
 File.WriteAllText(path,mixedInvalidJson);
 foreach(var migrate in new Func<string,bool>[] {ShellConfigMigration.EnsureApheProfile,ReleaseProfiles.Upgrade})
 {
  try {migrate(path);Check(false,"migration rejects invalid custom profile");}
  catch(ArgumentOutOfRangeException ex) {Check(ex.Message.Contains("Profile 'early_heat': maximumVelocityFactor is 0.75"),"migration preserves actionable parse error");}
  Check(File.ReadAllText(path)==mixedInvalidJson && Directory.GetFiles(diagnosticsDir).Length==1,"invalid custom config remains exact with no backups or temporary writes");
 }
} finally {Directory.Delete(diagnosticsDir,true);}
Console.WriteLine($"PROFILE DIAGNOSTICS PASS: {checks} checks.");

var konkurs=releaseDefaults.Single(p=>p.Id=="atgm_konkurs_test");
Check(konkurs.Behavior=="atgm" && konkurs.Atgm is {FlightSpeed:200,GuidanceMode:"sight"},"Konkurs-like example enables experimental sight guidance");
Check(Math.Abs(ShellBalance.ChemicalPenetration(konkurs,135)-600)<1e-9,"ATGM has 600mm budget at 135mm reference calibre");
Check(Math.Abs(ShellBalance.ChemicalPenetration(konkurs,90)-400)<1e-9,"ATGM remains selectable and chemical penetration scales with calibre");
Check(ShellBalance.Calculate(135,800,1800,konkurs).Velocity==50 && ShellBalance.Calculate(90,100,1800,konkurs).Velocity==50,"ATGM flight speed is independent of calibre and cannon charge");
Check(ShellBalance.ImpactBehavior(konkurs)=="heat","ATGM reuses HEAT impact mechanics without changing its flight behavior");
Check(Math.Abs(ShellBalance.SpacedRetention(konkurs,135,20,250)-ShellBalance.SpacedRetention(konkurs with {Behavior="heat"},135,20,250))<1e-12,"ATGM uses same HEAT air-gap curve");
var flightSettings=konkurs.Atgm!;
var forward=System.Numerics.Vector3.UnitZ;
var offAxis=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(1,0,1));
var guided=AtgmGuidance.Step(forward,offAxis,flightSettings,.02);
var missileTurn=Math.Acos(Math.Clamp(System.Numerics.Vector3.Dot(forward,System.Numerics.Vector3.Normalize(guided)),-1,1))*180/Math.PI;
Check(Math.Abs(guided.Length()-200)<.001 && missileTurn<=.405 && missileTurn>.39,"guidance preserves commanded speed and limits turning to .4 degrees per 20ms tick");
var leftGuided=AtgmGuidance.Step(forward,new(-1,0,1),flightSettings,.02);
Check(Math.Abs(leftGuided.X+guided.X)<1e-5 && Math.Abs(leftGuided.Z-guided.Z)<1e-5,"guidance has mirrored left/right behavior");
var upGuided=AtgmGuidance.Step(forward,new(0,1,1),flightSettings,.02);
Check(upGuided.Y>0 && Math.Abs(upGuided.Y-guided.X)<1e-5,"guidance turns toward upward aim in correct axis");
Check(AtgmGuidance.Step(forward,null,flightSettings,.02)==forward*200,"lost scope continues straight powered flight");
Check(AtgmGuidance.Step(forward,-forward,flightSettings,.02)==forward*200,"passed aim point cannot make missile U-turn");
Check(AtgmGuidance.Step(forward,offAxis,flightSettings with {GuidanceMode="none"},.02)==forward*200,"unguided custom missile mode remains straight");
Check(AtgmGuidance.Step(forward,offAxis,flightSettings with {MaxTurnRate=0},.02)==forward*200,"zero turn rate prevents steering");
var smallAngle=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(.001f,0,1));
Check(System.Numerics.Vector3.Distance(AtgmGuidance.Step(forward,smallAngle,flightSettings,.02),smallAngle*200)<.001,"small corrections reach aim without overshoot");
var oneSecond=forward;for(var i=0;i<50;i++)oneSecond=System.Numerics.Vector3.Normalize(AtgmGuidance.Step(oneSecond,offAxis,flightSettings,.02));
Check(System.Numerics.Vector3.Distance(oneSecond,System.Numerics.Vector3.Normalize(AtgmGuidance.Step(forward,offAxis,flightSettings,1)))<.001,"turn integration is stable across timestep subdivision");
foreach(var badFlight in new[]{flightSettings with{FlightSpeed=double.NaN},flightSettings with{MaxTurnRate=-1},flightSettings with{MaximumFlightTime=0},flightSettings with{GuidanceDelay=30},flightSettings with{GuidanceMode="lock"}})
{
 var rejected=false;try{ShellPayload.Validate(konkurs with{Atgm=badFlight});}catch(FormatException ex){rejected=ex.Message.Contains("atgm_konkurs_test");}
 Check(rejected,"invalid ATGM settings rejected with profile context");
}
Check(ShellProfiles.Parse(ReleaseProfiles.Defaults()).Single(p=>p.Id=="atgm_konkurs_test")==konkurs,"ATGM optional fields survive JSON parsing");
var upgradeAtgmDir=Path.Combine(Path.GetTempPath(),"atgm-upgrade-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(upgradeAtgmDir);
try
{
 var originalNode=System.Text.Json.Nodes.JsonNode.Parse(ReleaseProfiles.Defaults())!;
 var entries=originalNode["profiles"]!.AsArray();entries.Remove(entries.Single(p=>p!["id"]!.GetValue<string>()=="atgm_konkurs_test"));
 entries.Remove(entries.Single(p=>p!["id"]!.GetValue<string>()=="atgm_mclos_test"));
 entries.Single(p=>p!["id"]!.GetValue<string>()=="heat")!["chemicalPenetrationMm"]=555;
 var path=Path.Combine(upgradeAtgmDir,"shells.json");var original=originalNode.ToJsonString();File.WriteAllText(path,original);
 Check(ReleaseProfiles.Upgrade(path),"existing v0.9.8 config gains ATGM example");
 var upgraded=ShellProfiles.Parse(File.ReadAllText(path));
 Check(upgraded.Count==9 && upgraded.Single(p=>p.Id=="heat").ChemicalPenetrationMm==555,"ATGM migration preserves user HEAT tuning");
 Check(File.ReadAllText(path+".pre-v094-backup")==original && !ReleaseProfiles.Upgrade(path),"ATGM migration backs up exact input and is idempotent");
}finally{Directory.Delete(upgradeAtgmDir,true);}
Console.WriteLine($"ATGM TEST 1 PASS: {checks} checks.");
var distantMissile=new System.Numerics.Vector3(0,0,1500);
var beamTarget=AtgmGuidance.AimAlongRay(System.Numerics.Vector3.Zero,forward,distantMissile,200);
Check(beamTarget==new System.Numerics.Vector3(0,0,1700),"sight-ray guidance remains forward beyond fixed scope convergence distance");
var movingOrigin=new System.Numerics.Vector3(100,0,0);
Check(AtgmGuidance.AimAlongRay(movingOrigin,forward,new(120,0,1000),200)==new System.Numerics.Vector3(100,0,1200),"beam aim respects launcher translation and corrects lateral error");
foreach(var existing in releaseDefaults.Where(p=>!ShellBalance.IsAtgm(p.Behavior)))
 Check(ShellBalance.Calculate(135,800,1800,existing)==ShellBallistics.Calculate(135,800,1800,ShellBalance.BallisticSettings(existing)),"existing profile ballistics are unchanged by ATGM launch path");
Console.WriteLine($"ATGM FINAL PASS: {checks} checks.");
Check(AtgmGuidance.FreshAim(10,9.9),"recent player aim remains usable during reload");
Check(!AtgmGuidance.FreshAim(10,9.5) && !AtgmGuidance.FreshAim(10,11),"stale and future aim samples rejected");
Check(!AtgmGuidance.FreshAim(double.NaN,10),"invalid player clock cannot activate guidance");
Console.WriteLine($"ATGM TEST 2 PASS: {checks} checks.");

var keyboardProfile=releaseDefaults.Single(p=>p.Id=="atgm_mclos_test");
var keyboardSettings=keyboardProfile.Atgm!;
Check(keyboardSettings.GuidanceMode=="keyboard" && flightSettings.GuidanceMode=="sight", "manual and sight guidance are separate presets");
var kr=AtgmGuidance.KeyboardStep(forward,1,0,keyboardSettings,.02);
var kl=AtgmGuidance.KeyboardStep(forward,-1,0,keyboardSettings,.02);
var ku=AtgmGuidance.KeyboardStep(forward,0,1,keyboardSettings,.02);
var kd=AtgmGuidance.KeyboardStep(forward,0,-1,keyboardSettings,.02);
Check(kr.X>0 && kl.X<0 && ku.Y>0 && kd.Y<0,"MCLOS keys turn right/left/up/down correctly");
Check(Math.Abs(kr.X+kl.X)<1e-5 && Math.Abs(ku.Y+kd.Y)<1e-5,"MCLOS opposite commands are symmetric");
Check(AtgmGuidance.KeyboardStep(forward,0,0,keyboardSettings,.02)==forward*200,"no keyboard input preserves last heading");
var diagonal=AtgmGuidance.KeyboardStep(forward,1,1,keyboardSettings,.02);
Check(Math.Abs(diagonal.Length()-200)<.001 && Math.Acos(System.Numerics.Vector3.Dot(forward,System.Numerics.Vector3.Normalize(diagonal)))*180/Math.PI<.405,"diagonal keys cannot double turn rate or change speed");
Check(AtgmGuidance.KeyboardStep(forward,1,1,flightSettings,.02)==forward*200,"keyboard cannot steer sight-guided preset");
var kbSub=forward;for(var i=0;i<50;i++)kbSub=System.Numerics.Vector3.Normalize(AtgmGuidance.KeyboardStep(kbSub,1,0,keyboardSettings,.02));
Check(System.Numerics.Vector3.Distance(kbSub,System.Numerics.Vector3.Normalize(AtgmGuidance.KeyboardStep(forward,1,0,keyboardSettings,1)))<.001,"keyboard yaw stable across timestep subdivision");
Check(float.IsFinite(AtgmGuidance.KeyboardStep(System.Numerics.Vector3.UnitY,0,1,keyboardSettings,.02).Z),"vertical missile heading remains finite");
Console.WriteLine($"ATGM TEST 3 PASS: {checks} checks.");

var motorTestSettings=flightSettings with {MotorDelay=.15,MotorBurnTime=0,CoastDeceleration=0};
Check(AtgmGuidance.InitialSpeed(flightSettings,800)==50,"fixed motor launch speed ignores cannon charge");
var chargeFlight=flightSettings with {LaunchSpeedMode="cannon",LaunchSpeedMultiplier=.2};
Check(AtgmGuidance.InitialSpeed(chargeFlight,400)==80 && AtgmGuidance.InitialSpeed(chargeFlight,800)==160,"cannon mode follows native muzzle velocity and multiplier");
Check(AtgmGuidance.InitialSpeed(chargeFlight,2000)==200 && AtgmGuidance.InitialSpeed(chargeFlight,0)==10,"cannon launch speed has finite lower and cruise caps");
Check(AtgmGuidance.SpeedAtAge(motorTestSettings,50,.1)==50 && AtgmGuidance.SpeedAtAge(motorTestSettings,50,.15)==50,"motor delay preserves launch speed");
Check(Math.Abs(AtgmGuidance.SpeedAtAge(motorTestSettings,50,.65)-125)<1e-9 && AtgmGuidance.SpeedAtAge(motorTestSettings,50,10)==200,"motor acceleration follows elapsed flight time and cruise cap");
Check(AtgmGuidance.SpeedAtAge(motorTestSettings with {Acceleration=0},50,10)==50,"zero acceleration preserves chosen launch speed");
Check(AtgmGuidance.InitialSpeed(new AtgmSettings(FlightSpeed:300),800)==300 && AtgmGuidance.SpeedAtAge(new AtgmSettings(FlightSpeed:300),300,10)==300,"omitted motor fields retain legacy constant speed");
var oldAtgmNode=System.Text.Json.Nodes.JsonNode.Parse(ReleaseProfiles.Defaults())!;
foreach(var node in oldAtgmNode["profiles"]!.AsArray().Where(n=>n!["behavior"]?.GetValue<string>()=="atgm"))
 foreach(var name in new[]{"launchSpeed","launchSpeedMode","launchSpeedMultiplier","acceleration","motorDelay","motorBurnTime","coastDeceleration"})node!.AsObject().Remove(name);
Check(ShellProfiles.Parse(oldAtgmNode.ToJsonString()).Where(p=>p.Behavior=="atgm").All(p=>p.Atgm is {LaunchSpeed:null,Acceleration:0,MotorDelay:0}),"old custom ATGM configs parse without changing flight");
foreach(var invalidMotor in new[]{flightSettings with {LaunchSpeed=201},flightSettings with {LaunchSpeed=double.NaN},flightSettings with {LaunchSpeedMode="charge"},flightSettings with {Acceleration=-1},flightSettings with {LaunchSpeedMultiplier=0},flightSettings with {MotorDelay=6}})
{
 var rejected=false;try{ShellPayload.Validate(konkurs with{Atgm=invalidMotor});}catch(FormatException ex){rejected=ex.Message.Contains(konkurs.Id);}
 Check(rejected,"invalid motor fields rejected with profile context");
}
Console.WriteLine($"ATGM MOTOR PASS: {checks} checks.");

var motorMigrationDir=Path.Combine(Path.GetTempPath(),"motor-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(motorMigrationDir);
try
{
 var path=Path.Combine(motorMigrationDir,"shells.json");var before=oldAtgmNode.ToJsonString();File.WriteAllText(path,before);
 Check(ReleaseProfiles.Upgrade(path),"legacy motor fields are exposed by safe config migration");
 var after=ShellProfiles.Parse(File.ReadAllText(path));
 Check(after.Where(p=>p.Behavior=="atgm").All(p=>AtgmGuidance.InitialSpeed(p.Atgm!,800)==200 && p.Atgm!.Acceleration==0),"motor migration preserves legacy constant-speed behavior");
 Check(File.ReadAllText(path+".pre-v094-backup")==before && !ReleaseProfiles.Upgrade(path),"motor migration has exact backup and is idempotent");
}finally{Directory.Delete(motorMigrationDir,true);}
Console.WriteLine($"RELEASE CANDIDATE PASS: {checks} checks.");

var gunMissile=releaseDefaults.Single(p=>p.Id=="atgm_gun_saclos");
Check(gunMissile.Behavior=="atgm_gun" && gunMissile.Atgm is {GuidanceMode:"sight",LaunchSpeedMode:"cannon"},"gun-launched example parses complete guidance and motor settings");
Check(ShellBalance.ImpactBehavior(gunMissile)=="heat" && ShellBalance.Calculate(135,800,1800,gunMissile).Velocity==160,"gun missile uses chemical impact and charge-dependent launch");
Check(Math.Abs(ShellBalance.ChemicalPenetration(gunMissile,135)-600)<1e-9 && Math.Abs(ShellBalance.SpacedRetention(gunMissile,135,20,250)-ShellBalance.SpacedRetention(konkurs,135,20,250))<1e-12,"gun-launch audio type retains original warhead and airgap mechanics");
Check(ShellProfiles.Parse(ReleaseProfiles.Defaults()).Single(p=>p.Id==gunMissile.Id).Atgm==gunMissile.Atgm,"gun missile settings round-trip from embedded JSON");
Console.WriteLine($"ATGM AUDIO TYPE PASS: {checks} checks.");

foreach(var sound in new[]{"atgm","atgm_gun"})
{
 using var stream=typeof(ShellProfiles).Assembly.GetManifestResourceStream($"ShellSelector.Audio.{sound}.wav")!;
 var decoded=PcmWave.Read(stream);
 Check(decoded.Frequency==48000 && decoded.Channels==1 && decoded.Samples.Length>48000,"bundled launch WAV decodes as 48kHz mono");
 Check(decoded.Samples.All(float.IsFinite) && decoded.Samples.Max(Math.Abs)<.85f,"launch WAV samples are finite and not clipped");
 Check(Math.Abs(decoded.Samples[0])<.001 && Math.Abs(decoded.Samples[^1])<.001,"launch WAV has smooth silence at both endpoints");
}
using(var invalidWave=new MemoryStream(new byte[16]))
{
 var rejected=false;try{PcmWave.Read(invalidWave);}catch(InvalidDataException){rejected=true;}
 Check(rejected,"invalid replacement WAV fails safely before upload");
}
Console.WriteLine($"LAUNCH AUDIO PASS: {checks} checks.");

var flatAi=AtgmGuidance.AiIntercept(new(0,0,1000),System.Numerics.Vector3.Zero,System.Numerics.Vector3.Zero,flightSettings);
Check(flatAi==new System.Numerics.Vector3(0,0,1000),"AI missile aim has no artillery elevation for stationary target");
var movingAi=AtgmGuidance.AiIntercept(new(0,0,1000),new(10,0,0),System.Numerics.Vector3.Zero,flightSettings);
Check(movingAi.X>49 && movingAi.X<51 && movingAi.Y==0,"AI missile lead uses its native estimated velocity without adding shell drop");
var burnout=flightSettings with {MotorDelay=.1,MotorBurnTime=1,Acceleration=150,CoastDeceleration=5};
Check(AtgmGuidance.SpeedAtAge(burnout,50,1.1)==200 && AtgmGuidance.SpeedAtAge(burnout,50,3.1)==190,"finite boost ends then coasting speed decays");
Check(AtgmGuidance.SpeedAtAge(burnout,50,100)==10,"coast model retains a finite minimum speed");
Check(AtgmGuidance.SpeedAtAge(burnout with {MotorBurnTime=0},50,100)==200,"zero burn time preserves old sustained motor semantics");
Console.WriteLine($"AI / FLIGHT PASS: {checks} checks.");
using(var fixture=typeof(ShellProfiles).Assembly.GetManifestResourceStream("ArmourResponse.Fixture")!)
{
 var json=new StreamReader(fixture).ReadToEnd();
 var cfg=ArmourResponses.Parse(json) with {Enabled=true};
 var glass=cfg.Responses.Single(r=>r.Kind=="glassTextolite");
 var nera=cfg.Responses.Single(r=>r.Kind=="nera");
 var era=cfg.Responses.Single(r=>r.Kind=="lightEra");
 var composite=cfg.Responses.Single(r=>r.Kind=="passiveComposite");
 Check(cfg.Responses.Count==4,"canonical catalogue binds four distinct exact material IDs");
 Check(!ArmourResponses.Parse(json).Enabled,"standalone response catalogue defaults disabled");
 Check(ArmourResponses.PassiveMatches(nera,5633.3335f,.55f,.2f),"float passive recipe matches after exact ID resolution");
 Check(!ArmourResponses.PassiveMatches(nera,7850,1,.2f),"custom passive recipe disables additional response");
 var shot=new ArmourShotState();
 Check(ArmourResponseModel.Apply(cfg,glass,shot,"glass0","apfsds",true,true,100,30,25,false)==1,"unconditioned rod has no glass bonus");
 Check(ArmourResponseModel.PerforatedSteel(shot,cfg.Preconditioning,"rha",20,50,0,"steel1"),"traversed oblique steel conditions rod");
 Check(Math.Abs(shot.Disturbance-.35)<1e-12,"steel disturbance follows candidate curve");
 Check(!ArmourResponseModel.PerforatedSteel(shot,cfg.Preconditioning,"rha",20,50,0,"steel1")&&shot.Disturbance==.35,"repeat callback cannot precondition twice");
 Check(Math.Abs(ArmourResponseModel.Apply(cfg,glass,shot,"glass1","apfsds",true,true,100,30,25,false)-.93)<1e-12,"downstream glass applies proportional disturbance loss");
 Check(ArmourResponseModel.Apply(cfg,glass,shot,"glass1","apfsds",true,true,100,30,25,false)==1,"same layer applies once");
 foreach(var gap in new[]{double.NaN,-5,0,4,301})
  Check(ArmourResponseModel.Apply(cfg,glass,shot,"gap"+gap,"apfsds",true,true,100,30,gap,false)==1,"unproven/out-of-band gap cannot grant synergy");
 Check(ArmourResponseModel.Apply(cfg,nera,new(),"n0","heat",true,true,30,0,0,false)==1,"NERA needs obliquity");
 Check(ArmourResponseModel.Apply(cfg,nera,new(),"n45","heat",true,true,30,45,0,false)==.75,"declared NERA works without fictitious outside gap");
 Check(ArmourResponseModel.Apply(cfg,nera,new(),"n85","heat",true,true,30,85,0,false)==1,"outside angular curve no extrapolation benefit");
 foreach(var mode in new[]{"ap","aphe","hesh"})Check(ArmourResponseModel.Apply(cfg,era,new(),mode,mode,true,true,30,30,0,true)==1,"light ERA has no invented anti-AP/HESH bonus");
 Check(ArmourResponseModel.Apply(cfg,era,new(),"rod","apfsds",true,true,30,30,0,true)==1,"light ERA adds no anti-rod loss");
 Check(ArmourResponseModel.Apply(cfg,era,new(),"heat","heat",true,true,30,30,0,true)==era.Calibration.HeatRetention,"intact eligible ERA follows canonical catalogue retention");
 Check(ArmourResponseModel.Apply(cfg,era,new(),"spent","heat",true,true,30,30,0,false)==1,"spent cell retains only native passive resistance");
 Check(ArmourResponseModel.Apply(cfg,era,new(),"frag","heat",false,true,30,30,0,true)==1,"secondary fragment receives no response");
 Check(ArmourResponseModel.Apply(cfg,era,new(),"old","heat",true,false,30,30,0,true)==1,"non-ColdWar target receives no response");
 Check(ArmourResponseModel.Apply(cfg with {Enabled=false},era,new(),"off","heat",true,true,30,30,0,true)==1,"disabled catalogue keeps passive simulation");
 var many=new ArmourShotState();
 for(var i=0;i<20;i++)ArmourResponseModel.Apply(cfg,nera,many,"many"+i,"heat",true,true,30,45,0,false);
 Check(Math.Abs(many.AdditionalRetention-.4)<1e-12,"additional HEAT losses capped cumulatively");
 var disturbed=new ArmourShotState();
 for(var i=0;i<10;i++)ArmourResponseModel.PerforatedSteel(disturbed,cfg.Preconditioning,"sheetMetal",20,60,0,"s"+i);
 Check(disturbed.Disturbance==.6,"disturbance capped across traversed layers");
 for(var i=0;i<30;i++)ArmourResponseModel.Apply(cfg,composite,disturbed,"c"+i,"apfsds",true,true,100,30,0,false);
 Check(Math.Abs(disturbed.AdditionalRetention-.65)<1e-12,"additional rod loss capped cumulatively");
 var cells=new EraCells();var cell=new EraCell(1,2,1,0,0);var preview=new EraCells();
 Check(cells.Consume(cell)&&!cells.Consume(cell),"ERA cell benefits exactly once");
 Check(cells.Consume(cell with {X=1}),"adjacent cell remains intact");
 Check(preview.Consume(cell)&&cells.Count==2,"preview ledger cannot change combat ledger");
 Check(cells.Consume(cell with {Spawn=2}),"new spawn has fresh ERA cells");
 cells.RemoveElement(1,2);Check(cells.Count==1,"element cleanup removes only its consumed cells");
 cells.RemoveSpawn(2);Check(cells.Count==0,"spawn removal clears transient battle damage");
 var concurrent=new EraCells();var winners=0;
 Parallel.For(0,64,_=>{if(concurrent.Consume(cell))Interlocked.Increment(ref winners);});
 Check(winners==1,"concurrent impacts receive only one intact ERA benefit");
 var invalid=json.Replace("\"angleFullDegrees\": 50","\"angleFullDegrees\": 20");
 var rejected=false;try{ArmourResponses.Parse(invalid);}catch(FormatException){rejected=true;}
 Check(rejected,"equal full/min angle is rejected before division");
 var invalidShot=new ArmourShotState();
 Check(!ArmourResponseModel.PerforatedSteel(invalidShot,cfg.Preconditioning with {FullAngle=20},"rha",20,20,0,"invalid")&&double.IsFinite(invalidShot.Disturbance),"model defensively rejects zero slope");
}
Console.WriteLine($"ARMOUR RESPONSE PASS: {checks} checks.");
Check(ArmourResponseModel.ResidualSpeed(800,.5f)==400,"native continuation preserves parent speed reduction after cached KE");
Check(ArmourResponseModel.ResidualSpeed(800,1)==800,"unmodified material leaves native continuation alone");
Check(ArmourResponseModel.ResidualSpeed(800,2)==800,"energy correction cannot accelerate native child");
Check(ArmourResponseModel.ResidualSpeed(800,float.NaN)==800,"invalid cache correction fails closed");
Console.WriteLine($"ARMOUR ENERGY PASS: {checks} checks.");
var nativeProportionalSpeed=(float)Math.Sqrt(2*100*.5/2);
var correctedProportionalSpeed=ArmourResponseModel.ResidualSpeed(nativeProportionalSpeed,.8f);
Check(Math.Abs(.5*2*correctedProportionalSpeed*correctedProportionalSpeed-32)<.00001,"native proportional passive energy loss uses corrected input energy");
var changedMassSpeed=(float)Math.Sqrt(2*100*.5/5);
var correctedMassSpeed=ArmourResponseModel.ResidualSpeed(changedMassSpeed,.8f);
Check(Math.Abs(.5*5*correctedMassSpeed*correctedMassSpeed-32)<.00001,"native changed child mass retains proportional KE correction");
Check(ArmourResponseModel.ResidualSpeed(0,.8f)==0,"native stopped continuation is not revived");
Console.WriteLine($"ARMOUR NATIVE FORMULA PASS: {checks} checks.");
var actualEraNames=new[]{"WWI","Interwar","Earlywar","Midwar","Latewar","Coldwar"};
foreach(var behavior in new[]{"ap","he","aphe","heat","hesh","apfsds","atgm","atgm_gun"})
foreach(var nativeEra in actualEraNames)
{
 var custom=new ShellProfile("renamed_"+behavior,"Custom",defaults,behavior);
 var expected=behavior is "ap" or "he"||nativeEra=="Coldwar"||(behavior is "aphe" or "heat"&&nativeEra is "Earlywar" or "Midwar" or "Latewar");
 Check(ShellEraPolicy.Allowed(custom,nativeEra)==expected,"actual native era x behaviour floor");
 Check(ShellEraPolicy.Effective(custom,nativeEra)!=null==expected,"shared runtime/preview resolver matches selection");
}

var storedAtgm=new ShellProfile("custom_alias","User missile",defaults,"atgm");
var savedId=storedAtgm.Id;
Check(ShellEraPolicy.Effective(storedAtgm,"Latewar")==null&&storedAtgm.Id==savedId,"incompatible imported missile retains saved ID with native fallback");
Check(ShellEraPolicy.Effective(storedAtgm,"Coldwar")?.Id==savedId,"saved profile becomes effective again in ColdWar");
Check(ShellEraPolicy.Effective(storedAtgm,null)==null,"unknown vehicle era fails closed");
Check(!ShellEraPolicy.Allowed(storedAtgm with {MinimumEra="WWI"},"Latewar"),"earlier requested minimum cannot lower ATGM floor");
var strictHe=storedAtgm with {Behavior="he",MinimumEra="Coldwar"};
Check(!ShellEraPolicy.Allowed(strictHe,"Latewar")&&ShellEraPolicy.Allowed(strictHe,"Coldwar"),"optional minimum can restrict an earlier shell further");
Check(!ShellEraPolicy.Allowed(strictHe with {MinimumEra="bogus"},"Coldwar"),"invalid minimum fails closed defensively");
var capturedAvailable=new[]{storedAtgm};
Check(!ShellEraPolicy.Allowed(capturedAvailable[0],"Latewar"),"stale ColdWar dropdown callback rechecks changed owner era");
var eraJson=ReleaseProfiles.Defaults().Replace("\"behavior\": \"he\"","\"behavior\": \"he\", \"minimumEra\": \"Coldwar\"");
Check(ShellProfiles.Parse(eraJson).Single(p=>p.Behavior=="he").MinimumEra=="Coldwar","optional era parses without rewriting legacy fields");
var badEraRejected=false;try{ShellProfiles.Parse(eraJson.Replace("\"minimumEra\": \"Coldwar\"","\"minimumEra\": \"bogus\""));}catch(FormatException){badEraRejected=true;}
Check(badEraRejected,"malformed optional minimum is rejected");
var nativeTypes=new ShellNativeTypeCache<string>();
nativeTypes.Remember((IntPtr)1,"custom missile cached id","actual native APHE id");
var cachedType="custom missile cached id";
Check(nativeTypes.Restore((IntPtr)1,cachedType,ref cachedType)&&cachedType=="actual native APHE id","cached custom projectile ID restores exact native input before blocked launch");
var otherRegisterType="custom missile cached id";
Check(!nativeTypes.Restore((IntPtr)2,otherRegisterType,ref otherRegisterType)&&otherRegisterType=="custom missile cached id","cached mapping is scoped to actual native register");
nativeTypes.Clear((IntPtr)1);cachedType="custom missile cached id";
Check(!nativeTypes.Restore((IntPtr)1,cachedType,ref cachedType),"register release clears stale type fallback mappings");
var shooterProfile=ShellEraPolicy.Effective(storedAtgm,"Coldwar");
Check(shooterProfile!=null&&shooterProfile.Id==savedId,"legitimate launched ColdWar snapshot remains usable against earlier target");
Console.WriteLine($"SHELL ERA REPAIR PASS: {checks} checks.");
nativeTypes.Remember((IntPtr)1,"shared custom id","native AP for cannon A",(IntPtr)10);
nativeTypes.Remember((IntPtr)1,"shared custom id","native APHE for cannon B",(IntPtr)20);
var cannonAType="shared custom id";var cannonBType="shared custom id";
Check(nativeTypes.Restore((IntPtr)1,cannonAType,ref cannonAType,(IntPtr)10)&&cannonAType=="native AP for cannon A","cached fallback retains actual native ammo for first cannon");
Check(nativeTypes.Restore((IntPtr)1,cannonBType,ref cannonBType,(IntPtr)20)&&cannonBType=="native APHE for cannon B","shared custom prototype cannot mix native ammo across cannons");
var unknownSourceType="shared custom id";
Check(!nativeTypes.Restore((IntPtr)1,unknownSourceType,ref unknownSourceType,(IntPtr)30)&&nativeTypes.Known((IntPtr)1,unknownSourceType),"unknown-source cached custom shot can be withheld without inventing AP ID");
Console.WriteLine($"SHELL ERA SOURCE CACHE PASS: {checks} checks.");
nativeTypes.Remember((IntPtr)1,"custom prototype native_AP","AP original",(IntPtr)10);
nativeTypes.Remember((IntPtr)1,"custom prototype native_APHE","APHE original",(IntPtr)10);
var previousAp="custom prototype native_AP";var currentAphe="custom prototype native_APHE";
Check(nativeTypes.Restore((IntPtr)1,previousAp,ref previousAp,(IntPtr)10)&&previousAp=="AP original","same source cached former AP type remains AP after switching to APHE");
Check(nativeTypes.Restore((IntPtr)1,currentAphe,ref currentAphe,(IntPtr)10)&&currentAphe=="APHE original","same source APHE type has independent actual native binding");
Console.WriteLine($"SHELL ERA AMMO CACHE PASS: {checks} checks.");

var stockHeatBudget=ShellProfiles.Parse(ReleaseProfiles.Defaults()).Single(p=>p.Id=="heat");
foreach(var period in new[]{"Earlywar","Midwar","Latewar"})
    Check(Math.Abs(ShellChemicalBudget.Resolve(stockHeatBudget,85,period)-102)<1e-9,"stock WWII85mm HEAT budget102: "+period);
Check(ShellChemicalBudget.Resolve(stockHeatBudget,85,"Coldwar")==340,"stock ColdWar85mm retains340");
foreach(var period in new string?[]{"WWI","Interwar",null,"unknown"})
    Check(ShellChemicalBudget.Resolve(stockHeatBudget,85,period)==0,"unavailable era fails closed");
Check(ShellChemicalBudget.Resolve(stockHeatBudget with{ChemicalPenetrationMm=450},85,"Midwar")==382.5,"custom450 budget retained");
Check(ShellChemicalBudget.Resolve(stockHeatBudget with{Id="custom_heat"},85,"Midwar")==340,"alias stock numbers are exempt");
var changedStockFields=new[]{
stockHeatBudget with{Label="Personal HEAT"},stockHeatBudget with{Behavior="hesh"},
stockHeatBudget with{NativeExplosivePower=1},stockHeatBudget with{SpallMultiplier=2},
stockHeatBudget with{ConeHalfAngleDegrees=10},stockHeatBudget with{ExplosionScale=.8},
stockHeatBudget with{SecondPlatePenetrationFactor=.2},stockHeatBudget with{AirGapLossPerCalibre=.4},
stockHeatBudget with{ReferenceCalibreMm=105},stockHeatBudget with{MinimumEra="Midwar"},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{DiameterRatio=.8}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{LengthInCalibres=3}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{Density=1900}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{VelocityEfficiency=.2}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{VelocityMultiplier=1.1}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{MaxVelocityFactor=1.1}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{MaxVelocity=650}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{PenetrationQuality=.5}},
stockHeatBudget with{Settings=stockHeatBudget.Settings with{DamageMultiplier=.9}}
};
foreach(var modified in changedStockFields) Check(!ShellChemicalBudget.IsStock(modified),"complete fingerprint exempts every changed field");
Check(ShellChemicalBudget.Resolve(stockHeatBudget,105,"Midwar")==126,"generic105mm gameplaybudget126 not Gr39 historical95");
Check(ShellChemicalBudget.Resolve(stockHeatBudget,1000,"Coldwar")==2000,"chemical cap2000 retained");
var launchedColdwarBudget=ShellChemicalBudget.Resolve(stockHeatBudget,85,"Coldwar");
var laterEditorBudget=ShellChemicalBudget.Resolve(stockHeatBudget,85,"Midwar");
Check(launchedColdwarBudget==340&&laterEditorBudget==102,"captured budget remains independent of subsequent era resolve");
foreach(var unchangedChemical in ShellProfiles.Parse(ReleaseProfiles.Defaults()).Where(p=>p.Behavior is "hesh" or "atgm" or "atgm_gun"))
    Check(ShellChemicalBudget.Resolve(unchangedChemical,85,"Coldwar")==ShellBalance.ChemicalPenetration(unchangedChemical,85),"other chemical profiles unchanged "+unchangedChemical.Id);
Check(ShellChemicalBudget.Description(stockHeatBudget,"Midwar").Contains("1.20"),"stock UI names gameplayperiod");
Check(ShellChemicalBudget.Description(stockHeatBudget with{Id="custom_heat"},"Midwar").Contains("exempt"),"custom UI exemption explained");
Console.WriteLine($"HEAT PERIOD REVIEW PASS: {checks} checks.");

Check(!ShellChemicalBudget.IsStock(stockHeatBudget with{Atgm=new AtgmSettings()}),"full fingerprint includes nested Atgm presence");
Console.WriteLine($"FINAL HEAT REVIEW PASS: {checks} checks.");

using(var heavyStream=typeof(ShellProfiles).Assembly.GetManifestResourceStream("ArmourResponse.Fixture")!)
using(var heavyReader=new StreamReader(heavyStream))
{
 var heavyRoot=System.Text.Json.Nodes.JsonNode.Parse(heavyReader.ReadToEnd())!;
 var entries=heavyRoot["responses"]!.AsArray();
 var heavyEntry=System.Text.Json.Nodes.JsonNode.Parse(entries.First(n=>n!["kind"]!.GetValue<string>()=="lightEra")!.ToJsonString())!;
 heavyEntry["responseId"]="heavyEraCassette";heavyEntry["kind"]="heavyEra";
 heavyEntry["compatibleMaterialIds"]=new System.Text.Json.Nodes.JsonArray("cwepHeavyEraCassette");
 heavyEntry["passiveMaterial"]!["density"]=4000;heavyEntry["passiveMaterial"]!["rhaFactor"]=.55;
 heavyEntry["passiveMaterial"]!["spallFactor"]=.25;heavyEntry["passiveMaterial"]!["requestedCostMultiplier"]=10.5;
 heavyEntry["calibration"]!["heatRetention"]=.5;heavyEntry["calibration"]!["intactRodRetention"]=.85;
 heavyEntry["calibration"]!["disturbedRodRetention"]=.85;
 heavyEntry["geometry"]!["minNormalThicknessMm"]=60;heavyEntry["geometry"]!["maxNormalThicknessMm"]=80;
 heavyEntry["geometry"]!["angularCurve"]=System.Text.Json.Nodes.JsonNode.Parse("[{\"angleDegrees\":0,\"weight\":1},{\"angleDegrees\":60,\"weight\":1},{\"angleDegrees\":80,\"weight\":0}]");
 heavyEntry["geometry"]!["kineticAngularCurve"]=System.Text.Json.Nodes.JsonNode.Parse("[{\"angleDegrees\":0,\"weight\":0},{\"angleDegrees\":30,\"weight\":1},{\"angleDegrees\":60,\"weight\":1},{\"angleDegrees\":80,\"weight\":0}]");
 entries.Add(heavyEntry);
 var heavyCfg=ArmourResponses.Parse(heavyRoot.ToJsonString()) with{Enabled=true};
 var heavy=heavyCfg.Responses.Single(r=>r.Kind=="heavyEra");
 double Heavy(string threat,double angle,bool intact=true,double thickness=70,bool original=true,bool cold=true)=>ArmourResponseModel.Apply(heavyCfg,heavy,new(),"heavy",threat,original,cold,thickness,angle,0,intact);
 Check(Heavy("heat",0)==.5&&Heavy("heat",60)==.5,"heavy ERA full HEAT response normal through60");
 Check(Math.Abs(Heavy("heat",70)-.75)<1e-9&&Heavy("heat",80)==1,"heavy HEAT angular fade");
 Check(Heavy("apfsds",0)==1&&Heavy("apfsds",30)==.85&&Heavy("apfsds",60)==.85,"heavy KE distinct angular response");
 Check(Math.Abs(Heavy("apfsds",15)-.925)<1e-9&&Math.Abs(Heavy("apfsds",70)-.925)<1e-9,"heavy KE ramps on both sides");
 foreach(var threat in new[]{"heat","apfsds"})
 {
  Check(Heavy(threat,45,false)==1,"shared spentcell passive for "+threat);
  Check(Heavy(threat,45,thickness:59.9)==1&&Heavy(threat,45,thickness:80.1)==1,"thickness band enforced");
  Check(Heavy(threat,45,thickness:60)<1&&Heavy(threat,45,thickness:80)<1,"inclusive thickness endpoints");
  Check(Heavy(threat,45,original:false)==1&&Heavy(threat,45,cold:false)==1,"secondary and earlier target passive");
  Check(Heavy(threat,90)==1,"outside angle passive");
 }
 foreach(var threat in new[]{"ap","aphe","he","hesh"})
  Check(Heavy(threat,45)==1&&!ArmourResponseModel.ReactiveThreat(heavy,threat),"unsupported threat passive/no activation "+threat);
 var heavyCells=new EraCells();var sharedCell=new EraCell(88,2,1,0,0);
 Check(heavyCells.Consume(sharedCell)&&!heavyCells.Consume(sharedCell),"HEAT/KE share exactly one finitecell");
 Check(heavyCells.Consume(sharedCell with{Element=3})&&heavyCells.Consume(sharedCell with{Spawn=89}),"separate plate/spawn isolated");
 Check(ArmourResponseModel.AngularWeight(heavy,"apfsds",0)==0&&ArmourResponseModel.AngularWeight(heavy,"heat",0)>0,"zero-weight rod doesnot trigger but normalHEAT does");
 var heavyCumulative=new ArmourShotState();
 for(var i=0;i<20;i++)ArmourResponseModel.Apply(heavyCfg,heavy,heavyCumulative,"h"+i,"heat",true,true,70,45,0,true);
 Check(Math.Abs(heavyCumulative.AdditionalRetention-.4)<1e-9,"heavy HEAT cumulative60percent cap unchanged");
 var heavyKeCumulative=new ArmourShotState();
 for(var i=0;i<20;i++)ArmourResponseModel.Apply(heavyCfg,heavy,heavyKeCumulative,"k"+i,"apfsds",true,true,70,45,0,true);
 Check(Math.Abs(heavyKeCumulative.AdditionalRetention-.65)<1e-9,"heavy KE cumulative35percent cap unchanged");
 Check(ArmourResponses.PassiveMatches(heavy,4000,.55f,.25f)&&!ArmourResponses.PassiveMatches(heavy,4300,.35f,.2f),"heavy exact passive fingerprint");
 var withoutKe=System.Text.Json.Nodes.JsonNode.Parse(heavyRoot.ToJsonString())!;
 withoutKe["responses"]!.AsArray().Last()!["geometry"]!.AsObject().Remove("kineticAngularCurve");
 bool missingRejected=false;try{ArmourResponses.Parse(withoutKe.ToJsonString());}catch(FormatException){missingRejected=true;}
 Check(missingRejected,"heavy missing KE curve rejected");
 var oldWithKe=System.Text.Json.Nodes.JsonNode.Parse(heavyRoot.ToJsonString())!;
 var oldEntry=oldWithKe["responses"]!.AsArray().First(n=>n!["kind"]!.GetValue<string>()=="lightEra")!;
 oldEntry["geometry"]!["kineticAngularCurve"]=System.Text.Json.Nodes.JsonNode.Parse(heavyEntry["geometry"]!["kineticAngularCurve"]!.ToJsonString());
 bool oldRejected=false;try{ArmourResponses.Parse(oldWithKe.ToJsonString());}catch(FormatException){oldRejected=true;}
 Check(oldRejected,"old kinds forbid new optional curve");
}
Console.WriteLine($"HEAVY ERA REVIEW PASS: {checks} checks.");

if(Environment.GetEnvironmentVariable("SHELL_REVIEW_ARMOUR_CATALOGUE") is {} producerPath)
{
 var producerCatalogue=ArmourResponses.Parse(File.ReadAllText(producerPath));
 var producerHeavy=producerCatalogue.Responses.Single(r=>r.Kind=="heavyEra");
 Check(producerHeavy.CompatibleMaterialIds.SequenceEqual(new[]{"cwepHeavyEraCassette"}),"actualproducer exact heavy materialID");
 Check(producerHeavy.Calibration.HeatRetention==.5&&producerHeavy.Calibration.IntactRodRetention==.85&&producerHeavy.Calibration.DisturbedRodRetention==.85,"actualproducer calibration");
 Check(producerHeavy.Geometry.MinNormalThicknessMm==60&&producerHeavy.Geometry.MaxNormalThicknessMm==80&&producerHeavy.CellPitchM==.25,"actualproducer geometry/cells");
 Check(ArmourResponseModel.AngularWeight(producerHeavy,"heat",0)==1&&ArmourResponseModel.AngularWeight(producerHeavy,"apfsds",0)==0,"actualproducer distinctanglecurves");
 Check(!producerCatalogue.Enabled&&producerCatalogue.MaximumAdditionalHeatLoss==.6&&producerCatalogue.MaximumAdditionalKineticLoss==.35,"actualproducer defaultdisabled/caps preserved");
 Check(producerCatalogue.Responses.Single(r=>r.Kind=="lightEra").Calibration.HeatRetention==.62,"actualproducer lightERAunchanged");
 Console.WriteLine($"ACTUAL PRODUCER CONTRACT PASS: {checks} checks.");
}

if(Environment.GetEnvironmentVariable("SHELL_REVIEW_CANDIDATE_DLL") is {} candidateDll && Environment.GetEnvironmentVariable("SHELL_REVIEW_ARMOUR_CATALOGUE") is {} candidateCatalogue)
{
 var candidateAssembly=System.Reflection.Assembly.LoadFrom(candidateDll);
 var candidateParser=candidateAssembly.GetType("SprocketShellSelector.ArmourResponses",true)!;
 var parsedCandidate=candidateParser.GetMethod("Parse",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{File.ReadAllText(candidateCatalogue)})!;
 var candidateResponses=(System.Collections.IEnumerable)parsedCandidate.GetType().GetProperty("Responses")!.GetValue(parsedCandidate)!;
 var candidateKinds=candidateResponses.Cast<object>().Select(r=>(string)r.GetType().GetProperty("Kind")!.GetValue(r)!).ToArray();
 Check(candidateKinds.Count(k=>k=="heavyEra")==1,"actual frozen candidate DLL parses producer heavyEra catalogue");
 Console.WriteLine($"ACTUAL DLL CONTRACT PASS: {checks} checks.");
}
