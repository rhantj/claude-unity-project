using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using BlackHoleSim;
using BlackHoleSim.UI;

namespace BlackHoleSim.Editor
{
    /// <summary>
    /// Editor-only builder for the runtime parameter panel UGUI hierarchy.
    /// MCP cannot configure RectTransform via generic primitive tools, so the whole
    /// panel is constructed here in C# (Editor API has full RectTransform control)
    /// and invoked once via editor_invoke_method, mirroring BlackHoleSimEditorTools.
    /// </summary>
    public static class BlackHoleSimUIBuilder
    {
        const string CanvasName = "ParamCanvas";
        const float PanelWidth = 360f;

        static TMP_FontAsset _font;
        static int _wiredCount;
        static int _nullCount;

        public static void BuildParamPanel()
        {
            _wiredCount = 0;
            _nullCount = 0;
            _font = LoadDefaultFont();

            // Idempotency: remove any prior canvas so re-running is clean.
            var existing = GameObject.Find(CanvasName);
            if (existing != null) Object.DestroyImmediate(existing);

            var canvasGO = CreateCanvas();
            EnsureEventSystem();

            var panelRoot = CreatePanelRoot(canvasGO.transform);
            var header = CreateHeader(panelRoot.transform);
            var scrollContent = CreateScrollView(panelRoot.transform);

            CollapsibleSection dangerZoneSection;
            var rows = new Dictionary<string, Component>();

            BuildDiskSection(scrollContent.transform, rows);
            BuildRelativitySection(scrollContent.transform, rows);
            BuildRenderQualitySection(scrollContent.transform, rows);
            BuildParticlesSection(scrollContent.transform, rows);
            dangerZoneSection = BuildDangerZoneSection(scrollContent.transform, rows);

            var view = panelRoot.AddComponent<ParamPanelView>();
            WireParamPanelView(view, panelRoot, header.collapseButton, scrollContent, dangerZoneSection, rows);

            EditorUtility.SetDirty(panelRoot);
            EditorSceneManager.MarkSceneDirty(panelRoot.scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[Editor] ParamPanel built. wired={_wiredCount} nullRefs={_nullCount}");
        }

        // ---------- Top-level structure ----------

        static GameObject CreateCanvas()
        {
            var go = new GameObject(CanvasName, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            // ScreenSpaceCamera (not Overlay): MCP's screenshot_game tool captures via
            // Camera.Render() into an offscreen RenderTexture, which never includes
            // ScreenSpaceOverlay canvases (Unity only composites those into the real
            // Game View window during the normal player loop). Camera mode is captured
            // correctly by camera.Render() while still behaving like a screen overlay.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        static GameObject CreatePanelRoot(Transform parent)
        {
            var go = CreateRect("ParamPanel", parent, Vector2.one, Vector2.one, Vector2.one, Vector2.zero, new Vector2(PanelWidth, 0f));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PanelWidth, 0f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            // Must control height so the ScrollView's LayoutElement.flexibleHeight=1
            // actually expands it to fill the panel below the fixed-height header.
            vlg.childControlHeight = true;
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            return go;
        }

        struct HeaderRefs
        {
            public Button collapseButton;
        }

        static HeaderRefs CreateHeader(Transform parent)
        {
            var go = CreateRect("Header", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 36f));
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
            le.flexibleHeight = 0f;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.14f, 1f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 6, 6);
            hlg.spacing = 8f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            var title = CreateText("Title", go.transform, "Parameters", 18, FontStyles.Bold);
            var titleLE = title.gameObject.AddComponent<LayoutElement>();
            titleLE.flexibleWidth = 1f;

            var btnGO = CreateRect("CollapseButton", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 28f;
            btnLE.flexibleWidth = 0f;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.25f, 0.32f, 1f);
            var button = btnGO.AddComponent<Button>();
            var btnText = CreateText("Label", btnGO.transform, "-", 16, FontStyles.Bold);
            StretchFull(btnText.rectTransform);
            btnText.alignment = TextAlignmentOptions.Center;

            return new HeaderRefs { collapseButton = button };
        }

        static GameObject CreateScrollView(Transform parent)
        {
            var scrollGO = CreateRect("ScrollView", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var scrollLE = scrollGO.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var viewportGO = CreateRect("Viewport", scrollGO.transform, Vector2.zero, Vector2.one, Vector2.up, Vector2.zero, Vector2.zero);
            StretchFull(viewportGO.GetComponent<RectTransform>());
            // RectMask2D (scissor-rect clipping) instead of Mask (stencil-buffer clipping):
            // the project's custom URP RenderGraph fullscreen pass (BlackHoleLensFeature)
            // does not preserve the stencil state UGUI's Mask relies on, which silently
            // clipped away all masked content. RectMask2D has no stencil dependency.
            viewportGO.AddComponent<RectMask2D>();

            var contentGO = CreateRect("Content", viewportGO.transform, Vector2.up, Vector2.one, Vector2.up, Vector2.zero, Vector2.zero);
            var contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(6, 6, 6, 6);

            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();
            scrollRect.content = contentRT;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            return contentGO;
        }

        // ---------- Sections ----------

        static GameObject CreateSectionContainer(string name, Transform parent, out CollapsibleSection section, out Button headerButton, Color? tint = null)
        {
            var go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.spacing = 2f;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headerGO = CreateRect("Header", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 28f));
            var headerLE = headerGO.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 28f;
            var headerImg = headerGO.AddComponent<Image>();
            headerImg.color = tint ?? new Color(0.16f, 0.16f, 0.22f, 1f);
            headerButton = headerGO.AddComponent<Button>();

            var headerLabel = CreateText("Label", headerGO.transform, name, 15, FontStyles.Bold);
            StretchFull(headerLabel.rectTransform, 8f, 8f);
            headerLabel.alignment = TextAlignmentOptions.MidlineLeft;

            var contentGO = CreateRect("Content", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.spacing = 4f;
            contentVlg.padding = new RectOffset(8, 8, 4, 8);
            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (tint.HasValue)
            {
                var contentImg = contentGO.AddComponent<Image>();
                contentImg.color = tint.Value;
            }

            section = go.AddComponent<CollapsibleSection>();
            var so = new SerializedObject(section);
            so.FindProperty("headerButton").objectReferenceValue = headerButton;
            so.FindProperty("content").objectReferenceValue = contentGO;
            so.ApplyModifiedPropertiesWithoutUndo();
            TrackWire(headerButton, "headerButton");
            TrackWire(contentGO, "content");

            return contentGO;
        }

        static void BuildDiskSection(Transform parent, Dictionary<string, Component> rows)
        {
            CollapsibleSection section;
            Button headerBtn;
            var content = CreateSectionContainer("Disk", parent, out section, out headerBtn);

            rows["diskInnerRadiusRow"] = CreateFloatRow("DiskInnerRadius", content.transform);
            rows["diskOuterRadiusRow"] = CreateFloatRow("DiskOuterRadius", content.transform);
            rows["diskThicknessRow"] = CreateFloatRow("DiskThickness", content.transform);
            rows["diskDensityRow"] = CreateFloatRow("DiskDensity", content.transform);
            rows["diskTempInnerRow"] = CreateFloatRow("DiskTempInner", content.transform);
            rows["diskTempOuterRow"] = CreateFloatRow("DiskTempOuter", content.transform);
            rows["diskColorTintRow"] = CreateColorRow("DiskColorTint", content.transform);
        }

        static void BuildRelativitySection(Transform parent, Dictionary<string, Component> rows)
        {
            CollapsibleSection section;
            Button headerBtn;
            var content = CreateSectionContainer("Relativity", parent, out section, out headerBtn);

            rows["beamingRow"] = CreateFloatRow("Beaming", content.transform);
            rows["redshiftRow"] = CreateFloatRow("Redshift", content.transform);
            rows["photonRingRow"] = CreateFloatRow("PhotonRing", content.transform);
        }

        static void BuildRenderQualitySection(Transform parent, Dictionary<string, Component> rows)
        {
            CollapsibleSection section;
            Button headerBtn;
            var content = CreateSectionContainer("Render Quality", parent, out section, out headerBtn);

            rows["stepCountRow"] = CreateIntRow("StepCount", content.transform);
            rows["stepSizeRow"] = CreateFloatRow("StepSize", content.transform);
            rows["starDensityRow"] = CreateFloatRow("StarDensity", content.transform);
        }

        static void BuildParticlesSection(Transform parent, Dictionary<string, Component> rows)
        {
            CollapsibleSection section;
            Button headerBtn;
            var content = CreateSectionContainer("Particles", parent, out section, out headerBtn);

            rows["particlesEnabledRow"] = CreateToggleRow("ParticlesEnabled", content.transform);
            rows["particleCountRow"] = CreateIntRow("ParticleCount", content.transform);
            rows["particleInnerRadiusRow"] = CreateFloatRow("ParticleInnerRadius", content.transform);
            rows["particleOuterRadiusRow"] = CreateFloatRow("ParticleOuterRadius", content.transform);
            rows["particleDiskThicknessRow"] = CreateFloatRow("ParticleDiskThickness", content.transform);
            rows["particleSpeedJitterRow"] = CreateFloatRow("ParticleSpeedJitter", content.transform);
            rows["particleMaxRadiusRow"] = CreateFloatRow("ParticleMaxRadius", content.transform);
            rows["particleInfallSpeedFactorRow"] = CreateFloatRow("ParticleInfallSpeedFactor", content.transform);
            rows["particleSizeRow"] = CreateFloatRow("ParticleSize", content.transform);
        }

        static CollapsibleSection BuildDangerZoneSection(Transform parent, Dictionary<string, Component> rows)
        {
            CollapsibleSection section;
            Button headerBtn;
            var dangerTint = new Color(0.6f, 0.15f, 0.15f, 0.6f);
            var content = CreateSectionContainer("Danger Zone", parent, out section, out headerBtn, dangerTint);

            rows["gravitationalConstantRow"] = CreateFloatRow("GravitationalConstant", content.transform);
            rows["massRow"] = CreateFloatRow("Mass", content.transform);
            rows["softeningRow"] = CreateFloatRow("Softening", content.transform);
            rows["eventHorizonRadiusRow"] = CreateFloatRow("EventHorizonRadius", content.transform);

            return section;
        }

        // ---------- Row builders ----------

        static FloatSliderRow CreateFloatRow(string name, Transform parent)
        {
            var go = CreateRowBase(name, parent);
            var label = CreateRowLabel(go.transform);
            var valueText = CreateRowValueText(go.transform);
            var slider = CreateRowSlider(go.transform);

            var row = go.AddComponent<FloatSliderRow>();
            var so = new SerializedObject(row);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("valueText").objectReferenceValue = valueText;
            so.FindProperty("slider").objectReferenceValue = slider;
            so.ApplyModifiedPropertiesWithoutUndo();
            TrackWire(label, "label"); TrackWire(valueText, "valueText"); TrackWire(slider, "slider");
            return row;
        }

        static IntSliderRow CreateIntRow(string name, Transform parent)
        {
            var go = CreateRowBase(name, parent);
            var label = CreateRowLabel(go.transform);
            var valueText = CreateRowValueText(go.transform);
            var slider = CreateRowSlider(go.transform);

            var row = go.AddComponent<IntSliderRow>();
            var so = new SerializedObject(row);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("valueText").objectReferenceValue = valueText;
            so.FindProperty("slider").objectReferenceValue = slider;
            so.ApplyModifiedPropertiesWithoutUndo();
            TrackWire(label, "label"); TrackWire(valueText, "valueText"); TrackWire(slider, "slider");
            return row;
        }

        static ToggleRow CreateToggleRow(string name, Transform parent)
        {
            var go = CreateRowBase(name, parent, 28f);
            var label = CreateRowLabel(go.transform);
            var toggle = CreateRowToggle(go.transform);

            var row = go.AddComponent<ToggleRow>();
            var so = new SerializedObject(row);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("toggle").objectReferenceValue = toggle;
            so.ApplyModifiedPropertiesWithoutUndo();
            TrackWire(label, "label"); TrackWire(toggle, "toggle");
            return row;
        }

        static ColorSwatchRow CreateColorRow(string name, Transform parent)
        {
            var go = CreateRowBase(name, parent, 84f);
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;

            var label = CreateRowLabel(go.transform);

            var swatchAndSlidersRow = CreateRect("SwatchRow", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 60f));
            var swRowLE = swatchAndSlidersRow.AddComponent<LayoutElement>();
            swRowLE.preferredHeight = 60f;
            var hlg = swatchAndSlidersRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var swatchGO = CreateRect("Swatch", swatchAndSlidersRow.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 0f));
            var swatchLE = swatchGO.AddComponent<LayoutElement>();
            swatchLE.preferredWidth = 40f;
            var swatchImg = swatchGO.AddComponent<Image>();
            swatchImg.color = Color.white;

            var slidersGO = CreateRect("Sliders", swatchAndSlidersRow.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var slidersLE = slidersGO.AddComponent<LayoutElement>();
            slidersLE.flexibleWidth = 1f;
            var slidersVlg = slidersGO.AddComponent<VerticalLayoutGroup>();
            slidersVlg.spacing = 2f;
            slidersVlg.childForceExpandWidth = true;
            slidersVlg.childForceExpandHeight = false;
            slidersVlg.childControlHeight = false;

            var rSlider = CreateRowSlider(slidersGO.transform, "SliderR");
            var gSlider = CreateRowSlider(slidersGO.transform, "SliderG");
            var bSlider = CreateRowSlider(slidersGO.transform, "SliderB");

            var row = go.AddComponent<ColorSwatchRow>();
            var so = new SerializedObject(row);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("rSlider").objectReferenceValue = rSlider;
            so.FindProperty("gSlider").objectReferenceValue = gSlider;
            so.FindProperty("bSlider").objectReferenceValue = bSlider;
            so.FindProperty("swatch").objectReferenceValue = swatchImg;
            so.ApplyModifiedPropertiesWithoutUndo();
            TrackWire(label, "label"); TrackWire(rSlider, "rSlider"); TrackWire(gSlider, "gSlider");
            TrackWire(bSlider, "bSlider"); TrackWire(swatchImg, "swatch");
            return row;
        }

        // ---------- Row primitive helpers ----------

        static GameObject CreateRowBase(string name, Transform parent, float height = 44f)
        {
            var go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height));
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = false;
            vlg.spacing = 2f;
            return go;
        }

        static TMP_Text CreateRowLabel(Transform parent)
        {
            var t = CreateText("Label", parent, string.Empty, 13, FontStyles.Normal);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 16f;
            return t;
        }

        static TMP_Text CreateRowValueText(Transform parent)
        {
            // value text shares row width with the slider via a thin horizontal strip is overkill;
            // keep it simple: value label sits above the slider, right-aligned via its own row.
            var rowGO = CreateRect("ValueRow", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 14f));
            var le = rowGO.AddComponent<LayoutElement>();
            le.preferredHeight = 14f;
            var t = CreateText("Value", rowGO.transform, string.Empty, 11, FontStyles.Normal);
            StretchFull(t.rectTransform);
            t.alignment = TextAlignmentOptions.MidlineRight;
            return t;
        }

        static Slider CreateRowSlider(Transform parent, string name = "Slider")
        {
            var go = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 18f));
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 18f;

            var slider = go.AddComponent<Slider>();

            var bgGO = CreateRect("Background", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(bgGO.GetComponent<RectTransform>());
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var fillAreaGO = CreateRect("Fill Area", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(fillAreaGO.GetComponent<RectTransform>());
            var fillGO = CreateRect("Fill", fillAreaGO.transform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(fillGO.GetComponent<RectTransform>());
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.55f, 0.9f, 1f);

            var handleAreaGO = CreateRect("Handle Slide Area", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(handleAreaGO.GetComponent<RectTransform>());
            var handleGO = CreateRect("Handle", handleAreaGO.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.targetGraphic = handleImg;
            slider.fillRect = fillGO.GetComponent<RectTransform>();
            slider.handleRect = handleGO.GetComponent<RectTransform>();
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        static Toggle CreateRowToggle(Transform parent)
        {
            var go = CreateRect("Toggle", parent, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 20f;
            le.preferredWidth = 20f;

            var toggle = go.AddComponent<Toggle>();
            var bgGO = CreateRect("Background", go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(bgGO.GetComponent<RectTransform>());
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            var checkGO = CreateRect("Checkmark", bgGO.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(checkGO.GetComponent<RectTransform>());
            var checkImg = checkGO.AddComponent<Image>();
            checkImg.color = new Color(0.35f, 0.85f, 0.45f, 1f);

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;

            return toggle;
        }

        // ---------- Generic primitives ----------

        static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        static void StretchFull(RectTransform rt, float left = 0f, float right = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(-right, 0f);
        }

        static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        static TMP_FontAsset LoadDefaultFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null)
                Debug.LogWarning("[Editor] Default TMP font not found; rows will use TMP fallback font.");
            return font;
        }

        // ---------- ParamPanelView wiring ----------

        static void WireParamPanelView(
            ParamPanelView view,
            GameObject panelRoot,
            Button collapseButton,
            GameObject panelContent,
            CollapsibleSection dangerZoneSection,
            Dictionary<string, Component> rows)
        {
            var blackHole = Object.FindAnyObjectByType<BlackHole>();
            var lens = Object.FindAnyObjectByType<BlackHoleLensController>();
            var particles = Object.FindAnyObjectByType<ParticleField>(FindObjectsInactive.Include);

            var so = new SerializedObject(view);

            SetRef(so, "blackHole", blackHole);
            SetRef(so, "lens", lens);
            SetRef(so, "particles", particles);
            SetRef(so, "panelContent", panelContent);
            SetRef(so, "headerCollapseButton", collapseButton);
            SetRef(so, "dangerZoneSection", dangerZoneSection);

            foreach (var kv in rows)
                SetRef(so, kv.Key, kv.Value);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        static void SetRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] ParamPanelView field not found: {fieldName}");
                _nullCount++;
                return;
            }
            prop.objectReferenceValue = value;
            TrackWire(value, fieldName);
        }

        static void TrackWire(Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"[Editor] Null reference for field: {fieldName}");
                _nullCount++;
            }
            else
            {
                _wiredCount++;
            }
        }
    }
}
