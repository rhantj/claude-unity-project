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

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, Vector2.one, data.material, 0);
                    });
                }
            }
        }
    }
}
