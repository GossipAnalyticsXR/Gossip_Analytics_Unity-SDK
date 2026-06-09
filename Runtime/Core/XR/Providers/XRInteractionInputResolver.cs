using UnityEngine;
using UnityEngine.XR;

namespace GossipSDK.Core.XR
{
    public static class XRInteractionInputResolver
    {
        public static InteractionInputType GetCurrentInputType()
        {
            if (OpenXRHandTrackingActive())
                return InteractionInputType.Hand;

            if (XRControllerActive())
                return InteractionInputType.Controller;
            return InteractionInputType.Unknown;
        }

        private static bool OpenXRHandTrackingActive()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            // Hand tracking no reporta trigger / grip
            bool leftHand = left.isValid && !left.TryGetFeatureValue(CommonUsages.trigger, out _);
            bool rightHand = right.isValid && !right.TryGetFeatureValue(CommonUsages.trigger, out _);

            return leftHand || rightHand;
        }

        private static bool XRControllerActive()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            return (left.isValid && left.TryGetFeatureValue(CommonUsages.trigger, out _))
                || (right.isValid && right.TryGetFeatureValue(CommonUsages.trigger, out _));
        }
    }
}
