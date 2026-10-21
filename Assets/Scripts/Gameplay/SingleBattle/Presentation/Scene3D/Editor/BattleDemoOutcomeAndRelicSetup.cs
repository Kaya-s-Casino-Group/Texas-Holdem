using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    public static class BattleDemoOutcomeAndRelicSetup
    {
        private static readonly string[] Ids = { "10001", "10002", "11002", "11003", "11004", "12001", "13001", "13002", "13003", "13004", "14001", "15001" };
        private static readonly string[] Files = { "遗物3", "遗物2", "心连心", "存折", "一串手雷", "反甲", "债券", "遗物1", "遗物4", "替死鬼", "六面骰", "俄罗斯转盘" };
        [MenuItem("工具/卡牌战斗/BattleDemo/接入效果预览与遗物美术")]
        public static void Configure()
        { Configure(SceneManager.GetActiveScene()); }

        public static void Configure(Scene scene)
        {
            if (Application.isPlaying || (scene.path != "Assets/Scenes/BattleDemo.unity" && scene.path != "Assets/Scenes/SingleChapterAdventure.unity"))
                throw new System.InvalidOperationException("Open BattleDemo or SingleChapterAdventure in Edit Mode first.");
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            var bindings = Ids.Select((id, index) => {
                string path = "Assets/Art/UI/Relics/" + Files[index] + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new System.InvalidOperationException("Missing relic art: " + path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 1024;
                importer.filterMode = FilterMode.Bilinear;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                return new BattleDemoRelicArt.Entry { definitionId = id, sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) };
            }).ToArray();
            var art = hud.GetComponent<BattleDemoRelicArt>();
            if (art == null) art = hud.gameObject.AddComponent<BattleDemoRelicArt>();
            art.Configure(bindings); EditorUtility.SetDirty(art);
            var candidate = hud.GetComponentInChildren<HandCandidatePresenter>(true);
            candidate.enabled = false; EditorUtility.SetDirty(candidate);
            foreach (Transform child in candidate.transform) child.gameObject.SetActive(false);
            var rect = (RectTransform)candidate.transform;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset");
            var name = Label("Outcome Hand", rect, font, new Vector2(0, 0), new Vector2(98, 27), 16);
            var title = Label("Outcome Value", rect, font, new Vector2(104, 0), new Vector2(330, 27), 20);
            title.color = new Color32(255, 219, 135, 255);
            title.enableAutoSizing = true; title.fontSizeMin = 12; title.fontSizeMax = 20;
            var details = Label("Outcome Details", rect, font, new Vector2(0, -25), new Vector2(434, 18), 11);
            details.enableWordWrapping = false; details.overflowMode = TextOverflowModes.Ellipsis; details.enableAutoSizing = true; details.fontSizeMin = 9; details.fontSizeMax = 11;
            var popup = rect.Find("Outcome Calculation") as RectTransform;
            if (popup == null) popup = new GameObject("Outcome Calculation", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            popup.SetParent(rect, false); popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0, 0);
            popup.anchoredPosition = new Vector2(0, 52); popup.sizeDelta = new Vector2(360, 150);
            popup.GetComponent<Image>().color = new Color32(25, 23, 31, 250); popup.GetComponent<Image>().raycastTarget = false;
            var math = Label("Calculation Text", popup, font, new Vector2(12, -10), new Vector2(336, 130), 13);
            math.enableWordWrapping = true;
            var preview = candidate.GetComponent<BattleDemoOutcomePreview>();
            if (preview == null) preview = candidate.gameObject.AddComponent<BattleDemoOutcomePreview>();
            preview.Configure(host, name, title, details, math, popup.gameObject); popup.gameObject.SetActive(false);
            candidate.GetComponent<Image>().raycastTarget = true;
            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log(scene.name + ": outcome-first preview and all 12 relic art bindings configured.");
        }
        private static TMP_Text Label(string name, RectTransform parent, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
        {
            var old = parent.Find(name);
            var text = old != null ? old.GetComponent<TMP_Text>() : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.gameObject.SetActive(true); text.rectTransform.SetParent(parent, false); text.gameObject.layer = 5;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.font = font; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color32(242, 230, 203, 255); text.raycastTarget = false;
            text.enableWordWrapping = false; text.text = "";
            return text;
        }
    }

    [CustomEditor(typeof(BattleDemoRelicArt))]
    public sealed class BattleDemoRelicArtEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("按规则 ID 查找图片；未覆盖的遗物沿用旧图集。尺寸与间距只影响当前场景的遗物栏，不改变持有状态。", MessageType.Info);
            var art = (BattleDemoRelicArt)target;
            for (int row = 0; row < art.Entries.Length; row += 4)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var entry in art.Entries.Skip(row).Take(4))
                {
                    EditorGUILayout.BeginVertical(GUILayout.Width(72));
                    if (entry?.sprite != null) GUILayout.Label(entry.sprite.texture, GUILayout.Width(64), GUILayout.Height(64));
                    GUILayout.Label(entry?.definitionId ?? "", EditorStyles.miniLabel); EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
