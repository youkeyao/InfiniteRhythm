Shader "Custom/Spectrum"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.1, 0.3, 0.6, 1.0)
        _ColorB ("Color B", Color) = (0.3, 0.6, 0.2, 1.0)
        _ColorC ("Color C", Color) = (0.8, 0.7, 0.5, 1.0)
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
            #define SAMPLE_NUM 64

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
            float _Spectrum[SAMPLE_NUM];

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // --------------------------------------------------
            float3 mod(float3 x, float3 y)
            {
                return x - y * floor(x / y);
            }
            float3 mod289(float3 x)
            {
                return x - floor(x / 289.0) * 289.0;
            }
            float4 mod289(float4 x)
            {
                return x - floor(x / 289.0) * 289.0;
            }
            float4 permute(float4 x)
            {
                return mod289(((x*34.0)+1.0)*x);
            }
            float4 taylorInvSqrt(float4 r)
            {
                return (float4)1.79284291400159 - r * 0.85373472095314;
            }
            float3 fade(float3 t)
            {
                return t*t*t*(t*(t*6.0-15.0)+10.0);
            }
            // Classic Perlin noise
            float PerlinNoise(float3 P)
            {
                float3 Pi0 = floor(P); // Integer part for indexing
                float3 Pi1 = Pi0 + (float3)1.0; // Integer part + 1
                Pi0 = mod289(Pi0);
                Pi1 = mod289(Pi1);
                float3 Pf0 = frac(P); // Fractional part for interpolation
                float3 Pf1 = Pf0 - (float3)1.0; // Fractional part - 1.0
                float4 ix = float4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
                float4 iy = float4(Pi0.y, Pi0.y, Pi1.y, Pi1.y);
                float4 iz0 = (float4)Pi0.z;
                float4 iz1 = (float4)Pi1.z;

                float4 ixy = permute(permute(ix) + iy);
                float4 ixy0 = permute(ixy + iz0);
                float4 ixy1 = permute(ixy + iz1);

                float4 gx0 = ixy0 / 7.0;
                float4 gy0 = frac(floor(gx0) / 7.0) - 0.5;
                gx0 = frac(gx0);
                float4 gz0 = (float4)0.5 - abs(gx0) - abs(gy0);
                float4 sz0 = step(gz0, (float4)0.0);
                gx0 -= sz0 * (step((float4)0.0, gx0) - 0.5);
                gy0 -= sz0 * (step((float4)0.0, gy0) - 0.5);

                float4 gx1 = ixy1 / 7.0;
                float4 gy1 = frac(floor(gx1) / 7.0) - 0.5;
                gx1 = frac(gx1);
                float4 gz1 = (float4)0.5 - abs(gx1) - abs(gy1);
                float4 sz1 = step(gz1, (float4)0.0);
                gx1 -= sz1 * (step((float4)0.0, gx1) - 0.5);
                gy1 -= sz1 * (step((float4)0.0, gy1) - 0.5);

                float3 g000 = float3(gx0.x,gy0.x,gz0.x);
                float3 g100 = float3(gx0.y,gy0.y,gz0.y);
                float3 g010 = float3(gx0.z,gy0.z,gz0.z);
                float3 g110 = float3(gx0.w,gy0.w,gz0.w);
                float3 g001 = float3(gx1.x,gy1.x,gz1.x);
                float3 g101 = float3(gx1.y,gy1.y,gz1.y);
                float3 g011 = float3(gx1.z,gy1.z,gz1.z);
                float3 g111 = float3(gx1.w,gy1.w,gz1.w);

                float4 norm0 = taylorInvSqrt(float4(dot(g000, g000), dot(g010, g010), dot(g100, g100), dot(g110, g110)));
                g000 *= norm0.x;
                g010 *= norm0.y;
                g100 *= norm0.z;
                g110 *= norm0.w;

                float4 norm1 = taylorInvSqrt(float4(dot(g001, g001), dot(g011, g011), dot(g101, g101), dot(g111, g111)));
                g001 *= norm1.x;
                g011 *= norm1.y;
                g101 *= norm1.z;
                g111 *= norm1.w;

                float n000 = dot(g000, Pf0);
                float n100 = dot(g100, float3(Pf1.x, Pf0.y, Pf0.z));
                float n010 = dot(g010, float3(Pf0.x, Pf1.y, Pf0.z));
                float n110 = dot(g110, float3(Pf1.x, Pf1.y, Pf0.z));
                float n001 = dot(g001, float3(Pf0.x, Pf0.y, Pf1.z));
                float n101 = dot(g101, float3(Pf1.x, Pf0.y, Pf1.z));
                float n011 = dot(g011, float3(Pf0.x, Pf1.y, Pf1.z));
                float n111 = dot(g111, Pf1);

                float3 fade_xyz = fade(Pf0);
                float4 n_z = lerp(float4(n000, n100, n010, n110), float4(n001, n101, n011, n111), fade_xyz.z);
                float2 n_yz = lerp(n_z.xy, n_z.zw, fade_xyz.y);
                float n_xyz = lerp(n_yz.x, n_yz.y, fade_xyz.x);
                return 2.2 * n_xyz;
            }

            // -----------------------------------------------

            Varyings UnlitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 translation = TransformObjectToWorld(float3(0, 0, 0));
                float noiseScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NoiseScale);
                float noiseRaw = PerlinNoise(translation * noiseScale);
                int spectrumIndex = noiseRaw * (SAMPLE_NUM - 6) + 6;
                float spectrum = _Spectrum[spectrumIndex] * 200;
                input.positionOS.y *= spectrum + 1;
                output.positionCS = mul(UNITY_MATRIX_MVP, input.positionOS);

                float4 colorA = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorA);
                float4 colorB = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorB);
                float4 colorC = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorC);
                float s = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SegmentPoint);
                if (noiseRaw < s)
                {
                    output.color = lerp(colorA, colorB, noiseRaw / s);
                }
                else
                {
                    output.color = lerp(colorB, colorC, (noiseRaw - s) / (1 - s));
                }

                return output;
            }

            half4 UnlitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 color = input.color.rgb;
                half alpha = input.color.a;

                alpha = AlphaDiscard(alpha, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
                color = AlphaModulate(color, alpha);

                InputData inputData = (InputData)0;
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 finalColor = UniversalFragmentUnlit(inputData, color, alpha);
                float emission = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Emission);
                finalColor.rgb = finalColor.rgb + emission * finalColor;
                finalColor.a = OutputAlpha(finalColor.a, IsSurfaceTypeTransparent(UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Surface)));

                return finalColor;
            }

            ENDHLSL
        }
    }
}
