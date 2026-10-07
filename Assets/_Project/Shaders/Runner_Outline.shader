// Runner_Outline.shader
// Outline cho Skinned Mesh bang cach "phinh" vertex ra theo normal NGAY TRONG VERTEX SHADER,
// sau khi Unity da tinh bien dang xuong (skinning). Vi vay KHONG phu thuoc Scale cua GameObject
// chua SkinnedMeshRenderer (ly do Scale 0.0103/0.012 truoc do khong co tac dung gi).
// Cach dung: Create > Shader > tao file nay (hoac import file .shader nay vao Assets/_Project/Shaders/),
// roi Create > Material, chon Shader nay, gan vao ban sao Character_Male_Jacket_OUTLINE
// (giu nguyen Render Face mac dinh - shader nay da tu Cull Front, khong can chinh gi them
// trong Inspector material ngoai 2 thong so Outline Color va Outline Width).
Shader "Custom/Runner_Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0, 1)
        _OutlineWidth ("Outline Width (object space)", Range(0.0, 0.1)) = 0.015
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Cull Front   // Chi ve mat trong lat ra ngoai -> tao hieu ung vien bao quanh
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Khong can khai bao gi them cho Skinned Mesh: Unity tu dong tinh bien dang vertex
            // theo xuong (skinning) TRUOC khi du lieu toi vertex shader nay, nen positionOS/
            // normalOS nhan duoc o day DA la vi tri/huong SAU skinning - shader thuong dung
            // duoc ngay cho ca Skinned Mesh Renderer lan Mesh Renderer tinh, khong phan biet gi.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Phinh vertex ra theo huong normal (object space), do tinh bang object-space units
                // nen khong lien quan gi toi Transform Scale cua GameObject.
                float3 expandedPosOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;

                OUT.positionHCS = TransformObjectToHClip(expandedPosOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
