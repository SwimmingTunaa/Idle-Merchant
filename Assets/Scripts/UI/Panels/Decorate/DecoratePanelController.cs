using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Decorate modal (DecoratePanel.uxml, instanced in MainUI.uxml before StarFanfare) — opened by clicking a swappable
/// guild-hall <see cref="DecorSlot"/> (GUILD_HALL_DESIGN.md "Swapping"). Workshop-panel look; one iron-plate row per
/// variant of the slot's group: pixel-crisp preview, name, and Placed / Place / Buy n seals. X or a backdrop click closes.
/// </summary>
public class DecoratePanelController : BasePanelController
{
    public override string PanelID => "DecoratePanel";

    private const float PreviewBox = 96f;   // px; the sprite is drawn at the largest whole scale that fits

    // UITK clamps the background tint at 1, so an HDR tint (the red banner is the teal crop × 2.5 red) is previewed
    // from a CPU-tinted copy of the crop, made once per variant
    private static readonly Dictionary<DecorVariantDef, Texture2D> tintedPreviews = new();

    private DecorSlot slot;
    private VisualElement rows;
    private Label title;

    void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponentInParent<UIDocument>();
    }

    protected override void Start()
    {
        BuildUI();
        base.Start();
    }

    /// <summary>Open (or retarget, if already open) for the clicked slot.</summary>
    public void OpenFor(DecorSlot s)
    {
        if (s == null || !s.CanDecorate || panel == null || DecorCatalog.Instance == null) return;
        slot = s;
        if (State == PanelState.Open) Rebuild();
        else UIManager.Instance.OpenPanel(this);
    }

    protected override void BuildUI()
    {
        if (uiDocument == null) return;
        panel = uiDocument.rootVisualElement.Q<VisualElement>("decorate-panel");
        if (panel == null)
        {
            Debug.LogError("[DecoratePanelController] 'decorate-panel' not found — is DecoratePanel instanced in MainUI.uxml?");
            return;
        }
        panel.style.display = DisplayStyle.None;
        panel.style.opacity = 0f;
        if (panel.parent != null) panel.parent.pickingMode = PickingMode.Ignore;   // full-screen TemplateContainer

        rows = panel.Q("dp-rows");
        title = panel.Q<Label>("dp-title");
        panel.Q<Button>("dp-close").clicked += () => UIManager.Instance.ClosePanel(this);
        panel.RegisterCallback<ClickEvent>(e => { if (e.target == panel) UIManager.Instance.ClosePanel(this); });   // backdrop
    }

    protected override void OnOpenStart()
    {
        GameSignals.OnSealsChanged += OnSealsChanged;
        Rebuild();
    }

    protected override void OnCloseStart() => GameSignals.OnSealsChanged -= OnSealsChanged;

    protected override void OnDestroy()
    {
        GameSignals.OnSealsChanged -= OnSealsChanged;
        base.OnDestroy();
    }

    private void OnSealsChanged(int _) => Rebuild();

    private void Rebuild()
    {
        if (slot == null) return;
        title.text = slot.DisplayName;
        rows.Clear();
        int seals = ContractManager.Instance != null ? ContractManager.Instance.Seals : 0;
        foreach (var v in DecorCatalog.Instance.ForSlot(slot.DefaultVariant.slot))
            rows.Add(BuildRow(v, seals));
    }

    private VisualElement BuildRow(DecorVariantDef v, int seals)
    {
        var row = new VisualElement();
        row.AddToClassList("dp-row");

        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.AddToClassList("dp-preview");
        if (v.sprite != null)
        {
            var r = v.sprite.rect;
            float k = Mathf.Max(1f, Mathf.Floor(Mathf.Min(PreviewBox / r.width, PreviewBox / r.height)));
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.style.width = r.width * k;
            art.style.height = r.height * k;
            if (v.tint == Color.white) art.style.backgroundImage = new StyleBackground(v.sprite);
            else art.style.backgroundImage = new StyleBackground(TintedPreview(v));
            box.Add(art);
        }
        row.Add(box);

        var name = new Label(v.displayName) { pickingMode = PickingMode.Ignore };
        name.AddToClassList("dp-row__name");
        row.Add(name);

        bool placed = slot.Placed == v, owned = slot.IsOwned(v);
        var button = new Button(() => OnRowAction(v));
        button.AddToClassList("dp-btn");
        button.AddToClassList(placed ? "btn-secondary" : owned ? "btn-primary" : "btn-success");
        button.Add(new Label(placed ? "Placed" : owned ? "Place" : $"Buy {v.price}") { pickingMode = PickingMode.Ignore });
        if (!owned)
        {
            var seal = new VisualElement { pickingMode = PickingMode.Ignore };
            seal.AddToClassList("btn-seal");
            seal.AddToClassList("dp-btn__seal");
            button.Add(seal);
        }
        button.SetEnabled(!placed && (owned || seals >= v.price));
        row.Add(button);
        return row;
    }

    // Same multiply the world SpriteRenderer does (gamma-space; linear colour space gives the same product), unclamped tint
    private static Texture2D TintedPreview(DecorVariantDef v)
    {
        if (tintedPreviews.TryGetValue(v, out var tex) && tex != null) return tex;
        var src = v.sprite.texture;
        var r = v.sprite.textureRect;
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);   // the sheet isn't CPU-readable
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color32((byte)Mathf.Min(255f, px[i].r * v.tint.r), (byte)Mathf.Min(255f, px[i].g * v.tint.g),
                                (byte)Mathf.Min(255f, px[i].b * v.tint.b), px[i].a);
        tex.SetPixels32(px);
        tex.Apply();
        return tintedPreviews[v] = tex;
    }

    // Buy spends seals and hangs it straight away (nobody buys a banner not to hang it); Place swaps the world sprite
    private void OnRowAction(DecorVariantDef v)
    {
        SfxManager.Instance?.Play(SfxId.UiTick);
        if (slot.IsOwned(v) || slot.TryBuy(v)) slot.Place(v);
        Rebuild();
    }
}
