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

            float3 _BHCamPos, _BHCamForward, _BHCamRight, _BHCamUp;
            float _BHTanHalfFovX, _BHTanHalfFovY, _BHStarDensity;
            float3 _BHWorldPos;
            float _BHRs;
            float _BHDiskInner, _BHDiskOuter, _BHDiskThickness, _BHDiskDensity;
            float _BHDiskTempInner, _BHDiskTempOuter;
            float _BHBeaming, _BHRedshift, _BHPhotonRing;
            float _BHStepSize;
            int _BHStepCount;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 texcoord : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                o.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return o;
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

            // PhotonGeodesic.Acceleration(BlackHoleSim/Runtime/PhotonGeodesic.cs)의 HLSL 미러.
            float3 GeodesicAccel(float3 pos, float h2, float rs)
            {
                float r2 = dot(pos, pos);
                float invR5 = 1.0 / (r2 * r2 * sqrt(r2));
                return -1.5 * rs * h2 * invR5 * pos;
            }

            // 측지선 적분. BH 중심 좌표(pos = world - _BHWorldPos). 반환: HDR 색.
            half3 March(float3 worldOrigin, float3 dir)
            {
                float rs = _BHRs;
                float photonR = 1.5 * rs;
                float3 pos = worldOrigin - _BHWorldPos;
                float3 vel = dir;
                float h2 = dot(cross(pos, vel), cross(pos, vel));

                half3 accum = 0;
                float minR = 1e9;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float r = length(pos);
                    minR = min(minR, r);
                    if (r <= rs) return accum; // 호라이즌(앞쪽 누적색 유지)

                    float3 a0 = GeodesicAccel(pos, h2, rs);
                    float3 nextPos = pos + vel * _BHStepSize + 0.5 * a0 * _BHStepSize * _BHStepSize;
                    float3 a1 = GeodesicAccel(nextPos, h2, rs);
                    vel += 0.5 * (a0 + a1) * _BHStepSize;
                    pos = nextPos;
                }

                accum += StarField(normalize(vel));

                // 포톤 링: 광선의 최근접 반경이 포톤 스피어에 가까울수록 밝은 고리.
                float ring = _BHPhotonRing * exp(-pow((minR - photonR) / (0.18 * rs), 2.0));
                accum += ring * half3(1.0, 0.95, 0.85);

                return accum;
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
