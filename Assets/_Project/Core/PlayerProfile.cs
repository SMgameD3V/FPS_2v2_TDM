#if UNITY_EDITOR
using ParrelSync;
#endif
using UnityEngine;

public static class PlayerProfile
{
    // ParrelSync clones get a different PlayerPrefs key so they
    // don't share username data with the main editor instance
    private static string UsernameKey
    {
        get
        {
            #if UNITY_EDITOR
                        bool isClone = ClonesManager.IsClone();
                        return isClone ? "player_username_clone" : "player_username";
            #else
                        return "player_username";
            #endif
        }
    }

    private static string DefaultUsername
    {
        get
        {
            #if UNITY_EDITOR
                        bool isClone = ClonesManager.IsClone();
                        return isClone
                            ? "Player" + Random.Range(5000, 9999)
                            : "Player" + Random.Range(1000, 4999);
            #else
                        return "Player" + Random.Range(1000, 9999);
            #endif
        }
    }

    public static string Username
    {
        get => PlayerPrefs.GetString(UsernameKey, DefaultUsername);
        set
        {
            PlayerPrefs.SetString(UsernameKey, value);
            PlayerPrefs.Save();
        }
    }
}