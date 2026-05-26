#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using GossipSDK.Core.Configuration;

namespace Editor.InspectorViews
{
    [CustomEditor(typeof(GossipSettings))]
    public class GossipSettingsEditor : UnityEditor.Editor
    {
        private Texture2D logo;
        private Texture2D header;

        private SerializedProperty environmentProp;


        private SerializedProperty devApiKeyProp;
        private SerializedProperty betaApiKeyProp;
        private SerializedProperty prodApiKeyProp;

        private SerializedProperty endPoint;

        private SerializedProperty serverURLProp;
        private SerializedProperty enableDebugProp;

        private SerializedProperty apiKeyHeaderProp;
        private SerializedProperty apiKeyValueProp;

        private SerializedProperty enableHeatmaps;

#if META_CORE
        private SerializedProperty trackMetaUserIDProp;
#endif

        private void OnEnable()
        {
            string logoPath = Path.Combine("Assets", "Gossip Analytics", "Samples", "Images", "logo.png");
            string headerPath = Path.Combine("Assets", "Gossip Analytics", "Samples", "Images", "gossip.png");

            logo = LoadImage(logoPath);
            header = LoadImage(headerPath);

            environmentProp = serializedObject.FindProperty("environment");
            devApiKeyProp = serializedObject.FindProperty("devApiKey");
            betaApiKeyProp = serializedObject.FindProperty("betaApiKey");
            prodApiKeyProp = serializedObject.FindProperty("prodApiKey");

            endPoint = serializedObject.FindProperty("useHttpEndpoint");

            serverURLProp = serializedObject.FindProperty("serverURL");
            enableDebugProp = serializedObject.FindProperty("enableDebug");

            apiKeyHeaderProp = serializedObject.FindProperty("apiKeyHeader");
            apiKeyValueProp = serializedObject.FindProperty("apiKeyValue");

            enableHeatmaps = serializedObject.FindProperty("enableHeatmaps");

#if META_CORE
            trackMetaUserIDProp = serializedObject.FindProperty("trackMetaUserID");
#endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            DrawTexture(logo);
            EditorGUILayout.Space(10);
            DrawTexture(header);
            EditorGUILayout.Space(10);

            EditorStyles.wordWrappedLabel.fontStyle = FontStyle.Bold;
            EditorGUILayout.LabelField("Get your API Keys from the gossip analytics Dashboard, paste them here and choose an Environment.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);


            if (environmentProp != null)
                EditorGUILayout.PropertyField(environmentProp, new GUIContent("Environment"));

            EditorGUILayout.PropertyField(enableDebugProp, new GUIContent("Enable Debug Logging"));

            var settingsTarget = target as GossipSettings;
            if (settingsTarget != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Effective (based on Environment)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Server URL", settingsTarget.ServerURL);
                EditorGUILayout.LabelField("API Key (value)", settingsTarget.ApiKeyValue);
                EditorGUILayout.Space();
            }

            if (endPoint != null)
                EditorGUILayout.PropertyField(endPoint, new GUIContent("Use EndPoint"));

            EditorGUILayout.Space();



            if (devApiKeyProp != null)
                EditorGUILayout.PropertyField(devApiKeyProp, new GUIContent("Dev API Key"));
            if (betaApiKeyProp != null)
                EditorGUILayout.PropertyField(betaApiKeyProp, new GUIContent("Beta API Key"));
            if (prodApiKeyProp != null)
                EditorGUILayout.PropertyField(prodApiKeyProp, new GUIContent("Prod API Key"));

            EditorGUILayout.Space();
            EditorGUILayout.Space();


            if (apiKeyHeaderProp != null)
                EditorGUILayout.PropertyField(apiKeyHeaderProp, new GUIContent("API Key Header"));
            if (apiKeyValueProp != null)
                EditorGUILayout.PropertyField(apiKeyValueProp, new GUIContent("API Key Value"));

            EditorGUILayout.Space();

            if (enableHeatmaps != null)
                EditorGUILayout.PropertyField(enableHeatmaps, new GUIContent("Enable Heatmaps"));


#if META_CORE
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Meta Settings", EditorStyles.boldLabel);
            if (trackMetaUserIDProp != null) EditorGUILayout.PropertyField(trackMetaUserIDProp, new GUIContent("Track Meta User ID"));
#endif

            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTexture(Texture2D texture)
        {
            if (texture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                float maxWidth = 150f;
                float w = Mathf.Min(texture.width, maxWidth);
                float h = (texture.height * w) / texture.width;

                Rect rect = GUILayoutUtility.GetRect(w, h);
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        private Texture2D LoadImage(string relativePath)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
        }
    }
}
#endif
