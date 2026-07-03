using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Weapon Sounds")]
    [SerializeField] private AudioSource weaponAudioSource;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip reloadSound;

    [Header("Hit Sounds")]
    [SerializeField] private AudioClip playerHitSound;
    [SerializeField] private AudioClip[] environmentHitSounds; // random pick

    [Header("Player Sounds")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip respawnSound;

    [Header("Footstep Sounds")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private AudioClip runSound;
    [SerializeField] private float walkStepInterval = 0.5f;
    [SerializeField] private float runStepInterval = 0.3f;

    private float _footstepTimer;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Weapon ────────────────────────────────────────────────────────────
    public void PlayFireSound()
    {
        if (fireSound == null || weaponAudioSource == null) return;
        weaponAudioSource.PlayOneShot(fireSound);
    }

    public void PlayReloadSound()
    {
        if (reloadSound == null || weaponAudioSource == null) return;
        weaponAudioSource.PlayOneShot(reloadSound);
    }

    // ── Hit sounds ────────────────────────────────────────────────────────
    public void PlayPlayerHitSound()
    {
        if (playerHitSound == null || weaponAudioSource == null) return;
        weaponAudioSource.PlayOneShot(playerHitSound);
    }

    public void PlayEnvironmentHitSound()
    {
        if (environmentHitSounds == null ||
            environmentHitSounds.Length == 0 ||
            weaponAudioSource == null) return;

        // Pick a random clip from the array each time
        int index = Random.Range(0, environmentHitSounds.Length);
        weaponAudioSource.PlayOneShot(environmentHitSounds[index]);
    }

    // ── Player ────────────────────────────────────────────────────────────
    public void PlayDeathSound()
    {
        if (deathSound == null || playerAudioSource == null) return;
        playerAudioSource.PlayOneShot(deathSound);
    }

    public void PlayRespawnSound()
    {
        if (respawnSound == null || playerAudioSource == null) return;
        playerAudioSource.PlayOneShot(respawnSound);
    }

    // ── Footsteps ─────────────────────────────────────────────────────────
    // Call this from PlayerController.HandleMovement() every frame
    public void HandleFootsteps(bool isMoving, bool isSprinting)
    {
        if (!isMoving)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            AudioClip clip = isSprinting ? runSound : walkSound;
            float interval = isSprinting
                ? runStepInterval : walkStepInterval;

            if (clip != null && footstepAudioSource != null)
                footstepAudioSource.PlayOneShot(clip);

            _footstepTimer = interval;
        }
    }

    public AudioClip GetFireClip() => fireSound;
    public AudioClip GetWalkClip() => walkSound;
    public AudioClip GetRunClip() => runSound;
    public AudioClip GetDeathClip() => deathSound;
}