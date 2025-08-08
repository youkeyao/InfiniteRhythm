Shader "Custom/TreeSwing"
{
    Properties
    {
        _Thickness ("Thickness", RANGE(0, 800)) = 100
		_Smoothness ("Smoothness", RANGE(0, 20)) = 3
		_BaseColor ("Base Color", Color) = (0.0, 0.0, 0.0, 0.0)
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

        Pass
        {
            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Thickness;
                float _Smoothness;
            CBUFFER_END
            float _Clip;
            float _ClipSign;
            float _Swing;

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2g
            {
                float4 positionCS : SV_POSITION;
                float cameraDist : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct g2f
            {
                float4 positionCS : SV_POSITION;
                float cameraDist : TEXCOORD1;
                float3 dist : TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // -----------------------------------------------

            v2g vert(Attributes input)
            {
                v2g output = (v2g)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // swing
                float3 posOS = input.positionOS.xyz;
                float3 cameraForward = UNITY_MATRIX_I_V[2].xyz;
                float3 windDirOS = mul(UNITY_MATRIX_I_M, cross(float3(0, 1, 0), cameraForward));
                float partFactor = sqrt(posOS.x * posOS.x + posOS.z * posOS.z + posOS.y * posOS.y * posOS.y);
                float finalDisplacement = _Swing * partFactor;
                input.positionOS.xyz += windDirOS * finalDisplacement;

                float3 cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                output.cameraDist = dot(cameraPos, cameraPos);
                output.positionCS = mul(UNITY_MATRIX_MVP, input.positionOS);

                return output;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> triStream)
            {
                g2f output = (g2f)0;

                float2 p0 = input[0].positionCS.xy / input[0].positionCS.w;
                float2 p1 = input[1].positionCS.xy / input[1].positionCS.w;
                float2 p2 = input[2].positionCS.xy / input[2].positionCS.w;

                float2 edge0 = p2 - p1;
                float2 edge1 = p2 - p0;
                float2 edge2 = p1 - p0;

                float area = abs(edge1.x * edge2.y - edge1.y * edge2.x);
                float wireThickness = 800 - _Thickness;

                output.positionCS = input[0].positionCS;
                output.cameraDist = input[0].cameraDist;
                output.dist = float3( (area / length(edge0)), 0.0, 0.0) * wireThickness;
                triStream.Append(output);

                output.positionCS = input[1].positionCS;
                output.cameraDist = input[1].cameraDist;
                output.dist = float3(0.0, (area / length(edge1)), 0.0) * wireThickness;
                triStream.Append(output);

                output.positionCS = input[2].positionCS;
                output.cameraDist = input[2].cameraDist;
                output.dist = float3(0.0, 0.0, (area / length(edge2))) * wireThickness;
                triStream.Append(output);

                triStream.RestartStrip();
            }

            half4 frag(g2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_ClipSign * (_Clip - input.cameraDist));

                float minDistanceToEdge = min(input.dist[0], min(input.dist[1], input.dist[2]));
                if (minDistanceToEdge > 0.9)
                {
                    clip(-1);
                    return half4(_BaseColor.rgb, 0);
                }

                float alpha = exp2(_Smoothness * -1.0 * minDistanceToEdge * minDistanceToEdge);
                return half4(_BaseColor.rgb, alpha);
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
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Thickness;
                float _Smoothness;
            CBUFFER_END
            float _Clip;
            float _ClipSign;
            float _Swing;

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2g
            {
                float4 positionCS : SV_POSITION;
                float cameraDist : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct g2f
            {
                float4 positionCS : SV_POSITION;
                float cameraDist : TEXCOORD1;
                float3 dist : TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // -----------------------------------------------

            v2g vert(Attributes input)
            {
                v2g output = (v2g)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // swing
                float3 posOS = input.positionOS.xyz;
                float3 cameraForward = UNITY_MATRIX_I_V[2].xyz;
                float3 windDirOS = mul(UNITY_MATRIX_I_M, cross(float3(0, 1, 0), cameraForward));
                float partFactor = sqrt(posOS.x * posOS.x + posOS.z * posOS.z + posOS.y * posOS.y * posOS.y);
                float finalDisplacement = _Swing * partFactor;
                input.positionOS.xyz += windDirOS * finalDisplacement;

                float3 cameraPos = mul(UNITY_MATRIX_MV, input.positionOS);
                output.cameraDist = dot(cameraPos, cameraPos);
                output.positionCS = mul(UNITY_MATRIX_MVP, input.positionOS);

                return output;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> triStream)
            {
                g2f output = (g2f)0;

                float2 p0 = input[0].positionCS.xy / input[0].positionCS.w;
                float2 p1 = input[1].positionCS.xy / input[1].positionCS.w;
                float2 p2 = input[2].positionCS.xy / input[2].positionCS.w;

                float2 edge0 = p2 - p1;
                float2 edge1 = p2 - p0;
                float2 edge2 = p1 - p0;

                float area = abs(edge1.x * edge2.y - edge1.y * edge2.x);
                float wireThickness = 800 - _Thickness;

                output.positionCS = input[0].positionCS;
                output.cameraDist = input[0].cameraDist;
                output.dist = float3( (area / length(edge0)), 0.0, 0.0) * wireThickness;
                triStream.Append(output);

                output.positionCS = input[1].positionCS;
                output.cameraDist = input[1].cameraDist;
                output.dist = float3(0.0, (area / length(edge1)), 0.0) * wireThickness;
                triStream.Append(output);

                output.positionCS = input[2].positionCS;
                output.cameraDist = input[2].cameraDist;
                output.dist = float3(0.0, 0.0, (area / length(edge2))) * wireThickness;
                triStream.Append(output);

                triStream.RestartStrip();
            }

            float frag(g2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_ClipSign * (_Clip - input.cameraDist));

                float minDistanceToEdge = min(input.dist[0], min(input.dist[1], input.dist[2]));
                if (minDistanceToEdge > 0.9)
                {
                    clip(-1);
                    return half4(_BaseColor.rgb, 0);
                }

                return input.positionCS.z;
            }

            ENDHLSL
        }
    }
}
