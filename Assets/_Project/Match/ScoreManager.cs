using Unity.Netcode;
using UnityEngine;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }
    public const int KillsToWin = 20;

    private NetworkVariable<int> _redScore = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _blueScore = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int RedScore => _redScore.Value;
    public int BlueScore => _blueScore.Value;

    public event System.Action<int, int> OnScoreChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _redScore.OnValueChanged += (_, __) =>
            OnScoreChanged?.Invoke(_redScore.Value, _blueScore.Value);
        _blueScore.OnValueChanged += (_, __) =>
            OnScoreChanged?.Invoke(_redScore.Value, _blueScore.Value);

        // Fire immediately so HUD gets current values on spawn
        OnScoreChanged?.Invoke(_redScore.Value, _blueScore.Value);
    }

    public void RecordKill(ulong killerClientId, ulong victimClientId)
    {
        if (!IsServer) return;

        TeamType killerTeam = TeamManager.Instance.GetTeam(killerClientId);
        Debug.Log($"[SCORE] Kill recorded. Killer={killerClientId}" +
                  $" Team={killerTeam} Before: R={_redScore.Value} B={_blueScore.Value}");

        if (killerTeam == TeamType.Red) _redScore.Value++;
        else _blueScore.Value++;

        Debug.Log($"[SCORE] After: R={_redScore.Value} B={_blueScore.Value}");

        // Pass killer's username to kill feed
        string killerName = GetPlayerUsername(killerClientId);
        string victimName = GetPlayerUsername(victimClientId);
        BroadcastKillClientRpc(killerName, victimName, killerTeam);

        if (_redScore.Value >= KillsToWin || _blueScore.Value >= KillsToWin)
            NetworkGameManager.Instance.EndMatch();
    }

    // Gets username from SessionManager's cached session data
    private string GetPlayerUsername(ulong clientId)
    {
        // Try to get from the player's NetworkObject
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            if (client.ClientId == clientId && client.PlayerObject != null)
            {
                var usernameHolder = client.PlayerObject
                    .GetComponent<PlayerUsernameHolder>();
                if (usernameHolder != null)
                    return usernameHolder.Username;
            }
        }
        return $"Player {clientId}"; // fallback
    }

    [ClientRpc]
    private void BroadcastKillClientRpc(string killerName,
        string victimName, TeamType killerTeam)
    {
        KillFeedUI.Instance?.AddKill(killerName, victimName, killerTeam);
    }

    public void ResetScores()
    {
        if (!IsServer) return;
        _redScore.Value = 0;
        _blueScore.Value = 0;
    }
}