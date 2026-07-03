using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -20f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Transform cameraHolder;

    [Header("Recoil Recovery")]
    [SerializeField] private float recoilRecoverySpeed = 8f;

    private CharacterController _cc;
    private Vector3 _velocity;
    private float _xRotation;

    // Recoil applied per-shot, decays over time
    private float _recoilX;
    private float _recoilY;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (Camera.main != null)
        {
            Camera.main.transform.SetParent(cameraHolder);
            Camera.main.transform.localPosition = Vector3.zero;
            Camera.main.transform.localRotation = Quaternion.identity;
        }
    }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        // Hide gun model for other players (first-person — only owner sees it)
        var weaponMount = transform.Find("WeaponMount");
        if (weaponMount != null)
        {
            foreach (var rend in
                weaponMount.GetComponentsInChildren<Renderer>())
            {
                rend.enabled = true;
            }
        }
    }

    void Update()
    {
        // CRITICAL — only owner runs input
        if (!IsOwner) return;

        HandleMouseLook();
        HandleMovement();
        DecayRecoil();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Vertical look — combine mouse input with recoil kick
        _xRotation -= mouseY;
        _xRotation += _recoilY; // recoil.y is negative so this goes UP
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);

        cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Horizontal rotation — body turns with mouse + horizontal recoil
        transform.Rotate(Vector3.up * (mouseX + _recoilX));

        // Consume recoil this frame so it doesn't stack infinitely
        _recoilY = 0f;
        _recoilX = 0f;
    }

    void HandleMovement()
    {
        bool isGrounded = _cc.isGrounded;
        if (isGrounded && _velocity.y < 0f) _velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        bool sprinting = Input.GetKey(KeyCode.LeftShift);

        float speed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 move = transform.right * x + transform.forward * z;
        _cc.Move(move * speed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && isGrounded)
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);

        // Footsteps — only plays for the local player
        bool isMoving = (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f) && isGrounded;
        bool isSprinting = Input.GetKey(KeyCode.LeftShift);

        AudioManager.Instance?.HandleFootsteps(isMoving, isSprinting);

        // Sync to other clients via server
        if (isMoving)
            RequestFootstepServerRpc(isSprinting,transform.position);
    }

    // ── Called by WeaponController each shot ──────────────────────────────
    public void AddRecoil(Vector2 recoil)
    {
        // FIXED: negative Y so camera kicks UP not down
        _recoilY -= recoil.y;
        _recoilX += recoil.x;
    }

    // ── Smoothly returns view toward center between shots ─────────────────
    void DecayRecoil()
    {
        // These are consumed each frame in HandleMouseLook
        // so this just ensures leftover recoil bleeds off
        _recoilX = Mathf.Lerp(_recoilX, 0f, Time.deltaTime * recoilRecoverySpeed);
        _recoilY = Mathf.Lerp(_recoilY, 0f, Time.deltaTime * recoilRecoverySpeed);
    }
    [ServerRpc]
    private void RequestFootstepServerRpc(bool isSprinting,
        Vector3 position)
    {
        PlayFootstepClientRpc(isSprinting, position);
    }

    [ClientRpc]
    private void PlayFootstepClientRpc(bool isSprinting,Vector3 position)
    {
        if (IsOwner) return; // owner already plays locally above
        AudioClip clip = isSprinting? AudioManager.Instance?.GetRunClip(): AudioManager.Instance?.GetWalkClip();
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, 0.5f);
    }
}