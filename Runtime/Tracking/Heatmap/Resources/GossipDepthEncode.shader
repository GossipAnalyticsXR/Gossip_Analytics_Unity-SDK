Shader "Gossip/DepthEncode" {
SubShader
{
Tags { "RenderType"="Opaque" }
Pass
{
Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata { float4 vertex : POSITION; };
struct v2f { float4 pos : SV_POSITION; float eyeDepth : TEXCOORD0; };
v2f vert(appdata v)
{
v2f o;
o.pos = UnityObjectToClipPos(v.vertex);
o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
return o;
}
fixed4 frag(v2f i) : SV_Target
{
float d01 = saturate(i.eyeDepth / 50.0);
float v = d01 * 65535.0;
float hi = floor(v / 256.0);
float lo = v - hi * 256.0;
return fixed4(hi / 255.0, lo / 255.0, 0.0, 1.0);
}
ENDCG
}
}
}
