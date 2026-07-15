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
                float far = _ProjectionParams.z;
                float n = saturate(i.eyeDepth / far);
                return fixed4(n, n, n, 1);
            }
            ENDCG
        }
    }
}
