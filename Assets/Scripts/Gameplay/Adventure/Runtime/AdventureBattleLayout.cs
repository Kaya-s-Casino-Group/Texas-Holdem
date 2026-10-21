using Poker.SingleBattle;
using PokerEngine;
using System.Linq;
using UnityEngine;

namespace Poker.Adventure
{
    /// <summary>Encounter scenery and adaptive enemy positions for the single-scene run.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class AdventureBattleLayout : MonoBehaviour
    {
        [SerializeField] private AdventureSceneHost host;
        [SerializeField] private BattleBackgroundSwitcher backgrounds;
        [SerializeField] private Material minionArtwork, eliteArtwork, originalArtwork;
        private string location;
        public void Configure(AdventureSceneHost owner, BattleBackgroundSwitcher scenery)
        { host = owner; backgrounds = scenery; }

        public void ConfigureArtwork(Material minion, Material elite, Material original)
        { minionArtwork = minion; eliteArtwork = elite; originalArtwork = original; }

        // Called while the battle root is inactive, before body/ash presenters cache their textures.
        public void PrepareEncounter()
        {
            if (host == null || host.BattleView == null || host.Adventure?.CurrentNode == null) return;
            var kind = host.Adventure.CurrentNode.Kind;
            var material = kind == AdventureNodeKind.Battle ? minionArtwork :
                kind == AdventureNodeKind.Elite ? eliteArtwork : originalArtwork;
            if (material == null) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var actor in root.GetComponentsInChildren<EnemyBattleActor>(true))
            {
                if (actor.Battle != host.BattleView) continue;
                ApplyArtwork(actor, material);
            }
        }

        private static void ApplyArtwork(EnemyBattleActor actor, Material material)
        {
            if (material == null) return;
            if (actor.Presentation is BattleDemoAshPresenter ash) ash.ConfigureArtwork(material);
            else
            {
                var artwork = actor.Visual != null ? actor.Visual.Find("Artwork") : null;
                var renderer = artwork != null ? artwork.GetComponent<MeshRenderer>() : null;
                if (renderer != null && renderer.sharedMaterial != material)
                { renderer.sharedMaterial = material; renderer.SetPropertyBlock(null); }
            }
            var texture = material.GetTexture("_BaseMap");
            if (texture != null) actor.GetComponent<BattleBillboardLayout>()?.SetArtworkAspect((float)texture.width / texture.height);
        }

        private void LateUpdate()
        {
            var view = host != null ? host.BattleView : null;
            if (view == null || !view.isActiveAndEnabled || view.PresentedState == null) return;
            var node = host.Adventure?.CurrentNode;
            if (node != null && location != node.Id)
            {
                location = node.Id;
                if (backgrounds != null) backgrounds.Select(node.Kind == AdventureNodeKind.Boss ? BattleBackground.Boss :
                    node.Kind == AdventureNodeKind.Elite ? BattleBackground.Elite : BattleBackground.Minion);
            }
            int count = view.Enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var actor = view.Enemies[i];
                var enemy = view.PresentedState.Duel.Enemies.FirstOrDefault(unit => unit.Id == actor.ParticipantId);
                ApplyArtwork(actor, node?.Kind == AdventureNodeKind.Boss ? originalArtwork :
                    node?.Kind == AdventureNodeKind.Elite && enemy != null && !enemy.SummonerId.HasValue ? eliteArtwork : minionArtwork);
                var layout = view.Enemies[i].GetComponent<BattleBillboardLayout>();
                if (layout == null) continue;
                Rect area;
                if (count == 1) area = new Rect(.56f, .35f, .28f, .42f);
                else if (count == 2) area = new Rect(.46f + i * .22f, .35f, .19f, .42f);
                else if (count == 3) area = new Rect(.40f + i * .16f, .35f, .15f, .42f);
                else
                {
                    int rows = (count + 2) / 3;
                    float height = .50f / rows;
                    area = new Rect(.40f + i % 3 * .16f, .32f + (rows - 1 - i / 3) * (height + .03f), .15f, height - .03f);
                }
                layout.SetViewportArea(area);
            }
        }
    }
}
