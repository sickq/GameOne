using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    public static class ASPShadowUtil
    {
        static Vector3 s_BL_Dir = Vector3.zero;
        static Vector3 s_BR_Dir = Vector3.zero;
        static Vector3 s_TL_Dir = Vector3.zero;
        static Vector3 s_TR_Dir = Vector3.zero;

        //
        public static bool ComputeDirectionalShadowMatricesAndCullingSphere
        (ref CameraData cameraData, ref ASP.ASPShadowData shadowData, int cascadeIndex, Light light,
            int shadowResolution,
            float[] splitArray, out Vector4 cullingSphere, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref Plane[] planes,
            out float ZDistance)
        {
            var s_camCenter = cameraData.camera.transform.position;
            var s_camNear = cameraData.camera.nearClipPlane;
            if (cascadeIndex == 0)
            {
                Matrix4x4 VP = cameraData.GetProjectionMatrix() * cameraData.GetViewMatrix();
                Matrix4x4 I_VP = VP.inverse;
                Vector3 nearBL = I_VP.MultiplyPoint(new Vector3(-1, -1, -1));
                Vector3 nearBR = I_VP.MultiplyPoint(new Vector3(1, -1, -1));
                Vector3 nearTR = I_VP.MultiplyPoint(new Vector3(1, 1, -1));
                Vector3 nearTL = I_VP.MultiplyPoint(new Vector3(-1, 1, -1));
                s_camCenter = cameraData.camera.transform.position;
                s_BL_Dir = nearBL - s_camCenter;
                s_BR_Dir = nearBR - s_camCenter;
                s_TR_Dir = nearTR - s_camCenter;
                s_TL_Dir = nearTL - s_camCenter;

            }

            float cascadeFar = s_camNear + shadowData.shadowDistance * splitArray[cascadeIndex];
            float cascadeNear = s_camNear;
            if (cascadeIndex > 0)
            {
                cascadeNear = s_camNear + shadowData.shadowDistance * splitArray[cascadeIndex - 1];
            }

            Vector3 cascadeNearBL = s_camCenter + s_BL_Dir / s_camNear * cascadeNear;
            Vector3 cascadeNearTR = s_camCenter + s_TR_Dir / s_camNear * cascadeNear;

            Vector3 cascadeFarBL = s_camCenter + s_BL_Dir / s_camNear * cascadeFar;
            Vector3 cascadeFarTR = s_camCenter + s_TR_Dir / s_camNear * cascadeFar;
            
            //sphere bounding box
            float a = Vector3.Distance(cascadeNearBL, cascadeNearTR);
            float b = Vector3.Distance(cascadeFarBL, cascadeFarTR);
            float l = cascadeFar - cascadeNear;

            float x = (b * b - a * a) / (8 * l) + l / 2;

            Vector3 sphereCenter = cameraData.camera.transform.position +
                                   cameraData.camera.transform.forward * (cascadeNear + x);
            float sphereR = Mathf.Sqrt(x * x + a * a / 4);
            var d = Mathf.Sqrt(x * x + b * b / 4) * (4 - cascadeIndex)*2;
            //Anti-Shimmering
            if (cascadeIndex < 4)
            {
                float squrePixelWidth = 2 * sphereR / shadowResolution;
                Vector3 sphereCenterLS = light.transform.worldToLocalMatrix.MultiplyPoint(sphereCenter);
                sphereCenterLS.x /= squrePixelWidth;
                sphereCenterLS.x = Mathf.Floor(sphereCenterLS.x);
                sphereCenterLS.x *= squrePixelWidth;
                sphereCenterLS.y /= squrePixelWidth;
                sphereCenterLS.y = Mathf.Floor(sphereCenterLS.y);
                sphereCenterLS.y *= squrePixelWidth;
                sphereCenter = light.transform.localToWorldMatrix.MultiplyPoint(sphereCenterLS);
            }

            cullingSphere.x = sphereCenter.x;
            cullingSphere.y = sphereCenter.y;
            cullingSphere.z = sphereCenter.z;
            cullingSphere.w = sphereR;

            float backDistance = sphereR * light.shadowNearPlane * 10;
            Vector3 shadowMapEye = sphereCenter - light.transform.forward * backDistance;
            Vector3 shadowMapAt = sphereCenter;

            Matrix4x4 lookMatrix = Matrix4x4.LookAt(shadowMapEye, shadowMapAt, light.transform.up);
            // Matrix that mirrors along Z axis, to match the camera space convention.
            Matrix4x4 scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
            // Final view matrix is inverse of the LookAt matrix, and then mirrored along Z.
            viewMatrix = scaleMatrix * lookMatrix.inverse;
            projMatrix = Matrix4x4.Ortho(-sphereR, sphereR, -sphereR, sphereR, 0.0f, 2.0f * backDistance);
            ZDistance = 2.0f * backDistance;
            
            var backExtend = -ZDistance * 2 * (4 - cascadeIndex);
            var cullProjMatrix = Matrix4x4.Ortho(-sphereR, sphereR, -sphereR, sphereR, backExtend, ZDistance);
            GeometryUtility.CalculateFrustumPlanes(cullProjMatrix * viewMatrix, planes);

            // Now 
            return true;
        }

        public static void ApplySliceTransform(ref Matrix4x4 shadowTransform, int offsetX, int offsetY,
            int tileResolution, int atlasWidth, int atlasHeight)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = tileResolution * oneOverAtlasWidth;
            sliceTransform.m11 = tileResolution * oneOverAtlasHeight;
            sliceTransform.m03 = offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = offsetY * oneOverAtlasHeight;

            // Apply shadow slice scale and offset
            shadowTransform = sliceTransform * shadowTransform;
        }

        public static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
        {
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            if (SystemInfo.usesReversedZBuffer)
            {
                proj.m20 = -proj.m20;
                proj.m21 = -proj.m21;
                proj.m22 = -proj.m22;
                proj.m23 = -proj.m23;
            }

            Matrix4x4 worldToShadow = proj * view;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            // textureScaleAndBias maps texture space coordinates from [-1,1] to [0,1]

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }

        public static void GetScaleAndBiasForLinearDistanceFade(float fadeDistance, float border, out float scale,
            out float bias)
        {
            // To avoid division from zero
            // This values ensure that fade within cascade will be 0 and outside 1
            if (border < 0.0001f)
            {
                float multiplier = 1000f; // To avoid blending if difference is in fractions
                scale = multiplier;
                bias = -fadeDistance * multiplier;
                return;
            }

            border = 1 - border;
            border *= border;

            // Fade with distance calculation is just a linear fade from 90% of fade distance to fade distance. 90% arbitrarily chosen but should work well enough.
            float distanceFadeNear = border * fadeDistance;
            scale = 1.0f / (fadeDistance - distanceFadeNear);
            bias = -distanceFadeNear / (fadeDistance - distanceFadeNear);
        }
    }
}