using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float gravity = -22f;

    [Header("Salto y Físicas")]
    public float jumpHeight = 2.4f;
    public float coyoteTime = 0.2f;
    public float jumpBufferTime = 0.18f;
    public LayerMask groundCheckLayers = ~0;

    private float coyoteCounter = 0f;
    private float jumpBufferCounter = 0f;
    private bool isJumping = false;

    [Header("Cámara (Mirar con el Mouse)")]
    public float mouseSensitivity = GameSettings.DefaultMouseSensitivity;
    public Transform playerCamera;
    
    [Header("Cámara 3ra Persona")]
    public bool isThirdPerson = false;
    public float cameraDistance = 3.5f;
    public float cameraHeight = 1.5f;
    public Vector3 cameraOffset = new Vector3(0.5f, 0f, 0f); // Offset lateral (ej. sobre el hombro)
    
    private Transform cameraPivot;
    private CharacterController controller;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private Light flashlight;
    private Vector3 lastSafePosition;

    public static SimplePlayerController Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        lastSafePosition = transform.position;
        mouseSensitivity = GameSettings.MouseSensitivity;
        isThirdPerson = false; // Forzar a primera persona por defecto
        
        // 1. Configuración Automática del Controlador y Físicas
        controller = GetComponent<CharacterController>();
        controller.slopeLimit = 55f;
        controller.stepOffset = 0.5f;
        controller.skinWidth = 0.08f;
        controller.minMoveDistance = 0f;

        // Evitar conflictos con Rigidbody dinámico si existe en el objeto
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
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
                // Creamos un pivote para la cámara para manejar la rotación vertical independientemente
                GameObject pivotObj = new GameObject("CameraPivot");
                pivotObj.transform.SetParent(transform);
                pivotObj.transform.localPosition = isThirdPerson ? new Vector3(0, cameraHeight, 0) : new Vector3(0, 0.6f, 0);
                cameraPivot = pivotObj.transform;

                // Si no tiene cámara hija, le creamos una automáticamente atada al pivote
                GameObject camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(cameraPivot);
                
                if (isThirdPerson)
                {
                    // Posicionamos la cámara atrás y con el offset deseado
                    camObj.transform.localPosition = new Vector3(cameraOffset.x, cameraOffset.y, -cameraDistance);
                }
                else
                {
                    // Primera persona
                    camObj.transform.localPosition = Vector3.zero;
                }
                
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
        if (Time.timeScale <= 0f || OptionsMenuUI.IsOpen || GameIntroductionUI.IsOpen
            || OptionsMenuUI.EscapeConsumedThisFrame) return;

        Look();
        Move();

        // 1. Red de seguridad contra caídas fuera del mapa (Void Fallback)
        if (transform.position.y < -15f)
        {
            RespawnAtSafePosition();
        }
        else if (controller != null && controller.isGrounded && transform.position.y >= -1.0f)
        {
            lastSafePosition = transform.position;
        }
        
        // Alternar linterna
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            flashlight.enabled = !flashlight.enabled;
        }

        // Alternar perspectiva (1ra / 3ra persona)
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            TogglePerspective();
        }
    }

    void OnEnable()
    {
        mouseSensitivity = GameSettings.MouseSensitivity;
        GameSettings.SensitivityChanged += ApplyMouseSensitivity;
    }

    void OnDisable() { GameSettings.SensitivityChanged -= ApplyMouseSensitivity; }
    void ApplyMouseSensitivity(float value) { mouseSensitivity = value; }

    void TogglePerspective()
    {
        isThirdPerson = !isThirdPerson;
        
        if (cameraPivot != null && playerCamera != null)
        {
            // Ajustar altura del pivote
            cameraPivot.localPosition = isThirdPerson ? new Vector3(0, cameraHeight, 0) : new Vector3(0, 0.6f, 0);
            
            // Ajustar posición de la cámara relativa al pivote
            if (isThirdPerson)
            {
                playerCamera.localPosition = new Vector3(cameraOffset.x, cameraOffset.y, -cameraDistance);
            }
            else
            {
                playerCamera.localPosition = Vector3.zero;
            }
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

        // Rotar la cámara o el pivote en el eje X (arriba/abajo) con límite
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f);
        
        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
        else
        {
            playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    void Move()
    {
        // Obtener input del teclado manualmente (soporta nuevo Input System y clásico)
        float x = 0f;
        float z = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
        }
        else
        {
            x = Input.GetAxisRaw("Horizontal");
            z = Input.GetAxisRaw("Vertical");
        }

        // Determinar velocidad
        bool isSprinting = (Keyboard.current != null && Keyboard.current.shiftKey.isPressed) || Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // Mover en la dirección a la que miramos
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Comprobación de suelo híbrida (CharacterController + SphereCast para rampas y desniveles 3D)
        bool isGrounded = controller.isGrounded;
        if (!isGrounded && velocity.y <= 0.1f)
        {
            Vector3 sphereOrigin = transform.position + Vector3.up * 0.2f;
            if (Physics.SphereCast(sphereOrigin, 0.25f, Vector3.down, out RaycastHit hit, 0.35f, groundCheckLayers, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit)
                {
                    isGrounded = true;
                }
            }
        }

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            isJumping = false;
            if (velocity.y < 0f)
            {
                velocity.y = -3.5f; // Mantener pegado al suelo y a rampas en bajada
            }
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        // Buffer de entrada de salto (Espacio)
        bool jumpPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Space);
        if (jumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // Ejecutar salto
        if (jumpBufferCounter > 0f && coyoteCounter > 0f && !isJumping)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            isJumping = true;
        }

        // Salto variable: al soltar la tecla mientras sube rápido, se reduce manteniendo altura mínima segura
        bool jumpReleased = (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Space);
        float minJumpVelocity = Mathf.Sqrt(1.1f * -2f * gravity);
        if (jumpReleased && velocity.y > minJumpVelocity && isJumping)
        {
            velocity.y = minJumpVelocity;
        }

        // Aplicar gravedad
        velocity.y += gravity * Time.deltaTime;

        // Unificar movimiento horizontal y vertical en UNA SOLA llamada por frame
        // Esto previene jittering, garantiza isGrounded consistente y respuesta inmediata en rampas
        Vector3 totalMovement = (move * currentSpeed + velocity) * Time.deltaTime;
        CollisionFlags flags = controller.Move(totalMovement);

        // Si choca contra un techo mientras salta, cancelar la inercia ascendente
        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
        {
            velocity.y = -0.5f;
        }
    }

    /// <summary>
    /// Teletransporta al jugador de forma segura reseteando velocidades físicas, acumuladores de salto y CharacterController.
    /// </summary>
    public void TeleportTo(Vector3 targetPosition, Quaternion? targetRotation = null)
    {
        if (controller != null) controller.enabled = false;

        velocity = Vector3.zero;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        isJumping = false;

        transform.position = targetPosition;
        lastSafePosition = targetPosition;

        if (targetRotation.HasValue)
        {
            transform.rotation = targetRotation.Value;
        }

        Physics.SyncTransforms();

        if (controller != null) controller.enabled = true;
    }

    /// <summary>
    /// Reubica al jugador en su última posición segura conocida si cae al abismo.
    /// </summary>
    public void RespawnAtSafePosition()
    {
        Debug.LogWarning("<b>[PlayerController]</b> El jugador cayó fuera del mapa. Reapareciendo en zona segura.");
        TeleportTo(lastSafePosition + Vector3.up * 0.5f);
    }
}
