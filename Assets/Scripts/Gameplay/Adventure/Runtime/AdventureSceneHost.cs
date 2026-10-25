using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Poker.SingleBattle;
using PokerEngine;
using UnityEngine;

namespace Poker.Adventure
{
    /// <summary>The only run authority. Accepted candidates are saved before replacing the live run.</summary>
    [DefaultExecutionOrder(-500), DisallowMultipleComponent]
    public sealed partial class AdventureSceneHost : MonoBehaviour, IBattleSessionCommands
    {
        [SerializeField] private TextAsset battleXml;
        [SerializeField] private TextAsset adventureXml;
        [SerializeField] private SingleBattleSceneAdapter battleView;
        [SerializeField] private GameObject[] battleRoots = Array.Empty<GameObject>();
        [SerializeField] private bool saveAutomatically = true;
        private AdventureSaveStore store;
        private BattleAdventureSession savedSession;
        private BattleSession lastBattle;
        private bool awaitingPresentation;
        private readonly BattleResultDelay resultDelay = new BattleResultDelay();
        private bool initialized;
        private readonly Dictionary<string, string> names = new Dictionary<string, string>();
        private readonly Dictionary<string, string> descriptions = new Dictionary<string, string>();
        public BattleAdventureSession Adventure { get; private set; }
        public CompiledBattleContent Content { get; private set; }
        public BattleAdventureDefinition Definition { get; private set; }
        public bool IsAtMenu { get; private set; } = true;
        public string Message { get; private set; } = "";
        public bool IsBusy => IsLoading || awaitingPresentation || battleView != null && battleView.IsPresenting;
        public bool HasSavedRun => savedSession != null;
        public bool HasUnfinishedRun => (Adventure ?? savedSession) != null && !Terminal(Adventure ?? savedSession);
        public SingleBattleSceneAdapter BattleView => battleView;
        public BattleSession Session => lastBattle;
        public long Revision => Adventure?.Revision ?? 0;
        public event Action Changed;
        public IReadOnlyList<AdventureTarotDefinition> AvailableTarots => Definition == null ? Array.Empty<AdventureTarotDefinition>() :
            Definition.Tarots.Where(t => (Adventure ?? savedSession)?.Progression.IsUnlocked(new ProgressionUnlock(UnlockKind.Tarot, t.Id)) ?? t.InitiallyUnlocked).ToArray();

        public void Configure(TextAsset battle, TextAsset adventure, SingleBattleSceneAdapter view = null, GameObject[] roots = null,
            bool autoSave = true, string savePath = null)
        {
            if (initialized) throw new InvalidOperationException("Configure before initializing the adventure.");
            battleXml = battle; adventureXml = adventure; battleView = view; battleRoots = roots ?? Array.Empty<GameObject>();
            saveAutomatically = autoSave;
            if (savePath != null) store = new AdventureSaveStore(savePath);
        }
        private void Awake()
        {
            if (battleXml == null || adventureXml == null) return;
            if (Application.isPlaying && BeginStartup()) return;
            try { Initialize(); }
            catch (Exception error) { Report("配置载入失败：" + error.Message); }
        }
        public void Initialize()
        {
            if (initialized) return;
            if (battleXml == null || adventureXml == null) throw new InvalidOperationException("请绑定单章战斗与冒险 XML。");
            var compiled = BattleContentCompiler.CompileXml(battleXml.text, battleXml.name + ".xml");
            if (!compiled.Success) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
            Content = compiled.Content; Definition = BattleAdventureXml.Load(adventureXml.text, Content);
            foreach (var element in XDocument.Parse(battleXml.text).Descendants("Relic"))
            {
                string id = (string)element.Attribute("id"); if (id == null) continue;
                names[id] = (string)element.Attribute("name") ?? id;
                if (names[id].StartsWith(id + "·", StringComparison.Ordinal)) names[id] = names[id].Substring(id.Length + 1);
            }
            foreach (var item in Definition.ShopItems) { names[item.RelicId] = item.Name; descriptions[item.RelicId] = item.Description; }
            store = store ?? new AdventureSaveStore(Path.Combine(Application.persistentDataPath, "single-chapter-run.json"));
            if (battleView != null) battleView.SceneHost.Configure(battleView, battleView, false, true);
            initialized = true;
            if (saveAutomatically && store.Exists)
            {
                try { savedSession = BattleAdventureSession.Restore(Definition, Content, store.Read()); }
                catch (Exception error) { Message = "存档无法载入，原文件已保留：" + error.Message; }
            }
            ShowBattle(false); Changed?.Invoke();
        }
        public string ItemName(string id) => id != null && names.TryGetValue(id, out var name) ? name : id ?? "";
        public string ItemDescription(string id) => id != null && descriptions.TryGetValue(id, out var text) && !string.IsNullOrEmpty(text) ? text :
            id != null && Content.Relics.ContainsKey(id) ? Poker.BattlePresentation.BattlePresentationRuleText.RelicDescription(Content.Relics[id].Rules) : "";
        public string CharacterName => Content == null ? "旅人" : Content.Encounters[Definition.StartingEncounterId].Players[0].TemplateId;

        public bool NewRun(string tarotId, ulong? seed = null)
        {
            if (IsBusy) return false;
            try
            {
                Initialize();
                if (HasUnfinishedRun) return Report("当前旅程尚未结束，请继续旅程或先放弃本局。");
                var next = new BattleAdventureSession(Definition, Content, "chapter/" + Guid.NewGuid().ToString("N"),
                    seed ?? BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0), tarotId, (Adventure ?? savedSession)?.Progression);
                Commit(next); IsAtMenu = false; ShowBattle(false); Publish(); return true;
            }
            catch (Exception error) { return Report(error.Message); }
        }
        public bool Resume()
        {
            if (IsBusy) return false;
            try
            {
                Initialize(); Adventure = Adventure ?? savedSession;
                if (Adventure == null) return Report("暂无可继续的旅程。");
                IsAtMenu = false;
                if (Adventure.Phase == AdventurePhase.Battle && battleView != null)
                {
                    // OnEnable synchronizes an existing bridge. Keep its previous battle until that completes.
                    ShowBattle(true); lastBattle = Adventure.Battle;
                    battleView.ConfigureContent(battleXml, Adventure.CurrentNode.EncounterId);
                    battleView.AttachCommands(this, animate: false);
                }
                else { ShowBattle(false); lastBattle = Adventure.Battle ?? lastBattle; }
                ClearGoldIncomeFeedback();
                Message = "";
                Publish(); return true;
            }
            catch (Exception error) { return Report(error.Message); }
        }
        public void ReturnToMenu()
        {
            if (IsBusy) return;
            ClearGoldIncomeFeedback();
            IsAtMenu = true; ShowBattle(false); Publish();
        }
        public bool Abandon()
        {
            if (Adventure == null && savedSession != null) Adventure = savedSession;
            return ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Abandon));
        }
        public bool ExecuteAdventure(AdventureCommand command)
        {
            if (Adventure == null || IsBusy) return false;
            try
            {
                var next = Adventure.Fork();
                var result = next.Execute(Adventure.Revision, command);
                if (!result.Accepted) return Report(result.Error);
                // Save failure must leave the currently visible battle and its input state untouched.
                // Keep the old view during OnDisable/OnEnable; attach the next battle only afterwards.
                Commit(next, updateBattleView: false, command: command); IsAtMenu = false;
                if (result.BattleResult != null && battleView != null)
                {
                    resultDelay.Reset(); awaitingPresentation = true;
                    ShowBattle(true); lastBattle = Adventure.Battle;
                    battleView.ConfigureContent(battleXml, Adventure.CurrentNode.EncounterId);
                    battleView.AttachCommands(this, result.BattleResult, true);
                    // Switch away from the map now so the opening animation is actually visible.
                    Publish();
                }
                else { ShowBattle(false); lastBattle = Adventure.Battle ?? lastBattle; Publish(); }
                return true;
            }
            catch (Exception error) { return Report(error.Message); }
        }
        public BattleSessionResult Execute(long expectedRevision, ParticipantId actor, CardDuelAction action,
            IEnumerable<CardInstanceId> selected = null, IEnumerable<ParticipantId> targets = null,
            BattleTargetBindings targetBindings = null, int drawCount = 0) =>
            SubmitBattle(expectedRevision, actor, next => next.ExecuteBattle(expectedRevision, action, selected, targets, targetBindings, drawCount));
        public BattleSessionResult UseItem(long expectedRevision, ParticipantId actor, string instanceId, string targetInstanceId = null) =>
            SubmitBattle(expectedRevision, actor, next => next.UseBattleItem(expectedRevision, instanceId, targetInstanceId));
        public BattleSessionResult ApplyAdmin(long expectedRevision, BattleAdminPatch patch) =>
            SubmitBattle(expectedRevision, Adventure?.PlayerId ?? default, next => next.ApplyAdmin(expectedRevision, patch), reportGold: false);
        private BattleSessionResult SubmitBattle(long expectedRevision, ParticipantId actor, Func<BattleAdventureSession, AdventureResult> submit, bool reportGold = true)
        {
            if (lastBattle == null) throw new InvalidOperationException("Enter a battle before submitting battle commands.");
            if (Adventure != null && expectedRevision != Adventure.Revision)
                return BattleSessionResult.Rejected(lastBattle.State, CardDuelError.StaleRevision);
            if (Adventure == null || Adventure.Phase != AdventurePhase.Battle || actor != Adventure.PlayerId || IsAtMenu || IsBusy)
                return BattleSessionResult.Rejected(lastBattle.State, CardDuelError.InvalidPhase);
            try
            {
                var next = Adventure.Fork(); var result = submit(next);
                if (!result.Accepted) { Report(result.Error); return BattleSessionResult.Rejected(lastBattle.State, CardDuelError.InvalidPhase); }
                Commit(next, battleCommand: reportGold); lastBattle = Adventure.Battle; resultDelay.Reset(); awaitingPresentation = battleView != null;
                if (!awaitingPresentation) Publish();
                return result.BattleResult;
            }
            catch (Exception error)
            { Report("操作未保存：" + error.Message); return BattleSessionResult.Rejected(lastBattle.State, CardDuelError.InvalidPhase); }
        }
        private void Commit(BattleAdventureSession next, bool updateBattleView = true, AdventureCommand command = null, bool battleCommand = false)
        {
            // Export validates serializability before the authority or UI can observe this candidate.
            if (saveAutomatically) store.Write(next.Export());
            var previous = Adventure;
            Adventure = next; savedSession = next;
            CaptureGoldIncomeFeedback(previous, next, command, battleCommand);
            if (updateBattleView) lastBattle = next.Battle ?? lastBattle;
            Message = "";
        }
        private void LateUpdate()
        {
            // A script reload can retain the pending flag while dropping the runtime-only session.
            if (Adventure == null) { awaitingPresentation = false; resultDelay.Reset(); return; }
            if (!awaitingPresentation) return;
            bool presenting = battleView != null && battleView.IsPresenting;
            bool finished = Adventure.Phase != AdventurePhase.Battle;
            bool ready = resultDelay.Ready(finished, presenting, Time.unscaledTimeAsDouble);
            if (presenting || finished && !ready) return;
            awaitingPresentation = false;
            if (Adventure.Phase != AdventurePhase.Battle) ShowBattle(false);
            Publish();
        }
        private void ShowBattle(bool visible)
        {
            if (visible) GetComponent<AdventureBattleLayout>()?.PrepareEncounter();
            foreach (var root in battleRoots) if (root != null && root.activeSelf != visible) root.SetActive(visible);
        }
        private bool Report(string message) { Message = message; Changed?.Invoke(); return false; }
        private void Publish() => Changed?.Invoke();
        private static bool Terminal(BattleAdventureSession session) => session.Phase == AdventurePhase.Completed || session.Phase == AdventurePhase.Failed;
    }
}
