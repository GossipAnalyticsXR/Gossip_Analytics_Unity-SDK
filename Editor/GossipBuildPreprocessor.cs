#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using GossipSDK.Core.Configuration;

namespace GossipSDK.Editor
{
    public class GossipBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var guids = AssetDatabase.FindAssets("t:GossipSettings");
            if (guids.Length == 0) return;

            var path     = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<GossipSettings>(path);
            if (settings == null) return;
            // El GUID del asset .unity es lo unico que sobrevive a renombrar una escena,
            // y solo se lee desde el editor. Se hornea en TODOS los builds, tambien los
            // de desarrollo: el early return de abajo dejaria el dato de dev sin identidad.
            BakeSceneGuids(settings);

            bool isDevelopmentBuild = (report.summary.options & BuildOptions.Development) != 0;
            if (isDevelopmentBuild) return;

            if (settings.EnableDebug)
            {
                var so = new SerializedObject(settings);
                so.Update();
                var prop = so.FindProperty("enableDebug");
                if (prop != null)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    UnityEngine.Debug.Log("[Gossip Analytics] EnableDebug auto-disabled for build.");
                }
            }
        }

        // Hornea ruta de escena -> GUID del asset dentro de GossipSettings. El GUID solo
        // existe en el editor (AssetDatabase); en runtime SceneIdentity lo busca en este
        // mapa por la ruta de la escena activa.
        private static void BakeSceneGuids(GossipSettings settings)
        {
            var rutas = new System.Collections.Generic.List<string>();

            foreach (var bs in EditorBuildSettings.scenes)
            {
                if (bs != null && bs.enabled && !string.IsNullOrEmpty(bs.path) && !rutas.Contains(bs.path))
                    rutas.Add(bs.path);
            }

            foreach (var g in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(p) && !rutas.Contains(p))
                    rutas.Add(p);
            }

            var so = new SerializedObject(settings);
            so.Update();

            var lista = so.FindProperty("sceneGuids");
            if (lista == null) return;

            lista.arraySize = rutas.Count;
            for (int i = 0; i < rutas.Count; i++)
            {
                var item = lista.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("path").stringValue = rutas[i];
                item.FindPropertyRelative("guid").stringValue = AssetDatabase.AssetPathToGUID(rutas[i]);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[Gossip Analytics] SceneGuids baked: {rutas.Count} escenas.");
        }
    }
}
#endif
