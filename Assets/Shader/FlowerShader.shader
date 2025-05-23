Shader "Unlit/FlowerShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CutThreshold ("Cut Threshold", Range(0.0, 1.0)) = 0.3
        _noiseHeightFactor ("Noise to Height Factor", float) = 4.0
        _alpha ("Flower Alpha", Range(0.0,1.0)) = 0.5
        _groundHeightFactor ("Ground to Height factor", float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct FlowerData
            {
                float3 position;
                float noise;
                float3 wind;
                float angle;
                float2 groundUV;
            };
            
            StructuredBuffer<FlowerData> _FlowerBuffer;
            float _noiseHeightFactor;
            float _CutThreshold;
            float _alpha;
            sampler2D _MainTex;
            sampler2D _HeightRT;
            float _groundHeightFactor;
            sampler2D _CloudRT;

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
                float n = _FlowerBuffer[v.instanceID].noise;
                float height = (n * _noiseHeightFactor);

                v.vertex = RotateAroundYInDegrees(v.vertex, _FlowerBuffer[v.instanceID].angle);

                float3 worldPos = _FlowerBuffer[v.instanceID].position + v.vertex.xyz;
                worldPos *= float3(1.,height,1.) + (float3(0.,height,0.));

                float3 wind = _FlowerBuffer[v.instanceID].wind;

                float h = tex2Dlod(_HeightRT, float4(_FlowerBuffer[v.instanceID].groundUV,0.,0.)).r;
                worldPos.y += h * _groundHeightFactor;
                worldPos += wind * v.vertex.y;

                o.pos = UnityObjectToClipPos(float4(worldPos, 1.0)) ;



                o.instanceID = v.instanceID;
                return o;
            }



            float4 frag(v2f i) : SV_Target
            {
                if(_FlowerBuffer[i.instanceID].noise <= _CutThreshold) discard;

                float n = _FlowerBuffer[i.instanceID].noise;
                float4 col = tex2D(_MainTex, i.uv);
                float4 cloud = tex2D(_CloudRT, _FlowerBuffer[i.instanceID].groundUV).r;
                col.w = 0.1;
                return col * cloud;
            }
            ENDCG
        }
    }
}
