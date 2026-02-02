using Cysharp.Threading.Tasks;

namespace GossipSDK.Core.Transport
{
    public interface ITransport
    {
        UniTask<bool> PostJsonAsync(string url, string json, string apiKeyHeader = null, string apiKeyValue = null);

        UniTask<bool> SendJsonEventAsync(string url, string json, string apiKeyHeader = null, string apiKeyValue = null);
    }
}
