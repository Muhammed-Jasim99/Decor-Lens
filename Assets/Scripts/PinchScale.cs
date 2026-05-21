using UnityEngine;

public class PinchScale : MonoBehaviour
{
    private float initialDistance;
    private Vector3 initialScale;

    public float minScale = 0.3f;
    public float maxScale = 3f;

    void Update()
    {
        // Check 2 fingers touching
        if (Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            // Distance between fingers
            float currentDistance =
                Vector2.Distance(touch1.position, touch2.position);

            // Save initial values
            if (touch1.phase == TouchPhase.Began ||
                touch2.phase == TouchPhase.Began)
            {
                initialDistance = currentDistance;
                initialScale = transform.localScale;
            }
            else
            {
                if (initialDistance > 0)
                {
                    // Calculate scale factor
                    float scaleFactor =
                        currentDistance / initialDistance;

                    // Apply scale
                    Vector3 newScale =
                        initialScale * scaleFactor;

                    // Limit minimum and maximum size
                    newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
                    newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
                    newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);

                    transform.localScale = newScale;
                }
            }
        }
    }
}
