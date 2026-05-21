using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    private ObjectSpawner spawner;

    void Start()
    {
        spawner = FindObjectOfType<ObjectSpawner>();
    }

    // Meta XR interaction / collider click
    private void OnMouseDown()
    {
        spawner.SelectObject(gameObject);
    }
}