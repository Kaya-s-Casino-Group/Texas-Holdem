#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardInteraction;
using Poker.BattleHosting;
using Poker.BattlePresentation;
using UnityEditor;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using global::BattlePresentation;
using global::BattlePresentation.Unity;

namespace Poker.SingleBattle.Tests
{
    public sealed class SceneTimelinePlaybackTests
    {
        private readonly List<string> errors = new List<string>();
        private bool previousLogPolicy;
        private bool previousRunInBackground;
        [SetUp] public void CaptureProductErrors()
        {
            errors.Clear(); previousLogPolicy = LogAssert.ignoreFailingMessages;
            previousRunInBackground = Application.runInBackground; Application.runInBackground = true;
            // The editor connector reconnects during scene loading. Still fail on every product error.
            LogAssert.ignoreFailingMessages = true; Application.logMessageReceived += CaptureError;
        }
        private void CaptureError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message == "[MCP Unity] WebSocket error: An error has occurred in closing the connection.") return;
            errors.Add(message + "\n" + stack);
        }
        [TearDown] public void CheckProductErrors()
        {
            Application.logMessageReceived -= CaptureError;
            LogAssert.ignoreFailingMessages = previousLogPolicy;
            Application.runInBackground = previousRunInBackground;
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest]
        public IEnumerator AuthoredSceneUsesTimelineForOpeningSelectionPlayDiscardAndCancellation()
        {
            var resultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BattleRuleAlignment/scene-flow.txt"));
            if (File.Exists(resultPath)) File.Delete(resultPath);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            Assert.AreEqual("Assets/GameAssets/SingleBattle/Presentation/Scene3D/BattleDemoValidation.xml",
                AssetDatabase.GetAssetPath(host.ContentXml));
            Assert.AreSame(host.SceneHost, host.GetComponent<BattleSceneHost>());
            Assert.AreSame(host, host.SceneHost.SceneAdapter);
            Assert.AreSame(host.Session, host.SceneHost.Session);
            Assert.NotNull(host.GetComponent<ScenePresentationDirector>(), "The saved scene must carry the new frontend.");
            Assert.NotNull(host.GetComponent<SceneCandidateFeedback>());
            Assert.IsTrue(host.PresentationDirector.HasConfiguredTimeline);
            Assert.IsNull(typeof(SingleBattleSceneAdapter).GetMethod("BuildPlan", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic),
                "The current scene sequence must come from authored configuration, not a Host plan factory.");
            var uiActions = host.GetComponentsInChildren<UiFeedbackTimelineAction>(true);
            Assert.IsTrue(uiActions.Any(action => action.Kind == UiFeedbackKind.Damage));
            Assert.IsTrue(uiActions.Any(action => action.Kind == UiFeedbackKind.Hand));
            var cameraTarget = host.PresentationDirector.Targets.Single(binding => binding.key == "camera").target as Transform;
            Assert.NotNull(cameraTarget);
            yield return Idle(host);
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count);
            var alignedHand = host.Actions.Hand.GetComponent<HandZoneView>();
            var alignedPile = Object.FindObjectsOfType<DrawPilePresenter>().Single(pile => pile.Source == host.Actions.Hand).View;
            Assert.AreSame(alignedPile.DealAnchor, alignedHand.DeckAnchor);
            Canvas.ForceUpdateCanvases();
            var pileCanvas = alignedPile.GetComponentInParent<Canvas>().rootCanvas;
            var pileScreen = RectTransformUtility.WorldToScreenPoint(pileCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : pileCanvas.worldCamera,
                alignedPile.DealAnchor.TransformPoint(alignedPile.DealAnchor.rect.center));
            var drawScreen = alignedHand.FacingCamera.WorldToScreenPoint(alignedHand.DeckWorldPosition);
            Assert.Less(Vector2.Distance(pileScreen, new Vector2(drawScreen.x, drawScreen.y)), .1f,
                "The deal source must project onto the visible pile UI's top card.");
            Assert.AreSame(host.AuthorityState, host.State);
            Assert.IsTrue(host.CanEndTurn);
            Assert.AreEqual(BattlePlayerWindow.Action, host.State.FlowFor(host.Player.ParticipantId).Window,
                "The date standard automatically fills the opening hand and enters the player action window.");
            Assert.AreEqual(3, host.Player.Turn.Energy);
            var authority = host.AuthorityState;
            Select(host, 1); host.RefreshCandidate();
            Assert.AreEqual(CandidateState.Valid, host.Candidate.State);
            Assert.AreEqual(1, host.Candidate.SelectedIds.Count);
            Select(host, 2); host.RefreshCandidate();
            Assert.AreEqual(2, host.Candidate.SelectedIds.Count);
            Select(host, 0); host.RefreshCandidate();
            Assert.AreEqual(CandidateState.Empty, host.Candidate.State);
            Assert.AreSame(authority, host.AuthorityState, "Selection is a local presentation session.");
            Select(host, 1); host.RefreshCandidate();
            var cursors = new List<DisplayCursor>();
            host.PresentationDirector.Display.Changed += update => cursors.Add(update.Current.Cursor);
            Assert.AreEqual(3, host.Enemies.Count);
            var targetInput = host.GetComponent<BattleDemoTargetSelection>();
            Assert.NotNull(targetInput);
            Assert.IsTrue(targetInput.ChooseTarget(host.Enemies[0]));
            long oldEnemy = host.Enemy.Unit.Health;
            var cameraPosition = cameraTarget.localPosition;
            var cameraRotation = cameraTarget.localRotation;
            var playHand = host.Actions.Hand.GetComponent<HandZoneView>();
            var compactCard = playHand.Views.First(card => !card.Data.Interaction.IsSelected);
            Vector3 compactFrom = default, compactTravel = default;
            bool compacting = false, sawCompactionTravel = false, sawCompactionOvershoot = false;
            System.Action onPlayReflow = () =>
            {
                if (compacting) return;
                compacting = true; compactFrom = compactCard.DisplayPose.Position;
                compactTravel = compactCard.transform.localPosition - compactFrom;
            };
            playHand.ReflowStarted += onPlayReflow;
            Assert.AreEqual(1, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            var settledPlay = host.AuthorityState;
            Assert.IsTrue(host.IsPresenting);
            Assert.AreEqual(CandidateState.Closed, host.Candidate.State);
            Assert.IsFalse(host.CanEndTurn);
            Assert.AreEqual(oldEnemy, host.Display.Unit(host.Enemy.ParticipantId.Value).Health, "Health waits for the actual impact marker.");
            bool sawDamageUi = false, sawCameraMotion = false, capturedDamage = false;
            bool sawProjectileTail = false;
            bool sawHorizontalFlight = false;
            float tailVisibleSince = -1;
            bool capturedProjectile = false;
            float damageVisibleSince = -1;
            float playDeadline = Time.realtimeSinceStartup + 15;
            while (host.IsPresenting && Time.realtimeSinceStartup < playDeadline)
            {
                if (compacting && compactTravel.sqrMagnitude > .00000001f)
                {
                    float progress = Vector3.Dot(compactCard.DisplayPose.Position - compactFrom, compactTravel) / compactTravel.sqrMagnitude;
                    sawCompactionTravel |= progress > .01f && progress < .99f;
                    sawCompactionOvershoot |= progress > 1.001f;
                }
                var damageVisible = uiActions.Any(action => action.Kind == UiFeedbackKind.Damage && action.ActiveViewCount > 0);
                var projectiles = host.GetComponentsInChildren<TrailRenderer>().Where(tail => tail.transform.parent.name.StartsWith("Card Projectile ")).ToArray();
                sawHorizontalFlight |= projectiles.Any(tail => tail.emitting &&
                    Vector3.Dot(tail.transform.parent.up, Vector3.up) > .999f);
                if (!sawProjectileTail && projectiles.Any(tail => tail.emitting))
                {
                    sawProjectileTail = true;
                    tailVisibleSince = Time.realtimeSinceStartup;
                }
                if (!capturedProjectile && tailVisibleSince >= 0 && Time.realtimeSinceStartup - tailVisibleSince >= .22f)
                {
                    capturedProjectile = true;
                    Directory.CreateDirectory("Library/BattlePresentationValidation");
                    ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/card-projectile.png");
                }
                if (projectiles.Any(tail => tail.transform.parent.GetComponent<MeshRenderer>().enabled))
                {
                    Assert.IsFalse(damageVisible, "Enemy hit feedback must wait until the last played card reaches its target.");
                    Assert.AreEqual(oldEnemy, host.Display.Unit(host.Enemy.ParticipantId.Value).Health);
                }
                sawDamageUi |= damageVisible;
                if (damageVisible && damageVisibleSince < 0) damageVisibleSince = Time.realtimeSinceStartup;
                if (damageVisible && !capturedDamage && Time.realtimeSinceStartup - damageVisibleSince >= .12f)
                {
                    Directory.CreateDirectory("Library/BattlePresentationValidation");
                    ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/configured-damage.png");
                    capturedDamage = true;
                }
                sawCameraMotion |= Vector3.Distance(cameraPosition, cameraTarget.localPosition) > .00001f ||
                    Quaternion.Angle(cameraRotation, cameraTarget.localRotation) > .001f;
                yield return null;
            }
            playHand.ReflowStarted -= onPlayReflow;
            Assert.IsFalse(host.IsPresenting);
            Assert.IsTrue(sawCompactionTravel, "The post-play hand closes with a visible tween, even when later facts repeat the same hand.");
            Assert.IsTrue(sawCompactionOvershoot, "The compaction must play its OutBack overshoot before settling.");
            Assert.Less(Vector3.Distance(compactCard.DisplayPose.Position, compactFrom + compactTravel), .00001f);
            Assert.IsTrue(sawDamageUi, "The configured damage UI action must actually reveal its owned text.");
            Assert.IsTrue(sawProjectileTail, "Each played card must carry an emitting tail during its projectile flight.");
            Assert.IsTrue(sawHorizontalFlight, "Played cards must turn onto the horizontal world plane during flight.");
            Assert.IsTrue(sawCameraMotion, "The enemy damage filter must start the configured camera action.");
            Assert.Less(Vector3.Distance(cameraPosition, cameraTarget.localPosition), .00001f);
            Assert.Less(Quaternion.Angle(cameraRotation, cameraTarget.localRotation), .001f);
            Assert.AreSame(settledPlay, host.State);
            Assert.Less(host.Enemy.Unit.Health, oldEnemy);
            Assert.AreEqual(7, host.Actions.Hand.Cards.Count, "Playing a card does not refill it.");
            Assert.AreEqual(2, host.Player.Turn.Energy);
            Assert.AreEqual(0, host.State.Duel.CompletedRounds);
            Assert.AreEqual(cursors.Count, cursors.Select(c => c.Sequence).Distinct().Count());
            Assert.IsTrue(cursors.Zip(cursors.Skip(1), (a,b) => a.Sequence < b.Sequence).All(x => x));
            Assert.IsNull(host.PresentationDirector.LastError);
            Select(host, 3);
            var handView = host.Actions.Hand.GetComponent<HandZoneView>();
            float discardStarted = -1, compactStarted = -1, refillStarted = -1;
            var originalSlots = handView.Views.ToDictionary(card => card.ViewId, card => card.DisplayPose.Position);
            var survivors = handView.Views.Where(card => !card.Data.Interaction.IsSelected).Select(card => card.ViewId).ToArray();
            System.Action<CardInstanceView> onDiscard = _ =>
            {
                discardStarted = Time.realtimeSinceStartup;
                foreach (var id in survivors)
                {
                    Assert.IsTrue(handView.TryGetView(id, out var card));
                    Assert.Less(Vector3.Distance(originalSlots[id], card.DisplayPose.Position), .00001f,
                        "Survivors must retain their slots while the outgoing batch is flying: " + id +
                        " old=" + originalSlots[id] + " now=" + card.DisplayPose.Position +
                        " departures=" + handView.DepartingViews.Count + " pendingSort=" + host.Actions.Hand.HasPendingSort);
                }
            };
            System.Action onCompact = () =>
            {
                if (compactStarted >= 0) return;
                compactStarted = Time.realtimeSinceStartup;
                Assert.IsEmpty(handView.DepartingViews, "Compaction starts after the last discard has left.");
                CollectionAssert.AreEqual(survivors, handView.Views.Select(card => card.ViewId), "Compaction preserves hand order.");
            };
            System.Action<CardInstanceView> onDeal = _ => { if (refillStarted < 0) refillStarted = Time.realtimeSinceStartup; };
            handView.CardDiscardStarted += onDiscard; handView.ReflowStarted += onCompact; handView.CardDealStarted += onDeal;
            Assert.AreEqual(3, host.Player.Discard(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle(host);
            handView.CardDiscardStarted -= onDiscard; handView.ReflowStarted -= onCompact; handView.CardDealStarted -= onDeal;
            Assert.GreaterOrEqual(discardStarted, 0);
            Assert.GreaterOrEqual(compactStarted - discardStarted, handView.DiscardSettings.FlightSeconds - .06f);
            Assert.GreaterOrEqual(refillStarted - compactStarted, handView.TransitionSettings.ReflowSeconds - .06f,
                "Refill starts only after the survivors finish compacting.");
            Assert.IsFalse(host.Actions.Hand.HasPendingSort, "Replacement cards land before the final sort completes.");
            var discardViewport = handView.FacingCamera.WorldToViewportPoint(handView.DiscardWorldPosition);
            var handViewport = handView.FacingCamera.WorldToViewportPoint(handView.transform.position);
            Assert.AreEqual(handViewport.x, discardViewport.x, .001f, "Discard target stays directly below the hand center.");
            Assert.Less(discardViewport.y, 0);
            Assert.AreSame(host.AuthorityState, host.State);
            Assert.AreEqual(0, host.State.Duel.CompletedRounds, "Discarding does not end a turn with action points remaining.");
            Assert.AreEqual(1, host.Player.Turn.Energy);
            Assert.AreEqual(BattlePlayerWindow.Action, host.State.FlowFor(host.Player.ParticipantId).Window);
            Assert.AreEqual(host.Player.Unit.Block, host.Player.Shields.TotalBlock);
            Assert.AreEqual(7, host.Actions.Hand.Cards.Count, "Discarding one card immediately replaces one card.");
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.IsTrue(host.EndTurn(true));
            yield return null;
            var final = host.AuthorityState;
            Assert.IsTrue(host.IsPresenting);
            host.SnapToState();
            Assert.IsTrue(uiActions.All(action => action.ActiveViewCount == 0), "Recovery releases all scoped UI feedback.");
            Assert.AreSame(final, host.State);
            Assert.IsFalse(host.IsPresenting);
            Assert.AreEqual(1, final.Duel.CompletedRounds);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count, "The next player turn fills the hand to its limit.");
            Assert.AreEqual(3, host.Player.Turn.Energy);
            var cursor = host.Display.Cursor;
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreSame(final, host.AuthorityState);
            Assert.AreEqual(cursor, host.Display.Cursor, "Cancelled callbacks cannot advance the recovered timeline.");
            Assert.AreEqual(host.Player.Unit.Block, host.Player.Shields.TotalBlock);
            foreach (var zone in final.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, host.Match.State.GetZone(zone.Id).Cards);
            host.RestartBattle();
            yield return Idle(host);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count);
            Assert.AreEqual(3, host.Player.Turn.Energy);
            Assert.IsNull(host.PresentationDirector.LastError);
            var beforeDisable = host.AuthorityState;
            host.Match.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse(host.Match.IsRunning);
            Assert.AreSame(beforeDisable, host.State);
            host.Match.gameObject.SetActive(true);
            yield return Idle(host);
            Assert.AreSame(beforeDisable, host.AuthorityState, "Reactivation only restores the display; it cannot replay rules.");
            Assert.IsTrue(host.Match.IsRunning);
            Assert.AreEqual(8, host.Actions.Hand.Cards.Count);
            Assert.IsTrue(host.CanEndTurn);
            Assert.IsNull(host.PresentationDirector.LastError);
            Assert.IsEmpty(errors, "The authored scene flow must finish without product errors.");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, "PASS\nAuthoredSceneUsesTimelineForOpeningSelectionPlayDiscardAndCancellation\n" +
                System.DateTime.UtcNow.ToString("O"));
        }
        [UnityTest]
        public IEnumerator SavedScenePlaysStraightCardByCardAndRoyalEliminationBeforeDeath()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            yield return Idle(host);
            Assert.NotNull(host.GetComponent<BattleCardEffectBridge>());
            var original = host.ContentXml;
            var temporary = new List<TextAsset>();
            try
            {
                foreach (bool royal in new[] { false, true })
                {
                    var ranks = royal ? new[] { 10, 11, 12, 13, 14 } : new[] { 6, 2, 5, 3, 4 };
                    var xml = XDocument.Parse(original.text);
                    xml.Root.Element("Decks").Elements().First().ReplaceNodes(ranks.Select((rank, index) => new XElement("Card",
                        new XAttribute("id", "visual-" + index), new XAttribute("face", "standard:" + (royal ? 1 : index % 4 + 1) + ":" + rank))));
                    xml.Root.Element("Enemies").Elements().First().SetAttributeValue("maxHealth", 100000);
                    var asset = new TextAsset(xml.ToString()) { name = "runtime-only-card-effect-validation" }; temporary.Add(asset);
                    host.ConfigureContent(asset, "standard-single-battle"); host.RestartBattle();
                    yield return Idle(host); Select(host, 5);
                    Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
                    bool sawFlight = false, sawRoyalPulse = false, sawRoyalText = false, sawOverlap = false;
                    var launchTimes = new Dictionary<string, float>();
                    var cardViews = new HashSet<string>();
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (host.IsPresenting && Time.realtimeSinceStartup < deadline)
                    {
                        var trails = host.GetComponentsInChildren<TrailRenderer>();
                        var flying = trails.Where(tail => tail.transform.parent.name.StartsWith("Card Projectile ") &&
                            tail.transform.parent.GetComponent<MeshRenderer>().enabled).ToArray();
                        sawFlight |= flying.Length > 0;
                        foreach (var tail in flying) cardViews.Add(tail.transform.parent.name);
                        if (!royal)
                        {
                            sawOverlap |= flying.Length > 1;
                            foreach (var tail in flying.Where(item => item.emitting))
                                if (!launchTimes.ContainsKey(tail.transform.parent.name)) launchTimes.Add(tail.transform.parent.name, Time.realtimeSinceStartup);
                        }
                        if (royal)
                        {
                            sawRoyalPulse |= host.GetComponentsInChildren<LineRenderer>().Any(line => line.name.StartsWith("Royal Flush Impact "));
                            sawRoyalText |= host.GetComponentsInChildren<UiFeedbackTimelineAction>(true)
                                .Any(ui => ui.name == "Royal Flush Feedback" && ui.ActiveViewCount > 0);
                            Assert.IsFalse(host.GetComponentsInChildren<UiFeedbackTimelineAction>(true)
                                .Any(ui => ui.Kind == UiFeedbackKind.Damage && ui.ActiveViewCount > 0), "Elimination must not masquerade as ordinary HP damage.");
                            if (flying.Length > 0) Assert.Greater(host.Display.Unit(host.Enemy.ParticipantId.Value).Health, 0,
                                "The target cannot die on screen before its royal volley arrives.");
                        }
                        yield return null;
                    }
                    Assert.IsFalse(host.IsPresenting); Assert.IsTrue(sawFlight); Assert.AreEqual(royal ? 15 : 5, cardViews.Count);
                    if (!royal)
                    {
                        Assert.IsTrue(sawOverlap, "The next straight card launches before the prior flight has completed.");
                        Assert.AreEqual(5, launchTimes.Count);
                        var times = launchTimes.Values.OrderBy(time => time).ToArray();
                        Assert.Less(times.Last() - times.First(), .65f, "Five straight cards launch in a short 0.1-second cadence.");
                        Assert.IsTrue(host.Enemies.Skip(1).All(enemy => !enemy.IsAlive), "The straight kills the two low-health targets and transfers to the surviving enemy.");
                    }
                    Assert.IsNull(host.PresentationDirector.LastError);
                    Assert.AreSame(host.AuthorityState, host.State);
                    Assert.IsEmpty(host.GetComponentsInChildren<TrailRenderer>());
                    if (royal) { Assert.IsTrue(sawRoyalPulse); Assert.IsTrue(sawRoyalText); Assert.AreEqual(0, host.Enemy.Unit.Health); }
                }
            }
            finally
            {
                host.SnapToState(); host.ConfigureContent(original, "standard-single-battle");
                foreach (var asset in temporary) Object.Destroy(asset);
            }
        }

        [UnityTest]
        public IEnumerator SavedScenePortraitClicksChooseRuleTargetsAndGroupVolleyReachesThreeEnemies()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            yield return Idle(host);
            // Final sorting settles in Update; candidate and target buttons refresh in LateUpdate.
            yield return new WaitForEndOfFrame();
            var selector = host.GetComponent<BattleDemoTargetSelection>();
            Assert.AreEqual(3, host.Enemies.Count);
            CollectionAssert.AreEqual(new long[] { 600, 4, 6 }, host.Enemies.Select(actor => actor.Unit.Health));
            var original = host.AuthorityState;
            string targetReadiness = "input=" + host.CanChoosePlayTarget + " presenting=" + host.IsPresenting +
                " candidate=" + host.Candidate?.State + " choices=" + host.PlayTargetChoices.Count +
                " selected=" + host.Actions.Hand.CaptureSelection().Cards.Count +
                " moving=" + host.Actions.Hand.GetComponent<HandZoneView>().IsTransitioning +
                " pendingSort=" + host.Actions.Hand.HasPendingSort +
                " authority=" + ReferenceEquals(host.State, host.AuthorityState) +
                " error=" + host.PresentationDirector.LastError;
            TestContext.WriteLine("Target readiness: " + targetReadiness);
            Assert.IsTrue(selector.ChooseTarget(host.Enemies[2]), "A target can be picked before selecting cards: " + targetReadiness);
            Assert.AreSame(original, host.AuthorityState);
            Select(host, 1); host.RefreshCandidate();
            yield return Idle(host);
            yield return new WaitForEndOfFrame();
            var button = Object.FindObjectsOfType<UnityEngine.UI.Button>().Single(item => item.name == "Target Enemy 1");
            Assert.IsTrue(button.interactable, "Target button must be active for a selected single card: choices=" + host.PlayTargetChoices.Count + " input=" + host.CanChoosePlayTarget);
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = host.Actions.Hand.GetComponent<HandZoneView>().FacingCamera.WorldToScreenPoint(host.Enemies[1].transform.position) };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.AreSame(button.gameObject, hits.First().gameObject, "The visible portrait must be reachable through the scene UI raycaster.");
            button.onClick.Invoke();
            Assert.AreEqual(host.Enemies[1].DisplayId, host.PlayTargetChoices.Single().SelectedId);
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(host.Enemies[1].GetComponentsInChildren<MeshRenderer>().Single(renderer => renderer.name == "Selected enemy contour").enabled);
            Directory.CreateDirectory("Library/BattlePresentationValidation");
            ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/selected-enemy-contour.png");
            var preview = host.PreviewSelectedPlay(); Assert.IsTrue(preview.Accepted);
            var predicted = preview.State.Duel.Combat.Get(host.Enemies[1].ParticipantId).Health;
            Assert.AreEqual(1, host.Player.Play(host.Actions.Hand.CaptureSelection(), false));
            Assert.AreEqual(predicted, host.Enemies[1].Unit.Health);
            Assert.AreEqual(600, host.Enemies[0].Unit.Health);
            Assert.IsFalse(selector.ChooseTarget(null));
            if (!host.Enemies[1].IsAlive) Assert.IsFalse(selector.ChooseTarget(host.Enemies[1]));
            host.RestartBattle(); yield return Idle(host);
            var patch = new BattleAdminPatch(zones: new[] { new BattleAdminZone(host.Player.ParticipantId, CardZoneKinds.Hand,
                new[] { new BattleAdminCard(definitionId: "standard:1:7"), new BattleAdminCard(definitionId: "standard:2:7"),
                    new BattleAdminCard(definitionId: "standard:3:7") }) });
            Assert.IsTrue(host.Playback.ApplyAdmin(host.AuthorityState.Revision, patch).Accepted);
            yield return Idle(host); Select(host, 3); host.RefreshCandidate();
            Assert.IsEmpty(host.PlayTargetChoices, "Group attack supplies its own targets.");
            Assert.IsFalse(selector.ChooseTarget(host.Enemies[0]));
            Assert.AreEqual(3, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            var branches = new HashSet<string>(); bool captured = false;
            float deadline = Time.realtimeSinceStartup + 15;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline)
            {
                var trails = host.GetComponentsInChildren<TrailRenderer>().Where(tail => tail.name == "Tail" && tail.emitting).ToArray();
                foreach (var tail in trails) branches.Add(tail.transform.parent.name);
                if (!captured && trails.Length == 9)
                {
                    captured = true; Directory.CreateDirectory("Library/BattlePresentationValidation");
                    ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/three-enemy-group.png");
                }
                yield return null;
            }
            Assert.IsFalse(host.IsPresenting); Assert.AreEqual(9, branches.Count);
            foreach (var actor in host.Enemies) Assert.AreEqual(3, branches.Count(name => name.EndsWith(" -> " + actor.DisplayId)));
            Assert.IsNull(host.PresentationDirector.LastError);
        }

        [UnityTest]
        public IEnumerator EnemyWorldHudShowsIndependentIntentsStatusesAndDisplayHealth()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            yield return Idle(host); Assert.NotNull(host.GetComponent<BattleDemoEnemyWorldHud>());
            var original = host.ContentXml;
            var xml = XDocument.Parse(original.text);
            var definitions = xml.Root.Element("Enemies").Elements().Where(item =>
                new[] { "rotator", "validation-1", "validation-2" }.Contains((string)item.Attribute("id"))).ToArray();
            var statuses = new[] { new[] { "layer-armor", "shield-reflect", "life-steal" },
                new[] { "bleed", "control-immunity", "overheal-damage" }, new[] { "stun" } };
            for (int index = 0; index < definitions.Length; index++)
            {
                definitions[index].SetAttributeValue("maxHealth", 100000);
                var behavior = definitions[index].Element("Behavior");
                behavior.AddFirst(new XElement("OpeningEffects", statuses[index].Select(id => new XElement("Effect",
                    new XAttribute("id", "ui-" + id), new XAttribute("kind", "ApplyStatus"), new XAttribute("statusRef", id),
                    new XAttribute("stacks", id == "layer-armor" ? 3 : id == "bleed" ? 2 : 1), new XAttribute("target", "Self")))));
                if (index == 1)
                    foreach (var effects in behavior.Descendants("Effects"))
                    {
                        foreach (var effect in effects.Elements())
                        { effect.SetAttributeValue("kind", "GainBlock"); effect.SetAttributeValue("amount", 90); effect.SetAttributeValue("target", "Self"); }
                        effects.Add(new XElement("Effect", new XAttribute("id", "ui-counter"), new XAttribute("kind", "Damage"), new XAttribute("amount", 15)));
                    }
            }
            var asset = new TextAsset(xml.ToString()) { name = "runtime-only-enemy-ui-validation" };
            try
            {
                host.ConfigureContent(asset, "standard-single-battle"); host.RestartBattle(); yield return Idle(host);
                host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions();
                yield return new WaitForEndOfFrame();
                var world = Object.FindObjectsOfType<Canvas>().Where(canvas => canvas.name.StartsWith("Enemy World HUD ")).OrderBy(canvas => canvas.name).ToArray();
                Assert.AreEqual(3, world.Length);
                foreach (var canvas in world) { Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode); Assert.NotNull(canvas.worldCamera); }
                var symbols = new[] { BattleDemoEnemySymbol.Sword, BattleDemoEnemySymbol.Shield, BattleDemoEnemySymbol.Stun };
                var expectedColors = new[] { BattleDemoEnemyHudModel.AttackColor, BattleDemoEnemyHudModel.ShieldColor,
                    BattleDemoEnemyHudModel.Status("stun", 1).Color };
                for (int index = 0; index < world.Length; index++)
                {
                    var intent = world[index].transform.Find("Intents/Intent 0");
                    var icon = intent.GetComponentInChildren<BattleDemoEnemyIcon>();
                    var value = intent.GetComponentInChildren<TMPro.TMP_Text>();
                    Assert.AreEqual(symbols[index], icon.Symbol); Assert.AreEqual(expectedColors[index], icon.color);
                    Assert.AreEqual(icon.color, value.color, "Intent values share their icon's exact tint.");
                    Assert.AreEqual(index == 0 ? "40" : index == 1 ? "90" : "", value.text);
                    Assert.AreEqual(statuses[index].Length, world[index].transform.Find("Statuses").GetComponentsInChildren<BattleDemoEnemyIcon>().Length);
                }
                var composite = world[1].transform.Find("Intents/Intent 1");
                Assert.AreEqual(BattleDemoEnemySymbol.Sword, composite.GetComponentInChildren<BattleDemoEnemyIcon>().Symbol);
                Assert.AreEqual("15", composite.GetComponentInChildren<TMPro.TMP_Text>().text);
                Assert.AreEqual(BattleDemoEnemyHudModel.AttackColor, composite.GetComponentInChildren<TMPro.TMP_Text>().color);
                var basePosition = world[0].transform.position;
                host.Enemies[0].Visual.localPosition += Vector3.up * .05f;
                yield return new WaitForEndOfFrame(); Assert.Greater(Vector3.Distance(basePosition, world[0].transform.position), .03f);
                host.Enemies[0].Visual.localPosition -= Vector3.up * .05f;
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                var geometry = new List<string>();
                foreach (var icon in world.SelectMany(canvas => canvas.GetComponentsInChildren<BattleDemoEnemyIcon>()))
                {
                    Assert.NotNull(icon.canvasRenderer, "World-space vector icons require a CanvasRenderer.");
                    var mesh = icon.canvasRenderer.GetMesh();
                    geometry.Add(icon.Symbol + " rect=" + icon.rectTransform.rect + " mesh=" + mesh.vertexCount + " bounds=" + mesh.bounds + " color=" + icon.color + " culled=" + icon.canvasRenderer.cull);
                    Assert.Greater(mesh.vertexCount, 0, "Every symbol must produce visible UI geometry: " + icon.Symbol);
                }
                Directory.CreateDirectory("Library/BattlePresentationValidation");
                File.WriteAllLines("Library/BattlePresentationValidation/enemy-icon-geometry.txt", geometry);
                ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/enemy-world-hud.png");
                var badge = world[1].transform.Find("Statuses/Status 0").gameObject;
                var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                    { position = world[1].worldCamera.WorldToScreenPoint(badge.transform.position) };
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
                Assert.AreSame(badge, hits.First().gameObject, "World-space status badges receive actual pointer rays.");
                UnityEngine.EventSystems.ExecuteEvents.Execute<UnityEngine.EventSystems.IPointerEnterHandler>(badge, pointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                var tooltip = world[1].transform.Find("Enemy Badge Tooltip"); Assert.IsTrue(tooltip.gameObject.activeSelf);
                StringAssert.Contains("中毒", tooltip.GetComponentInChildren<TMPro.TMP_Text>().text);
                UnityEngine.EventSystems.ExecuteEvents.Execute<UnityEngine.EventSystems.IPointerExitHandler>(badge, pointer,
                    UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                Select(host, 1); host.RefreshCandidate();
                Assert.IsTrue(host.GetComponent<BattleDemoTargetSelection>().ChooseTarget(host.Enemies[0]));
                var health = world[0].transform.Find("Health Bar/Health Value").GetComponent<TMPro.TMP_Text>();
                var oldHealth = health.text; Assert.AreEqual(1, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
                Assert.AreEqual(oldHealth, health.text, "World HUD must not reveal settled authority HP before impact.");
                yield return Idle(host); yield return new WaitForEndOfFrame();
                Assert.AreEqual(host.Enemies[0].DisplayUnit.Health + " / " + host.Enemies[0].DisplayUnit.MaxHealth, health.text);
                Assert.IsTrue(host.EndTurn(false)); yield return Idle(host); yield return new WaitForEndOfFrame();
                Assert.AreEqual("90", world[1].transform.Find("Current Block/Value").GetComponent<TMPro.TMP_Text>().text);
                Assert.IsTrue(world[1].transform.Find("Current Block").gameObject.activeSelf);
                Assert.IsNull(host.PresentationDirector.LastError);
            }
            finally { host.SnapToState(); host.ConfigureContent(original, "standard-single-battle"); Object.Destroy(asset); }
        }

        [UnityTest]
        public IEnumerator OutcomePreviewAndRelicArtUseActualResultsAndAllImportedSprites()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single(); yield return Idle(host);
            var ui = Object.FindObjectOfType<BattleDemoOutcomePreview>(); Assert.NotNull(ui);
            var art = Object.FindObjectOfType<BattleDemoRelicArt>(); Assert.NotNull(art);
            Assert.AreEqual(12, art.Entries.Length);
            foreach (var entry in art.Entries)
            {
                Assert.NotNull(entry.sprite); Assert.AreSame(entry.sprite, art.Resolve(entry.definitionId));
                StringAssert.StartsWith("Assets/Art/UI/Relics/", AssetDatabase.GetAssetPath(entry.sprite));
                Assert.AreNotEqual("BattleRelics", entry.sprite.texture.name);
            }
            Assert.AreEqual("六面骰", art.Resolve("14001").name);
            Assert.IsFalse(host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Any(), "Art must not grant items to the starting loadout.");
            var patch = new BattleAdminPatch(zones: new[] { new BattleAdminZone(host.Player.ParticipantId, CardZoneKinds.Hand,
                new[] { new BattleAdminCard(definitionId: "standard:1:7"), new BattleAdminCard(definitionId: "standard:2:7"), new BattleAdminCard(definitionId: "standard:3:7") }) });
            Assert.IsTrue(host.Playback.ApplyAdmin(host.AuthorityState.Revision, patch).Accepted);
            yield return Idle(host); Select(host, 3); host.RefreshCandidate(); yield return Idle(host); yield return new WaitForEndOfFrame();
            StringAssert.Contains("群攻 3", ui.OutcomeText); StringAssert.Contains("击杀 2", ui.OutcomeText);
            StringAssert.DoesNotContain("×", ui.OutcomeText + ui.DetailsText);
            StringAssert.Contains("怪物1 生命 600→558", ui.DetailsText);
            var before = host.AuthorityState; var preview = host.PreviewSelectedPlay(); Assert.AreSame(before, host.AuthorityState);
            var expected = preview.State.Duel.Combat.Get(host.Enemies[0].ParticipantId).Health;
            Assert.AreEqual(3, host.Player.Play(host.Actions.Hand.CaptureSelection(), false));
            Assert.AreEqual(expected, host.Enemies[0].Unit.Health);
            Assert.IsTrue(host.Enemies.Skip(1).All(actor => !actor.IsAlive));
            host.RestartBattle(); yield return Idle(host);
            Assert.IsTrue(host.Playback.ApplyAdmin(host.AuthorityState.Revision, new BattleAdminPatch(relics: new[] {
                new BattleAdminRelics(host.Player.ParticipantId, art.Entries.Select(entry => new BattleAdminRelic(entry.definitionId))) })).Accepted);
            yield return Idle(host); Select(host, 1); host.RefreshCandidate(); yield return Idle(host); yield return new WaitForEndOfFrame();
            var icons = art.GetComponentsInChildren<UnityEngine.UI.Image>().Where(image => image.transform.parent.name == "Relic Icons" && image.isActiveAndEnabled).ToArray();
            Assert.AreEqual(12, icons.Length);
            foreach (var image in icons) Assert.IsTrue(art.Entries.Any(entry => entry.sprite == image.sprite));
            var die = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Single(item => item.DefinitionId == "14001");
            art.GetComponent<SingleBattleHud>().SelectItem(die.InstanceId);
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(art.GetComponentsInChildren<UnityEngine.UI.Image>().Any(image => image.name == "Icon" && image.sprite == art.Resolve("14001")));
            Directory.CreateDirectory("Library/BattlePresentationValidation");
            ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/outcome-and-relic-art.png");
            Assert.IsNull(host.PresentationDirector.LastError);
        }

        [UnityTest]
        public IEnumerator RelicHoverOwnsDescriptionsUseButtonsAndLimitedActiveChargeBadges()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single(); yield return Idle(host);
            var art = Object.FindObjectOfType<BattleDemoRelicArt>(); var hud = art.GetComponent<SingleBattleHud>();
            Assert.IsTrue(host.Playback.ApplyAdmin(host.AuthorityState.Revision, new BattleAdminPatch(relics: new[] {
                new BattleAdminRelics(host.Player.ParticipantId, art.Entries.Select(entry => new BattleAdminRelic(entry.definitionId))) })).Accepted);
            yield return Idle(host); yield return new WaitForEndOfFrame();
            var icons = art.GetComponentsInChildren<UnityEngine.UI.Image>().Where(image => image.transform.parent.name == "Relic Icons" && image.isActiveAndEnabled).ToArray();
            Assert.AreEqual(12, icons.Length); Assert.IsTrue(string.IsNullOrEmpty(hud.RuleFeedbackText));
            var loadout = host.State.Duel.Player(host.Player.ParticipantId).Relics;
            foreach (var relic in loadout.Relics)
            {
                var icon = icons.Single(image => image.sprite == art.Resolve(relic.DefinitionId));
                var count = icon.transform.Find("Remaining Uses").GetComponent<TMPro.TMP_Text>();
                var spec = loadout.RulesFor(relic.InstanceId).Item;
                bool limitedActive = spec != null && spec.Activation != BattleItemActivation.None && relic.RemainingCharges.HasValue;
                Assert.AreEqual(limitedActive, count.gameObject.activeSelf);
                if (limitedActive) Assert.AreEqual(relic.RemainingCharges.ToString(), count.text);
                Assert.AreEqual(new Vector2(1, 0), count.rectTransform.anchorMin);
            }
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            var passive = icons.Single(image => image.sprite == art.Resolve("12001"));
            var before = host.AuthorityState;
            UnityEngine.EventSystems.ExecuteEvents.Execute(passive.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            var panel = art.transform.Find("Relic Hover"); Assert.IsTrue(panel.gameObject.activeSelf);
            StringAssert.Contains("反给攻击者", panel.Find("Effect Description").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(panel.Find("Use Item").gameObject.activeSelf); Assert.AreSame(before, host.AuthorityState);
            UnityEngine.EventSystems.ExecuteEvents.Execute(passive.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.23f); Assert.IsFalse(panel.gameObject.activeSelf);
            var grenade = icons.Single(image => image.sprite == art.Resolve("11004"));
            UnityEngine.EventSystems.ExecuteEvents.Execute(grenade.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            var use = panel.Find("Use Item").GetComponent<UnityEngine.UI.Button>(); Assert.IsTrue(use.isActiveAndEnabled);
            yield return null; Assert.IsTrue(use.interactable);
            UnityEngine.EventSystems.ExecuteEvents.Execute(grenade.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(panel.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.25f); Assert.IsTrue(panel.gameObject.activeSelf, "The user can move from the icon into the use panel.");
            long actions = host.State.FlowFor(host.Player.ParticipantId).ActionsRemaining;
            use.onClick.Invoke(); Assert.IsTrue(host.IsPresenting); Assert.IsFalse(host.CanUseItem(loadout.Relics.Single(item => item.DefinitionId == "11004").InstanceId));
            yield return Idle(host); yield return new WaitForEndOfFrame();
            Assert.AreEqual("2", grenade.transform.Find("Remaining Uses").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual(actions, host.State.FlowFor(host.Player.ParticipantId).ActionsRemaining);
            StringAssert.Contains("次数 2", panel.Find("Availability").GetComponent<TMPro.TMP_Text>().text);
            Directory.CreateDirectory("Library/BattlePresentationValidation"); ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/relic-hover.png");
            var die = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Single(item => item.DefinitionId == "14001");
            Assert.IsTrue(hud.SelectItem(die.InstanceId)); yield return null;
            Assert.IsFalse(use.interactable, "Dice requires an explicit replacement target.");
            var replacement = panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button != use);
            replacement.onClick.Invoke(); yield return null; Assert.IsTrue(use.interactable);
            host.RestartBattle(); yield return Idle(host); yield return null; Assert.IsFalse(panel.gameObject.activeSelf);
            Assert.IsNull(host.PresentationDirector.LastError);
        }

        [UnityTest]
        public IEnumerator PairDropsKickersWithoutTailsAndKillsThroughAshShaderWithRecovery()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/BattleDemo.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single(); yield return Idle(host);
            var ash = host.Enemies[1].Presentation as BattleDemoAshPresenter; Assert.NotNull(ash);
            var patch = new BattleAdminPatch(zones: new[] { new BattleAdminZone(host.Player.ParticipantId, CardZoneKinds.Hand,
                new[] { new BattleAdminCard(definitionId: "standard:1:7"), new BattleAdminCard(definitionId: "standard:2:7"),
                    new BattleAdminCard(definitionId: "standard:3:2"), new BattleAdminCard(definitionId: "standard:4:4"), new BattleAdminCard(definitionId: "standard:1:9") }) });
            Assert.IsTrue(host.Playback.ApplyAdmin(host.AuthorityState.Revision, patch).Accepted); yield return Idle(host);
            Select(host, 5); host.RefreshCandidate();
            Assert.IsTrue(host.GetComponent<BattleDemoTargetSelection>().ChooseTarget(host.Enemies[1]));
            Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            bool sawConcurrent = false, sawDownward = false, sawAsh = false, captured = false;
            var starts = new Dictionary<string, float>(); float deadline = Time.realtimeSinceStartup + 15;
            var camera = host.Actions.Hand.GetComponent<HandZoneView>().FacingCamera;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline)
            {
                var all = host.GetComponentsInChildren<MeshRenderer>();
                var drops = all.Where(renderer => renderer.name.StartsWith("Unscored Card ") && renderer.enabled).ToArray();
                var scoring = all.Where(renderer => renderer.name.StartsWith("Card Projectile ") && renderer.enabled).ToArray();
                if (drops.Length > 0)
                {
                    Assert.AreEqual(3, drops.Length); Assert.IsTrue(drops.All(renderer => renderer.GetComponentInChildren<TrailRenderer>() == null));
                    sawConcurrent |= scoring.Length == 2;
                    foreach (var drop in drops)
                    {
                        float y = camera.WorldToViewportPoint(drop.transform.position).y;
                        if (!starts.ContainsKey(drop.name)) starts.Add(drop.name, y);
                        sawDownward |= y < starts[drop.name] - .08f;
                    }
                    foreach (var tail in host.GetComponentsInChildren<TrailRenderer>())
                    {
                        var mesh = tail.transform.parent.GetComponent<MeshFilter>().sharedMesh;
                        Assert.Less(tail.widthMultiplier, mesh.bounds.size.x * Mathf.Abs(tail.transform.parent.lossyScale.x) * .7f);
                    }
                }
                if (ash.Progress > .1f && ash.Progress < .9f)
                {
                    sawAsh = true; Assert.AreEqual("Poker/BattleDemo Enemy Ash", ash.Artwork.sharedMaterial.shader.name);
                    Assert.IsTrue(ash.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.name == "Enemy Ash Fragments" && renderer.enabled));
                    if (!captured && ash.Progress > .38f)
                    { captured = true; Directory.CreateDirectory("Library/BattlePresentationValidation"); ScreenCapture.CaptureScreenshot("Library/BattlePresentationValidation/enemy-ash-death.png"); }
                }
                yield return null;
            }
            Assert.IsFalse(host.IsPresenting); Assert.IsTrue(sawConcurrent); Assert.IsTrue(sawDownward); Assert.IsTrue(sawAsh);
            Assert.AreEqual(1, ash.Progress); Assert.IsFalse(host.Enemies[1].IsAlive);
            host.SnapToState(); Assert.AreEqual(1, ash.Progress);
            host.RestartBattle(); yield return Idle(host);
            Assert.IsTrue(host.Enemies.All(actor => ((BattleDemoAshPresenter)actor.Presentation).Progress == 0));
            Assert.IsFalse(host.GetComponentsInChildren<Renderer>().Any(renderer => renderer.name.StartsWith("Unscored Card ")));
            yield return new WaitForSecondsRealtime(.9f); Assert.AreEqual(0, ash.Progress);
            Assert.IsNull(host.PresentationDirector.LastError);
        }

        [UnityTest]
        public IEnumerator MinimalPrefabAutoStartsTwoIndependentBattlesWithoutSceneBindings()
        {
            var previous = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("Battle Host Minimal Runtime Validation");
            SceneManager.SetActiveScene(empty);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/SingleBattle/Hosting/BattleSceneHostMinimal.prefab");
            Assert.NotNull(prefab);
            var first = Object.Instantiate(prefab).GetComponent<BattleSceneHost>();
            var second = Object.Instantiate(prefab).GetComponent<BattleSceneHost>();
            yield return null;
            Assert.NotNull(first.Session); Assert.NotNull(second.Session);
            Assert.IsTrue(first.AuthorityState.HasStarted); Assert.IsTrue(second.AuthorityState.HasStarted);
            var adapter = (BattleTimelineSceneAdapter)first.SceneAdapter;
            first.Playback.Submit(adapter.ObserverId, CardDuelAction.SkipDraw, animate: false);
            var untouched = second.AuthorityState;
            adapter.SelectCards(first.AuthorityState.Zone(CardZoneKinds.Hand, adapter.ObserverId).Cards.Take(1).Select(id => id.Value));
            Assert.IsTrue(adapter.PlaySelected().Accepted);
            Assert.AreSame(untouched, second.AuthorityState);
            var state = first.AuthorityState;
            first.gameObject.SetActive(false); yield return null;
            first.gameObject.SetActive(true); yield return null;
            Assert.AreSame(state, first.AuthorityState); Assert.IsTrue(adapter.CanAct);
            first.RestartBattle(); yield return null;
            Assert.AreNotSame(state, first.AuthorityState); Assert.AreSame(untouched, second.AuthorityState);
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(empty);
        }
        [UnityTest]
        public IEnumerator RevivalStopsAnActiveDeathCoroutineAndItsLatePoseWrite()
        {
            var resultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BattleRuleAlignment/revival-coroutine.txt"));
            if (File.Exists(resultPath)) File.Delete(resultPath);
            var root = new GameObject("Revival coroutine regression");
            try
            {
                var actor = root.AddComponent<PlayerBattleActor>();
                var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
                var presenter = model.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(model);
                actor.ConfigurePresentation(presenter);
                UnitDisplayData Unit(long hp) => new UnitDisplayData("revival", "Player", "player", hp, 100, 0, 3, 3);
                actor.SynchronizeDisplay(Unit(100), true);
                var lethal = new EffectDisplayData("Damage", "enemy", "revival", 100, 100, 0, Unit(100), Unit(0), true);
                var death = actor.PresentDisplay(BattlePresentationKind.Death, lethal, null);
                yield return null;
                Assert.IsTrue(death.keepWaiting);
                actor.SynchronizeDisplay(Unit(60));
                Assert.AreEqual(BattlePresentationStatus.Completed, death.Status);
                yield return new WaitForSecondsRealtime(.45f);
                Assert.AreEqual(Vector3.one, model.localScale);
                Assert.AreEqual(Quaternion.identity, model.localRotation);
                Assert.AreEqual(60, actor.DisplayUnit.Health);
                Assert.IsFalse(actor.IsDisplayBusy);
            }
            finally { Object.Destroy(root); }
            yield return null;
            Assert.IsEmpty(errors, "The revival regression must finish without product errors.");
            // Preserve a scoped assertion result when the editor's test-runner connection fails during its later cleanup.
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, "PASS\nRevivalStopsAnActiveDeathCoroutineAndItsLatePoseWrite\n" + System.DateTime.UtcNow.ToString("O"));
        }

        private static IEnumerator Idle(SingleBattleSceneAdapter host)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            yield return null;
            while (host.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsPresenting, "The scene did not reach a decision window before the test deadline.");
        }
        private static void Select(SingleBattleSceneAdapter host, int count)
        {
            var hand = host.Actions.Hand;
            foreach(var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, default);
            foreach(var card in hand.Cards.Take(count).ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected:true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions();
        }
    }
}
#endif
