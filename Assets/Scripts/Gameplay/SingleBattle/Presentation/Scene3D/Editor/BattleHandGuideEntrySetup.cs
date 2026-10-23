using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    public static class BattleHandGuideEntrySetup
    {
        private const string ScenePath = "Assets/Scenes/SingleChapterAdventure.unity";
        private const string IconPath = "Assets/Art/UI/BattleConcept/Resources/BattleConcept/Icons/icon_hand_guide.png";

        [MenuItem("Tools/BattleDemo/Install Hand Guide Entry")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open SingleChapterAdventure in Edit mode to save the hand guide entry.");
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            var data = new SerializedObject(hud);
            var host = (SingleBattleSceneAdapter)data.FindProperty("host").objectReferenceValue;
            var pile = host.DrawPile.View.transform;
            var icon = pile.Find("Icon") ?? pile.Find("Safe Area/Icon");
            if (icon == null) throw new InvalidOperationException("The draw pile icon is not bound.");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (sprite == null) throw new InvalidOperationException("Import the hand guide sprite before installing its entry.");
            var button = data.FindProperty("guideEntry").objectReferenceValue as Button;
            if (button == null) button = hud.transform.Find("Hand Guide")?.GetComponent<Button>() ?? hud.transform.Find("Safe Area/Hand Guide")?.GetComponent<Button>();
            if (button == null) button = icon.parent.Find("Hand Guide")?.GetComponent<Button>();
            if (button == null)
            {
                var go = new GameObject("Hand Guide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(go, "创建牌型指南入口");
                go.transform.SetParent(hud.transform.Find("Safe Area") ?? hud.transform, false);
                button = go.GetComponent<Button>();
            }
            Undo.RecordObject(hud, "绑定牌型指南入口");
            Undo.RecordObject(button, "绑定牌型指南点击");
            button.gameObject.layer = icon.gameObject.layer;
            button.gameObject.SetActive(true);
            var image = button.GetComponent<Image>();
            image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = true;
            button.targetGraphic = image; button.interactable = true;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick, hud.OpenHandGuide);
            data.FindProperty("guideEntry").objectReferenceValue = button;
            data.ApplyModifiedProperties();
            hud.RefreshHandGuideEntryLayout();
            EditorUtility.SetDirty(hud); EditorUtility.SetDirty(button); EditorUtility.SetDirty(image);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = button.gameObject;
            Debug.Log("Saved hand guide entry in SingleChapterAdventure: Gameplay/UI/Battle HUD/Hand Guide.");
        }
    }
}
