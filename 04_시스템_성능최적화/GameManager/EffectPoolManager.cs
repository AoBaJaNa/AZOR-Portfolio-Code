using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class EffectPoolManager : MonoBehaviour
{
    public static EffectPoolManager Instance;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, Coroutine> autoReturnCoroutines = new Dictionary<GameObject, Coroutine>();
    private HashSet<GameObject> queuedObjects = new HashSet<GameObject>();
    private Transform inactiveSpawnRoot;

    private void Awake()
    {
        Instance = this;

        GameObject inactiveRootObject = new GameObject("__EffectPoolInactiveRoot");
        inactiveRootObject.transform.SetParent(transform, false);
        inactiveRootObject.SetActive(false);
        inactiveSpawnRoot = inactiveRootObject.transform;
    }

    public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, bool autoReturn = true)
    {
        return GetFromPoolInternal(prefab, position, rotation, autoReturn, true);
    }

    public GameObject GetFromPoolInactive(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return GetFromPoolInternal(prefab, position, rotation, false, false);
    }

    private GameObject GetFromPoolInternal(GameObject prefab, Vector3 position, Quaternion rotation, bool autoReturn, bool activateOnSpawn)
    {
        // 프리팹 자체가 Null인 경우 방어
        if (prefab == null) return null;

        if (!poolDictionary.ContainsKey(prefab))
        {
            poolDictionary.Add(prefab, new Queue<GameObject>());
        }

        GameObject obj = null;

        //  [수정] 큐에 들어있던 오브젝트가 파괴되었을 수 있으므로, 진짜 살아있는 애를 찾을 때까지 루프를 돕니다.
        while (poolDictionary[prefab].Count > 0)
        {
            obj = poolDictionary[prefab].Dequeue();
            queuedObjects.Remove(obj);

            // 유니티에서 obj == null 검사는 Destroy 되었는지 검사해 줍니다.
            if (obj != null)
            {
                break; // 살아있는 오브젝트를 찾았으면 루프 탈출!
            }
            // 만약 이미 파괴된 시체라면 그냥 무시하고 다음 녀석을 꺼냅니다.
            obj = null;
        }

        // 큐에 살아있는 애가 없었다면 새로 생성합니다.
        if (obj == null)
        {
            if (activateOnSpawn)
            {
                obj = Instantiate(prefab);
            }
            else
            {
                obj = inactiveSpawnRoot != null
                    ? Instantiate(prefab, inactiveSpawnRoot)
                    : Instantiate(prefab);

                if (obj.activeSelf)
                    obj.SetActive(false);

                obj.transform.SetParent(null, false);
            }
        }

        if (autoReturnCoroutines.TryGetValue(obj, out Coroutine pendingCoroutine) && pendingCoroutine != null)
        {
            StopCoroutine(pendingCoroutine);
            autoReturnCoroutines.Remove(obj);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;

        if (activateOnSpawn)
            obj.SetActive(true);
        else if (obj.activeSelf)
            obj.SetActive(false);

        if (autoReturn)
        {
            // 파티클 자동 반납 코루틴 실행
            Coroutine autoReturnCoroutine = StartCoroutine(ReturnToPoolAfterTime(prefab, obj));
            autoReturnCoroutines[obj] = autoReturnCoroutine;
        }

        return obj;
    }

    private IEnumerator ReturnToPoolAfterTime(GameObject prefab, GameObject obj)
    {
        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        float duration = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : 3f;

        yield return new WaitForSeconds(duration);

        // 🚨 [치명적 버그 수정] 코루틴이 쉬고 있는 사이에 오브젝트가 삭제되었는지 '먼저' 검사해야 합니다.
        if (obj == null)
        {
            autoReturnCoroutines.Remove(obj);
            // 이미 외부에서 Destroy로 지워버렸다면, 큐에 넣지 않고 조용히 코루틴을 종료합니다.
            yield break;
        }

        autoReturnCoroutines.Remove(obj);

        // 부모 자식 관계가 꼬여있을 수 있으므로 반납할 땐 부모를 풀어주는 것이 안전합니다 (선택)
        // obj.transform.SetParent(null); 

        obj.SetActive(false);

        // 딕셔너리 자체가 날아갔을 경우를 대비한 최후의 방어선
        if (poolDictionary.ContainsKey(prefab))
        {
            if (!queuedObjects.Contains(obj))
            {
                poolDictionary[prefab].Enqueue(obj);
                queuedObjects.Add(obj);
            }
        }
    }
    public void ReturnToPoolDirect(GameObject prefab, GameObject obj)
    {
        if (obj == null || prefab == null) return;

        if (autoReturnCoroutines.TryGetValue(obj, out Coroutine pendingCoroutine) && pendingCoroutine != null)
        {
            StopCoroutine(pendingCoroutine);
            autoReturnCoroutines.Remove(obj);
        }

        if (obj.activeSelf)
            obj.SetActive(false);

        if (!poolDictionary.ContainsKey(prefab))
            poolDictionary.Add(prefab, new Queue<GameObject>());

        if (poolDictionary.ContainsKey(prefab))
        {
            if (!queuedObjects.Contains(obj))
            {
                poolDictionary[prefab].Enqueue(obj);
                queuedObjects.Add(obj);
            }
        }
    }
}

