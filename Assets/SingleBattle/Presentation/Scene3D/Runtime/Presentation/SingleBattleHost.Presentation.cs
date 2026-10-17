using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    // Only 3D projection and playback live here. Commands and rule progression use the shared session port.
    public sealed partial class SingleBattleHost
    {
        private readonly Dictionary<CombatEvent, ShieldSnapshot> shieldFrames = new Dictionary<CombatEvent, ShieldSnapshot>();
        private Coroutine playback;
        private bool busy;
        private BattleSessionResult pendingPresentation;

        void IBattleSessionPresentation.Initialize(CardDuelState initialState)
        {
            shieldFrames.Clear(); pendingPresentation = null;
            PresentedState = initialState;
            LastScore = null; LastDamage = 0; LastPlayResolution = null; LastPlayedBy = default;
            LastDiscardResolution = null; LastDiscardedBy = default;
            var limits = initialState.Duel.Player(initialState.Duel.PlayerId).Rules;
            Message = "出牌 1–" + limits.MaxPlayCards + " 张 · 弃牌 1–" + limits.MaxDiscardCards + " 张 · 能量用尽或手动结束回合";
            BindActors(); Bind();
        }
        void IBattleSessionPresentation.Present(BattleSessionResult batch, bool animate)
        {
            var first = batch.Steps[0];
            if (first.Action == CardDuelAction.Start)
            {
                PresentedState = batch.State; Project(animate); Publish(); return;
            }
            PreparePresentation(batch);
            if (first.Action == CardDuelAction.Play || first.Action == CardDuelAction.Discard)
            {
                bool play = first.Action == CardDuelAction.Play;
                if (play) { LastPlayResolution = first.Transition; LastPlayedBy = first.Actor; }
                else { LastDiscardResolution = first.Transition; LastDiscardedBy = first.Actor; }
                var hand = actions.Hand;
                var frame = play
                    ? match.PrepareExternalPlay(playerActor, hand.Zone.Id, PlayerZone(CardZoneKinds.Played).Id, first.Selected, playerActor.Rules.MaxPlayCards)
                    : match.PrepareExternalDiscard(playerActor, hand.Zone.Id, hand.DiscardZone.Id, first.Selected);
                var view = hand.GetComponent<HandZoneView>();
                var cardAction = ((ICardActionView)view).PresentActionCommitted(play ? CardViewActionKind.Play : CardViewActionKind.Discard,
                    frame.Project(hand.Zone.Id), first.Selected.Select(id => id.Value), () => match.Commit(frame), animate);
                match.Notify(frame, view.Data); BeginPresentation(batch, animate, cardAction);
            }
            else BeginPresentation(batch, animate);
        }
        void IBattleSessionPresentation.Synchronize(CardDuelState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            try
            {
                if (playback != null) StopCoroutine(playback);
                SynchronizePresentation(state);
            }
            finally { playback = null; busy = false; Publish(); }
        }
        private void RecordBlockEffects(CardDuelTransition result)
        {
            foreach (var effect in result.Events)
            {
                if (shieldFrames.ContainsKey(effect)) continue;
                ShieldCard[] cards = null;
                if (result.DiscardReward != null && effect.Effect.Kind == CombatEffectKind.GainBlock &&
                    effect.Effect.Source == effect.Effect.Target)
                    cards = result.DiscardReward.Context.Cards.Select(card => new ShieldCard(card.Id.Value, StandardArt(Engine.Face(card.Card)))).ToArray();
                var target = Actors.FirstOrDefault(actor => actor != null && actor.ParticipantId == effect.Effect.Target);
                if (target != null) shieldFrames[effect] = target.RecordBlockEffect(effect, cards);
            }
        }
        private void PreparePresentation(BattleSessionResult batch)
        {
            pendingPresentation = batch; busy = true; shieldFrames.Clear();
            PresentStep(batch.Steps[0]);
        }
        private void PresentStep(BattleSessionStep step)
        {
            PresentedState = step.Transition.State;
            RecordBlockEffects(step.Transition);
            if (step.Action == CardDuelAction.CollectPlayed && PlayerZone(CardZoneKinds.Hand).Cards.Count == 0)
                Message = "手牌已用尽 · 请结束回合";
        }
        private void BeginPresentation(BattleSessionResult batch, bool animate, CardActionViewOperation cardAction = null)
        {
            if (animate && Application.isPlaying && isActiveAndEnabled)
            {
                var running = StartCoroutine(RunPresentation(PresentAfterCardAction(batch, cardAction)));
                playback = busy ? running : null;
            }
            else
            {
                try
                {
                    Show(batch.Steps[0].Transition); SnapEffects(batch.Steps[0].Transition);
                    foreach (var step in batch.Steps.Skip(1))
                    {
                        PresentStep(step); SnapEffects(step.Transition);
                        if (step.Action == CardDuelAction.EnemyAction) ShowEnemyAction(step.Transition);
                    }
                    Project(false); pendingPresentation = null;
                }
                catch { SnapToState(); throw; }
                finally { busy = false; Publish(); }
            }
            actions.Refresh();
        }
        // Flatten nested sequences so exceptions/cancellation from any protocol operation reach one recovery boundary.
        private IEnumerator RunPresentation(IEnumerator sequence)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(sequence);
            try
            {
                while (stack.Count > 0)
                {
                    bool next = false; object current = null; Exception failure = null;
                    try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null)
                    {
                        Debug.LogException(failure, this);
                        yield break;
                    }
                    if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested && !(current is CustomYieldInstruction)) { stack.Push(nested); continue; }
                    yield return current;
                }
            }
            finally
            {
                try
                {
                    while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                    // Completion, disposal and failed/cancelled animations cannot execute rules.
                    if (pendingPresentation != null) SynchronizePresentation(pendingPresentation.State);
                }
                finally { playback = null; busy = false; Publish(); }
            }
        }
        private static IEnumerator WaitForPresentation(BattlePresentationOperation operation)
        {
            yield return operation;
            if (operation.Status != BattlePresentationStatus.Completed)
                throw new InvalidOperationException("Battle presentation did not complete: " + operation.Status, operation.Error);
        }
        private IEnumerator Present(BattleSessionResult batch) => PresentAfterCardAction(batch, null);
        private IEnumerator PresentAfterCardAction(BattleSessionResult batch, CardActionViewOperation cardAction)
        {
            if (cardAction != null)
            {
                yield return cardAction;
                if (cardAction.Status != CardActionViewStatus.Completed)
                    throw new InvalidOperationException("Card action View did not complete: " + cardAction.Status, cardAction.Error);
            }
            else while (actions.Hand.GetComponent<HandZoneView>().IsTransitioning) yield return null;
            Show(batch.Steps[0].Transition);
            yield return PlayEffects(batch.Steps[0].Transition);
            foreach (var step in batch.Steps.Skip(1))
            {
                if (step.Action == CardDuelAction.EnemyAction)
                {
                    Message = "玩家回合结束 · 敌人准备攻击"; Publish();
                    yield return WaitForPresentation(Actor(step.Actor).PresentPreparation(true));
                }
                PresentStep(step);
                if (step.Action == CardDuelAction.EnemyAction) ShowEnemyAction(step.Transition);
                yield return PlayEffects(step.Transition);
            }
            Project(true); Publish();
            while (actions.Hand.GetComponent<HandZoneView>().IsTransitioning) yield return null;
            pendingPresentation = null;
        }
        private void ShowEnemyAction(CardDuelTransition result)
        {
            Message = "敌人造成 " + result.Events.Sum(item => item.HealthLost) + " 伤害"; Publish();
        }
        private void Show(CardDuelTransition result)
        {
            if (result.Score != null)
            {
                LastScore = result.Score; LastDamage = result.Events.Sum(item => (long)item.HealthLost);
                Message = SingleBattleHud.HandName(LastScore.Category) + " · " + LastScore.Total + " 分 → " + LastDamage + " 伤害";
            }
            else Message = result.State.Duel.Actions.LastOrDefault()?.Kind == ScoredDuelActionKind.Discard
                ? "弃牌" + ((playerActor.Rules.DealPolicy as CardDuelDealPolicy)?.AfterDiscard == CardDuelRefillTiming.AfterAction ? "并补牌" : " · 下回合补牌") + " · 消耗 " + playerActor.Rules.DiscardEnergyCost + " 能量"
                : State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "玩家主动结束回合" : "选择牌型继续行动";
            Publish();
        }
        private BattleActor Actor(ParticipantId id) => Actors.Single(actor => actor.ParticipantId == id);
        private IEnumerator PlayEffects(CardDuelTransition result)
        {
            foreach (var group in result.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).GroupBy(item => item.Effect.Source))
                yield return WaitForPresentation(Actor(group.Key).PresentAction(group.First(), true));
            foreach (var effect in result.Events)
            {
                var target = Actor(effect.Effect.Target);
                yield return WaitForPresentation(target.PresentEffect(effect, true, shieldFrames.TryGetValue(effect, out var shields) ? shields : null));
                yield return WaitForPresentation(target.PresentDeath(effect, true));
            }
        }
        private void SnapEffects(CardDuelTransition result)
        {
            foreach (var group in result.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).GroupBy(item => item.Effect.Source))
                Actor(group.Key).PresentAction(group.First(), false);
            foreach (var effect in result.Events)
            { var target = Actor(effect.Effect.Target); target.PresentEffect(effect, false, shieldFrames.TryGetValue(effect, out var shields) ? shields : null); target.PresentDeath(effect, false); }
        }
        private void Project(bool animate)
        {
            var old = actions.Hand.Cards.ToDictionary(card => card.Card.Id);
            var zones = State.Table.Zones.Select(zone => zone.Id != PlayerZone(CardZoneKinds.Hand).Id ? zone : new CardZone(zone.Id, zone.Kind, zone.ControllerId,
                actions.Hand.Cards.Select(card => card.Card.Id).Where(zone.Cards.Contains).Concat(zone.Cards.Where(id => !old.ContainsKey(id))))).ToArray();
            var styles = State.Table.Cards.Select(card => Style(card, old.TryGetValue(card.Id, out var before) &&
                PlayerZone(CardZoneKinds.Hand).Cards.Contains(card.Id) ? before.Interaction : default)).ToArray();
            var update = match.PrepareExternalProjection(playerActor, new CardTableState(State.Table.MatchId, State.Table.Cards, zones, State.Revision), styles);
            actions.Hand.GetComponent<HandZoneView>().PresentCommitted(update.Project(actions.Hand.Zone.Id), () => match.Commit(update), animate);
            match.Notify(update, actions.Hand.GetComponent<HandZoneView>().Data); overview?.Refresh(); drawPile?.Refresh();
            if (PlayerZone(CardZoneKinds.Hand).Cards.Any(id => !old.ContainsKey(id))) actions.Hand.RequestSortAfterLanding(actions.Hand.SortOrder);
        }
        public void SnapToState() => SynchronizeBattle();
        private void SynchronizePresentation(CardDuelState state)
        {
            foreach (var actor in Actors) if (actor != null) actor.CancelPresentation();
            // Apply skipped shield bookkeeping in event order before reconciling the final snapshot.
            // This only reconstructs visual card sources; Session has already committed all rules.
            if (pendingPresentation != null)
                foreach (var step in pendingPresentation.Steps) RecordBlockEffects(step.Transition);
            PresentedState = state; pendingPresentation = null;
            // Scene teardown order is independent of our hierarchy; Unity's destroyed objects
            // do not compare null through the null-conditional operator.
            if (actions == null || actions.Hand == null || match == null) return;
            var handView = actions.Hand.GetComponent<HandZoneView>();
            if (handView != null) handView.CompleteTransitions();
            if (State != null && ReferenceEquals(match.Commands, playerActor))
            {
                foreach (var actor in Actors) if (actor != null) actor.SnapToCurrentState();
                Project(false);
            }
        }
    }
}
