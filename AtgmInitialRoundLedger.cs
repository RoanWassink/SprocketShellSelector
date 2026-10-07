namespace SprocketShellSelector;
// Lifetime is a native combat vehicle instance, identity is its native launcher VUID.
internal sealed class AtgmInitialRoundLedger
{
    private readonly HashSet<(long Instance,int Launcher)> issued=new();
    internal bool Claim(long instance,int launcher,bool own,bool allowed,bool neverLoaded)
        =>instance!=0&&own&&allowed&&neverLoaded&&issued.Add((instance,launcher));
    internal void End(long instance)=>issued.RemoveWhere(k=>k.Instance==instance);
}

internal static class AtgmInitialRoundResources
{
    // Native AmmoRack.Build, Sprocket 0.2.55.5: one-round ammunition categories.
    internal static (float Material,float Assembly) Cost(float calibre,float propellant,float fullRoundMass)
    {
        if(!float.IsFinite(calibre)||!float.IsFinite(propellant)||!float.IsFinite(fullRoundMass)||calibre<=0||propellant<0||fullRoundMass<=0)throw new ArgumentOutOfRangeException(nameof(calibre));
        var assembly=MathF.Pow(calibre/37f,2)*1.6199724674224854f;
        var material=MathF.Pow(calibre*.0005f,2)*propellant*MathF.PI*.001f*1150f*12.959779739379883f+fullRoundMass*.4859917163848877f;
        return(material,assembly);
    }
}

internal static class AtgmInitialAssemblyRules
{
    internal static bool EmptyFirstAssembly(bool firstAssembly,int nativeState,int loadedType,float lastFireTime)
        =>firstAssembly&&(nativeState==2||nativeState==3)&&loadedType==-1&&lastFireTime==0;
    internal static bool NeedsLauncherDefault(bool own,string? storedId)=>own&&(storedId==null||storedId==ShellProfiles.Vanilla);
}
