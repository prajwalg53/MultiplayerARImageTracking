using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Lets any client grab this NetworkObject with a touch/click, then pinch to scale and
/// swipe to rotate it. Ownership is transferred to whichever client starts the gesture so that
/// NetworkTransform (owner-authoritative) can sync the resulting rotation/scale to every other
/// connected client, on any platform.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkGestureTransform : NetworkBehaviour
{
    [Header("Rotation (one-finger swipe / mouse drag)")]
    [SerializeField] float m_RotateDegreesPerPixel = 0.25f;

    [Header("Scale (two-finger pinch / mouse scroll)")]
    [SerializeField] float m_MinScale = 0.25f;
    [SerializeField] float m_MaxScale = 3f;
    [SerializeField] float m_MouseScrollScaleSpeed = 0.1f;

    /// <summary>
    /// The one shared instance currently spawned in this session, if any. Non-server clients use
    /// this to find the object that the host spawned so they can attach it to their own local
    /// AR-tracked pose without instantiating a second copy themselves.
    /// </summary>
    public static NetworkGestureTransform ActiveInstance { get; private set; }

    Camera m_Camera;
    bool m_IsDragging;
    Vector2 m_LastSingleTouchPosition;
    float m_LastPinchDistance;

    void Awake()
    {
        m_Camera = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        ActiveInstance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }
    }

    void Update()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (m_Camera == null)
        {
            m_Camera = Camera.main;
            if (m_Camera == null)
            {
                return;
            }
        }

        if (Input.touchCount == 2)
        {
            HandlePinch();
        }
        else if (Input.touchCount == 1)
        {
            HandleSingleTouchSwipe(Input.GetTouch(0));
        }
        else if (Input.touchCount == 0)
        {
            HandleMouseFallback();
        }
    }

    void HandleSingleTouchSwipe(Touch touch)
    {
        if (touch.phase == TouchPhase.Began)
        {
            m_IsDragging = IsTouchOnThisObject(touch.position);
            m_LastSingleTouchPosition = touch.position;
            return;
        }

        if (!m_IsDragging || touch.phase == TouchPhase.Canceled)
        {
            m_IsDragging = false;
            return;
        }

        if (touch.phase == TouchPhase.Ended)
        {
            m_IsDragging = false;
            return;
        }

        Vector2 delta = touch.position - m_LastSingleTouchPosition;
        m_LastSingleTouchPosition = touch.position;
        ApplySwipeRotation(delta);
    }

    void HandlePinch()
    {
        Touch touchA = Input.GetTouch(0);
        Touch touchB = Input.GetTouch(1);
        float currentDistance = Vector2.Distance(touchA.position, touchB.position);

        bool justStarted = touchA.phase == TouchPhase.Began || touchB.phase == TouchPhase.Began;
        if (justStarted)
        {
            Vector2 midpoint = (touchA.position + touchB.position) * 0.5f;
            m_IsDragging = IsTouchOnThisObject(midpoint);
            m_LastPinchDistance = currentDistance;
            return;
        }

        if (!m_IsDragging)
        {
            return;
        }

        float pinchDelta = currentDistance - m_LastPinchDistance;
        m_LastPinchDistance = currentDistance;

        // Normalize against screen size so the same finger movement feels consistent across devices.
        float scaleMultiplier = 1f + pinchDelta / Screen.height;
        ApplyScale(scaleMultiplier);
    }

    void HandleMouseFallback()
    {
        if (Input.GetMouseButtonDown(0))
        {
            m_IsDragging = IsTouchOnThisObject(Input.mousePosition);
            m_LastSingleTouchPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0) && m_IsDragging)
        {
            Vector2 currentPosition = Input.mousePosition;
            Vector2 delta = currentPosition - m_LastSingleTouchPosition;
            m_LastSingleTouchPosition = currentPosition;
            ApplySwipeRotation(delta);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            m_IsDragging = false;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0f && IsTouchOnThisObject(Input.mousePosition))
        {
            ApplyScale(1f + scroll * m_MouseScrollScaleSpeed);
        }
    }

    bool IsTouchOnThisObject(Vector2 screenPosition)
    {
        Ray ray = m_Camera.ScreenPointToRay(screenPosition);
        return Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform;
    }

    void ApplySwipeRotation(Vector2 screenDelta)
    {
        if (!EnsureOwnership())
        {
            return;
        }

        transform.Rotate(Vector3.up, -screenDelta.x * m_RotateDegreesPerPixel, Space.World);
        transform.Rotate(Vector3.right, screenDelta.y * m_RotateDegreesPerPixel, Space.World);
    }

    void ApplyScale(float multiplier)
    {
        if (!EnsureOwnership())
        {
            return;
        }

        float newScale = Mathf.Clamp(transform.localScale.x * multiplier, m_MinScale, m_MaxScale);
        transform.localScale = Vector3.one * newScale;
    }

    /// <summary>
    /// Returns true once this client is (or already was) the owner. If it isn't yet, a request is
    /// sent and the caller should skip applying this frame's delta to avoid a jump when ownership lands.
    /// </summary>
    bool EnsureOwnership()
    {
        if (IsOwner)
        {
            return true;
        }

        RequestOwnershipServerRpc(NetworkManager.LocalClientId);
        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestOwnershipServerRpc(ulong requesterClientId)
    {
        if (!IsSpawned)
        {
            return;
        }

        NetworkObject.ChangeOwnership(requesterClientId);
    }
}
