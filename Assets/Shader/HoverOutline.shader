Shader "Hidden/HoverOutline"
{
    // Full-screen composite for HoverOutlineFeature. Reads a silhouette mask (bound by
    // Blitter as _BlitTexture) and draws the outline colour on the ring just OUTSIDE the
    // silhouette, alpha-blended over whatever is already in the target.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "HoverOutlineComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Core.hlsl first — defines TEXTURE2D_X and the texture/sampler macros that
            // Blit.hlsl relies on. Blit.hlsl then provides Vert, Varyings (with .texcoord),
            // _BlitTexture, sampler_LinearClamp, and _BlitTexture_TexelSize.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float  _OutlineWidthPixels;

            float SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).a;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Inside the silhouette → draw nothing (outline sits outside).
                if (SampleMask(uv) > 0.1)
                    return half4(0.0, 0.0, 0.0, 0.0);

                float2 t = _BlitTexture_TexelSize.xy * _OutlineWidthPixels;

                float n = 0.0;
                n = max(n, SampleMask(uv + float2( t.x, 0.0)));
                n = max(n, SampleMask(uv + float2(-t.x, 0.0)));
                n = max(n, SampleMask(uv + float2(0.0,  t.y)));
                n = max(n, SampleMask(uv + float2(0.0, -t.y)));
                n = max(n, SampleMask(uv + float2( t.x,  t.y)));
                n = max(n, SampleMask(uv + float2(-t.x, -t.y)));
                n = max(n, SampleMask(uv + float2( t.x, -t.y)));
                n = max(n, SampleMask(uv + float2(-t.x,  t.y)));

                float edge = step(0.1, n);             // a neighbour is filled → we're on the ring
                return half4(_OutlineColor.rgb, _OutlineColor.a * edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
