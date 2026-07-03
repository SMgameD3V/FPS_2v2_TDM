using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class WeaponController : NetworkBehaviour
{
    [Header("Data")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask hitMask = ~0;

    private int _currentMag;
    private int _reserveAmmo;
    private float _nextFireTime;
    private bool _isReloading;
    private int _recoilIndex;
    private float _recoilResetTimer;
    private const float RecoilResetDelay = 0.4f;

    public event System.Action<int, int> OnAmmoChanged;
    public event System.Action OnReloadStarted;
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

        if (NetworkGameManager.Instance != null &&
            NetworkGameManager.Instance.CurrentState != MatchState.Playing)
            return;

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

        Ray ray = Camera.main.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0));
        Vector3 hitPoint = ray.origin + ray.direction * 200f;
        bool hitPlayer = false;

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, hitMask))
        {
            hitPoint = hit.point;
            hitPlayer = hit.collider
                .GetComponentInParent<Health>() != null;

            // Send damage to server
            FireServerRpc(hit.point, hit.normal);
        }

        // Tell server to broadcast effects to ALL clients
        RequestFireEffectsServerRpc(
            firePoint.position,
            hitPoint,
            hitPlayer);
    }

    // ── Server broadcasts fire effects to ALL clients ─────────────────────
    [ServerRpc]
    private void RequestFireEffectsServerRpc(
        Vector3 fromPosition, Vector3 hitPoint, bool hitPlayer)
    {
        PlayFireEffectsClientRpc(fromPosition, hitPoint, hitPlayer);
    }

    // ── ALL clients spawn bullet + flash + play fire sound ────────────────
    [ClientRpc]
    private void PlayFireEffectsClientRpc(
        Vector3 fromPosition, Vector3 hitPoint, bool hitPlayer)
    {
        // Spawn bullet visual on every client
        SpawnBullet(fromPosition, hitPoint);

        // Spawn muzzle flash on every client
        SpawnMuzzleFlash();

        // Play fire sound on every client
        // Use positional audio for other players, direct for owner
        if (IsOwner)
            AudioManager.Instance?.PlayFireSound();
        else
            PlaySoundAtPosition(
                AudioManager.Instance?.GetFireClip(),
                fromPosition);

        // Play hit sound on every client
        if (hitPlayer)
            AudioManager.Instance?.PlayPlayerHitSound();
        else
            AudioManager.Instance?.PlayEnvironmentHitSound();
    }

    // ── Spawn bullet locally on this client ───────────────────────────────
    private void SpawnBullet(Vector3 fromPosition, Vector3 hitPoint)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab,
            fromPosition, Quaternion.identity);

        Vector3 direction = (hitPoint - fromPosition).normalized;
        bullet.GetComponent<BulletProjectile>()?.Initialize(direction);
    }

    // ── Spawn muzzle flash at firePoint ───────────────────────────────────
    void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || firePoint == null) return;
        GameObject flash = Instantiate(muzzleFlashPrefab,
            firePoint.position, firePoint.rotation, firePoint);
        Destroy(flash, 0.15f);
    }

    // ── Play a sound at a world position (for other players' sounds) ──────
    private void PlaySoundAtPosition(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        // AudioSource.PlayClipAtPoint plays a 3D sound at a world position
        // so distant players hear it quieter — positional audio
        AudioSource.PlayClipAtPoint(clip, position, 0.8f);
    }

    // ── Server applies damage ─────────────────────────────────────────────
    [ServerRpc]
    private void FireServerRpc(Vector3 hitPoint, Vector3 hitNormal)
    {
        Collider[] cols = Physics.OverlapSphere(hitPoint, 0.3f);
        foreach (var col in cols)
        {
            var health = col.GetComponentInParent<Health>();
            if (health != null && col.transform.root != transform.root)
            {
                var targetTeam = col.GetComponentInParent<TeamMember>();
                var myTeam = GetComponent<TeamMember>();
                if (targetTeam != null && myTeam != null &&
                    targetTeam.Team == myTeam.Team) continue;

                health.TakeDamage(weaponData.damage, OwnerClientId);
                break;
            }
        }
    }

    void ApplyRecoilPattern()
    {
        if (weaponData.recoilPattern == null ||
            weaponData.recoilPattern.Length == 0) return;

        int idx = Mathf.Min(_recoilIndex,
            weaponData.recoilPattern.Length - 1);
        Vector2 recoil = weaponData.recoilPattern[idx];

        GetComponent<PlayerController>()?.AddRecoil(recoil);
        _recoilIndex++;
        _recoilResetTimer = RecoilResetDelay;
    }

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