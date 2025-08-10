Shader "Custom/Wave"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _PeakColor("Peak Color", Color) = (1, 1, 1, 1)
        _ComboColor("Combo Color", Color) = (1, 0, 0, 1)
        _NoiseScale ("Noise Scale", Float) = 1.0
        _Emission("__emission", Float) = 0.0
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5

        // BlendMode
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        [HideInInspector] _SampleGI("SampleGI", float) = 0.0 // needed from bakedlit
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        // -------------------------------------
        // Render State Commands
        Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "Unlit"

            // -------------------------------------
            // Render State Commands
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #define SpreadNum 10

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PeakColor;
                half4 _ComboColor;
                float _NoiseScale;
                half _Cutoff;
                half _Surface;
                float _Emission;
            CBUFFER_END
            float _Combo;
            float _Clip;
            float _ClipSign;
            int _GridX;
            int _GridZ;
            float _SpreadTime[SpreadNum];
            float _HitSpreadTime[SpreadNum];

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            #include "PerlinNoise.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float cameraDist : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                float4 positionCS : SV_POSITION;
                float bump : TEXCOORD3;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings UnlitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Spread
                float3 translation = TransformObjectToWorld(float3(0, 0, 0));
                translation.x += _GridX;
                translation.z += _GridZ;
                float3 cameraCoord = TransformWorldToView(translation);
                float bump = 0;
                for (int i = 0; i < SpreadNum; i ++)
                {
                    float dist = abs(cameraCoord.x) - (1 - _HitSpreadTime[i]) * 300;
                    bump += exp(-dist * dist / 200) * PerlinNoise(cameraCoord * _NoiseScale);
                }
                for (int i = 0; i < SpreadNum; i ++)
                {
                    float dist = -cameraCoord.z - _SpreadTime[i] * 300;
                    bump += exp(-dist * dist / 200) * PerlinNoise(cameraCoord * _NoiseScale) * (1 - exp(-cameraCoord.x * cameraCoord.x / 800));
                }
                bump *= 2;

                float4 positionWS = mul(UNITY_MATRIX_M, input.positionOS);
                positionWS.x += _GridX;
                positionWS.z += _GridZ;
                positionWS.y += bump * 10;
                float4 positionVS = mul(UNITY_MATRIX_V, positionWS);
                output.positionCS = mul(UNITY_MATRIX_P, positionVS);
                output.bump = bump;
                #if defined(_FOG_FRAGMENT)
                output.fogCoord = positionVS.z;
                #else
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                float3 cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                output.cameraDist = dot(cameraPos, cameraPos);

                return output;
            }

            half4 UnlitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                clip(_ClipSign * (_Clip - input.cameraDist));

                half3 color = _BaseColor.rgb;
                half alpha = _BaseColor.a;

                alpha = AlphaDiscard(alpha, _Cutoff);
                color = AlphaModulate(color, alpha);

                InputData inputData = (InputData)0;
                inputData.shadowMask = half4(1, 1, 1, 1);

                #ifdef _DBUFFER
                    ApplyDecalToBaseColor(input.positionCS, color);
                #endif

                half4 finalColor = UniversalFragmentUnlit(inputData, color, alpha);

                #if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
                    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
                    finalColor.rgb *= aoFactor.directAmbientOcclusion;
                #endif
                #if defined(_FOG_FRAGMENT)
                #if (defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2))
                    float viewZ = -input.fogCoord;
                    float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
                    half fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
                #else
                    half fogFactor = 0;
                #endif
                #else
                    half fogFactor = input.fogCoord;
                #endif

                finalColor = lerp(finalColor, _PeakColor, saturate(input.bump));
                finalColor.rgb = MixFog(finalColor.rgb, fogFactor) * (1 + _Emission) + _Combo * _ComboColor.rgb;
                finalColor.a = OutputAlpha(finalColor.a, IsSurfaceTypeTransparent(_Surface));

                return finalColor;
            }

            ENDHLSL
        }
        
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #define SpreadNum 10

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _PeakColor;
                half4 _ComboColor;
                float _NoiseScale;
                half _Cutoff;
                half _Surface;
                float _Emission;
            CBUFFER_END
            float _Clip;
            float _ClipSign;
            int _GridX;
            int _GridZ;
            float _SpreadTime[SpreadNum];
            float _HitSpreadTime[SpreadNum];

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
            #include "PerlinNoise.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float cameraDist : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                float4 positionCS : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Spread
                float3 translation = TransformObjectToWorld(float3(0, 0, 0));
                translation.x += _GridX;
                translation.z += _GridZ;
                float3 cameraCoord = TransformWorldToView(translation);
                float bump = 0;
                for (int i = 0; i < SpreadNum; i ++)
                {
                    float dist = abs(cameraCoord.x) - (1 - _HitSpreadTime[i]) * 300;
                    bump += exp(-dist * dist / 200) * PerlinNoise(cameraCoord * _NoiseScale);
                }
                for (int i = 0; i < SpreadNum; i ++)
                {
                    float dist = -cameraCoord.z - _SpreadTime[i] * 300;
                    bump += exp(-dist * dist / 200) * PerlinNoise(cameraCoord * _NoiseScale) * (1 - exp(-cameraCoord.x * cameraCoord.x / 800));
                }
                bump *= 2;

                float4 positionWS = mul(UNITY_MATRIX_M, input.positionOS);
                positionWS.x += _GridX;
                positionWS.z += _GridZ;
                positionWS.y += bump * 10;
                float4 positionVS = mul(UNITY_MATRIX_V, positionWS);
                output.positionCS = mul(UNITY_MATRIX_P, positionVS);
                #if defined(_FOG_FRAGMENT)
                output.fogCoord = positionVS.z;
                #else
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                float3 cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                output.cameraDist = dot(cameraPos, cameraPos);

                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                clip(_ClipSign * (_Clip - input.cameraDist));

                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
