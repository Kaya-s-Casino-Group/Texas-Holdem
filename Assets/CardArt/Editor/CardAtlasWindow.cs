using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Poker.EditorTools;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    public sealed partial class CardAtlasWindow : EditorWindow
    {
        private readonly ToolWindowChrome windowChrome = new ToolWindowChrome();
        [SerializeField] private CardAtlasRecipe recipe;
        [SerializeField] private int selected;
        [SerializeField] private int tab;
        [SerializeField] private bool onlyIssues;
        [SerializeField] private string scannedFolder;
        private bool showNamingRules;
        private Vector2 sidebarScroll, contentScroll;
        [SerializeField] private float zoom = .3f;
        [SerializeField] private bool fitPreview = true;
        [SerializeField] private bool showSettings;
        private bool WideLayout => position.width >= 720;
        private float SidebarWidth => Mathf.Clamp(position.width * .3f, 285, 360);
        private float ContentWidth => position.width - (WideLayout ? SidebarWidth + 12 : 12);
        private string error, status;
        private string dimensionError;
        private readonly CardImageValidator imageValidator = new CardImageValidator();
        private List<CardImageEntryCheck> imageChecks;
        private readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();
        private readonly List<Texture2D> ownedThumbnails = new List<Texture2D>();

        [MenuItem("Tools/Cards/Atlas Builder")]
        public static void Open()
        {
            var window = GetWindow<CardAtlasWindow>("卡牌工具");
            window.ConfigureWindowSize();
            window.tab = 0;
            window.Show();
        }

        private void OnEnable()
        {
            ConfigureWindowSize();
            if (recipe == null)
            {
                recipe = CreateInstance<CardAtlasRecipe>();
                recipe.hideFlags = HideFlags.HideAndDontSave;
            }
            if (recipe.imageValidation == null) recipe.imageValidation = new CardImageValidationSettings();
            TryScan();
            InitializePrefabExport();
            Undo.undoRedoPerformed += OnUndo;
        }

        private void OnDisable()
        {
            windowChrome.Release();
            Undo.undoRedoPerformed -= OnUndo;
            ClearThumbnails(); DisposeArtworkPreview();
        }

        private void OnDestroy()
        {
            if (recipe != null && !AssetDatabase.Contains(recipe)) DestroyImmediate(recipe);
        }

        private void OnUndo() { combinationDraft = null; prefabCheck = null; Changed(); ClearThumbnails(); Repaint(); }
        private void Changed() { status = null; error = null; imageChecks = null; EditorUtility.SetDirty(recipe); Repaint(); }

        private void OnFocus() { imageChecks = null; prefabCheck = null; ClearThumbnails(); }
        private void OnLostFocus() => windowChrome.Release();
        private void OnProjectChange() { prefabCheck = null; Repaint(); }

        private void ConfigureWindowSize()
        {
            minSize = new Vector2(360, 260);
            maxSize = new Vector2(10000, 10000);
        }

        private List<CardImageEntryCheck> ImageChecks()
        {
            if (imageChecks == null)
            {
                try { imageChecks = imageValidator.Analyze(recipe); dimensionError = null; }
                catch (Exception e) { imageChecks = new List<CardImageEntryCheck>(); dimensionError = e.Message; }
            }
            return imageChecks;
        }

        private void OnGUI()
        {
            windowChrome.Draw(this);
            GUILayout.BeginArea(new Rect(0, 0, position.width, Mathf.Max(0, position.height - ToolWindowChrome.FooterHeight)));
            try { DrawWindowContents(); }
            finally { GUILayout.EndArea(); }
        }

        private void DrawWindowContents()
        {
            if (recipe == null) return;
            var nextTab = GUILayout.Toolbar(tab, new[] { $"卡牌 ({recipe.cards.Count})", "图集预览", "Prefab 导出", "参数组合" });
            if (nextTab != tab) { tab = nextTab; contentScroll = Vector2.zero; }
            if (tab == 2) { DrawPrefabExport(); return; }
            if (tab == 3) { DrawArtwork(); return; }
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.KeyDown || Event.current.type == EventType.DragPerform)
                Undo.RecordObject(recipe, "编辑卡牌图集");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("源目录", GUILayout.Width(45));
                EditorGUI.BeginChangeCheck();
                recipe.sourceFolder = GUILayout.TextField(recipe.sourceFolder, GUILayout.MinWidth(20));
                if (GUILayout.Button("选择目录", EditorStyles.toolbarButton, GUILayout.Width(68))) PickFolder(true);
                recipe.recursive = GUILayout.Toggle(recipe.recursive, "含子目录", EditorStyles.toolbarButton, GUILayout.Width(70));
                if (EditorGUI.EndChangeCheck()) Changed();
                if (GUILayout.Button("扫描 / 刷新", EditorStyles.toolbarButton, GUILayout.Width(85))) TryScan();
            }
            if (WideLayout)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth))) DrawSidebar();
                    using (new EditorGUILayout.VerticalScope()) DrawContent();
                }
            }
            else
            {
                showSettings = GUILayout.Toggle(showSettings, "配置 / 替换", EditorStyles.toolbarButton);
                if (showSettings) DrawSidebar();
                else DrawContent();
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                try
                {
                    var count = recipe.cards.Count(card => card.included);
                    var size = CardAtlasBuilder.GetSize(recipe.settings, count);
                    GUILayout.Label($"{count} 张 · {size.x} × {size.y} · RGBA32 {(long)size.x * size.y * 4 / 1048576f:0.0} MB", EditorStyles.miniLabel, GUILayout.MinWidth(0));
                }
                catch (Exception e) { GUILayout.Label(e.Message, EditorStyles.miniLabel, GUILayout.MinWidth(0)); }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("导出图集", EditorStyles.toolbarButton, GUILayout.Width(90))) Export();
            }
        }

        private void DrawSidebar()
        {
            sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 150;
            DrawSettings();
            EditorGUILayout.Space(10);
            DrawSelected();
            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUILayout.EndScrollView();
        }

        private void DrawContent()
        {
            if (tab == 0) DrawCards(); else DrawAtlas();
        }

        private void DrawSettings()
        {
            GUILayout.Label("构建配置", EditorStyles.boldLabel);
            var asset = EditorGUILayout.ObjectField("加载配置", AssetDatabase.Contains(recipe) ? recipe : null, typeof(CardAtlasRecipe), false) as CardAtlasRecipe;
            if (asset != null && asset != recipe)
            {
                if (!AssetDatabase.Contains(recipe)) DestroyImmediate(recipe);
                recipe = asset;
                if (recipe.imageValidation == null) recipe.imageValidation = new CardImageValidationSettings();
                selected = 0; Changed(); ClearThumbnails();
            }
            if (GUILayout.Button("保存配置 / 另存为…")) SaveRecipe();
            EditorGUI.BeginChangeCheck();
            var s = recipe.settings;
            recipe.order = (CardAtlasOrder)EditorGUILayout.IntPopup("图集排列", (int)recipe.order,
                new[] { "按花色（卡背优先）", "手动顺序" }, new[] { 0, 1 });
            if (recipe.order == CardAtlasOrder.BySuit)
                EditorGUILayout.LabelField("卡背 → 黑桃 / 红桃 / 梅花 / 方块 A–K → 大小王 → 额外牌", EditorStyles.wordWrappedMiniLabel);
            s.cardWidth = EditorGUILayout.IntField(new GUIContent("单牌宽度", "从原始 PNG/JPG 重采样，不使用 Unity 已缩小的导入贴图。"), s.cardWidth);
            s.cardHeight = EditorGUILayout.IntField("单牌高度", s.cardHeight);
            s.columns = EditorGUILayout.IntField("每行列数", s.columns);
            s.padding = EditorGUILayout.IntField(new GUIContent("边缘扩展像素", "复制卡牌边缘到留边区域，降低邻牌串色。"), s.padding);
            s.powerOfTwo = EditorGUILayout.Toggle("图集尺寸取 2 次幂", s.powerOfTwo);
            s.maxSize = EditorGUILayout.IntField(new GUIContent("最大图集尺寸", "正方形图集边长。关闭二次幂时严格使用此整数；开启时向上取二次幂。"), s.maxSize);
            s.fit = (CardFitMode)EditorGUILayout.EnumPopup(new GUIContent("比例处理", "控制排列好的整组牌如何填充正方形图集，从左上角开始。Fit = 等比完整放入；Fill = 等比填满并裁切；Stretch = 拉伸整组。"), s.fit);
            EditorGUILayout.LabelField("比例处理作用于整组牌；图集始终为正方形。", EditorStyles.wordWrappedMiniLabel);
            s.resizeFilter = (CardResizeFilter)EditorGUILayout.EnumPopup("缩图算法", s.resizeFilter);
            EditorGUILayout.Space(6);
            GUILayout.Label("尺寸 / 比例校验", EditorStyles.boldLabel);
            var validation = recipe.imageValidation;
            validation.checkSize = EditorGUILayout.Toggle("校验原图尺寸", validation.checkSize);
            using (new EditorGUI.DisabledScope(!validation.checkSize))
            {
                validation.sourceWidth = EditorGUILayout.IntField("原图标准宽度", validation.sourceWidth);
                validation.sourceHeight = EditorGUILayout.IntField("原图标准高度", validation.sourceHeight);
            }
            validation.ratioWidth = EditorGUILayout.IntField("标准比例 · 宽", validation.ratioWidth);
            validation.ratioHeight = EditorGUILayout.IntField("标准比例 · 高", validation.ratioHeight);
            validation.ratioTolerancePercent = EditorGUILayout.Slider("比例容差 (%)", validation.ratioTolerancePercent, 0, 5);
            EditorGUILayout.LabelField("读取原始文件；原图、替换图和图集单牌分别校验。", EditorStyles.wordWrappedMiniLabel);
            DrawAtlasRatioCheck();
            EditorGUILayout.Space(6);
            GUILayout.Label("纹理导入与采样", EditorStyles.boldLabel);
            s.filter = (FilterMode)EditorGUILayout.EnumPopup("采样方式", s.filter);
            s.wrap = (TextureWrapMode)EditorGUILayout.EnumPopup("边界模式", s.wrap);
            s.mipmaps = EditorGUILayout.Toggle("生成 Mipmap", s.mipmaps);
            s.sRGB = EditorGUILayout.Toggle("sRGB 颜色纹理", s.sRGB);
            s.compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("压缩", s.compression);
            s.anisoLevel = EditorGUILayout.IntSlider("各向异性", s.anisoLevel, 0, 16);
            if (s.mipmaps) EditorGUILayout.HelpBox("远距离的高层级 mipmap 仍可能混入邻牌。像素牌推荐关闭；开启时请增加边缘扩展。", MessageType.Info);
            if (s.filter != FilterMode.Point && s.padding < 2) EditorGUILayout.HelpBox("线性采样建议至少 2 像素边缘扩展。", MessageType.Warning);
            EditorGUILayout.Space(6);
            GUILayout.Label("导出", EditorStyles.boldLabel);
            recipe.outputName = EditorGUILayout.TextField("名称", recipe.outputName);
            recipe.outputFolder = EditorGUILayout.TextField("目录", recipe.outputFolder);
            if (GUILayout.Button("选择导出目录…")) PickFolder(false);
            EditorGUILayout.HelpBox("Assets 内：PNG + JSON + 图集数据 .asset。\n项目外：PNG + JSON（包含采样配置）。", MessageType.None);
            if (EditorGUI.EndChangeCheck()) Changed();
        }

        private void DrawSelected()
        {
            if (recipe.cards.Count == 0) return;
            selected = Mathf.Clamp(selected, 0, recipe.cards.Count - 1);
            var card = recipe.cards[selected];
            GUILayout.Label("当前卡牌", EditorStyles.boldLabel);
            if (tab == 0)
            {
                var rect = GUILayoutUtility.GetRect(180, 210, GUILayout.ExpandWidth(true));
                DrawImage(rect, card.EffectivePath);
            }
            EditorGUI.BeginChangeCheck();
            card.id = EditorGUILayout.TextField("图集数据 ID", card.id);
            EditorGUILayout.LabelField("命名识别", CardDeckValidator.TryParse(card.sourcePath, out var identity) ? identity : "额外牌 / 未识别");
            card.included = EditorGUILayout.Toggle("参与图集", card.included);
            EditorGUILayout.LabelField("原文件", card.sourcePath, EditorStyles.wordWrappedMiniLabel);
            var check = (tab == 0 ? ImageChecks() : imageChecks)?.FirstOrDefault(item => item.entry == card);
            if (check != null)
                EditorGUILayout.HelpBox(check.Detail, check.HasIssue ? MessageType.Warning : MessageType.Info);
            var replacement = EditorGUILayout.ObjectField("替换贴图", card.replacement, typeof(Texture2D), false) as Texture2D;
            if (replacement != card.replacement) { card.replacement = replacement; card.replacementPath = null; ClearThumbnails(); }
            if (!string.IsNullOrEmpty(card.replacementPath)) EditorGUILayout.LabelField("替换文件", card.replacementPath, EditorStyles.wordWrappedMiniLabel);
            if (EditorGUI.EndChangeCheck()) Changed();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从文件替换…"))
                {
                    var path = EditorUtility.OpenFilePanelWithFilters("选择替换卡牌", CardAtlasBuilder.ProjectRoot, new[] { "卡牌图片", "png,jpg,jpeg" });
                    if (!string.IsNullOrEmpty(path)) { card.replacement = null; card.replacementPath = CardAtlasBuilder.ProjectPath(path); Changed(); ClearThumbnails(); }
                }
                if (GUILayout.Button("恢复原牌")) { card.replacement = null; card.replacementPath = null; Changed(); ClearThumbnails(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var orderIndex = recipe.order == CardAtlasOrder.BySuit
                    ? CardDeckValidator.OrderForAtlas(recipe.cards).IndexOf(card) : selected;
                using (new EditorGUI.DisabledScope(orderIndex == 0))
                    if (GUILayout.Button("前移")) MoveSelected(-1);
                using (new EditorGUI.DisabledScope(orderIndex == recipe.cards.Count - 1))
                    if (GUILayout.Button("后移")) MoveSelected(1);
            }
            EditorGUILayout.HelpBox("替换不修改原图。前移 / 后移会切换为手动顺序。索引从 0 开始；取消勾选或改变排列会更新索引。支持 PNG / JPG。", MessageType.None);
        }

        private void DrawCards()
        {
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
            var report = CardDeckValidator.Analyze(recipe.cards);
            var chosen = CardDeckValidator.Analyze(recipe.cards, true);
            var checks = ImageChecks();
            var byEntry = checks.ToDictionary(check => check.entry);
            if (!string.Equals(scannedFolder, recipe.sourceFolder, StringComparison.Ordinal))
                EditorGUILayout.HelpBox("路径已变更，下面仍是上次扫描结果。请点击“扫描 / 刷新”。", MessageType.Warning);
            EditorGUILayout.HelpBox("目录：" + ReportSummary(report) + "\n当前勾选：" + ReportSummary(chosen),
                report.HasAllCards && chosen.IsExactDeck ? MessageType.Info : MessageType.Warning);
            if (dimensionError != null) EditorGUILayout.HelpBox(dimensionError, MessageType.Error);
            else
            {
                EditorGUILayout.HelpBox("图片：" + ImageSummary(checks) + "\n当前勾选：" + ImageSummary(checks.Where(check => check.entry.included)),
                    checks.Any(check => check.HasIssue) ? MessageType.Warning : MessageType.Info);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全选")) { foreach (var card in recipe.cards) card.included = true; Changed(); }
                if (GUILayout.Button("全不选")) { foreach (var card in recipe.cards) card.included = false; Changed(); }
                if (GUILayout.Button(new GUIContent("每牌选一张", "每个标准位置选择当前图集顺序中第一张；取消其他重复牌和额外牌。")))
                {
                    foreach (var card in recipe.cards) card.included = false;
                    foreach (var slot in report.slots) if (slot.entries.Count > 0) slot.entries[0].included = true;
                    Changed();
                }
                onlyIssues = GUILayout.Toggle(onlyIssues, "仅看问题牌", "Button");
            }
            showNamingRules = EditorGUILayout.Foldout(showNamingRules, "命名规则 / 55 个标准位置", true);
            if (showNamingRules)
                EditorGUILayout.HelpBox("四个花色各 A–K + joker_big + joker_small + card_back。\n示例：spades_a、hearts_02、clubs_j、diamonds_k；支持花色单数、中文、牌值在前、大小写和空格 / - / _。1= A，11 / 12 / 13 = J / Q / K。\nv2、copy、alt、variant、副本或 (2) 后缀按同一张牌校验重复。其他命名单列为额外牌。替换图片或编辑图集 ID 不改变原文件的牌位身份。缺牌不阻止导出自选图集。", MessageType.None);

            var indices = new Dictionary<CardAtlasEntry, int>();
            var index = 0;
            foreach (var card in CardAtlasBuilder.OrderedEntries(recipe)) indices[card] = index++;
            foreach (var group in report.slots.GroupBy(slot => slot.group))
            {
                var slots = group.Where(slot => !onlyIssues || slot.entries.Count != 1
                    || slot.entries.Any(card => byEntry.TryGetValue(card, out var check) && check.HasIssue)).ToList();
                if (slots.Count == 0) continue;
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                var cells = new List<(CardDeckSlot slot, CardAtlasEntry card, int duplicateIndex)>();
                foreach (var slot in slots)
                {
                    if (slot.entries.Count == 0) cells.Add((slot, null, 0));
                    else for (var i = 0; i < slot.entries.Count; i++) cells.Add((slot, slot.entries[i], i));
                }
                DrawCardGrid(cells, indices, byEntry);
            }
            EditorGUILayout.LabelField($"额外牌 / 未识别 ({report.extras.Count})", EditorStyles.boldLabel);
            if (report.extras.Count == 0) EditorGUILayout.LabelField("无额外牌。", EditorStyles.miniLabel);
            else DrawCardGrid(report.extras.Select(card => ((CardDeckSlot)null, card, 0)).ToList(), indices, byEntry);
            EditorGUILayout.EndScrollView();
        }

        private static string ReportSummary(CardDeckReport report)
        {
            var state = report.IsExactDeck ? "完整标准牌组" : report.HasAllCards ? "可组成完整牌组" : "牌组不完整";
            return $"{state} · {55 - report.MissingCount}/55 牌位 · 缺失 {report.MissingCount} · 重复 {report.DuplicateCount} 牌位 · 额外 {report.extras.Count} 张";
        }

        private static string ImageSummary(IEnumerable<CardImageEntryCheck> checks)
        {
            var list = checks.ToList();
            return $"{list.Count} 张 · 尺寸异常 {list.Count(check => check.original.sizeMismatch || check.effective.sizeMismatch)}"
                + $" · 比例异常 {list.Count(check => check.original.ratioMismatch || check.effective.ratioMismatch)}"
                + $" · 需放大 {list.Count(check => check.effective.needsUpscale)}"
                + $" · 无法读取 {list.Count(check => check.original.error != null || check.effective.error != null)}";
        }

        private void DrawAtlasRatioCheck()
        {
            try
            {
                var validation = recipe.imageValidation;
                if (validation.checkSize && !CardImageValidator.MatchesRatio(validation.sourceWidth, validation.sourceHeight, validation))
                    EditorGUILayout.HelpBox("原图标准尺寸与标准比例不一致，请检查校验标准。", MessageType.Warning);
                if (!CardImageValidator.MatchesRatio(recipe.settings.cardWidth, recipe.settings.cardHeight, validation))
                {
                    EditorGUILayout.HelpBox($"单牌 {recipe.settings.cardWidth}×{recipe.settings.cardHeight} 与标准比例 {validation.ratioWidth}:{validation.ratioHeight} 不符，放入牌位时会拉伸牌面。整组牌的比例处理另行计算。", MessageType.Warning);
                }
            }
            catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); }
        }

        private void DrawCardGrid(List<(CardDeckSlot slot, CardAtlasEntry card, int duplicateIndex)> cells,
            Dictionary<CardAtlasEntry, int> indices, Dictionary<CardAtlasEntry, CardImageEntryCheck> checks)
        {
            var columns = Mathf.Max(1, Mathf.FloorToInt((ContentWidth - 20) / 122));
            var area = GUILayoutUtility.GetRect(1, Mathf.CeilToInt((float)cells.Count / columns) * 262 + 4, GUILayout.ExpandWidth(true));
            for (var i = 0; i < cells.Count; i++)
            {
                var slot = cells[i].slot;
                var card = cells[i].card;
                var cell = new Rect(area.x + (i % columns) * 122, area.y + (i / columns) * 262, 116, 256);
                CardImageEntryCheck check = null;
                if (card != null) checks.TryGetValue(card, out check);
                var color = card == null ? new Color(.35f, .16f, .16f)
                    : check != null && check.HasIssue ? new Color(.35f, .16f, .16f)
                    : slot != null && slot.entries.Count > 1 ? new Color(.35f, .27f, .13f) : new Color(.17f, .17f, .17f);
                if (card != null && recipe.cards.IndexOf(card) == selected) color = new Color(.20f, .37f, .48f);
                if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(cell, color);
                var label = slot == null ? "额外牌" : slot.label;
                GUI.Label(new Rect(cell.x + 5, cell.y + 2, 106, 18), label, EditorStyles.boldLabel);
                var imageRect = new Rect(cell.x + 5, cell.y + 23, 106, 140);
                if (card == null)
                {
                    GUI.Label(imageRect, "缺失\n" + slot.id, EditorStyles.centeredGreyMiniLabel);
                    GUI.Label(new Rect(cell.x + 5, cell.y + 168, 106, 18), "没有对应文件", EditorStyles.miniLabel);
                    continue;
                }
                DrawImage(imageRect, card.EffectivePath);
                if (GUI.Button(imageRect, GUIContent.none, GUIStyle.none)) { selected = recipe.cards.IndexOf(card); Repaint(); }
                var enabled = GUI.Toggle(new Rect(cell.x + 4, cell.y + 165, 64, 18), card.included,
                    card.included && indices.TryGetValue(card, out var atlasIndex) ? "#" + atlasIndex : "未选");
                if (enabled != card.included) { card.included = enabled; Changed(); }
                var badge = slot != null && slot.entries.Count > 1 ? $"重复 {cells[i].duplicateIndex + 1}/{slot.entries.Count}" : "";
                GUI.Label(new Rect(cell.x + 66, cell.y + 165, 50, 18), badge, EditorStyles.miniLabel);
                GUI.Label(new Rect(cell.x + 5, cell.y + 183, 106, 16), new GUIContent(card.id, "图集 ID：" + card.id), EditorStyles.miniLabel);
                var replaced = card.replacement != null || !string.IsNullOrEmpty(card.replacementPath);
                GUI.Label(new Rect(cell.x + 5, cell.y + 199, 106, 16),
                    new GUIContent((replaced ? "已替换 · " : "") + Path.GetFileName(card.sourcePath), "原文件：" + card.sourcePath + "\n采样图片：" + card.EffectivePath), EditorStyles.miniLabel);
                if (check != null)
                {
                    GUI.Label(new Rect(cell.x + 5, cell.y + 217, 106, 16), new GUIContent(check.effective.Dimensions, check.Detail), EditorStyles.miniLabel);
                    var message = !check.HasIssue ? "尺寸 / 比例正常" : check.effective.HasIssue ? check.effective.Status : "原图异常";
                    GUI.Label(new Rect(cell.x + 5, cell.y + 235, 106, 18), new GUIContent(message, check.Detail), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawAtlas()
        {
            if (!recipe.cards.Any(card => card.included))
            {
                EditorGUILayout.HelpBox("尚未勾选卡牌。勾选后自动显示实时布局。", MessageType.Info);
                GUILayout.FlexibleSpace(); return;
            }
            CardAtlasLayout layout;
            try { layout = CardAtlasBuilder.CreateLayout(recipe); }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Error); GUILayout.FlexibleSpace(); return;
            }
            EditorGUILayout.LabelField($"实时布局 · {layout.size.x}×{layout.size.y} · {layout.columns} 列 / {layout.rows} 行", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"牌组网格 {layout.gridSize.x}×{layout.gridSize.y} · {recipe.settings.fit} · 整组缩放 {layout.scale.x:0.###} / {layout.scale.y:0.###}", EditorStyles.miniLabel);
            var cropped = layout.cards.Count(card => card.clipped);
            if (cropped > 0) EditorGUILayout.HelpBox($"Fill 将裁切 {cropped} 张牌（含完全移出图集的牌）；导出数据会标记裁切范围。", MessageType.Warning);
            EditorGUILayout.LabelField("内框：单牌范围 · 外框：扩展留边 · 点击牌位选中；实际图片在导出时生成。", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 65;
                var percent = EditorGUILayout.Slider(new GUIContent("缩放 %", "Ctrl / Cmd + 滚轮以鼠标位置为中心缩放。"), zoom * 100, 1, 400);
                EditorGUIUtility.labelWidth = oldLabelWidth;
                if (EditorGUI.EndChangeCheck()) { zoom = percent / 100; fitPreview = false; }
                if (GUILayout.Button("100%", GUILayout.Width(48))) { zoom = 1; fitPreview = false; contentScroll = Vector2.zero; }
                var fit = GUILayout.Toggle(fitPreview, "适应窗口", "Button", GUILayout.Width(66));
                if (fit != fitPreview) { fitPreview = fit; contentScroll = Vector2.zero; }
            }
            var viewport = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (fitPreview && viewport.width > 20 && viewport.height > 20)
            {
                zoom = Mathf.Clamp(Mathf.Min((viewport.width - 20) / layout.size.x, (viewport.height - 20) / layout.size.y), .001f, 4);
                contentScroll = Vector2.zero;
            }
            var evt = Event.current;
            if (evt.type == EventType.ScrollWheel && (evt.control || evt.command) && viewport.Contains(evt.mousePosition))
            {
                var mouse = evt.mousePosition - viewport.position;
                var point = (mouse + contentScroll - Vector2.one * 8) / zoom;
                zoom = Mathf.Clamp(zoom * Mathf.Pow(1.1f, -evt.delta.y), .01f, 4);
                fitPreview = false;
                contentScroll = point * zoom + Vector2.one * 8 - mouse;
                contentScroll.x = Mathf.Max(0, contentScroll.x); contentScroll.y = Mathf.Max(0, contentScroll.y);
                evt.Use(); Repaint();
            }
            var canvas = new Rect(0, 0, layout.size.x * zoom + 16, layout.size.y * zoom + 16);
            contentScroll = GUI.BeginScrollView(viewport, contentScroll, canvas);
            var rect = new Rect(8, 8, layout.size.x * zoom, layout.size.y * zoom);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(.12f, .12f, .12f));
                DrawFrame(rect, new Color(.55f, .55f, .55f));
            }
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = true,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(11 * zoom / .3f), 8, 16)
            };
            GUI.BeginGroup(rect);
            foreach (var region in layout.cards)
            {
                var entry = layout.entries[region.index];
                var cardRect = layout.cardRects[region.index];
                var cellRect = layout.cellRects[region.index];
                var cell = new Rect(cardRect.x * zoom, (layout.size.y - cardRect.yMax) * zoom, cardRect.width * zoom, cardRect.height * zoom);
                var outer = new Rect(cellRect.x * zoom, (layout.size.y - cellRect.yMax) * zoom, cellRect.width * zoom, cellRect.height * zoom);
                var active = recipe.cards.IndexOf(entry) == selected;
                var color = CardDeckValidator.TryParse(entry.sourcePath, out var identity)
                    && (identity.StartsWith("hearts_", StringComparison.Ordinal) || identity.StartsWith("diamonds_", StringComparison.Ordinal))
                    ? new Color(1f, .57f, .49f) : new Color(.68f, .8f, .91f);
                if (Event.current.type == EventType.Repaint)
                {
                    DrawFrame(outer, new Color(.35f, .35f, .35f));
                    DrawFrame(cell, active ? new Color(1f, .86f, .35f) : color, active ? 2 : 1);
                }
                labelStyle.normal.textColor = active ? new Color(1f, .86f, .35f) : color;
                if (cell.width >= 22 && cell.height >= 28)
                    GUI.Label(cell, "#" + region.index + "\n" + CardMark(entry), labelStyle);
                var tooltip = $"#{region.index} · {CardMark(entry)}\nID：{entry.id}\n{entry.sourcePath}\n像素：{region.pixels}\nUV：{region.uv}"
                    + (region.clipped ? "\n已裁切 · 可见像素：" + region.visiblePixels : "");
                if (GUI.Button(cell, new GUIContent("", tooltip), GUIStyle.none)) { selected = recipe.cards.IndexOf(entry); Repaint(); }
            }
            GUI.EndGroup();
            GUI.EndScrollView();
        }

        private static void DrawFrame(Rect rect, Color color, float width = 1)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private static string CardMark(CardAtlasEntry entry)
        {
            if (!CardDeckValidator.TryParse(entry.sourcePath, out var id))
                return string.IsNullOrWhiteSpace(entry.id) ? Path.GetFileNameWithoutExtension(entry.sourcePath) : entry.id;
            switch (id)
            {
                case "card_back": return "卡背";
                case "joker_big": return "大王";
                case "joker_small": return "小王";
            }
            var parts = id.Split('_');
            var suit = parts[0] == "spades" ? "♠" : parts[0] == "hearts" ? "♥" : parts[0] == "clubs" ? "♣" : "♦";
            return suit + " " + (int.TryParse(parts[1], out var rank) ? rank.ToString() : parts[1].ToUpperInvariant());
        }

        private void DrawImage(Rect rect, string path)
        {
            var texture = Thumbnail(path);
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            else GUI.Label(rect, "图片缺失 / 非 PNG/JPG", EditorStyles.centeredGreyMiniLabel);
        }

        private Texture2D Thumbnail(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (thumbnails.TryGetValue(path, out var cached)) return cached;
            Texture2D thumbnail = null;
            try
            {
                var projectPath = CardAtlasBuilder.ProjectPath(path);
                if (projectPath.StartsWith("Assets/", StringComparison.Ordinal)) thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(projectPath);
                if (thumbnail == null)
                {
                    var source = CardAtlasBuilder.ReadSource(path);
                    try
                    {
                        var width = Mathf.Min(128, source.width);
                        var height = Mathf.Max(1, Mathf.RoundToInt((float)source.height / source.width * width));
                        height = Mathf.Min(height, 512);
                        thumbnail = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                        var input = source.GetPixels32(); var output = new Color32[width * height];
                        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) output[y * width + x] = input[(y * source.height / height) * source.width + x * source.width / width];
                        thumbnail.SetPixels32(output); thumbnail.Apply(); ownedThumbnails.Add(thumbnail);
                    }
                    finally { DestroyImmediate(source); }
                }
            }
            catch { /* A missing replacement is shown in the grid; Build reports its full error. */ }
            thumbnails[path] = thumbnail;
            return thumbnail;
        }

        private void ClearThumbnails()
        {
            foreach (var texture in ownedThumbnails) if (texture != null) DestroyImmediate(texture);
            ownedThumbnails.Clear(); thumbnails.Clear();
        }

        private void MoveSelected(int delta)
        {
            if (recipe.order != CardAtlasOrder.Manual)
            {
                var selectedCard = recipe.cards[selected];
                recipe.cards = CardDeckValidator.OrderForAtlas(recipe.cards);
                selected = recipe.cards.IndexOf(selectedCard);
                recipe.order = CardAtlasOrder.Manual;
            }
            if (selected + delta < 0 || selected + delta >= recipe.cards.Count) return;
            var card = recipe.cards[selected]; recipe.cards.RemoveAt(selected); selected += delta;
            recipe.cards.Insert(selected, card); Changed();
        }

        private void PickFolder(bool source)
        {
            var path = EditorUtility.OpenFolderPanel(source ? "卡牌目录" : "导出目录", CardAtlasBuilder.ProjectRoot, "");
            if (string.IsNullOrEmpty(path)) return;
            if (source) recipe.sourceFolder = CardAtlasBuilder.ProjectPath(path); else recipe.outputFolder = CardAtlasBuilder.ProjectPath(path);
            Changed();
        }

        private void TryScan()
        {
            try { CardAtlasBuilder.Scan(recipe); scannedFolder = recipe.sourceFolder; selected = 0; error = null; imageValidator.Clear(); Changed(); ClearThumbnails(); }
            catch (Exception e) { error = e.Message; }
        }

        private void Export()
        {
            try
            {
                CardImageValidator.ValidateSettings(recipe.imageValidation);
                var path = Path.Combine(CardAtlasBuilder.Absolute(recipe.outputFolder), recipe.outputName + ".png");
                if (File.Exists(path) && !EditorUtility.DisplayDialog("覆盖已有图集？", "将更新同名 PNG、JSON 和 Unity 图集数据。\n" + path, "覆盖", "取消")) return;
                string exported;
                using (var build = CardAtlasBuilder.Build(recipe, true)) exported = CardAtlasBuilder.Export(recipe, build);
                status = "已导出：" + exported; error = null;
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(exported);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            }
            catch (Exception e) { error = e.Message; }
        }

        private void SaveRecipe()
        {
            var path = EditorUtility.SaveFilePanelInProject("保存卡牌图集配置", recipe.outputName + "Recipe", "asset", "保存替换关系、卡牌顺序和导出参数。", "Assets/CardArt/Editor");
            if (string.IsNullOrEmpty(path)) return;
            var existing = AssetDatabase.LoadAssetAtPath<CardAtlasRecipe>(path);
            if (existing != null)
            {
                if (existing != recipe) EditorUtility.CopySerialized(recipe, existing);
                existing.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) { error = "该路径已有其他类型的资产。"; return; }
                existing = Instantiate(recipe); existing.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(existing, path);
            }
            if (!AssetDatabase.Contains(recipe)) DestroyImmediate(recipe);
            recipe = existing; AssetDatabase.SaveAssets(); status = "配置已保存：" + path;
        }
    }
}
