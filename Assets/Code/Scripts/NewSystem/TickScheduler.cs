using System;
using System.Collections.Generic;
using UnityEngine;
public enum TickPhase { Simulation = 0,Presentation = 1}

public interface ITickable
{
    void Tick(float dt);
}
public sealed class TickScheduler
{
    #region Types
    private sealed class Entry
    {
        public ITickable target;
        public string name;
        public TickPhase phase;
        public int order;
        public Func<bool> activeWhen;
        public bool removed;
        public int errorCount;
    }

    public readonly struct TickInfo
    {
        public readonly string Name;
        public readonly TickPhase Phase;
        public readonly int Order;
        public readonly bool Active;
        public readonly int ErrorCount;

        public TickInfo(string name,TickPhase phase,int order, bool active, int errorCount)
        {
            Name = name; Phase = phase; Order = order; Active = active; ErrorCount = errorCount;
        }
    }

    public sealed class PauseHandle
    {
        private TickScheduler _owner;
        public TickPhase Phase { get; }
        public string Reason { get; }

        public PauseHandle(TickScheduler owner,TickPhase phase,string reason)
        {
            _owner = owner;Phase = phase; Reason = reason;
        }
        public void Release()
        {
            if (_owner == null) return;
            _owner._pauses.Remove(this);
            _owner = null;
        }
    }
    #endregion

    #region Fields
    private readonly List<Entry> _entries = new List<Entry>();
    private readonly List<Entry> _pendingAdd = new List<Entry>();
    private readonly List<PauseHandle> _pauses = new List<PauseHandle>();
    private bool _running;
    private bool _dirty;
    #endregion

    #region Register
    public void Register(ITickable target, TickPhase phase, int order = 0, Func<bool> activeWhen = null, string name = null)
    {
        if (target == null) return;
        _pendingAdd.Add(new Entry 
        {
            target = target,
            name = string.IsNullOrEmpty(name) ? target.GetType().Name : name,
            phase = phase,
            order = order,
            activeWhen = activeWhen 
        });
        _dirty = true;
        if (!_running) Flush();
    }

    public void Unregister(ITickable target)
    {
        foreach (var e in _entries) if (e.target == target) e.removed = true;
        foreach (var e in _pendingAdd) if (e.target == target) e.removed = true;
        _dirty = true;
        if (!_running) Flush();
    }
    #endregion

    #region Pause
    public PauseHandle Pause(TickPhase phase,string reason = "")
    {
        var handle = new PauseHandle(this, phase, reason);
        _pauses.Add(handle);
        return handle;
    }

    public bool IsPaused(TickPhase phase)
    {
        for (int i = 0; i < _pauses.Count; i++)
            if (_pauses[i].Phase == phase) return true;
        return false;
    }

    public IReadOnlyList<PauseHandle> ActiveRauses => _pauses;
    #endregion

    #region Debug

    public void CollectInfo(List<TickInfo>buffer)
    {
        buffer.Clear();
        for(int i = 0;i < _entries.Count;i++)
        {
            var e = _entries[i];
            if (e.removed) continue;

            bool active;
            try { active = e.activeWhen == null || e.activeWhen(); }
            catch { active = false; }

            buffer.Add(new TickInfo(e.name, e.phase, e.order, active, e.errorCount));
        }
    }
    #endregion

    #region Run
    public void Run(TickPhase phase,float dt)
    { 
        if (IsPaused(phase)) return;
        _running = true;
        for(int i = 0;i<_entries.Count;i++)
        {
            var e = _entries[i];
            if (e.removed || e.phase != phase) continue;

            try
            {
                if (e.activeWhen != null && !e.activeWhen()) continue;
                e.target.Tick(dt);
            }
            catch(Exception ex)
            {
                if(e.errorCount == 0)
                {
                    Debug.LogError($"[TickScheduler] {e.name}({phase})で例外。以降、同じTickの例外はログを省略し、回数だけ数える");
                    Debug.LogException(ex);
                }
                e.errorCount++;
            }
        }
        _running = false;
        if (_dirty) Flush();
    }

    private void Flush()
    {
        _entries.RemoveAll(e => e.removed);
        foreach (var e in _pendingAdd) if (!e.removed) _entries.Add(e);
        _pendingAdd.Clear();

        var sorted = new List<Entry>(_entries);
        for(int i = 0;i<sorted.Count;i++)
        {
            var key = sorted[i]; int j = i - 1;
            while(j >= 0 && sorted[j].order > key.order) { sorted[j + 1] = sorted[j];j--; }
            sorted[j + 1] = key;
        }
        _entries.Clear();
        _entries.AddRange(sorted);
        _dirty = false;
    }
    #endregion
}

public sealed class ActionTickable : ITickable
{
    private readonly Action<float> _action;
    public ActionTickable(Action<float> action) { _action = action; }
    public void Tick(float dt) => _action(dt);
}

