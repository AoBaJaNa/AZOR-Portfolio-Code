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
            new ShaderTagId("SRPDefaultUnlit"), // �Ϲ����� Unlit ��ƼŬ��
            new ShaderTagId("Universal2D")      // Ȥ�� �𸣴� �߰�
        };

        public HitParticlePass()
        {
            int layerMask = LayerMask.GetMask("HitParticle");
            filteringSettings = new FilteringSettings(RenderQueueRange.transparent, layerMask);

            // Depth ���� �߰�
            renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);
            renderStateBlock.depthState = new DepthState(false, CompareFunction.LessEqual);
            // ZTest�� �ϵ�(LessEqual), ZWrite�� ���δ� �� ���� ����Ʈ�� �⺻�Դϴ�.

            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void Execute(
     ScriptableRenderContext context,
     ref RenderingData renderingData)
        {
            var drawingSettings = CreateDrawingSettings(shaderTagIds, ref renderingData, SortingCriteria.CommonTransparent);
            drawingSettings.perObjectData = PerObjectData.None; // Shared materials determine batching compatibility.
            // UniversalForward �±� �ϳ��� ��� (Forward ������ ����)
/*            var drawingSettings = CreateDrawingSettings(
                shaderTagIds,        // �� List<ShaderTagId> �����ε� ���
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
