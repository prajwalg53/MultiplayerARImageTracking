using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(NetworkObject))]
public class ImageTrackingObjectManager : NetworkBehaviour
{
    [SerializeField]
    [Tooltip("Image manager on the AR Session Origin")]
    ARTrackedImageManager m_ImageManager;

    // bool IsTracking;

    /// <summary>
    /// Get the <c>ARTrackedImageManager</c>
    /// </summary>
    public ARTrackedImageManager ImageManager
    {
        get => m_ImageManager;
        set => m_ImageManager = value;
    }

    [SerializeField]
    [Tooltip("Reference Image Library")]
    XRReferenceImageLibrary m_ImageLibrary;

    /// <summary>
    /// Get the <c>XRReferenceImageLibrary</c>
    /// </summary>
    public XRReferenceImageLibrary ImageLibrary
    {
        get => m_ImageLibrary;
        set => m_ImageLibrary = value;
    }

    [SerializeField]
    [Tooltip("Prefab for tracked 1 image")]
    GameObject m_OnePrefab;

    /// <summary>
    /// Get the one prefab
    /// </summary>
    public GameObject onePrefab
    {
        get => m_OnePrefab;
        set => m_OnePrefab = value;
    }

    GameObject m_SpawnedOnePrefab;

    /// <summary>
    /// get the spawned one prefab
    /// </summary>
    public GameObject spawnedOnePrefab
    {
        get => m_SpawnedOnePrefab;
        set => m_SpawnedOnePrefab = value;
    }



    NumberManager m_OneNumberManager;

    static Guid s_FirstImageGUID;

    void OnEnable()
    {
        s_FirstImageGUID = m_ImageLibrary[0].guid;

        m_ImageManager.trackedImagesChanged += ImageManagerOnTrackedImagesChanged;
    }

    void OnDisable()
    {
        m_ImageManager.trackedImagesChanged -= ImageManagerOnTrackedImagesChanged;
    }

    void ImageManagerOnTrackedImagesChanged(ARTrackedImagesChangedEventArgs obj)
    {
        // added, spawn (or attach to) the shared networked prefab
        foreach (ARTrackedImage image in obj.added)
        {
            if (image.referenceImage.guid == s_FirstImageGUID)
            {
                EnsureSharedObject();
            }
            UpdateARImage(image);
        }

        // updated, set prefab position only - rotation/scale are driven by NetworkGestureTransform
        // and synced over the network, so this manager must never write to them.
        foreach (ARTrackedImage image in obj.updated)
        {
            if (image.trackingState == TrackingState.Tracking)
            {
                if (image.referenceImage.guid == s_FirstImageGUID)
                {
                    EnsureSharedObject();
                    UpdateARImage(image);
                }
            }
            else
            {
                if (image.referenceImage.guid == s_FirstImageGUID && m_SpawnedOnePrefab != null)
                {
                    var anchor = m_SpawnedOnePrefab.GetComponent<ARAnchor>();
                    if (anchor != null)
                    {
                        Destroy(anchor);
                    }
                }
            }
        }

        // removed, drop the anchor so a re-detection re-assigns the pose
        foreach (ARTrackedImage image in obj.removed)
        {
            if (image.referenceImage.guid == s_FirstImageGUID && m_SpawnedOnePrefab != null)
            {
                if (m_SpawnedOnePrefab.GetComponent<ARAnchor>() == null)
                {
                    UpdateARImage(image);
                }
            }
        }
    }

    bool m_SpawnRequested;

    /// <summary>
    /// Only the host/server actually spawns the shared NetworkObject (once). Any client - not just
    /// the host - can trigger this the moment its own AR tracking detects the marker; non-server
    /// clients ask the host over RPC instead of spawning a copy themselves.
    /// </summary>
    void EnsureSharedObject()
    {
        if (m_SpawnedOnePrefab != null || !IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            SpawnSharedObject();
        }
        else if (NetworkGestureTransform.ActiveInstance != null)
        {
            m_SpawnedOnePrefab = NetworkGestureTransform.ActiveInstance.gameObject;
        }
        else if (!m_SpawnRequested)
        {
            m_SpawnRequested = true;
            RequestSpawnServerRpc();
        }
    }

    void SpawnSharedObject()
    {
        if (NetworkGestureTransform.ActiveInstance != null)
        {
            m_SpawnedOnePrefab = NetworkGestureTransform.ActiveInstance.gameObject;
            return;
        }

        m_SpawnedOnePrefab = Instantiate(m_OnePrefab);
        m_SpawnedOnePrefab.GetComponent<NetworkObject>().Spawn();
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestSpawnServerRpc()
    {
        SpawnSharedObject();
    }

    public void UpdateARImage(ARTrackedImage trackedImage)
    {
        AssignGameObject(trackedImage);
    }

    public void AssignGameObject(ARTrackedImage rTrackedImage)
    {
        // The host may not have spawned the shared object yet (e.g. this client detected the
        // marker first); nothing to position until it exists.
        if (m_SpawnedOnePrefab == null)
        {
            return;
        }

        if (m_SpawnedOnePrefab.GetComponent<ARAnchor>() == null)
        {
            m_SpawnedOnePrefab.AddComponent<ARAnchor>();
        }

        // Position only: every client tracks the marker independently, but rotation/scale come
        // from NetworkGestureTransform's pinch/swipe gestures and are synced over the network.
        m_SpawnedOnePrefab.transform.position = rTrackedImage.transform.position;
        m_SpawnedOnePrefab.SetActive(true);
    }
}
