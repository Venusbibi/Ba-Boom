using TMPro;
using UnityEngine;

public class DominoPopupText2D : MonoBehaviour
{
    [SerializeField] private float floatSpeed = 1.4f;
    [SerializeField] private float duration = 0.85f;
    [SerializeField] private float popScale = 1.35f;

    private TextMeshPro tmp;
    private Color startColor;
    private float elapsed;

    public static void Spawn(Vector2 worldPos, string label, Color color, DominoPopupText2D customPrefab = null, TMP_FontAsset font = null, float fontSize = 6.5f)
    {
        DominoPopupText2D instance;

        if (customPrefab != null)
        {
            instance = Instantiate(customPrefab, new Vector3(worldPos.x, worldPos.y, 0f), Quaternion.identity);
        }
        else
        {
            GameObject go = new GameObject("DominoComboPopup");
            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            instance = go.AddComponent<DominoPopupText2D>();
        }

        instance.Setup(label, color, font, fontSize);
    }

    private void Awake()
    {
        tmp = GetComponent<TextMeshPro>();
        if (tmp == null)
        {
            tmp = gameObject.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 6.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 50;
        }
    }

    public void Setup(string label, Color color, TMP_FontAsset font = null, float fontSize = 6.5f)
    {
        if (font != null)
        {
            tmp.font = font;
        }

        if (fontSize > 0f)
        {
            tmp.fontSize = fontSize;
        }

        tmp.text = label;
        tmp.color = color;
        startColor = color;
        elapsed = 0f;
        transform.localScale = Vector3.one * 0.5f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

        if (t < 0.25f)
        {
            float popT = t / 0.25f;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one * popScale, popT);
        }
        else
        {
            float settleT = (t - 0.25f) / 0.75f;
            transform.localScale = Vector3.Lerp(Vector3.one * popScale, Vector3.one, settleT);
        }

        if (t > 0.45f)
        {
            float fadeT = (t - 0.45f) / 0.55f;
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, fadeT));
        }

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }
}