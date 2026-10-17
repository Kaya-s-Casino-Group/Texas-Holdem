#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardPresentation.Editor
{
    /// <summary>Editor-owned decks per zone, with independent serializable copies for one Play session.</summary>
    [Serializable]
    public sealed class CardZoneDrawExperimentStore
    {
        [Serializable]
        private sealed class ZoneState
        {
            public string Key;
            public CardZoneDrawExperiment Edit = new CardZoneDrawExperiment();
            public CardZoneDrawExperiment Play;
            public bool HasPlayCopy;
        }

        [SerializeField] private List<ZoneState> zones = new List<ZoneState>();
        [SerializeField] private bool playing;

        public CardZoneDrawExperiment Get(string key, bool inPlay, CardZoneDrawExperiment initial = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A zone context is required.", nameof(key));
            if (inPlay && !playing) BeginPlay();
            var state = zones.FirstOrDefault(zone => zone.Key == key);
            if (state == null)
            {
                state = new ZoneState { Key = key, Edit = initial ?? new CardZoneDrawExperiment() };
                zones.Add(state);
            }
            if (!inPlay) return state.Edit;
            if (!state.HasPlayCopy) CopyForPlay(state);
            return state.Play;
        }

        // Idempotent across editor re-enables and domain reloads during the same Play session.
        public void BeginPlay()
        {
            if (playing) return;
            foreach (var state in zones) CopyForPlay(state);
            playing = true;
        }

        public void EndPlay()
        {
            foreach (var state in zones) { state.Play = null; state.HasPlayCopy = false; }
            playing = false;
        }

        private static void CopyForPlay(ZoneState state)
        {
            state.Play = JsonUtility.FromJson<CardZoneDrawExperiment>(JsonUtility.ToJson(state.Edit));
            state.HasPlayCopy = true;
        }
    }
}
#endif
