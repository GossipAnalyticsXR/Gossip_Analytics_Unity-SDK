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

        [SerializeField] private bool useHttpEndpoint = true;
        public bool UseHttpEndpoint => useHttpEndpoint;

        [SerializeField] private string ingestPath = "/ingest";
        public string IngestPath => ingestPath;

        [Header("API Key Header (same for all envs)")]
        [SerializeField] private string apiKeyHeader = "x-api-key";
        public string ApiKeyHeader => apiKeyHeader;

        [Header("Server URLs per environment")]
        [SerializeField] private string devServerUrl = "https://gossip-url.com";
        [SerializeField] private string betaServerUrl = "https://gossip-url.com";
        [SerializeField] private string prodServerUrl = "https://gossip-url.com";

        public string DevServerUrl => devServerUrl;
        public string BetaServerUrl => betaServerUrl;
        public string ProdServerUrl => prodServerUrl;

        [Header("API Key per environment")]
        [SerializeField] private string devApiKey = "dev-key";
        [SerializeField] private string betaApiKey = "beta-key";
        [SerializeField] private string prodApiKey = "prod-key";

        public string DevApiKey => devApiKey;
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
                return environment switch
                {
                    Environment.Dev => string.IsNullOrWhiteSpace(devServerUrl) ? devServerUrl : devServerUrl,
                    Environment.Beta => string.IsNullOrWhiteSpace(betaServerUrl) ? betaServerUrl : betaServerUrl,
                    Environment.Production => string.IsNullOrWhiteSpace(prodServerUrl) ? prodServerUrl : prodServerUrl,
                    _ => devServerUrl
                };
            }
        }

        public string ApiKeyValue
        {
            get
            {
                return environment switch
                {
                    Environment.Dev => devApiKey,
                    Environment.Beta => betaApiKey,
                    Environment.Production => prodApiKey,
                    _ => devApiKey
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
        private void Reset()
        {
            enableHeatmaps = true;
        }

                }

#if META_CORE
        [SerializeField] private bool trackMetaUserID = false;
        public bool TrackMetaUserID => trackMetaUserID;
#endif
    }
}
