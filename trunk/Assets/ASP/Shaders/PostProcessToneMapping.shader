/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

Shader "Hidden/ASP/PostProcess/ToneMapping"
{
	SubShader
    {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always
        Pass
        {
            Name "GT ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            
            static const float e = 2.71828;

			float W_f(float x,float e0,float e1) {
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				float a = (x - e0) / (e1 - e0);
				return a * a*(3 - 2 * a);
			}
			float H_f(float x, float e0, float e1) {
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				return (x - e0) / (e1 - e0);
			}

			float GranTurismoTonemapper(float x) {
				float P = 1;
				float a = 1;
				float m = 0.22;
				float l = 0.4;
				float c = 1.33;
				float b = 0;
				float l0 = (P - m)*l / a;
				float L0 = m - m / a;
				float L1 = m + (1 - m) / a;
				float L_x = m + a * (x - m);
				float T_x = m * pow(x / m, c) + b;
				float S0 = m + l0;
				float S1 = m + a * l0;
				float C2 = a * P / (P - S1);
				float S_x = P - (P - S1)*pow(e,-(C2*(x-S0)/P));
				float w0_x = 1 - W_f(x, 0, m);
				float w2_x = H_f(x, m + l0, m + l0);
				float w1_x = 1 - w0_x - w2_x;
				float f_x = T_x * w0_x + L_x * w1_x + S_x * w2_x;
				return f_x;
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _IgnoreCharacterPixels;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(sceneDepth, characterDepth);
            	
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return col;
            	}

                float r = GranTurismoTonemapper(col.r);
				float g = GranTurismoTonemapper(col.g);
				float b = GranTurismoTonemapper(col.b);
				half4 toneMappedCol = half4(r,g,b,col.a);
                return toneMappedCol;
            }
            ENDHLSL
        }

		Pass
        {
            Name "Filmic ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            
            static const float e = 2.71828;

			float3 reinhard_jodie(float3 v)
			{
			    float l = Luminance(v);
			    float3 tv = v / (1.0f + v);
			    return lerp(v / (1.0f + l), tv, tv);
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

           float3 F(float3 x)
			{
				const float A = 0.22f;
				const float B = 0.30f;
				const float C = 0.10f;
				const float D = 0.20f;
				const float E = 0.01f;
				const float F = 0.30f;
			 
				return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
			}

			float3 Uncharted2ToneMapping(float3 color, float adapted_lum)
			{
				const float WHITE = 11.2f;
				return F(1.6f * adapted_lum * color) / F(WHITE);
			}

            float _IgnoreCharacterPixels;
			float _Exposure;
            float _ToneMapLowerBound;
            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(LinearEyeDepth(characterDepth, _ZBufferParams), ASP_DEPTH_EYE_BIAS + LinearEyeDepth(sceneDepth, _ZBufferParams));
            	
				half4 toneMappedCol = half4(Uncharted2ToneMapping(col.rgb, _Exposure),col.a);
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		 return lerp(col, toneMappedCol, pow(saturate(_ToneMapLowerBound*1.2), 0.5));
            	}
            	
                return toneMappedCol;
            }
            ENDHLSL
        }

		Pass
        {
            Name "agx ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            #if UNITY_VERSION >= 202201
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #endif
            
            static const float e = 2.71828;

			float3 reinhard_jodie(float3 v)
			{
			    float l = Luminance(v);
			    float3 tv = v / (1.0f + v);
			    return lerp(v / (1.0f + l), tv, tv);
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _IgnoreCharacterPixels;
			float _Exposure;
            TEXTURE2D(_InternalLut);

            #define AGX_LOOK 2

            float3 agx7thDefaultContrastApprox(float3 x) {
				  float3 x2 = x * x;
				  float3 x4 = x2 * x2;
				  float3 x6 = x4 * x2;
				  
				  return - 17.86     * x6 * x
				         + 78.01     * x6
				         - 126.7     * x4 * x
				         + 92.06     * x4
				         - 28.72     * x2 * x
				         + 4.361     * x2
				         - 0.1718    * x
				         + 0.002857;
				}

			float3 agxDefaultContrastApprox(float3 x) {
			  float3 x2 = x * x;
			  float3 x4 = x2 * x2;
			  
			  return + 15.5     * x4 * x2
			         - 40.14    * x4 * x
			         + 31.96    * x4
			         - 6.868    * x2 * x
			         + 0.4298   * x2
			         + 0.1191   * x
			         - 0.00232;
			}

			float3 agx(float3 val) {
			  const float3x3 agx_mat = float3x3(
			    0.842479062253094, 0.0423282422610123, 0.0423756549057051,
			    0.0784335999999992,  0.878468636469772,  0.0784336,
			    0.0792237451477643, 0.0791661274605434, 0.879142973793104);
			    
			  const float min_ev = -12.47393f;
			  const float max_ev = 4.026069f;

			  // Input transform (inset)
			  val = mul(agx_mat, val);
			  
			  // Log2 space encoding
			  val = clamp(log2(val), min_ev, max_ev);
			  val = (val - min_ev) / (max_ev - min_ev);
			  
			  // Apply sigmoid function approximation
			  val = agxDefaultContrastApprox(val);

			  return val;
			}

			float3 agxEotf(float3 val) {
			  const float3x3 agx_mat_inv = float3x3(
			    1.19687900512017, -0.0528968517574562, -0.0529716355144438,
			    -0.0980208811401368, 1.15190312990417, -0.0980434501171241,
			    -0.0990297440797205, -0.0989611768448433, 1.15107367264116);
			    
			  // Inverse input transform (outset)
			  val = mul(agx_mat_inv, val);
				
			  
			  // sRGB IEC 61966-2-1 2.2 Exponent Reference EOTF Display
			  // NOTE: We're linearizing the output here. Comment/adjust when
			  // *not* using a sRGB render target
			  val = pow(val, float3(2.2, 2.2, 2.2));

			  return val;
			}

			float3 agxLook(float3 val) {
			  const float3 lw = float3(0.2126, 0.7152, 0.0722);
			  float luma = dot(val, lw);
			  
			  // Default
			  float3 offset = float3(0.0, 0.0, 0.0);
			  float3 slope = float3(1.0, 1.0, 1.0);
			  float3 power = float3(1.0, 1.0, 1.0);
			  float sat = 1.0;
			 
			#if AGX_LOOK == 1
			  // Golden
			  slope = float3(1.0, 0.9, 0.5);
			  power = float3(0.8, 0.8, 0.8);
			  sat = 0.8;
			#elif AGX_LOOK == 2
			  // Punchy
			  slope = float3(1.0, 1.0, 1.0);
			  power = float3(1.35, 1.35, 1.35);
			  sat = 1.4;
			#endif
			             // ASC CDL
			  val = pow(val * slope + offset, power);
			  return luma + sat * (val - luma);
			}

            float4 _Lut_Params;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(sceneDepth, characterDepth);
            	
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return col;
            	}
				//float3 toneCol = saturate(NeutralTonemap( col.rgb));
            	float3 agxCol = agx(col.rgb);
            	agxCol = agxLook(agxCol);
            	agxCol = agxEotf(agxCol);
				//toneCol = ApplyLut2D(TEXTURE2D_ARGS(_InternalLut, sampler_LinearClamp), toneCol, _Lut_Params.xyz);
            	
				half4 toneMappedCol = half4(agxCol, col.a);
                return toneMappedCol;
            }
            ENDHLSL
        }
    }
}