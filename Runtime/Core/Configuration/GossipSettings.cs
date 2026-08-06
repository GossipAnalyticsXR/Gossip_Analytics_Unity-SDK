using UnityEngine;

namespace GossipSDK.Core.Configuration
{
    [CreateAssetMenu(fileName = "GossipAnalyticsSettings", menuName = "GossipAnalytics/Settings", order = 1)]
    public class GossipSettings : ScriptableObject
    {
        public enum Environment
        {
            Dev,
            Beta,
            Production
        }

        [Header("Environment / Versioning")]
        [SerializeField] private Environment environment = Environment.Dev;

        public Environment SelectedEnvironment => environment;

        [HideInInspector] [SerializeField] private bool useHttpEndpoint = true;
        public bool UseHttpEndpoint => useHttpEndpoint;

        [SerializeField] private string ingestPath = "/ingest";
        public string IngestPath => ingestPath;

        [Header("API Key Header (same for all envs)")]
        [HideInInspector] [SerializeField] private string apiKeyHeader = "x-api-key";
        public string ApiKeyHeader => apiKeyHeader;

        // Server URLs are hardcoded — users should never need to change these.
        [HideInInspector] public string devServerUrl    = "https://service-a-unity-sdk.onrender.com";
        [HideInInspector] public string betaServerUrl   = "https://service-a-unity-sdk.onrender.com";
        [HideInInspector] public string productionServerUrl   = "https://service-a-unity-sdk.onrender.com";

        public string DevServerUrl  => devServerUrl;
        public string BetaServerUrl => betaServerUrl;
        public string ProductionServerUrl => productionServerUrl;

        [Header("API Key per environment")]
        [SerializeField] private string devApiKey = "";
        [SerializeField] private string betaApiKey = "";
        [SerializeField] private string prodApiKey = "";

        public string DevApiKey  => devApiKey;
        public string BetaApiKey => betaApiKey;
        public string ProdApiKey => prodApiKey;

        [Header("Heatmaps")]
        public bool enableHeatmaps = true;
        public bool EnableHeatmaps => enableHeatmaps;

        [SerializeField] private string heatmapSceneUploadPath = "/heatmaps/scenes";
        public string HeatmapSceneUploadPath => heatmapSceneUploadPath;

        public string ServerURL
        {
            get
            {
                return SelectedEnvironment switch
                {
                    Environment.Dev        => devServerUrl,
                    Environment.Beta       => betaServerUrl,
                    Environment.Production => productionServerUrl,
                    _                      => devServerUrl
                };
            }
        }

        public string GetActiveServerUrl()
        {
            string stored = SelectedEnvironment switch
            {
                Environment.Dev        => devServerUrl,
                Environment.Beta       => betaServerUrl,
                Environment.Production => productionServerUrl,
                _                      => devServerUrl
            };

            const string fallback = "https://service-a-unity-sdk.onrender.com";
            return string.IsNullOrEmpty(stored) ? fallback : stored;
        }

        public string ApiKeyValue
        {
            get
            {
                return SelectedEnvironment switch
                {
                    Environment.Dev        => devApiKey,
                    Environment.Beta       => betaApiKey,
                    Environment.Production => prodApiKey,
                    _                      => devApiKey
                };
            }
        }

        [Header("Debug")]
        [SerializeField] private bool enableDebug = true;
        public bool EnableDebug => enableDebug;

        public string HeatmapSceneUploadUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ServerURL))
                    return string.Empty;

                return $"{ServerURL}{heatmapSceneUploadPath}";
            }
        }

        private void Reset()
        {
            enableHeatmaps = true;
        }
    }
}
