using TMPro;
using UnityEngine;

public class ShowLocation : MonoBehaviour
{
    public TextMeshProUGUI myLocation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        myLocation.text = transform.position.ToString();
    }
}
