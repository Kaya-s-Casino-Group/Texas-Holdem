#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using global::BattlePresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Adventure.Tests
{
    /// <summary>Real seven-stop input must survive consuming an item after an earlier play created round memory.</summary>
    public sealed class AdventureItemRoundMemoryTests
    {
        private readonly List<string> errors = new List<string>();
        private string temporaryScene;
        private bool previousLogPolicy, previousRunInBackground;
        private AdventureSceneHost host;

        [UnitySetUp]
        public IEnumerator LoadSceneWithoutUserSaves()
        {
            errors.Clear();
            previousLogPolicy = LogAssert.ignoreFailingMessages;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CaptureError;
            temporaryScene = "Assets/Scenes/__ItemRoundMemory_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", temporaryScene));
            string yaml = File.ReadAllText(temporaryScene);
            Assert.That(yaml, Does.Contain("saveAutomatically: 1"));
            File.WriteAllText(temporaryScene, yaml.Replace("saveAutomatically: 1", "saveAutomatically: 0"));
            AssetDatabase.ImportAsset(temporaryScene, ImportAssetOptions.ForceSynchronousImport);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(temporaryScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            host = Object.FindObjectsOfType<AdventureSceneHost>().Single();
            Assert.IsNotNull(host.Definition, host.Message);
            Assert.IsFalse(host.HasSavedRun, "This scene copy must neither read nor write the user's run save.");
            Assert.IsNotNull(EventSystem.current);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (!string.IsNullOrEmpty(temporaryScene))
            {
                var loaded = SceneManager.GetSceneByPath(temporaryScene);
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Item round memory cleanup"));
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                AssetDatabase.DeleteAsset(temporaryScene);
                temporaryScene = null;
            }
            Application.logMessageReceived -= CaptureError;
            LogAssert.ignoreFailingMessages = previousLogPolicy;
            Application.runInBackground = previousRunInBackground;
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        private void CaptureError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message == "[MCP Unity] WebSocket error: An error has occurred in closing the connection.") return;
            errors.Add(message + "\n" + stack);
        }

        [UnityTest]
        public IEnumerator LastPlayWithRoundMemoryConsumesReviverAndRestoresPlayerInput()
        {
            yield return VerifyLastAction(false);
        }

        [UnityTest]
        public IEnumerator LastDiscardWithRoundMemoryConsumesReviverAndRestoresPlayerInput()
        {
            yield return VerifyLastAction(true);
        }

        [UnityTest]
        public IEnumerator RepeatedDeadPortraitSnapshotsLetHitAndDeathAnimationsFinish()
        {
            Assert.IsTrue(host.NewRun("fool", 312), host.Message);
            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "battle-1")), host.Message);
            yield return Idle();
            var actor = host.BattleView.Player;
            var portrait = actor.Visual.GetComponent<PortraitBattleActorPresenter>();
            Assert.IsNotNull(portrait, "Exercise the actual seven-stop player portrait, including its phase transitions.");
            var alive = actor.DisplayUnit;
            var dead = new UnitDisplayData(alive.Id, alive.Name, "player", 0, 144, 0, 3, 3);
            var lethal = new EffectDisplayData("Damage", host.BattleView.Enemies.Single().DisplayId, alive.Id,
                144, 144, 0, alive, dead, true);
            var artwork = actor.Visual.Find("Artwork");
            var restPosition = artwork.localPosition;
            try
            {
                foreach (var kind in new[] { BattlePresentationKind.Hit, BattlePresentationKind.Death })
                {
                    portrait.ResetPresentation();
                    portrait.PresentPose("action", false);
                    portrait.PresentPose("guard");
                    portrait.Advance(.03f);
                    Assert.IsTrue(portrait.IsChangingPose);
                    var operation = portrait.Play(new BattlePresentationCue(kind, alive.Id, lethal));
                    Assert.IsTrue(operation.keepWaiting, "The authored animation must be in flight when its checkpoint is revealed.");
                    portrait.SynchronizeDisplay(dead);
                    portrait.SynchronizeDisplay(dead);
                    Assert.AreEqual(BattlePresentationStatus.Pending, operation.Status,
                        "Revealing the same dead snapshot must not cancel the timeline's " + kind + " animation.");
                    Assert.IsFalse(portrait.IsChangingPose);
                    Assert.AreEqual(1, portrait.Opacity);
                    Assert.AreEqual(restPosition, artwork.localPosition);
                    float deadline = Time.realtimeSinceStartup + 3;
                    while (operation.keepWaiting && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.AreEqual(BattlePresentationStatus.Completed, operation.Status, kind + " must finish naturally.");
                }
            }
            finally { portrait.ResetPresentation(); }
        }

        private IEnumerator VerifyLastAction(bool discard)
        {
            yield return ReachBoss();
            var view = host.BattleView;
            var player = host.Adventure.PlayerId;
            var body = view.Enemies.Single(actor => actor.ParticipantId == WrathBossQuery.Read(view.State.Duel).BodyId);
            var patch = new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(player, health: 1, maxHealth: 144, block: 0) },
                relics: new[] { new BattleAdminRelics(player, new[] { new BattleAdminRelic("10001"), new BattleAdminRelic("13004") }) },
                zones: new[] { new BattleAdminZone(player, CardZoneKinds.Hand, new[] {
                    "standard:1:2", "standard:2:3", "standard:3:4", "standard:4:5",
                    "standard:1:6", "standard:2:7", "standard:3:8", "standard:4:9"
                }.Select(id => new BattleAdminCard(definitionId: id))) });
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, patch).Accepted, host.Message);
            yield return Idle();
            Assert.IsTrue(view.GetComponent<BattleDemoTargetSelection>().ChooseTarget(body));

            // These are actual UI commands, not an injected memory counter. The third action ends the turn.
            for (int i = 0; i < 2; i++)
            {
                SelectFirstCard();
                yield return ClickButton(view.Actions.Panel.PlayButton);
                yield return Idle();
            }
            var before = host.Session.State;
            Assert.AreEqual(1, before.FlowFor(player).ActionsRemaining);
            Assert.IsTrue(before.Duel.Player(player).Relics.Relics.Single(relic => relic.DefinitionId == "10001")
                .Memory.Counters.Any(counter => counter.Scope == MemoryScope.Round), "Earlier actual plays must create the failing precondition.");
            var restored = BattleAdventureSession.Restore(host.Definition, host.Content, host.Adventure.Export());
            Assert.AreEqual(1, restored.Battle.State.FlowFor(player).ActionsRemaining);
            Assert.IsTrue(restored.Battle.State.Duel.Player(player).Relics.Relics.Single(relic => relic.DefinitionId == "10001")
                .Memory.Counters.Any(counter => counter.Scope == MemoryScope.Round), "Replay must retain round memory until the round ends.");

            var intent = view.Engine.Duel.PreviewEnemyAction(before.Duel, body.ParticipantId);
            Assert.IsNotNull(intent);
            Assert.IsTrue(intent.Accepted, "Lethal enemy intent must preview the reviver: " + intent.Error);
            Assert.IsTrue(intent.RuleEvents.OfType<CombatEvent>().Any(fact => fact.Effect.Kind == CombatEffectKind.Revive));
            Assert.AreSame(before, host.Session.State, "Enemy preview must not consume the live item or advance the battle.");
            Assert.IsTrue(host.Adventure.Relics.Any(relic => relic.DefinitionId == "13004"));

            SelectFirstCard();
            Assert.IsTrue(view.PreviewSelectedPlay().Accepted, "The last play must be available before the lethal enemy phase.");
            Assert.IsTrue(view.PreviewSelectedDiscard().Accepted, "The last discard must be available before the lethal enemy phase.");
            long revision = host.Revision;
            yield return ClickButton(discard ? view.Actions.Panel.DiscardButton : view.Actions.Panel.PlayButton);
            yield return Idle();
            Assert.AreEqual(revision + 1, host.Revision, "The button submits one atomic adventure command, including automatic enemies.");
            Assert.AreEqual(AdventurePhase.Battle, host.Adventure.Phase);
            Assert.AreEqual(86, host.Session.State.Duel.Combat.Get(player).Health, "floor(144 × 0.6); later enemies must not attack after revival.");
            Assert.IsFalse(host.Adventure.Relics.Any(relic => relic.DefinitionId == "13004"), "Exactly the used reviver must leave the run loadout.");
            Assert.AreEqual(1, host.Adventure.Relics.Count);
            Assert.AreEqual("10001", host.Adventure.Relics.Single().DefinitionId);
            Assert.AreEqual(1, host.Session.State.Duel.CompletedRounds);
            Assert.AreEqual(3, host.Session.State.FlowFor(player).ActionsRemaining);
            Assert.AreEqual(ScoredDuelPhase.AwaitingPlayerHand, host.Session.State.Duel.Phase);
            Assert.IsTrue(view.Player.CanInteract);

            // Check real input after revival, rather than only inspecting a phase enum.
            SelectFirstCard();
            // The action panel observes the new selection in HandActionPresenter.LateUpdate.
            yield return null;
            Assert.IsTrue(view.Actions.Panel.PlayButton.interactable);
            Assert.IsTrue(view.Actions.Panel.DiscardButton.interactable);
            yield return ClickButton(view.Actions.Panel.PlayButton);
            yield return Idle();
            Assert.AreEqual(2, host.Session.State.FlowFor(player).ActionsRemaining);
            Assert.AreEqual(86, host.Session.State.Duel.Combat.Get(player).Health);
        }

        private IEnumerator ReachBoss()
        {
            Assert.IsTrue(host.NewRun("fool", 312), host.Message);
            foreach (string node in new[] { "battle-1", "chest", "battle-2", "rest-1", "elite", "rest-2", "boss" })
            {
                Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, node)), host.Message);
                yield return Idle();
                if (node == "boss") yield break;
                if (host.Adventure.Phase == AdventurePhase.Battle)
                {
                    var enemies = host.Session.State.Duel.Enemies.Select(enemy => new BattleAdminVitals(enemy.Id, health: 0)).ToArray();
                    Assert.IsTrue(host.BattleView.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(vitals: enemies)).Accepted, host.Message);
                    yield return Idle();
                }
                while (host.Adventure.PendingRelicRewards.Count > 0)
                    Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.SkipRelicReward)), host.Message);
                Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), host.Message);
                yield return Idle();
            }
            Assert.Fail("The authored adventure did not reach its boss node.");
        }

        private void SelectFirstCard()
        {
            var view = host.BattleView;
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, default);
            var selected = hand.Cards.First(card => card.Card.CanPlay && card.Card.CanDiscard);
            hand.SetInteraction(selected.Id, new CardInteractionState(isSelected: true));
            hand.Publish();
            hand.GetComponent<HandZoneView>().CompleteTransitions();
            view.RefreshCandidate();
        }

        private IEnumerator ClickButton(Button button)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(button.isActiveAndEnabled && button.interactable, "The real action button must be enabled: " + button.name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left, clickCount = 1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "The action button must be reachable through the scene's UI raycasters.");
            var clickable = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.AreEqual(button.gameObject, clickable, "Another UI surface blocks the action button.");
            ExecuteEvents.Execute(clickable, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private IEnumerator Idle()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 25;
            while (host.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsBusy, "The adventure presentation did not restore input before the deadline.");
            Assert.IsEmpty(host.Message, host.Message);
            if (host.Adventure?.Phase == AdventurePhase.Battle)
            {
                Assert.IsNull(host.BattleView.PresentationDirector.LastError);
                Assert.AreSame(host.Session.State, host.BattleView.PresentedState);
            }
            yield return null;
        }
    }
}
#endif
