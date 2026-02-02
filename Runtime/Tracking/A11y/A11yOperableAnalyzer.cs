using System.Collections.Generic;
using GossipSDK.A11y;

namespace GossipSDK.A11y.Analysis
{
    public class A11yOperableAnalyzer
    {
        private readonly Dictionary<string, HashSet<string>> actionInputs =
            new Dictionary<string, HashSet<string>>();

        public void RegisterAction(A11yActionEvent evt)
        {
            if (!actionInputs.ContainsKey(evt.ActionId))
                actionInputs[evt.ActionId] = new HashSet<string>();

            actionInputs[evt.ActionId].Add(evt.InputType);
        }

        public bool IsOperable(string actionId)
        {
            return actionInputs.ContainsKey(actionId)
                   && actionInputs[actionId].Count >= 2;
        }

        public Dictionary<string, int> GetCoverage()
        {
            var result = new Dictionary<string, int>();

            foreach (var kv in actionInputs)
                result[kv.Key] = kv.Value.Count;

            return result;
        }
    }
}
