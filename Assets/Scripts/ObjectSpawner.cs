using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ObjectSpawner : MonoBehaviour
{
    public GameObject[] objects; // All prefabs

    private int currentIndex = 0;

    // Spawn Position
    public Transform spawnPoint;

    //UI TEXT
    public TMP_Text objectNameText;

    //Spawned objects list
    private List<GameObject> spawnedObjects = new List<GameObject>();

    //Currently selected object
    private GameObject selectedObject;

    private void Start()
    {
        UpdateObjectName();
    }

    // NEXT OBJECT
    public void NextObject()
    {
        currentIndex++;

        if (currentIndex >= objects.Length)
        {
            currentIndex = 0;
        }

        UpdateObjectName();

        Debug.Log("Current Object: " + objects[currentIndex].name);
    }

    // PREVIOUS OBJECT
    public void PreviousObject()
    {
        currentIndex--;

        if (currentIndex < 0)
        {
            currentIndex = objects.Length - 1;
        }

        UpdateObjectName();

        Debug.Log("Current Object: " + objects[currentIndex].name);
    }

    // SPAWN SELECTED OBJECT
    public void SpawnObject()
    {
        GameObject spawned = Instantiate(
            objects[currentIndex],
            spawnPoint.position,
            spawnPoint.rotation);

        //Add to list
        spawnedObjects.Add(spawned);
        spawned.tag = "Placeable";

        Debug.Log("Spawned: " + spawned.name);
    }

    //UPDATE UI TEXT
    void UpdateObjectName()
    {
        objectNameText.text = objects[currentIndex].name;
    }

    // SELECT OBJECT
    public void SelectObject(GameObject obj)
    {
        selectedObject = obj;

        Debug.Log("Selected: " + obj.name);
    }

    // DELETE SELECTED OBJECT
    public void DeleteSelectedObject()
    {
        if (selectedObject != null)
        {
            spawnedObjects.Remove(selectedObject);

            Destroy(selectedObject);

            selectedObject = null;

            Debug.Log("Object Deleted");
        }
    }
}

