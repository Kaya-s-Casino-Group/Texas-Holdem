using System;
using System.Collections.Generic;
using PokerEngine;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private const int GuideQuickNodeBudget = 4000, GuideExpandedNodeBudget = 12000, GuideCandidateLimit = 6;
        private static readonly HandGuideType[] GuidePreloadTypes = (HandGuideType[])Enum.GetValues(typeof(HandGuideType));
        private readonly List<GuidePreloadJob> guidePreloadJobs = new List<GuidePreloadJob>();
        private readonly HashSet<int> guideExpandedQueries = new HashSet<int>();
        private int guidePreloadCursor, guidePreloadFrame;
        private bool guidePriorityTurn;
        private int CurrentGuideQueryKey => (int)guideType * 2 + (guideFlush ? 1 : 0);
        private sealed class GuidePreloadJob
        {
            public int Key;
            public bool Expanded;
            public IEnumerator<HandGuideResult> Search;
        }

        private void QueueHandGuidePreloads()
        {
            for (int pass = 0; pass < 2; pass++) foreach (var type in GuidePreloadTypes)
            {
                bool flush = pass == 0 ? guideFlush : !guideFlush;
                int key = (int)type * 2 + (flush ? 1 : 0);
                if (FindGuidePreload(key) != null) continue;
                bool cached = guideQueries.TryGetValue(key, out var result);
                if (cached && (!result.Truncated || result.Error != CardDuelError.None || guideExpandedQueries.Contains(key))) continue;
                AddGuidePreload(key, cached);
            }
            // Give the new selection one render before any work; existing jobs retain their progress.
            guidePreloadFrame = Time.frameCount; guidePriorityTurn = true;
        }
        private void AddGuidePreload(int key, bool expanded)
        {
            var request = new HandGuideRequest((HandGuideType)(key / 2), key % 2 != 0,
                expanded ? GuideCandidateLimit : 1, expanded ? GuideExpandedNodeBudget : GuideQuickNodeBudget);
            guidePreloadJobs.Add(new GuidePreloadJob { Key = key, Expanded = expanded,
                Search = guideCacheEngine.QueryHandGuideIncremental(guideCacheState, guideCacheActor, request).GetEnumerator() });
        }
        private GuidePreloadJob FindGuidePreload(int key)
        {
            foreach (var job in guidePreloadJobs) if (job.Key == key) return job;
            return null;
        }
        private GuidePreloadJob NextGuidePreload()
        {
            guidePriorityTurn = !guidePriorityTurn;
            if (!guidePriorityTurn && (!guideQueries.TryGetValue(CurrentGuideQueryKey, out var result) || result.Candidates.Count == 0))
            {
                var current = FindGuidePreload(CurrentGuideQueryKey);
                if (current != null) return current;
            }
            // Fairly share background batches: obtain an example for every filter before expanding them.
            bool quick = false;
            foreach (var job in guidePreloadJobs) if (!job.Expanded) { quick = true; break; }
            for (int i = 0; i < guidePreloadJobs.Count; i++)
            {
                guidePreloadCursor %= guidePreloadJobs.Count;
                var job = guidePreloadJobs[guidePreloadCursor++];
                if (!quick || !job.Expanded) return job;
            }
            return null;
        }
        private void AdvanceHandGuidePreloads()
        {
            if (Time.frameCount == guidePreloadFrame) return;
            long started = Stopwatch.GetTimestamp();
            while (guidePreloadJobs.Count != 0 && (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency < 2)
            {
                var job = NextGuidePreload();
                if (job == null) break;
                try
                {
                    if (!job.Search.MoveNext()) { RemoveGuidePreload(job); continue; }
                    var result = job.Search.Current;
                    if (result == null) continue;
                    RemoveGuidePreload(job);
                    if (job.Expanded) guideExpandedQueries.Add(job.Key);
                    // A bounded follow-up must never erase an already found valid example.
                    if (result.Candidates.Count == 0 && guideQueries.TryGetValue(job.Key, out var previous) && previous.Candidates.Count != 0) result = previous;
                    guideQueries[job.Key] = result;
                    if (!job.Expanded && result.Truncated && result.Error == CardDuelError.None) AddGuidePreload(job.Key, true);
                    if (result.Candidates.Count != 0) GetGuideCandidateText(result.Candidates[0]);
                    if (job.Key == CurrentGuideQueryKey) PresentHandGuideResult(result);
                    // At most one newly prepared formula/settlement per frame, including background work.
                    if (result.Candidates.Count != 0) return;
                }
                catch (Exception error)
                {
                    RemoveGuidePreload(job); guideExpandedQueries.Add(job.Key);
                    if (job.Key == CurrentGuideQueryKey && (guideResult == null || guideResult.Candidates.Count == 0)) PresentHandGuideBasicRules();
                    Debug.LogException(error, this);
                }
            }
            if (guidePreloadJobs.Count != 0) return;
            // Reopening retains queries but refreshes target-dependent previews. Warm one remaining preview per frame.
            foreach (var result in guideQueries.Values) foreach (var candidate in result.Candidates)
                if (!guideTexts.ContainsKey(candidate.Id)) { GetGuideCandidateText(candidate); return; }
        }
        private void RemoveGuidePreload(GuidePreloadJob job)
        { guidePreloadJobs.Remove(job); job.Search.Dispose(); }
        private void CancelHandGuidePreloads()
        {
            foreach (var job in guidePreloadJobs) job.Search.Dispose();
            guidePreloadJobs.Clear(); guidePreloadCursor = 0;
        }
    }
}
