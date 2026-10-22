using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    // Run after DeskBattleLayout (550), leaving gameplay presenters and safe-area handling in charge.
    [DefaultExecutionOrder(650), DisallowMultipleComponent]
    public sealed class BattleAuthoredUILayout : MonoBehaviour
    {
        [Serializable] public sealed class Root { public string name; public Transform transform; }
        [SerializeField] private BattleUILayoutProfile profile;
        [SerializeField] private Root[] roots = Array.Empty<Root>();
        private readonly Dictionary<string, RectTransform> targets = new Dictionary<string, RectTransform>();
        public BattleUILayoutProfile Profile => profile;
        public void Configure(BattleUILayoutProfile value, Root[] bindings)
        { profile = value; roots = bindings; targets.Clear(); }
        private void OnEnable() => targets.Clear();
        private void LateUpdate() => Apply();
        public Transform FindRoot(string name)
        { foreach (var root in roots) if (root.name == name) return root.transform; return null; }
        public RectTransform Resolve(string rootName, string path)
        {
            var key = rootName + "/" + path;
            if (targets.TryGetValue(key, out var found) && found != null) return found;
            var root = FindRoot(rootName); if (root == null) return null;
            found = (root.Find(path) ?? root.Find("Safe Area/" + path)) as RectTransform;
            if (found != null) targets[key] = found;
            return found;
        }
        public void Apply()
        {
            if (profile == null) return;
            foreach (var canvas in profile.canvases)
            {
                var root = FindRoot(canvas.root);
                if (root != null && root.TryGetComponent<CanvasScaler>(out var scaler)) canvas.Apply(scaler);
            }
            foreach (var node in profile.nodes)
            {
                if (!BattleUILayoutProfile.Includes(node.root, node.pose.path)) continue;
                var target = Resolve(node.root, node.pose.path);
                if (target != null) node.Apply(target);
            }
        }
    }
}
