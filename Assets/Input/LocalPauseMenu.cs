using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Owner-only pause and settings UI. Disables local gameplay input only;
/// never sets Time.timeScale. Menu navigation is bound to the assigned
/// local player's devices rather than Gamepad.current.
/// </summary>
public class LocalPauseMenu : NetworkBehaviour
{
    [SerializeField] private InputActionReference pauseAction;
    [SerializeField] private LocalPlayerMenuState menuState;

    private Canvas canvas;
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private Selectable resumeButton;
    private Selectable settingsButton;
    private Selectable exitButton;
    private Selectable backButton;
    private bool ownerMenuEnabled;
    private bool built;
    private bool viewingSettings;
    private PlayerHealth playerHealth;
    private InputAction resolvedPauseAction;
    private InputAction cancelAction;
    private InputAction navigateAction;
    private Slider mouseXSlider;
    private Slider mouseYSlider;
    private Slider controllerXSlider;
    private Slider controllerYSlider;
    private Slider aimSlider;
    private Slider volumeSlider;
    private Toggle invertToggle;
    private bool suppressUiCallbacks;
    private LocalPlayerInputBinding inputBinding;
    private InputSystemUIInputModule uiInputModule;

    public override void OnNetworkSpawn()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (menuState == null)
            menuState = GetComponent<LocalPlayerMenuState>();
        TryGetComponent(out inputBinding);

        ownerMenuEnabled = IsOwner;
        if (!ownerMenuEnabled)
        {
            enabled = false;
            return;
        }

        ConfigureEventSystem();
        EnsureUi();
        EnablePauseAction();
        BindUiActions();
        SetMenuVisible(false, false);
    }

    public override void OnNetworkDespawn()
    {
        UnbindUiActions();
        if (ownerMenuEnabled)
            CloseMenu();
        ownerMenuEnabled = false;
    }

    private void OnEnable()
    {
        EnablePauseAction();
        BindUiActions();
    }

    private void OnDisable()
    {
        UnbindUiActions();
    }

    private void Update()
    {
        if (!ownerMenuEnabled)
            return;

        if (playerHealth != null && playerHealth.IsDead && menuState != null && menuState.IsMenuOpen)
        {
            CloseMenu();
            return;
        }

        if (PauseInput != null && PauseInput.WasPressedThisFrame())
        {
            TogglePause();
            return;
        }

        RestoreSelectionIfNeeded();
    }

    public void TogglePause()
    {
        if (!ownerMenuEnabled || menuState == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (menuState.IsMenuOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        if (!ownerMenuEnabled || menuState == null)
            return;

        menuState.SetMenuOpen(true);
        SetGameplayOrMenuInput(true);
        ShowPausePanel();
        SetMenuVisible(true, true);
    }

    public void CloseMenu()
    {
        if (menuState != null)
            menuState.SetMenuOpen(false);

        viewingSettings = false;
        SetMenuVisible(false, false);
        SetGameplayOrMenuInput(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private InputAction PauseInput
    {
        get
        {
            if (resolvedPauseAction != null)
                return resolvedPauseAction;

            if (pauseAction != null && pauseAction.action != null)
            {
                resolvedPauseAction = pauseAction.action;
                return resolvedPauseAction;
            }

            if (inputBinding != null && inputBinding.PlayerActions != null)
                resolvedPauseAction = inputBinding.PlayerActions.FindAction("Pause");

            return resolvedPauseAction;
        }
    }

    private void EnablePauseAction()
    {
        InputAction action = PauseInput;
        if (action != null)
            action.Enable();
    }

    private void SetGameplayOrMenuInput(bool menuOpen)
    {
        if (uiInputModule != null)
            uiInputModule.enabled = menuOpen;

        if (inputBinding != null)
            inputBinding.SetMenuInputActive(menuOpen);
        else
            EnablePauseAction();
    }

    private void BindUiActions()
    {
        if (!ownerMenuEnabled || cancelAction != null)
            return;

        if (inputBinding == null || inputBinding.PlayerActions == null)
            return;

        InputActionMap uiMap = inputBinding.PlayerActions.FindActionMap("UI");
        if (uiMap == null)
            return;

        cancelAction = uiMap.FindAction("Cancel");
        navigateAction = uiMap.FindAction("Navigate");
        if (cancelAction != null)
            cancelAction.performed += OnCancelPerformed;
    }

    private void UnbindUiActions()
    {
        if (cancelAction != null)
            cancelAction.performed -= OnCancelPerformed;

        cancelAction = null;
        navigateAction = null;
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (!ownerMenuEnabled || menuState == null || !menuState.IsMenuOpen)
            return;

        if (viewingSettings)
            ShowPausePanel();
        else
            CloseMenu();
    }

    private void SetMenuVisible(bool visible, bool selectDefault)
    {
        if (canvas != null)
            canvas.gameObject.SetActive(visible);

        if (visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (selectDefault)
                SelectControl(resumeButton);
        }
        else
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ShowPausePanel()
    {
        viewingSettings = false;
        if (pausePanel != null)
            pausePanel.SetActive(true);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        SelectControl(resumeButton);
    }

    private void ShowSettingsPanel()
    {
        viewingSettings = true;
        RefreshSettingsWidgets();
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        SelectControl(mouseXSlider);
    }

    private void SelectControl(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    private void RestoreSelectionIfNeeded()
    {
        if (menuState == null || !menuState.IsMenuOpen || EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
            return;

        bool wantsSelection = HasAssignedGamepad();
        if (!wantsSelection && navigateAction != null)
            wantsSelection = navigateAction.ReadValue<Vector2>().sqrMagnitude > 0.25f;

        if (!wantsSelection)
            return;

        if (viewingSettings)
            SelectControl(mouseXSlider);
        else
            SelectControl(resumeButton);
    }

    private void RefreshSettingsWidgets()
    {
        suppressUiCallbacks = true;
        SetSlider(mouseXSlider, PlayerGameSettings.MouseSensitivityX);
        SetSlider(mouseYSlider, PlayerGameSettings.MouseSensitivityY);
        SetSlider(controllerXSlider, PlayerGameSettings.ControllerSensitivityX);
        SetSlider(controllerYSlider, PlayerGameSettings.ControllerSensitivityY);
        SetSlider(aimSlider, PlayerGameSettings.AimSensitivityMultiplier);
        SetSlider(volumeSlider, PlayerGameSettings.MasterVolume);
        if (invertToggle != null)
            invertToggle.isOn = PlayerGameSettings.InvertY;
        suppressUiCallbacks = false;
    }

    private static void SetSlider(Slider slider, float value)
    {
        if (slider != null)
            slider.value = value;
    }

    private void EnsureUi()
    {
        if (built)
            return;

        GameObject canvasObject = new GameObject("LocalPauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image dim = CreateImage(canvasObject.transform, "Dimmer", new Color(0.02f, 0.02f, 0.04f, 0.72f));
        Stretch(dim.rectTransform);

        pausePanel = CreatePanel(canvasObject.transform, "PausePanel");
        CreateLabel(pausePanel.transform, "Title", "PAUSED", 48, new Vector2(0f, 150f), new Vector2(520f, 64f));
        resumeButton = CreateButton(pausePanel.transform, "Resume", "Resume", new Vector2(0f, 50f), OpenFromResume);
        settingsButton = CreateButton(pausePanel.transform, "Settings", "Settings", new Vector2(0f, -20f), ShowSettingsPanel);
        exitButton = CreateButton(pausePanel.transform, "Exit", "Exit Game", new Vector2(0f, -90f), QuitGame);

        settingsPanel = CreatePanel(canvasObject.transform, "SettingsPanel");
        settingsPanel.SetActive(false);
        CreateLabel(settingsPanel.transform, "SettingsTitle", "SETTINGS", 40, new Vector2(0f, 300f), new Vector2(640f, 56f));

        mouseXSlider = CreateLabeledSlider(settingsPanel.transform, "Mouse X Sensitivity", 210f, 0.2f, 12f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetMouseSensitivity(value, PlayerGameSettings.MouseSensitivityY);
        });
        mouseYSlider = CreateLabeledSlider(settingsPanel.transform, "Mouse Y Sensitivity", 150f, 0.2f, 12f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetMouseSensitivity(PlayerGameSettings.MouseSensitivityX, value);
        });
        controllerXSlider = CreateLabeledSlider(settingsPanel.transform, "Controller X Sensitivity", 90f, PlayerGameSettings.MinControllerSensitivity, PlayerGameSettings.MaxControllerSensitivity, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetControllerSensitivity(value, PlayerGameSettings.ControllerSensitivityY);
        });
        controllerYSlider = CreateLabeledSlider(settingsPanel.transform, "Controller Y Sensitivity", 30f, PlayerGameSettings.MinControllerSensitivity, PlayerGameSettings.MaxControllerSensitivity, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetControllerSensitivity(PlayerGameSettings.ControllerSensitivityX, value);
        });
        aimSlider = CreateLabeledSlider(settingsPanel.transform, "Aim Sensitivity", -30f, 0.1f, 1f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetAimSensitivity(value);
        });
        volumeSlider = CreateLabeledSlider(settingsPanel.transform, "Master Volume", -90f, 0f, 1f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetMasterVolume(value);
        });
        invertToggle = CreateToggle(settingsPanel.transform, "Invert Y", -150f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetInvertY(value);
        });
        backButton = CreateButton(settingsPanel.transform, "Back", "Back", new Vector2(0f, -250f), ShowPausePanel);

        WireNavigation();
        built = true;
    }

    private void WireNavigation()
    {
        SetVerticalNav(resumeButton, exitButton, settingsButton);
        SetVerticalNav(settingsButton, resumeButton, exitButton);
        SetVerticalNav(exitButton, settingsButton, resumeButton);

        SetVerticalNav(mouseXSlider, backButton, mouseYSlider);
        SetVerticalNav(mouseYSlider, mouseXSlider, controllerXSlider);
        SetVerticalNav(controllerXSlider, mouseYSlider, controllerYSlider);
        SetVerticalNav(controllerYSlider, controllerXSlider, aimSlider);
        SetVerticalNav(aimSlider, controllerYSlider, volumeSlider);
        SetVerticalNav(volumeSlider, aimSlider, invertToggle);
        SetVerticalNav(invertToggle, volumeSlider, backButton);
        SetVerticalNav(backButton, invertToggle, mouseXSlider);
    }

    private static void SetVerticalNav(Selectable current, Selectable up, Selectable down)
    {
        if (current == null)
            return;

        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down
        };
        current.navigation = navigation;
    }

    private void OpenFromResume()
    {
        CloseMenu();
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        Image panel = CreateImage(parent, name, new Color(0.08f, 0.09f, 0.12f, 0.94f));
        panel.rectTransform.sizeDelta = new Vector2(640f, 720f);
        panel.rectTransform.anchoredPosition = Vector2.zero;
        return panel.gameObject;
    }

    private static Text CreateLabel(Transform parent, string name, string text, int size, Vector2 position, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = position;
        Text label = go.GetComponent<Text>();
        label.font = ResolveUiFont();
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = text;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Image image = CreateImage(parent, name, new Color(0.16f, 0.18f, 0.24f, 1f));
        image.rectTransform.sizeDelta = new Vector2(280f, 54f);
        image.rectTransform.anchoredPosition = position;

        Button button = image.gameObject.AddComponent<Button>();
        button.colors = MenuColors(new Color(0.16f, 0.18f, 0.24f, 1f));
        button.onClick.AddListener(onClick);
        WirePointerFocus(button);

        CreateLabel(image.transform, "Label", label, 24, Vector2.zero, new Vector2(280f, 54f));
        return button;
    }

    private Slider CreateLabeledSlider(Transform parent, string label, float y, float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
    {
        CreateLabel(parent, label + "Label", label, 18, new Vector2(0f, y + 22f), new Vector2(520f, 24f));

        Image track = CreateImage(parent, label + "Track", new Color(0.12f, 0.12f, 0.14f, 1f));
        track.rectTransform.sizeDelta = new Vector2(420f, 18f);
        track.rectTransform.anchoredPosition = new Vector2(0f, y);

        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.colors = MenuColors(Color.white);

        Image fill = CreateImage(track.transform, "Fill", new Color(0.22f, 0.72f, 0.34f, 1f));
        Stretch(fill.rectTransform);
        Image handle = CreateImage(track.transform, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(18f, 24f);

        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(track.transform, false);
        Stretch(fillArea);
        fill.rectTransform.SetParent(fillArea, false);
        Stretch(fill.rectTransform);

        RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(track.transform, false);
        Stretch(handleArea);
        handle.rectTransform.SetParent(handleArea, false);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.onValueChanged.AddListener(onChanged);
        WirePointerFocus(slider);
        return slider;
    }

    private Toggle CreateToggle(Transform parent, string label, float y, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        CreateLabel(parent, label + "Label", label, 18, new Vector2(-40f, y), new Vector2(200f, 28f));
        Image box = CreateImage(parent, label + "Box", new Color(0.12f, 0.12f, 0.14f, 1f));
        box.rectTransform.sizeDelta = new Vector2(28f, 28f);
        box.rectTransform.anchoredPosition = new Vector2(80f, y);

        Image check = CreateImage(box.transform, "Check", new Color(0.22f, 0.86f, 0.32f, 1f));
        Stretch(check.rectTransform);
        check.rectTransform.offsetMin = new Vector2(4f, 4f);
        check.rectTransform.offsetMax = new Vector2(-4f, -4f);

        Toggle toggle = box.gameObject.AddComponent<Toggle>();
        toggle.graphic = check;
        toggle.targetGraphic = box;
        toggle.colors = MenuColors(new Color(0.12f, 0.12f, 0.14f, 1f));
        toggle.onValueChanged.AddListener(onChanged);
        WirePointerFocus(toggle);
        return toggle;
    }

    private void WirePointerFocus(Selectable selectable)
    {
        EventTrigger trigger = selectable.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (menuState != null && menuState.IsMenuOpen && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        });
        trigger.triggers.Add(entry);
    }

    private static ColorBlock MenuColors(Color normal)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normal;
        colors.highlightedColor = new Color(0.25f, 0.55f, 0.32f, 1f);
        colors.selectedColor = new Color(0.35f, 0.82f, 0.42f, 1f);
        colors.pressedColor = new Color(0.18f, 0.4f, 0.24f, 1f);
        colors.disabledColor = new Color(0.12f, 0.12f, 0.14f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = UiWhiteSprite.Get();
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Font ResolveUiFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            return font;

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null)
            return font;

        string[] names = Font.GetOSInstalledFontNames();
        if (names != null && names.Length > 0)
            return Font.CreateDynamicFontFromOSFont(names[0], 16);

        return null;
    }

    private bool HasAssignedGamepad()
    {
        return inputBinding != null && inputBinding.AssignedGamepad != null;
    }

    private void ConfigureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            uiInputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            uiInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiInputModule == null)
            {
                StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                    Destroy(legacy);
                uiInputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        if (inputBinding != null && inputBinding.PlayerActions != null)
        {
            uiInputModule.actionsAsset = inputBinding.PlayerActions;
            if (menuState == null || !menuState.IsMenuOpen)
                inputBinding.SetMenuInputActive(false);
        }

        uiInputModule.deselectOnBackgroundClick = false;
        uiInputModule.moveRepeatDelay = 0.35f;
        uiInputModule.moveRepeatRate = 0.08f;
        uiInputModule.enabled = menuState != null && menuState.IsMenuOpen;
    }
}
