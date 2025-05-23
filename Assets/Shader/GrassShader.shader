// Shader "Unlit/shader" // SUGGESTION_CLARITY: Consider renaming to something more descriptive,
//                      // e.g., "Custom/GrassShader" or "Vegetation/GrassShader"
Shader "Unlit/shader"
{
    Properties
    {
        _NewColor ("young grass color", Color) = (0, 1, 0, 1)
        _OldColor ("old grass color", Color) = (0, 1, 1, 1)
        // FURTHER_IMPROVEMENT_IDEA: Extend color options for more variety:
        // - Add _DryColor, _TipColor for more detailed gradients.
        // - Consider a color texture for seasonal variations or specific patterns.
        _noiseHeightFactor ("Noise to Height Factor", float) = 4.0
        _CutThreshold ("Cut Threshold", Range(0.0, 1.0)) = 0.3
        _groundHeightFactor ("Ground to Height factor", float) = 1.0

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull off

        Pass
        {
            // FURTHER_IMPROVEMENT_IDEA: For more natural wind:
            // 1. Add a time-varying component to the wind calculation (e.g., using _Time.y) to simulate sway.
            // 2. Use a wind noise texture sampled with world coordinates to create more complex wind patterns
            //    instead of a uniform direction and strength.
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
            float _groundHeightFactor;
            float _CutThreshold;
            sampler2D _HeightRT;
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

                float n = GrassBuffer[v.instanceID].noise;
                float height = (n * _noiseHeightFactor); // This height is based on per-instance noise
                v.vertex = RotateAroundYInDegrees(v.vertex, GrassBuffer[v.instanceID].angle);

                float3 worldPos = GrassBuffer[v.instanceID].position + v.vertex.xyz;
                // IMPROVEMENT_FLEXIBILITY: The scaling 'height' derived from noise and _noiseHeightFactor
                // is applied to worldPos.y. If the base mesh (v.vertex.xyz) doesn't have a consistent
                // Y range (e.g., always 0 to 1), this scaling might behave unexpectedly across different meshes.
                // Consider normalizing v.vertex.y in calculations or clarifying expected mesh structure.

                // CLARITY_OPTIMIZATION: The line below:
                // worldPos *= float3(1.,height,1.) + (float3(0.,height,0.));
                // effectively scales worldPos.x and worldPos.z by 'height', and worldPos.y by 'height*height' (if it started at 1)
                // or more generally: worldPos.x_final = worldPos.x * height; worldPos.y_final = worldPos.y * height + worldPos.y*height = worldPos.y * 2 * height (if original v.vertex.y was part of worldPos.y).
                // This might be unintentional.
                // If the goal is to scale the mesh's original Y extent by 'height', it might be clearer as:
                // float3 scaledVertex = v.vertex.xyz;
                // scaledVertex.y *= height;
                // float3 worldPos = GrassBuffer[v.instanceID].position + scaledVertex;
                // Or if height is an offset:
                // worldPos.y += height; // (after scaledVertex has been added to position)
                // Please clarify the intended transformation.
                worldPos *= float3(1.,height,1.) + (float3(0.,height,0.));

                float3 wind = GrassBuffer[v.instanceID].wind;

                float h_ground = tex2Dlod(_HeightRT, float4(GrassBuffer[v.instanceID].groundUV,0.,0.)).r; // Renamed 'h' to 'h_ground' for clarity
                // The _groundHeightFactor scales the height contribution from the terrain heightmap.
                // The _noiseHeightFactor scales the height contribution from the noise value (affecting individual blade height).
                // These two factors work together: one sets the base height from terrain, the other modulates individual blade height.
                worldPos.y += h_ground * _groundHeightFactor;

                // IMPROVEMENT_CONTROL: Wind is applied proportionally to v.vertex.y.
                // This creates a nice sway. For more control, v.vertex.y could be normalized (0-1)
                // and then optionally passed through a power function (e.g., pow(normalized_y, _WindBendFactor))
                // to control the bend curve along the grass blade.
                worldPos += wind * v.vertex.y;

                o.pos = UnityObjectToClipPos(float4(worldPos, 1.0)) ;


                o.instanceID = v.instanceID;
                return o;
            }



            float4 frag(v2f i) : SV_Target
            {
                if(GrassBuffer[i.instanceID].noise <= _CutThreshold) discard;

                float n = GrassBuffer[i.instanceID].noise;
                float4 col = lerp(_NewColor, _OldColor, n);
                float4 cloud = tex2D(_CloudRT, GrassBuffer[i.instanceID].groundUV);
                // return h;
                // CLARITY_NOTE: The multiplication by i.uv.y darkens the base of the grass.
                // This assumes the grass mesh UVs are mapped such that uv.y = 0 at the root
                // and uv.y = 1 (or higher) at the tip.
                return col * n * i.uv.y * cloud;
            }
            ENDHLSL
        }
    }
}
