using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GossipSDK.Core.Utilities
{
    public class WaitForTask : CustomYieldInstruction
    {
        private UniTask task;

        public WaitForTask(UniTask task)
        {
            this.task = task;
        }

        public override bool keepWaiting => !task.AsTask().IsCompleted;
    }
}