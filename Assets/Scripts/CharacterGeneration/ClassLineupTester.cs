using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dev line-up of every adventurer class def in the <see cref="ClassCatalog"/>: one row per class line (Novice on top),
/// one column per tier, each a live prefab instance playing the real animator, gameplay disabled.
///
/// Controls (Play Mode):
///   SPACE  re-roll every unit's clothes + colours      C  toggle clothes / base body
///   1–5    Idle / Walk / Attack / Hit / Death           A  toggle repeating attacks
/// </summary>
public class ClassLineupTester : MonoBehaviour
{
    [SerializeField] private float columnSpacing = 4f;
    [SerializeField] private float rowSpacing = 3.6f;
    [Tooltip("Seconds between attacks while repeating (A)")]
    [SerializeField] private float attackEvery = 1.4f;

    private static readonly string[] States = { "Idle", "Walk", "Slash", "Hit", "Death" };

    private readonly List<(AdventurerDef def, GameObject go, Animator anim, CharacterAppearanceManager look)> units = new();
    private bool clothes = true, repeatAttack;
    private float attackTimer;

    void Start()
    {
        var catalog = ClassCatalog.Instance;
        if (catalog == null) { Debug.LogError("[ClassLineupTester] No ClassCatalog in Resources."); return; }

        int lines = System.Enum.GetValues(typeof(ClassLine)).Length;
        foreach (var def in catalog.defs)
        {
            if (def == null || def.prefab == null) continue;
            int row = (int)def.classLine;
            float x = def.classLine == ClassLine.Novice ? 0f : (def.rank - 3.5f) * columnSpacing;   // tiers 2–5 centred, Novice above
            var pos = new Vector3(x, ((lines - 1) * 0.5f - row) * rowSpacing - 1.5f, 0f);

            var go = Instantiate(def.prefab, transform);
            go.name = def.name;
            go.transform.localPosition = pos;
            if (go.TryGetComponent(out EntityBase entity)) entity.enabled = false;
            if (go.TryGetComponent(out Collider2D col2d)) col2d.enabled = false;
            if (go.TryGetComponent(out Rigidbody2D rb)) rb.simulated = false;

            var look = go.GetComponentInChildren<CharacterAppearanceManager>();
            if (look != null) look.SetEntityDef(def);   // before its Start, so the auto-randomise rolls this def's clothes
            units.Add((def, go, CharacterSpriteGenerator.FindBodyAnimator(go), look));
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) foreach (var u in units) { if (u.look == null) continue; u.look.ChangeAllRandom(); u.look.SetClothingVisible(clothes); }
            if (kb.cKey.wasPressedThisFrame) { clothes = !clothes; foreach (var u in units) u.look?.SetClothingVisible(clothes); }
            if (kb.aKey.wasPressedThisFrame) { repeatAttack = !repeatAttack; attackTimer = 0f; }
            for (int i = 0; i < States.Length; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) { repeatAttack = false; PlayAll(States[i]); }
        }

        if (repeatAttack && (attackTimer -= Time.deltaTime) <= 0f) { attackTimer = attackEvery; PlayAll("Slash"); }
    }

    private void PlayAll(string state)
    {
        foreach (var u in units) if (u.anim != null) u.anim.Play(state, 0, 0f);
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };
        foreach (var u in units)
        {
            Vector3 s = cam.WorldToScreenPoint(u.go.transform.position);
            GUI.Label(new Rect(s.x - 80f, Screen.height - s.y + 4f, 160f, 20f), u.def.name, style);
        }
        var help = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
        GUI.Label(new Rect(10, 10, 900, 24),
            $"SPACE re-roll   C clothes ({(clothes ? "on" : "off")})   1 Idle  2 Walk  3 Attack  4 Hit  5 Death   A repeat attack ({(repeatAttack ? "on" : "off")})", help);
    }
}
