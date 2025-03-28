Shader "Unlit/shader"
{
    Properties
    {
        _NewColor ("young grass color", Color) = (0, 1, 0, 1)
        _OldColor ("old grass color", Color) = (0, 1, 1, 1)
        _noiseHeightFactor ("Noise to Height Factor", float) = 4.0
        _CutThreshold ("Cut Threshold", Range(0.0, 1.0)) = 0.3
        // [HideInInspector] _HeightRT ("Noise RT", 2D) = "bump" {}

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
                float angle;
                float2 groundUV;
            };

            
            StructuredBuffer<GrassData> GrassBuffer;
            float4 _OldColor;
            float4 _NewColor;
            float _noiseHeightFactor;
            float _CutThreshold;
            // sampler2D _HeightRT;

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

            float4 RotateAroundYInDegrees (float4 vertex, float rad)
            {
                float sina, cosa;
                sincos(rad, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float4(mul(m, vertex.xz), vertex.yw).xzyw;
            }


            v2f vert(appdata v)
            {
                v2f o;
                o.uv = v.uv;

                float n = GrassBuffer[v.instanceID].noise;
                float height = (n * _noiseHeightFactor);
                v.vertex = RotateAroundYInDegrees(v.vertex, GrassBuffer[v.instanceID].angle);

                float3 worldPos = GrassBuffer[v.instanceID].position + v.vertex.xyz;
                worldPos *= float3(1.,height,1.) + (float3(0.,height,0.));

                float3 wind = GrassBuffer[v.instanceID].wind;

                // float h = tex2Dlod(_HeightRT, float4(GrassBuffer[v.instanceID].groundUV,0.,0.)).r;
                // worldPos.y += h * 10.0;

                worldPos += wind * worldPos.y;

                o.pos = UnityObjectToClipPos(float4(worldPos, 1.0)) ;


                o.instanceID = v.instanceID;
                return o;
            }



            float4 frag(v2f i) : SV_Target
            {
                if(GrassBuffer[i.instanceID].noise <= _CutThreshold) discard;

                float n = GrassBuffer[i.instanceID].noise;
                float4 col = lerp(_NewColor, _OldColor, n);
                // float3 wind = GrassBuffer[i.instanceID].wind;
                // return float4(wind, 1.0);
                // float h = tex2D(_HeightRT, GrassBuffer[i.instanceID].groundUV).r;

                return col * n * i.uv.y;
            }
            ENDHLSL
        }
    }
}
