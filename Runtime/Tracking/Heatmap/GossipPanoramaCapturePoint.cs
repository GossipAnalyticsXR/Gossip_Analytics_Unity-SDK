using UnityEngine;

namespace GossipSDK.Heatmaps
{
// Empty locator component. Place on any GameObject in the scene to mark the
// world position where the automatic 360 panorama capture should be taken.
// If no instance is present in the scene, HeatmapPanoramaAutoCapture falls
// back to Camera.main's position.
public class GossipPanoramaCapturePoint : MonoBehaviour
{
}
}
