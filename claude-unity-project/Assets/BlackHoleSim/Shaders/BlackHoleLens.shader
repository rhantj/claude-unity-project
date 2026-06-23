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
            float _BHDiskInner;
            float _BHDiskOuter;
            float _BHDiskTempInner;
            float _BHDiskTempOuter;
            float _BHDopplerStrength;

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

            // BlackBodyColor.Evaluate(BlackHoleSim/Runtime/BlackBodyColor.cs)의 HLSL 미러.
            half3 BlackBodyColorApprox(float tempKelvin)
            {
                float t = clamp(tempKelvin, 1000.0, 40000.0) / 100.0;
                float r = t <= 66.0
                    ? 255.0
                    : clamp(329.698727446 * pow(t - 60.0, -0.1332047592), 0.0, 255.0);
                float g = t <= 66.0
                    ? clamp(99.4708025861 * log(t) - 161.1195681661, 0.0, 255.0)
                    : clamp(288.1221695283 * pow(t - 60.0, -0.0755148492), 0.0, 255.0);
                float b = t >= 66.0
                    ? 255.0
                    : (t <= 19.0 ? 0.0 : clamp(138.5177312231 * log(t - 10.0) - 305.0447927307, 0.0, 255.0));
                return half3(r, g, b) / 255.0;
            }

            half3 DiskColorAt(float3 hitPoint, float3 marchDir)
            {
                float radius = length(hitPoint.xz - _BHWorldPos.xz);
                float u = saturate((radius - _BHDiskInner) / max(_BHDiskOuter - _BHDiskInner, 0.0001));
                float temp = lerp(_BHDiskTempInner, _BHDiskTempOuter, u);
                half3 baseColor = BlackBodyColorApprox(temp);

                float3 tangent = normalize(cross(float3(0, 1, 0), hitPoint - _BHWorldPos));
                float approach = dot(tangent, -marchDir);
                float doppler = 1.0 + _BHDopplerStrength * approach;

                return baseColor * max(doppler, 0.0);
            }

            half3 March(float3 origin, float3 dir)
            {
                float3 p = origin;
                float3 d = dir;
                float prevY = p.y - _BHWorldPos.y;

                for (int i = 0; i < _BHStepCount; i++)
                {
                    float3 toCenter = _BHWorldPos - p;
                    if (dot(toCenter, toCenter) <= _BHHorizonRadius * _BHHorizonRadius)
                        return half3(0, 0, 0);

                    d += AccelerationAt(_BHWorldPos, _BHLensMu, _BHSoftening, p) * _BHStepSize;
                    float3 next = p + normalize(d) * _BHStepSize;
                    float nextY = next.y - _BHWorldPos.y;

                    if (prevY * nextY < 0.0)
                    {
                        float tCross = prevY / (prevY - nextY);
                        float3 hit = lerp(p, next, tCross);
                        float radius = length(hit.xz - _BHWorldPos.xz);
                        if (radius >= _BHDiskInner && radius <= _BHDiskOuter)
                            return DiskColorAt(hit, normalize(d));
                    }

                    p = next;
                    prevY = nextY;
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
