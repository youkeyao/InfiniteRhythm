Shader "Custom/NoiseSphere"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.1, 0.3, 0.6, 1.0)
        _ColorB ("Color B", Color) = (0.3, 0.6, 0.2, 1.0)
        _ColorC ("Color C", Color) = (0.8, 0.7, 0.5, 1.0)
        _ComboColor ("Combo Color", Color) = (1.0, 0.0, 0.0, 1.0)
        _SegmentPoint ("Segment Point", Range(0, 1)) = 0.5
        _NoiseScale ("Noise Scale", Float) = 1.0
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5

        // BlendMode
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        _Emission("__emission", Float) = 0.0
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

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ColorA)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ColorB)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ColorC)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ComboColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _SegmentPoint)
                UNITY_DEFINE_INSTANCED_PROP(float, _NoiseScale)
                UNITY_DEFINE_INSTANCED_PROP(half, _Cutoff)
                UNITY_DEFINE_INSTANCED_PROP(half, _Surface)
                UNITY_DEFINE_INSTANCED_PROP(half, _Emission)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
            float _Combo;
            float _Clip;
            float _ClipSign;
            float _Beat;
            float _MaxSpectrum;

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PerlinNoise.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float cameraDist : TEXCOORD1;
                float4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings UnlitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float noiseScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NoiseScale);
                float4 cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                float translation = TransformObjectToWorld(float3(0, 0, 0));
                float deform = PerlinNoise(cameraPos * noiseScale) * _MaxSpectrum * 4 + 1;
                float posNoise = PerlinNoise(translation * 10);
                input.positionOS.xyz *= deform;
                input.positionOS.y += posNoise * 10;
                output.positionCS = mul(UNITY_MATRIX_MVP, input.positionOS);
                cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                output.cameraDist = dot(cameraPos, cameraPos);
                float4 colorA = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorA);
                float4 colorB = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorB);
                float4 colorC = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorC);
                float s = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SegmentPoint);
                posNoise += 0.5 * _Beat;
                if (posNoise > 1)
                {
                    posNoise = saturate(posNoise - 1);
                }
                if (posNoise < s)
                {
                    output.color = lerp(colorA, colorB, posNoise / s) * deform;
                }
                else
                {
                    output.color = lerp(colorB, colorC, (posNoise - s) / (1 - s)) * deform;
                }

                return output;
            }

            half4 UnlitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_ClipSign * (_Clip - input.cameraDist));

                half4 baseColor = input.color;
                half3 color = baseColor.rgb;
                half alpha = baseColor.a;

                alpha = AlphaDiscard(alpha, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
                color = AlphaModulate(color, alpha);

                InputData inputData = (InputData)0;
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 finalColor = UniversalFragmentUnlit(inputData, color, alpha);
                float emission = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Emission);
                finalColor.rgb = finalColor.rgb + emission * finalColor + _Combo * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ComboColor).rgb;
                finalColor.a = OutputAlpha(finalColor.a, IsSurfaceTypeTransparent(UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Surface)));

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

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ColorA)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ColorB)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ColorC)
                UNITY_DEFINE_INSTANCED_PROP(float, _SegmentPoint)
                UNITY_DEFINE_INSTANCED_PROP(float, _NoiseScale)
                UNITY_DEFINE_INSTANCED_PROP(half, _Cutoff)
                UNITY_DEFINE_INSTANCED_PROP(half, _Surface)
                UNITY_DEFINE_INSTANCED_PROP(half, _Emission)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
            float _Clip;
            float _ClipSign;
            float _Beat;
            float _MaxSpectrum;

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PerlinNoise.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float cameraDist : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float noiseScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NoiseScale);
                float4 cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                float translation = TransformObjectToWorld(float3(0, 0, 0));
                float deform = PerlinNoise(cameraPos * noiseScale) * _MaxSpectrum * 4 + 1;
                float posNoise = (PerlinNoise(translation) + 1) / 2;
                input.positionOS.xyz *= deform;
                input.positionOS.y += posNoise * 10;
                output.positionCS = mul(UNITY_MATRIX_MVP, input.positionOS);
                cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                output.cameraDist = dot(cameraPos, cameraPos);

                return output;
            }

            float DepthOnlyFragment(Varyings input) : SV_Target
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
