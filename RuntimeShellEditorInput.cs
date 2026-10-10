using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SprocketShellSelector;

// Suspend enabled non-UI actions, keeping the EventSystem's actual UI maps alive.
internal static class RuntimeShellEditorInput
{
    private static readonly Dictionary<IntPtr,InputAction> suspended=new();
    internal static void Tick()
    {
        if(!RuntimeShellEditor.IsOpen){Restore();return;}
        var ui=EventSystem.current?.currentInputModule?.TryCast<InputSystemUIInputModule>();
        if(ui==null)return; // Never disable controls when the UI input source cannot be identified.
        var maps=new HashSet<IntPtr>();
        foreach(var reference in new[]{ui.point,ui.leftClick,ui.rightClick,ui.middleClick,ui.scrollWheel,ui.move,ui.submit,ui.cancel})
            if(reference?.action?.actionMap is {} map)maps.Add(map.Pointer);
        if(maps.Count==0)return;
        foreach(var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if(asset==null)continue;
            foreach(var map in asset.actionMaps)
            {
                if(maps.Contains(map.Pointer))continue;
                foreach(var action in map.actions)
                    if(action.enabled){suspended.TryAdd(action.Pointer,action);action.Disable();}
            }
        }
    }
    internal static void Restore()
    {
        foreach(var action in suspended.Values)
            try{action.Enable();}catch(Exception ex){Plugin.ModLog.LogWarning("[Shell editor] Input restoration: "+ex.Message);}
        suspended.Clear();
    }
}
public sealed class ShellEditorInputDriver:MonoBehaviour
{
    public ShellEditorInputDriver(IntPtr pointer):base(pointer){}
    public void Update(){try{RuntimeShellEditorInput.Tick();}catch(Exception ex){Plugin.ModLog.LogWarning("[Shell editor] Input guard: "+ex.Message);}}
    public void LateUpdate(){try{RuntimeShellEditorInput.Tick();}catch(Exception){RuntimeShellEditorInput.Restore();}}
}
