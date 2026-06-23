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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(0.1, 0.6, 0.5, 1.0);
            }
            ENDHLSL
        }
    }
}
