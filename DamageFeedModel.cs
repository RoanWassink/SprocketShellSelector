namespace SprocketShellSelector;
internal enum PlateOutcome {Unknown,Penetrated,Stopped,Deflected}
internal static class DamageFeedTruth
{
    internal static PlateOutcome Outcome(bool penetrated,bool embedded,bool deflected,int continuations,bool error=false)
    {if(error)return PlateOutcome.Unknown;if(penetrated)return PlateOutcome.Penetrated;if(deflected)return PlateOutcome.Deflected;if(embedded&&continuations==0)return PlateOutcome.Stopped;return PlateOutcome.Unknown;}
    internal static double? Remaining(IReadOnlyList<double> continuationBudgets)=>continuationBudgets.Count==1&&double.IsFinite(continuationBudgets[0])&&continuationBudgets[0]>=0?continuationBudgets[0]:null;
    internal static string Label(string? text,string fallback)
    {var clean=new string((text??"").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').ToArray()).Trim();return clean.Length==0?fallback:clean.Length>32?clean[..32]:clean;}
    internal static bool DirectDamage(bool shot,bool nativeDamageCall,double delta,bool targetRegister=true)=>shot&&nativeDamageCall&&targetRegister&&double.IsFinite(delta)&&delta<0;
    internal static bool Died(bool? previouslyAlive,bool alive,double delta)=>previouslyAlive==true&&!alive&&double.IsFinite(delta)&&delta<0;
}
internal sealed record ComponentDamage(string Text,bool Fatal)
{
    internal ComponentDamage Merge(ComponentDamage next)=>Fatal&&!next.Fatal?this:next;
}
internal sealed class DamageFeedBuffer
{
    internal sealed record Row(double Time,string Text);
    private readonly List<Row> rows=new();
    internal const int MaximumRows=12;
    internal IReadOnlyList<Row> Visible(double now)
    {if(!double.IsFinite(now)){rows.Clear();return rows;}rows.RemoveAll(r=>now-r.Time>=8||now<r.Time);return rows;}
    internal void Add(double now,IEnumerable<string> lines)
    {if(!double.IsFinite(now))return;foreach(var line in lines.Take(MaximumRows))rows.Add(new(now,line));if(rows.Count>MaximumRows)rows.RemoveRange(0,rows.Count-MaximumRows);}
    internal void Clear()=>rows.Clear();
}

internal sealed class DamageFeedActivations
{
    private sealed record Activation(string Label,string Text);
    private readonly Dictionary<string,Activation> rows=new();
    internal IEnumerable<string> Rows=>rows.Values.Select(r=>r.Text);
    internal bool Record(string key,bool live,bool first,string kind,string label)
    {
        if(!live||!first||kind is not ("lightEra" or "heavyEra")||rows.Count>=4||rows.ContainsKey(key))return false;
        rows.Add(key,new(label,"ERA activated: "+label));return true;
    }
    internal void Budget(string key,double before,double after)
    {
        if(rows.TryGetValue(key,out var row)&&double.IsFinite(before)&&double.IsFinite(after)&&before>=0&&after>=0)
            rows[key]=row with {Text=$"ERA activated: {row.Label} - {before:0} -> {after:0} mm"};
    }
    internal static IEnumerable<string> Compose(IEnumerable<string> activations,IEnumerable<string> health,IEnumerable<string> layers)=>activations.Take(4).Concat(health.Take(8)).Concat(layers).Take(12);
}
