using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private float respawnDelay = 3f;

    private Dictionary<ulong, GameObject> _spawnedPlayers = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SpawnAllPlayers()
    {
        if (!IsServer) return;
        Debug.Log("[SPAWNER] SpawnAllPlayers called");
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            SpawnPlayer(client.ClientId);
    }

    public void SpawnPlayer(ulong clientId)
    {
        if (!IsServer) return;

        // Step 1: Remove any existing tracked player for this client
        if (_spawnedPlayers.TryGetValue(clientId, out var existing)
            && existing != null)
        {
            var oldNet = existing.GetComponent<NetworkObject>();
            if (oldNet != null && oldNet.IsSpawned)
                oldNet.Despawn(true);
            _spawnedPlayers.Remove(clientId);
        }

        // Step 2: Remove any NGO-assigned PlayerObject for this client
        // This catches any auto-spawned object NGO created on connect
        if (NetworkManager.Singleton.ConnectedClients
            .TryGetValue(clientId, out var connectedClient))
        {
            if (connectedClient.PlayerObject != null
                && connectedClient.PlayerObject.IsSpawned)
            {
                connectedClient.PlayerObject.Despawn(true);
            }
        }

        // Step 3: Small delay then spawn at correct position
        StartCoroutine(SpawnAfterCleanup(clientId));
    }

    public void RequestRespawn(ulong clientId)
    {
        if (!IsServer) return;
        StartCoroutine(RespawnAfterDelay(clientId, respawnDelay));
    }

    private IEnumerator RespawnAfterDelay(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_spawnedPlayers.TryGetValue(clientId, out var old) &&
            old != null)
        {
            var netObj = old.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn();
        }

        if (NetworkGameManager.Instance.CurrentState == MatchState.Playing)
            SpawnPlayer(clientId);
    }

    // Tells the specific client to play their respawn sound
    [ClientRpc]
    private void NotifyRespawnClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
            AudioManager.Instance?.PlayRespawnSound();
    }

    public void RemovePlayer(ulong clientId)
    {
        if (_spawnedPlayers.ContainsKey(clientId))
            _spawnedPlayers.Remove(clientId);
    }

    private IEnumerator SpawnAfterCleanup(ulong clientId)
    {
        // Wait 1 frame to ensure Despawn calls above complete
        yield return null;

        TeamType team = TeamManager.Instance.GetTeam(clientId);
        Transform spawnPoint = SpawnManager.Instance.GetSpawnPoint(team);

        GameObject player = Instantiate(playerPrefab,
            spawnPoint.position, spawnPoint.rotation);

        player.GetComponent<NetworkObject>()
            .SpawnAsPlayerObject(clientId, true);

        _spawnedPlayers[clientId] = player;
        NotifyRespawnClientRpc(clientId);
    }
}