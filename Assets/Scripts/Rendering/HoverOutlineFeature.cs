using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>
/// URP 2D renderer feature that draws a single clean outline around the *combined
/// silhouette* of every sprite whose renderingLayerMask includes
/// <see cref="Settings.selectedRenderingLayer"/> — used to highlight the hovered
/// adventurer (a multi-part modular sprite) with ONE outline rather than a per-part one.
///
/// Two passes:
///   1. Mask      — draw the selected sprites into a temp RT with their own shaders,
///                  so the RT's alpha is the true combined silhouette.
///   2. Composite — full-screen over the camera colour: where a texel is empty but a
///                  neighbour is filled, output the outline colour (alpha-blended).
///
/// Add this to Renderer2D.asset, assign a material using Hidden/HoverOutline, and set
/// AdventurerAgent.outlineRenderingLayer to the same bit as `selectedRenderingLayer`.
/// </summary>
public class HoverOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Sprites whose renderingLayerMask includes this bit get outlined. " +
                 "If you add ONE custom URP Rendering Layer, its bit value is 2.")]
        public uint selectedRenderingLayer = 2;

        public Color outlineColor = Color.white;
        [Range(1f, 8f)] public float outlineWidthPixels = 2f;

        [Tooltip("Material using the Hidden/HoverOutline shader.")]
        public Material outlineMaterial;
    }

    public Settings settings = new Settings();
    private HoverOutlinePass _pass;

    public override void Create()
    {
        _pass = new HoverOutlinePass(settings) { renderPassEvent = settings.renderPassEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.outlineMaterial == null) return;
        renderer.EnqueuePass(_pass);
    }

    // ──────────────────────────────────────────────────────────────────────────
    private class HoverOutlinePass : ScriptableRenderPass
    {
        // 2D sprites render under the "Universal2D" light-mode; the others are
        // belt-and-braces in case a custom sprite material uses a different tag.
        private static readonly List<ShaderTagId> s_SpriteTags = new()
        {
            new ShaderTagId("Universal2D"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidthPixels");

        private readonly Settings _s;
        public HoverOutlinePass(Settings s) { _s = s; }

        private class MaskData { public RendererListHandle list; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resource = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            // Temp silhouette mask, camera-sized, colour only.
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_HoverOutlineMask", false);

            // ── Pass 1: draw the selected sprites into the mask ──
            using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Hover Outline Mask", out var data))
            {
                var draw = RenderingUtils.CreateDrawingSettings(
                    s_SpriteTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var filter = new FilteringSettings(RenderQueueRange.all)
                {
                    renderingLayerMask = _s.selectedRenderingLayer
                };
                data.list = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, draw, filter));

                builder.UseRendererList(data.list);
                builder.SetRenderAttachment(mask, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((MaskData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                    ctx.cmd.DrawRendererList(d.list);
                });
            }

            // ── Pass 2: composite the outline over the camera colour (URP 6 material blit) ──
            // The mask is bound as _BlitTexture; the shader reads it and alpha-blends the
            // outline over the (loaded) camera colour via its Blend SrcAlpha OneMinusSrcAlpha.
            _s.outlineMaterial.SetColor(OutlineColorID, _s.outlineColor);
            _s.outlineMaterial.SetFloat(OutlineWidthID, _s.outlineWidthPixels);
            var blit = new RenderGraphUtils.BlitMaterialParameters(mask, resource.activeColorTexture, _s.outlineMaterial, 0);
            renderGraph.AddBlitPass(blit, "Hover Outline Composite");
        }
    }
}
