using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PalamedesAnimationPreviewWindow : EditorWindow
{
    [SerializeField]
    Animator target;
    [SerializeField]
    AnimationClip selectedClip;
    [SerializeField]
    string folder = PalamedesAnimationLibrary.SourceFolder;
    [SerializeField]
    string search = "";
    [SerializeField]
    int category, variant;
    [SerializeField]
    bool loop = true, dynamics = true, footIK;
    [SerializeField]
    bool showLegContacts = true;
    [SerializeField]
    float speed = 1;
    Vector2 scroll;
    AnimationClip[] clips = Array.Empty<AnimationClip>();
    PalamedesSceneAnimationPreview preview;
    bool playing;
    double previousTime, accumulator;
    string error;
    static readonly string[] Categories =
    {
        "全部动作",
        "地面 Ground",
        "飞行 Flying",
        "其他"
    };
    static readonly string[] Variants =
    {
        "全部版本",
        "原地 Inplace",
        "原始版本"
    };
    const float Step = 1f / 60;
    [MenuItem("Tools/Palamedes/Animation Scene Preview")]
    public static void Open()
    {
        var window = GetWindow<PalamedesAnimationPreviewWindow>("角色动画预览");
        window.minSize = new Vector2(460, 500);
        if (!window.target)
            window.target = PalamedesAnimationLibrary.FindTarget();
        window.RefreshClips();
        window.Show();
    }

    void OnEnable()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += EndPreview;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorApplication.quitting += EndPreview;
        EditorSceneManager.sceneClosing += SceneClosing;
        EditorSceneManager.sceneSaving += SceneSaving;
        Undo.undoRedoPerformed += EndPreview;
        SceneView.duringSceneGui += DrawPreviewContacts;
        previousTime = EditorApplication.timeSinceStartup;
        RefreshClips();
    }

    void OnDisable()
    {
        EndPreview();
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= EndPreview;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        EditorApplication.quitting -= EndPreview;
        EditorSceneManager.sceneClosing -= SceneClosing;
        EditorSceneManager.sceneSaving -= SceneSaving;
        Undo.undoRedoPerformed -= EndPreview;
        SceneView.duringSceneGui -= DrawPreviewContacts;
    }

    void PlayModeChanged(PlayModeStateChange state)
    {
        EndPreview();
    }

    void SceneClosing(Scene scene, bool removing)
    {
        EndPreview();
    }

    void SceneSaving(Scene scene, string path)
    {
        EndPreview();
    }

    void RefreshClips()
    {
        clips = PalamedesAnimationLibrary.FindClips(folder);
        if (!selectedClip || !clips.Contains(selectedClip))
            selectedClip = PalamedesAnimationLibrary.DefaultClip(clips);
        Repaint();
    }

    bool ValidTarget => target && target.gameObject.scene.IsValid() && target.gameObject.activeInHierarchy && !EditorUtility.IsPersistent(target) && target.avatar && target.avatar.isValid && target.avatar.isHuman;

    void OnGUI()
    {
        EditorGUILayout.Space(5);
        using (new EditorGUILayout.HorizontalScope())
        {
            var next = (Animator)EditorGUILayout.ObjectField("场景角色", target, typeof(Animator), true);
            if (next != target)
            {
                EndPreview();
                target = next;
            }

            if (GUILayout.Button("当前选择", GUILayout.Width(76)))
            {
                EndPreview();
                target = PalamedesAnimationLibrary.FindTarget();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
            var next = (DefaultAsset)EditorGUILayout.ObjectField("动画目录", asset, typeof(DefaultAsset), false);
            if (next != asset && next && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(next)))
            {
                EndPreview();
                folder = AssetDatabase.GetAssetPath(next);
                RefreshClips();
            }

            if (GUILayout.Button("刷新", GUILayout.Width(76)))
                RefreshClips();
        }

        EditorGUILayout.LabelField("Humanoid 重定向", ValidTarget ? $"已使用 {target.avatar.name} · {clips.Length} 个动画" : "请选择场景中具有有效 Humanoid Avatar 的角色");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("请退出 Play 模式后使用 Scene 预览。", MessageType.Info);
        if (!string.IsNullOrEmpty(error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
        using (new EditorGUI.DisabledScope(!ValidTarget || !selectedClip || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "暂停" : "▶ 播放", GUILayout.Height(28)))
                    Run(() =>
                    {
                        if (preview == null)
                            BeginPreview();
                        playing = !playing;
                        previousTime = EditorApplication.timeSinceStartup;
                    });
                if (GUILayout.Button("■ 结束预览", GUILayout.Height(28)))
                    EndPreview();
                if (GUILayout.Button("定位角色", GUILayout.Height(28)))
                    FrameTarget();
            }

            EditorGUILayout.LabelField(selectedClip ? PalamedesAnimationLibrary.Label(selectedClip) : "未选择动画", EditorStyles.boldLabel);
            float length = selectedClip ? selectedClip.length : 0;
            float time = preview?.Time ?? 0;
            float nextTime = EditorGUILayout.Slider("进度（秒）", time, 0, Mathf.Max(length, .001f));
            if (Mathf.Abs(nextTime - time) > .00001f)
                Seek(nextTime);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ 上一帧"))
                    Seek(time - 1 / Mathf.Max(1, selectedClip.frameRate));
                if (GUILayout.Button("下一帧 ▶"))
                    Seek(time + 1 / Mathf.Max(1, selectedClip.frameRate));
                loop = GUILayout.Toggle(loop, "循环", GUILayout.Width(55));
                speed = EditorGUILayout.Slider(speed, .1f, 2f);
                GUILayout.Label(speed.ToString("F2") + "×", GUILayout.Width(44));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string dynamicsLabel = target && GetPanels(target.gameObject).Any(c => c.partName == "FrontPanel" || c.partName == "SidePanel") ? "披风 / 小裙片动态" : "披风动态";
                bool next = GUILayout.Toggle(dynamics, dynamicsLabel);
                bool ik = GUILayout.Toggle(footIK, "脚部 IK");
                if (next != dynamics || ik != footIK)
                {
                    dynamics = next;
                    footIK = ik;
                    if (preview != null)
                    {
                        preview.Dynamics = dynamics;
                        preview.FootIK = footIK;
                        Seek(time);
                    }
                }
            }
        }

        EditorGUILayout.HelpBox("直接在 Scene 中原地预览，无需运行游戏。拖动进度会重置次级动态；连续播放可观察惯性。结束预览或关闭窗口后恢复场景角色。", MessageType.None);
        if (target && GetPanels(target.gameObject).Any(c => c.legColliders != null && c.legColliders.Length > 0))
        {
            bool draw = EditorGUILayout.ToggleLeft("显示预览腿部碰撞范围（橙色）", showLegContacts);
            if (draw != showLegContacts)
            {
                showLegContacts = draw;
                SceneView.RepaintAll();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);
            category = EditorGUILayout.Popup(category, Categories, GUILayout.Width(120));
            variant = EditorGUILayout.Popup(variant, Variants, GUILayout.Width(110));
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var clip in clips)
        {
            if (!Matches(clip))
                continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = clip == selectedClip;
                if (GUILayout.Toggle(selected, PalamedesAnimationLibrary.Label(clip), "Button") && !selected)
                    SelectClip(clip);
                GUILayout.Label(clip.length.ToString("F2") + " s", GUILayout.Width(58));
                if (GUILayout.Button("资源", GUILayout.Width(42)))
                    EditorGUIUtility.PingObject(clip);
            }
        }

        EditorGUILayout.EndScrollView();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("打开 Scene", GUILayout.Width(100)))
                GetWindow<SceneView>();
        }
    }

    bool Matches(AnimationClip clip)
    {
        string name = clip.name;
        if (name.IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) < 0)
            return false;
        bool ground = name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0;
        bool flying = name.IndexOf("Flying", StringComparison.OrdinalIgnoreCase) >= 0;
        if (category == 1 && !ground || category == 2 && !flying || category == 3 && (ground || flying))
            return false;
        bool inplace = AssetDatabase.GetAssetPath(clip).IndexOf("/Inplace/", StringComparison.OrdinalIgnoreCase) >= 0 || name.EndsWith("_inplace", StringComparison.OrdinalIgnoreCase);
        return variant == 0 || variant == 1 && inplace || variant == 2 && !inplace;
    }

    void BeginPreview()
    {
        EndPreview();
        error = null;
        if (!ValidTarget || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请在编辑模式选择有效角色。");
        preview = new PalamedesSceneAnimationPreview(target)
        {
            Dynamics = dynamics,
            FootIK = footIK
        };
        preview.SetClip(selectedClip);
        accumulator = 0;
        SceneView.RepaintAll();
    }

    void SelectClip(AnimationClip clip)
    {
        bool resume = playing;
        selectedClip = clip;
        Run(() =>
        {
            if (preview == null)
                BeginPreview();
            else
                preview.SetClip(clip);
            playing = resume;
            accumulator = 0;
            previousTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        });
    }

    void Seek(float time)
    {
        Run(() =>
        {
            if (preview == null)
                BeginPreview();
            playing = false;
            accumulator = 0;
            preview.Sample(time, 0, true);
            SceneView.RepaintAll();
            Repaint();
        });
    }

    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        double elapsed = Math.Min(.1, Math.Max(0, now - previousTime));
        previousTime = now;
        if (preview != null && (!target || !preview.Root || !ValidTarget))
        {
            EndPreview();
            return;
        }

        if (!playing || preview == null || !selectedClip)
            return;
        Run(() =>
        {
            accumulator += elapsed * speed;
            while (accumulator >= Step)
            {
                float time = preview.Time + Step;
                if (time > selectedClip.length)
                {
                    if (loop)
                        time = Mathf.Repeat(time, Mathf.Max(.001f, selectedClip.length));
                    else
                    {
                        preview.Sample(selectedClip.length, Step, false);
                        playing = false;
                        accumulator = 0;
                        break;
                    }
                }

                preview.Sample(time, Step, false);
                accumulator -= Step;
            }

            SceneView.RepaintAll();
            Repaint();
        });
    }

    public void EndPreview()
    {
        playing = false;
        accumulator = 0;
        preview?.Dispose();
        preview = null;
        SceneView.RepaintAll();
        Repaint();
    }

    void FrameTarget()
    {
        var root = preview?.Root ?? (target ? target.gameObject : null);
        if (!root)
            return;
        var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        if (renderers.Length == 0)
            return;
        var bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
        view.Frame(bounds, false);
    }

    void Run(Action action)
    {
        try
        {
            action();
            error = null;
        }
        catch (Exception e)
        {
            EndPreview();
            error = e.Message;
            Debug.LogException(e);
        }
    }

    static PalamedesPanelSettings[] GetPanels(GameObject root)
    {
        var cloth = root ? root.GetComponent<PalamedesClothDynamics>() : null;
        return cloth && cloth.enabled && cloth.panels != null
            ? cloth.panels.Where(p => p != null && p.enabled).ToArray()
            : Array.Empty<PalamedesPanelSettings>();
    }

    void DrawPreviewContacts(SceneView view)
    {
        if (!showLegContacts || preview == null || !preview.Root)
            return;
        var contacts = GetPanels(preview.Root).Where(c => c.enableLegCollision && c.legColliders != null).SelectMany(c => c.legColliders.Select(collider => (collider, c.collisionClearance))).Where(p => p.collider).GroupBy(p => p.collider).Select(g => g.First()).ToArray();
        if (contacts.Length == 0)
            return;
        var color = Handles.color;
        var depth = Handles.zTest;
        Handles.color = new Color(1, .6f, .12f, .85f);
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        try
        {
            foreach (var contact in contacts)
            {
                var c = contact.collider;
                if (!c)
                    continue;
                var scale = c.transform.lossyScale;
                int d = c.direction;
                float radial = d == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) : d == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                var axis = c.transform.TransformDirection(d == 0 ? Vector3.right : d == 1 ? Vector3.up : Vector3.forward).normalized;
                float radius = c.radius * radial + contact.collisionClearance;
                float half = Mathf.Max(0, c.height * Mathf.Abs(scale[d]) * .5f - c.radius * radial);
                var center = c.transform.TransformPoint(c.center);
                var a = center - axis * half;
                var b = center + axis * half;
                var u = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > .9f ? Vector3.forward : Vector3.up).normalized;
                var v = Vector3.Cross(axis, u);
                Handles.DrawWireDisc(a, axis, radius);
                Handles.DrawWireDisc(b, axis, radius);
                foreach (var radialAxis in new[]
                {
                    u,
                    -u,
                    v,
                    -v
                }

                )
                    Handles.DrawLine(a + radialAxis * radius, b + radialAxis * radius);
                Handles.DrawWireArc(a, -u, v, 180, radius);
                Handles.DrawWireArc(b, u, v, 180, radius);
                Handles.DrawWireArc(a, v, u, 180, radius);
                Handles.DrawWireArc(b, -v, u, 180, radius);
            }
        }
        finally
        {
            Handles.color = color;
            Handles.zTest = depth;
        }
    }
}
