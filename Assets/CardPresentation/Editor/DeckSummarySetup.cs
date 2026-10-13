#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    public static class DeckSummarySetup
    {
        public const string PrefabPath = "Assets/CardPresentation/Prefabs/DeckSummary.prefab";
        [MenuItem("Tools/Cards/Prepare Deck Hover Summary Prefab")]
        public static void Prepare()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                if (existing.GetComponentsInChildren<PixelPanelGraphic>(true).All(panel => panel.GetComponent<CanvasRenderer>() != null)) return;
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    foreach (var panel in contents.GetComponentsInChildren<PixelPanelGraphic>(true))
                        if (panel.GetComponent<CanvasRenderer>() == null) panel.gameObject.AddComponent<CanvasRenderer>();
                    PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardTooltipPrefabBuilder.FontPath);
            if (font == null) throw new InvalidOperationException("Prepare Fusion Pixel first.");
            var ink = new Color32(17,38,35,255); var cream = new Color32(224,232,214,255);
            var root = Rect("Deck Hover Summary",null,Vector2.zero,new Vector2(896,272));
            try
            {
                root.anchorMin = root.anchorMax = root.pivot = new Vector2(1,0);
                root.gameObject.AddComponent<CanvasGroup>();
                Panel("Shadow",root,new Vector2(2,-4),root.sizeDelta,new Color32(0,0,0,120),Color.clear,0);
                Panel("Frame",root,Vector2.zero,root.sizeDelta,ink,cream,3);
                var ranks = new TMP_Text[13]; var suits = new TMP_Text[4]; var grid = new TMP_Text[52];
                string[] names = { "A","K","Q","J","10","9","8","7","6","5","4","3","2" };
                float step = 720f/13;
                for (int i = 0; i < 13; i++)
                {
                    float x = -288+(i+.5f)*step;
                    var tile = Panel("Rank "+names[i],root,new Vector2(x,85),new Vector2(step-5,70),cream,new Color32(153,176,161,255),2);
                    Label("Rank",tile,new Vector2(0,15),new Vector2(step-6,32),names[i],font,28).color = ink;
                    var token = Panel("Total",tile,new Vector2(0,-20),new Vector2(step-6,24),new Color32(34,69,59,255),ink,0);
                    ranks[i] = Label("Count",token,Vector2.zero,token.sizeDelta,"0",font,20);
                }
                var colors = new Color[] { new Color32(41,44,102,255),new Color32(155,33,58,255),new Color32(18,77,66,255),new Color32(173,73,32,255) };
                var symbols = new[] { "♠","♥","♣","♦" };
                for (int i = 0; i < 4; i++)
                {
                    float y = 24-i*40;
                    var row = Panel("Suit Row "+DeckOverviewView.SuitOrder[i],root,new Vector2(72,y),new Vector2(720,34),colors[i],Color.clear,0);
                    var total = Panel("Suit Total "+i,root,new Vector2(-366,y),new Vector2(140,34),cream,cream,1);
                    var icon = Label("Suit",total,new Vector2(-48,0),new Vector2(36,32),symbols[i],font,28);
                    icon.color = i == 1 ? new Color32(215,37,58,255) : i == 3 ? new Color32(227,86,26,255) : ink;
                    var badge = Panel("Total",total,new Vector2(17,0),new Vector2(98,28),new Color32(34,69,59,255),ink,0);
                    suits[i] = Label("Count",badge,Vector2.zero,badge.sizeDelta,"0",font,24);
                    for (int rank = 0; rank < 13; rank++) grid[i*13+rank] = Label("Count "+names[rank],row,
                        new Vector2(-360+(rank+.5f)*step,0),new Vector2(step,34),"0",font,24);
                }
                var other = Label("Other",root,new Vector2(-366,85),new Vector2(140,64),"",font,16);
                root.gameObject.AddComponent<DeckSummaryView>().Configure(ranks,suits,grid,other);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject,PrefabPath);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false); rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color fill, Color outline, float border)
        {
            var rect = Rect(name,parent,position,size); rect.gameObject.AddComponent<PixelPanelGraphic>().Configure(fill,outline,border); return rect;
        }
        private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, TMP_FontAsset font, float fontSize)
        {
            var label = Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = fontSize; label.text = text; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = false; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = fontSize; return label;
        }
    }
}
#endif
