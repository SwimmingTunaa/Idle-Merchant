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

            // Largest outline width (in texels) the box dilation supports — matches the feature's
            // Outline Width Pixels Range(1,8). The sample grid is (2*MAX+1)^2 unrolled at compile
            // time, so larger values cost more per pixel; raise only if you need thicker outlines.
            #define OUTLINE_MAX_RADIUS 8

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

                // Square (Chebyshev) dilation: this texel is outline if ANY filled texel sits
                // within the [-r, r] box around it. A box rather than a ring/circle is what gives
                // crisp right-angle corners instead of rounded ones. The loop bounds are a
                // compile-time constant (so it unrolls on Metal); the runtime width just gates
                // which samples count.
                int r = clamp((int)round(_OutlineWidthPixels), 0, OUTLINE_MAX_RADIUS);
                float n = 0.0;
                [unroll]
                for (int yy = -OUTLINE_MAX_RADIUS; yy <= OUTLINE_MAX_RADIUS; yy++)
                {
                    [unroll]
                    for (int xx = -OUTLINE_MAX_RADIUS; xx <= OUTLINE_MAX_RADIUS; xx++)
                    {
                        if (abs(xx) <= r && abs(yy) <= r)
                            n = max(n, SampleMask(uv + _BlitTexture_TexelSize.xy * float2(xx, yy)));
                    }
                }

                float edge = step(0.1, n);             // a neighbour is filled → we're on the outline
                return half4(_OutlineColor.rgb, _OutlineColor.a * edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
