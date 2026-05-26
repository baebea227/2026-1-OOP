using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class PlayerSplitScreenCamera : NetworkBehaviour
{
    private const int SplitScreenPlayerCount = 2;
    private const int SplitSeparatorSortingOrder = 50;
    private const int MaxControlHintCount = 3;
    private const float ControlHintYOffset = 24f;
    private const float ControlHintHeight = 26f;
    private const float ControlHintRowGap = 6f;
    private const float ControlHintSpacing = 8f;
    private const float ControlHintPaddingX = 10f;
    private const float ControlHintKeyWidth = 24f;
    private const float ControlHintKeyInsetY = 4f;
    private const float ControlHintInnerGap = 6f;
    private const float ControlHintScreenPadding = 12f;

    private static readonly Rect[] SplitScreenRects = new Rect[]
    {
        new Rect(0f, 0f, 0.5f, 1f),
        new Rect(0.5f, 0f, 0.5f, 1f)
    };
    private static readonly List<PlayerSplitScreenCamera> registeredCameras = new List<PlayerSplitScreenCamera>();
    private static Camera splitScreenBackgroundCamera;
    private static GameObject splitSeparatorCanvasObject;
    private static RectTransform splitSeparatorRect;
    private static Image splitSeparatorImage;
    private static bool hasSplitSeparatorDimmedOverride;
    private static bool splitSeparatorDimmedOverride;

    [Header("Third Person Settings")]
    public Vector3 pivotOffset = new Vector3(0.35f, 1.35f, 0f);
    public float distance = 4f;
    public float sideOffset = 0.2f;
    public float followSmoothTime = 0.04f;
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.1f;

    [Header("Split Screen UI")]
    public bool showSplitSeparator = true;
    public float splitSeparatorWidth = 4f;
    [Range(0f, 1f)] public float splitSeparatorNormalAlpha = 0.75f;
    [Range(0f, 1f)] public float splitSeparatorMenuAlpha = 0.22f;

    [Header("Crosshair")]
    public bool showCrosshair = true;
    public Color crosshairColor = Color.white;
    public int crosshairDiameter = 18;
    public int crosshairThickness = 2;

    [Header("Control Hints")]
    public bool showControlHints = true;
    public Color controlHintTextColor = Color.white;
    public Color controlHintBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color controlHintKeyColor = Color.white;
    public Color controlHintKeyTextColor = Color.black;

    private Camera cam;
    private PlayerInputHandler inputHandler;
    private PlayerMovement playerMovement;
    private PlayerGrabHandler grabHandler;
    private Transform target;
    private Collider[] ownerColliders;
    private Vector3 followVelocity;
    private Transform originalParent;
    private Texture2D crosshairTexture;
    private readonly string[] controlHintKeys = new string[MaxControlHintCount];
    private readonly string[] controlHintActions = new string[MaxControlHintCount];
    private readonly RaycastHit[] cameraCollisionHits = new RaycastHit[16];
    private GUIStyle controlHintKeyStyle;
    private GUIStyle controlHintActionStyle;
    private int cachedCrosshairDiameter;
    private int cachedCrosshairThickness;
    private Color cachedCrosshairColor;
    private bool initialized;
    private bool splitSeparatorDimmed;

    public bool IsSplitSeparatorDimmed => splitSeparatorDimmed;

    public void SetSplitSeparatorDimmed(bool dimmed)
    {
        splitSeparatorDimmed = dimmed;
        hasSplitSeparatorDimmedOverride = true;
        splitSeparatorDimmedOverride = dimmed;
        UpdateSplitSeparatorUi();
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        target = GetComponentInParent<NetworkObject>()?.transform;
        originalParent = transform.parent;
    }

    public override void Spawned()
    {
        inputHandler = GetComponentInParent<PlayerInputHandler>();
        playerMovement = GetComponentInParent<PlayerMovement>();
        grabHandler = GetComponentInParent<PlayerGrabHandler>();

        if (target == null)
            target = GetComponentInParent<NetworkObject>()?.transform;

        if (target != null)
            ownerColliders = target.GetComponentsInChildren<Collider>();

        transform.SetParent(null, true);
        initialized = false;
        followVelocity = Vector3.zero;

        RegisterCamera();

        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterCamera();

        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (originalParent != null)
            transform.SetParent(originalParent, true);
    }

    private void OnDestroy()
    {
        UnregisterCamera();

        if (crosshairTexture != null)
            Destroy(crosshairTexture);
    }

    public override void Render()
    {
        UpdateSplitScreenCameras();

        if (target == null || cam == null || !cam.enabled) return;

        GetCameraAngles(out float yaw, out float pitch);
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot =
            target.position +
            Vector3.up * pivotOffset.y +
            orbitRotation * Vector3.right * pivotOffset.x +
            orbitRotation * Vector3.forward * pivotOffset.z;
        Vector3 desiredPosition =
            pivot - orbitRotation * Vector3.forward * distance +
            orbitRotation * Vector3.right * sideOffset;

        Vector3 correctedPosition = ResolveCameraCollision(pivot, desiredPosition);
        if (!initialized)
        {
            transform.position = correctedPosition;
            initialized = true;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                correctedPosition,
                ref followVelocity,
                followSmoothTime);
        }

        Vector3 lookDirection = pivot - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
    }

    private void OnGUI()
    {
        if (!HasInputAuthority)
            return;

        if (cam == null || !cam.enabled)
            return;

        Rect cameraPixelRect = cam.pixelRect;
        float centerX = cameraPixelRect.x + cameraPixelRect.width * 0.5f;
        float centerY = Screen.height - (cameraPixelRect.y + cameraPixelRect.height * 0.5f);
        float diameter = Mathf.Max(1, crosshairDiameter);

        if (showCrosshair)
        {
            Texture2D texture = GetCrosshairTexture();
            if (texture != null)
            {
                GUI.DrawTexture(
                    new Rect(centerX - diameter * 0.5f, centerY - diameter * 0.5f, diameter, diameter),
                    texture);
            }
        }

        if (showControlHints)
            DrawControlHints(centerX, centerY + diameter * 0.5f + ControlHintYOffset, cameraPixelRect.width);
    }

    private void DrawControlHints(float centerX, float topY, float cameraWidth)
    {
        int hintCount = CollectControlHints();
        if (hintCount == 0)
            return;

        EnsureControlHintStyles();

        float maxRowWidth = Mathf.Max(ControlHintKeyWidth, cameraWidth - ControlHintScreenPadding * 2f);
        int rowStart = 0;
        while (rowStart < hintCount)
        {
            int rowEnd = rowStart;
            float rowWidth = 0f;

            while (rowEnd < hintCount)
            {
                float hintWidth = GetControlHintWidth(controlHintActions[rowEnd]);
                float candidateWidth = rowWidth > 0f ? rowWidth + ControlHintSpacing + hintWidth : hintWidth;
                if (rowEnd > rowStart && candidateWidth > maxRowWidth)
                    break;

                rowWidth = candidateWidth;
                rowEnd++;
            }

            DrawControlHintRow(rowStart, rowEnd, centerX - rowWidth * 0.5f, topY);
            rowStart = rowEnd;
            topY += ControlHintHeight + ControlHintRowGap;
        }
    }

    private int CollectControlHints()
    {
        if (grabHandler == null || PlayerInputHandler.IsGameplayInputBlocked)
            return 0;

        int hintCount = 0;
        if (grabHandler.ShowInteractHint)
            AddControlHint(ref hintCount, "E", "Interact");

        if (grabHandler.ShowGrabHint)
            AddControlHint(ref hintCount, "F", grabHandler.GrabHintAction);

        if (grabHandler.ShowThrowHint)
            AddControlHint(ref hintCount, "G", "Throw");

        return hintCount;
    }

    private void AddControlHint(ref int hintCount, string key, string action)
    {
        if (hintCount >= MaxControlHintCount || string.IsNullOrEmpty(action))
            return;

        controlHintKeys[hintCount] = key;
        controlHintActions[hintCount] = action;
        hintCount++;
    }

    private void DrawControlHintRow(int startIndex, int endIndex, float x, float y)
    {
        for (int i = startIndex; i < endIndex; i++)
        {
            float hintWidth = GetControlHintWidth(controlHintActions[i]);
            DrawControlHint(new Rect(x, y, hintWidth, ControlHintHeight), controlHintKeys[i], controlHintActions[i]);
            x += hintWidth + ControlHintSpacing;
        }
    }

    private float GetControlHintWidth(string action)
    {
        EnsureControlHintStyles();
        float actionWidth = controlHintActionStyle.CalcSize(new GUIContent(action)).x;
        return Mathf.Ceil(ControlHintPaddingX + ControlHintKeyWidth + ControlHintInnerGap + actionWidth + ControlHintPaddingX);
    }

    private void DrawControlHint(Rect rect, string key, string action)
    {
        Color oldColor = GUI.color;

        GUI.color = controlHintBackgroundColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        Rect keyRect = new Rect(
            rect.x + ControlHintPaddingX,
            rect.y + ControlHintKeyInsetY,
            ControlHintKeyWidth,
            rect.height - ControlHintKeyInsetY * 2f);

        GUI.color = controlHintKeyColor;
        GUI.DrawTexture(keyRect, Texture2D.whiteTexture);
        GUI.color = oldColor;

        Rect actionRect = new Rect(
            keyRect.xMax + ControlHintInnerGap,
            rect.y,
            rect.xMax - keyRect.xMax - ControlHintInnerGap - ControlHintPaddingX,
            rect.height);

        GUI.Label(keyRect, key, controlHintKeyStyle);
        GUI.Label(actionRect, action, controlHintActionStyle);
    }

    private void EnsureControlHintStyles()
    {
        if (controlHintKeyStyle == null)
        {
            controlHintKeyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                clipping = TextClipping.Clip
            };
        }

        if (controlHintActionStyle == null)
        {
            controlHintActionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                clipping = TextClipping.Clip
            };
        }

        controlHintKeyStyle.normal.textColor = controlHintKeyTextColor;
        controlHintActionStyle.normal.textColor = controlHintTextColor;
    }

    private void GetCameraAngles(out float yaw, out float pitch)
    {
        if (HasInputAuthority && inputHandler != null)
        {
            yaw = inputHandler.CameraYaw;
            pitch = inputHandler.CameraPitch;
            return;
        }

        if (playerMovement != null)
        {
            yaw = playerMovement.CameraYaw;
            pitch = playerMovement.CameraPitch;
            return;
        }

        yaw = target != null ? target.eulerAngles.y : 0f;
        pitch = 0f;
    }

    private void RegisterCamera()
    {
        if (!registeredCameras.Contains(this))
            registeredCameras.Add(this);

        UpdateSplitScreenCameras();
    }

    private void UnregisterCamera()
    {
        registeredCameras.Remove(this);

        if (registeredCameras.Count == 0)
        {
            DestroySplitScreenBackgroundCamera();
            DestroySplitSeparatorCanvas();
        }
        else
        {
            UpdateSplitScreenCameras();
        }
    }

    private static void UpdateSplitScreenCameras()
    {
        registeredCameras.RemoveAll(IsInvalidRegisteredCamera);

        if (registeredCameras.Count == 0)
        {
            DestroySplitScreenBackgroundCamera();
            DestroySplitSeparatorCanvas();
            return;
        }

        EnsureSplitScreenBackgroundCamera();
        registeredCameras.Sort(CompareCameraSlots);

        for (int i = 0; i < registeredCameras.Count; i++)
        {
            bool visible = i < SplitScreenPlayerCount;
            Rect viewport = visible ? SplitScreenRects[i] : default;
            registeredCameras[i].ConfigureSplitScreenSlot(visible, viewport);
        }

        UpdateSplitSeparatorUi();
    }

    private static bool IsInvalidRegisteredCamera(PlayerSplitScreenCamera camera)
    {
        return camera == null || camera.cam == null || camera.Object == null;
    }

    private static int CompareCameraSlots(PlayerSplitScreenCamera a, PlayerSplitScreenCamera b)
    {
        int playerOrder = a.GetPlayerSortKey().CompareTo(b.GetPlayerSortKey());
        if (playerOrder != 0)
            return playerOrder;

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    private int GetPlayerSortKey()
    {
        return Object != null ? Object.InputAuthority.RawEncoded : int.MaxValue;
    }

    private void ConfigureSplitScreenSlot(bool visible, Rect viewport)
    {
        if (cam == null)
            return;

        cam.enabled = visible;

        if (visible)
        {
            cam.rect = viewport;
            cam.targetDisplay = 0;
            cam.depth = 0f;
        }

        AudioListener listener = cam.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = visible && HasInputAuthority;
    }

    private static void EnsureSplitScreenBackgroundCamera()
    {
        if (splitScreenBackgroundCamera != null)
        {
            splitScreenBackgroundCamera.enabled = true;
            splitScreenBackgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        GameObject backgroundObject = new GameObject("SplitScreenBlackBackgroundCamera");
        backgroundObject.hideFlags = HideFlags.DontSave;

        splitScreenBackgroundCamera = backgroundObject.AddComponent<Camera>();
        splitScreenBackgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        splitScreenBackgroundCamera.backgroundColor = Color.black;
        splitScreenBackgroundCamera.cullingMask = 0;
        splitScreenBackgroundCamera.depth = -1000f;
        splitScreenBackgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
        splitScreenBackgroundCamera.targetDisplay = 0;
        splitScreenBackgroundCamera.allowHDR = false;
        splitScreenBackgroundCamera.allowMSAA = false;
        splitScreenBackgroundCamera.useOcclusionCulling = false;
    }

    private static void DestroySplitScreenBackgroundCamera()
    {
        if (splitScreenBackgroundCamera == null)
            return;

        Camera backgroundCamera = splitScreenBackgroundCamera;
        splitScreenBackgroundCamera = null;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(backgroundCamera.gameObject);
        else
            UnityEngine.Object.DestroyImmediate(backgroundCamera.gameObject);
    }

    private static void EnsureSplitSeparatorCanvas()
    {
        if (splitSeparatorCanvasObject != null &&
            splitSeparatorRect != null &&
            splitSeparatorImage != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "SplitScreenSeparatorCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.hideFlags = HideFlags.DontSave;
        canvasObject.layer = 5;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SplitSeparatorSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        StretchToParent(canvasRect);

        GameObject separatorObject = new GameObject(
            "SplitScreenSeparator",
            typeof(RectTransform),
            typeof(Image));
        separatorObject.layer = 5;
        separatorObject.transform.SetParent(canvasRect, false);

        splitSeparatorCanvasObject = canvasObject;
        splitSeparatorRect = separatorObject.GetComponent<RectTransform>();
        splitSeparatorRect.anchorMin = new Vector2(0.5f, 0f);
        splitSeparatorRect.anchorMax = new Vector2(0.5f, 1f);
        splitSeparatorRect.pivot = new Vector2(0.5f, 0.5f);
        splitSeparatorRect.anchoredPosition = Vector2.zero;
        splitSeparatorRect.offsetMin = new Vector2(0f, 0f);
        splitSeparatorRect.offsetMax = new Vector2(0f, 0f);

        splitSeparatorImage = separatorObject.GetComponent<Image>();
        splitSeparatorImage.raycastTarget = false;
    }

    private static void UpdateSplitSeparatorUi()
    {
        PlayerSplitScreenCamera owner = ResolveSplitSeparatorOwner();
        if (owner == null || !owner.showSplitSeparator || owner.cam == null || !owner.cam.enabled)
        {
            if (splitSeparatorCanvasObject != null)
                splitSeparatorCanvasObject.SetActive(false);

            return;
        }

        EnsureSplitSeparatorCanvas();
        splitSeparatorCanvasObject.SetActive(true);

        float width = Mathf.Max(1f, owner.splitSeparatorWidth);
        splitSeparatorRect.sizeDelta = new Vector2(width, 0f);

        bool dimmed = hasSplitSeparatorDimmedOverride
            ? splitSeparatorDimmedOverride
            : owner.splitSeparatorDimmed;

        float alpha = dimmed
            ? owner.splitSeparatorMenuAlpha
            : owner.splitSeparatorNormalAlpha;

        splitSeparatorImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private static PlayerSplitScreenCamera ResolveSplitSeparatorOwner()
    {
        foreach (PlayerSplitScreenCamera camera in registeredCameras)
        {
            if (camera != null && camera.HasInputAuthority)
                return camera;
        }

        foreach (PlayerSplitScreenCamera camera in registeredCameras)
        {
            if (camera != null && camera.cam != null && camera.cam.enabled)
                return camera;
        }

        return null;
    }

    private static void DestroySplitSeparatorCanvas()
    {
        if (splitSeparatorCanvasObject == null)
            return;

        GameObject canvasObject = splitSeparatorCanvasObject;
        splitSeparatorCanvasObject = null;
        splitSeparatorRect = null;
        splitSeparatorImage = null;
        hasSplitSeparatorDimmedOverride = false;
        splitSeparatorDimmedOverride = false;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(canvasObject);
        else
            UnityEngine.Object.DestroyImmediate(canvasObject);
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private Texture2D GetCrosshairTexture()
    {
        int diameter = Mathf.Max(1, crosshairDiameter);
        int thickness = Mathf.Clamp(crosshairThickness, 1, diameter);

        if (crosshairTexture != null &&
            cachedCrosshairDiameter == diameter &&
            cachedCrosshairThickness == thickness &&
            cachedCrosshairColor == crosshairColor)
        {
            return crosshairTexture;
        }

        if (crosshairTexture != null)
            Destroy(crosshairTexture);

        crosshairTexture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        crosshairTexture.filterMode = FilterMode.Bilinear;
        crosshairTexture.wrapMode = TextureWrapMode.Clamp;

        float center = (diameter - 1) * 0.5f;
        float outerRadius = diameter * 0.5f;
        float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        Color clear = Color.clear;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool onRing = distance <= outerRadius && distance >= innerRadius;
                crosshairTexture.SetPixel(x, y, onRing ? crosshairColor : clear);
            }
        }

        crosshairTexture.Apply();
        cachedCrosshairDiameter = diameter;
        cachedCrosshairThickness = thickness;
        cachedCrosshairColor = crosshairColor;

        return crosshairTexture;
    }

    private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 toCamera = desiredPosition - pivot;
        float targetDistance = toCamera.magnitude;
        if (targetDistance <= 0.0001f)
            return desiredPosition;

        Vector3 direction = toCamera / targetDistance;
        float nearestDistance = targetDistance;
        if (Runner != null)
        {
            int hitCount = Runner.GetPhysicsScene().SphereCast(
                pivot,
                collisionRadius,
                direction,
                cameraCollisionHits,
                targetDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = cameraCollisionHits[i];
                if (IsOwnerCollider(hit.collider))
                    continue;

                if (hit.distance < nearestDistance)
                    nearestDistance = hit.distance;
            }
        }

        float resolvedDistance = Mathf.Max(0f, nearestDistance - collisionPadding);
        return pivot + direction * resolvedDistance;
    }

    private bool IsOwnerCollider(Collider candidate)
    {
        if (ownerColliders == null || candidate == null)
            return false;

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider == candidate)
                return true;
        }

        return false;
    }
}
