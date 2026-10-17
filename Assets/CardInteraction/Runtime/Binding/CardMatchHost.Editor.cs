#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardInteraction
{
    public sealed partial class CardMatchHost
    {
        /// <summary>Clear validation seeds and require an external gameplay authority to provide all zones/cards.</summary>
        public void ConfigureRuntimeManaged()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Runtime ownership configuration is editor-only.");
            StopMatch(); runtimeManaged = true; matchId = null; initialRevision = 0; initialZones.Clear();
            runtimeSeed = null; runtimeSeedPresentation = null;
        }
        public void ConfigureInitial(IEnumerable<CardMatchZoneSnapshot> zones, string identity = null)
        {
            RequireLocalCommands();
            if (Application.isPlaying) throw new InvalidOperationException("Initial match configuration is editor-only.");
            var snapshots = Guard.Copy(zones, nameof(zones));
            string nextId = identity ?? (string.IsNullOrEmpty(matchId) ? "match/" + Guid.NewGuid().ToString("N") : matchId);
            var state = new CardTableState(new MatchId(nextId), snapshots.SelectMany(zone => zone.Cards).Select(card => card.Card),
                snapshots.Select(zone => zone.Zone), checked(initialRevision + 1));
            SaveInitial(state, snapshots.SelectMany(zone => zone.Cards).ToDictionary(card => card.Id, card => card.Copy(clearHover: true)));
        }
        private void SaveInitial(CardTableState state, IReadOnlyDictionary<string, CardZoneCardState> styles)
        {
            var snapshots = state.Zones.Select(zone => new CardMatchZoneSnapshot(zone, zone.Cards.Select(id => styles[id.Value]))).ToList();
            initialZones = snapshots; matchId = state.MatchId.Value; initialRevision = state.Revision;
            runtimeSeed = null; runtimeSeedPresentation = null;
            StopMatch();
        }
        public CardMatchUpdate PrepareDebugEdit(IEnumerable<CardMatchZoneSnapshot> zones, bool preview)
        {
            RequireLocalCommands();
            var snapshots = Guard.Copy(zones, nameof(zones));
            var owner = preview ? new CardTableSession(InitialState) : session ?? throw new InvalidOperationException("Start the match.");
            var styles = snapshots.SelectMany(zone => zone.Cards).ToArray();
            return Wrap(owner.PrepareZones(snapshots.Select(zone => zone.Zone), styles.Select(card => card.Card)), owner, preview, styles);
        }
        public CardMatchUpdate PreparePreviewPlay(ZoneId source, ZoneId destination, IEnumerable<CardInstanceId> selected, int capacity)
        {
            RequireLocalCommands();
            if (Application.isPlaying) throw new InvalidOperationException("Preview commands are editor-only.");
            var owner = new CardTableSession(InitialState);
            return Wrap(owner.PreparePlay(source, destination, selected, capacity), owner, true, clearTransferredInput: true);
        }
        public static CardMatchHost FindOrCreate(Scene scene)
        {
            var found = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardMatchHost>(true)).FirstOrDefault();
            if (found != null) return found;
            var root = new GameObject("Card Match"); SceneManager.MoveGameObjectToScene(root, scene);
            var match = root.AddComponent<CardMatchHost>(); match.ConfigureInitial(Array.Empty<CardMatchZoneSnapshot>()); return match;
        }
    }
}
#endif
