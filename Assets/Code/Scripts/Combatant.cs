using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Combatant : MonoBehaviour,ICombatant
{
    private static readonly List<Combatant> _all = new();
    public static IReadOnlyList<Combatant> All => _all;

    private Base_Item _data;
    private MatchManager _matchManager;

    public Base_Item.Affiliation Affiliation => _data.affiliation;
    public int CurrentHP { get; private set; }
    public bool IsDead => CurrentHP <= 0;
    public int MatchCapacity => _data.matchCapacity;
    public float MoveSpeed => _data.moveSpeed;

    private readonly List<Match> _matches = new();
    public int CurrentMatchCount => _matches.Count;

    private Combatant _provisionalTarget;
    private readonly List<Combatant> _waitingOnMe = new();

    private float _attackTimer;

    private TextMesh _statusText;

    public void Initialize(Base_Item data, MatchManager matchManager)
    {
        _data = data;
        _matchManager = matchManager;
        CurrentHP = _data.maxHP;

        CreateeStatusDisplay();
    }

    private void OnEnable() => _all.Add(this);
    private void OnDisable() => _all.Remove(this);

    public void Tick(float fixedDt)
    {
        if (_data == null || IsDead) return;

        TickAttack(fixedDt);

        if(CurrentMatchCount < MatchCapacity)
        {
            TryRequestMatch();
        }

        UpdateStatusDisplay();
    }

    private void CreateeStatusDisplay()
    {
        var displayObj = new GameObject("StatusDisplay");
        displayObj.transform.SetParent(transform);
        displayObj.transform.localPosition = new Vector3(0f, 1f, 0f);

        _statusText = displayObj.AddComponent<TextMesh>();
        _statusText.characterSize = 0.1f;
        _statusText.fontSize = 48;
        _statusText.anchor = TextAnchor.LowerCenter;
        _statusText.alignment = TextAlignment.Center;
    }

    private void UpdateStatusDisplay()
    {
        if (_statusText == null) return;

        string strategyLabel = _data.targetingStrategy switch
        {
            Base_Item.TargetingStrategy.Nearest => "N",
            Base_Item.TargetingStrategy.Farthest => "F",
            Base_Item.TargetingStrategy.LowestHp => "L",
            _ => "?"
        };

        string stateLabel;
        Color color;
        if(CurrentMatchCount > 0)
        {
            stateLabel = "Match";
            color = Color.red;
        }
        else if(_provisionalTarget != null)
        {
            stateLabel = "Stand-by";
            color = Color.yellow;
        }
        else
        {
            stateLabel = "Free";
            color = Color.yellow;
        }

        _statusText.text = $"{MoveSpeed:F1}[{strategyLabel}]\n{stateLabel}";
        _statusText.color = color;
    }

    private void TryRequestMatch()
    {
        if(_provisionalTarget != null)
        {
            if(_provisionalTarget.IsDead)
            {
                ClearProvisionalTarget();
            }
            else
            {
                _matchManager?.RequestMatch(this, _provisionalTarget);
                return;
            }
        }

        var target = SelectFromCandidates(_all.Where(c => c != this && !c.IsDead && c._data != null && c.Affiliation != Affiliation));
        if (target == null) return;

        SetProvisionalTarget(target);
        _matchManager?.RequestMatch(this, target);
    }

    private Combatant SelectFromCandidates(IEnumerable<Combatant> candidates)
    {
        switch(_data.targetingStrategy)
        {
            case Base_Item.TargetingStrategy.Farthest:
                return candidates.OrderByDescending(c => Vector3.Distance(transform.position, c.transform.position)).FirstOrDefault();
            case Base_Item.TargetingStrategy.LowestHp:
                return candidates.OrderBy(c => c.CurrentHP).FirstOrDefault();
            case Base_Item.TargetingStrategy.Nearest:
            default:
                return candidates.OrderBy(c => Vector3.Distance(transform.position, c.transform.position)).FirstOrDefault();
        }
    }

    private void SetProvisionalTarget(Combatant target)
    {
        _provisionalTarget = target;
        target._waitingOnMe.Add(this);
    }

    private void ClearProvisionalTarget()
    {
        if (_provisionalTarget == null) return;
        _provisionalTarget._waitingOnMe.Remove(this);
        _provisionalTarget = null;
    }

    private void TickAttack(float deltaTime)
    {
        if (_matches.Count == 0) return;

        _attackTimer += deltaTime;
        if (_attackTimer < _data.attackInterval) return;
        _attackTimer -= _data.attackInterval;

        foreach(var match in _matches.ToList())
        {
            var opponent = match.GetOpponent(this);
            if (opponent == null) continue;
            match.ApplyDamage(this, opponent, _data.attackPower);
            Debug.Log("[Combatant] {name}attacked,opponent HP remaining check needed via opponent side");
        }
    }

    public void ApplyDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }

    public void NotifyMatchStarted(Match match)
    {
        _matches.Add(match);

        var opponent = match.GetOpponent(this);
        if(ReferenceEquals(opponent,_provisionalTarget))
        {
            ClearProvisionalTarget();
        }
    }

    public void NotifyMatchEnded(Match match)
    {
        _matches.Remove(match);
        TryPullWaiter();
    }

    private void TryPullWaiter()
    {
        if (CurrentMatchCount >= MatchCapacity) return;

        _waitingOnMe.RemoveAll(w => w.IsDead);
        if (_waitingOnMe.Count == 0) return;

        var chosen = SelectFromCandidates(_waitingOnMe);
        if (chosen == null) return;

        _waitingOnMe.Remove(chosen);
        _matchManager?.RequestMatchPriority(chosen, this);
    }

    public static void DestroyDead()
    {
        foreach(var combatant in _all.Where(c => c.IsDead).ToList())
        {
            Destroy(combatant.gameObject);
        }
    }
    
}
