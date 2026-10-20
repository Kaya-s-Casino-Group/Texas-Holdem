using System;
using System.Linq;
using System.Xml.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleInputRuleAlignmentTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            private readonly TextAsset content;
            public readonly SingleBattleSceneAdapter Host;
            public readonly CardZonePlayHost Hand;
            public readonly HandZoneView HandView;
            public Fixture(int enemyCount = 3)
            {
                var root = New("Input rule host");
                var match = root.AddComponent<CardMatchHost>(); match.ConfigureRuntimeManaged();
                var hand = New("Hand"); HandView = hand.AddComponent<HandZoneView>();
                HandView.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>(); Hand.ConfigureRuntimeBinding(match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), scene);
                var panel = ui.GetComponent<HandActionPanelView>();
                panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(),
                    panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                var actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); actions.Configure(Hand, panel);
                Host = root.AddComponent<SingleBattleSceneAdapter>(); Host.Configure(match, actions, null, null);
                var player = New("Player").AddComponent<PlayerBattleActor>(); player.Configure(Host);
                var enemies = Enumerable.Range(0, enemyCount).Select(index =>
                { var enemy = New("Enemy " + index).AddComponent<EnemyBattleActor>(); enemy.Configure(Host); return enemy; }).ToArray();
                Host.ConfigureActors(new[] { player }, enemies);
                var xml = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml").text);
                var enemyDefinition = xml.Root.Element("Enemies").Elements("Enemy").Single(item => (string)item.Attribute("id") == "rotator");
                enemyDefinition.RemoveNodes(); enemyDefinition.SetAttributeValue("maxHealth", 10000); enemyDefinition.SetAttributeValue("damage", 0);
                xml.Descendants("Loadout").Single(item => (string)item.Attribute("id") == "baseline").RemoveNodes();
                var encounter = xml.Root.Element("Encounters").Elements("Encounter").Single(item => (string)item.Attribute("id") == "standard-single-battle");
                encounter.Elements("Enemy").Remove();
                encounter.Add(Enumerable.Range(0, enemyCount).Select(index => new XElement("Enemy",
                    new XAttribute("id", "enemy-" + index), new XAttribute("ref", "rotator"))));
                content = new TextAsset(xml.ToString()); Host.ConfigureContent(content, "standard-single-battle");
                Editor.BattlePresentationSceneSetup.ConfigureHost(Host, includeFeedback: false); Host.StartBattle();
                HandView.CompleteTransitions();
            }
            private GameObject New(string name)
            { var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value; }
            public void Edit(BattleAdminPatch patch)
            {
                Assert.IsTrue(Host.Playback.ApplyAdmin(Host.AuthorityState.Revision, patch).Accepted);
                HandView.CompleteTransitions();
            }
            public void Cards(params BattleAdminCard[] cards)
            {
                Edit(new BattleAdminPatch(zones: new[] { new BattleAdminZone(Host.Player.ParticipantId, CardZoneKinds.Hand, cards) }));
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Host.RefreshCandidate();
            }
            public void Dispose()
            { Hand.StopSession(); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(content); }
        }
        private static BattleAdminCard Card(int rank, int suit = 1, CardEffects effects = CardEffects.None, bool disabled = false) =>
            new BattleAdminCard(definitionId: "standard:" + suit + ":" + rank, effects: effects, isDisabled: disabled);
        private static BattleAdminCard[] FullHouse() => new[] { Card(2), Card(2, 2), Card(2, 3), Card(3), Card(3, 2) };

        [Test] public void FullHouseRequiresAChoiceThenPreviewAndSubmitUseTheSamePairTarget()
        {
            using (var f = new Fixture())
            {
                f.Cards(FullHouse());
                var slot = f.Host.PlayTargetChoices.Single();
                Assert.IsTrue(slot.Required); Assert.AreEqual(3, slot.Candidates.Count); Assert.IsNull(slot.SelectedId);
                Assert.IsFalse(f.Host.Player.CanPlay(f.Hand.CaptureSelection())); Assert.IsFalse(f.Host.Candidate.CanPlay);
                Assert.AreEqual(CardDuelError.InvalidTarget, f.Host.PreviewSelectedPlay().Error);
                Assert.IsFalse(f.Host.SelectPlayTarget(slot.Slot, f.Host.Player.ParticipantId.Value));
                string chosen = f.Host.Enemies[1].ParticipantId.Value;
                Assert.IsTrue(f.Host.SelectPlayTarget(slot.Slot, chosen));
                Assert.IsTrue(f.Host.Candidate.CanPlay);
                var before = f.Host.AuthorityState;
                var preview = f.Host.PreviewSelectedPlay(); Assert.IsTrue(preview.Accepted);
                Assert.AreSame(before, f.Host.AuthorityState);
                Assert.AreEqual(5, f.Host.Player.Play(f.Hand.CaptureSelection(), false));
                var damage = f.Host.LastPlayResolution.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).ToArray();
                Assert.AreEqual(4, damage.Length); Assert.AreEqual(chosen, damage.Last().Effect.Target.Value);
                foreach (var enemy in f.Host.Enemies)
                    Assert.AreEqual(preview.State.Duel.Combat.Get(enemy.ParticipantId).Health, f.Host.AuthorityState.Duel.Combat.Get(enemy.ParticipantId).Health);
            }
        }

        [Test] public void OneLivingCandidateIsSelectedAutomaticallyAndDeadChoicesAreRemoved()
        {
            using (var f = new Fixture())
            {
                f.Cards(FullHouse());
                var choice = f.Host.PlayTargetChoices.Single();
                Assert.IsTrue(f.Host.SelectPlayTarget(choice.Slot, f.Host.Enemies[1].ParticipantId.Value));
                f.Edit(new BattleAdminPatch(vitals: new[] {
                    new BattleAdminVitals(f.Host.Enemies[0].ParticipantId, health: 0),
                    new BattleAdminVitals(f.Host.Enemies[1].ParticipantId, health: 0) }));
                f.Cards(FullHouse());
                Assert.AreEqual(f.Host.Enemies[2].ParticipantId.Value, f.Host.PlayTargetChoices.Single().SelectedId);
                Assert.IsTrue(f.Host.PreviewSelectedPlay().Accepted);
                Assert.AreEqual(5, f.Host.Player.Play(f.Hand.CaptureSelection(), false));
            }
        }

        [Test] public void StraightStartsAtChosenEnemyAndTheRulesRetargetLowestHealthAfterDeath()
        {
            using (var f = new Fixture())
            {
                f.Edit(new BattleAdminPatch(vitals: new[] {
                    new BattleAdminVitals(f.Host.Enemies[0].ParticipantId, health: 2),
                    new BattleAdminVitals(f.Host.Enemies[1].ParticipantId, health: 3),
                    new BattleAdminVitals(f.Host.Enemies[2].ParticipantId, health: 1) }));
                f.Cards(Card(2), Card(3, 2), Card(4, 3), Card(5, 4), Card(6));
                var slot = f.Host.PlayTargetChoices.Single();
                Assert.IsTrue(f.Host.SelectPlayTarget(slot.Slot, f.Host.Enemies[1].ParticipantId.Value));
                Assert.AreEqual(5, f.Host.Player.Play(f.Hand.CaptureSelection(), false));
                var damage = f.Host.LastPlayResolution.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).ToArray();
                CollectionAssert.AreEqual(new[] { f.Host.Enemies[1].ParticipantId, f.Host.Enemies[2].ParticipantId, f.Host.Enemies[0].ParticipantId },
                    damage.Take(3).Select(item => item.Effect.Target));
            }
        }

        [Test] public void UnplayableCardCanBeSelectedAndDiscardedButDisabledCardCannot()
        {
            using (var f = new Fixture(1))
            {
                f.Cards(Card(2, effects: CardEffects.Unplayable));
                string junk = f.Hand.Cards.Single().Id;
                Assert.IsTrue(f.Hand.CanSelect(junk)); Assert.IsFalse(f.Host.Player.CanPlay(f.Hand.CaptureSelection()));
                Assert.IsFalse(f.Host.Candidate.CanPlay); Assert.IsTrue(f.Host.Candidate.CanDiscard);
                Assert.IsTrue(f.Host.PreviewSelectedDiscard().Accepted);
                Assert.AreEqual(1, f.Hand.DiscardSelected(false));
                Assert.IsTrue(f.Host.PlayerZone(CardZoneKinds.Discard).Cards.Any(id => id.Value == junk));
                Assert.AreEqual(1, f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Count, "Discard replaces only the discarded card.");
                f.HandView.CompleteTransitions();
                f.Cards(Card(3, disabled: true));
                Assert.IsFalse(f.Hand.CanSelect(f.Hand.Cards.Single().Id));
                Assert.IsFalse(f.Host.Player.CanDiscard(f.Hand.CaptureSelection()));
            }
        }
    }
}
