using System;
using System.Linq;
using CardPresentation;
using Poker.SingleBattle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.Adventure.Editor
{
    public static class AdventureAudioSetup
    {
        public const string AudioRoot = "Assets/Audio/Adventure/";
        private const string SfxRoot = "Assets/Audio/Balatro/SFX/";

        [MenuItem("工具/局外冒险/接入单章音乐与音效")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放模式。");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != AdventureSceneSetup.ScenePath) throw new InvalidOperationException("请先打开单章冒险场景。");
            ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("已接入单章主菜单、普通、精英、Boss、休息音乐，以及卡牌、战斗、按钮与结算音效。");
        }

        public static AdventureAudioController ApplyToScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != AdventureSceneSetup.ScenePath)
                throw new InvalidOperationException("只在编辑模式配置 SingleChapterAdventure。");
            // Validate all delivered clips before changing the scene.
            var music = new[] { "MainMenu", "NormalBattle", "EliteBattle", "Boss", "Rest" }.Select(name => Import(name, true)).ToArray();
            var draws = new[] { Import("Draw1", false), Import("Draw2", false) };
            var enemyAttack = Import("EnemyAttack", false);
            var enemyHit = Import("EnemyHit", false);
            var button = Existing("03_按钮与界面提示/button__按钮点击与总分显示.ogg");
            var invalid = Existing("03_按钮与界面提示/cancel__失效警示与结算条目.ogg");
            var coins = Existing("02_筹码与金钱/coin1__金钱余额变化.ogg");
            var win = Existing("04_计分与结算强调/win__通关胜利.ogg");
            var death = Existing("06_销毁与动作特效/crumple1__卡牌揉碎消散.ogg");
            var playerAttack = Existing("06_销毁与动作特效/whoosh1__掠过与生成过渡.ogg");
            var playerHit = Existing("04_计分与结算强调/multhit1__加倍率计分.ogg");
            var blockHit = Existing("02_筹码与金钱/chips2__分数入账与计数变化.ogg");
            var roots = scene.GetRootGameObjects();
            var host = roots.SelectMany(r => r.GetComponentsInChildren<AdventureSceneHost>(true)).Single();
            var controller = host.GetComponent<AdventureAudioController>();
            if (controller == null) controller = host.gameObject.AddComponent<AdventureAudioController>();
            var musicSource = Voice(controller.transform, "Adventure Music");
            var feedback = Voice(controller.transform, "Adventure Feedback");
            musicSource.loop = true; musicSource.volume = 0;
            controller.Configure(host, musicSource, feedback, music, draws);
            controller.ConfigureFeedback(button, invalid, coins, win, invalid, invalid);
            EditorUtility.SetDirty(controller);

            var view = host.BattleView;
            var hand = view.Actions.Hand.GetComponent<HandZoneView>();
            hand.SetDrawSound(draws[0], draws[0].length > 1.5f ? .2f : .55f);
            hand.SetSelectionDeniedSound(invalid, .55f);
            EditorUtility.SetDirty(hand);
            foreach (var actor in roots.SelectMany(r => r.GetComponentsInChildren<BattleActor>(true)))
            {
                if (actor.Battle != view || !(actor.Presentation is BattleActorPresenter presenter)) continue;
                var audioRoot = Child(presenter.transform, "Audio");
                var actorAudio = presenter.Audio as BattleActorAudio;
                if (actorAudio == null) actorAudio = audioRoot.GetComponent<BattleActorAudio>();
                if (actorAudio == null) actorAudio = audioRoot.gameObject.AddComponent<BattleActorAudio>();
                SetCue(actorAudio, BattlePresentationKind.Attack, actor is EnemyBattleActor ? enemyAttack : playerAttack, .75f);
                SetCue(actorAudio, BattlePresentationKind.Hit, actor is EnemyBattleActor ? enemyHit : playerHit, .65f);
                SetCue(actorAudio, BattlePresentationKind.Death, death, .6f);
                actorAudio.Configure(Voice(actorAudio.transform, "Attack"), Voice(actorAudio.transform, "Hit"), Voice(actorAudio.transform, "Death"));
                presenter.ConfigureAudio(actorAudio);
                EditorUtility.SetDirty(actorAudio); EditorUtility.SetDirty(presenter);
            }
            foreach (var shield in roots.SelectMany(r => r.GetComponentsInChildren<DiscardShieldPresenter>(true)))
            {
                shield.BlockHitSound = blockHit; shield.BlockHitVolume = .5f;
                EditorUtility.SetDirty(shield);
            }
            foreach (var uiButton in roots.SelectMany(r => r.GetComponentsInChildren<Button>(true)))
            {
                AdventureAudioController.BindButton(uiButton, controller);
                EditorUtility.SetDirty(uiButton.GetComponent<AdventureButtonAudio>());
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return controller;
        }

        private static void SetCue(BattleActorAudio audio, BattlePresentationKind kind, AudioClip clip, float volume)
        {
            var cue = audio.GetSettings(kind); cue.Clip = clip; cue.Volume = volume;
            cue.Pitch = 1; cue.DelaySeconds = 0; cue.SpatialBlend = 0;
        }
        private static AudioClip Import(string name, bool music)
        {
            string path = AudioRoot + (music ? "Music/" : "SFX/") + name + ".mp3";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("缺少交付音频：" + path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = music ? .85f : 1;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false; importer.loadInBackground = music;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        private static AudioClip Existing(string relativePath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + relativePath);
            if (clip == null) throw new InvalidOperationException("缺少已有音效：" + relativePath);
            return clip;
        }
        private static Transform Child(Transform root, string name)
        {
            var child = root.Find(name);
            if (child != null) return child;
            var created = new GameObject(name); created.transform.SetParent(root, false); return created.transform;
        }
        private static AudioSource Voice(Transform root, string name)
        {
            var child = Child(root, name);
            var source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.dopplerLevel = 0;
            EditorUtility.SetDirty(source); return source;
        }
    }
}
