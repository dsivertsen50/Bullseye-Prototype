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
    private enum MenuPage
    {
        Pause,
        Settings,
        Controls
    }

    [SerializeField] private InputActionReference pauseAction;
    [SerializeField] private LocalPlayerMenuState menuState;
    [SerializeField] private MenuAudioController menuAudio;

    private Canvas canvas;
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject controlsPanel;
    private Selectable resumeButton;
    private Selectable settingsButton;
    private Selectable controlsButton;
    private Selectable exitButton;
    private Selectable settingsBackButton;
    private Selectable controlsBackButton;
    private bool ownerMenuEnabled;
    private bool built;
    private MenuPage currentPage = MenuPage.Pause;
    private PlayerHealth playerHealth;
    private InputAction resolvedPauseAction;
    private InputAction cancelAction;
    private InputAction navigateAction;
    private Slider mouseXSlider;
    private Slider mouseYSlider;
    private Slider controllerXSlider;
    private Slider controllerYSlider;
    private Slider aimSlider;
    private Slider masterVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider brightnessSlider;
    private Toggle invertToggle;
    private bool suppressUiCallbacks;
    private bool suppressNavigateSound;
    private int lastBackFrame = -1;
    private GameObject lastSelected;
    private LocalPlayerInputBinding inputBinding;
    private InputSystemUIInputModule uiInputModule;

    public override void OnNetworkSpawn()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (menuState == null)
            menuState = GetComponent<LocalPlayerMenuState>();
        if (menuAudio == null)
            menuAudio = GetComponent<MenuAudioController>();
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
            if (menuState != null && menuState.IsMenuOpen)
                HandleBack(false);
            else
                OpenMenu();
            return;
        }

        RestoreSelectionIfNeeded();
        TrackSelectionForNavigateSound();
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
        ShowPausePanel(false);
        SetMenuVisible(true, true);
    }

    public void CloseMenu()
    {
        if (menuState != null)
            menuState.SetMenuOpen(false);

        currentPage = MenuPage.Pause;
        lastSelected = null;
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

        HandleBack(true);
    }

    private void HandleBack(bool fromCancel)
    {
        if (Time.frameCount == lastBackFrame)
            return;

        lastBackFrame = Time.frameCount;

        if (currentPage == MenuPage.Settings || currentPage == MenuPage.Controls)
        {
            if (menuAudio != null)
                menuAudio.PlayBack();
            ShowPausePanel(false);
            return;
        }

        if (fromCancel && menuAudio != null)
            menuAudio.PlaySelect();

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

    private void ShowPausePanel(bool playSelectSound)
    {
        if (playSelectSound && menuAudio != null)
            menuAudio.PlaySelect();

        currentPage = MenuPage.Pause;
        if (pausePanel != null)
            pausePanel.SetActive(true);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
        SelectControl(resumeButton);
    }

    private void ShowSettingsPanel()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();

        currentPage = MenuPage.Settings;
        RefreshSettingsWidgets();
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
        SelectControl(masterVolumeSlider);
    }

    private void ShowControlsPanel()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();

        currentPage = MenuPage.Controls;
        RefreshSettingsWidgets();
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(true);
        SelectControl(aimSlider);
    }

    private void ReturnToPauseFromSubmenu()
    {
        if (menuAudio != null)
            menuAudio.PlayBack();

        ShowPausePanel(false);
    }

    private void SelectControl(Selectable selectable)
    {
        suppressNavigateSound = true;
        lastSelected = selectable != null ? selectable.gameObject : null;

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

        SelectControl(DefaultSelectableForCurrentPage());
    }

    private Selectable DefaultSelectableForCurrentPage()
    {
        switch (currentPage)
        {
            case MenuPage.Settings:
                return masterVolumeSlider;
            case MenuPage.Controls:
                return aimSlider;
            default:
                return resumeButton;
        }
    }

    private void TrackSelectionForNavigateSound()
    {
        if (menuState == null || !menuState.IsMenuOpen || EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == lastSelected)
            return;

        GameObject previous = lastSelected;
        lastSelected = selected;

        if (suppressNavigateSound)
        {
            suppressNavigateSound = false;
            return;
        }

        if (previous == null || selected == null || !selected.activeInHierarchy)
            return;

        if (menuAudio != null)
            menuAudio.PlayNavigate();
    }

    private void RefreshSettingsWidgets()
    {
        suppressUiCallbacks = true;
        SetSlider(masterVolumeSlider, PlayerGameSettings.MasterVolume);
        SetSlider(sfxVolumeSlider, PlayerGameSettings.SfxVolume);
        SetSlider(musicVolumeSlider, PlayerGameSettings.MusicVolume);
        SetSlider(brightnessSlider, PlayerGameSettings.Brightness);
        SetSlider(mouseXSlider, PlayerGameSettings.MouseSensitivityX);
        SetSlider(mouseYSlider, PlayerGameSettings.MouseSensitivityY);
        SetSlider(controllerXSlider, PlayerGameSettings.ControllerSensitivityX);
        SetSlider(controllerYSlider, PlayerGameSettings.ControllerSensitivityY);
        SetSlider(aimSlider, PlayerGameSettings.AimSensitivityMultiplier);
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

        pausePanel = CreatePanel(canvasObject.transform, "PausePanel", new Vector2(640f, 520f));
        CreateLabel(pausePanel.transform, "Title", "PAUSED", 48, new Vector2(0f, 170f), new Vector2(520f, 64f));
        resumeButton = CreateButton(pausePanel.transform, "Resume", "Resume", new Vector2(0f, 80f), OpenFromResume);
        settingsButton = CreateButton(pausePanel.transform, "Settings", "Settings", new Vector2(0f, 10f), ShowSettingsPanel);
        controlsButton = CreateButton(pausePanel.transform, "Controls", "Controls", new Vector2(0f, -60f), ShowControlsPanel);
        exitButton = CreateButton(pausePanel.transform, "Exit", "Exit Game", new Vector2(0f, -130f), QuitFromMenu);

        settingsPanel = CreatePanel(canvasObject.transform, "SettingsPanel", new Vector2(640f, 560f));
        settingsPanel.SetActive(false);
        CreateLabel(settingsPanel.transform, "SettingsTitle", "SETTINGS", 40, new Vector2(0f, 210f), new Vector2(640f, 56f));

        masterVolumeSlider = CreateLabeledSlider(settingsPanel.transform, "Master Volume", 140f, 0f, 1f, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetMasterVolume(value);
            PlaySliderSound();
        });
        sfxVolumeSlider = CreateLabeledSlider(settingsPanel.transform, "Sound Effects", 70f, 0f, 1f, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetSfxVolume(value);
            PlaySliderSound();
        });
        musicVolumeSlider = CreateLabeledSlider(settingsPanel.transform, "Music Volume", 0f, 0f, 1f, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetMusicVolume(value);
            PlaySliderSound();
        });
        brightnessSlider = CreateLabeledSlider(settingsPanel.transform, "Brightness", -70f, PlayerGameSettings.MinBrightness, PlayerGameSettings.MaxBrightness, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetBrightness(value);
            PlaySliderSound();
        });
        settingsBackButton = CreateButton(settingsPanel.transform, "Back", "Back", new Vector2(0f, -170f), ReturnToPauseFromSubmenu);

        controlsPanel = CreatePanel(canvasObject.transform, "ControlsPanel", new Vector2(640f, 720f));
        controlsPanel.SetActive(false);
        CreateLabel(controlsPanel.transform, "ControlsTitle", "CONTROLS", 40, new Vector2(0f, 300f), new Vector2(640f, 56f));
        aimSlider = CreateLabeledSlider(controlsPanel.transform, "Aim Sensitivity", 220f, 0.1f, 1f, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetAimSensitivity(value);
            PlaySliderSound();
        });
        mouseXSlider = CreateLabeledSlider(controlsPanel.transform, "Mouse X Sensitivity", 150f, 0.2f, 12f, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetMouseSensitivity(value, PlayerGameSettings.MouseSensitivityY);
            PlaySliderSound();
        });
        mouseYSlider = CreateLabeledSlider(controlsPanel.transform, "Mouse Y Sensitivity", 80f, 0.2f, 12f, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetMouseSensitivity(PlayerGameSettings.MouseSensitivityX, value);
            PlaySliderSound();
        });
        controllerXSlider = CreateLabeledSlider(controlsPanel.transform, "Controller X Sensitivity", 10f, PlayerGameSettings.MinControllerSensitivity, PlayerGameSettings.MaxControllerSensitivity, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetControllerSensitivity(value, PlayerGameSettings.ControllerSensitivityY);
            PlaySliderSound();
        });
        controllerYSlider = CreateLabeledSlider(controlsPanel.transform, "Controller Y Sensitivity", -60f, PlayerGameSettings.MinControllerSensitivity, PlayerGameSettings.MaxControllerSensitivity, value =>
        {
            if (suppressUiCallbacks)
                return;
            PlayerGameSettings.SetControllerSensitivity(PlayerGameSettings.ControllerSensitivityX, value);
            PlaySliderSound();
        });
        invertToggle = CreateToggle(controlsPanel.transform, "Invert Y", -130f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetInvertY(value);
        });
        controlsBackButton = CreateButton(controlsPanel.transform, "Back", "Back", new Vector2(0f, -220f), ReturnToPauseFromSubmenu);

        WireNavigation();
        built = true;
    }

    private void PlaySliderSound()
    {
        if (menuAudio != null)
            menuAudio.PlaySliderAdjust();
    }

    private void WireNavigation()
    {
        SetVerticalNav(resumeButton, exitButton, settingsButton);
        SetVerticalNav(settingsButton, resumeButton, controlsButton);
        SetVerticalNav(controlsButton, settingsButton, exitButton);
        SetVerticalNav(exitButton, controlsButton, resumeButton);

        SetVerticalNav(masterVolumeSlider, settingsBackButton, sfxVolumeSlider);
        SetVerticalNav(sfxVolumeSlider, masterVolumeSlider, musicVolumeSlider);
        SetVerticalNav(musicVolumeSlider, sfxVolumeSlider, brightnessSlider);
        SetVerticalNav(brightnessSlider, musicVolumeSlider, settingsBackButton);
        SetVerticalNav(settingsBackButton, brightnessSlider, masterVolumeSlider);

        SetVerticalNav(aimSlider, controlsBackButton, mouseXSlider);
        SetVerticalNav(mouseXSlider, aimSlider, mouseYSlider);
        SetVerticalNav(mouseYSlider, mouseXSlider, controllerXSlider);
        SetVerticalNav(controllerXSlider, mouseYSlider, controllerYSlider);
        SetVerticalNav(controllerYSlider, controllerXSlider, invertToggle);
        SetVerticalNav(invertToggle, controllerYSlider, controlsBackButton);
        SetVerticalNav(controlsBackButton, invertToggle, aimSlider);
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
        if (menuAudio != null)
            menuAudio.PlaySelect();
        CloseMenu();
    }

    private void QuitFromMenu()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();
        QuitGame();
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        Image panel = CreateImage(parent, name, new Color(0.08f, 0.09f, 0.12f, 0.94f));
        panel.rectTransform.sizeDelta = size;
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
