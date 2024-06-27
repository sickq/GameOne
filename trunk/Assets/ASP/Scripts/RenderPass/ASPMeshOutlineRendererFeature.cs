/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ASP
{
    [DisallowMultipleRendererFeature("ASP Mesh Outline")]
    public class ASPMeshOutlineRendererFeature : ScriptableRendererFeature
    {
        [SingleLayerMask]
        public int m_layer;
        [RenderingLayerMask]
        public int m_renderingLayerMask;
        public RenderPassEvent InjectPoint = RenderPassEvent.AfterRenderingOpaques;
        private RenderQueueRange Range = RenderQueueRange.opaque;
        [FormerlySerializedAs("InjectPassLightModeTag")] private string lightModeTag = "ASPOutlineObject";
        private MeshOutlinePass m_meshOutlinePass;
        public override void Create()
        {
            m_meshOutlinePass = new MeshOutlinePass(name, lightModeTag, InjectPoint, Range, (uint)m_renderingLayerMask, 1 << m_layer, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_meshOutlinePass);
        }

        public class MeshOutlinePass : ScriptableRenderPass
        {
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private  ShaderTagId m_shaderTagId;
            private string m_profilerTag;

            public MeshOutlinePass(string profilerTag, string shaderTagId, RenderPassEvent evt, RenderQueueRange renderQueueRange, uint renderingLayerMask, int layerMask, StencilState stencilState, int stencilReference)
            {
                m_profilerTag = profilerTag;
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                m_filteringSettings.layerMask = layerMask;
                m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
            }
            
            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("Mesh Outline Pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = SortingCriteria.CommonOpaque;
                    var sortingSettings = new SortingSettings(renderingData.cameraData.camera);
                    sortingSettings.criteria = sortFlags;
                    var drawSettings = new DrawingSettings(m_shaderTagId, sortingSettings);
                    drawSettings.perObjectData = PerObjectData.None;
                    
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                        ref m_filteringSettings, ref m_renderStateBlock);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
}