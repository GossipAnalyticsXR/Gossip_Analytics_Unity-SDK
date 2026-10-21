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
// El GUID se congela aqui, en el mismo instante que el nombre: la subida ocurre
// despues y en un hilo de fondo, donde no se puede resolver.
public string SceneGuid;
public string Version;
public string TimestampUtc;
public float PositionX;
public float PositionY;
public float PositionZ;
public float YawOffsetDeg;
public int ImageWidth;
public int ImageHeight;

// Procedencia y ventana real. TimestampUtc es una sola marca para las seis
// caras; estos dos dicen cuanto duro de verdad la captura, y CaptureSource
// que componente la tomo. ISO 8601, igual que TimestampUtc.
public string CaptureSource;
public string CaptureStartUtc;
public string CaptureEndUtc;

public static HeatmapPanoramaSpec CreateForCurrentScene(Vector3 pos, float yawOffsetDeg, int width, int height)
{
return new HeatmapPanoramaSpec
{
SceneName = SceneManager.GetActiveScene().name,
SceneGuid = GossipSDK.Core.Configuration.SceneIdentity.CurrentSceneGuid(GossipSDK.Core.Gossip.Instance?.Settings),
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
