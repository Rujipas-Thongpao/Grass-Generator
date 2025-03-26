Shader "Unlit/shader"
{
    Properties
    {
        _NewColor ("young grass color", Color) = (0, 1, 0, 1)
        _OldColor ("old grass color", Color) = (0, 1, 1, 1)
        _noiseHeightFactor ("Noise to Height Factor", float) = 4.0

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct GrassData
            {
                float3 position;
                float noise;
                float3 wind;
            };

            
            StructuredBuffer<GrassData> GrassBuffer;
            float4 _OldColor;
            float4 _NewColor;
            float _noiseHeightFactor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                uint instanceID : SV_InstanceID;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float n = GrassBuffer[v.instanceID].noise;
                float height = 1+(n * _noiseHeightFactor);
                float3 worldPos = GrassBuffer[v.instanceID].position + v.vertex.xyz ;
                worldPos *= float3(1.,height,1.) + (float3(0.,height/2,0.));

                float3 wind = GrassBuffer[v.instanceID].wind;

                worldPos += wind * v.uv.y;
                o.pos = UnityObjectToClipPos(float4(worldPos, 1.0)) ;
                o.uv = v.uv;
                o.instanceID = v.instanceID;
                return o;
            }



            float4 frag(v2f i) : SV_Target
            {
                float n = GrassBuffer[i.instanceID].noise;
                float4 col = lerp(_NewColor, _OldColor, n);
                // float3 wind = GrassBuffer[i.instanceID].wind;
                // return float4(wind, 1.0);

                return col * n * i.uv.y;
            }
            ENDHLSL
        }
    }
}
