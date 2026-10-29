using System;
using System.Linq;
using BattlePresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.BattlePresentation.Editor
{
    public static class PlayerHudSetup
    {
        const string Art = "Assets/GameAssets/UI/PlayerHUD/";
        [MenuItem("工具/卡牌战斗/Player HUD/接入左下玩家信息")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var adapter = roots.SelectMany(r=>r.GetComponentsInChildren<BattleTimelineSceneAdapter>(true)).Single();
            var camera = roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.name == "UI Camera");
            if (adapter.transform.Find("PlayerHUDCanvas") != null)
            {
                foreach (var cutout in adapter.GetComponentsInChildren<PortraitCutout>(true))
                    if (cutout.GetComponent<CanvasRenderer>() == null) Undo.AddComponent<CanvasRenderer>(cutout.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                SetupShader();
                Debug.Log("Player HUD already installed; bindings preserved."); return;
            }
            var frame = Import("player-frame.png", false);
            var fill = Import("health-fill.png", true);
            var portrait = Import("portrait.jpg", false);
            var canvasRect = Rect("PlayerHUDCanvas", adapter.transform, 0,0,0,0);
            Undo.RegisterCreatedObjectUndo(canvasRect.gameObject,"Create Player HUD");
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 10;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2340,1080); scaler.matchWidthOrHeight = .5f;
            // Work in source-image coordinates, then scale the entire composition as one unit.
            var hud = Rect("PlayerInfo",canvasRect,32,22,1997,788);
            hud.localScale = Vector3.one * .32f;
            var mask = Rect("PortraitMask",hud,118,182,442,465);
            mask.gameObject.AddComponent<PortraitCutout>().raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var face = Picture("Portrait",mask,0,0,442,465,portrait);
            // Cover the opening without stretching the supplied landscape portrait.
            face.rectTransform.sizeDelta = new Vector2(465f*349/245,465);
            face.rectTransform.anchoredPosition = new Vector2(-95,0);
            Picture("ConnectedFrame",hud,0,0,1997,788,frame);
            var hp = Picture("HealthFill",hud,625,426,1050,88,fill);
            hp.type = Image.Type.Filled; hp.fillMethod = Image.FillMethod.Horizontal; hp.fillOrigin = 0;
            var health = Label("HealthValue",hud,625,418,1050,105,72,new Color(.95f,.88f,.72f));
            var shield = Label("ShieldValue",hud,1714,375,165,175,110,new Color(.13f,.14f,.14f));
            var view = hud.gameObject.AddComponent<PlayerHudView>(); view.Configure(adapter,hp,health,shield);
            view.Render(null);
            SetupShader();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            Debug.Log("Player HUD installed with independent portrait, fill and values on UI Camera.");
        }
        static Sprite Import(string name, bool trim)
        {
            string path = Art+name;
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var i = (TextureImporter)AssetImporter.GetAtPath(path);
            i.textureType=TextureImporterType.Sprite; i.spriteImportMode=SpriteImportMode.Single;
            i.mipmapEnabled=false; i.alphaIsTransparency=true; i.textureCompression=TextureImporterCompression.Uncompressed;
            i.maxTextureSize=4096; i.isReadable=trim; i.filterMode=FilterMode.Bilinear;
            i.SaveAndReimport();
            if (trim)
            {
                var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path); var pixels=t.GetPixels32();
                int x0=t.width,y0=t.height,x1=0,y1=0;
                for(int y=0;y<t.height;y++) for(int x=0;x<t.width;x++) if(pixels[y*t.width+x].a>16)
                { x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y); }
                i.spriteImportMode=SpriteImportMode.Multiple;
                i.spritesheet=new[]{new SpriteMetaData{name="HealthFill",rect=new Rect(x0,y0,x1-x0+1,y1-y0+1),pivot=new Vector2(.5f,.5f),alignment=0}};
                i.isReadable=false;i.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
        }
        [MenuItem("工具/卡牌战斗/Player HUD/接入溶解与裁切")]
        public static void SetupShader()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var hud=UnityEngine.Object.FindObjectOfType<PlayerHudView>();
            var shader=Shader.Find("Poker/UI/Player HUD");
            if(hud==null || shader==null) throw new InvalidOperationException("HUD or shader missing.");
            Material Material(string name, float dissolve)
            {
                var path=Art+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
                Undo.RecordObject(mat,"HUD shader");mat.shader=shader;mat.SetFloat("_Dissolve",dissolve);
                mat.SetFloat("_UseUIAlphaClip",1);EditorUtility.SetDirty(mat);return mat;
            }
            var health=Material("HealthDissolve",1);var clipped=Material("HudAlphaClip",0);
            var fill=hud.transform.Find("HealthFill").GetComponent<Image>();
            Undo.RecordObject(fill,"Texture dissolve");fill.type=Image.Type.Simple;fill.material=health;
            var effect=fill.GetComponent<HealthDissolve>();
            if(effect==null) effect=Undo.AddComponent<HealthDissolve>(fill.gameObject);
            var canvas=fill.canvas;Undo.RecordObject(canvas,"HUD vertex data");
            canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
            foreach(var path in new[]{"ConnectedFrame","PortraitMask/Portrait"})
            {
                var image=hud.transform.Find(path).GetComponent<Image>();Undo.RecordObject(image,"HUD alpha clip");image.material=clipped;
            }
            hud.Render(null);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);AssetDatabase.SaveAssets();
            Debug.Log("HUD texture dissolve + alpha clip installed; portrait stencil mask retained.");
        }
        [MenuItem("工具/卡牌战斗/Player HUD/预览半血溶解")]
        public static void PreviewHalf()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Preview in Play Mode only.");
            var hud=UnityEngine.Object.FindObjectOfType<PlayerHudView>();hud.enabled=false;
            hud.Render(new UnitDisplayData("preview","preview","player",60,120,12,0,0));
        }
        static RectTransform Rect(string name, Transform parent,float x,float y,float w,float h)
        {
            var r = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;
            r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static Image Picture(string name,Transform parent,float x,float y,float w,float h,Sprite sprite)
        { var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image; }
        static Text Label(string name,Transform parent,float x,float y,float w,float h,int size,Color color)
        {
            var text=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();
            text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=color;text.raycastTarget=false;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=30;text.resizeTextMaxSize=size;return text;
        }
        [MenuItem("工具/卡牌战斗/Player HUD/验证数值显示")]
        public static void Validate()
        {
            var hud=UnityEngine.Object.FindObjectOfType<PlayerHudView>();
            if(hud==null)throw new InvalidOperationException("HUD missing.");
            var fill=hud.transform.Find("HealthFill").GetComponent<Image>();
            var health=hud.transform.Find("HealthValue").GetComponent<Text>();
            var shield=hud.transform.Find("ShieldValue").GetComponent<Text>();
            var dissolve=fill.GetComponent<HealthDissolve>();
            if(dissolve!=null && (fill.type!=Image.Type.Simple || fill.material.shader.name!="Poker/UI/Player HUD" ||
                fill.material.GetFloat("_Dissolve")<.5f || ShaderUtil.ShaderHasError(fill.material.shader)))
                throw new InvalidOperationException("Dissolve must use a full quad and an error-free HUD shader.");
            var portrait=hud.transform.Find("PortraitMask/Portrait").GetComponent<Image>();
            if(dissolve!=null && (portrait.GetComponentInParent<Mask>()==null || !portrait.maskable ||
                portrait.material.GetFloat("_UseUIAlphaClip")<.5f))
                throw new InvalidOperationException("Portrait stencil and alpha clip must remain enabled.");
            foreach(var amount in new long[]{0,50,100})
            {
                hud.Render(new UnitDisplayData("p","p","player",amount,100,12,0,0));
                var effect=fill.GetComponent<HealthDissolve>();
                if(Mathf.Abs((effect!=null?effect.Amount:fill.fillAmount)-amount/100f)>.001f || health.text!=amount+" / 100" || shield.text!="12")
                    throw new InvalidOperationException("HUD value/fill mismatch.");
            }
            hud.Render(new UnitDisplayData("p","p","player",10,0,0,0,0));
            if((fill.GetComponent<HealthDissolve>()?.Amount ?? fill.fillAmount)!=0)throw new InvalidOperationException("Zero max health must be empty.");
            hud.Render(null);
            Debug.Log("Player HUD validation passed: empty/half/full, zero maximum, shield and independent text.");
        }
    }
}
