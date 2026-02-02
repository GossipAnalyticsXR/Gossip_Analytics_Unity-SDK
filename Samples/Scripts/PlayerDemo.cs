using UnityEngine;
using GossipSDK.Components; // using components for Gossip
using GossipSDK.Heatmaps;
using GossipSDK.Core;

public class PlayerDemo : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpForce = 2f;
    public float gravity = -9.81f;

    [Header("Mouse")]
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;

    [Header("UI")]
    public GameObject panelPause;

    [Header("Sent Interaction")]
    public bool sentInteractionHeatmap = false;
    public bool sentInteractionObjectImage = false;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private PauseComponent pauseComponent;
    private bool pause = false;
    public GameObject currentInteraction;
    private InteractableComponent interactableComponent;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        pauseComponent = GetComponent<PauseComponent>();
        interactableComponent = GetComponent<InteractableComponent>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

    }

    void Update()
    {
        Move();
        Look();

        // Demo Use PauseComponent
        if (Input.GetKeyDown(KeyCode.Escape)) 
        {
            if (panelPause == null) return;
            Pause();
        }

        if (Input.GetKeyDown(KeyCode.E)) Interaction();

    }

    void Move()
    {
        if (pause) return;

        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            Gossip.Instance.UserEventTracker?.CaptureEvent("Level", "CompleteLevel");
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void Look()
    {
        if (pause) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Pause() 
    {
        pause = !pause;
        panelPause.SetActive(pause);

        if (pause) pauseComponent.OnPause();
        else pauseComponent.OnResume();
    }

    void Interaction() 
    {
        if (currentInteraction != null) 
        {
            if (sentInteractionHeatmap) interactableComponent.OnInteractInstant("PickUp");
        }
    }
}
