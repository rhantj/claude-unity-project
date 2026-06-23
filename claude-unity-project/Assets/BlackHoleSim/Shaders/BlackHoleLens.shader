Shader "Hidden/BlackHoleSim/BlackHoleLens"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "BlackHoleLens"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _BHCamPos;
            float3 _BHCamForward;
            float3 _BHCamRight;
            float3 _BHCamUp;
            float _BHTanHalfFovX;
            float _BHTanHalfFovY;
            float _BHStarDensity;

            float3 _BHWorldPos;
            float _BHLensMu;
            float _BHSoftening;
            float _BHStepSize;
            float _BHHorizonRadius;
            int _BHStepCount;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float3 CameraRayDir(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                float3 dir = _BHCamForward
                    + ndc.x * _BHTanHalfFovX * _BHCamRight
                    + ndc.y * _BHTanHalfFovY * _BHCamUp;
                return normalize(dir);
            }

            half3 StarField(float3 dir)
            {
                float3 cell = floor(dir * 400.0);
                float n = Hash13(cell);
                float star = step(1.0 - _BHStarDensity, n) * n;
                return half3(star, star, star);
            }

            // GravityField.AccelerationAt(BlackHoleSim/Runtime/GravityField.cs)의 HLSL 미러.
            float3 AccelerationAt(float3 source, float mu, float softening, float3 pos)
            {
                float3 r = source - pos;
                float dist2 = dot(r, r) + softening * softening;
                float invDist = rsqrt(max(dist2, 1e-6));
                float invDist3 = invDist / dist2;
                return mu * invDist3 * r;
            }

            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float3 toCenter = _BHWorldPos - p;
                    if (dot(toCenter, toCenter) <= _BHHorizonRadius * _BHHorizonRadius)
                        return half3(0, 0, 0);

                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    p += normalize(d) * _BHStepSize;
                }

                return StarField(normalize(d));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = CameraRayDir(input.texcoord);
                half3 color = March(_BHCamPos, dir);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
