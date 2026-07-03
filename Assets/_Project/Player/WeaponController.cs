using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class WeaponController : NetworkBehaviour
{
    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private Transform firePoint;      // tip of the gun barrel
    [SerializeField] private GameObject bulletPrefab;  // Bullet prefab with TrailRenderer
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask hitMask = ~0;

    // Runtime ammo state
    private int _currentMag;
    private int _reserveAmmo;
    private float _nextFireTime;
    private bool _isReloading;

    // Recoil pattern tracking
    private int _recoilIndex;
    private float _recoilResetTimer;
    private const float RecoilResetDelay = 0.4f;

    // Events for HUD
    public event System.Action<int, int> OnAmmoChanged;
    public event System.Action OnReloadStarted;

    // Public getters for HUD
    public int CurrentMag => _currentMag;
    public int ReserveAmmo => _reserveAmmo;

    public override void OnNetworkSpawn()
    {
        if (weaponData == null) return;
        _currentMag = weaponData.magazineSize;
        _reserveAmmo = weaponData.reserveAmmo;
    }

    void Update()
    {
        if (!IsOwner) return;

        // Only allow shooting during Playing state
        if (NetworkGameManager.Instance != null &&
            NetworkGameManager.Instance.CurrentState != MatchState.Playing)
            return;

        // Recoil pattern reset when player stops firing
        if (_recoilIndex > 0)
        {
            _recoilResetTimer -= Time.deltaTime;
            if (_recoilResetTimer <= 0f) _recoilIndex = 0;
        }

        if (Input.GetButton("Fire1") && CanFire())
            Fire();

        if (Input.GetKeyDown(KeyCode.R) && !_isReloading
            && _currentMag < weaponData.magazineSize
            && _reserveAmmo > 0)
            StartCoroutine(Reload());
    }

    bool CanFire() =>
        !_isReloading &&
        _currentMag > 0 &&
        Time.time >= _nextFireTime;

    void Fire()
    {
        _nextFireTime = Time.time + 60f / weaponData.fireRateRPM;
        _currentMag--;
        OnAmmoChanged?.Invoke(_currentMag, _reserveAmmo);

        ApplyRecoilPattern();
        SpawnMuzzleFlash();

        // Play fire sound on shooter's machine
        AudioManager.Instance?.PlayFireSound();

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Default hit point if ray hits nothing — max range
        Vector3 hitPoint = ray.origin + ray.direction * 200f;

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, hitMask))
        {
            hitPoint = hit.point;
            FireServerRpc(hit.point, hit.normal);

            // Play hit sound based on what was hit
            if (hit.collider.GetComponentInParent<Health>() != null)
                AudioManager.Instance?.PlayPlayerHitSound();
            else
                AudioManager.Instance?.PlayEnvironmentHitSound();
        }

        // Spawn visual bullet traveling toward hit point
        SpawnBullet(hitPoint);
    }

    // ── Spawn bullet prefab with TrailRenderer ────────────────────────────
    private void SpawnBullet(Vector3 hitPoint)
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab,
            firePoint.position, firePoint.rotation);

        // Direction from barrel tip toward where the ray hit
        Vector3 direction = (hitPoint - firePoint.position).normalized;

        bullet.GetComponent<BulletProjectile>()?.Initialize(direction);
    }

    // ── Spawn muzzle flash at barrel tip facing bullet direction ──────────
    void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || firePoint == null) return;

        // firePoint.rotation makes the Cone particle system
        // face the same direction as the gun barrel automatically
        GameObject flash = Instantiate(muzzleFlashPrefab,
            firePoint.position,
            firePoint.rotation,
            firePoint);

        Destroy(flash, 0.15f);
    }

    // ── Recoil pattern ────────────────────────────────────────────────────
    void ApplyRecoilPattern()
    {
        if (weaponData.recoilPattern == null ||
            weaponData.recoilPattern.Length == 0) return;

        int idx = Mathf.Min(_recoilIndex, weaponData.recoilPattern.Length - 1);
        Vector2 recoil = weaponData.recoilPattern[idx];

        GetComponent<PlayerController>()?.AddRecoil(recoil);

        _recoilIndex++;
        _recoilResetTimer = RecoilResetDelay;
    }

    // ── Server validates hit and applies damage ───────────────────────────
    [ServerRpc]
    private void FireServerRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        Collider[] cols = Physics.OverlapSphere(hitPoint, 0.3f);
        foreach (var col in cols)
        {
            var health = col.GetComponentInParent<Health>();
            if (health != null && col.transform.root != transform.root)
            {
                // Friendly fire check
                var targetTeam = col.GetComponentInParent<TeamMember>();
                var myTeam = GetComponent<TeamMember>();
                if (targetTeam != null && myTeam != null &&
                    targetTeam.Team == myTeam.Team) continue;

                health.TakeDamage(weaponData.damage, OwnerClientId);
                break;
            }
        }
    }

    // ── Reload coroutine ──────────────────────────────────────────────────
    private IEnumerator Reload()
    {
        _isReloading = true;
        OnReloadStarted?.Invoke();
        AudioManager.Instance?.PlayReloadSound();

        yield return new WaitForSeconds(weaponData.reloadTime);

        int needed = weaponData.magazineSize - _currentMag;
        int toLoad = Mathf.Min(needed, _reserveAmmo);
        _currentMag += toLoad;
        _reserveAmmo -= toLoad;

        _isReloading = false;
        OnAmmoChanged?.Invoke(_currentMag, _reserveAmmo);
    }
}