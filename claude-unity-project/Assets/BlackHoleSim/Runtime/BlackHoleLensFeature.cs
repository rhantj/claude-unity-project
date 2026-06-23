using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BlackHoleSim
{
    /// <summary>풀스크린 배경 패스: 레이마칭 렌즈를 BeforeRenderingOpaques에 그려, 1단계 씬 오브젝트가 그 위에 그려지게 한다.</summary>
    public class BlackHoleLensFeature : ScriptableRendererFeature
    {
        [SerializeField] public Material lensMaterial;

        BlackHoleLensPass pass;

        public override void Create()
        {
            pass = new BlackHoleLensPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (lensMaterial == null) return;
            pass.SetMaterial(lensMaterial);
            renderer.EnqueuePass(pass);
        }

        class BlackHoleLensPass : ScriptableRenderPass
        {
            Material material;
            const string PassName = "BlackHoleLens";

            public void SetMaterial(Material mat) => material = mat;

            class PassData
            {
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData))
                {
                    passData.material = material;
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.AllowPassCulling(false);

                    // 풀스크린 삼각형 직접 드로우. Blit.hlsl/Blitter 경로는 이 URP RenderGraph 셋업에서 프래그먼트를 출력하지 않아 사용하지 않는다.
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
