using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Samples existing render state only; never refreshes the Canvas, mesh, HUD or battle.</summary>
    public static class WrathBossValidation
    {
        [MenuItem("工具/卡牌战斗/暴怒验证/导出当前显示诊断")]
        public static void ExportDisplayDiagnostics()
        {
            if (!Application.isPlaying) { Debug.LogWarning("请先进入暴怒场景 Play，再导出当前显示诊断。"); return; }
            var transforms = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .Where(value => value.gameObject.scene.IsValid() && value.gameObject.scene.isLoaded).ToArray();
            var selected = new HashSet<Transform>();
            foreach (var root in transforms.Where(value => value.name == "WorldUIRoot" || value.name == "Wrath Boss" ||
                value.name == "Wrath Opening Warning" || value.GetComponent<SingleBattleHud>() != null || value.GetComponent<Canvas>() != null))
            {
                selected.Add(root);
                if (root.name.StartsWith("Wrath") || root.name == "WorldUIRoot")
                    foreach (var child in root.GetComponentsInChildren<Transform>(true)) selected.Add(child);
                for (var parent = root.parent; parent != null; parent = parent.parent) selected.Add(parent);
            }
            var report = new Report {
                capturedUtc = DateTime.UtcNow.ToString("O"), screenSize = new Vector2(Screen.width, Screen.height),
                safeArea = Screen.safeArea, frame = Time.frameCount,
                cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true).Select(CameraState).ToArray(),
                hosts = UnityEngine.Object.FindObjectsOfType<SingleBattleSceneAdapter>(true).Select(host => new HostState {
                    path = PathOf(host.transform), enabled = host.enabled, active = host.gameObject.activeInHierarchy,
                    encounter = host.ContentEncounterId, isPresenting = host.IsPresenting,
                    battleId = host.Display?.BattleId, phase = host.Display?.Phase,
                    displayHasBody = host.Display != null && host.Display.Labels.ContainsKey("wrath.body") }).ToArray(),
                nodes = selected.OrderBy(PathOf).Select(NodeState).ToArray()
            };
            string path = Path.GetFullPath("Logs/WrathUnityValidation/display-diagnostics.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("暴怒当前显示诊断已保存（未刷新或修改战斗/显示状态）：" + path);
        }

        private static string PathOf(Transform value) => value == null ? null :
            (value.parent == null ? value.gameObject.scene.name + "/" : PathOf(value.parent) + "/") + value.name;

        private static CameraInfo CameraState(Camera camera) => new CameraInfo {
            path = PathOf(camera.transform), enabled = camera.enabled, active = camera.gameObject.activeInHierarchy,
            position = camera.transform.position, rotation = camera.transform.eulerAngles,
            orthographic = camera.orthographic, fieldOfView = camera.fieldOfView, orthographicSize = camera.orthographicSize,
            near = camera.nearClipPlane, far = camera.farClipPlane, cullingMask = camera.cullingMask,
            pixelRect = camera.pixelRect, targetTexture = camera.targetTexture != null ? camera.targetTexture.name : null };

        private static NodeInfo NodeState(Transform value)
        {
            var node = new NodeInfo { path = PathOf(value), activeSelf = value.gameObject.activeSelf,
                activeInHierarchy = value.gameObject.activeInHierarchy, layer = value.gameObject.layer,
                localPosition = value.localPosition, worldPosition = value.position,
                localScale = value.localScale, lossyScale = value.lossyScale, worldRotation = value.eulerAngles };
            try
            {
                var canvas = value.GetComponent<Canvas>();
                var enclosing = value.GetComponentInParent<Canvas>(true);
                var rootCanvas = enclosing != null ? enclosing.rootCanvas : null;
                node.canvasPath = enclosing != null ? PathOf(enclosing.transform) : null;
                node.rootCanvasPath = rootCanvas != null ? PathOf(rootCanvas.transform) : null;
                if (canvas != null) node.canvas = new CanvasInfo {
                    enabled = canvas.enabled, renderMode = canvas.renderMode.ToString(), isRoot = canvas.isRootCanvas,
                    overrideSorting = canvas.overrideSorting, sortingLayer = canvas.sortingLayerName, sortingOrder = canvas.sortingOrder,
                    scaleFactor = canvas.scaleFactor, pixelRect = canvas.pixelRect,
                    worldCamera = canvas.worldCamera != null ? PathOf(canvas.worldCamera.transform) : null,
                    additionalShaderChannels = canvas.additionalShaderChannels.ToString(),
                    graphicRaycaster = value.GetComponent<GraphicRaycaster>() != null };
                var camera = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
                    enclosing != null && enclosing.worldCamera != null ? enclosing.worldCamera : Camera.main;
                node.projectionCamera = camera != null ? PathOf(camera.transform) : "screen-overlay";
                var rect = value as RectTransform;
                if (rect != null)
                {
                    node.rect = rect.rect; node.anchorMin = rect.anchorMin; node.anchorMax = rect.anchorMax;
                    node.pivot = rect.pivot; node.offsetMin = rect.offsetMin; node.offsetMax = rect.offsetMax;
                    node.worldCorners = new Vector3[4]; rect.GetWorldCorners(node.worldCorners);
                    node.screenCorners = node.worldCorners.Select(corner => camera != null ? camera.WorldToScreenPoint(corner) :
                        new Vector3(corner.x, corner.y, 0)).ToArray();
                }
                var renderer = value.GetComponent<CanvasRenderer>();
                if (renderer != null)
                {
                    node.canvasRenderer = new CanvasRendererInfo {
                        cull = renderer.cull, cullTransparentMesh = renderer.cullTransparentMesh,
                        color = renderer.GetColor(), alpha = renderer.GetAlpha(), inheritedAlpha = renderer.GetInheritedAlpha(),
                        absoluteDepth = renderer.absoluteDepth, relativeDepth = renderer.relativeDepth, materialCount = renderer.materialCount,
                        materials = Enumerable.Range(0, renderer.materialCount).Select(index => MaterialState(renderer.GetMaterial(index))).ToArray() };
                    try
                    {
                        var mesh = renderer.GetMesh();
                        if (mesh != null)
                        {
                            node.canvasRenderer.meshName = mesh.name; node.canvasRenderer.meshVertexCount = mesh.vertexCount;
                            node.canvasRenderer.meshIndexCount = Enumerable.Range(0, mesh.subMeshCount).Sum(index => (long)mesh.GetIndexCount(index));
                            node.canvasRenderer.meshBounds = mesh.bounds;
                            if (mesh.isReadable && mesh.colors32.Length > 0)
                            {
                                node.canvasRenderer.vertexAlphaMin = mesh.colors32.Min(color => (int)color.a);
                                node.canvasRenderer.vertexAlphaMax = mesh.colors32.Max(color => (int)color.a);
                            }
                        }
                    }
                    catch (Exception error) { node.canvasRenderer.meshError = error.ToString(); }
                }
                var graphic = value.GetComponent<Graphic>();
                if (graphic != null) node.graphic = new GraphicInfo {
                    type = graphic.GetType().FullName, enabled = graphic.enabled, color = graphic.color,
                    raycastTarget = graphic.raycastTarget, material = MaterialState(graphic.material),
                    texture = graphic.mainTexture != null ? graphic.mainTexture.name : null };
                var text = value.GetComponent<TMP_Text>();
                if (text != null) node.text = new TextInfo {
                    text = text.text, enabled = text.enabled, alpha = text.alpha, color = text.color,
                    font = text.font != null ? text.font.name : null, fontSize = text.fontSize,
                    fontMaterial = MaterialState(text.fontSharedMaterial),
                    characterCount = text.textInfo?.characterCount ?? 0,
                    visibleCharacters = text.textInfo?.characterInfo == null ? 0 : text.textInfo.characterInfo.Take(text.textInfo.characterCount).Count(character => character.isVisible),
                    meshCount = text.textInfo?.meshInfo?.Length ?? 0, overflow = text.isTextOverflowing };
                node.canvasGroups = value.GetComponentsInParent<CanvasGroup>(true).Select(group => new GroupInfo {
                    path = PathOf(group.transform), enabled = group.enabled, alpha = group.alpha,
                    blocksRaycasts = group.blocksRaycasts, interactable = group.interactable, ignoreParentGroups = group.ignoreParentGroups }).ToArray();
                node.renderers = value.GetComponents<Renderer>().Select(item => new RendererInfo {
                    type = item.GetType().FullName, enabled = item.enabled, isVisible = item.isVisible,
                    sortingLayer = item.sortingLayerName, sortingOrder = item.sortingOrder,
                    materials = item.sharedMaterials.Select(MaterialState).ToArray() }).ToArray();
            }
            catch (Exception error) { node.error = error.ToString(); }
            return node;
        }

        private static MaterialInfo MaterialState(Material material) => material == null ? null : new MaterialInfo {
            name = material.name, shader = material.shader != null ? material.shader.name : null, renderQueue = material.renderQueue,
            faceColor = material.HasProperty("_FaceColor") ? material.GetColor("_FaceColor") : Color.white,
            tint = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white,
            cull = material.HasProperty("_Cull") ? material.GetFloat("_Cull") : -1,
            zTest = material.HasProperty("_ZTestMode") ? material.GetFloat("_ZTestMode") : -1 };

        [Serializable] private sealed class Report { public string capturedUtc; public int frame; public Vector2 screenSize; public Rect safeArea; public CameraInfo[] cameras; public HostState[] hosts; public NodeInfo[] nodes; }
        [Serializable] private sealed class HostState { public string path, encounter, battleId, phase; public bool enabled, active, isPresenting, displayHasBody; }
        [Serializable] private sealed class CameraInfo { public string path, targetTexture; public bool enabled, active, orthographic; public Vector3 position, rotation; public float fieldOfView, orthographicSize, near, far; public int cullingMask; public Rect pixelRect; }
        [Serializable] private sealed class NodeInfo { public string path, canvasPath, rootCanvasPath, projectionCamera, error; public bool activeSelf, activeInHierarchy; public int layer; public Vector3 localPosition, worldPosition, localScale, lossyScale, worldRotation; public Rect rect; public Vector2 anchorMin, anchorMax, pivot, offsetMin, offsetMax; public Vector3[] worldCorners, screenCorners; public CanvasInfo canvas; public CanvasRendererInfo canvasRenderer; public GraphicInfo graphic; public TextInfo text; public GroupInfo[] canvasGroups; public RendererInfo[] renderers; }
        [Serializable] private sealed class CanvasInfo { public string renderMode, sortingLayer, worldCamera, additionalShaderChannels; public bool enabled, isRoot, overrideSorting, graphicRaycaster; public int sortingOrder; public float scaleFactor; public Rect pixelRect; }
        [Serializable] private sealed class CanvasRendererInfo { public bool cull, cullTransparentMesh; public Color color; public float alpha, inheritedAlpha; public int absoluteDepth, relativeDepth, materialCount, meshVertexCount, vertexAlphaMin = -1, vertexAlphaMax = -1; public long meshIndexCount; public string meshName, meshError; public Bounds meshBounds; public MaterialInfo[] materials; }
        [Serializable] private sealed class GraphicInfo { public string type, texture; public bool enabled, raycastTarget; public Color color; public MaterialInfo material; }
        [Serializable] private sealed class TextInfo { public string text, font; public bool enabled, overflow; public float alpha, fontSize; public Color color; public int characterCount, visibleCharacters, meshCount; public MaterialInfo fontMaterial; }
        [Serializable] private sealed class GroupInfo { public string path; public bool enabled, blocksRaycasts, interactable, ignoreParentGroups; public float alpha; }
        [Serializable] private sealed class RendererInfo { public string type, sortingLayer; public bool enabled, isVisible; public int sortingOrder; public MaterialInfo[] materials; }
        [Serializable] private sealed class MaterialInfo { public string name, shader; public int renderQueue; public Color faceColor, tint; public float cull, zTest; }
    }
}
