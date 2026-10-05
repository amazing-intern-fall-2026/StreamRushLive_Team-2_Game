// Runner_GroundRing.shader
// Ve 1 vong tron sang (ring) ngay tren mesh Quad bang cong thuc khoang cach tu tam UV,
// KHONG can texture co san - tranh phai di tim/g\u00e1n texture tron nhu truoc.
// Cach dung: gan vao 1 Quad nam phang (Rotation X = 90) dat duoi chan Runner.
// 2 thong so chinh: Ring Color (mau), Ring Thickness (do day vanh), con Inner/Outer Radius
// chinh kich thuoc vong tron so voi canh Quad (UV 0..1, tam la 0.5,0.5).
Shader "Custom/Runner_GroundRing"
{
    Properties
    {
        _RingColor ("Ring Color", Color) = (1, 0.85, 0, 1)
        _InnerRadius ("Inner Radius (0-0.5)", Range(0.0, 0.5)) = 0.32
        _OuterRadius ("Outer Radius (0-0.5)", Range(0.0, 0.5)) = 0.42
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "GroundRing"
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha   // Alpha blend thuong - doi sang "Blend One One" neu muon kieu Additive sang hon

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RingColor;
                float _InnerRadius;
                float _OuterRadius;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float dist = distance(IN.uv, float2(0.5, 0.5));

                // Vanh ngoai: mo dan tu trong suot -> hien ro tai _OuterRadius
                float outerMask = smoothstep(_OuterRadius, _OuterRadius - _Softness, dist);
                // Vanh trong: mo dan ve trong suot ben trong _InnerRadius (tao lo rong giua)
                float innerMask = smoothstep(_InnerRadius, _InnerRadius + _Softness, dist);

                float alpha = outerMask * innerMask * _RingColor.a;
                return half4(_RingColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
