#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public static class CardMatchSetup
    {
        public static CardMatchHost GetOrCreate(Scene scene)
        {
            var match = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardMatchHost>(true)).FirstOrDefault();
            if (match != null) return match;
            var root = new GameObject("Card Match"); SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "创建局内对局"); match = Undo.AddComponent<CardMatchHost>(root);
            match.ConfigureInitial(Array.Empty<CardMatchZoneSnapshot>()); return match;
        }
        [MenuItem("工具/卡牌表现/场景接入/迁移牌桌状态")]
        public static void MigrateCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("退出 Play 后迁移局内对局。");
            var scene = SceneManager.GetActiveScene();
            var records = new List<Record>();
            foreach (var host in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true)))
                if (host.TryReadLegacy(out var zone, out var deck, out var discard)) records.Add(new Record { Host = host, Zone = zone, Deck = deck, Discard = discard });
            if (records.Count == 0) { CardZoneValidationWindow.RefreshMatchInCurrentWindow(); return; }
            var fullDecks = records.Where(record => record.Deck.Count > 0).ToArray();
            var snapshots = records.Select(record => record.Zone).ToList();
            var discarded = records.SelectMany(record => record.Discard).ToArray();
            snapshots.Add(Snapshot("discard", CardZoneKinds.Discard, discarded));
            if (fullDecks.Length <= 1) snapshots.Add(Snapshot("deck", CardZoneKinds.Deck, fullDecks.FirstOrDefault()?.Deck ?? Array.Empty<CardZoneCardState>()));
            else foreach (var record in fullDecks) snapshots.Add(Snapshot(record.Zone.Zone.Id.Value + "/deck", CardZoneKinds.Deck, record.Deck));
            // Validate the full migration before creating an object or changing any binding.
            new CardTableState(new MatchId("migration-check"), snapshots.SelectMany(zone => zone.Cards).Select(card => card.Card), snapshots.Select(zone => zone.Zone));
            var match = GetOrCreate(scene); Undo.RecordObject(match, "迁移卡牌到对局");
            if (match.InitialState.Zones.Count != 0) throw new InvalidOperationException("已有对局包含牌区，请先明确迁移目标，避免覆盖。");
            match.ConfigureInitial(snapshots);
            foreach (var record in records)
            {
                Undo.RecordObject(record.Host, "绑定对局牌区");
                string deckId = fullDecks.Length <= 1 ? "deck" : (record.Deck.Count > 0 ? record.Zone.Zone.Id.Value + "/deck" : fullDecks[0].Zone.Zone.Id.Value + "/deck");
                record.Host.Bind(match, record.Zone.Zone.Id, new ZoneId(deckId), new ZoneId("discard"), record.Host.GetComponent<CardZoneView>().FacingCamera);
                record.Host.ClearLegacy(); EditorUtility.SetDirty(record.Host);
            }
            EditorUtility.SetDirty(match); EditorSceneManager.MarkSceneDirty(scene);
            CardZoneValidationWindow.RefreshMatchInCurrentWindow();
            Debug.Log("Card Match now owns " + match.State.Cards.Count + " cards across " + match.State.Zones.Count + " zones. Scene remains unsaved.");
        }
        private static CardMatchZoneSnapshot Snapshot(string id, string kind, IEnumerable<CardZoneCardState> states)
        {
            var cards = states.ToArray(); return new CardMatchZoneSnapshot(new CardZone(new ZoneId(id), kind, null, cards.Select(card => card.Card.Id)), cards);
        }
        private sealed class Record
        { public CardZonePlayHost Host; public CardMatchZoneSnapshot Zone; public IReadOnlyList<CardZoneCardState> Deck, Discard; }
    }
}
#endif
