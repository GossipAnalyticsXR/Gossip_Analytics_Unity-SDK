namespace GossipSDK.Core.Session
{
    public interface ISessionInfo
    {
        string PlayerID { get; set; }
        string SessionID { get; set; }
    }
}