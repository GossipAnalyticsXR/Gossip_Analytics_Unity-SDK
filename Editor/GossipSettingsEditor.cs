using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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

        private string _connectionStatus = "";
        private bool _isTesting = false;
        private Color _statusColor = Color.gray;

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

            var version = GetSDKVersion();
            var versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };
            EditorGUILayout.LabelField("Gossip Analytics SDK v" + version, versionStyle);
            EditorGUILayout.Space(4);

            EditorGUILayout.Space();
            DrawTexture(logo);
            EditorGUILayout.Space(10);
            DrawTexture(header);
            EditorGUILayout.Space(10);

            EditorStyles.wordWrappedLabel.fontStyle = FontStyle.Bold;
            EditorGUILayout.LabelField("Get your API Keys from the Gossip Analytics Dashboard, paste them here and choose an Environment.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(5);


            if (environmentProp != null)
                EditorGUILayout.PropertyField(environmentProp, new GUIContent("Environment"));

            EditorGUILayout.PropertyField(enableDebugProp, new GUIContent("Enable Debug Logging"));

            var settingsTarget = target as GossipSettings;
            if (settingsTarget != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Effective (based on Environment)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("API Key (value)", settingsTarget.ApiKeyValue);
                EditorGUILayout.Space();
            }


            EditorGUILayout.Space();



            if (devApiKeyProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string devVal = devApiKeyProp.stringValue;
                string newDevVal = EditorGUILayout.TextField(new GUIContent("Dev API Key"), devVal);
                if (EditorGUI.EndChangeCheck())
                {
                    devApiKeyProp.stringValue = newDevVal;
                    serializedObject.ApplyModifiedProperties();
                }
                if (string.IsNullOrEmpty(newDevVal))
                {
                    var devRect = GUILayoutUtility.GetLastRect();
                    var devHint = new GUIStyle(EditorStyles.label);
                    devHint.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                    devHint.fontStyle = FontStyle.Italic;
                    GUI.Label(new Rect(devRect.x + EditorGUIUtility.labelWidth + 4, devRect.y, devRect.width - EditorGUIUtility.labelWidth - 8, devRect.height),
                        "Paste your Dev API Key here", devHint);
                }
            }
            if (betaApiKeyProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string betaVal = betaApiKeyProp.stringValue;
                string newBetaVal = EditorGUILayout.TextField(new GUIContent("Beta API Key"), betaVal);
                if (EditorGUI.EndChangeCheck())
                {
                    betaApiKeyProp.stringValue = newBetaVal;
                    serializedObject.ApplyModifiedProperties();
                }
                if (string.IsNullOrEmpty(newBetaVal))
                {
                    var betaRect = GUILayoutUtility.GetLastRect();
                    var betaHint = new GUIStyle(EditorStyles.label);
                    betaHint.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                    betaHint.fontStyle = FontStyle.Italic;
                    GUI.Label(new Rect(betaRect.x + EditorGUIUtility.labelWidth + 4, betaRect.y, betaRect.width - EditorGUIUtility.labelWidth - 8, betaRect.height),
                        "Paste your Beta API Key here", betaHint);
                }
            }
            if (prodApiKeyProp != null)
            {
                EditorGUI.BeginChangeCheck();
                string prodVal = prodApiKeyProp.stringValue;
                string newProdVal = EditorGUILayout.TextField(new GUIContent("Prod API Key"), prodVal);
                if (EditorGUI.EndChangeCheck())
                {
                    prodApiKeyProp.stringValue = newProdVal;
                    serializedObject.ApplyModifiedProperties();
                }
                if (string.IsNullOrEmpty(newProdVal))
                {
                    var prodRect = GUILayoutUtility.GetLastRect();
                    var prodHint = new GUIStyle(EditorStyles.label);
                    prodHint.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                    prodHint.fontStyle = FontStyle.Italic;
                    GUI.Label(new Rect(prodRect.x + EditorGUIUtility.labelWidth + 4, prodRect.y, prodRect.width - EditorGUIUtility.labelWidth - 8, prodRect.height),
                        "Paste your Production API Key here", prodHint);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();


            if (apiKeyValueProp != null)
                EditorGUILayout.PropertyField(apiKeyValueProp, new GUIContent("API Key Value"));

            EditorGUILayout.Space();

            // --- Check Connection ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connection Test", EditorStyles.boldLabel);
            GUI.enabled = !_isTesting;
            if (GUILayout.Button("Check Connection"))
            {
                CheckConnectionAsync();
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_connectionStatus))
            {
                var msgType = _isTesting ? MessageType.Info : (_statusColor == Color.green ? MessageType.None : MessageType.Error);
                if (_statusColor == Color.green)
                {
                    var prevColor = GUI.color;
                    GUI.color = Color.green;
                    EditorGUILayout.HelpBox(_connectionStatus, MessageType.None);
                    GUI.color = prevColor;
                }
                else
                {
                    EditorGUILayout.HelpBox(_connectionStatus, msgType);
                }
            }

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

        private async void CheckConnectionAsync()
        {
            _isTesting = true;
            _connectionStatus = "Testing connection...";
            _statusColor = Color.gray;
            Repaint();

            var st = target as GossipSDK.Core.Configuration.GossipSettings;
            if (st == null)
            {
                _connectionStatus = "\u274C No GossipSettings target found.";
                _statusColor = Color.red;
                _isTesting = false;
                Repaint();
                return;
            }

            string serverUrl = st.GetActiveServerUrl();
            if (string.IsNullOrEmpty(serverUrl))
            {
                _connectionStatus = "\u274C Server URL is not configured.";
                _statusColor = Color.red;
                _isTesting = false;
                Repaint();
                return;
            }

            string testUrl = serverUrl.TrimEnd('/') + "/health";
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var response = await client.GetAsync(testUrl);
                    if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                    {
                        _connectionStatus = "\u2705 Connected \u2014 server is reachable";
                        _statusColor = Color.green;
                    }
                    else
                    {
                        _connectionStatus = "\u274C Server returned: " + (int)response.StatusCode;
                        _statusColor = Color.red;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _connectionStatus = "\u274C Connection failed: " + ex.Message;
                _statusColor = Color.red;
            }
            finally
            {
                _isTesting = false;
                Repaint();
            }
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

        private static string GetSDKVersion()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GossipSettingsEditor).Assembly);
            return info != null ? info.version : "Unknown";
        }
    }
}
