using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class PlayerSplitScreenCamera : NetworkBehaviour
{
    private const int SplitScreenPlayerCount = 2;
    private const float SeparatorWidth = 2f;

    private static readonly Rect[] SplitScreenRects = new Rect[]
    {
        new Rect(0f, 0f, 0.5f, 1f),
        new Rect(0.5f, 0f, 0.5f, 1f)
    };
    private static readonly List<PlayerSplitScreenCamera> registeredCameras = new List<PlayerSplitScreenCamera>();
    private static Camera splitScreenBackgroundCamera;

    [Header("Third Person Settings")]
    public Vector3 pivotOffset = new Vector3(0.35f, 1.35f, 0f);
    public float distance = 4f;
    public float sideOffset = 0.2f;
    public float followSmoothTime = 0.04f;
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.1f;

    [Header("Crosshair")]
    public bool showCrosshair = true;
    public Color crosshairColor = Color.white;
    public int crosshairDiameter = 18;
    public int crosshairThickness = 2;

    private Camera cam;
    private PlayerInputHandler inputHandler;
    private PlayerMovement playerMovement;
    private Transform target;
    private Collider[] ownerColliders;
    private Vector3 followVelocity;
    private Transform originalParent;
    private Texture2D crosshairTexture;
    private readonly RaycastHit[] cameraCollisionHits = new RaycastHit[16];
    private int cachedCrosshairDiameter;
    private int cachedCrosshairThickness;
    private Color cachedCrosshairColor;
    private bool initialized;

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

        DrawSplitSeparator();

        if (!showCrosshair || cam == null || !cam.enabled)
            return;

        Texture2D texture = GetCrosshairTexture();
        if (texture == null)
            return;

        Rect cameraPixelRect = cam.pixelRect;
        float centerX = cameraPixelRect.x + cameraPixelRect.width * 0.5f;
        float centerY = Screen.height - (cameraPixelRect.y + cameraPixelRect.height * 0.5f);
        float diameter = Mathf.Max(1, crosshairDiameter);

        GUI.DrawTexture(
            new Rect(centerX - diameter * 0.5f, centerY - diameter * 0.5f, diameter, diameter),
            texture);
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
            DestroySplitScreenBackgroundCamera();
        else
            UpdateSplitScreenCameras();
    }

    private static void UpdateSplitScreenCameras()
    {
        registeredCameras.RemoveAll(IsInvalidRegisteredCamera);

        if (registeredCameras.Count == 0)
        {
            DestroySplitScreenBackgroundCamera();
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

    private static void DrawSplitSeparator()
    {
        Color oldColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(
            new Rect(Screen.width * 0.5f - SeparatorWidth * 0.5f, 0f, SeparatorWidth, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = oldColor;
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
