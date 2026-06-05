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

        // ADDED: Individual primitive elements to hold the item's transform scaling signatures safely
        public float savedScaleX;
        public float savedScaleY;
        public float savedScaleZ;
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
        //debugText.text = "Gathering placeable objects...";
        GameObject[] placeableObjects = GameObject.FindGameObjectsWithTag(TargetTag);

        if (placeableObjects.Length == 0)
        {
            //debugText.text = "No objects found to save.";
            return;
        }

        SaveAllPlaceablesAsync(placeableObjects);
    }

    private async void SaveAllPlaceablesAsync(GameObject[] objectsToAnchor)
    {
        AnchorSaveCollection collection = new AnchorSaveCollection();
        _activeAnchors.Clear();

        //debugText.text = $"Baking {objectsToAnchor.Length} anchors...";

        foreach (GameObject go in objectsToAnchor)
        {
            if (!go.TryGetComponent<AnchorIdentity>(out var identity))
            {
                Debug.LogWarning($"Skipping {go.name}: Missing AnchorIdentity script component!");
                continue;
            }

            // Note down the custom transform scale the player manipulated in the scene BEFORE adding the anchor
            Vector3 currentObjectScale = go.transform.localScale;

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

                // ADDED: Store scale properties into serialization block
                data.savedScaleX = currentObjectScale.x;
                data.savedScaleY = currentObjectScale.y;
                data.savedScaleZ = currentObjectScale.z;

                collection.anchors.Add(data);

                Debug.Log($"[SAVED OK] Meta Anchor: {data.uuid} | Prefab: {data.prefabId} | Scale: {currentObjectScale}");
            }
            else
            {
                Debug.LogError($"Meta failed to save anchor. Status: {result.Status}");
            }
        }

        if (collection.anchors.Count > 0)
        {
            string json = JsonUtility.ToJson(collection);
            PlayerPrefs.SetString(AnchorRegistryKey, json);
            PlayerPrefs.Save();

            debugText.text = "Saved successfully!";
        }
        else
        {
            //debugText.text = "Failed to save any anchors via Meta.";
        }
    }

    public void OnLoad(InputAction.CallbackContext context)
    {
        //debugText.text = "Loading registry...";

        if (!PlayerPrefs.HasKey(AnchorRegistryKey))
        {
            //debugText.text = "No saved anchor registry found.";
            return;
        }

        string json = PlayerPrefs.GetString(AnchorRegistryKey);
        AnchorSaveCollection collection = JsonUtility.FromJson<AnchorSaveCollection>(json);

        if (collection == null || collection.anchors.Count == 0)
        {
            //debugText.text = "Saved data registry is empty.";
            return;
        }

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
        //debugText.text = "Querying anchors from Meta...";

        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, _unboundAnchors);

        if (result.Success && _unboundAnchors.Count > 0)
        {
            int loadCount = 0;

            foreach (var unboundAnchor in _unboundAnchors)
            {
                bool localizationSuccess = await unboundAnchor.LocalizeAsync();

                if (localizationSuccess)
                {
                    string unboundUuidStr = unboundAnchor.Uuid.ToString();

                    // Create a reference placeholder for our matched registry dataset entry
                    AnchorSaveData matchedData = null;

                    foreach (var savedAnchor in savedCollection.anchors)
                    {
                        if (savedAnchor.uuid.Equals(unboundUuidStr, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedData = savedAnchor;
                            break;
                        }
                    }

                    if (matchedData == null) continue;

                    GameObject prefabToSpawn = GetPrefabById(matchedData.prefabId);
                    if (prefabToSpawn == null)
                    {
                        Debug.LogError($"Prefab identity lookup failed for ID string: '{matchedData.prefabId}'");
                        continue;
                    }

                    // Build spatial driver hierarchy node
                    GameObject anchorDriverRoot = new GameObject($"SpatialAnchor_{matchedData.prefabId}");
                    var spatialAnchor = anchorDriverRoot.AddComponent<OVRSpatialAnchor>();

                    unboundAnchor.BindTo(spatialAnchor);

                    // Re-instantiate the distinct prefab element onto the tracked coordinate framework
                    GameObject visualObject = Instantiate(prefabToSpawn, anchorDriverRoot.transform);
                    visualObject.transform.localPosition = Vector3.zero;
                    visualObject.transform.localRotation = Quaternion.identity;
                    visualObject.tag = TargetTag;

                    // FIX / ADDED: Explicitly re-apply the structural scale values from the JSON data back onto the local child object
                    visualObject.transform.localScale = new Vector3(matchedData.savedScaleX, matchedData.savedScaleY, matchedData.savedScaleZ);

                    loadCount++;
                }
            }

            debugText.text = "loaded successfully!";
        }
        else
        {
            //debugText.text = $"0 items loaded successfully.";
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

