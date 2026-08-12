using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight runtime pool for combat VFX (muzzle, trails, hit FX).
/// Avoids Instantiate/Destroy spikes during rapid fire.
/// </summary>
public static class CombatVfxPool
{
    const int DefaultPrewarm = 4;
    const int MaxPerPrefab = 48;

    static readonly Dictionary<int, Stack<GameObject>> Pools = new Dictionary<int, Stack<GameObject>>(16);
    static CombatVfxPoolRunner _runner;
    static Transform _root;

    static CombatVfxPoolRunner Runner
    {
        get
        {
            if (_runner != null) return _runner;
            var go = new GameObject("[CombatVfxPool]");
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
            _runner = go.AddComponent<CombatVfxPoolRunner>();
            return _runner;
        }
    }

    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        int key = prefab.GetInstanceID();
        if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>(DefaultPrewarm);
            Pools[key] = stack;
        }

        GameObject instance = null;
        while (stack.Count > 0 && instance == null)
        {
            instance = stack.Pop();
            if (instance == null) continue;
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab);
            instance.name = prefab.name + " (Pooled)";
        }

        Transform t = instance.transform;
        if (parent != null)
        {
            t.SetParent(parent, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            // Caller often wants world pose at fire point — apply after parent
            t.SetPositionAndRotation(position, rotation);
        }
        else
        {
            t.SetParent(_root, false);
            t.SetPositionAndRotation(position, rotation);
        }

        instance.SetActive(true);
        return instance;
    }

    public static void Release(GameObject prefab, GameObject instance, float delay = 0f)
    {
        if (instance == null) return;

        if (delay > 0f)
        {
            Runner.ReleaseAfter(prefab, instance, delay);
            return;
        }

        ReleaseNow(prefab, instance);
    }

    static void ReleaseNow(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;

        instance.SetActive(false);
        instance.transform.SetParent(_root, false);

        if (prefab == null)
        {
            Object.Destroy(instance);
            return;
        }

        int key = prefab.GetInstanceID();
        if (!Pools.TryGetValue(key, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>(DefaultPrewarm);
            Pools[key] = stack;
        }

        if (stack.Count >= MaxPerPrefab)
        {
            Object.Destroy(instance);
            return;
        }

        stack.Push(instance);
    }

    sealed class CombatVfxPoolRunner : MonoBehaviour
    {
        public void ReleaseAfter(GameObject prefab, GameObject instance, float delay)
        {
            StartCoroutine(ReleaseRoutine(prefab, instance, delay));
        }

        IEnumerator ReleaseRoutine(GameObject prefab, GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReleaseNow(prefab, instance);
        }
    }
}
