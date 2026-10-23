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
        private readonly List<GuidePreloadJob> guidePreloadJobs = new List<GuidePreloadJob>();
        private readonly HashSet<int> guideExpandedQueries = new HashSet<int>();
        private int guidePreloadFrame;
        private int CurrentGuideQueryKey => (int)guideType * 2 + (guideFlush ? 1 : 0);
        private sealed class GuidePreloadJob
        {
            public int Key;
            public bool Expanded;
            public IEnumerator<HandGuideResult> Search;
        }

        private void QueueHandGuidePreloads()
        {
            // Only spend work on the condition the player is reading. Other selections retain their
            // enumerators while paused, but opening the guide never starts twenty background searches.
            int key = CurrentGuideQueryKey;
            if (FindGuidePreload(key) == null)
            {
                bool cached = guideQueries.TryGetValue(key, out var result);
                if (!cached || result.Truncated && result.Error == CardDuelError.None && !guideExpandedQueries.Contains(key))
                    AddGuidePreload(key, cached);
            }
            // Give the new selection one render before any work; existing jobs retain their progress.
            guidePreloadFrame = Time.frameCount;
        }
        private void AddGuidePreload(int key, bool expanded)
        {
            var request = new HandGuideRequest((HandGuideType)(key / 2), key % 2 != 0,
                expanded ? GuideCandidateLimit : 1, expanded ? GuideExpandedNodeBudget : GuideQuickNodeBudget);
            guidePreloadJobs.Add(new GuidePreloadJob { Key = key, Expanded = expanded,
                Search = guideCacheEngine.QueryHandGuideIncremental(guideCacheState, guideCacheActor, request, nodesPerStep: 1).GetEnumerator() });
        }
        private GuidePreloadJob FindGuidePreload(int key)
        {
            foreach (var job in guidePreloadJobs) if (job.Key == key) return job;
            return null;
        }
        private void AdvanceHandGuidePreloads()
        {
            if (Time.frameCount == guidePreloadFrame) return;
            long started = Stopwatch.GetTimestamp();
            while (guidePreloadJobs.Count != 0 && (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency < 2)
            {
                var job = FindGuidePreload(CurrentGuideQueryKey);
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
                    PresentHandGuideResult(result);
                    // Present only the visible candidate. Do not eagerly settle the remaining examples.
                    return;
                }
                catch (Exception error)
                {
                    RemoveGuidePreload(job); guideExpandedQueries.Add(job.Key);
                    if (job.Key == CurrentGuideQueryKey && (guideResult == null || guideResult.Candidates.Count == 0)) PresentHandGuideBasicRules();
                    Debug.LogException(error, this);
                }
            }
        }
        private void RemoveGuidePreload(GuidePreloadJob job)
        { guidePreloadJobs.Remove(job); job.Search.Dispose(); }
        private void CancelHandGuidePreloads()
        {
            foreach (var job in guidePreloadJobs) job.Search.Dispose();
            guidePreloadJobs.Clear();
        }
    }
}
