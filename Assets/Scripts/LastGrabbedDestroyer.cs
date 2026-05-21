using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using System.Collections.Generic;

public class LastGrabbedDestroyer : MonoBehaviour
{
    private GameObject lastGrabbedObject;
    private GrabInteractor[] activeInteractors;
    private List<GameObject> lockedObjects = new List<GameObject>();

    

    private void Start()
    {
        // Automatically find all GrabInteractors anywhere under the Interaction Rig
        activeInteractors = FindObjectsByType<GrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (activeInteractors.Length == 0)
        {
            Debug.LogError("LastGrabbedDestroyer: No GrabInteractors found in the scene! Ensure your MR rig is fully initialized.");
            return;
        }

        // Subscribe to the unselect events dynamically
        foreach (var interactor in activeInteractors)
        {
            interactor.WhenInteractableUnselected.Action += HandleObjectReleased;
        }

        Debug.Log($"Successfully hooked into {activeInteractors.Length} Meta Grab Interactors.");
    }

    private void OnDestroy()
    {
        // Clean up event listeners when this script is destroyed
        if (activeInteractors != null)
        {
            foreach (var interactor in activeInteractors)
            {
                if (interactor != null)
                {
                    interactor.WhenInteractableUnselected.Action -= HandleObjectReleased;
                }
            }
        }
    }

    private void HandleObjectReleased(IInteractable interactable)
    {
        if (interactable is MonoBehaviour monoBehaviour)
        {
            // Safely get the root object (handles nested child colliders gracefully)
            lastGrabbedObject = monoBehaviour.transform.root.gameObject;
            Debug.Log($"Tracked last grabbed object: {lastGrabbedObject.name}");
        }
    }

    // Call this function via your UI event or controller shortcut to delete the object
    public void DeleteLastGrabbedObject()
    {
        if (lastGrabbedObject != null)
        {
            Debug.Log($"Destroying: {lastGrabbedObject.name}");
            Destroy(lastGrabbedObject);
            lastGrabbedObject = null; // Reset tracker
        }
        else
        {
            Debug.LogWarning("No object in history to delete.");
        }
    }
    public void LockObject()
    {
        if (lastGrabbedObject != null)
        {
            Grabbable grabbable = lastGrabbedObject.GetComponent<Grabbable>();
            
            if (grabbable != null)
            {

                grabbable.enabled = false;
                foreach (Transform child in lastGrabbedObject.transform)
                {
                    if(child.CompareTag("Grabber"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
                if (!lockedObjects.Contains(lastGrabbedObject))
                {
                    lockedObjects.Add(lastGrabbedObject);

                    
                }

            Debug.Log(lastGrabbedObject.name + "Locked");
            }
            
        }
    }
    public void UnlockObject()
    {
        foreach (GameObject obj in lockedObjects)
        {
            if (obj != null)
            {
                Grabbable grabbable = obj.GetComponent<Grabbable>();

                if (grabbable != null)
                {
                    grabbable.enabled = true;
                    foreach (Transform child in obj.transform)
                    {
                        if (child.CompareTag("Grabber"))
                        {
                            child.gameObject.SetActive(true);
                        }
                    }
                    Debug.Log(lastGrabbedObject.name + "Unlocked");
                    
                }
            }
        }

        lockedObjects.Clear();
        
    }
}