using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar above a player body. Reads PlayerHealth and is hidden
/// for the local owner so it does not obstruct first-person view.
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Transform followPoint;
    [SerializeField] private float heightOffset = 2.15f;
    [SerializeField] private Vector2 barSize = new Vector2(90f, 10f);
    [SerializeField] private float worldScale = 0.012f;
    [SerializeField] private float fillLerpSpeed = 8f;
    [SerializeField] private Color fillColor = new Color(0.22f, 0.86f, 0.32f, 0.95f);
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.82f);

    private Canvas canvas;
    private Image fillImage;
    private float displayedFill = 1f;
    private bool built;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (followPoint == null)
            followPoint = transform;

        EnsureUi();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (playerHealth == null || !playerHealth.IsSpawned)
        {
            SetVisible(false);
            return;
        }

        if (playerHealth.IsOwner || playerHealth.IsDead)
        {
            SetVisible(false);
            displayedFill = playerHealth.IsDead ? 0f : 1f;
            return;
        }

        SetVisible(true);
        followPoint.localPosition = new Vector3(0f, heightOffset, 0f);

        float target = playerHealth.MaxHealth > 0
            ? Mathf.Clamp01((float)playerHealth.CurrentHealth / playerHealth.MaxHealth)
            : 0f;
        displayedFill = Mathf.MoveTowards(displayedFill, target, fillLerpSpeed * Time.deltaTime);
        if (fillImage != null)
            fillImage.fillAmount = displayedFill;

        Camera cam = PlayerNetworkSetup.LocalOwnedCamera;
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        Vector3 toCamera = followPoint.position - cam.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            followPoint.rotation = Quaternion.LookRotation(toCamera);
    }

    private void EnsureUi()
    {
        if (built)
            return;

        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("HealthBarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(followPoint, false);
            canvas = canvasObject.GetComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = barSize;
        canvas.transform.localPosition = Vector3.zero;
        canvas.transform.localRotation = Quaternion.identity;
        canvas.transform.localScale = Vector3.one * worldScale;

        Image background = GetOrCreateImage(canvas.transform, "Background", backgroundColor, false);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        fillImage = GetOrCreateImage(canvas.transform, "Fill", fillColor, true);
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(1.5f, 1.5f);
        fillRect.offsetMax = new Vector2(-1.5f, -1.5f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;

        Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        built = true;
    }

    private static Image GetOrCreateImage(Transform parent, string name, Color color, bool filled)
    {
        Transform existing = parent.Find(name);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            image = go.GetComponent<Image>();
        }

        image.color = color;
        if (image.sprite == null)
            image.sprite = UiWhiteSprite.Get();
        image.type = filled ? Image.Type.Filled : Image.Type.Simple;
        return image;
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible)
            canvas.gameObject.SetActive(visible);
    }
}
