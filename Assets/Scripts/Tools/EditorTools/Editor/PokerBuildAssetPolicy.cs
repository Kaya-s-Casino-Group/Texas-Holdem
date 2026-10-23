using System;
using System.Collections.Generic;
using UnityEditor;

namespace Poker.EditorTools
{
    /// <summary>Reviewed desktop artwork only. Fonts, card atlases, masks and effects are deliberately excluded.</summary>
    public static class PokerBuildAssetPolicy
    {
        public readonly struct TextureRule
        {
            public readonly int MaxSize;
            public readonly bool DisableMipmaps;
            public TextureRule(int maxSize, bool disableMipmaps = false)
            { MaxSize = maxSize; DisableMipmaps = disableMipmaps; }
        }

        private static readonly Dictionary<string, TextureRule> Rules = CreateRules();
        public static IEnumerable<string> Paths => Rules.Keys;

        public static bool TryGetRule(string path, out TextureRule rule) => Rules.TryGetValue(path, out rule);

        // Called last by artwork setup tools so recreating a layout cannot remove its release override.
        // Retain source geometry, sprite borders and CPU alpha access. Four measured NPOT images
        // use no mip chain so their unchanged, block-aligned dimensions can actually import as BC7.
        public static bool ApplyStandalone(TextureImporter importer)
        {
            if (importer == null || !TryGetRule(importer.assetPath, out var rule)) return false;
            bool changed = rule.DisableMipmaps && importer.mipmapEnabled;
            if (changed) importer.mipmapEnabled = false;
            var settings = importer.GetPlatformTextureSettings("Standalone");
            if (settings.overridden && settings.maxTextureSize == rule.MaxSize &&
                settings.format == TextureImporterFormat.BC7 && settings.compressionQuality == 100 &&
                settings.textureCompression == TextureImporterCompression.CompressedHQ && !settings.crunchedCompression)
                return changed;
            settings.name = "Standalone";
            settings.overridden = true;
            settings.maxTextureSize = rule.MaxSize;
            settings.format = TextureImporterFormat.BC7;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            settings.crunchedCompression = false; // Boss hit testing uses GetPixelBilinear.
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        private static Dictionary<string, TextureRule> CreateRules()
        {
            var result = new Dictionary<string, TextureRule>(StringComparer.Ordinal);
            void Add(string root, int size, params string[] files)
            {
                foreach (var file in files) result.Add(root + file, new TextureRule(size));
            }
            const string characters = "Assets/Art/Characters/BattleBillboards/";
            Add(characters, 2048, "Elite-Updated.png", "Minion-Updated.png", "Enemy.png", "Player.png",
                "PlayerPoses/Player-Action.png", "PlayerPoses/Player-Discard.png", "PlayerPoses/Player-Ending.png",
                "WrathBoss/Body.png", "WrathBoss/Hand1.png", "WrathBoss/Hand2.png", "WrathBoss/Hand3.png",
                "WrathBoss/Hand4.png", "WrathBoss/Hand5.png");
            Add(characters, 1024, "WrathBoss/WarningGlow.png");
            Add("Assets/Art/Environment/BattleBackgrounds/", 2048,
                "Sources/Elite_Updated.jpg", "Sources/Minion_Updated.jpg", "Textures/Boss_Back.png",
                "Textures/Boss_Front.png", "Textures/Boss_Mid.png", "Textures/Minion_Back.png",
                "Textures/Minion_Front.png", "Textures/Minion_Mid.png");

            const string concept = "Assets/Art/UI/BattleConcept/Resources/BattleConcept/";
            Add(concept, 512, "Icons/icon_attack_sword.png", "Icons/icon_bleeding.png", "Icons/icon_buff.png",
                "Icons/icon_cleanse.png", "Icons/icon_coin.png", "Icons/icon_discard_trash.png",
                "Icons/icon_draw_pile.png", "Icons/icon_hand_guide.png", "Icons/icon_heal.png",
                "Icons/icon_immunity.png", "Icons/icon_overheal.png", "Icons/icon_poison.png",
                "Icons/icon_reflect.png", "Icons/icon_settings_gear.png", "Icons/icon_shield.png",
                "Icons/icon_sort_triangle.png", "Icons/icon_stun.png", "Icons/icon_unknown.png",
                "Icons/icon_weakness.png", "Portraits/portrait_hero.png");
            Add(concept, 1024, "Frames/frame_action_points.png", "Frames/frame_hand_tray.png",
                "Frames/frame_health_bar.png", "Frames/frame_player_portrait.png", "Frames/frame_relic_slot.png",
                "HealthBars/boss/fill.png", "HealthBars/boss/frame.png", "HealthBars/boss/track.png",
                "HealthBars/minion/fill.png", "HealthBars/minion/frame.png", "HealthBars/minion/track.png",
                "HealthBars/player/fill.png", "HealthBars/player/frame.png", "HealthBars/player/track.png",
                "Panels/panel_button_discard_compact.png", "Panels/panel_button_play_compact.png",
                "Panels/panel_button_primary.png", "Panels/panel_button_secondary.png", "Panels/panel_counter.png",
                "Panels/panel_effect_preview.png", "Panels/panel_enemy_intent.png", "Panels/panel_player_status.png",
                "Panels/panel_relic_rail.png", "Panels/panel_round.png", "Panels/panel_sort.png");

            const string desk = "Assets/Art/UI/Desk/Resources/DeskUI/";
            Add(desk, 2048, "MapBackground.png", "MapPaper.png", "MenuBackground.png", "RestBackground.png", "ShopBackground.png");
            Add(desk, 1024, "BossHealthFrame.png", "Button.png", "ContentButton.png", "ContentPanel.png",
                "DeckFrame.png", "FailureLogo.png", "HandTray.png", "HealthFrame.png", "MapPanel.png",
                "Menu.png", "MenuButton.png", "MenuLogo.png", "Panel.png", "RelicTray.png", "RoundFrame.png",
                "ShopCard.png", "ShopServiceCard.png", "StatFrame.png", "VictoryLogo.png");
            Add(desk, 512, "NodeBattle.png", "NodeBoss.png", "NodeChest.png", "NodeElite.png", "NodeRest.png",
                "NodeShop.png", "ServiceDelete.png", "ServiceDuplicate.png", "ServiceSuit.png", "SortButton.png");
            Add("Assets/Art/UI/Underworld/Resources/RitualUI/", 2048, "Campfire.jpg", "Map.png");
            Add("Assets/Art/UI/Underworld/Resources/RitualUI/", 1024, "Nodes.png", "Panel.png");
            Add("Assets/GameAssets/CardPresentation/UI/Icons/", 512, "DeckAce.png", "DeckFace.png", "DeckNumber.png");
            Add("Assets/Art/UI/Relics/", 512, "一串手雷.png", "俄罗斯转盘.png", "债券.png", "六面骰.png",
                "反甲.png", "存折.png", "心连心.png", "替死鬼.png", "遗物1.png", "遗物2.png", "遗物3.png", "遗物4.png");
            // Copy-only import probe: each keeps its exact dimensions, UVs and source aspect.
            // Other NPOT artwork retains its mip setting; never round its axes independently.
            foreach (var path in new[] { characters + "Elite-Updated.png", characters + "WrathBoss/WarningGlow.png",
                "Assets/Art/Environment/BattleBackgrounds/Sources/Elite_Updated.jpg",
                "Assets/Art/Environment/BattleBackgrounds/Sources/Minion_Updated.jpg" })
                result[path] = new TextureRule(result[path].MaxSize, disableMipmaps: true);
            // The fallback relic atlas is addressed with authored pixel rectangles; leave it unchanged.
            return result;
        }
    }
}
