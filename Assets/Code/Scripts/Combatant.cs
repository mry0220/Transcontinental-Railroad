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

    public event System.Action<int> OnDamageTaken;

    //private Combatant _provisionalTarget;
    private readonly List<Combatant> _provisionalTargets = new();
    private readonly List<Combatant> _waitingOnMe = new();

    private float _attackTimer;

    private TextMesh _statusText;
    private LineRenderer _rangeIndicator;

    private bool _isActive;

    private enum MovementState
    {
        Idle,
        Moving,
        InRange,
    }

    private MovementState _movementState = MovementState.Idle;

    private float _idleTimer;
    [SerializeField] private float _matchCooldownDuration = 0.5f;
   

    public void Initialize(Base_Item data, MatchManager matchManager)
    {
        _data = data;
        _matchManager = matchManager;
        CurrentHP = _data.maxHP;
        _isActive = true;
        
    }

    public void InitializeAsPreview(Base_Item data)
    {
        _data = data;
        CurrentHP = data.maxHP;
        _isActive = false;

        CreateStatusDisplay();
        CreateRangeIndicator();
        UpdateStatusDisplay();
    }

    public void Activate(MatchManager matchManager)
    {
        _matchManager = matchManager;
        _isActive = true;

        DestroyDeploymentVisuals();
    }

    private void OnEnable() => _all.Add(this);
    private void OnDisable() => _all.Remove(this);

    public void Tick(float fixedDt)
    {
        if (!_isActive || _data == null || IsDead) return;

        bool hasAnyTarget = CurrentMatchCount > 0 || _provisionalTargets.Count > 0;
        _idleTimer = hasAnyTarget ? 0f : _idleTimer + fixedDt;


        UpdateMovement(fixedDt);
        TickAttack(fixedDt);

        if(CurrentMatchCount < MatchCapacity && (hasAnyTarget || _idleTimer >= _matchCooldownDuration))
        {
            TryRequestMatch();
        }

        UpdateStatusDisplay();
    }

    public void SetPreviewVisualsViisivle(bool visible)
    {
        if(visible)
        {
            if(_statusText == null ||  _rangeIndicator == null)
            {
                CreateStatusDisplay();
                CreateRangeIndicator();
            }
        }
        else
        {
            DestroyDeploymentVisuals();
        }
    }

    private void DestroyDeploymentVisuals()
    {
        if(_statusText != null)
        {
            Destroy(_statusText.gameObject);
            _statusText = null;
        }
        if(_rangeIndicator != null)
        {
            Destroy(_rangeIndicator.gameObject);
            _rangeIndicator = null;
        }
    }

    private void CreateStatusDisplay()
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

    

    private void CreateRangeIndicator()
    {
        var rangeObj = new GameObject("RangeIndicator");
        rangeObj.transform.SetParent(transform);
        rangeObj.transform.localPosition = Vector3.zero;

        _rangeIndicator = rangeObj.AddComponent<LineRenderer>();
        _rangeIndicator.useWorldSpace = false;
        _rangeIndicator.loop = true;
        _rangeIndicator.widthMultiplier = 0.03f;
        _rangeIndicator.positionCount = 32;
        _rangeIndicator.material = new Material(Shader.Find("Sprites/Default"));
        _rangeIndicator.startColor = _rangeIndicator.endColor = new Color(1f, 1f, 1f, 0.3f);

        float radius = _data.attackRange;
        for(int i = 0;i < 32;i++)
        {
            float angle = i / 32f * Mathf.PI * 2f;
            _rangeIndicator.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private void UpdateMovement(float fixedDt)
    {
        var primary = GetPrimaryTarget();
        if(primary == null)
        {
            _movementState = MovementState.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, primary.transform.position);
        if(distance <= _data.attackRange)
        {
            _movementState = MovementState.InRange;
            return;
        }

        _movementState = MovementState.Moving;
        Vector3 dir = (primary.transform.position - transform.position).normalized;
        transform.position += dir * MoveSpeed * fixedDt;
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
        else if(_provisionalTargets.Count > 0)
        {
            stateLabel = "Stand-by";
            color = Color.yellow;
        }
        else
        {
            stateLabel = "Free";
            color = Color.white;
        }

        _statusText.text = $"{MoveSpeed:F1}[{strategyLabel}]\n{stateLabel}";
        _statusText.color = color;
    }

    private Combatant GetPrimaryTarget()
    {
        foreach ( var match in _matches)
        {
            if (match.GetOpponent(this) is Combatant opponent) return opponent;
        }

        return _provisionalTargets.Count > 0 ? _provisionalTargets[0] : null;
    }

    private void TryRequestMatch()
    {
       for(int i = _provisionalTargets.Count -1;i >=0;i--)
        {
            if (_provisionalTargets[i].IsDead)
            {
                ClearProvisionalTarget(_provisionalTargets[i]);
            }
        }

       foreach(var t in _provisionalTargets.ToList())
        {
            _matchManager?.RequestMatch(this, t);
        }

        int openSlots = MatchCapacity - CurrentMatchCount - _provisionalTargets.Count;
        if (openSlots <= 0) return;

        var exculuded = new HashSet<Combatant>(_provisionalTargets) { this };


        for(int i =0;i < openSlots;i++)
        {
            var candidates = _all.Where(c => !exculuded.Contains(c) && !c.IsDead && c._isActive && c._data != null && c.Affiliation != Affiliation);
            var target = SelectFromCandidates(candidates);
            if (target == null) break;

            SetProvisionalTarget(target);
            exculuded.Add(target);
            _matchManager?.RequestMatch(this, target);
        }

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
        _provisionalTargets.Add(target);
        target._waitingOnMe.Add(this);
    }

    private void ClearProvisionalTarget(Combatant target)
    {
        if (!_provisionalTargets.Remove(target)) return;
        target._waitingOnMe.Remove(this);
    }

    private void TickAttack(float deltaTime)
    {
        bool hasTarget = _matches.Count > 0 || _provisionalTargets.Count > 0;
        if (!hasTarget || _movementState != MovementState.InRange) return;

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

        foreach(var target in _provisionalTargets.ToList())
        {
            if (target.IsDead) continue;
            target.ApplyDamage(_data.attackPower);
        }
    }

    public void ApplyDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnDamageTaken?.Invoke(amount);
    }

    public void NotifyMatchStarted(Match match)
    {
        _matches.Add(match);

        var opponent = match.GetOpponent(this);
        var provisional = _provisionalTargets.FirstOrDefault(t => ReferenceEquals(t, opponent));
        if(provisional != null)
        {
            ClearProvisionalTarget(provisional);
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
