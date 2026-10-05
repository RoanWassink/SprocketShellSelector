namespace SprocketShellSelector;
internal static class ShellDatePolicy
{
    internal static readonly DateTime ModernFloor=new(1945,9,3);
    internal static bool ValidTimeline(ReadOnlySpan<DateTime> starts)
    {
        if(starts.Length==0||starts[^1].Date==DateTime.MaxValue.Date)return false;
        for(var i=1;i<starts.Length;i++)if(starts[i]<=starts[i-1])return false;
        return true;
    }
    internal static bool Modern(DateTime? date,ReadOnlySpan<DateTime> starts)
    {
        if(date==null||!ValidTimeline(starts))return false;
        if(date.Value.Date==DateTime.MaxValue.Date)return starts[^1].Date>=ModernFloor;
        return date.Value.Date>=ModernFloor&&date.Value.Date>=starts[0].Date;
    }
}
