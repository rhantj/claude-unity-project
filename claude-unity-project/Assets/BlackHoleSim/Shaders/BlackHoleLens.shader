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

            float3 GeodesicAccel(float3 pos, float h2, float rs)
            {
                float r2 = dot(pos, pos);
                float invR5 = 1.0 / (r2 * r2 * sqrt(r2));
                return -1.5 * rs * h2 * invR5 * pos;
            }

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

            float DiskDensity(float rCyl, float absY)
            {
                if (rCyl < _BHDiskInner || rCyl > _BHDiskOuter || absY > _BHDiskThickness) return 0.0;
                float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                float vGauss = exp(-pow(absY / (0.5 * _BHDiskThickness), 2.0));
                return _BHDiskDensity * (1.0 - u) * vGauss;
            }

            // Relativity(BlackHoleSim/Runtime/Relativity.cs)의 HLSL 미러.
            float OrbitalSpeedRel(float r, float rs)
            {
                float denom = r - rs;
                if (denom <= 0.0) return 0.0;
                return min(sqrt((rs * 0.5) / denom), 0.999);
            }
            float DopplerFactor(float beta, float cosAngle)
            {
                float gamma = 1.0 / sqrt(max(1.0 - beta * beta, 1e-6));
                return 1.0 / (gamma * (1.0 - beta * cosAngle));
            }
            float GravRedshift(float r, float rs)
            {
                return sqrt(max(1.0 - rs / r, 0.0));
            }

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
                    if (r <= rs) return accum;

                    float rCyl = length(pos.xz);
                    float dens = DiskDensity(rCyl, abs(pos.y));
                    if (dens > 0.0)
                    {
                        float u = saturate((rCyl - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 1e-4));
                        float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);

                        // 궤도 운동: xz평면 접선 방향. 광선 진행 방향(vel) 대비 접근/후퇴.
                        float beta = OrbitalSpeedRel(length(pos), rs);
                        float3 tangent = normalize(cross(float3(0, 1, 0), pos));
                        float cosAng = dot(tangent, -normalize(vel));
                        float shift = DopplerFactor(beta, cosAng) * GravRedshift(length(pos), rs);

                        // 주파수 이동: 색온도(파랑/빨강)와 세기(빔ing ∝ shift⁴)에 반영.
                        float tShift = lerp(1.0, shift, _BHRedshift);
                        half3 emit = BlackBodyColorApprox(temp * tShift) * dens;
                        emit *= lerp(1.0, pow(max(shift, 0.0), 4.0), _BHBeaming);

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
