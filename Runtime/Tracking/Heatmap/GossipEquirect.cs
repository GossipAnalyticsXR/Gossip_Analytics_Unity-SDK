using System.Threading.Tasks;
using UnityEngine;

namespace GossipSDK.Heatmaps
{
public static class GossipEquirect
{
public struct FaceBasis
{
public Vector3 forward;
public Vector3 up;
public Vector3 right;
}

// Six axis-aligned faces (rotated together by yawOffsetDeg around world Y), each with a
// 90-degree field of view. Render + sampling both use these SAME basis vectors, so the
// mapping is self-consistent regardless of any external cubemap-layout convention.
// Validated against Unity's known identity case: forward=+Z, up=+Y -> right=+X.
public static FaceBasis[] BuildFaceBases(float yawOffsetDeg)
{
var yaw = Quaternion.Euler(0f, yawOffsetDeg, 0f);
Vector3[] forwards = new Vector3[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
Vector3[] ups = new Vector3[] { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

var bases = new FaceBasis[6];
for (int i = 0; i < 6; i++)
{
var fwd = (yaw * forwards[i]).normalized;
var up = (yaw * ups[i]).normalized;
var right = Vector3.Cross(up, fwd).normalized;
bases[i] = new FaceBasis { forward = fwd, up = up, right = right };
}
return bases;
}

// Builds the equirect pixel array (Unity Texture2D convention: array index 0 = bottom-left row).
public static Color32[] BuildEquirect(Color32[][] faceColors, FaceBasis[] faceBases, int width, int height, int faceSize)
{
var result = new Color32[width * height];

Parallel.For(0, height, rowFromTop =>
{
// rowFromTop = 0 is the TOP of the final image (north pole / +Y), matching the
// dashboard's v = 0.5 - lat/PI convention where v=0 -> lat=+90.
float v = (rowFromTop + 0.5f) / height;
float lat = (0.5f - v) * Mathf.PI;
float cosLat = Mathf.Cos(lat);
float sinLat = Mathf.Sin(lat);

int destRow = height - 1 - rowFromTop;
int destRowOffset = destRow * width;

for (int px = 0; px < width; px++)
{
float u = (px + 0.5f) / width;
float lon = (0.5f - u) * 2f * Mathf.PI;

Vector3 dir = new Vector3(Mathf.Sin(lon) * cosLat, sinLat, Mathf.Cos(lon) * cosLat);

result[destRowOffset + px] = SampleCube(dir, faceColors, faceBases, faceSize);
}
});

return result;
}

public static Color32 SampleCube(Vector3 dir, Color32[][] faceColors, FaceBasis[] faceBases, int faceSize)
{
int bestFace = 0;
float bestDot = -2f;
for (int f = 0; f < 6; f++)
{
float d = Vector3.Dot(dir, faceBases[f].forward);
if (d > bestDot)
{
bestDot = d;
bestFace = f;
}
}

var basis = faceBases[bestFace];
float forwardComp = Vector3.Dot(dir, basis.forward);
if (forwardComp <= 0.0001f) forwardComp = 0.0001f;
float rightComp = Vector3.Dot(dir, basis.right) / forwardComp;
float upComp = Vector3.Dot(dir, basis.up) / forwardComp;

float s = Mathf.Clamp01((1f - rightComp) * 0.5f);
float t = Mathf.Clamp01((upComp + 1f) * 0.5f);

int px = Mathf.Clamp((int)(s * faceSize), 0, faceSize - 1);
int py = Mathf.Clamp((int)(t * faceSize), 0, faceSize - 1);

return faceColors[bestFace][py * faceSize + px];
}
}
}
