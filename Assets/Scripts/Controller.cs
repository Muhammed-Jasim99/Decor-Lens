using UnityEngine;
using UnityEngine.InputSystem;

public class Controller : MonoBehaviour
{
    InputMaps control;

    public GameObject menu;

    private void Awake()
    {
        
        control = new InputMaps();
    }

    private void OnEnable()
    {
        control.Enable();
        control.UI.Menu.performed += ToggleMenu;
    }

    private void OnDisable()
    {
        control.UI.Menu.performed -= ToggleMenu;
        control.Disable();
    }


    private void ToggleMenu(InputAction.CallbackContext context)
    {
        if (menu.activeInHierarchy)
        {
            menu.SetActive(false);
        }
        else
        {
            menu.SetActive(true);
        }
    }
}
