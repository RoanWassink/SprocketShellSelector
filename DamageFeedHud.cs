using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace SprocketShellSelector;
public sealed class DamageFeedBehaviour:MonoBehaviour
{
    public DamageFeedBehaviour(IntPtr pointer):base(pointer){}
    public void Update(){try{DamageFeedHud.Update();}catch(Exception ex){DamageFeedHud.Failed(ex);}}
}
internal static class DamageFeedHud
{
    private static GameObject? root;private static TextMeshProUGUI? text;private static RectTransform? panel;
    private static bool fontReported,shownReported;private static int failures;private static float retryAt;
    internal static void Failed(Exception ex){if(failures++<4)Plugin.ModLog.LogWarning("[Damage feed HUD] "+ex);Destroy();retryAt=Time.unscaledTime+5;}
    private static GameObject RectObject(string name)=>new GameObject(name,new Il2CppSystem.Type[]{Il2CppType.Of<RectTransform>()});
    internal static void Hide(){if(root!=null)root.SetActive(false);}
    internal static void Destroy(){if(root!=null)Object.Destroy(root);root=null;text=null;panel=null;}
    internal static void Update()
    {
        if(Time.unscaledTime<retryAt)return;
        var rows=RuntimeDamageFeed.Buffer.Visible(Time.unscaledTime);
        if(!RuntimeDamageFeed.Show||rows.Count==0){Hide();return;}
        if(root==null)
        {
            var font=TMP_Settings.defaultFontAsset;if(font==null){if(!fontReported){fontReported=true;RuntimeDamageFeed.Probe("TMP default font unavailable; waiting");}return;}
            root=RectObject("Shell damage feed");Object.DontDestroyOnLoad(root);
            var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var child=RectObject("Feed panel");child.transform.SetParent(root.transform,false);
            panel=child.GetComponent<RectTransform>();panel.sizeDelta=new Vector2(560,310);
            var background=child.AddComponent<Image>();background.color=new Color(0,0,0,.55f);background.raycastTarget=false;
            var content=RectObject("Recent impacts");content.transform.SetParent(child.transform,false);
            var rect=content.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,10);rect.offsetMax=new Vector2(-12,-10);
            text=content.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=18;text.color=Color.white;text.richText=false;text.raycastTarget=false;text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Truncate;
        }
        root.SetActive(true);if(!shownReported){shownReported=true;RuntimeDamageFeed.Probe("HUD created and showing rows");}
        var anchor=new Vector2(RuntimeDamageFeed.Left?0:1,1);panel!.anchorMin=anchor;panel.anchorMax=anchor;panel.pivot=anchor;panel.anchoredPosition=new Vector2(RuntimeDamageFeed.Left?24:-24,-140);
        text!.text=string.Join("\n",rows.Select(r=>r.Text));
        panel.sizeDelta=new Vector2(560,Math.Clamp(text.GetPreferredValues(text.text,536,1000).y+20,42,310));
    }
}
