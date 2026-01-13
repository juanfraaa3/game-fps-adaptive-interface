
/*
using UnityEngine;

public class JoystickDebugger : MonoBehaviour
{
    void Update()
    {
        // Detectar botones digitales
        //for (int i = 0; i < 20; i++)
        //{
        //if (Input.GetKey("joystick button " + i))
        //Debug.Log("Botón presionado: " + i);
        //}

        // Detectar ejes analógicos
        string[] axes = {
            "X axis", "Y axis", "3rd axis", "4th axis", "5th axis", "6th axis",
            "7th axis", "8th axis", "9th axis", "10th axis", "11th axis", "12th axis"
        };

        for (int a = 0; a < axes.Length; a++)
        {
            float val = Input.GetAxis("Axis " + (a + 1));
            if (Mathf.Abs(val) > 0.1f)
                Debug.Log($"Eje {a + 1} ({axes[a]}): {val}");
        }
    }
}
*/