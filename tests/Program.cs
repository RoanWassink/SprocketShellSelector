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
Check(profileList.Count == 1 && profileList[0].Id == "apfsds", "default profile ID");
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
var fuse=new ApheFuse();
Check(!fuse.Traverse((IntPtr)1,1,10,spallSettings),"thin first plate does not arm APHE");
Check(!fuse.Traverse((IntPtr)1,1,10,spallSettings) && fuse.AccumulatedRhaMm==10,"repeated burst for same segment cannot arm fuse twice");
Check(!fuse.Traverse((IntPtr)1,2,10,spallSettings),"two thin plates below RHA threshold retain AP continuation");
Check(fuse.Traverse((IntPtr)1,3,5,spallSettings),"thin plates cumulatively arm fuse at threshold");
Check(new ApheFuse().Traverse((IntPtr)2,1,30,spallSettings),"single sufficiently thick traversed plate arms APHE");
Check(!new ApheFuse().Traverse((IntPtr)2,1,5,spallSettings),"new shell does not inherit another shell fuse state");
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
