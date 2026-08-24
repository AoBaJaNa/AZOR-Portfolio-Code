using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses scaled-time yield instructions used by repeated gameplay coroutines.
/// WaitForSecondsRealtime is intentionally excluded because its wait state is mutable.
/// </summary>
public static class YieldInstructionCache
{
    private static readonly Dictionary<float, WaitForSeconds> waitCache = new();

    public static WaitForSeconds GetWait(float seconds)
    {
        if (!waitCache.TryGetValue(seconds, out WaitForSeconds wait))
        {
            wait = new WaitForSeconds(seconds);
            waitCache.Add(seconds, wait);
        }

        return wait;
    }
}

