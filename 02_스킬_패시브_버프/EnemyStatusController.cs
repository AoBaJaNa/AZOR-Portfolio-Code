using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStatusController : MonoBehaviour
{
    private readonly Dictionary<EnemyStatusKey, RuntimeEnemyStatusEntry> runtimeStatuses = new Dictionary<EnemyStatusKey, RuntimeEnemyStatusEntry>();

    private EnemyStatusBarView statusBarView;

    public void BindView(EnemyStatusBarView view)
    {
        statusBarView = view;

        if (statusBarView == null)
            return;

        foreach (RuntimeEnemyStatusEntry entry in runtimeStatuses.Values)
            statusBarView.ShowOrUpdateStatus(entry.definition, entry.stackCount, entry.remainingTime);
    }

    public void UnbindView()
    {
        statusBarView = null;
    }

    public void SetPersistentStatus(EnemyStatusKey key, bool visible, Sprite icon, bool showCount = false, int stackCount = 0, string displayName = "")
    {
        if (!visible)
        {
            RemoveStatus(key);
            return;
        }

        RuntimeEnemyStatusEntry entry = GetOrCreateEntry(key, new EnemyStatusDefinition(key, icon, showCount, false, displayName));
        entry.isTimed = false;
        entry.stackCount = stackCount;
        entry.remainingTime = 0f;
        entry.duration = 0f;

        UpdateView(entry);
    }

    public void ApplyTimedStatus(
        EnemyStatusKey key,
        Sprite icon,
        float duration,
        bool showCount = false,
        int stackCount = 0,
        string displayName = "",
        Action onActivated = null,
        Action onExpired = null)
    {
        RuntimeEnemyStatusEntry entry = GetOrCreateEntry(key, new EnemyStatusDefinition(key, icon, showCount, true, displayName));
        bool wasInactive = !entry.isTimed;

        entry.definition.icon = icon;
        entry.definition.showCount = showCount;
        entry.definition.showDuration = true;
        entry.stackCount = stackCount;
        entry.isTimed = true;
        entry.duration = duration;
        entry.remainingTime = duration;
        entry.onActivated = onActivated;
        entry.onExpired = onExpired;

        if (wasInactive)
            entry.onActivated?.Invoke();

        if (entry.timerRoutine == null)
            entry.timerRoutine = StartCoroutine(RunTimedStatus(entry));

        UpdateView(entry);
    }

    public void RemoveStatus(EnemyStatusKey key)
    {
        if (!runtimeStatuses.TryGetValue(key, out RuntimeEnemyStatusEntry entry))
            return;

        if (entry.timerRoutine != null)
            StopCoroutine(entry.timerRoutine);

        runtimeStatuses.Remove(key);
        entry.onExpired?.Invoke();
        statusBarView?.HideStatus(key);
    }

    public void ClearAllStatuses()
    {
        foreach (RuntimeEnemyStatusEntry entry in runtimeStatuses.Values)
        {
            if (entry.timerRoutine != null)
                StopCoroutine(entry.timerRoutine);

            entry.onExpired?.Invoke();
        }

        runtimeStatuses.Clear();
        statusBarView?.ClearAll();
    }

    private RuntimeEnemyStatusEntry GetOrCreateEntry(EnemyStatusKey key, EnemyStatusDefinition definition)
    {
        if (runtimeStatuses.TryGetValue(key, out RuntimeEnemyStatusEntry existingEntry))
        {
            existingEntry.definition = definition;
            return existingEntry;
        }

        RuntimeEnemyStatusEntry entry = new RuntimeEnemyStatusEntry
        {
            definition = definition
        };
        runtimeStatuses.Add(key, entry);
        return entry;
    }

    private IEnumerator RunTimedStatus(RuntimeEnemyStatusEntry entry)
    {
        while (entry.remainingTime > 0f)
        {
            entry.remainingTime = Mathf.Max(0f, entry.remainingTime - Time.deltaTime);
            UpdateView(entry);
            yield return null;
        }

        EnemyStatusKey key = entry.definition.key;
        entry.timerRoutine = null;
        entry.onExpired?.Invoke();
        runtimeStatuses.Remove(key);
        statusBarView?.HideStatus(key);
    }

    private void UpdateView(RuntimeEnemyStatusEntry entry)
    {
        statusBarView?.ShowOrUpdateStatus(entry.definition, entry.stackCount, entry.remainingTime);
    }

    private void OnDisable()
    {
        ClearAllStatuses();
    }
}

