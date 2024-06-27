using ASPUtil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    #region UNITY2021

#if UNITY_2021
    [DisallowMultipleRendererFeature("ASP ToneMapping")]
    public class ASPToneMappingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material material;
        private ASPToneMappingPass m_aspToneMapPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_aspToneMapPass = new ASPToneMappingPass(name);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/ToneMapping");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }

                return;
            }

            m_aspToneMapPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_aspToneMapPass.ConfigureInput(ScriptableRenderPassInput.None);
            m_aspToneMapPass.SetupMembers(material);

            renderer.EnqueuePass(m_aspToneMapPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_aspToneMapPass.Dispose();
        }

        public class ASPToneMappingPass : ScriptableRenderPass
        {
            private Material m_toneMapMaterial;
            private RenderTexture m_copiedColor;
            private ASPToneMap m_toneMapComponent;

            public ASPToneMappingPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material material)
            {
                m_toneMapMaterial = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                if (m_copiedColor == null)
                {
                    m_copiedColor = RenderTexture.GetTemporary(desc);
                }

                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);

                m_toneMapComponent = VolumeManager.instance.stack.GetComponent<ASPToneMap>();
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
            }


            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_toneMapMaterial == null)
                    return;
                if (m_toneMapComponent == null)
                {
                    Debug.LogWarning("need to enable asp tonemaping inside the camera volume component as well.");
                    return;
                }

                if (!m_toneMapComponent.IsActive())
                    return;
                ref var postProcessingData = ref renderingData.postProcessingData;
                bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
                int lutHeight = postProcessingData.lutSize;
                int lutWidth = lutHeight * lutHeight;
                m_toneMapMaterial.SetVector("_Lut_Params",
                    new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f, 0));

                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP ToneMap Pass")))
                {
                    //fetch current camera Color to copiedColor RT
                    Blit(cmd, cameraData.renderer.cameraColorTarget, m_copiedColor);

                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);

                    m_toneMapMaterial.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    m_toneMapMaterial.SetTexture("_MainTex", m_copiedColor);
                    m_toneMapMaterial.SetTexture("_BaseMap", m_copiedColor);
                    m_toneMapMaterial.SetFloat("_ToneMapLowerBound",
                        (m_toneMapComponent.CharacterPixelsToneMapStrength.value));
                    m_toneMapMaterial.SetFloat("_Exposure", m_toneMapComponent.FilmicExposure.value);
                    m_toneMapMaterial.SetFloat("_IgnoreCharacterPixels",
                        m_toneMapComponent.IgnoreCharacterPixels.value ? 1.0f : 0);
                    DrawTriangle(cmd, m_toneMapMaterial, (int)m_toneMapComponent.ToneMapType.value);
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
        [DisallowMultipleRendererFeature("ASP ToneMapping")]
    public class ASPToneMappingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material material;
        private ASPToneMappingPass m_aspToneMapPass;
        /// <inheritdoc/>
        public override void Create()
        {
            m_aspToneMapPass = new ASPToneMappingPass(name);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/ToneMapping");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }
                return;
            }

            m_aspToneMapPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_aspToneMapPass.ConfigureInput(ScriptableRenderPassInput.None);
            m_aspToneMapPass.SetupMembers(material);

            renderer.EnqueuePass(m_aspToneMapPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_aspToneMapPass.Dispose();
        }

        public class ASPToneMappingPass : ScriptableRenderPass
        {
            private Material m_toneMapMaterial;
            private RTHandle m_copiedColor;
            private ASPToneMap m_toneMapComponent;
            
            public ASPToneMappingPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material material)
            {
                m_toneMapMaterial = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_copiedColor, desc, name: "_CameraColorTexture");
                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);

                m_toneMapComponent = VolumeManager.instance.stack.GetComponent<ASPToneMap>();
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
            }
            
        
            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                
                if (m_toneMapMaterial == null)
                    return;
                if (m_toneMapComponent == null)
                {
                    Debug.LogWarning("need to enable asp tonemaping inside the camera volume component as well.");
                    return;
                }
                if(!m_toneMapComponent.IsActive())
                    return;
                ref var postProcessingData = ref renderingData.postProcessingData;
                bool hdr = postProcessingData.gradingMode == ColorGradingMode.HighDynamicRange;
                int lutHeight = postProcessingData.lutSize;
                int lutWidth = lutHeight * lutHeight;
                m_toneMapMaterial.SetVector("_Lut_Params", new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f, 0));
                
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP ToneMap Pass")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, m_copiedColor);
                  
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    var viewportScale = m_copiedColor.useScaling ? new Vector2(m_copiedColor.rtHandleProperties.rtHandleScale.x, m_copiedColor.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                    m_toneMapMaterial.SetVector("_BlitScaleBias", viewportScale);
                    m_toneMapMaterial.SetTexture("_BaseMap", m_copiedColor);
                    m_toneMapMaterial.SetFloat("_ToneMapLowerBound", (m_toneMapComponent.CharacterPixelsToneMapStrength.value));
                    m_toneMapMaterial.SetFloat("_Exposure", m_toneMapComponent.FilmicExposure.value);
                    m_toneMapMaterial.SetFloat("_IgnoreCharacterPixels", m_toneMapComponent.IgnoreCharacterPixels.value ? 1.0f : 0);
                    DrawTriangle(cmd, m_toneMapMaterial, (int)m_toneMapComponent.ToneMapType.value);
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