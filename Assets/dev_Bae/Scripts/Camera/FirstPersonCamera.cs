using UnityEngine;
using Fusion;

public class FirstPersonCamera : NetworkBehaviour
{
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
        if (HasInputAuthority)
        {
            inputHandler = GetComponentInParent<PlayerInputHandler>();

            if (target != null)
                ownerColliders = target.GetComponentsInChildren<Collider>();

            transform.SetParent(null, true);
            initialized = false;
            followVelocity = Vector3.zero;

            cam.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            cam.enabled = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasInputAuthority)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (originalParent != null)
                transform.SetParent(originalParent, true);
        }
    }

    private void OnDestroy()
    {
        if (crosshairTexture != null)
            Destroy(crosshairTexture);
    }

    public override void Render()
    {
        if (!HasInputAuthority || target == null) return;

        float yaw = inputHandler != null ? inputHandler.CameraYaw : target.eulerAngles.y;
        float pitch = inputHandler != null ? inputHandler.CameraPitch : 0f;
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
        if (!HasInputAuthority || !showCrosshair)
            return;

        Texture2D texture = GetCrosshairTexture();
        if (texture == null)
            return;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float diameter = Mathf.Max(1, crosshairDiameter);

        GUI.DrawTexture(
            new Rect(centerX - diameter * 0.5f, centerY - diameter * 0.5f, diameter, diameter),
            texture);
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
