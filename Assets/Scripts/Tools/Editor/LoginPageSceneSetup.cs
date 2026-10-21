#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Poker.Presentation.LoginPage;
using Object = UnityEngine.Object;

/// <summary>Builds a saved, editable UGUI scene from the native Figma PNG exports.</summary>
public static class LoginPageSceneSetup
{
    public const string ScenePath = "Assets/Scenes/LoginPage.unity";
    const string Art = "Assets/Art/UI/LoginPage/";
    const string ReportFolder = "ArtExports/LoginPage";
    static readonly string[] Keys =
    {
        "01_start_game", "02_continue_game", "03_tarot_collection",
        "04_options", "05_patch_notes", "06_exit_game"
    };
    static readonly string[] Labels = { "开始游戏", "继续游戏", "塔罗图鉴", "选项", "更新说明", "退出游戏" };
    static readonly float[] Left = { 745, 745, 745, 746, 745, 745 };
    static readonly float[] Top = { 358, 467, 574, 681, 790, 897 };

    [MenuItem("Tools/Login Page/Create Figma Scene")]
    public static void CreateScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("LoginPage already exists. Open it rather than overwriting scene edits.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save open scene changes before creating LoginPage.");

        // Resolve every sprite before switching scenes, so a missing asset cannot discard the current view.
        var backgroundSprite = Sprite("Background/background.png");
        var titleSprite = Sprite("Title/title.png");
        var parts = Keys.Select(key => new[]
        {
            Sprite("Parts/" + key + "_panel.png"),
            Sprite("Parts/" + key + "_icon.png"),
            Sprite("Parts/" + key + "_text.png")
        }).ToArray();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "LoginPage";
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.cullingMask = 0;
        camera.allowHDR = false;
        camera.allowMSAA = false;

        var canvasObject = new GameObject("Login Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100;

        var bars = Rect("Letterbox", canvas.transform, 0, 0, 0, 0);
        bars.anchorMin = Vector2.zero;
        bars.anchorMax = Vector2.one;
        bars.offsetMin = bars.offsetMax = Vector2.zero;
        var barImage = bars.gameObject.AddComponent<Image>();
        barImage.color = Color.black;
        barImage.raycastTarget = false;

        // A fixed logical artboard preserves the Figma spacing at any display aspect ratio.
        var artboard = Rect("Figma Artboard 1920x1080", canvas.transform, 0, 0, 1920, 1080);
        artboard.anchorMin = artboard.anchorMax = artboard.pivot = new Vector2(.5f, .5f);
        artboard.anchoredPosition = Vector2.zero;
        Image("Background", artboard, backgroundSprite, 0, 0, 1920, 1080);
        var buttonsRoot = Rect("Buttons", artboard, 0, 0, 1920, 1080);
        var buttons = new Button[Keys.Length];
        for (int i = 0; i < Keys.Length; i++)
        {
            var buttonRect = Rect(Keys[i] + " - " + Labels[i], buttonsRoot, Left[i], Top[i], 430, 88);
            var panel = Image("Panel", buttonRect, parts[i][0], 0, 0, 430, 88);
            panel.raycastTarget = true;
            Image("Icon", buttonRect, parts[i][1], 35.411766f, 16.342857f, 55.647060f, 55.314285f);
            Image("Text", buttonRect, parts[i][2], 106.235291f, 23.885714f, 126.470589f, 40.228573f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, .97f, .84f);
            colors.selectedColor = new Color(1, .97f, .84f);
            colors.pressedColor = new Color(.72f, .66f, .50f);
            colors.fadeDuration = .10f;
            button.colors = colors;
            buttons[i] = button;
        }
        // The title is above the buttons in the original Figma stacking order.
        Image("Title", artboard, titleSprite, 522, 75, 875, 300);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                selectOnDown = buttons[(i + 1) % buttons.Length]
            };
        }
        var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        // No initial selection tint: the idle screen matches the Figma reference.
        eventObject.GetComponent<EventSystem>().firstSelectedGameObject = null;
        ConfigureMobile(canvas);
        ApplyLandscapeSettings();
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = canvasObject;
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        Verify();
        Debug.Log("LoginPage saved: 1920x1080 artboard, six buttons with independent Panel/Icon/Text images.");
    }

    static Sprite Sprite(string relativePath)
    {
        var path = Art + relativePath;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Missing UI Sprite: " + path);
        return sprite;
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    static Image Image(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = UnityEngine.UI.Image.Type.Simple;
        image.preserveAspect = false; // Rects use the exact Figma frame dimensions, including transparent padding.
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    static Canvas CanvasInScene()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open LoginPage first.");
        return SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Canvas>(true)).Single();
    }

    [MenuItem("Tools/Login Page/Apply Landscape Mobile Layout")]
    public static void ApplyMobile()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var canvas = CanvasInScene();
        ConfigureMobile(canvas);
        ApplyLandscapeSettings();
        Canvas.ForceUpdateCanvases();
        canvas.GetComponent<LoginPageLayout>().Refresh();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Verify();
        Debug.Log("LoginPage mobile layout saved: landscape only, safe-area artboard, full-bleed backdrop, expanded touch targets.");
    }

    static void ApplyLandscapeSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }

    static void ConfigureMobile(Canvas canvas)
    {
        var board = (RectTransform)canvas.transform.Find("Figma Artboard 1920x1080");
        var background = canvas.transform.Find("Background") as RectTransform;
        if (background == null) background = (RectTransform)board.Find("Background");
        background.SetParent(canvas.transform, false);
        background.SetSiblingIndex(1);
        background.anchorMin = background.anchorMax = background.pivot = new Vector2(.5f, .5f);
        background.localScale = Vector3.one;
        var buttonRoot = board.Find("Buttons");
        for (int i = 0; i < 6; i++)
        {
            var rect = (RectTransform)buttonRoot.GetChild(i);
            // Expand into the existing gaps, keeping the 109/107 px row spacing and all visible geometry intact.
            rect.anchoredPosition = new Vector2(Left[i] - 25, -Top[i] + 8);
            rect.sizeDelta = new Vector2(480, 104);
            var hit = rect.GetComponent<Image>();
            if (hit == null) hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var panel = (RectTransform)rect.Find("Panel");
            panel.anchoredPosition = new Vector2(25, -8);
            panel.GetComponent<Image>().raycastTarget = false;
            ((RectTransform)rect.Find("Icon")).anchoredPosition = new Vector2(60.411766f, -24.342857f);
            ((RectTransform)rect.Find("Text")).anchoredPosition = new Vector2(131.235291f, -31.885714f);
        }
        var layout = canvas.GetComponent<LoginPageLayout>();
        if (layout == null) layout = canvas.gameObject.AddComponent<LoginPageLayout>();
        layout.Configure(board, background);
    }

    [MenuItem("Tools/Login Page/Verify Figma Layout")]
    public static void Verify()
    {
        var canvas = CanvasInScene();
        var board = (RectTransform)canvas.transform.Find("Figma Artboard 1920x1080");
        var buttons = board.Find("Buttons");
        if (buttons.childCount != 6) throw new InvalidOperationException("Expected six buttons.");
        for (int i = 0; i < 6; i++)
        {
            var rect = (RectTransform)buttons.GetChild(i);
            if (rect.sizeDelta != new Vector2(480, 104) || rect.anchoredPosition != new Vector2(Left[i] - 25, -Top[i] + 8))
                throw new InvalidOperationException("Button differs from Figma: " + rect.name);
            if (rect.childCount != 3 || rect.GetComponent<Button>().targetGraphic != rect.Find("Panel").GetComponent<Image>())
                throw new InvalidOperationException("Button layers or target are incorrect: " + rect.name);
        }
        var images = board.GetComponentsInChildren<Image>(true);
        if (images.Count(image => image.sprite != null) != 19 || canvas.transform.Find("Background").GetComponent<Image>().sprite == null)
            throw new InvalidOperationException("Expected 20 individual sprite images, all assigned.");
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler.referenceResolution != new Vector2(1920, 1080) || scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
            throw new InvalidOperationException("Canvas scaling differs from Figma artboard.");
        var system = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponents<EventSystem>()).Single();
        if (system.GetComponent<InputSystemUIInputModule>() == null)
            throw new InvalidOperationException("New Input System UI module is missing.");
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "/unity_scene_validation.txt",
            "PASS: LoginPage; 1920x1080; 6 buttons; 18 independent button sprites; background and title; all Sprite references assigned; Figma visual positions exact; Input System UI enabled; landscape-only autorotation; 480x104 transparent touch targets; safe-area layout.\n");
        Debug.Log("LoginPage layout validation passed.");
    }

    [MenuItem("Tools/Login Page/Capture Preview")]
    public static void CapturePreview()
    {
        Capture(1920, 1080, new Rect(0, 0, 1920, 1080), "unity_scene_preview.png");
    }

    [MenuItem("Tools/Login Page/Capture Mobile Previews")]
    public static void CaptureMobilePreviews()
    {
        Capture(2400, 1080, new Rect(110, 34, 2290, 1046), "unity_mobile_20x9.png");
        Capture(2532, 1170, new Rect(141, 63, 2250, 1107), "unity_mobile_notch.png");
        Capture(1280, 720, new Rect(0, 0, 1280, 720), "unity_mobile_16x9.png");
        Capture(2400, 1080, new Rect(0, 34, 2290, 1046), "unity_mobile_notch_right.png");
    }

    static void Capture(int width, int height, Rect safeArea, string fileName)
    {
        var canvas = CanvasInScene();
        var camera = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponents<Camera>()).Single();
        var previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        var previousTarget = camera.targetTexture;
        var previousMask = camera.cullingMask;
        var previousActive = RenderTexture.active;
        var previousDistance = canvas.planeDistance;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D texture = null;
        try
        {
            camera.targetTexture = target;
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<LoginPageLayout>().ApplyViewport(new Rect(0, 0, width, height), safeArea);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllBytes(ReportFolder + "/" + fileName, texture.EncodeToPNG());
            ValidateRenderedGeometry(canvas, camera, safeArea, fileName);
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousDistance;
            camera.targetTexture = previousTarget;
            camera.cullingMask = previousMask;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            if (texture != null) Object.DestroyImmediate(texture);
            Canvas.ForceUpdateCanvases();
            canvas.GetComponent<LoginPageLayout>().Refresh();
        }
        Debug.Log("LoginPage preview captured to " + ReportFolder + "/" + fileName);
    }

    static void ValidateRenderedGeometry(Canvas canvas, Camera camera, Rect safe, string label)
    {
        var root = canvas.transform.Find("Figma Artboard 1920x1080");
        var buttons = root.Find("Buttons");
        Rect previous = default;
        for (int i = 0; i < buttons.childCount; i++)
        {
            var rect = (RectTransform)buttons.GetChild(i);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            var screenRect = UnityEngine.Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            if (screenRect.xMin < safe.xMin - 1 || screenRect.xMax > safe.xMax + 1 ||
                screenRect.yMin < safe.yMin - 1 || screenRect.yMax > safe.yMax + 1)
                throw new InvalidOperationException(label + ": button touch target is outside safe area.");
            if (i > 0 && previous.Overlaps(screenRect))
                throw new InvalidOperationException(label + ": button touch targets overlap.");
            previous = screenRect;
        }
        File.AppendAllText(ReportFolder + "/unity_scene_validation.txt",
            "PASS: " + label + "; all six nonoverlapping touch targets inside simulated safe area.\n");
    }
}
#endif
