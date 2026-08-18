using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies one visual language to every Canvas in the game at runtime.</summary>
public class GameUITheme : MonoBehaviour
{
    private static readonly Color Ink = new(0.035f, 0.05f, 0.09f, 0.96f);
    private static readonly Color Surface = new(0.09f, 0.12f, 0.20f, 0.96f);
    private static readonly Color Raised = new(0.14f, 0.18f, 0.29f, 1f);
    private static readonly Color Gold = new(0.96f, 0.73f, 0.26f, 1f);
    private static readonly Color Ivory = new(0.94f, 0.95f, 0.98f, 1f);
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (canvas.GetComponent<GameUITheme>() == null)
                canvas.gameObject.AddComponent<GameUITheme>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += (_, _) => Apply();
        Apply();
        StartCoroutine(ApplyNextFrame());
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.75f;
        Apply(); // Picks up buttons created by deck/library screens after startup.
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;
        Apply();
    }

    private void Apply()
    {
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            string name = image.gameObject.name.ToLowerInvariant();
            if (name.Contains("commander art") || name.Contains("card art") || name.Contains("card"))
            {
                image.color = Color.white;
                continue;
            }

            Button button = image.GetComponent<Button>();
            if (button != null)
            {
                image.color = Raised;
                StyleButton(button);
                AddGoldOutline(image.gameObject, 2);
            }
            else if (name.Contains("menu") || name.Contains("panel") || name.Contains("background"))
                image.color = Ink;
            else if (image.sprite != null)
                image.color = Surface;
        }

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            string name = text.gameObject.name.ToLowerInvariant();
            bool heading = name.Contains("title") || name.Contains("label") || text.fontSize >= 40;
            text.color = heading ? Gold : Ivory;
            if (heading) text.fontStyle |= FontStyles.Bold;
            AddTextShadow(text.gameObject);
        }

        foreach (TMP_InputField input in GetComponentsInChildren<TMP_InputField>(true))
        {
            Image background = input.targetGraphic as Image;
            if (background == null) continue;
            background.color = new Color(0.06f, 0.08f, 0.14f, 0.98f);
            AddGoldOutline(background.gameObject, 1);
        }
    }

    private static void StyleButton(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 0.9f, 0.48f, 1f);
        colors.pressedColor = new Color(0.7f, 0.48f, 0.14f, 1f);
        colors.selectedColor = new Color(1f, 0.82f, 0.36f, 1f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
    }

    private static void AddGoldOutline(GameObject target, int width)
    {
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
        outline.effectDistance = new Vector2(width, -width);
    }

    private static void AddTextShadow(GameObject target)
    {
        Shadow shadow = target.GetComponent<Shadow>() ?? target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }
}
