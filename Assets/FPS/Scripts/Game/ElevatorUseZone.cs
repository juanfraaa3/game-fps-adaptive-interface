using UnityEngine;
using Unity.FPS.Gameplay; // para ObjectiveElevatorPoints
using Unity.FPS.Game;


public class ElevatorUseZone : MonoBehaviour
{
    [Header("Refs")]
    public ObjectiveElevatorPoints Elevator;  // arrastra aquí el ascensor

    [Header("Opcional (PC test)")]
    public bool EnableKeyboardTest = true;

    // ⬜ Cuadrado (PS) / X (Xbox) con Input Manager clásico
    const KeyCode SQUARE_PAD = KeyCode.JoystickButton2;

    bool _playerInside;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInside = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _playerInside = false;
    }

    void Update()
    {
        if (!_playerInside || Elevator == null) return;

        // Presionado de Cuadrado en gamepad o tecla de prueba en PC
        if (Input.GetKeyDown(SQUARE_PAD) || (EnableKeyboardTest && Input.GetKeyDown(KeyCode.E)))
        {
            if (!Elevator.HasAlreadyUsed)
            {
                Elevator.StartMoveUp();
            }
            else
            {
                Elevator.StartMoveDown(); // 👈 ahora también puede bajar (si waves terminadas)
            }
        }
    }
}
