using GossipSDK.A11y;

namespace GossipSDK.Analytics
{
    public static class InteractionToA11yAdapter
    {
        public static A11yActionEvent ToA11yEvent(
            string actionId,
            string inputType,
            string scene,
            string timestamp)
        {
            return new A11yActionEvent
            {
                ActionId = actionId,
                InputType = inputType,
                Scene = scene,
                Timestamp = timestamp
            };
        }
    }
}
