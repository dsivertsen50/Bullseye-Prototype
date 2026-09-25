using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Player-facing Profile / Career screen. Reads the loaded PlayerProfile
/// through PlayerProfileManager and never writes combat statistics.
/// Additional categories can be added to ProfileCategory without rewriting
/// the menu flow.
/// </summary>
public class PlayerProfileUI : MonoBehaviour
{
    public enum ProfileCategory
    {
        Overview = 0,
        Weapons = 1,
        Bullseye = 2
    }

    private const float MenuPanelAlpha = 0.78f;
    private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, MenuPanelAlpha);

    private WeaponCatalog catalog;
    private MenuAudioController menuAudio;
    private Action onBack;

    private Text displayNameLabel;
    private Text profileIdLabel;
    private InputField displayNameField;
    private Text nameErrorLabel;
    private DisplayNameKeyboardUI nameKeyboard;
    private CanvasGroup profileGroup;
    private Button saveNameButton;
    private Button overviewTab;
    private Button weaponsTab;
    private Button bullseyeTab;
    private Button backButton;
    private GameObject overviewRoot;
    private GameObject weaponsRoot;
    private GameObject bullseyeRoot;
    private ScrollRect overviewScroll;
    private ScrollRect weaponsScroll;
    private ScrollRect bullseyeScroll;
    private Text weaponsEmptyLabel;

    private ProfileStatRow matchesPlayedRow;
    private ProfileStatRow matchesCompletedRow;
    private ProfileStatRow winsRow;
    private ProfileStatRow lossesRow;
    private ProfileStatRow winRateRow;
    private ProfileStatRow eliminationsRow;
    private ProfileStatRow deathsRow;
    private ProfileStatRow assistsRow;
    private ProfileStatRow kdRow;
    private ProfileStatRow shotsFiredRow;
    private ProfileStatRow shotsHitRow;
    private ProfileStatRow accuracyRow;
    private ProfileStatRow damageRow;
    private ProfileStatRow favoriteWeaponRow;
    private ProfileStatRow longestElimRow;
    private ProfileStatRow playTimeRow;

    private ProfileStatRow bullseyeHitsRow;
    private ProfileStatRow bullseyeEliminationsRow;
    private ProfileStatRow attachedEliminationsRow;
    private ProfileStatRow detachedEliminationsRow;
    private ProfileStatRow grenadeDetachmentsRow;
    private ProfileStatRow hitBullseyeRow;
    private ProfileStatRow hitHeadRow;
    private ProfileStatRow hitBodyRow;
    private ProfileStatRow bodySlamRow;
    private ProfileStatRow ricochetRow;
    private ProfileStatRow specialDetachedRow;

    private readonly List<WeaponProfileEntry> weaponEntries = new List<WeaponProfileEntry>();
    private ProfileCategory currentCategory = ProfileCategory.Overview;
    private bool suppressNameCallback;

    public Selectable DefaultSelectable => overviewTab;
    public Selectable BackButton => backButton;

    public static PlayerProfileUI Create(
        Transform canvasParent,
        float leftPadding,
        WeaponCatalog weaponCatalog,
        MenuAudioController audio,
        Action backCallback)
    {
        GameObject panel = MenuUiFactory.CreatePanel(canvasParent, "ProfilePanel", new Vector2(980f, 920f), PanelColor);
        MenuUiFactory.DockLeft(panel, leftPadding);
        panel.SetActive(false);

        PlayerProfileUI ui = panel.AddComponent<PlayerProfileUI>();
        ui.profileGroup = panel.AddComponent<CanvasGroup>();
        ui.catalog = weaponCatalog;
        ui.menuAudio = audio;
        ui.onBack = backCallback;
        ui.Build();
        return ui;
    }

    public void Open()
    {
        WeaponDisplayNames.SetCatalog(catalog);
        currentCategory = ProfileCategory.Overview;
        SetProfileBlocked(false);
        if (nameKeyboard != null)
            nameKeyboard.Hide();
        RefreshProfileUI();
        ShowCategory(ProfileCategory.Overview, false);
    }

    public void RefreshProfileUI()
    {
        PlayerProfile profile = PlayerProfileManager.Ensure().GetProfile();
        if (profile == null)
            return;

        profile.EnsureCollections();
        PopulateHeader(profile);
        PopulateOverview(profile);
        PopulateBullseye(profile.LifetimeStats);
        PopulateWeapons(profile);
        WireNavigation();
        Canvas.ForceUpdateCanvases();
    }

    private void Update()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        HandleCategoryHotkeys();
        EnsureSelectedWeaponVisible();
    }

    private void Build()
    {
        MenuUiFactory.CreateLabel(transform, "Title", "PROFILE", 44, new Vector2(0f, 410f), new Vector2(900f, 56f));
        displayNameLabel = MenuUiFactory.CreateLabel(transform, "DisplayName", PlayerProfileConstants.DefaultDisplayName, 36, new Vector2(0f, 358f), new Vector2(900f, 44f));
        profileIdLabel = MenuUiFactory.CreateLabel(transform, "ProfileId", "", 18, new Vector2(0f, 322f), new Vector2(900f, 24f));
        profileIdLabel.color = ProfileUiFactory.MutedColor;

        MenuUiFactory.CreateLabel(transform, "NameLabel", "Display Name", 18, new Vector2(-210f, 286f), new Vector2(200f, 24f), TextAnchor.MiddleLeft);
        displayNameField = MenuUiFactory.CreateInputField(transform, "DisplayNameField", "Display Name", new Vector2(-40f, 248f), new Vector2(420f, 50f));
        displayNameField.characterLimit = PlayerProfileConstants.MaxDisplayNameLength;
        displayNameField.onValidateInput = DisplayNameRules.ValidateInput;
        displayNameField.onEndEdit.AddListener(HandleNameEndEdit);
        if (displayNameField is NavigableInputField namedField)
            namedField.GamepadSubmit += OpenNameKeyboard;
        saveNameButton = MenuUiFactory.CreateButton(transform, "SaveName", "Save", new Vector2(280f, 248f), SaveDisplayName, new Vector2(150f, 50f));
        nameErrorLabel = MenuUiFactory.CreateLabel(transform, "NameError", string.Empty, 16, new Vector2(0f, 210f), new Vector2(860f, 22f));
        nameErrorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
        nameKeyboard = DisplayNameKeyboardUI.Create(transform.parent, (RectTransform)transform, menuAudio, CommitDisplayName, CloseNameKeyboard);

        overviewTab = MenuUiFactory.CreateButton(transform, "OverviewTab", "Overview", new Vector2(-250f, 178f), () => ShowCategory(ProfileCategory.Overview, true), new Vector2(220f, 50f));
        weaponsTab = MenuUiFactory.CreateButton(transform, "WeaponsTab", "Weapons", new Vector2(0f, 178f), () => ShowCategory(ProfileCategory.Weapons, true), new Vector2(220f, 50f));
        bullseyeTab = MenuUiFactory.CreateButton(transform, "BullseyeTab", "Bullseye", new Vector2(250f, 178f), () => ShowCategory(ProfileCategory.Bullseye, true), new Vector2(220f, 50f));

        overviewScroll = ProfileUiFactory.CreateScrollArea(transform, "OverviewScroll", new Vector2(0f, -50f), new Vector2(900f, 390f));
        weaponsScroll = ProfileUiFactory.CreateScrollArea(transform, "WeaponsScroll", new Vector2(0f, -50f), new Vector2(900f, 390f));
        bullseyeScroll = ProfileUiFactory.CreateScrollArea(transform, "BullseyeScroll", new Vector2(0f, -50f), new Vector2(900f, 390f));
        overviewRoot = overviewScroll.content.gameObject;
        weaponsRoot = weaponsScroll.content.gameObject;
        bullseyeRoot = bullseyeScroll.content.gameObject;

        BuildOverviewRows(overviewRoot.transform);
        BuildBullseyeRows(bullseyeRoot.transform);
        weaponsEmptyLabel = ProfileUiFactory.CreateBodyLabel(weaponsRoot.transform, "Empty", "No weapon statistics yet.", 22, TextAnchor.MiddleCenter, 48f);

        MenuUiFactory.CreateLabel(transform, "Hint", "LB / RB  Categories     B / Esc  Back", 16, new Vector2(0f, -278f), new Vector2(900f, 24f)).color = ProfileUiFactory.MutedColor;
        backButton = MenuUiFactory.CreateButton(transform, "Back", "Back", new Vector2(0f, -330f), () => onBack?.Invoke());
    }

    private void BuildOverviewRows(Transform parent)
    {
        ProfileUiFactory.CreateSectionHeader(parent, "MatchesHeader", "MATCHES");
        matchesPlayedRow = ProfileUiFactory.CreateStatRow(parent, "MatchesPlayed", "Matches Played");
        matchesCompletedRow = ProfileUiFactory.CreateStatRow(parent, "MatchesCompleted", "Matches Completed");
        winsRow = ProfileUiFactory.CreateStatRow(parent, "Wins", "Wins");
        lossesRow = ProfileUiFactory.CreateStatRow(parent, "Losses", "Losses");
        winRateRow = ProfileUiFactory.CreateStatRow(parent, "WinRate", "Win Rate");

        ProfileUiFactory.CreateSectionHeader(parent, "CombatHeader", "COMBAT");
        eliminationsRow = ProfileUiFactory.CreateStatRow(parent, "Eliminations", "Eliminations");
        deathsRow = ProfileUiFactory.CreateStatRow(parent, "Deaths", "Deaths");
        assistsRow = ProfileUiFactory.CreateStatRow(parent, "Assists", "Assists");
        kdRow = ProfileUiFactory.CreateStatRow(parent, "KD", "K/D");
        shotsFiredRow = ProfileUiFactory.CreateStatRow(parent, "ShotsFired", "Shots Fired");
        shotsHitRow = ProfileUiFactory.CreateStatRow(parent, "ShotsHit", "Shots Hit");
        accuracyRow = ProfileUiFactory.CreateStatRow(parent, "Accuracy", "Accuracy");
        damageRow = ProfileUiFactory.CreateStatRow(parent, "Damage", "Damage Dealt");

        ProfileUiFactory.CreateSectionHeader(parent, "RecordsHeader", "RECORDS");
        favoriteWeaponRow = ProfileUiFactory.CreateStatRow(parent, "FavoriteWeapon", "Favorite Weapon");
        longestElimRow = ProfileUiFactory.CreateStatRow(parent, "LongestElim", "Longest Elimination");
        playTimeRow = ProfileUiFactory.CreateStatRow(parent, "PlayTime", "Play Time");
    }

    private void BuildBullseyeRows(Transform parent)
    {
        ProfileUiFactory.CreateSectionHeader(parent, "BullseyeHeader", "BULLSEYE");
        bullseyeHitsRow = ProfileUiFactory.CreateStatRow(parent, "BullseyeHits", "Bullseye Hits");
        bullseyeEliminationsRow = ProfileUiFactory.CreateStatRow(parent, "BullseyeEliminations", "Bullseye Eliminations");
        attachedEliminationsRow = ProfileUiFactory.CreateStatRow(parent, "AttachedEliminations", "Attached Bullseye Eliminations");
        detachedEliminationsRow = ProfileUiFactory.CreateStatRow(parent, "DetachedEliminations", "Detached Bullseye Eliminations");
        grenadeDetachmentsRow = ProfileUiFactory.CreateStatRow(parent, "GrenadeDetachments", "Grenade Detachments");

        ProfileUiFactory.CreateSectionHeader(parent, "HitHeader", "HIT LOCATION");
        hitBullseyeRow = ProfileUiFactory.CreateStatRow(parent, "HitBullseye", "Bullseye Hits");
        hitHeadRow = ProfileUiFactory.CreateStatRow(parent, "HitHead", "Head Hits");
        hitBodyRow = ProfileUiFactory.CreateStatRow(parent, "HitBody", "Body Hits");

        ProfileUiFactory.CreateSectionHeader(parent, "SpecialHeader", "SPECIAL ELIMINATIONS");
        bodySlamRow = ProfileUiFactory.CreateStatRow(parent, "BodySlam", "Body Slam");
        ricochetRow = ProfileUiFactory.CreateStatRow(parent, "Ricochet", "Ricochet");
        specialDetachedRow = ProfileUiFactory.CreateStatRow(parent, "SpecialDetached", "Detached Bullseye");
    }

    private void PopulateHeader(PlayerProfile profile)
    {
        string displayName = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? PlayerProfileConstants.DefaultDisplayName
            : profile.DisplayName;

        if (displayNameLabel != null)
            displayNameLabel.text = displayName;
        if (profileIdLabel != null)
            profileIdLabel.text = "Profile ID: " + ProfileStatFormatter.ShortProfileId(profile.PlayerProfileId);

        suppressNameCallback = true;
        if (displayNameField != null)
            displayNameField.text = displayName;
        suppressNameCallback = false;
    }

    private void PopulateOverview(PlayerProfile profile)
    {
        LifetimeStats life = profile.LifetimeStats ?? new LifetimeStats();
        matchesPlayedRow.SetValue(ProfileStatFormatter.Count(life.MatchesPlayed));
        matchesCompletedRow.SetValue(ProfileStatFormatter.Count(life.MatchesCompleted));
        matchesCompletedRow.gameObject.SetActive(PlayerProfilePresentation.ShowMatchesCompleted(life));
        winsRow.SetValue(ProfileStatFormatter.Count(life.Wins));
        lossesRow.SetValue(ProfileStatFormatter.Count(life.Losses));
        winRateRow.SetValue(ProfileStatFormatter.Percent(life.WinRate));

        eliminationsRow.SetValue(ProfileStatFormatter.Count(life.Eliminations));
        deathsRow.SetValue(ProfileStatFormatter.Count(life.Deaths));
        assistsRow.SetValue(ProfileStatFormatter.Count(life.Assists));
        kdRow.SetValue(ProfileStatFormatter.Ratio(life.KDRatio));
        shotsFiredRow.SetValue(ProfileStatFormatter.Count(life.ShotsFired));
        shotsHitRow.SetValue(ProfileStatFormatter.Count(life.ShotsHit));
        accuracyRow.SetValue(ProfileStatFormatter.Percent(life.Accuracy));
        damageRow.SetValue(ProfileStatFormatter.Count(life.TotalDamageDealt));

        favoriteWeaponRow.SetValue(WeaponDisplayNames.FavoriteWeapon(profile));
        longestElimRow.SetValue(ProfileStatFormatter.DistanceMeters(life.LongestEliminationDistance));
        playTimeRow.SetValue(ProfileStatFormatter.PlayTime(life.TotalPlayTimeSeconds));
    }

    private void PopulateBullseye(LifetimeStats life)
    {
        if (life == null)
            life = new LifetimeStats();

        bullseyeHitsRow.SetValue(ProfileStatFormatter.Count(life.BullseyeHits));
        bullseyeEliminationsRow.SetValue(ProfileStatFormatter.Count(PlayerProfilePresentation.BullseyeEliminations(life)));
        attachedEliminationsRow.SetValue(ProfileStatFormatter.Count(life.AttachedBullseyeEliminations));
        detachedEliminationsRow.SetValue(ProfileStatFormatter.Count(life.DetachedBullseyeEliminations));
        grenadeDetachmentsRow.SetValue(ProfileStatFormatter.Count(life.GrenadeBullseyeDetachments));

        hitBullseyeRow.SetValue(ProfileStatFormatter.Count(life.BullseyeHits));
        hitHeadRow.SetValue(ProfileStatFormatter.Count(life.HeadHits));
        hitBodyRow.SetValue(ProfileStatFormatter.Count(life.BodyHits));

        bodySlamRow.SetValue(ProfileStatFormatter.Count(life.BodySlamEliminations));
        ricochetRow.SetValue(ProfileStatFormatter.Count(life.RicochetEliminations));
        specialDetachedRow.SetValue(ProfileStatFormatter.Count(life.DetachedBullseyeEliminations));
    }

    private void PopulateWeapons(PlayerProfile profile)
    {
        for (int i = 0; i < weaponEntries.Count; i++)
        {
            if (weaponEntries[i] != null)
                Destroy(weaponEntries[i].gameObject);
        }

        weaponEntries.Clear();
        List<WeaponLifetimeStats> weapons = PlayerProfilePresentation.GetSortedWeaponStats(profile);
        if (weaponsEmptyLabel != null)
            weaponsEmptyLabel.gameObject.SetActive(weapons.Count == 0);

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponProfileEntry entry = ProfileUiFactory.CreateWeaponEntry(weaponsRoot.transform, "Weapon" + i);
            entry.Bind(weapons[i]);
            weaponEntries.Add(entry);
        }

        if (weaponsEmptyLabel != null)
            weaponsEmptyLabel.transform.SetAsFirstSibling();
    }

    private void ShowCategory(ProfileCategory category, bool playSound)
    {
        currentCategory = category;
        SetActive(overviewScroll.gameObject, category == ProfileCategory.Overview);
        SetActive(weaponsScroll.gameObject, category == ProfileCategory.Weapons);
        SetActive(bullseyeScroll.gameObject, category == ProfileCategory.Bullseye);
        MenuUiFactory.SetButtonSelectedVisual(overviewTab, category == ProfileCategory.Overview);
        MenuUiFactory.SetButtonSelectedVisual(weaponsTab, category == ProfileCategory.Weapons);
        MenuUiFactory.SetButtonSelectedVisual(bullseyeTab, category == ProfileCategory.Bullseye);
        WireNavigation();

        if (playSound && menuAudio != null)
            menuAudio.PlaySelect();

        Selectable tab = TabFor(category);
        if (tab != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(tab.gameObject);
    }

    private void CycleCategory(int delta)
    {
        int count = Enum.GetValues(typeof(ProfileCategory)).Length;
        int next = ((int)currentCategory + delta) % count;
        if (next < 0)
            next += count;
        ShowCategory((ProfileCategory)next, true);
    }

    private void HandleCategoryHotkeys()
    {
        Gamepad pad = Gamepad.current;
        if (nameKeyboard != null && nameKeyboard.IsOpen)
            return;
        if (IsEditingName())
            return;

        if (pad != null)
        {
            if (pad.leftShoulder.wasPressedThisFrame)
                CycleCategory(-1);
            if (pad.rightShoulder.wasPressedThisFrame)
                CycleCategory(1);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.qKey.wasPressedThisFrame)
            CycleCategory(-1);
        if (keyboard.eKey.wasPressedThisFrame)
            CycleCategory(1);
    }

    private bool IsEditingName()
    {
        if (displayNameField == null || EventSystem.current == null)
            return false;

        return EventSystem.current.currentSelectedGameObject == displayNameField.gameObject && displayNameField.isFocused;
    }

    /// <summary>
    /// Escape / B while naming. Returns true when the menu should stay on Profile.
    /// </summary>
    public bool TryHandleCancel()
    {
        if (nameKeyboard != null && nameKeyboard.IsOpen)
        {
            nameKeyboard.Cancel();
            return true;
        }

        if (!IsEditingName())
            return false;

        suppressNameCallback = true;
        displayNameField.DeactivateInputField();
        suppressNameCallback = false;
        RestoreNameField();
        return true;
    }

    private void OpenNameKeyboard()
    {
        if (nameKeyboard == null || displayNameField == null)
            return;

        suppressNameCallback = true;
        displayNameField.DeactivateInputField();
        suppressNameCallback = false;
        string seed = displayNameField.text;
        if (string.IsNullOrWhiteSpace(seed))
            seed = PlayerProfileManager.Ensure().DisplayName;
        SetProfileBlocked(true);
        nameKeyboard.Open(seed);
    }

    private void CloseNameKeyboard()
    {
        SetProfileBlocked(false);
        RestoreNameField();
        if (displayNameField != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(displayNameField.gameObject);
        }
    }

    private void SetProfileBlocked(bool blocked)
    {
        if (profileGroup == null)
            return;

        profileGroup.interactable = !blocked;
        profileGroup.blocksRaycasts = !blocked;
    }

    private void HandleNameEndEdit(string _)
    {
        if (suppressNameCallback)
            return;

        SaveDisplayName();
    }

    private void CommitDisplayName(string raw)
    {
        if (!DisplayNameRules.TryNormalize(raw, out string normalized, out string error))
        {
            if (nameKeyboard != null)
                nameKeyboard.SetError(error);
            return;
        }

        ApplyDisplayName(normalized);
        if (nameKeyboard != null)
            nameKeyboard.Hide();
        CloseNameKeyboard();
    }

    private void SaveDisplayName()
    {
        if (displayNameField == null)
            return;

        if (!DisplayNameRules.TryNormalize(displayNameField.text, out string normalized, out string error))
        {
            SetNameError(error);
            RestoreNameField();
            return;
        }

        ApplyDisplayName(normalized);
    }

    private void ApplyDisplayName(string normalized)
    {
        PlayerProfileManager.Ensure().SetDisplayName(normalized);
        SetNameError(null);
        if (menuAudio != null)
            menuAudio.PlaySelect();
        RefreshProfileUI();
    }

    private void RestoreNameField()
    {
        suppressNameCallback = true;
        if (displayNameField != null)
            displayNameField.text = PlayerProfileManager.Ensure().DisplayName;
        suppressNameCallback = false;
    }

    private void SetNameError(string message)
    {
        if (nameErrorLabel != null)
            nameErrorLabel.text = message ?? string.Empty;
    }

    private void WireNavigation()
    {
        Selectable firstWeapon = FirstWeaponSelectable();
        Selectable lastWeapon = LastWeaponSelectable();
        Selectable downFromTabs = currentCategory == ProfileCategory.Weapons && firstWeapon != null
            ? firstWeapon
            : backButton;

        MenuUiFactory.SetNav(displayNameField, backButton, overviewTab, saveNameButton, saveNameButton);
        MenuUiFactory.SetNav(saveNameButton, backButton, bullseyeTab, displayNameField, displayNameField);
        MenuUiFactory.SetNav(overviewTab, displayNameField, downFromTabs, bullseyeTab, weaponsTab);
        MenuUiFactory.SetNav(weaponsTab, displayNameField, downFromTabs, overviewTab, bullseyeTab);
        MenuUiFactory.SetNav(bullseyeTab, saveNameButton, downFromTabs, weaponsTab, overviewTab);

        for (int i = 0; i < weaponEntries.Count; i++)
        {
            Selectable current = weaponEntries[i] != null ? weaponEntries[i].FocusTarget : null;
            if (current == null)
                continue;

            Selectable up = i == 0 ? weaponsTab : weaponEntries[i - 1].FocusTarget;
            Selectable down = i == weaponEntries.Count - 1 ? backButton : weaponEntries[i + 1].FocusTarget;
            MenuUiFactory.SetVerticalNav(current, up, down);
        }

        Selectable upFromBack = currentCategory == ProfileCategory.Weapons && lastWeapon != null
            ? lastWeapon
            : TabFor(currentCategory);
        MenuUiFactory.SetVerticalNav(backButton, upFromBack, displayNameField);
    }

    private Selectable FirstWeaponSelectable()
    {
        return weaponEntries.Count > 0 && weaponEntries[0] != null ? weaponEntries[0].FocusTarget : null;
    }

    private Selectable LastWeaponSelectable()
    {
        int last = weaponEntries.Count - 1;
        return last >= 0 && weaponEntries[last] != null ? weaponEntries[last].FocusTarget : null;
    }

    private Selectable TabFor(ProfileCategory category)
    {
        switch (category)
        {
            case ProfileCategory.Weapons: return weaponsTab;
            case ProfileCategory.Bullseye: return bullseyeTab;
            default: return overviewTab;
        }
    }

    private void EnsureSelectedWeaponVisible()
    {
        if (currentCategory != ProfileCategory.Weapons || weaponsScroll == null || EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return;

        RectTransform selectedRect = selected.GetComponent<RectTransform>();
        RectTransform content = weaponsScroll.content;
        RectTransform viewport = weaponsScroll.viewport;
        if (selectedRect == null || content == null || viewport == null)
            return;
        if (!selected.transform.IsChildOf(content))
            return;

        Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selectedRect);
        float viewHeight = viewport.rect.height;
        float overflowTop = itemBounds.max.y - viewHeight * 0.5f;
        float overflowBottom = -viewHeight * 0.5f - itemBounds.min.y;
        Vector2 pos = content.anchoredPosition;
        if (overflowTop > 0f)
            pos.y -= overflowTop;
        else if (overflowBottom > 0f)
            pos.y += overflowBottom;
        else
            return;

        content.anchoredPosition = pos;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
