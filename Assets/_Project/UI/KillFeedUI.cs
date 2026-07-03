using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class KillFeedUI : MonoBehaviour
{
    public static KillFeedUI Instance { get; private set; }

    [SerializeField] private GameObject killEntryPrefab;
    [SerializeField] private Transform feedContainer;
    [SerializeField] private int maxEntries = 5;
    [SerializeField] private float entryLifetime = 4f;

    private Queue<GameObject> _entries = new Queue<GameObject>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Now receives actual names instead of client IDs
    public void AddKill(string killerName, string victimName,
        TeamType killerTeam)
    {
        string color = killerTeam == TeamType.Red
            ? "#FF4444" : "#4488FF";

        string text = $"<color={color}>{killerName}</color>" +
                      $" ✦ {victimName}";

        if (_entries.Count >= maxEntries)
        {
            var old = _entries.Dequeue();
            if (old != null) Destroy(old);
        }

        var entry = Instantiate(killEntryPrefab, feedContainer);
        var label = entry.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;
        _entries.Enqueue(entry);

        StartCoroutine(FadeOutAndRemove(entry));
    }

    private IEnumerator FadeOutAndRemove(GameObject entry)
    {
        yield return new WaitForSeconds(entryLifetime - 0.5f);

        var cg = entry.GetComponent<CanvasGroup>();
        if (cg == null) cg = entry.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            if (entry == null) yield break;
            cg.alpha = 1f - (elapsed / 0.5f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (entry != null) Destroy(entry);
    }
}