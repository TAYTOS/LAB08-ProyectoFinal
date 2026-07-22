using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float gravity = -9.81f;

    [Header("Cámara (Mirar con el Mouse)")]
    public float mouseSensitivity = 0.1f;
    public Transform playerCamera;
    
    private CharacterController controller;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private Light flashlight;

    void Awake()
    {
        // 1. Configuración Automática del Controlador
        controller = GetComponent<CharacterController>();
        
        // 2. Configuración Automática de la Cámara
        if (playerCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.transform.parent == transform)
            {
                playerCamera = mainCam.transform;
            }
            else
            {
                // Si no tiene cámara hija, le creamos una automáticamente
                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(transform);
                camObj.transform.localPosition = new Vector3(0, 0.6f, 0); // Altura de los ojos
                Camera cam = camObj.AddComponent<Camera>();
                cam.tag = "MainCamera";
                playerCamera = camObj.transform;
            }
        }

        // 3. Configuración Automática de Tags y Mecánicas de Ansiedad
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Player";
        }
        
        if (GetComponent<AnxietyManager>() == null)
        {
            gameObject.AddComponent<AnxietyManager>();
        }

        // 4. Configurar Linterna
        GameObject flashObj = new GameObject("Flashlight");
        flashObj.transform.SetParent(playerCamera);
        flashObj.transform.localPosition = Vector3.zero;
        flashObj.transform.localRotation = Quaternion.identity;
        
        flashlight = flashObj.AddComponent<Light>();
        flashlight.type = LightType.Spot;
        flashlight.range = 80f;
        flashlight.spotAngle = 75f;
        flashlight.intensity = 6f;
        flashlight.color = new Color(0.9f, 0.95f, 1f); // Luz blanca/fría
        flashlight.enabled = false; // Empieza apagada

        // Ocultar y bloquear el cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Si el juego está en pausa, no procesar movimiento ni rotación de cámara
        if (Time.timeScale == 0f) return;

        Look();
        Move();
        
        // Alternar linterna
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            flashlight.enabled = !flashlight.enabled;
        }
    }

    void Look()
    {
        // Usar el Nuevo Sistema de Input para el Mouse
        if (Mouse.current == null) return;
        
        float mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity;
        float mouseY = Mouse.current.delta.y.ReadValue() * mouseSensitivity;

        // Rotar al jugador en el eje Y (izquierda/derecha)
        transform.Rotate(Vector3.up * mouseX);

        // Rotar la cámara en el eje X (arriba/abajo) con límite
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f);
        playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void Move()
    {
        if (Keyboard.current == null) return;
        
        // Obtener input del teclado manualmente (Nuevo Sistema)
        float x = 0f;
        float z = 0f;
        if (Keyboard.current.dKey.isPressed) x += 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.wKey.isPressed) z += 1f;
        if (Keyboard.current.sKey.isPressed) z -= 1f;

        // Determinar velocidad
        float currentSpeed = Keyboard.current.shiftKey.isPressed ? sprintSpeed : walkSpeed;

        // Mover en la dirección a la que miramos
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Aplicar gravedad
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Mantenerlo pegado al suelo
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
