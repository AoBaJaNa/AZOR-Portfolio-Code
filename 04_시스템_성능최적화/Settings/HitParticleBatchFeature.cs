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
            new ShaderTagId("SRPDefaultUnlit"), // 일반적인 Unlit 파티클용
            new ShaderTagId("Universal2D")      // 혹시 모르니 추가
        };

        public HitParticlePass()
        {
            int layerMask = LayerMask.GetMask("HitParticle");
            filteringSettings = new FilteringSettings(RenderQueueRange.transparent, layerMask);

            // Depth 설정 추가
            renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);
            renderStateBlock.depthState = new DepthState(false, CompareFunction.LessEqual);
            // ZTest는 하되(LessEqual), ZWrite는 꺼두는 게 투명 이펙트의 기본입니다.

            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void Execute(
     ScriptableRenderContext context,
     ref RenderingData renderingData)
        {
            var drawingSettings = CreateDrawingSettings(shaderTagIds, ref renderingData, SortingCriteria.CommonTransparent);
            drawingSettings.perObjectData = PerObjectData.None; // Shared materials determine batching compatibility.
            // UniversalForward 태그 하나면 충분 (Forward 렌더러 기준)
/*            var drawingSettings = CreateDrawingSettings(
                shaderTagIds,        // ← List<ShaderTagId> 오버로드 사용
                ref renderingData,
                SortingCriteria.None
            );*/

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