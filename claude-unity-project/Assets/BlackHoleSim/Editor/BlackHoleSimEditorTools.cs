using System.IO;
using UnityEditor;
using UnityEngine;

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
    }
}
