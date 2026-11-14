Shader "Acceleration/UI/FrostedButton"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _FrostTint ("Frost tint", Color) = (0.90,0.96,0.97,0.32)
        _BlurRadius ("Blur radius in pixels", Range(0,8)) = 4
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float4 localPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color, _FrostTint, _TextureSampleAdd;
            float4 _ClipRect;
            float _BlurRadius;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.localPosition = v.vertex;
                VertexPositionInputs positions = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = positions.positionCS;
                o.screenPosition = positions.positionNDC;
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.screenPosition.xy / i.screenPosition.w;
                float2 d = rcp(_ScaledScreenParams.xy) * _BlurRadius;
                half3 blur = SampleSceneColor(uv) * 4;
                blur += SampleSceneColor(uv + float2(d.x,0)) * 2;
                blur += SampleSceneColor(uv - float2(d.x,0)) * 2;
                blur += SampleSceneColor(uv + float2(0,d.y)) * 2;
                blur += SampleSceneColor(uv - float2(0,d.y)) * 2;
                blur += SampleSceneColor(uv + d);
                blur += SampleSceneColor(uv - d);
                blur += SampleSceneColor(uv + float2(d.x,-d.y));
                blur += SampleSceneColor(uv + float2(-d.x,d.y));
                half4 result;
                result.rgb = lerp(blur / 16, _FrostTint.rgb, _FrostTint.a) * i.color.rgb;
                result.a = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) + _TextureSampleAdd).a * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, i.localPosition.xy) * step(i.localPosition.xy, _ClipRect.zw);
                result.a *= inside.x * inside.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
