using System.Numerics;
using SprocketShellSelector;
internal static class WireChecks { internal static void Run(Action<bool,string> Check) {
var wire = new WireCable(Vector3.Zero, Vector3.Zero, 10, 1, 8, 20);
wire.Observe(new(3,0,0), 1, true, true);
wire.Observe(new(3,4,0), 2, true, true);
Check(Math.Abs(wire.PaidOut-7)<.001, "payout follows bends rather than displacement");
Check(wire.Points[0]==Vector3.Zero && wire.Points[^1]==new Vector3(3,4,0), "anchor and moving tip");
wire.Observe(new(0,4,0), 3, true, true);
Check(wire.Connected, "exact limit remains connected");
wire.Observe(new(0,5,0), 4, true, true);
Check(!wire.Connected && wire.BreakReason=="wire-exhausted", "exhaustion breaks commands");
wire.Observe(new(0,6,0), 5, true, true);
Check(wire.Points[^1]==new Vector3(0,4,0), "broken wire stays behind; no infinite payout follows rocket");
wire.Finish(6);
Check(!wire.Expired(23) && wire.Expired(24), "wire break starts TTL before later projectile impact");
wire.Observe(new(100,100,100), 7, true, true);
Check(wire.Points[^1]==new Vector3(0,4,0), "finished endpoint frozen");
var bends=new WireCable(Vector3.Zero,Vector3.Zero,100000, .1, 8);
for(int i=1;i<1000;i++)bends.Observe(new(i, i%2,0), i, true, true);
Check(bends.Points.Count<=8 && bends.Points[0]==Vector3.Zero && bends.Points[^1].X==999, "bounded geometry preserves endpoints");
Check(bends.PaidOut>999, "compression never reduces consumed budget");
var sag=bends.RenderPoints(1);
Check(sag[0]==bends.Points[0] && sag[^1]==bends.Points[^1], "sag preserves endpoints");
var owner=new WireCable(Vector3.Zero,Vector3.Zero);owner.Observe(Vector3.UnitX,0,true,false);
Check(owner.BreakReason=="superseded", "new missile permanently severs old commandlink");
owner.Observe(Vector3.UnitX*2,1,true,true);Check(!owner.Connected,"link never reconnects");
var dead=new WireCable(Vector3.Zero,Vector3.Zero);dead.Observe(Vector3.UnitX,0,false,true);
Check(dead.BreakReason=="launcher-unavailable","dead launcher loses commands");
var invalid=new WireCable(Vector3.Zero,Vector3.Zero);invalid.Observe(new(float.NaN,0,0),0,true,true);
Check(invalid.BreakReason=="invalid-position" && invalid.Points.Count==2,"nonfinite position cannot contaminate geometry");
Check(invalid.Expired(20), "invalid-position cleanup starts immediately");
var initial = new WireCable(Vector3.Zero, new(10,0,0), maximumLength: 1, retentionSeconds: 5, createdAt: 50);
Check(initial.Points[^1] == Vector3.UnitX && initial.PaidOut == 1 && !initial.Connected, "initial excess is clipped to spool budget");
Check(!initial.Expired(54) && initial.Expired(55), "initial break TTL uses actual creation time");
var huge = new WireCable(Vector3.Zero,Vector3.Zero);
huge.Observe(new(float.MaxValue,0,0), 1, true, true);
Check(huge.BreakReason=="invalid-distance" && double.IsFinite(huge.PaidOut) && huge.Points[^1]==Vector3.Zero && huge.Expired(21), "derived distance overflow cannot poison renderer or retention");
bool rejected=false;
try { _ = new WireCable(Vector3.Zero,new(float.MaxValue,0,0)); } catch(ArgumentOutOfRangeException) { rejected=true; }
Check(rejected, "initial derived distance overflow rejected");

var profiles=ReleaseProfiles.Defaults();var draft=new ModularShellDraft(profiles);
draft.Set(0,"modifiers.guidanceTransport","wire");draft.Set(0,"modifiers.wireDisplayWidth","0.025");
var compiled=ShellProfiles.Parse(draft.Compile().RuntimeJson!)[0];
Check(compiled.Wire?.Transport=="wire"&&compiled.Wire.DisplayWidth==.025,"wire fields compile under stable profile ID");
Check(compiled.Behavior==ShellProfiles.Parse(profiles)[0].Behavior,"wire transport cannot change impact family");
Check(draft.Active(0,"guidanceTransport")&&draft.Active(0,"wireMaximumLength"),"wire available even unguided and fields active");
draft.Set(0,"modifiers.guidanceTransport","current");
Check(!draft.Active(0,"wireMaximumLength")&&draft.Value(0,"modifiers.wireDisplayWidth").GetValue<double>()==.025,"inactive cable settings preserved");
Check(ShellProfiles.Parse(profiles).All(p=>p.Wire==null),"old profiles gain no cable by default");
bool bad=false;try{new WireOptions("radio").Validate("bad");}catch(FormatException){bad=true;}Check(bad,"unknown transport fails explicitly");
bad=false;try{new WireOptions("wire",DisplayWidth:double.NaN).Validate("bad");}catch(FormatException){bad=true;}Check(bad,"nonfinite visual settings rejected");
var rocket=ShellProfiles.Parse(profiles).First(p=>p.Behavior=="atgm");var withWire=rocket with {Wire=new WireOptions("wire")};
Check(ShellBalance.Calculate(135,800,1800,rocket)==ShellBalance.Calculate(135,800,1800,withWire),"wire cannot alter motor launch or ballistic geometry");
Check(ShellBalance.RequiredTechnologyIds(rocket).SequenceEqual(ShellBalance.RequiredTechnologyIds(withWire)),"wire uses native existing technology context");

} }
