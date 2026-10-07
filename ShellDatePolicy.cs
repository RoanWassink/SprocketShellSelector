namespace SprocketShellSelector;
internal static class ShellDatePolicy
{
    internal static bool ValidTimeline(ReadOnlySpan<ShellNativeDate> starts)
    {
        if(starts.Length==0||starts[^1]==ShellNativeDate.Maximum)return false;
        foreach(var start in starts)if(!start.Valid)return false;
        for(var i=1;i<starts.Length;i++)if(starts[i]<=starts[i-1])return false;
        return true;
    }
}
