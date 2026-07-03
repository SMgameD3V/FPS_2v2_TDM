using Unity.Netcode;
using UnityEngine;

public class PlayerUsernameHolder : NetworkBehaviour
{
    // Synced to all clients so kill feed can read it anywhere
    private NetworkVariable<Unity.Collections.FixedString64Bytes> _username =
        new NetworkVariable<Unity.Collections.FixedString64Bytes>("",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    public string Username => _username.Value.ToString();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Set our username from local PlayerPrefs when we spawn
            _username.Value = PlayerProfile.Username;
            Debug.Log($"[USERNAME] Set to: {PlayerProfile.Username}" +
                      $" for client {OwnerClientId}");
        }
    }
}