using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; 

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStats))] 
public class PlayerController : NetworkBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    private Rigidbody rb;
    private PlayerStats playerStats; 

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference lookAction;
    public InputActionReference turnLeftAction;
    public InputActionReference turnRightAction;
    public InputActionReference callPodAction; 
    public InputActionReference boostAction; 
    public InputActionReference pauseAction; 

    [Header("Movement Variables")]
    public float acceleration = 15f;
    public float deceleration = 10f;

    [Header("Look Variables")]
    public float mouseSensitivity = 0.5f;
    public float turnSmoothTime = 0.15f;

    [Header("Boost Skill Settings")]
    public float boostCooldown = 12f;
    public float boostUpwardVelocity = 12f; 
    public float hoverDuration = 2.5f;
    public float hoverSpeedMultiplier = 1.5f;
    public AudioClip boostSoundClip;
    public AudioClip landSoundClip;

    private float targetRotationY;
    private float currentRotationVelocity;
    private Vector2 moveInput;
    
    // 부스트 제어 변수
    private float lastBoostTime = -100f;
    private bool isHovering = false;
    private bool isGrounded = true;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerStats = GetComponent<PlayerStats>();
        audioSource = GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (playerCamera != null) playerCamera.gameObject.SetActive(true);
            
            targetRotationY = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            EnableInputs();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            DisableInputs();
        }
    }

    private void EnableInputs()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        
        turnLeftAction.action.Enable();
        turnLeftAction.action.performed += _ => targetRotationY -= 90f;

        turnRightAction.action.Enable();
        turnRightAction.action.performed += _ => targetRotationY += 90f;

        callPodAction.action.Enable();
        callPodAction.action.performed += _ => playerStats.CallDropPod();

        if (boostAction != null && boostAction.action != null)
        {
            boostAction.action.Enable();
            boostAction.action.performed += _ => TryBoost();
        }
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.Enable();
            pauseAction.action.performed += _ => TryTogglePause();
        }
    }

    private void DisableInputs()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        turnLeftAction.action.Disable();
        turnRightAction.action.Disable();
        callPodAction.action.Disable();
        if (boostAction != null && boostAction.action != null) boostAction.action.Disable();
        if (pauseAction != null && pauseAction.action != null) pauseAction.action.Disable();
    }

    private void TryBoost()
    {
        if (!IsOwner || playerStats.isDown.Value) return;

        if (isGrounded && Time.time >= lastBoostTime + boostCooldown)
        {
            StartCoroutine(BoostSequence());
        }
    }

    private System.Collections.IEnumerator BoostSequence()
    {
        lastBoostTime = Time.time;
        isGrounded = false;

        rb.constraints = RigidbodyConstraints.FreezeRotation; 
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, boostUpwardVelocity, rb.linearVelocity.z);
        
        if (audioSource != null && boostSoundClip != null)
        {
            audioSource.PlayOneShot(boostSoundClip);
        }

        yield return new WaitForSeconds(0.4f);

        isHovering = true;
        rb.useGravity = false;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); 

        yield return new WaitForSeconds(hoverDuration);

        isHovering = false;
        rb.useGravity = true;
    }

    public float GetBoostCooldownTimer()
    {
        return Mathf.Max(0, (lastBoostTime + boostCooldown) - Time.time);
    }

    void Update()
    {
        if (!IsOwner) return;
        if (playerStats.isDown.Value) return; 

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        targetRotationY += lookInput.x * mouseSensitivity;

        moveInput = moveAction.action.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;

        if (playerStats.isDown.Value) 
        {
            isHovering = false;
            rb.useGravity = true;
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        HandleMovement();
        ApplyRotation();
    }

    private void HandleMovement()
    {
        float currentSpeed = playerStats.GetMoveSpeed();
        if (isHovering) currentSpeed *= hoverSpeedMultiplier; 

        Vector3 targetVelocity = (transform.right * moveInput.x + transform.forward * moveInput.y) * currentSpeed;
        
        targetVelocity.y = rb.linearVelocity.y;

        float speedChangeRate = moveInput.sqrMagnitude > 0.01f ? acceleration : deceleration;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, speedChangeRate * Time.fixedDeltaTime);
    }

    private void ApplyRotation()
    {
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotationY, ref currentRotationVelocity, turnSmoothTime);
        rb.MoveRotation(Quaternion.Euler(0f, smoothedAngle, 0f));
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            // 체공 상태에서 바닥에 닿은 최초 시점에만 착지음 재생
            if (!isGrounded)
            {
                if (audioSource != null && landSoundClip != null)
                {
                    audioSource.PlayOneShot(landSoundClip);
                }
            }

            isGrounded = true;
            // 착지 시 Y축 고정을 다시 복원합니다.
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
    }
    private void TryTogglePause()
    {
        if (!IsOwner) return;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestTogglePauseRpc();
        }
    }
}