using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GossipSDK.Heatmaps
{
[Serializable]
public class HeatmapPanoramaSpec
{
public string PlayerID;
public string SessionID;
public string SceneName;
public string Version;
public string TimestampUtc;
public float PositionX;
public float PositionY;
public float PositionZ;
public float YawOffsetDeg;
public int ImageWidth;
public int ImageHeight;

public static HeatmapPanoramaSpec CreateForCurrentScene(Vector3 pos, float yawOffsetDeg, int width, int height)
{
return new HeatmapPanoramaSpec
{
SceneName = SceneManager.GetActiveScene().name,
Version = Application.version,
TimestampUtc = DateTime.UtcNow.ToString("o"),
PositionX = pos.x,
PositionY = pos.y,
PositionZ = pos.z,
YawOffsetDeg = yawOffsetDeg,
ImageWidth = width,
ImageHeight = height,
};
}
}
}
