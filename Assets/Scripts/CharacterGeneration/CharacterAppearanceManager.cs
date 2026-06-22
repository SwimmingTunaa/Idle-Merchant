using UnityEngine;
using System.Collections;
using UnityEngine.U2D.Animation;
using System.Linq;
using System;

[Serializable]
public struct CharacterAppearanceIndices
{
    public int pants;
    public int shirt;
    public int hairTop;
    public int hairBack;
    public int frontWeapon;
    public int backWeapon;

    public float skinColour;
    public int shirtColour;
    public int pantsColour;
    public int hairColour;
}


public class CharacterAppearanceManager : MonoBehaviour
{
    [SerializeField] private EntityDef entityDef;
    [SerializeField] private bool autoChangeOnStart = true;
    [SerializeField] private CharacterAppearanceIndices appearanceIndices;

    [Header("Sprite Library References")]
    //References to the Sprite Library component on the character
    [SerializeField] private SpriteLibrary baseReference;
    [SerializeField] private SpriteLibrary shirtReference;
    [SerializeField] private SpriteLibrary pantsReference;
    [SerializeField] private SpriteLibrary hairTopReference;
    [SerializeField] private SpriteLibrary hairBackReference;
    [SerializeField] private SpriteLibrary frontWeaponReference;
    [SerializeField] private SpriteLibrary backWeaponReference;

    [Header("Material References")]
    private Material[] skinMaterials;
    private Material[] shirtMaterials;
    private Material pantsMaterial;
    private Material[] hairMaterials;

    void Awake()
    {
        skinMaterials = baseReference.GetComponentsInChildren<SpriteRenderer>().Select(x => x.material).ToArray();
        shirtMaterials = shirtReference.GetComponentsInChildren<SpriteRenderer>().Select(x => x.material).ToArray();
        pantsMaterial = pantsReference.GetComponentInChildren<SpriteRenderer>().material;
        hairMaterials = hairTopReference.GetComponentsInChildren<SpriteRenderer>().Select(x => x.material).ToArray();
    }

    void Start()
    {
        if (autoChangeOnStart && entityDef.useModularCharacter)
            ChangeAllRandom();
    }

    public void SetEntityDef(EntityDef def)
    {
        entityDef = def;
    }

    public void ChangeRandomShirt()
    {
        if (entityDef.shirtSpriteLibraries.Length == 0) return;
        int randomIndex = UnityEngine.Random.Range(0, entityDef.shirtSpriteLibraries.Length);
        shirtReference.spriteLibraryAsset = entityDef.shirtSpriteLibraries[randomIndex];

        if (shirtMaterials.Length == 0) return;
        Color randomColour = entityDef.ShirtColourPalette.GetRandomPaletteColour();
        for (int i = 0; i < shirtMaterials.Length; i++)
        {
            shirtMaterials[i].SetColor("_New_Colour", randomColour);
        }
    }
    public void ChangeRandomPants()
    {
        if (entityDef.pantsSpriteLibraries.Length == 0) return;
        int randomIndex = UnityEngine.Random.Range(0, entityDef.pantsSpriteLibraries.Length);
        pantsReference.spriteLibraryAsset = entityDef.pantsSpriteLibraries[randomIndex];
        if (entityDef.PantsColourPalette == null) return;
        Color randomColour = entityDef.PantsColourPalette.GetRandomPaletteColour();
        pantsMaterial.SetColor("_New_Colour", randomColour);
    }

    public void ChangeRandomHair()
    {
        if (entityDef.hairTopSpriteLibraries.Length != 0)
        {
            int randomIndexTop = UnityEngine.Random.Range(0, entityDef.hairTopSpriteLibraries.Length);
            hairTopReference.spriteLibraryAsset = entityDef.hairTopSpriteLibraries[randomIndexTop];
        }

        if (entityDef.hairBackSpriteLibraries.Length != 0)
        {
            int randomIndexBack = UnityEngine.Random.Range(0, entityDef.hairBackSpriteLibraries.Length);
            hairBackReference.spriteLibraryAsset = entityDef.hairBackSpriteLibraries[randomIndexBack];
        }

        if (hairMaterials.Length == 0) return;
        Color randomColour = entityDef.HairColourPalette.GetRandomPaletteColour();
        for (int i = 0; i < hairMaterials.Length; i++)
        {
            hairMaterials[i].SetColor("_New_Colour", randomColour);
        }
    }

    public void ChangeRandomSkin()
    {
        // Skin is a Gradient palette — its colourSet is just the default {white}, so the palette
        // path would tint everyone white. Pick from the gradient (fall back to palette for a
        // genuinely palette-typed skin).
        var palette = entityDef.skinColourPalette;
        Color randomColour = palette.colourType == ColourType.Gradient
            ? palette.GetRandomGradientColour()
            : palette.GetRandomPaletteColour();

        for (int i = 0; i < skinMaterials.Length; i++)
        {
            skinMaterials[i].SetColor("_New_Colour", randomColour);
        }
    }

    public void ChangeRandomFrontWeapon()
    {
        if (entityDef.frontWeaponSpriteLibraries.Length == 0) return;
        int randomIndex = UnityEngine.Random.Range(0, entityDef.frontWeaponSpriteLibraries.Length);
        frontWeaponReference.spriteLibraryAsset = entityDef.frontWeaponSpriteLibraries[randomIndex];
    }

    public void ChangeRandomBackWeapon()
    {
        if (entityDef.backWeaponSpriteLibraries.Length == 0) return;
        int randomIndex = UnityEngine.Random.Range(0, entityDef.backWeaponSpriteLibraries.Length);
        backWeaponReference.spriteLibraryAsset = entityDef.backWeaponSpriteLibraries[randomIndex];
    }

    public void ChangeAllRandom()
    {
        ChangeRandomShirt();
        ChangeRandomPants();
        ChangeRandomHair();
        ChangeRandomSkin();
        ChangeRandomFrontWeapon();
        ChangeRandomBackWeapon();
    }

    public void SetAppearanceIndices(CharacterAppearanceIndices indices)
    {
        appearanceIndices = indices;
    }

    public CharacterAppearanceIndices GetCurrentIndices() => appearanceIndices;

    public void ApplyAppearance()
    {
        if (entityDef.shirtSpriteLibraries?.Length > 0)
            shirtReference.spriteLibraryAsset = entityDef.shirtSpriteLibraries[appearanceIndices.shirt];
        if (entityDef.pantsSpriteLibraries?.Length > 0)
            pantsReference.spriteLibraryAsset = entityDef.pantsSpriteLibraries[appearanceIndices.pants];
        if (entityDef.hairTopSpriteLibraries?.Length > 0)
            hairTopReference.spriteLibraryAsset = entityDef.hairTopSpriteLibraries[appearanceIndices.hairTop];
        if (entityDef.hairBackSpriteLibraries?.Length > 0)
            hairBackReference.spriteLibraryAsset = entityDef.hairBackSpriteLibraries[appearanceIndices.hairBack];
        if (entityDef.frontWeaponSpriteLibraries?.Length > 0)
            frontWeaponReference.spriteLibraryAsset = entityDef.frontWeaponSpriteLibraries[appearanceIndices.frontWeapon];
        if (entityDef.backWeaponSpriteLibraries?.Length > 0)
            backWeaponReference.spriteLibraryAsset = entityDef.backWeaponSpriteLibraries[appearanceIndices.backWeapon];

        Color skinColour = entityDef.skinColourPalette != null ? entityDef.skinColourPalette.GetGradientColour(appearanceIndices.skinColour) : Color.white;
        Color shirtColour = entityDef.ShirtColourPalette != null ? entityDef.ShirtColourPalette.GetPaletteColour(appearanceIndices.shirtColour) : Color.white;
        Color pantsColour = entityDef.PantsColourPalette != null ? entityDef.PantsColourPalette.GetPaletteColour(appearanceIndices.pantsColour) : Color.white;
        Color hairColour = entityDef.HairColourPalette != null ? entityDef.HairColourPalette.GetPaletteColour(appearanceIndices.hairColour) : Color.white;

        for (int i = 0; i < skinMaterials.Length; i++)
            skinMaterials[i].SetColor("_New_Colour", skinColour);


        for (int i = 0; i < shirtMaterials.Length; i++)
            shirtMaterials[i].SetColor("_New_Colour", shirtColour);

        pantsMaterial.SetColor("_New_Colour", pantsColour);

        for (int i = 0; i < hairMaterials.Length; i++)
            hairMaterials[i].SetColor("_New_Colour", hairColour);
    }

    /// <summary>Show/hide the clothing layers (shirt, pants, hair, weapons), leaving the base
    /// body visible. Used by the character preview tool to compare dressed vs base body.</summary>
    public void SetClothingVisible(bool visible)
    {
        SetReferenceVisible(shirtReference, visible);
        SetReferenceVisible(pantsReference, visible);
        SetReferenceVisible(hairTopReference, visible);
        SetReferenceVisible(hairBackReference, visible);
        SetReferenceVisible(frontWeaponReference, visible);
        SetReferenceVisible(backWeaponReference, visible);
    }

    private static void SetReferenceVisible(SpriteLibrary reference, bool visible)
    {
        if (reference == null) return;
        foreach (var sr in reference.GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }
}