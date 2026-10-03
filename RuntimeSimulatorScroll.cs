using DynamicGUI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace SprocketShellSelector;

// Modify the template only while the simulator's native dropdown clones it.
// Restoring it also avoids leaking changes into pooled cannon/other UI controls.
[HarmonyPatch]
internal static class RuntimeSimulatorScroll
{
    private static readonly HashSet<IntPtr> SimulatorDropdowns=new();
    [HarmonyPostfix,HarmonyPatch(typeof(DynamicDropdown),nameof(DynamicDropdown.SetOptions))]
    private static void Register(DynamicDropdown __instance)
    {
        var dropdown=__instance.dropdown;if(dropdown==null)return;
        if(RuntimeArmourSimulator.DrawingShellChoice)SimulatorDropdowns.Add(dropdown.Pointer);
        else SimulatorDropdowns.Remove(dropdown.Pointer);
    }
    private sealed record ScrollState(RectTransform Template,Vector2 Size,ScrollRect Scroll,bool Vertical,bool Horizontal,
        Scrollbar? Bar,ScrollRect.ScrollbarVisibility Visibility,GameObject? TemporaryBar);
    [HarmonyPrefix,HarmonyPatch(typeof(TMP_Dropdown),nameof(TMP_Dropdown.Show))]
    private static void Begin(TMP_Dropdown __instance,out ScrollState? __state)
    {
        __state=null;
        if(!SimulatorDropdowns.Contains(__instance.Pointer)||__instance.options.Count<=6)return;
        var template=__instance.template;if(template==null)return;
        var scroll=template.GetComponentInChildren<ScrollRect>(true);
        if(scroll==null){Plugin.ModLog.LogWarning("[Simulator scroll] Native dropdown template has no ScrollRect.");return;}
        __state=new(template,template.sizeDelta,scroll,scroll.vertical,scroll.horizontal,scroll.verticalScrollbar,scroll.verticalScrollbarVisibility,null);
        try
        {
            template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Math.Clamp(Screen.height*.4f,160,320));
            scroll.vertical=true;scroll.horizontal=false;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            if(scroll.verticalScrollbar!=null)return;
            var barObject=new GameObject("Shell selector scrollbar");
            __state=__state with{TemporaryBar=barObject};
            var rect=barObject.AddComponent<RectTransform>();rect.SetParent(template,false);
            rect.anchorMin=new Vector2(1,0);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,.5f);
            rect.sizeDelta=new Vector2(12,-8);rect.anchoredPosition=new Vector2(-2,0);
            var track=barObject.AddComponent<Image>();track.color=new Color(.08f,.08f,.08f,.95f);
            var handleObject=new GameObject("Handle");var handle=handleObject.AddComponent<RectTransform>();handle.SetParent(rect,false);
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=new Vector2(2,2);handle.offsetMax=new Vector2(-2,-2);
            var handleImage=handleObject.AddComponent<Image>();handleImage.color=new Color(.75f,.65f,.4f,1);
            var bar=barObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handleImage;bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;
        }
        catch(Exception ex){Plugin.ModLog.LogError("[Simulator scroll] "+ex);}
    }
    [HarmonyFinalizer,HarmonyPatch(typeof(TMP_Dropdown),nameof(TMP_Dropdown.Show))]
    private static void End(ScrollState? __state)
    {
        if(__state==null)return;
        __state.Template.sizeDelta=__state.Size;
        __state.Scroll.vertical=__state.Vertical;__state.Scroll.horizontal=__state.Horizontal;
        __state.Scroll.verticalScrollbar=__state.Bar;__state.Scroll.verticalScrollbarVisibility=__state.Visibility;
        if(__state.TemporaryBar!=null){__state.TemporaryBar.SetActive(false);UnityEngine.Object.Destroy(__state.TemporaryBar);}
    }
}
