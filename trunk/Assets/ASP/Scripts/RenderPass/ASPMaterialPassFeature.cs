/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ASP
{
    #region UNITY2021
#if UNITY_2021
    [DisallowMultipleRendererFeature("ASP Material Pass")]
    public class ASPMaterialPassFeature : ScriptableRendererFeature
    {
        //[RenderingLayerMask]
        private int m_renderingLayerMask;

        public RenderQueueRange Range = RenderQueueRange.opaque;
        private RenderPassEvent Event = RenderPassEvent.BeforeRenderingOpaques;
        private string MaterialPassShaderTag = "ASPMaterialPass";
        private ASPMaterialPass m_aspMaterialPass;
        public override void Create()
        {
            m_aspMaterialPass = new ASPMaterialPass(name, MaterialPassShaderTag, Event, Range, (uint)m_renderingLayerMask, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_aspMaterialPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(m_aspMaterialPass);
            m_aspMaterialPass.Setup();
        }
        
        protected override void Dispose(bool disposing)
        {
            m_aspMaterialPass.Dispose();
        }

        public class ASPMaterialPass : ScriptableRenderPass
        {
            private RenderTargetHandle m_materialTextureHandle;
            private RenderTargetHandle m_depthTextureHandle;
            private RenderTextureDescriptor m_depthTargetDesc;
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private  ShaderTagId m_shaderTagId;
            private string m_profilerTag;

            public void Setup()
            {
            }

            public ASPMaterialPass(string profilerTag, string shaderTagId, RenderPassEvent evt, RenderQueueRange renderQueueRange, uint renderingLayerMask, StencilState stencilState, int stencilReference)
            {
                m_profilerTag = profilerTag;
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                //m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
                
                m_materialTextureHandle.Init("_ASPMaterialTexture");
                m_depthTextureHandle.Init("_ASPMaterialDepthTexture");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.ARGB32;
                
                cmd.GetTemporaryRT(m_materialTextureHandle.id, desc);

                m_depthTargetDesc = desc;
                m_depthTargetDesc.width = desc.width;
                m_depthTargetDesc.height = desc.height;
                m_depthTargetDesc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                // Never have MSAA on this depth texture. When doing MSAA depth priming this is the texture that is resolved to and used for post-processing.
                m_depthTargetDesc.msaaSamples = 1;// Depth-Only pass don't use MSAA
                
              //  m_depthTargetDesc.graphicsFormat = GraphicsFormat.None;
                m_depthTargetDesc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                cmd.GetTemporaryRT(m_depthTextureHandle.id, m_depthTargetDesc);
                
                ConfigureTarget(m_materialTextureHandle.Identifier(), m_depthTextureHandle.Identifier());
                ConfigureClear(ClearFlag.All, Color.black);
            }


            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(m_materialTextureHandle.id);

                cmd.ReleaseTemporaryRT(m_depthTextureHandle.id);
            }
            
            
            public void Dispose()
            {
               
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("ASP Material Pass")))
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
                cmd.SetGlobalTexture("_ASPMaterialTexture", m_materialTextureHandle.Identifier());
                cmd.SetGlobalTexture("_ASPMaterialDepthTexture", m_depthTextureHandle.Identifier());
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
    #endif
    #endregion
    
    #region UNITY2022
#if UNITY_2022_1_OR_NEWER 
[DisallowMultipleRendererFeature("ASP Material Pass")]
    public class ASPMaterialPassFeature : ScriptableRendererFeature
    {
        //un-comment below line to use rendering layer mask to filter out objects
        //[RenderingLayerMask]
        private int m_renderingLayerMask;

        public RenderQueueRange Range = RenderQueueRange.opaque;
        private RenderPassEvent Event = RenderPassEvent.BeforeRenderingOpaques;
        private string MaterialPassShaderTag = "ASPMaterialPass";
        private ASPMaterialPass m_aspMaterialPass;
        public override void Create()
        {
            m_aspMaterialPass = new ASPMaterialPass(name, MaterialPassShaderTag, Event, Range, (uint)m_renderingLayerMask, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_aspMaterialPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(m_aspMaterialPass);
            m_aspMaterialPass.Setup();
        }
        
        protected override void Dispose(bool disposing)
        {
            m_aspMaterialPass.Dispose();
        }

        public class ASPMaterialPass : ScriptableRenderPass
        {
            private RTHandle m_materialPassTarget;
            private RTHandle m_detphTarget;
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private  ShaderTagId m_shaderTagId;
            private string m_profilerTag;

            public void Setup()
            {
            }

            public ASPMaterialPass(string profilerTag, string shaderTagId, RenderPassEvent evt, RenderQueueRange renderQueueRange, uint renderingLayerMask, StencilState stencilState, int stencilReference)
            {
                m_profilerTag = profilerTag;
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                //un-comment below line to use rendering layer mask to filter out objects
                //m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.ARGB32;
                
                RenderingUtils.ReAllocateIfNeeded(ref m_materialPassTarget,desc,  name: "_ASPMaterialTexture");
                //desc.graphicsFormat = GraphicsFormat.D24_UNorm;
                
                desc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                RenderingUtils.ReAllocateIfNeeded(ref m_detphTarget, desc);

                
                ConfigureTarget(m_materialPassTarget, m_detphTarget);
                ConfigureClear(ClearFlag.All, Color.black);
            }

            public void Dispose()
            {
                m_materialPassTarget?.Release();
                m_detphTarget?.Release();
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("ASP Material Pass")))
                {
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
                cmd.SetGlobalTexture("_ASPMaterialTexture", m_materialPassTarget);
                cmd.SetGlobalTexture("_ASPMaterialDepthTexture", m_detphTarget);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
#endif
    #endregion
}