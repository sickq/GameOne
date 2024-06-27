/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using ASPUtil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    #region UNITY2021
#if UNITY_2021
    [DisallowMultipleRendererFeature("ASP Screen Space Outline")]
    public class ASPScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        private Material material;
        public bool UseHalfScale;
        private ASPScreenSpaceOutlinePass m_asplLightShaftPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_asplLightShaftPass = new ASPScreenSpaceOutlinePass(name, UseHalfScale);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/Outline");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }
                return;
            }

            m_asplLightShaftPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_asplLightShaftPass.ConfigureInput(requirements);
            m_asplLightShaftPass.SetupMembers(material);

            renderer.EnqueuePass(m_asplLightShaftPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_asplLightShaftPass.Dispose();
        }

        public class ASPScreenSpaceOutlinePass : ScriptableRenderPass
        {
            private Material m_outlineEffectMaterial;
            private RenderTexture m_copiedColor;
            private RenderTexture m_outlineInfoRT;
            
            private ASP.ASPSreenSpaceOutline m_screenSpaceOutlineSetting;
            private bool m_UseHalfScale;
            public ASPScreenSpaceOutlinePass(string passName, bool useHalfScale)
            {
                profilingSampler = new ProfilingSampler(passName);
                m_UseHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material)
            {
                m_outlineEffectMaterial = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                m_screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                
                if (m_copiedColor == null)
                {
                    m_copiedColor = RenderTexture.GetTemporary(desc);
                }
                
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;

                if (m_outlineInfoRT == null)
                {
                    m_outlineInfoRT = RenderTexture.GetTemporary(desc);
                }

                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
                m_outlineInfoRT?.Release();
            }
            
            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }
            
            private void SetKeyword(Material material, string keyword, bool state)
            {
                //UnityEngine.Debug.Log(keyword + " = "+state);
                if (state)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_outlineEffectMaterial == null)
                    return;
                if(!m_screenSpaceOutlineSetting.IsActive())
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP Screen Space Outline Pass")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blit(cmd, cameraData.renderer.cameraColorTarget, m_copiedColor);
                    
                    //fetch color mask
                    CoreUtils.SetRenderTarget(cmd, m_outlineInfoRT);
                    
                    SetKeyword(m_outlineEffectMaterial, "_IS_DEBUG_MODE", m_screenSpaceOutlineSetting.EnableDebugMode.value);
                    m_outlineEffectMaterial.SetColor("_DebugBackgroundColor", m_screenSpaceOutlineSetting.DebugBackground.value);
                    m_outlineEffectMaterial.SetFloat("_DebugEdgeType", (float)((int)m_screenSpaceOutlineSetting.ScreenSpaceOutlineDebugMode.value));
                    
                    m_outlineEffectMaterial.SetFloat("_OutlineWidth", m_screenSpaceOutlineSetting.OutlineWidth.value);
                    m_outlineEffectMaterial.SetFloat("_OuterLineToggle", m_screenSpaceOutlineSetting.EnableOuterline.value ? 1f : 0f);
                    m_outlineEffectMaterial.SetFloat("_MaterialThreshold", m_screenSpaceOutlineSetting.MaterialEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_MaterialBias", (float)m_screenSpaceOutlineSetting.MaterialEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_MaterialWeight", m_screenSpaceOutlineSetting.MaterialEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableMaterialEdge.value));
                    
                    m_outlineEffectMaterial.SetFloat("_LumaThreshold", m_screenSpaceOutlineSetting.AlbedoEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_LumaBias", (float)m_screenSpaceOutlineSetting.AlbedoEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_LumaWeight", m_screenSpaceOutlineSetting.AlbedoEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableAlbedoEdge.value));

                    m_outlineEffectMaterial.SetFloat("_DepthThreshold", m_screenSpaceOutlineSetting.DepthEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_DepthBias", (float)m_screenSpaceOutlineSetting.DepthEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_DepthWeight", m_screenSpaceOutlineSetting.DepthEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableDepthEdge.value));

                    m_outlineEffectMaterial.SetFloat("_NormalsThreshold", m_screenSpaceOutlineSetting.NormalsEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_NormalsBias", (float)m_screenSpaceOutlineSetting.NormalsEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_NormalWeight", m_screenSpaceOutlineSetting.NormalsEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableNormalsEdge.value));
                    
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "MATERIAL_EDGE", m_screenSpaceOutlineSetting.EnableMaterialEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "LUMA_EDGE", m_screenSpaceOutlineSetting.EnableAlbedoEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "NORMAL_EDGE", m_screenSpaceOutlineSetting.EnableNormalsEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "DEPTH_EDGE", m_screenSpaceOutlineSetting.EnableDepthEdge.value);

                    m_outlineEffectMaterial.SetFloat("_EnableColorDistanceFade", m_screenSpaceOutlineSetting.FadingColorByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetFloat("_EnableWeightDistanceFade", m_screenSpaceOutlineSetting.FadingWeghtByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetVector("_ColorWeightFadeDistanceStartEnd", m_screenSpaceOutlineSetting.ColorWeightFadingStartEndDistance.value);
                    
                    m_outlineEffectMaterial.SetFloat("_EnableWidthDistanceFade", m_screenSpaceOutlineSetting.FadingWidthByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetVector("_WidthFadeDistanceStartEnd", m_screenSpaceOutlineSetting.WidthFadingStartEndDistance.value);

                    m_outlineEffectMaterial.SetVector("_BlitScaleBias", new Vector4(1,1,0,0));
                    DrawTriangle(cmd, m_outlineEffectMaterial, 0);
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);

                    m_outlineEffectMaterial.SetTexture("_ASPOutlineTexture", m_outlineInfoRT);
                    m_outlineEffectMaterial.SetTexture("_BaseMap", m_copiedColor);
                    m_outlineEffectMaterial.SetColor("_OutlineColor", m_screenSpaceOutlineSetting.OutlineColor.value);
                    DrawTriangle(cmd, m_outlineEffectMaterial, 1);
                }
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
 [DisallowMultipleRendererFeature("ASP Screen Space Outline")]
    public class ASPScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        private Material material;
        public bool UseHalfScale;
        private ASPScreenSpaceOutlinePass m_asplLightShaftPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_asplLightShaftPass = new ASPScreenSpaceOutlinePass(name, UseHalfScale);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/Outline");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }
                return;
            }

            m_asplLightShaftPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_asplLightShaftPass.ConfigureInput(requirements);
            m_asplLightShaftPass.SetupMembers(material);

            renderer.EnqueuePass(m_asplLightShaftPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_asplLightShaftPass.Dispose();
        }

        public class ASPScreenSpaceOutlinePass : ScriptableRenderPass
        {
            private Material m_outlineEffectMaterial;
            private RTHandle m_outlineInfoRT;
            private RTHandle m_copiedColor;
            private ASP.ASPSreenSpaceOutline m_screenSpaceOutlineSetting;
            private bool m_UseHalfScale;
            public ASPScreenSpaceOutlinePass(string passName, bool useHalfScale)
            {
                profilingSampler = new ProfilingSampler(passName);
                m_UseHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material)
            {
                m_outlineEffectMaterial = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                m_screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_copiedColor, desc, name: "_CameraColorTexture");
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_outlineInfoRT, desc, name: "_ASPOutlineTexture");
                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
                m_outlineInfoRT?.Release();
            }
            
            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }
            
            private void SetKeyword(Material material, string keyword, bool state)
            {
                //UnityEngine.Debug.Log(keyword + " = "+state);
                if (state)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if(!m_screenSpaceOutlineSetting.IsActive())
                return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP Screen Space Outline Pass")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, m_copiedColor);
                    
                    //fetch color mask
                    CoreUtils.SetRenderTarget(cmd, m_outlineInfoRT);
                    Vector2 viewportScale = m_copiedColor.useScaling ? new Vector2(m_copiedColor.rtHandleProperties.rtHandleScale.x, m_copiedColor.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                   // m_outlineEffectMaterial.SetVector("_BlitScaleBias", viewportScale);
                   // m_outlineEffectMaterial.SetTexture("_BaseMap", m_copiedColor);
                    
                    SetKeyword(m_outlineEffectMaterial, "_IS_DEBUG_MODE", m_screenSpaceOutlineSetting.EnableDebugMode.value);
                    m_outlineEffectMaterial.SetColor("_DebugBackgroundColor", m_screenSpaceOutlineSetting.DebugBackground.value);
                    m_outlineEffectMaterial.SetFloat("_DebugEdgeType", (float)((int)m_screenSpaceOutlineSetting.ScreenSpaceOutlineDebugMode.value));
                    
                    m_outlineEffectMaterial.SetFloat("_OutlineWidth", m_screenSpaceOutlineSetting.OutlineWidth.value);
                    m_outlineEffectMaterial.SetFloat("_OuterLineToggle", m_screenSpaceOutlineSetting.EnableOuterline.value ? 1f : 0f);
                    m_outlineEffectMaterial.SetFloat("_MaterialThreshold", m_screenSpaceOutlineSetting.MaterialEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_MaterialBias", (float)m_screenSpaceOutlineSetting.MaterialEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_MaterialWeight", m_screenSpaceOutlineSetting.MaterialEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableMaterialEdge.value));
                    
                    m_outlineEffectMaterial.SetFloat("_LumaThreshold", m_screenSpaceOutlineSetting.AlbedoEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_LumaBias", (float)m_screenSpaceOutlineSetting.AlbedoEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_LumaWeight", m_screenSpaceOutlineSetting.AlbedoEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableAlbedoEdge.value));

                    m_outlineEffectMaterial.SetFloat("_DepthThreshold", m_screenSpaceOutlineSetting.DepthEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_DepthBias", (float)m_screenSpaceOutlineSetting.DepthEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_DepthWeight", m_screenSpaceOutlineSetting.DepthEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableDepthEdge.value));

                    m_outlineEffectMaterial.SetFloat("_NormalsThreshold", m_screenSpaceOutlineSetting.NormalsEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_NormalsBias", (float)m_screenSpaceOutlineSetting.NormalsEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_NormalWeight", m_screenSpaceOutlineSetting.NormalsEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableNormalsEdge.value));
                    
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "MATERIAL_EDGE", m_screenSpaceOutlineSetting.EnableMaterialEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "LUMA_EDGE", m_screenSpaceOutlineSetting.EnableAlbedoEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "NORMAL_EDGE", m_screenSpaceOutlineSetting.EnableNormalsEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "DEPTH_EDGE", m_screenSpaceOutlineSetting.EnableDepthEdge.value);
                    
                    m_outlineEffectMaterial.SetFloat("_EnableColorDistanceFade", m_screenSpaceOutlineSetting.FadingColorByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetFloat("_EnableWeightDistanceFade", m_screenSpaceOutlineSetting.FadingWeghtByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetVector("_ColorWeightFadeDistanceStartEnd", m_screenSpaceOutlineSetting.ColorWeightFadingStartEndDistance.value);
                    
                    m_outlineEffectMaterial.SetFloat("_EnableWidthDistanceFade", m_screenSpaceOutlineSetting.FadingWidthByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetVector("_WidthFadeDistanceStartEnd", m_screenSpaceOutlineSetting.WidthFadingStartEndDistance.value);
                    
                    DrawTriangle(cmd, m_outlineEffectMaterial, 0);
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "_APPLY_FXAA", m_screenSpaceOutlineSetting.ApplyFXAA.value);
                    viewportScale = m_outlineInfoRT.useScaling ? new Vector2(m_outlineInfoRT.rtHandleProperties.rtHandleScale.x, m_outlineInfoRT.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                    m_outlineEffectMaterial.SetVector("_BlitScaleBias", viewportScale);
                    m_outlineEffectMaterial.SetTexture("_ASPOutlineTexture", m_outlineInfoRT);
                    m_outlineEffectMaterial.SetTexture("_BaseMap", m_copiedColor);
                    m_outlineEffectMaterial.SetColor("_OutlineColor", m_screenSpaceOutlineSetting.OutlineColor.value);
                    
                    DrawTriangle(cmd, m_outlineEffectMaterial, 1);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                CommandBufferPool.Release(cmd);
            }
        }
    }
#endif
    #endregion
}