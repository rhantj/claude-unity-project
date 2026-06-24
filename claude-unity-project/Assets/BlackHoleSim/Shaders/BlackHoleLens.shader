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

            // BlackBodyColor.Evaluate(BlackHoleSim/Runtime/BlackBodyColor.cs)의 HLSL 미러.
            half3 BlackBodyColorApprox(float tempKelvin)
            {
                float t = clamp(tempKelvin, 1000.0, 40000.0) / 100.0;
                float r = t <= 66.0 ? 255.0 : clamp(329.698727446 * pow(t - 60.0, -0.1332047592), 0.0, 255.0);
                float g = t <= 66.0
                    ? clamp(99.4708025861 * log(t) - 161.1195681661, 0.0, 255.0)
                    : clamp(288.1221695283 * pow(t - 60.0, -0.0755148492), 0.0, 255.0);
                float b = t >= 66.0 ? 255.0 : (t <= 19.0 ? 0.0 : clamp(138.5177312231 * log(t - 10.0) - 305.0447927307, 0.0, 255.0));
                return half3(r, g, b) / 255.0;
            }

            // 디스크 슬랩 밀도: 반경 falloff(안쪽 진함) × 수직 가우시안.
            float DiskDensity(float rCyl, float absY)
            {
                if (rCyl < _BHDiskInner || rCyl > _BHDiskOuter || absY > _BHDiskThickness) return 0.0;
                float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                float vGauss = exp(-pow(absY / (0.5 * _BHDiskThickness), 2.0));
                return _BHDiskDensity * (1.0 - u) * vGauss;
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
                float trans = 1.0;
                float minR = 1e9;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float r = length(pos);
                    minR = min(minR, r);
                    if (r <= rs) return accum; // 호라이즌(앞쪽 누적색 유지)

                    float rCyl = length(pos.xz);
                    float dens = DiskDensity(rCyl, abs(pos.y));
                    if (dens > 0.0)
                    {
                        float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                        float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);
                        half3 emit = BlackBodyColorApprox(temp) * dens;
                        accum += trans * emit * _BHStepSize;
                        trans *= exp(-dens * _BHStepSize);
                        if (trans < 0.01) return accum;
                    }

                    float3 a0 = GeodesicAccel(pos, h2, rs);
                    float3 nextPos = pos + vel * _BHStepSize + 0.5 * a0 * _BHStepSize * _BHStepSize;
                    float3 a1 = GeodesicAccel(nextPos, h2, rs);
                    vel += 0.5 * (a0 + a1) * _BHStepSize;
                    pos = nextPos;
                }

                accum += trans * StarField(normalize(vel));

                // 포톤 링: 광선의 최근접 반경이 포톤 스피어에 가까울수록 밝은 고리.
                float ring = _BHPhotonRing * exp(-pow((minR - photonR) / (0.18 * rs), 2.0));
                accum += trans * ring * half3(1.0, 0.95, 0.85);

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
