using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StageSettingsMenu : MonoBehaviour
{
    private const string MainStageSceneName = "MainStageScene";
    private const string StageASceneName = "StageA";
    private const string StageBSceneName = "StageB";
    private const string LobbySceneName = "LobbyScene";

    private static bool sceneHookRegistered;

    private GameObject menuRoot;
    private Button closeButton;
    private Button leaveButton;

    private PlayerInput cachedPlayerInput;
    private PlayerSplitScreenCamera cachedCamera;

    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private bool previousPlayerInputEnabled;
    private bool previousCrosshairVisible;
    private bool previousSplitSeparatorVisible;
    private bool previousSplitSeparatorDimmed;
    private bool hadPlayerInput;
    private bool hadCamera;
    private bool isOpen;
    private bool isLeaving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sceneHookRegistered = false;
        PlayerInputHandler.IsGameplayInputBlocked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScene()
    {
        RegisterSceneHook();
        CreateForScene(SceneManager.GetActiveScene());
    }

    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateForScene(scene);
    }

    private static void CreateForScene(Scene scene)
    {
        if (!IsStageScene(scene))
            return;

        if (FindMenuInScene(scene) != null)
            return;

        GameObject menuObject = new GameObject("StageSettingsMenu");
        SceneManager.MoveGameObjectToScene(menuObject, scene);
        menuObject.AddComponent<StageSettingsMenu>();
    }

    private void Awake()
    {
        if (!IsStageScene(gameObject.scene))
        {
            Destroy(gameObject);
            return;
        }

        BuildUi();
    }

    private static bool IsStageScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        return scene.name == MainStageSceneName ||
            scene.name == StageASceneName ||
            scene.name == StageBSceneName;
    }

    private static StageSettingsMenu FindMenuInScene(Scene scene)
    {
        StageSettingsMenu[] menus = FindObjectsByType<StageSettingsMenu>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (StageSettingsMenu menu in menus)
        {
            if (menu != null && menu.gameObject.scene == scene)
                return menu;
        }

        return null;
    }

    private void Update()
    {
        if (isLeaving)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (isOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    private void OnDestroy()
    {
        if (isOpen && !isLeaving)
            RestoreGameplayInput(true);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseMenu);

        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(OnClickLeaveGame);
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(
            "StageSettingsCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        StretchToParent(canvasRect);

        menuRoot = CreateRect("SettingsOverlay", canvasRect);
        menuRoot.layer = 5;

        Image overlayImage = menuRoot.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.58f);

        RectTransform overlayRect = menuRoot.GetComponent<RectTransform>();
        StretchToParent(overlayRect);

        GameObject panel = CreateRect("SettingsPanel", overlayRect);
        panel.layer = 5;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(440f, 280f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 34, 34);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText("Title", panelRect, "Settings", 40f, FontStyles.Bold);
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 58f;

        closeButton = CreateButton("CloseButton", panelRect, "Close", new Color(0.22f, 0.25f, 0.28f, 1f));
        closeButton.onClick.AddListener(CloseMenu);

        leaveButton = CreateButton("LeaveButton", panelRect, "Leave Game", new Color(0.55f, 0.14f, 0.14f, 1f));
        leaveButton.onClick.AddListener(OnClickLeaveGame);

        menuRoot.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));

        SceneManager.MoveGameObjectToScene(eventSystemObject, gameObject.scene);
    }

    private void OpenMenu()
    {
        if (menuRoot == null || isOpen)
            return;

        isOpen = true;
        CaptureGameplayInput();
        menuRoot.SetActive(true);

        if (EventSystem.current != null && closeButton != null)
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void CloseMenu()
    {
        if (menuRoot == null || !isOpen || isLeaving)
            return;

        menuRoot.SetActive(false);
        RestoreGameplayInput(true);
        isOpen = false;
    }

    private void CaptureGameplayInput()
    {
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        PlayerInputHandler.IsGameplayInputBlocked = true;

        cachedPlayerInput = ResolveLocalPlayerInput();
        hadPlayerInput = cachedPlayerInput != null;

        if (cachedPlayerInput != null)
        {
            previousPlayerInputEnabled = cachedPlayerInput.enabled;
            cachedPlayerInput.enabled = false;
        }

        cachedCamera = ResolveLocalCamera();
        hadCamera = cachedCamera != null;

        if (cachedCamera != null)
        {
            previousCrosshairVisible = cachedCamera.showCrosshair;
            previousSplitSeparatorVisible = cachedCamera.showSplitSeparator;
            previousSplitSeparatorDimmed = cachedCamera.IsSplitSeparatorDimmed;
            cachedCamera.showCrosshair = false;
            cachedCamera.showSplitSeparator = true;
            cachedCamera.SetSplitSeparatorDimmed(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreGameplayInput(bool restoreCursor, bool restoreCameraUi = true)
    {
        if (hadPlayerInput && cachedPlayerInput != null)
            cachedPlayerInput.enabled = previousPlayerInputEnabled;

        if (restoreCameraUi && hadCamera && cachedCamera != null)
        {
            cachedCamera.showCrosshair = previousCrosshairVisible;
            cachedCamera.showSplitSeparator = previousSplitSeparatorVisible;
            cachedCamera.SetSplitSeparatorDimmed(previousSplitSeparatorDimmed);
        }

        PlayerInputHandler.IsGameplayInputBlocked = false;

        if (restoreCursor)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
        }

        cachedPlayerInput = null;
        cachedCamera = null;
        hadPlayerInput = false;
        hadCamera = false;
    }

    private PlayerInput ResolveLocalPlayerInput()
    {
        PlayerInputHandler[] handlers = FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None);

        foreach (PlayerInputHandler handler in handlers)
        {
            if (handler == null || handler.Object == null || !handler.Object.HasInputAuthority)
                continue;

            return handler.GetComponent<PlayerInput>();
        }

        return null;
    }

    private PlayerSplitScreenCamera ResolveLocalCamera()
    {
        PlayerSplitScreenCamera[] cameras = FindObjectsByType<PlayerSplitScreenCamera>(FindObjectsSortMode.None);

        foreach (PlayerSplitScreenCamera camera in cameras)
        {
            if (camera == null || camera.Object == null || !camera.Object.HasInputAuthority)
                continue;

            return camera;
        }

        return null;
    }

    private async void OnClickLeaveGame()
    {
        if (isLeaving)
            return;

        isLeaving = true;

        if (closeButton != null)
            closeButton.interactable = false;

        if (leaveButton != null)
            leaveButton.interactable = false;

        if (isOpen)
        {
            RestoreGameplayInput(false, false);
            isOpen = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PlayerInputHandler.IsGameplayInputBlocked = false;

        NetworkSessionManager sessionManager = NetworkSessionManager.Instance;
        if (sessionManager == null)
            sessionManager = FindAnyObjectByType<NetworkSessionManager>(FindObjectsInactive.Include);

        if (sessionManager != null)
            await sessionManager.LeaveSessionAsync();

        SceneFlowManager sceneFlowManager = FindAnyObjectByType<SceneFlowManager>(FindObjectsInactive.Include);
        if (sceneFlowManager != null)
        {
            sceneFlowManager.LoadLobbySceneLocal();
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(LobbySceneName))
        {
            SceneManager.LoadScene(LobbySceneName);
            return;
        }

        SceneManager.LoadScene(0);
    }

    private static GameObject CreateRect(string objectName, RectTransform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        RectTransform parent,
        string text,
        float fontSize,
        FontStyles fontStyle)
    {
        GameObject textObject = CreateRect(objectName, parent);
        textObject.layer = 5;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return label;
    }

    private static Button CreateButton(
        string objectName,
        RectTransform parent,
        string text,
        Color normalColor)
    {
        GameObject buttonObject = CreateRect(objectName, parent);
        buttonObject.layer = 5;

        Image image = buttonObject.AddComponent<Image>();
        image.color = normalColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.7f);
        button.colors = colors;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 58f;

        TextMeshProUGUI label = CreateText("Label", buttonObject.GetComponent<RectTransform>(), text, 26f, FontStyles.Normal);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        StretchToParent(labelRect);

        return button;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}
