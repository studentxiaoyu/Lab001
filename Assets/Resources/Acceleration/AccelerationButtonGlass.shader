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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        // One shared capture for these desktop-only buttons in the Built-in pipeline.
        GrabPass { "_AccelerationButtonBackdrop" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
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
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 grab : TEXCOORD1;
                float4 localPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            sampler2D _AccelerationButtonBackdrop;
            float4 _AccelerationButtonBackdrop_TexelSize;
            fixed4 _Color, _FrostTint, _TextureSampleAdd;
            float4 _ClipRect;
            float _BlurRadius;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.localPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeGrabScreenPos(o.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grab.xy / i.grab.w;
                float2 d = _AccelerationButtonBackdrop_TexelSize.xy * _BlurRadius;
                fixed3 blur = tex2D(_AccelerationButtonBackdrop, uv).rgb * 4;
                blur += tex2D(_AccelerationButtonBackdrop, uv + float2(d.x,0)).rgb * 2;
                blur += tex2D(_AccelerationButtonBackdrop, uv - float2(d.x,0)).rgb * 2;
                blur += tex2D(_AccelerationButtonBackdrop, uv + float2(0,d.y)).rgb * 2;
                blur += tex2D(_AccelerationButtonBackdrop, uv - float2(0,d.y)).rgb * 2;
                blur += tex2D(_AccelerationButtonBackdrop, uv + d).rgb;
                blur += tex2D(_AccelerationButtonBackdrop, uv - d).rgb;
                blur += tex2D(_AccelerationButtonBackdrop, uv + float2(d.x,-d.y)).rgb;
                blur += tex2D(_AccelerationButtonBackdrop, uv + float2(-d.x,d.y)).rgb;
                fixed4 result;
                result.rgb = lerp(blur / 16, _FrostTint.rgb, _FrostTint.a) * i.color.rgb;
                result.a = (tex2D(_MainTex, i.uv) + _TextureSampleAdd).a * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
