using UnityEngine;

[DefaultExecutionOrder(10000)] // render late
public class CustomerWantDebug : MonoBehaviour
{
    [Header("Refs")]
    public CustomerAgent customer;        // auto-get if null
    public Camera worldCamera;            // auto-get if null (Camera.main)

    [Header("Positioning")]
    public Vector3 worldOffset = new Vector3(0f, 1.2f, 0f); // above head
    public Vector2 padding = new Vector2(6f, 2f);

    [Header("Style")]
    public int fontSize = 13;
    public Color textColor = Color.white;
    public Color bgColor = new Color(0f, 0f, 0f, 0.6f);
    public bool showState = false; // optional: also show FSM state

    // Global toggle (press F3) if you want
    public static bool globallyEnabled = true;

    GUIStyle _style;
    Texture2D _bgTex;

    void Awake()
    {
        if (!customer) customer = GetComponent<CustomerAgent>();
        if (!worldCamera) worldCamera = Camera.main;

     

        _bgTex = new Texture2D(1, 1);
        _bgTex.SetPixel(0, 0, bgColor);
        _bgTex.Apply();
    }

    void Update()
    {
        // Keep style in sync if tweaked in inspector at runtime
        if (_style != null)
        {
            _style.fontSize = fontSize;
            _style.normal.textColor = textColor;
        }

        // Update background if color changed
        if (_bgTex != null)
        {
            var c = _bgTex.GetPixel(0, 0);
            if (c != bgColor)
            {
                _bgTex.SetPixel(0, 0, bgColor);
                _bgTex.Apply();
            }
        }
    }

    void OnGUI()
    {
        _style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            normal = { textColor = textColor }
        };
        
       
        if (!globallyEnabled || customer == null || worldCamera == null) return;

        // Build the label text
        string line;
        if (customer.desiredItem != null && customer.desiredQty > 0)
            line = $"{customer.desiredItem.displayName} x{customer.desiredQty}";
        else
            line = "—";

        if (showState)
            line = $"{line}\n<color=#BBBBBB>{customer.State}</color>";

        // World → Screen
        Vector3 world = customer.transform.position + worldOffset;
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        if (screen.z < 0f) return; // behind camera

        // Flip Y for GUI space
        float x = screen.x;
        float y = Screen.height - screen.y;

        // Measure and center
        Vector2 size = _style.CalcSize(new GUIContent(line));
        size += padding;
        Rect rect = new Rect(x - size.x * 0.5f, y - size.y, size.x, size.y);

        // Background
        if (_bgTex)
        {
            var oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, _bgTex);
            GUI.color = oldColor;
        }

        // Text
        GUI.Label(rect, line, _style);
    }

    void OnDisable()
    {
        // Prevent ghost GUI if object is pooled and disabled
        // (OnGUI won’t run when disabled, this is just tidy)
    }
}
