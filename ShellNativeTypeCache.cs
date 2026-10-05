namespace SprocketShellSelector;
// Recover the real native type captured before our substitution, never a guessed AP ID.
internal sealed class ShellNativeTypeCache<T>
{
    private readonly Dictionary<(IntPtr Register,IntPtr Source,string Custom),T> native=new();
    internal void Remember(IntPtr register,string custom,T original,IntPtr source=default)=>native[(register,source,custom)]=original;
    internal bool Known(IntPtr register,string incoming)=>native.Keys.Any(k=>k.Register==register&&k.Custom==incoming);
    internal bool Restore(IntPtr register,string incoming,ref T type,IntPtr source=default)
    {
        if(!native.TryGetValue((register,source,incoming),out var original))return false;
        type=original;return true;
    }
    internal void Clear(IntPtr register)
    {
        foreach(var key in native.Keys.Where(k=>k.Register==register).ToArray())native.Remove(key);
    }
}
