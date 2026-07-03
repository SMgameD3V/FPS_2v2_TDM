using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 100;

    // Default 0 — server sets it to maxHealth in OnNetworkSpawn
    // OnValueChanged fires automatically and syncs to all clients
    private NetworkVariable<int> _currentHealth =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public int CurrentHealth => _currentHealth.Value;
    public int MaxHealth => maxHealth;
    public bool IsDead => _currentHealth.Value <= 0;

    public event System.Action<int> OnHealthChanged;
    public event System.Action<ulong> OnDied;

    public override void OnNetworkSpawn()
    {
        // Subscribe to changes — fires on all clients when value changes
        _currentHealth.OnValueChanged += (_, newVal) =>
            OnHealthChanged?.Invoke(newVal);

        if (IsServer)
        {
            // Server sets health — OnValueChanged syncs it to clients
            // including this client, which updates the HUD correctly
            _currentHealth.Value = maxHealth;
        }
    }

    // Only called on server (from WeaponController.FireServerRpc)
    public void TakeDamage(int amount, ulong killerClientId)
    {
        if (!IsServer || IsDead) return;

        _currentHealth.Value =
            Mathf.Max(0, _currentHealth.Value - amount);

        if (_currentHealth.Value <= 0)
            HandleDeath(killerClientId);
    }

    private void HandleDeath(ulong killerClientId)
    {
        // Notify score and kill feed
        ScoreManager.Instance.RecordKill(killerClientId, OwnerClientId);

        // Play death sound only on the dying player's machine
        if (IsOwner)
            AudioManager.Instance?.PlayDeathSound();

        OnDied?.Invoke(killerClientId);

        // Trigger 3-second respawn countdown on server
        PlayerSpawner.Instance.RequestRespawn(OwnerClientId);
    }
}