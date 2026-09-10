using System.Collections.Generic;
using UnityEngine;

public class InputBuffer : MonoBehaviour
{
    public enum InputType
    {
        PointerDown,
        PointerMove,
        PointerUp,
    }

    public struct InputEvent
    {
        public InputType type;
        public Vector2 position;
        public Vector2 delta;
        public double timestamp;
        public InputEvent(InputType t, Vector2 pos, Vector2 d, double ts)
        {
            type = t;
            position = pos;
            delta = d;
            timestamp = ts;
        }

    }

    private readonly List<InputEvent> _events = new List<InputEvent>(64);
    private readonly object _lock = new object();

    public bool isDragging { get; private set; }
    public Vector2 startPos{get; private set;}
    public Vector2 currentPos { get; private set; }
    public Vector2 dragDelta { get; private set; }

    public bool RecordMode = false;
    public bool PlaybackMode = false;
    private readonly List<InputEvent> _recorded = new List<InputEvent>(256);
    private int _playIndex = 0;
    public void EnqueueEvent(InputEvent e)
    {
        lock (_lock)
        {
            _events.Add(e);
            if (RecordMode)_recorded.Add(e);
        }

        switch (e.type)
        {
            case InputType.PointerDown:
                isDragging = true;
                startPos = e.position;
                currentPos = e.position;
                dragDelta = Vector2.zero;
                break;
            case InputType.PointerMove:
                if(isDragging)
                {
                    dragDelta = e.delta;
                    currentPos = e.position;
                }
                break;
            case InputType.PointerUp:
            isDragging = false;
                currentPos = e.position;
                dragDelta = Vector2.zero;
                break;
        }
    }

    public List<InputEvent> DequeueAll()
    {
       if(PlaybackMode)
       {
           var outList = new List<InputEvent>();
           if(_playIndex < _recorded.Count)
           {
               outList.Add(_recorded[_playIndex++]);
           }

           return outList;
       }
        List<InputEvent> snapshot;
        lock(_lock)
        {
            snapshot = new List<InputEvent>(_events);
            _events.Clear();
        }
        return snapshot;
    }

    public void Clear()
    {
        lock(_lock)
        _events.Clear();
        isDragging = false;
        dragDelta = Vector2.zero;
    }

    public void ResetPlayback()
    {
        _playIndex = 0;
    }

}
