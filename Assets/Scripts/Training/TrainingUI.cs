using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TrainingUI
{
    public static RectTransform Canvas(string name, Transform parent, Vector2 size)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * 0.0015f;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;
        RectTransform rect = (RectTransform)root.transform;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string text,
        Vector2 position, Vector2 size, float fontSize)
    {
        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(parent, false);
        TextMeshProUGUI result = label.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            result.font = font;
        }
        result.text = text;
        result.fontSize = fontSize;
        result.alignment = TextAlignmentOptions.Center;
        result.color = Color.white;
        result.raycastTarget = false;
        result.rectTransform.anchoredPosition = position;
        result.rectTransform.sizeDelta = size;
        return result;
    }
}
