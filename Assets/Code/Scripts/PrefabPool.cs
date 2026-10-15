using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>プールに戻される時に実行。再利用に向けた初期化を行う</summary>
public interface IPoolable
{
    void OnReleasedToPool();
}
/// <summary>
/// Prefabごとの使いまわし置き場。非アクティブな親の下に置くため、待機中のObjectはAwake/OnEnableが走らず,Combatant.Allにも入らない
/// </summary>
public class PrefabPool
{
    private readonly Transform _root;
    private readonly Dictionary<GameObject, Stack<GameObject>> _free = new();
    private readonly Dictionary<GameObject, GameObject> _prefabOf = new();
    private readonly HashSet<GameObject> _pooled = new();

    public PrefabPool(Transform root)
    {
        _root = root;
    }

    private Stack<GameObject> GetStack(GameObject prefab)
    {
        if(!_free.TryGetValue(prefab,out var stack))
        {
            stack = new Stack<GameObject>();
            _free[prefab] = stack;
        }
        return stack;
    }

    public int FreeCount(GameObject prefab)
    {
        return _free.TryGetValue(prefab, out var stack) ? stack.Count : 0;
    }
    /// <summary>空きをひとつ増やす　Load工程で呼ぶ</summary>
    public void Prewarm(GameObject prefab)
    {
        if (prefab == null) return;

        var inst = Object.Instantiate(prefab, _root);
        inst.name = prefab.name;
        _prefabOf[inst] = prefab;
        _pooled.Add(inst);
        GetStack(prefab).Push(inst);
    }
    /// <summary>空きがあれば再利用し、無ければ生成</summary>
    public GameObject Get(GameObject prefab,Vector3 position)
    {
        if (prefab == null) return null;

        GameObject inst = null;
        var stack = GetStack(prefab);
        while (stack.Count > 0 && inst == null) inst = stack.Pop();

        if(inst == null)
        {
            inst = Object.Instantiate(prefab, _root);
            inst.name = prefab.name;
            _prefabOf[inst] = prefab;
        }

        _pooled.Remove(inst);
        inst.transform.SetParent(null, false);
        inst.transform.position = position;
        inst.transform.rotation = Quaternion.identity;
        inst.SetActive(true);
        return inst;
    }
    /// <summary>プール管理外のObjectはそのままDestroy</summary>
    public void Release(GameObject inst)
    {
        if (inst == null) return;

        if(!_prefabOf.TryGetValue(inst,out var prefab))
        {
            Object.Destroy(inst);
            return;
        }
        if (!_pooled.Add(inst)) return; //二重返却？

        inst.GetComponent<IPoolable>()?.OnReleasedToPool();
        inst.transform.SetParent(_root, false);
        GetStack(prefab).Push(inst);
    }
}
