using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class HUD : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private TMP_Text healthText;

    [Header("Ammo")]
    [SerializeField] private TMP_Text ammoText;

    [Header("Scores")]
    [SerializeField] private TMP_Text redScoreText;
    [SerializeField] private TMP_Text blueScoreText;

    private Health _playerHealth;
    private WeaponController _playerWeapon;
    private bool _hooked;

    void OnEnable()
    {
        TryHookLocalPlayer();
        StartCoroutine(HookScoreManager());
    }

    private System.Collections.IEnumerator HookScoreManager()
    {
        // Wait until ScoreManager is ready (may not be spawned yet)
        while (ScoreManager.Instance == null)
            yield return null;

        ScoreManager.Instance.OnScoreChanged += UpdateScores;
        // Get current scores immediately
        UpdateScores(ScoreManager.Instance.RedScore,
                    ScoreManager.Instance.BlueScore);
    }

    void OnDisable()
    {
        if (_playerHealth != null)
            _playerHealth.OnHealthChanged -= UpdateHealth;
        if (_playerWeapon != null)
            _playerWeapon.OnAmmoChanged -= UpdateAmmo;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= UpdateScores;
        _hooked = false;
    }

    void Update()
    {
        // Keep checking for new PlayerObject after respawn
        // (cheap check — only does work if PlayerObject changed)
        if (!_hooked || NetworkManager.Singleton?.LocalClient?
            .PlayerObject?.GetComponent<Health>() != _playerHealth)
        {
            TryHookLocalPlayer();
        }
    }

    void TryHookLocalPlayer()
    {
        var localObj = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localObj == null) return;

        var newHealth = localObj.GetComponent<Health>();
        var newWeapon = localObj.GetComponent<WeaponController>();

        // Only re-hook if the PlayerObject changed (new spawn after death)
        if (newHealth == _playerHealth && _hooked) return;

        // Unsubscribe from old components (the dead player)
        if (_playerHealth != null)
            _playerHealth.OnHealthChanged -= UpdateHealth;
        if (_playerWeapon != null)
            _playerWeapon.OnAmmoChanged -= UpdateAmmo;

        // Subscribe to new components (the fresh respawned player)
        _playerHealth = newHealth;
        _playerWeapon = newWeapon;

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged += UpdateHealth;
            // Show current health immediately
            UpdateHealth(_playerHealth.CurrentHealth);
        }
        if (_playerWeapon != null)
        {
            _playerWeapon.OnAmmoChanged += UpdateAmmo;
            UpdateAmmo(_playerWeapon.CurrentMag, _playerWeapon.ReserveAmmo);
        }

        _hooked = true;
    }


    void UpdateHealth(int hp)
    {
        if (healthBar != null) healthBar.value = hp / 100f;
        if (healthText != null) healthText.text = hp.ToString();
    }

    void UpdateAmmo(int mag, int reserve)
    {
        if (ammoText != null) ammoText.text = $"{mag} / {reserve}";
    }

    void UpdateScores(int red, int blue)
    {
        if (redScoreText != null) redScoreText.text = $"RED  {red}";
        if (blueScoreText != null) blueScoreText.text = $"BLUE  {blue}";
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}