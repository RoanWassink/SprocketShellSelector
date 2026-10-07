namespace SprocketShellSelector;
internal sealed record ShellEraContext(ShellNativeDate Date,string[] Names,ShellNativeDate[] Starts,int Index,IReadOnlySet<string> AvailableBehaviors,double? HeatFactor=null);
internal static class ShellEraPolicy
{
    internal static bool Valid(ShellEraContext? c)
    {
        if(c==null||!c.Date.Valid||c.Names.Length!=c.Starts.Length||!ShellDatePolicy.ValidTimeline(c.Starts)||c.Index<0||c.Index>=c.Names.Length)return false;
        if(c.Names.Any(string.IsNullOrWhiteSpace)||c.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=c.Names.Length)return false;
        return c.Date==ShellNativeDate.Maximum ? c.Index==c.Names.Length-1 : c.Date>=c.Starts[c.Index]&&(c.Index==c.Starts.Length-1||c.Date<c.Starts[c.Index+1]);
    }
    private static bool Matches(string requested,string native)=>string.Equals(requested,native,StringComparison.OrdinalIgnoreCase)||(string.Equals(requested,"ww1",StringComparison.OrdinalIgnoreCase)&&string.Equals(native,"WWI",StringComparison.OrdinalIgnoreCase));
    internal static bool Allowed(ShellProfile p,ShellEraContext? c)
    {
        if(!Valid(c)||!c!.AvailableBehaviors.Contains(p.Behavior))return false;
        if(p.MinimumEra==null)return true;
        var minimum=Array.FindIndex(c.Names,name=>Matches(p.MinimumEra,name));
        return minimum>=0&&c.Index>=minimum;
    }
    internal static ShellProfile? Effective(ShellProfile? stored,ShellEraContext? c)=>stored!=null&&Allowed(stored,c)?stored:null;
}
