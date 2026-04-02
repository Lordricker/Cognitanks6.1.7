Shader "Custom/TankPaint"
{
    Properties
    {
        // Paint layer
        _BaseMap             ("Paint Albedo",                2D)           = "white" {}
        _BaseColor           ("Paint Tint Color",            Color)        = (1,1,1,1)
        [Normal] _BumpMap    ("Paint Normal Map",            2D)           = "bump"  {}
        _BumpScale           ("Paint Normal Scale",          Float)        = 1.0
        _MetallicGlossMap    ("Paint Metallic(R) Smooth(A)", 2D)           = "white" {}
        _Metallic            ("Paint Metallic",              Range(0,1))   = 0.0
        _Smoothness          ("Paint Smoothness",            Range(0,1))   = 0.5
        _ParallaxMap         ("Paint Height Map",            2D)           = "black" {}
        _Parallax            ("Paint Height Scale",          Range(0,0.1)) = 0.02

        // Rust layer  –  two separate textures so each has its own Tiling control:
        //   _RustTex  : tileable rust color (set Tiling to match Blender Mapping node scale)
        //   _RustMask : UV-space painted mask, keep Tiling at 1,1
        _RustTex             ("Rust Albedo (tileable)",        2D)           = "black" {}
        _RustMask            ("Rust Mask (UV-space painted)",  2D)           = "black" {}
        [Normal] _RustNormalMap ("Rust Normal Map",            2D)           = "bump"  {}
        _RustNormalScale     ("Rust Normal Scale",             Float)        = 1.0
        _RustMetallicMap     ("Rust Metallic(R) Smooth(A)",    2D)           = "white" {}
        _RustMetallic        ("Rust Metallic",                 Range(0,1))   = 0.0
        _RustSmoothness      ("Rust Smoothness",               Range(0,1))   = 0.8
        _RustHeightMap       ("Rust Height Map",               2D)           = "black" {}
        _RustParallax        ("Rust Height Scale",             Range(0,0.1)) = 0.02
        _RustBlendStrength   ("Rust Blend Strength",           Range(0,5))   = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"            = "Opaque"
            "RenderPipeline"        = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue"                 = "Geometry"
        }
        LOD 300

        // ── FORWARD LIT ───────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_ParallaxMap);      SAMPLER(sampler_ParallaxMap);
            TEXTURE2D(_RustTex);          SAMPLER(sampler_RustTex);
            TEXTURE2D(_RustMask);         SAMPLER(sampler_RustMask);
            TEXTURE2D(_RustNormalMap);    SAMPLER(sampler_RustNormalMap);
            TEXTURE2D(_RustMetallicMap);  SAMPLER(sampler_RustMetallicMap);
            TEXTURE2D(_RustHeightMap);    SAMPLER(sampler_RustHeightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _MetallicGlossMap_ST;
                float4 _ParallaxMap_ST;
                float4 _RustTex_ST;
                float4 _RustMask_ST;
                float4 _RustNormalMap_ST;
                float4 _RustMetallicMap_ST;
                float4 _RustHeightMap_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _Parallax;
                float  _RustNormalScale;
                float  _RustMetallic;
                float  _RustSmoothness;
                float  _RustParallax;
                float  _RustBlendStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normInputs.normalWS;
                OUT.tangentWS  = float4(normInputs.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv         = IN.uv;
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            float2 ApplyParallax(float2 uv, float scale, TEXTURE2D_PARAM(hTex, hSmp), float3 viewDirTS)
            {
                float h = SAMPLE_TEXTURE2D(hTex, hSmp, uv).r;
                return uv + normalize(viewDirTS).xy * (h * scale - scale * 0.5);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Tangent basis
                float3 bitangentWS = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                float3 viewDirWS   = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float3 viewDirTS   = float3(
                    dot(viewDirWS, IN.tangentWS.xyz),
                    dot(viewDirWS, bitangentWS),
                    dot(viewDirWS, IN.normalWS));

                // Per-layer UVs with optional parallax
                float2 paintUV = ApplyParallax(TRANSFORM_TEX(IN.uv, _BaseMap), _Parallax,
                                     TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS);
                float2 rustUV  = ApplyParallax(TRANSFORM_TEX(IN.uv, _RustTex), _RustParallax,
                                     TEXTURE2D_ARGS(_RustHeightMap, sampler_RustHeightMap), viewDirTS);

                // Rust color from tileable texture; mask from separate UV-space texture
                half4 rustSample = SAMPLE_TEXTURE2D(_RustTex,  sampler_RustTex,  rustUV);
                float2 maskUV    = TRANSFORM_TEX(IN.uv, _RustMask);
                half  maskValue  = SAMPLE_TEXTURE2D(_RustMask, sampler_RustMask,  maskUV).r;
                half  blend      = saturate(maskValue * _RustBlendStrength);

                // Albedo: paint tinted by color picker, rust color unchanged
                half4 paintSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, paintUV);
                half3 finalAlbedo = lerp(paintSample.rgb * _BaseColor.rgb, rustSample.rgb, blend);

                // Normal maps blended by same factor
                half3 paintNTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap,       sampler_BumpMap,
                        TRANSFORM_TEX(IN.uv, _BumpMap)), _BumpScale);
                half3 rustNTS  = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_RustNormalMap, sampler_RustNormalMap,
                        TRANSFORM_TEX(IN.uv, _RustNormalMap)), _RustNormalScale);
                half3 finalNTS = normalize(lerp(paintNTS, rustNTS, blend));
                half3 finalNWS = TransformTangentToWorld(
                    finalNTS, half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS));

                // Metallic / Smoothness (R=metallic, A=smoothness)
                half4 paintMS = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap,
                                    TRANSFORM_TEX(IN.uv, _MetallicGlossMap));
                half4 rustMS  = SAMPLE_TEXTURE2D(_RustMetallicMap,  sampler_RustMetallicMap,
                                    TRANSFORM_TEX(IN.uv, _RustMetallicMap));
                half finalMetallic   = lerp(paintMS.r * _Metallic,   rustMS.r * _RustMetallic,   blend);
                half finalSmoothness = lerp(paintMS.a * _Smoothness,  rustMS.a * _RustSmoothness, blend);

                // URP PBR lighting
                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalize(finalNWS);
                inputData.viewDirectionWS = viewDirWS;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0,0,0,0);
                #endif
                inputData.fogCoord                = IN.fogFactor;
                inputData.vertexLighting          = half3(0,0,0);
                inputData.bakedGI                 = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = finalAlbedo;
                surfaceData.metallic   = finalMetallic;
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS   = finalNTS;
                surfaceData.occlusion  = 1.0;
                surfaceData.alpha      = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb   = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // ── SHADOW CASTER ─────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _MetallicGlossMap_ST;
                float4 _ParallaxMap_ST;
                float4 _RustTex_ST;
                float4 _RustMask_ST;
                float4 _RustNormalMap_ST;
                float4 _RustMetallicMap_ST;
                float4 _RustHeightMap_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _Parallax;
                float  _RustNormalScale;
                float  _RustMetallic;
                float  _RustSmoothness;
                float  _RustParallax;
                float  _RustBlendStrength;
            CBUFFER_END

            float3 _LightDirection;

            struct ShadowAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVary { float4 positionCS : SV_POSITION; };

            ShadowVary ShadowVert(ShadowAttr IN)
            {
                ShadowVary OUT;
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS  = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normalWS, _LightDirection));
                return OUT;
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ── DEPTH ONLY ────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask 0

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _MetallicGlossMap_ST;
                float4 _ParallaxMap_ST;
                float4 _RustTex_ST;
                float4 _RustMask_ST;
                float4 _RustNormalMap_ST;
                float4 _RustMetallicMap_ST;
                float4 _RustHeightMap_ST;
                float4 _BaseColor;
                float  _BumpScale;
                float  _Metallic;
                float  _Smoothness;
                float  _Parallax;
                float  _RustNormalScale;
                float  _RustMetallic;
                float  _RustSmoothness;
                float  _RustParallax;
                float  _RustBlendStrength;
            CBUFFER_END

            struct DepthAttr { float4 positionOS : POSITION; };
            struct DepthVary { float4 positionCS : SV_POSITION; };

            DepthVary DepthVert(DepthAttr IN)
            {
                DepthVary OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
