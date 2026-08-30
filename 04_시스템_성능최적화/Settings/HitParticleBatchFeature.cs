using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class HitParticleBatchFeature : ScriptableRendererFeature
{

    class HitParticlePass : ScriptableRenderPass
    {
        private FilteringSettings filteringSettings;
        private RenderStateBlock renderStateBlock;
        private static readonly List<ShaderTagId> shaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("Universal2D")
        };

        public HitParticlePass()
        {
            int layerMask = LayerMask.GetMask("HitParticle");
            filteringSettings = new FilteringSettings(RenderQueueRange.transparent, layerMask);

            renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);
            renderStateBlock.depthState = new DepthState(false, CompareFunction.LessEqual);
            // 투명 파티클은 깊이를 검사하되 깊이 버퍼에는 기록하지 않는다.

            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void Execute(
     ScriptableRenderContext context,
     ref RenderingData renderingData)
        {
            var drawingSettings = CreateDrawingSettings(shaderTagIds, ref renderingData, SortingCriteria.CommonTransparent);
            drawingSettings.perObjectData = PerObjectData.None;

            context.DrawRenderers(
                renderingData.cullResults,
                ref drawingSettings,
                ref filteringSettings,
                ref renderStateBlock
            );
        }
    }

    HitParticlePass pass;

    public override void Create()
    {
        pass = new HitParticlePass();
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}
