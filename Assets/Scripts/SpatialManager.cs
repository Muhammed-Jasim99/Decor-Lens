using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using TMPro;

public class SpatialManager : MonoBehaviour
{
    [Header("Setup References")]
    [Tooltip("Drop ALL your different placeable prefabs here.")]
    [SerializeField] private List<GameObject> placeablePrefabs = new List<GameObject>();
    [SerializeField] private TextMeshProUGUI debugText;

    private InputMaps inputController;

    private List<OVRSpatialAnchor> _activeAnchors = new();
    private List<OVRSpatialAnchor.UnboundAnchor> _unboundAnchors = new();

    private const string AnchorRegistryKey = "SavedAnchorRegistry_V2";
    private const string TargetTag = "Placeable";

    // A structural wrapper to make saving data bulletproof
    [Serializable]
    public class AnchorSaveData
    {
        public string uuid;
        public string prefabId;
    }

    [Serializable]
    public class AnchorSaveCollection
    {
        public List<AnchorSaveData> anchors = new List<AnchorSaveData>();
    }

    private void Awake()
    {
        inputController = new InputMaps();
    }

    private void OnEnable()
    {
        inputController.Enable();
        inputController.UI.Save.performed += OnSave;
        inputController.UI.Load.performed += OnLoad;
    }

    private void OnDisable()
    {
        inputController.UI.Save.performed -= OnSave;
        inputController.UI.Load.performed -= OnLoad;
        inputController.Disable();
    }

    public void OnSave(InputAction.CallbackContext context)
    {
        debugText.text = "Gathering placeable objects...";
        GameObject[] placeableObjects = GameObject.FindGameObjectsWithTag(TargetTag);

        if (placeableObjects.Length == 0)
        {
            debugText.text = "No objects found to save.";
            return;
        }

        SaveAllPlaceablesAsync(placeableObjects);
    }

    private async void SaveAllPlaceablesAsync(GameObject[] objectsToAnchor)
    {
        AnchorSaveCollection collection = new AnchorSaveCollection();
        _activeAnchors.Clear();

        debugText.text = $"Baking {objectsToAnchor.Length} anchors...";

        foreach (GameObject go in objectsToAnchor)
        {
            if (!go.TryGetComponent<AnchorIdentity>(out var identity))
            {
                Debug.LogWarning($"Skipping {go.name}: Missing AnchorIdentity script component!");
                continue;
            }

            if (!go.TryGetComponent<OVRSpatialAnchor>(out var anchor))
            {
                anchor = go.AddComponent<OVRSpatialAnchor>();
            }

            while (!anchor.Created)
            {
                await Task.Yield();
            }

            var result = await anchor.SaveAnchorAsync();

            if (result.Success)
            {
                _activeAnchors.Add(anchor);

                // Construct clean serializable data points
                AnchorSaveData data = new AnchorSaveData();
                data.uuid = anchor.Uuid.ToString();
                data.prefabId = identity.prefabId;
                collection.anchors.Add(data);

                Debug.Log($"[SAVED OK] Meta Anchor: {data.uuid} matched to Prefab: {data.prefabId}");
            }
            else
            {
                Debug.LogError($"Meta failed to save anchor. Status: {result.Status}");
            }
        }

        if (collection.anchors.Count > 0)
        {
            // Convert the data cleanly to JSON to prevent string split corruption bugs
            string json = JsonUtility.ToJson(collection);
            PlayerPrefs.SetString(AnchorRegistryKey, json);
            PlayerPrefs.Save();

            debugText.text = $"Saved {collection.anchors.Count} items successfully!";
        }
        else
        {
            debugText.text = "Failed to save any anchors via Meta.";
        }
    }

    public void OnLoad(InputAction.CallbackContext context)
    {
        debugText.text = "Loading registry...";

        if (!PlayerPrefs.HasKey(AnchorRegistryKey))
        {
            debugText.text = "No saved anchor registry found.";
            return;
        }

        string json = PlayerPrefs.GetString(AnchorRegistryKey);
        AnchorSaveCollection collection = JsonUtility.FromJson<AnchorSaveCollection>(json);

        if (collection == null || collection.anchors.Count == 0)
        {
            debugText.text = "Saved data registry is empty.";
            return;
        }

        // Reconstruct pure Guids for Meta query
        List<Guid> uuidsToLoad = new List<Guid>();
        foreach (var item in collection.anchors)
        {
            if (Guid.TryParse(item.uuid, out Guid parsedGuid))
            {
                uuidsToLoad.Add(parsedGuid);
            }
        }

        LoadAnchorsByUuid(uuidsToLoad, collection);
    }

    private async void LoadAnchorsByUuid(IEnumerable<Guid> uuids, AnchorSaveCollection savedCollection)
    {
        _unboundAnchors.Clear();
        debugText.text = "Querying anchors from Meta...";

        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, _unboundAnchors);

        if (result.Success && _unboundAnchors.Count > 0)
        {
            int loadCount = 0;

            // Loop completely through the anchors Meta verified exist in space
            foreach (var unboundAnchor in _unboundAnchors)
            {
                bool localizationSuccess = await unboundAnchor.LocalizeAsync();

                if (localizationSuccess)
                {
                    // FIXED LOOKUP: Find the matching layout block by converting to string explicitly
                    string unboundUuidStr = unboundAnchor.Uuid.ToString();
                    string targetPrefabId = "";

                    foreach (var savedAnchor in savedCollection.anchors)
                    {
                        if (savedAnchor.uuid.Equals(unboundUuidStr, StringComparison.OrdinalIgnoreCase))
                        {
                            targetPrefabId = savedAnchor.prefabId;
                            break;
                        }
                    }

                    GameObject prefabToSpawn = GetPrefabById(targetPrefabId);
                    if (prefabToSpawn == null)
                    {
                        Debug.LogError($"Prefab identity lookup failed for ID string: '{targetPrefabId}'");
                        continue;
                    }

                    // Build spatial driver hierarchy node
                    GameObject anchorDriverRoot = new GameObject($"SpatialAnchor_{targetPrefabId}");
                    var spatialAnchor = anchorDriverRoot.AddComponent<OVRSpatialAnchor>();

                    unboundAnchor.BindTo(spatialAnchor);

                    // Re-instantiate the distinct prefab element onto the tracked coordinate framework
                    GameObject visualObject = Instantiate(prefabToSpawn, anchorDriverRoot.transform);
                    visualObject.transform.localPosition = Vector3.zero;
                    visualObject.transform.localRotation = Quaternion.identity;
                    visualObject.tag = TargetTag;

                    loadCount++;
                }
            }

            debugText.text = $"{loadCount} items loaded successfully!";
        }
        else
        {
            // If this logs out 0, check your PlayerPrefs setup
            debugText.text = $"0 items loaded successfully.";
            Debug.LogWarning($"Meta query returned success code: {result.Success}, but found {_unboundAnchors.Count} anchors in your physical area.");
        }
    }

    private GameObject GetPrefabById(string id)
    {
        foreach (GameObject prefab in placeablePrefabs)
        {
            if (prefab != null && prefab.TryGetComponent<AnchorIdentity>(out var identity))
            {
                if (identity.prefabId == id)
                {
                    return prefab;
                }
            }
        }
        return null;
    }
}


