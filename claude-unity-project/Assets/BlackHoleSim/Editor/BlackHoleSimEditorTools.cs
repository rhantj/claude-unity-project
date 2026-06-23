using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BlackHoleSim.Editor
{
    /// <summary>MCP hooks for one-off scene/prefab authoring that needs the Editor API.</summary>
    public static class BlackHoleSimEditorTools
    {
        const string ThrowablePath = "Assets/BlackHoleSim/Prefabs/Throwable.prefab";

        public static void SaveThrowablePrefab()
        {
            var go = GameObject.Find("Throwable");
            if (go == null) { Debug.LogWarning("[Editor] 'Throwable' not found in scene"); return; }

            Directory.CreateDirectory(Path.GetDirectoryName(ThrowablePath));
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, ThrowablePath, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            Debug.Log(prefab != null
                ? $"[Editor] Saved prefab: {ThrowablePath}"
                : "[Editor] Failed to save prefab");
        }

        public static void WireSimController()
        {
            var ctrl = Object.FindAnyObjectByType<SimController>();
            if (ctrl == null) { Debug.LogWarning("[Editor] SimController not found"); return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowablePath);
            if (prefab == null) { Debug.LogWarning("[Editor] Throwable prefab not found"); return; }

            var so = new SerializedObject(ctrl);
            so.FindProperty("throwablePrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
            Debug.Log("[Editor] Wired throwablePrefab on SimController");
        }

        const string LensShaderPath = "Assets/BlackHoleSim/Shaders/BlackHoleLens.shader";
        const string LensMaterialPath = "Assets/BlackHoleSim/Materials/BlackHoleLens.mat";
        const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";

        public static void RegisterLensFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
            if (rendererData == null) { Debug.LogWarning("[Editor] Renderer data not found: " + RendererDataPath); return; }

            foreach (var existing in rendererData.rendererFeatures)
            {
                if (existing is BlackHoleSim.BlackHoleLensFeature)
                {
                    Debug.Log("[Editor] BlackHoleLensFeature already registered");
                    return;
                }
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(LensMaterialPath);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LensShaderPath);
                if (shader == null) { Debug.LogWarning("[Editor] Lens shader not found: " + LensShaderPath); return; }

                material = new Material(shader);
                Directory.CreateDirectory(Path.GetDirectoryName(LensMaterialPath));
                AssetDatabase.CreateAsset(material, LensMaterialPath);
            }

            var feature = ScriptableObject.CreateInstance<BlackHoleSim.BlackHoleLensFeature>();
            feature.name = "BlackHoleLensFeature";
            feature.lensMaterial = material;
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            rendererData.rendererFeatures.Add(feature);

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] Registered BlackHoleLensFeature on " + RendererDataPath);
        }

        // 렌즈 패스가 BeforeRenderingOpaques에 배경을 그리므로, 스카이박스가 그 위를 덮지 않도록 카메라 클리어를 SolidColor로 바꾼다(절차적 별 배경이 스카이박스를 대체).
        public static void ConfigureCameraForLens()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Editor] Camera.main not found"); return; }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            EditorUtility.SetDirty(cam);
            if (cam.gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            Debug.Log("[Editor] Camera clearFlags set to SolidColor (black) for lens background");
        }

        // MCP editor_invoke_method가 인자 전달을 지원하지 않아, 렌즈용 Bloom 값을 고정 호출하는 무인자 래퍼.
        public static void TuneBloomForLens() => SetBloomIntensity(1.4f, 0.6f);

        public static void SetBloomIntensity(float intensity, float threshold)
        {
            const string profilePath = "Assets/Settings/SampleSceneProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { Debug.LogWarning("[Editor] Volume profile not found: " + profilePath); return; }

            if (profile.TryGet(out Bloom bloom))
            {
                bloom.intensity.value = intensity;
                bloom.threshold.value = threshold;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Editor] Bloom intensity={intensity} threshold={threshold}");
            }
            else
            {
                Debug.LogWarning("[Editor] Bloom component not found on volume profile");
            }
        }
    }
}
