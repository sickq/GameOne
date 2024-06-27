/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;
using ShadowResolution = UnityEngine.ShadowResolution;

namespace ASP
{
    public struct ASPShadowData
    {
        public float shadowDistance;
        public int mainLightShadowCascadesCount;
        public int mainLightShadowmapWidth;
        public int mainLightShadowmapHeight;
        public Vector3 builtInCascadeSplit;
        public float[] cascadeSplitArray;
        public float mainLightShadowCascadeBorder;
    }
    
    public enum CharacterShadowMapResolution
    {
        SIZE_1024 = 1024,
        SIZE_2048 = 2048,
        SIZE_4096 = 4096,
    }
    
    #region UNITY2021
            #if UNITY_2021
    public class ASPShadowMapFeature : ScriptableRendererFeature
    {
        private static class ASPMainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;
            public static int _ShadowParams;
            public static int _CascadeCount;
            public static int _CascadeShadowSplitSpheres0;
            public static int _CascadeShadowSplitSpheres1;
            public static int _CascadeShadowSplitSpheres2;
            public static int _CascadeShadowSplitSpheres3;
            public static int _CascadeShadowSplitSphereRadii;
            public static int _ShadowOffset0;
            public static int _ShadowOffset1;
            public static int _ShadowmapSize;
        }

        [Tooltip("Expensive, but can prevent shadow missing when object outside camera view")]
        private bool PerformExtraCull = true;
        public RenderQueueRange m_renderQueueRange = RenderQueueRange.all;
        private LayerMask m_layerMask = -1;
        private string m_CustomBufferName = "_ASPShadowMap";
        [SerializeField]
        private CharacterShadowMapResolution m_characterShadowMapResolution = CharacterShadowMapResolution.SIZE_2048;
        
        public float ClipDistance = 50;
        [Range(1,4)]
        public int CascadeCount = 1;
        /// Main light last cascade shadow fade border.
        /// Value represents the width of shadow fade that ranges from 0 to 1.
        /// Where value 0 is used for no shadow fade.
        ///
        [FormerlySerializedAs("LastBorder")]
        [Range(0, 1)]
        [Tooltip("Shadow fade out ratio on last cascade, set to 0 means no fading")]
        public float ShadowFadeRatio = 0.2f;
        //[RenderingLayerMask]
        private int m_renderingLayerMask = -1;
        private ASPShadowRenderPass _scriptablePass;

        /// <inheritdoc/>
        public override void Create()
        {
            var shadowData = SetupCascsadesData();
            _scriptablePass = new ASPShadowRenderPass((uint)m_renderingLayerMask, m_CustomBufferName,
                RenderPassEvent.AfterRenderingShadows, m_renderQueueRange, m_layerMask, shadowData);
            if (!isActive)
            {
                _scriptablePass.IsNotActive = true;
                _scriptablePass.DrawEmptyShadowMap();
            }
            else
            {
                _scriptablePass.IsNotActive = false;
            }
        }
        
        private ASPShadowData SetupCascsadesData()
        {
            // On GLES2 we strip the cascade keywords from the lighting shaders, so for consistency we force disable the cascades here too
            var shadowData = new ASPShadowData();
            var shadoweCascadeCount = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ? 1 : CascadeCount;
            shadowData.mainLightShadowCascadesCount = shadoweCascadeCount;
            shadowData.mainLightShadowmapWidth = (int)m_characterShadowMapResolution;
            shadowData.mainLightShadowmapHeight = (int)m_characterShadowMapResolution;
            shadowData.cascadeSplitArray = new float[4];
            shadowData.builtInCascadeSplit = new Vector3(1, 0, 0);
            shadowData.shadowDistance = ClipDistance;
            switch (shadoweCascadeCount)
            {
                
                case 1:
                    shadowData.builtInCascadeSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    shadowData.cascadeSplitArray = new float[]{1.0f, 0, 0};
                    break;

                case 2:
                    shadowData.builtInCascadeSplit = new Vector3(0.4f, 0.0f, 0.0f);
                    shadowData.cascadeSplitArray = new float[]{0.4f, 1.0f, 0.0f};
                    break;

                case 3:
                    shadowData.builtInCascadeSplit = new Vector3(0.1f, 0.3f, 0.0f);
                    shadowData.cascadeSplitArray = new float[]{0.1f, 0.3f, 1.0f};
                    break;

                default:
                    shadowData.builtInCascadeSplit = new Vector3(0.067f, 0.2f, 0.467f);
                    shadowData.cascadeSplitArray = new float[]{0.067f, 0.2f, 0.467f, 1.0f};
                    break;
            }   
            shadowData.mainLightShadowCascadeBorder = ShadowFadeRatio;
            
            return shadowData;
        }

        protected override void Dispose(bool disposing)
        {
            _scriptablePass.Dispose();
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_scriptablePass != null)
            {
                _scriptablePass.PerformExtraCull = PerformExtraCull;
            }

            renderer.EnqueuePass(_scriptablePass);
        }

        class ASPShadowRenderPass : ScriptableRenderPass
        {
            public bool PerformExtraCull;
            public bool IsNotActive;
            private RenderTexture _ShadowMapTexture;
            private RenderTexture _EmptyLightShadowmapTexture;

            private FilteringSettings _FilteringSettings;
            private RenderStateBlock _RenderStateBlock;
            private ShaderTagId _ShaderTagId = new ShaderTagId("ASPShadowCaster");
            private string _CustomBufferName;

            private ASPShadowData aspShadowData;
            private Matrix4x4[] _CustomWorldToShadowMatrices;
            private bool _IsEmptyShdaowMap;
            private List<Plane[]> _CascadeCullPlanes;
            private Matrix4x4[] _LightViewMatrices;
            private Matrix4x4[] _LightProjectionMatrices;
            private ShadowSliceData[] _ShadowSliceDatas;
            private ScriptableCullingParameters _CullingParameters = new ScriptableCullingParameters();
            Vector4[] _CascadeSplitDistances;
            int _ShadowCasterCascadesCount;
            
            const int k_ShadowmapBufferBits = 16;

            public void DrawEmptyShadowMap()
            {
                if (_EmptyLightShadowmapTexture == null)
                {
                    _EmptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
                }

                Shader.SetGlobalTexture(_CustomBufferName, _EmptyLightShadowmapTexture);
            }

            void SetupForEmptyRendering()
            {
                //Debug.Log("SetupForEmptyRendering");
                _IsEmptyShdaowMap = true;
                _EmptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
            }

            public ASPShadowRenderPass(uint renderingLayerMask, string customBufferName,
                RenderPassEvent passEvent, RenderQueueRange queueRange, LayerMask layerMask, ASPShadowData ShadowData)
            {
                profilingSampler = new ProfilingSampler("ASP Shadow Render Pass");
                ClearData();
                _CustomWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < _CustomWorldToShadowMatrices.Length; i++)
                {
                    _CustomWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                _CustomBufferName = customBufferName;
                renderPassEvent = passEvent;
                _FilteringSettings = new FilteringSettings(RenderQueueRange.all);

                //un-comment below line to use rendering layer mask to filter out objects
                //_FilteringSettings.renderingLayerMask = renderingLayerMask;

                _RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                
                aspShadowData = ShadowData;
                _CascadeSplitDistances = new Vector4[4];
                ASPMainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_ASPMainLightWorldToShadow");
                ASPMainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_ASPMainLightShadowParams");
                ASPMainLightShadowConstantBuffer._CascadeCount = Shader.PropertyToID("_ASPCascadeCount");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres0");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres1");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres2");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres3");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_ASPCascadeShadowSplitSphereRadii");
                ASPMainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_ASPMainLightShadowOffset0");
                ASPMainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_ASPMainLightShadowOffset1");
                ASPMainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_ASPMainLightShadowmapSize");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _ShadowCasterCascadesCount = aspShadowData.mainLightShadowCascadesCount;
                var renderTargetWidth = aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_ShadowCasterCascadesCount == 2)
                    ? aspShadowData.mainLightShadowmapHeight >> 1
                    : aspShadowData.mainLightShadowmapHeight;

                var cascadeCount = _ShadowCasterCascadesCount;
                int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                    aspShadowData.mainLightShadowmapWidth,
                    aspShadowData.mainLightShadowmapHeight, cascadeCount);

                int shadowLightIndex = renderingData.lightData.mainLightIndex;

                if (IsNotActive)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!renderingData.shadowData.supportsMainLightShadows)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (shadowLightIndex == -1)
                {
                    SetupForEmptyRendering();
                    return;
                }
                
                VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if (shadowLight.lightType != LightType.Directional)
                {
                    SetupForEmptyRendering();
                     return;
                }

                if (light.shadows == LightShadows.None)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!_IsEmptyShdaowMap)
                {
                    for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                    {
                        _ShadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                        var planes = _CascadeCullPlanes[cascadeIndex];
                        
                        bool success = ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(ref renderingData.cameraData, ref aspShadowData, 
                            cascadeIndex, shadowLight.light, shadowResolution, aspShadowData.cascadeSplitArray, out Vector4 cullingSphere, out 
                            Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);
                        
                        _LightViewMatrices[cascadeIndex] = viewMatrix;
                        _LightProjectionMatrices[cascadeIndex] = projMatrix;
                        _CascadeCullPlanes[cascadeIndex] = planes;
                        _CascadeSplitDistances[cascadeIndex] = cullingSphere;
                        
                        if (!success)
                        {
                            SetupForEmptyRendering();
                            ConfigureTarget(_EmptyLightShadowmapTexture);
                            ConfigureClear(ClearFlag.Depth, Color.black);
                            return;
                        }

                        _CustomWorldToShadowMatrices[cascadeIndex] =
                            ASPShadowUtil.GetShadowTransform(_LightProjectionMatrices[cascadeIndex],
                                _LightViewMatrices[cascadeIndex]);

                        // Handle shadow slices
                        var offsetX = (cascadeIndex % 2) * shadowResolution;
                        var offsetY = (cascadeIndex / 2) * shadowResolution;

                        ASPShadowUtil.ApplySliceTransform(ref _CustomWorldToShadowMatrices[cascadeIndex], offsetX, offsetY,
                            shadowResolution,
                            renderTargetWidth, renderTargetHeight);
                        _ShadowSliceDatas[cascadeIndex].projectionMatrix = _LightProjectionMatrices[cascadeIndex];
                        _ShadowSliceDatas[cascadeIndex].viewMatrix = _LightViewMatrices[cascadeIndex];
                        _ShadowSliceDatas[cascadeIndex].offsetX = offsetX;
                        _ShadowSliceDatas[cascadeIndex].offsetY = offsetY;
                        _ShadowSliceDatas[cascadeIndex].resolution = shadowResolution;
                        _ShadowSliceDatas[cascadeIndex].shadowTransform =
                            _CustomWorldToShadowMatrices[cascadeIndex];
                        _ShadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                    }
                    _ShadowMapTexture = ShadowUtils.GetTemporaryShadowTexture(renderTargetWidth, renderTargetHeight,
                        k_ShadowmapBufferBits);
                    ConfigureTarget(_ShadowMapTexture);
                    ConfigureClear(ClearFlag.Depth, Color.black);
                }
                else
                {
                    _EmptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
                    ConfigureTarget(_EmptyLightShadowmapTexture);
                    ConfigureClear(ClearFlag.Depth, Color.black);
                }
            }
            
            private void RenderEmpty(ScriptableRenderContext context)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.SetGlobalTexture(_CustomBufferName, _EmptyLightShadowmapTexture);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

 public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_IsEmptyShdaowMap)
                {
                    _IsEmptyShdaowMap = false;
                    RenderEmpty(context);
                    return;
                }

                if (_ShadowMapTexture == null)
                {
                    _IsEmptyShdaowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }

                
                CommandBuffer cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var prevViewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;
                var prevProjMatrix = renderingData.cameraData.camera.projectionMatrix;
                var visibleLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];
                
                bool hasValidCullParam = false;
                if (PerformExtraCull && renderingData.cameraData.camera.TryGetCullingParameters(out _CullingParameters))
                {
                    hasValidCullParam = true;
                    _CullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
                    _CullingParameters.isOrthographic = true;
                }

                var cullResult = renderingData.cullResults;
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP ShadowMap Pass")))
                {
                    for (int i = 0; i < _ShadowCasterCascadesCount; i++)
                    {
                        if (PerformExtraCull && hasValidCullParam)
                        {
                            _CullingParameters.cullingMatrix = _LightProjectionMatrices[i] * _LightViewMatrices[i];
                            for (int cullPlaneIndex = 0; cullPlaneIndex < 6; cullPlaneIndex++) 
                            {
                                _CullingParameters.SetCullingPlane (cullPlaneIndex, _CascadeCullPlanes[i][cullPlaneIndex]);
                            }
                            cullResult = context.Cull(ref _CullingParameters);
                        }
                        
                        // Handle drawing 
                        var drawSettings =
                            CreateDrawingSettings(_ShaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                        drawSettings.perObjectData = PerObjectData.None;
                        
                        // Need to start by setting the Camera position as that is not set for passes executed before normal rendering
                       //  cmd.SetGlobalVector("_WorldSpaceCameraPos", renderingData.cameraData.worldSpaceCameraPos);

                        // TODO handle empty rendering
                        cmd.SetGlobalDepthBias(1.0f, 3.5f); 
                        cmd.SetViewport(new Rect(_ShadowSliceDatas[i].offsetX, _ShadowSliceDatas[i].offsetY,
                            _ShadowSliceDatas[i].resolution, _ShadowSliceDatas[i].resolution));
                        cmd.SetViewProjectionMatrices(_LightViewMatrices[i], _LightProjectionMatrices[i]);
                        Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                            renderingData.lightData.mainLightIndex, ref renderingData.shadowData,
                            _LightProjectionMatrices[i], _ShadowSliceDatas[i].resolution);
                        // ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref visibleLight, shadowBias);
                        cmd.SetGlobalVector("_ASPShadowBias", shadowBias);

                        // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
                        Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                        cmd.SetGlobalVector("_ASPLightDirection",
                            new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                        // For punctual lights, computing light direction at each vertex position provides more consistent results (shadow shape does not change when "rotating the point light" for example)
                        Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                        cmd.SetGlobalVector("_ASPLightPosition",
                            new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        context.DrawRenderers(cullResult, ref drawSettings, ref _FilteringSettings,
                            ref _RenderStateBlock);
                        cmd.DisableScissorRect();
                        cmd.SetGlobalDepthBias(0.0f, 0.0f); 
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }
                
                cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
/*
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsLow, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsMedium, true);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsHigh, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, true);
*/
                cmd.SetGlobalTexture(_CustomBufferName, _ShadowMapTexture);
                SetupASPMainLightShadowReceiverConstants(cmd, ref visibleLight, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            private void SetupASPMainLightShadowReceiverConstants(CommandBuffer cmd, ref VisibleLight shadowLight, ref RenderingData renderingData)
            {
                Light light = shadowLight.light;
                
                var renderTargetWidth = aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_ShadowCasterCascadesCount == 2)
                    ? aspShadowData.mainLightShadowmapHeight >> 1
                    : aspShadowData.mainLightShadowmapHeight;
                
                Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
                noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
                for (int i = _ShadowCasterCascadesCount; i <= 4; ++i)
                    _CustomWorldToShadowMatrices[i] = noOpShadowMatrix;
                
                float invShadowAtlasWidth = 1.0f / renderTargetWidth;
                float invShadowAtlasHeight = 1.0f / renderTargetHeight;
                float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
                float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
                cmd.SetGlobalMatrixArray(ASPMainLightShadowConstantBuffer._WorldToShadow, _CustomWorldToShadowMatrices);

                bool softShadows = shadowLight.light.shadows == LightShadows.Soft &&
                                   renderingData.shadowData.supportsSoftShadows;
                float softShadowsProp = softShadows ? 1.0f : 0;

                var m_MaxShadowDistanceSq = aspShadowData.shadowDistance *
                                            aspShadowData.shadowDistance;
                var m_CascadeBorder = aspShadowData.mainLightShadowCascadeBorder;
                
                ASPShadowUtil.GetScaleAndBiasForLinearDistanceFade(m_MaxShadowDistanceSq, m_CascadeBorder, out float shadowFadeScale,
                    out float shadowFadeBias);
                
                cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowParams,
                    new Vector4(shadowLight.light.shadowStrength, softShadowsProp, shadowFadeScale, shadowFadeBias));
                
                if (renderingData.shadowData.supportsSoftShadows)
                {
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowOffset0,
                        new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight,
                            invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight));
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowOffset1,
                        new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight,
                            invHalfShadowAtlasWidth, invHalfShadowAtlasHeight));
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowmapSize, new Vector4(invShadowAtlasWidth,
                        invShadowAtlasHeight,
                        renderTargetWidth, renderTargetHeight));
                }
                
                cmd.SetGlobalFloat(ASPMainLightShadowConstantBuffer._CascadeCount, _ShadowCasterCascadesCount > 1 ? _ShadowCasterCascadesCount : 0);

                if (_ShadowCasterCascadesCount > 1)
                {
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres0,
                        _CascadeSplitDistances[0]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres1,
                        _CascadeSplitDistances[1]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres2,
                        _CascadeSplitDistances[2]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres3,
                        _CascadeSplitDistances[3]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                        _CascadeSplitDistances[0].w * _CascadeSplitDistances[0].w,
                        _CascadeSplitDistances[1].w * _CascadeSplitDistances[1].w,
                        _CascadeSplitDistances[2].w * _CascadeSplitDistances[2].w,
                        _CascadeSplitDistances[3].w * _CascadeSplitDistances[3].w));
                }
               
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (_EmptyLightShadowmapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_EmptyLightShadowmapTexture);
                    _EmptyLightShadowmapTexture = null;
                }

                if (_ShadowMapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_ShadowMapTexture);
                    _ShadowMapTexture = null;
                }
            }

            public void Dispose()
            {
            }

            private void ClearData()
            {
                _CustomWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < _CustomWorldToShadowMatrices.Length; i++)
                {
                    _CustomWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                _LightViewMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _LightViewMatrices[i] = Matrix4x4.identity;
                }

                _LightProjectionMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _LightProjectionMatrices[i] = Matrix4x4.identity;
                }

                _ShadowSliceDatas = new ShadowSliceData[4];
                
                _CascadeCullPlanes = new List<Plane[]>();
                for (int i = 0; i < 4; i++)
                {
                    _CascadeCullPlanes.Add(new Plane[6]);
                }
            }
        }
    }
#endif
    #endregion
    #region UNITY2022

#if UNITY_2022_1_OR_NEWER 
[DisallowMultipleRendererFeature("ASP ShadowMap")]
    public class ASPShadowMapFeature : ScriptableRendererFeature
    {
        private static class ASPMainLightShadowConstantBuffer
        {
            public static int _WorldToShadow;
            public static int _ShadowParams;
            public static int _CascadeCount;
            public static int _CascadeShadowSplitSpheres0;
            public static int _CascadeShadowSplitSpheres1;
            public static int _CascadeShadowSplitSpheres2;
            public static int _CascadeShadowSplitSpheres3;
            public static int _CascadeShadowSplitSphereRadii;
            public static int _ShadowOffset0;
            public static int _ShadowOffset1;
            public static int _ShadowmapSize;
        }

        [Tooltip("Expensive, but can prevent shadow missing when object outside camera view")]
        private bool PerformExtraCull = true;
        public RenderQueueRange m_renderQueueRange = RenderQueueRange.all;
        private LayerMask m_layerMask = -1;
        private string m_CustomBufferName = "_ASPShadowMap";
        [SerializeField]
        private CharacterShadowMapResolution m_characterShadowMapResolution = CharacterShadowMapResolution.SIZE_2048;
        
        [FormerlySerializedAs("ShadowDistance")] public float ClipDistance = 50;
        [Range(1,4)]
        public int CascadeCount = 1;
        /// Main light last cascade shadow fade border.
        /// Value represents the width of shadow fade that ranges from 0 to 1.
        /// Where value 0 is used for no shadow fade.
        ///
        [FormerlySerializedAs("LastBorder")]
        [Range(0, 1)]
        [Tooltip("Shadow fade out ratio on last cascade, set to 0 means no fading")]
        public float ShadowFadeRatio = 0.2f;
        //[RenderingLayerMask]
        private int m_renderingLayerMask = -1;
        private ASPShadowRenderPass _scriptablePass;

        /// <inheritdoc/>
        public override void Create()
        {
            var shadowData = SetupCascsadesData();
            _scriptablePass = new ASPShadowRenderPass((uint)m_renderingLayerMask, m_CustomBufferName,
                RenderPassEvent.AfterRenderingShadows, m_renderQueueRange, m_layerMask, shadowData);
            if (!isActive)
            {
                _scriptablePass.IsNotActive = true;
                _scriptablePass.DrawEmptyShadowMap();
            }
            else
            {
                _scriptablePass.IsNotActive = false;
            }
        }
        
        private ASPShadowData SetupCascsadesData()
        {
            // On GLES2 we strip the cascade keywords from the lighting shaders, so for consistency we force disable the cascades here too
            var shadowData = new ASPShadowData();
            var shadoweCascadeCount = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ? 1 : CascadeCount;
            shadowData.mainLightShadowCascadesCount = shadoweCascadeCount;
            shadowData.mainLightShadowmapWidth = (int)m_characterShadowMapResolution;
            shadowData.mainLightShadowmapHeight = (int)m_characterShadowMapResolution;
            shadowData.cascadeSplitArray = new float[4];
            shadowData.builtInCascadeSplit = new Vector3(1, 0, 0);
            shadowData.shadowDistance = ClipDistance;
            switch (shadoweCascadeCount)
            {
                
                case 1:
                    shadowData.builtInCascadeSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    shadowData.cascadeSplitArray = new float[]{1.0f, 0, 0};
                    break;

                case 2:
                    shadowData.builtInCascadeSplit = new Vector3(0.4f, 0.0f, 0.0f);
                    shadowData.cascadeSplitArray = new float[]{0.4f, 1.0f, 0.0f};
                    break;

                case 3:
                    shadowData.builtInCascadeSplit = new Vector3(0.1f, 0.3f, 0.0f);
                    shadowData.cascadeSplitArray = new float[]{0.1f, 0.3f, 1.0f};
                    break;

                default:
                    shadowData.builtInCascadeSplit = new Vector3(0.067f, 0.2f, 0.467f);
                    shadowData.cascadeSplitArray = new float[]{0.067f, 0.2f, 0.467f, 1.0f};
                    break;
            }   

            shadowData.mainLightShadowCascadeBorder = ShadowFadeRatio;
            return shadowData;
        }

        protected override void Dispose(bool disposing)
        {
            _scriptablePass.Dispose();
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_scriptablePass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if(_scriptablePass!= null)
                _scriptablePass.PerformExtraCull = PerformExtraCull;
        }
        
        class ASPShadowRenderPass : ScriptableRenderPass
        {
            public bool PerformExtraCull;
            public bool IsNotActive;
            private RTHandle _ShadowMapTexture;
            private RTHandle _EmptyLightShadowmapTexture;
            private FilteringSettings _FilteringSettings;
            private RenderStateBlock _RenderStateBlock;
            private ShaderTagId _ShaderTagId = new ShaderTagId("ASPShadowCaster");
            private string _CustomBufferName;

            private ASPShadowData aspShadowData;
            private Matrix4x4[] _CustomWorldToShadowMatrices;
            private bool _IsEmptyShdaowMap;
            private List<Plane[]> _CascadeCullPlanes;
            private Matrix4x4[] _LightViewMatrices;
            private Matrix4x4[] _LightProjectionMatrices;
            private ShadowSliceData[] _ShadowSliceDatas;
            private ScriptableCullingParameters _CullingParameters = new ScriptableCullingParameters();
            Vector4[] _CascadeSplitDistances;
            int _ShadowCasterCascadesCount;
            
            public void DrawEmptyShadowMap()
            {
                if (_EmptyLightShadowmapTexture == null)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _EmptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                }

                Shader.SetGlobalTexture(_CustomBufferName, _EmptyLightShadowmapTexture);
            }

            void SetupForEmptyRendering()
            {
                //Debug.Log("SetupForEmptyRendering");
                _IsEmptyShdaowMap = true;
                ShadowUtils.ShadowRTReAllocateIfNeeded(ref _EmptyLightShadowmapTexture, 1, 1, 16,
                    name: "_ASPEmptyLightShadowmapTexture");
            }
            
            public ASPShadowRenderPass(uint renderingLayerMask, string customBufferName,
                RenderPassEvent passEvent, RenderQueueRange queueRange, LayerMask layerMask, ASPShadowData ShadowData)
            {
                profilingSampler = new ProfilingSampler("ASP Shadow Render Pass");
                ClearData();
                _CustomWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < _CustomWorldToShadowMatrices.Length; i++)
                {
                    _CustomWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                _CustomBufferName = customBufferName;
                renderPassEvent = passEvent;
                _FilteringSettings = new FilteringSettings(RenderQueueRange.all);
                
                //un-comment below line to use rendering layer mask to filter out objects
                //_FilteringSettings.renderingLayerMask = renderingLayerMask;

                _RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

                //_ShaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
                //_ShaderTagIds.Add(new ShaderTagId("UniversalForward"));
                //_ShaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
                //_ShaderTagIds.Add(new ShaderTagId("LightweightForward"));
                aspShadowData = ShadowData;
                _CascadeSplitDistances = new Vector4[4];
                ASPMainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_ASPMainLightWorldToShadow");
                ASPMainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_ASPMainLightShadowParams");
                ASPMainLightShadowConstantBuffer._CascadeCount = Shader.PropertyToID("_ASPCascadeCount");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres0");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres1");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres2");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_ASPCascadeShadowSplitSpheres3");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_ASPCascadeShadowSplitSphereRadii");
                ASPMainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_ASPMainLightShadowOffset0");
                ASPMainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_ASPMainLightShadowOffset1");
                ASPMainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_ASPMainLightShadowmapSize");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _ShadowCasterCascadesCount = aspShadowData.mainLightShadowCascadesCount;
                var renderTargetWidth = aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_ShadowCasterCascadesCount == 2)
                    ? aspShadowData.mainLightShadowmapHeight >> 1
                    : aspShadowData.mainLightShadowmapHeight;

                var cascadeCount = _ShadowCasterCascadesCount;
               // var currentCascadeSplit = aspShadowData.cascadeSplit;
                int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                    aspShadowData.mainLightShadowmapWidth,
                    aspShadowData.mainLightShadowmapHeight, cascadeCount);

                int shadowLightIndex = renderingData.lightData.mainLightIndex;

                if (IsNotActive)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!renderingData.shadowData.supportsMainLightShadows)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (shadowLightIndex == -1)
                {
                    SetupForEmptyRendering();
                    return;
                }
                
                VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if (shadowLight.lightType != LightType.Directional)
                {
                    SetupForEmptyRendering();
                     return;
                }

                if (light.shadows == LightShadows.None)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!_IsEmptyShdaowMap)
                {
                    for (int cascadeIndex = 0; cascadeIndex < cascadeCount; ++cascadeIndex)
                    {
                        _ShadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                        var planes = _CascadeCullPlanes[cascadeIndex];
                        bool success = ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(ref renderingData.cameraData, ref aspShadowData, 
                            cascadeIndex, shadowLight.light, shadowResolution, aspShadowData.cascadeSplitArray, out Vector4 cullingSphere, out 
                            Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);
                        _LightViewMatrices[cascadeIndex] = viewMatrix;
                        _LightProjectionMatrices[cascadeIndex] = projMatrix;
                        _CascadeCullPlanes[cascadeIndex] = planes;
                        _CascadeSplitDistances[cascadeIndex] = cullingSphere;
                        
                        if (!success)
                        {
                            SetupForEmptyRendering();
                            ConfigureTarget(_EmptyLightShadowmapTexture);
                            ConfigureClear(ClearFlag.All, Color.black);
                            return;
                        }

                        _CustomWorldToShadowMatrices[cascadeIndex] =
                            ASPShadowUtil.GetShadowTransform(_LightProjectionMatrices[cascadeIndex],
                                _LightViewMatrices[cascadeIndex]);

                        // Handle shadow slices
                        var offsetX = (cascadeIndex % 2) * shadowResolution;
                        var offsetY = (cascadeIndex / 2) * shadowResolution;

                        ASPShadowUtil.ApplySliceTransform(ref _CustomWorldToShadowMatrices[cascadeIndex], offsetX, offsetY,
                            shadowResolution,
                            renderTargetWidth, renderTargetHeight);
                        _ShadowSliceDatas[cascadeIndex].projectionMatrix = _LightProjectionMatrices[cascadeIndex];
                        _ShadowSliceDatas[cascadeIndex].viewMatrix = _LightViewMatrices[cascadeIndex];
                        _ShadowSliceDatas[cascadeIndex].offsetX = offsetX;
                        _ShadowSliceDatas[cascadeIndex].offsetY = offsetY;
                        _ShadowSliceDatas[cascadeIndex].resolution = shadowResolution;
                        _ShadowSliceDatas[cascadeIndex].shadowTransform =
                            _CustomWorldToShadowMatrices[cascadeIndex];
                        _ShadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                    }
                    
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _ShadowMapTexture, renderTargetWidth, renderTargetHeight,
                            16, name: "_CustomMainLightShadowmapTexture");
                    ConfigureTarget(_ShadowMapTexture);
                    ConfigureClear(ClearFlag.All, Color.black);
                    
                }
                else
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _EmptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                    ConfigureTarget(_EmptyLightShadowmapTexture);
                    ConfigureClear(ClearFlag.All, Color.black);
                }
            }

            private void RenderEmpty(ScriptableRenderContext context)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.SetGlobalTexture(_CustomBufferName, _EmptyLightShadowmapTexture);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_IsEmptyShdaowMap)
                {
                    _IsEmptyShdaowMap = false;
                    RenderEmpty(context);
                    return;
                }

                if (_ShadowMapTexture == null)
                {
                    _IsEmptyShdaowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }

                
                CommandBuffer cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var prevViewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;
                var prevProjMatrix = renderingData.cameraData.camera.projectionMatrix;
                var visibleLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];
                
                bool hasValidCullParam = false;
                if (PerformExtraCull && renderingData.cameraData.camera.TryGetCullingParameters(out _CullingParameters))
                {
                    hasValidCullParam = true;
                    _CullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
                    _CullingParameters.isOrthographic = true;
                }

                var cullResult = renderingData.cullResults;
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP ShadowMap Pass")))
                {
                    for (int i = 0; i < _ShadowCasterCascadesCount; i++)
                    {
                        if (PerformExtraCull && hasValidCullParam)
                        {
                            _CullingParameters.cullingMatrix = _LightProjectionMatrices[i] * _LightViewMatrices[i];
                         //   var cullingPlaneCount = _ShadowSliceDatas[i].splitData.cullingPlaneCount >= _CullingParameters.cullingPlaneCount? _CullingParameters.cullingPlaneCount : _ShadowSliceDatas[i].splitData.cullingPlaneCount;
                            for (int cullPlaneIndex = 0; cullPlaneIndex < 6; cullPlaneIndex++) 
                            {
                                _CullingParameters.SetCullingPlane (cullPlaneIndex, _CascadeCullPlanes[i][cullPlaneIndex]);
                            }
                            cullResult = context.Cull(ref _CullingParameters);
                        }
                        
                        // Handle drawing 
                        var drawSettings =
                            CreateDrawingSettings(_ShaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                        drawSettings.perObjectData = PerObjectData.None;
                        
                        // Need to start by setting the Camera position as that is not set for passes executed before normal rendering
                       //  cmd.SetGlobalVector("_WorldSpaceCameraPos", renderingData.cameraData.worldSpaceCameraPos);

                        // TODO handle empty rendering
                        cmd.SetGlobalDepthBias(1.0f, 3.5f); 
                        cmd.SetViewport(new Rect(_ShadowSliceDatas[i].offsetX, _ShadowSliceDatas[i].offsetY,
                            _ShadowSliceDatas[i].resolution, _ShadowSliceDatas[i].resolution));
                        cmd.SetViewProjectionMatrices(_LightViewMatrices[i], _LightProjectionMatrices[i]);
                        Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                            renderingData.lightData.mainLightIndex, ref renderingData.shadowData,
                            _LightProjectionMatrices[i], _ShadowSliceDatas[i].resolution);
                        // ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref visibleLight, shadowBias);
                        cmd.SetGlobalVector("_ASPShadowBias", shadowBias);

                        // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
                        Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                        cmd.SetGlobalVector("_ASPLightDirection",
                            new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                        // For punctual lights, computing light direction at each vertex position provides more consistent results (shadow shape does not change when "rotating the point light" for example)
                        Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                        cmd.SetGlobalVector("_ASPLightPosition",
                            new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        context.DrawRenderers(cullResult, ref drawSettings, ref _FilteringSettings,
                            ref _RenderStateBlock);
                        cmd.DisableScissorRect();
                        cmd.SetGlobalDepthBias(0.0f, 0.0f); 
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }
                
                cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
/*
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsLow, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsMedium, true);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsHigh, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, true);
*/
                cmd.SetGlobalTexture(_CustomBufferName, _ShadowMapTexture);
                SetupASPMainLightShadowReceiverConstants(cmd, ref visibleLight, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            private void SetupASPMainLightShadowReceiverConstants(CommandBuffer cmd, ref VisibleLight shadowLight, ref RenderingData renderingData)
            {
                Light light = shadowLight.light;
                
                var renderTargetWidth = aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_ShadowCasterCascadesCount == 2)
                    ? aspShadowData.mainLightShadowmapHeight >> 1
                    : aspShadowData.mainLightShadowmapHeight;
                
                Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
                noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
                for (int i = _ShadowCasterCascadesCount; i <= 4; ++i)
                    _CustomWorldToShadowMatrices[i] = noOpShadowMatrix;
                
                float invShadowAtlasWidth = 1.0f / renderTargetWidth;
                float invShadowAtlasHeight = 1.0f / renderTargetHeight;
                float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
                float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
                cmd.SetGlobalMatrixArray(ASPMainLightShadowConstantBuffer._WorldToShadow, _CustomWorldToShadowMatrices);

                bool softShadows = shadowLight.light.shadows == LightShadows.Soft &&
                                   renderingData.shadowData.supportsSoftShadows;
                float softShadowsProp = softShadows ? 1.0f : 0;

                var m_MaxShadowDistanceSq = aspShadowData.shadowDistance *
                                            aspShadowData.shadowDistance;
                var m_CascadeBorder = aspShadowData.mainLightShadowCascadeBorder;
                
                ASPShadowUtil.GetScaleAndBiasForLinearDistanceFade(m_MaxShadowDistanceSq, m_CascadeBorder, out float shadowFadeScale,
                    out float shadowFadeBias);
                
                cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowParams,
                    new Vector4(shadowLight.light.shadowStrength, softShadowsProp, shadowFadeScale, shadowFadeBias));
                
                if (renderingData.shadowData.supportsSoftShadows)
                {
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowOffset0,
                        new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight,
                            invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight));
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowOffset1,
                        new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight,
                            invHalfShadowAtlasWidth, invHalfShadowAtlasHeight));
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._ShadowmapSize, new Vector4(invShadowAtlasWidth,
                        invShadowAtlasHeight,
                        renderTargetWidth, renderTargetHeight));
                }
                
                cmd.SetGlobalFloat(ASPMainLightShadowConstantBuffer._CascadeCount, _ShadowCasterCascadesCount > 1 ? _ShadowCasterCascadesCount : 0);
                
                if (_ShadowCasterCascadesCount > 1)
                {
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres0,
                        _CascadeSplitDistances[0]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres1,
                        _CascadeSplitDistances[1]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres2,
                        _CascadeSplitDistances[2]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres3,
                        _CascadeSplitDistances[3]);
                    cmd.SetGlobalVector(ASPMainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                        _CascadeSplitDistances[0].w * _CascadeSplitDistances[0].w,
                        _CascadeSplitDistances[1].w * _CascadeSplitDistances[1].w,
                        _CascadeSplitDistances[2].w * _CascadeSplitDistances[2].w,
                        _CascadeSplitDistances[3].w * _CascadeSplitDistances[3].w));
                }
            }

            public void Dispose()
            {
                if (_EmptyLightShadowmapTexture != null)
                {
                    Shader.SetGlobalTexture(_CustomBufferName, _EmptyLightShadowmapTexture);
                }

                _ShadowMapTexture?.Release();
                _EmptyLightShadowmapTexture?.Release();
            }

            private void ClearData()
            {
                _CustomWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < _CustomWorldToShadowMatrices.Length; i++)
                {
                    _CustomWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                _LightViewMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _LightViewMatrices[i] = Matrix4x4.identity;
                }

                _LightProjectionMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _LightProjectionMatrices[i] = Matrix4x4.identity;
                }

                _ShadowSliceDatas = new ShadowSliceData[4];

                _CascadeCullPlanes = new List<Plane[]>();
                for (int i = 0; i < 4; i++)
                {
                    _CascadeCullPlanes.Add(new Plane[6]);
                }
            }
        }
    }

#endif
    #endregion
}