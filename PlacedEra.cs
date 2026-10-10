using System.Numerics;
namespace SprocketShellSelector;
public readonly record struct PlacedEraActivation(IntPtr VehiclePointer,IntPtr VehicleRootPointer,long Spawn,
    int ComponentVuid,Vector3 Position,Vector3 Normal,string ResponseId);
// Main-thread companion API. Notifications observe committed live consumption; they cannot control it.
public static class PlacedEra
{
    public static event Action<PlacedEraActivation>? Activated;
    public static bool IsSpent(IntPtr vehicleRootPointer,int componentVuid)=>RuntimeArmourResponses.PlacedSpent(vehicleRootPointer,componentVuid);
    public static void ResetForEdit()=>RuntimeArmourResponses.ResetPlacedForEdit();
    internal static void Queue(ShellImpactContext context,PlacedEraActivation activation)
    {
        if(context.PlacedActivations.Count<128){context.PlacedActivations.Add(activation);RuntimeArmourResponses.PlacedTrace($"stage=notify-queued vuid={activation.ComponentVuid} spawn={activation.Spawn}");}
        else RuntimeArmourResponses.PlacedTrace($"reject=notification-cap vuid={activation.ComponentVuid}; IsSpent remains authoritative");
    }
    internal static void Flush(ShellImpactContext? context)
    {
        if(context==null||context.PlacedActivations.Count==0)return;
        var pending=context.PlacedActivations.ToArray();context.PlacedActivations.Clear();
        foreach(var activation in pending)if(RuntimeArmourResponses.CurrentPlaced(activation))Notify(activation);else RuntimeArmourResponses.PlacedTrace($"reject=stale-notification vuid={activation.ComponentVuid} spawn={activation.Spawn}");
    }
    private static void Notify(PlacedEraActivation activation)
    {
        if(Activated is not {} callbacks){RuntimeArmourResponses.PlacedTrace($"stage=notify-no-subscriber vuid={activation.ComponentVuid}");return;}
        RuntimeArmourResponses.PlacedTrace($"stage=notify-emitted vuid={activation.ComponentVuid} subscribers={callbacks.GetInvocationList().Length}");
        foreach(Action<PlacedEraActivation> callback in callbacks.GetInvocationList())
            try{callback(activation);}catch(Exception ex){Plugin.ModLog.LogWarning("[Placed ERA] Optional visual subscriber failed: "+ex.Message);}
    }
}
