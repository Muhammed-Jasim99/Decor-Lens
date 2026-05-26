using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class SavedObjectData
{
    public string uuid;
    public string prefabName;
    public Vector3 savedScale;
}

[System.Serializable]
public class RoomSaveContainer
{
    public List<SavedObjectData> savedObjects = new List<SavedObjectData>();
}

public class MRScenePersistence : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string targetTag = "Placeable";
    private const string SaveKey = "MR_Spatial_Scene_Flat_Simple";

    public async void SaveCurrentScene()
    {
        GameObject[] objectsToSave = GameObject.FindGameObjectsWithTag(targetTag);
        if (objectsToSave.Length == 0)
        {
            Debug.LogWarning("No objects found with tag: " + targetTag);
            return;
        }

        Debug.Log($"Saving {objectsToSave.Length} individual items...");
        await SaveRoutineAsync(objectsToSave);
    }

    private async Task SaveRoutineAsync(GameObject[] objects)
    {
        RoomSaveContainer saveContainer = new RoomSaveContainer();

        foreach (GameObject obj in objects)
        {
            // Attach anchor component directly to the live object if it doesn't have one
            OVRSpatialAnchor anchor = obj.GetComponent<OVRSpatialAnchor>();
            if (anchor == null)
            {
                anchor = obj.AddComponent<OVRSpatialAnchor>();
            }

            // Wait for Meta framework to initialize this specific object point
            while (!anchor.Created)
            {
                await Task.Yield();
            }

            // Lock this individual anchor to the headset's physical memory
            var saveResult = await anchor.SaveAnchorAsync();

            if (saveResult.Success)
            {
                string cleanName = obj.name.Replace("(Clone)", "").Trim();

                SavedObjectData data = new SavedObjectData
                {
                    uuid = anchor.Uuid.ToString(),
                    prefabName = cleanName,
                    savedScale = obj.transform.localScale
                };

                saveContainer.savedObjects.Add(data);
                Debug.Log($"Successfully saved individual item: {cleanName} ({anchor.Uuid})");
            }
            else
            {
                Debug.LogError($"Failed to save individual spatial anchor for {obj.name}. Status: {saveResult.Status}");
            }
        }

        // Commit configuration data to player preferences
        string json = JsonUtility.ToJson(saveContainer);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log("Flat scene save complete!");
    }

    public async void LoadSavedScene()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            Debug.LogWarning("No saved Mixed Reality scene profiles found.");
            return;
        }

        // Wipe out all current items to clear the room before loading
        GameObject[] existingObjects = GameObject.FindGameObjectsWithTag(targetTag);
        foreach (GameObject obj in existingObjects) Destroy(obj);

        string json = PlayerPrefs.GetString(SaveKey);
        RoomSaveContainer saveContainer = JsonUtility.FromJson<RoomSaveContainer>(json);

        List<Guid> uuidsToLoad = new List<Guid>();
        Dictionary<Guid, SavedObjectData> uuidToDataMap = new Dictionary<Guid, SavedObjectData>();

        foreach (var item in saveContainer.savedObjects)
        {
            if (Guid.TryParse(item.uuid, out Guid guid))
            {
                uuidsToLoad.Add(guid);
                uuidToDataMap[guid] = item;
            }
        }

        if (uuidsToLoad.Count > 0)
        {
            await LoadRoutineAsync(uuidsToLoad, uuidToDataMap);
        }
    }

    private async Task LoadRoutineAsync(List<Guid> uuids, Dictionary<Guid, SavedObjectData> uuidToDataMap)
    {
        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();

        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors);
        if (!loadResult.Success)
        {
            Debug.LogError("Meta runtime failed to look up individual tracking ids.");
            return;
        }

        foreach (var unboundAnchor in unboundAnchors)
        {
            if (!unboundAnchor.Localized)
            {
                bool localizeSuccess = await unboundAnchor.LocalizeAsync();
                if (!localizeSuccess) continue;
            }

            SavedObjectData trackingData = uuidToDataMap[unboundAnchor.Uuid];
            GameObject prefabAsset = Resources.Load<GameObject>(trackingData.prefabName);

            if (prefabAsset != null)
            {
                // CRITICAL RULE: Instantiate the prefab completely clean at absolute zero (0,0,0)
                GameObject spawnedItem = Instantiate(prefabAsset, Vector3.zero, Quaternion.identity);

                // Set up basic attributes before anchor binding takes over
                spawnedItem.tag = targetTag;
                spawnedItem.transform.localScale = trackingData.savedScale;

                // Add the anchor component directly onto our fresh gameplay object
                var spatialAnchorComponent = spawnedItem.AddComponent<OVRSpatialAnchor>();

                // BIND: Meta takes control over the object's transform, instantly teleporting 
                // it from (0,0,0) to its true, individual real-world position!
                unboundAnchor.BindTo(spatialAnchorComponent);

                Debug.Log($"Individual item restored and localized: {trackingData.prefabName}");
            }
        }
    }
}


