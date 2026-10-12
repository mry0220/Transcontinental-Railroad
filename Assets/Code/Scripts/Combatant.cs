using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Combatant : MonoBehaviour,ICombatant
{
    #region StaticRegistry
    private static readonly List<Combatant> _all = new();
    public static IReadOnlyList<Combatant> All => _all;

    public static BattleVisualSettings VisualSettings { get; set; } = new();

    public static event System.Action<Combatant, int> OnAnyDamaged;

    #endregion

    #region Types
    private enum MovementState
    {
        Idle,
        Moving,
        InRange,
    }

    private struct TimedBuff
    {
        public float attackMultiplier;
        public float speedMultiplier;
        public float remaining;
    }

    public readonly struct TargetView
    {
        public readonly Combatant Target;
        public readonly bool IsProvisional;
        public TargetView(Combatant target,bool isProvisional)
        {
            Target = target;
            IsProvisional = isProvisional;
        }
    }

    #endregion

    #region InspectorConfig
    [SerializeField] private float _matchCooldownDuration = 0.5f;

    #endregion

    #region References
    private Base_Item _data;
    private MatchManager _matchManager;
    private CombatantHealthBar _hpBar;

    #endregion

    #region RuntimeState
    private readonly List<Match> _matches = new();
    private readonly List<Combatant> _provisionalTargets = new();
    private readonly List<Combatant> _waitingOnMe = new();
    private float _attackTimer;
    private float _idleTimer;
    private bool _isActive;
    private MovementState _movementState = MovementState.Idle;
    private TextMesh _statusText;
    private LineRenderer _rangeIndicator;
    private readonly List<Combatant> _slotOrder = new();
    private readonly List<TimedBuff> _buffs = new();

    #endregion

    #region Properties
    public Base_Item.Affiliation Affiliation => _data.affiliation;
    public int CurrentHP { get; private set; }
    public bool IsDead => CurrentHP <= 0;
    public int MatchCapacity => _data.matchCapacity;
    public float MoveSpeed => _data.moveSpeed * BuffSpeedMultiplier;
    public int AttackPower => Mathf.RoundToInt(_data.attackPower * _statMultiplier * BuffAttackMultiplier);
    public int CurrentMatchCount => _matches.Count;
    public int MaxHp => Mathf.RoundToInt(_data.maxHP * _statMultiplier);
    public bool IsActive => _isActive && _data != null;
    public bool IsTrain => _data is Item_Train;
    public Sprite Icon => _data switch
    {
        Item_Unit u => u.icon,
        Item_Enemy e => e.icon,
        Item_Train t => t.icon,
        _ => null
    };

    private float _statMultiplier = 1f;

    private float BuffAttackMultiplier
    {
        get
        {
            float m = 1f;
            foreach (var b in _buffs) m *= b.attackMultiplier;
            return m;
        }
    }

    private float BuffSpeedMultiplier
    {
        get
        {
            float m = 1f;
            foreach (var b in _buffs) m *= b.speedMultiplier;
            return m;
        }
    }

    #endregion

    #region Events
    public event System.Action<int> OnDamageTaken;
    public event System.Action<int> OnDealtDamage;
    public event System.Action<int> OnActualDamageTaken;
    public event System.Action<Combatant> OnDied;
    #endregion

    #region UnityLifecycle
    private void OnEnable() => _all.Add(this);
    private void OnDisable()
    {
        _all.Remove(this);

        foreach (var w in _waitingOnMe)
            if (w != null) w._provisionalTargets.Remove(this);
        foreach (var t in _provisionalTargets)
            if (t != null) t._waitingOnMe.Remove(this);

        _waitingOnMe.Clear();
        _provisionalTargets.Clear();
        _slotOrder.Clear();
    }
    

    #endregion

    #region Initialization
    public void Initialize(Base_Item data, MatchManager matchManager)
    {
        _data = data;
        _matchManager = matchManager;
        CurrentHP = _data.maxHP;
        _isActive = true;
        AttachHud();
        
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
        AttachHud();
    }

    private void AttachHud()
    {
        if (_hpBar != null || _data == null) return;
        if (_data is Item_Train) return;
        if (!VisualSettings.showHpBars) return;

        _hpBar = CombatantHealthBar.Create(
            transform, Affiliation == Base_Item.Affiliation.Ally, VisualSettings);
        _hpBar.Refrash(CurrentHP, MaxHp);
    }

    public void SetStatMultiplier(float multiplier)
    {
        _statMultiplier = Mathf.Max(0.01f, multiplier);
        CurrentHP = MaxHp;
        if (_hpBar != null) _hpBar.Refrash(CurrentHP, MaxHp);
    }

    #endregion

    #region Tick
    public void Tick(float fixedDt)
    {
        if (_data == null) return;

        if(IsDead)
            return;

        if (!_isActive || _data == null || IsDead) return;

        TickBuffs(fixedDt);

        SyncSlots();

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

    private bool IsInRange(Combatant target)
    {
        return target != null && !target.IsDead
            && Vector3.Distance(transform.position, target.transform.position) <= _data.attackRange;
    }
    /// <summary>今この瞬間に攻撃できる相手がいるか。Matchの相手は相手ごとに射程を見る。待機はMainの時だけ</summary>
    private bool HasAttackableTarget()
    {
        foreach(var match in _matches)
        {
            if (match.IsFinished) continue;
            if (IsInRange(match.GetOpponent(this) as Combatant)) return true;
        }

        var main = GetPrimaryTarget();
        return main != null && _provisionalTargets.Contains(main) && IsInRange(main);
    }

    private void TickAttack(float deltaTime)
    {
        if (_movementState != MovementState.InRange) return;

        _attackTimer += deltaTime;
        if (_attackTimer < _data.attackInterval) return;
        _attackTimer -= _data.attackInterval;

        foreach(var match in _matches.ToList())
        {
            var opponent = match.GetOpponent(this) as Combatant;
            if (!IsInRange(opponent)) continue;

            int dealt = Mathf.Min(AttackPower, opponent.CurrentHP);
            match.ApplyDamage(this, opponent, AttackPower);
            ReportDealtDamage(dealt);
        }

        var main = GetPrimaryTarget();

        if(main != null && _provisionalTargets.Contains(main) && IsInRange(main))
        {
            int dealt = Mathf.Min(AttackPower, main.CurrentHP);
            main.ApplyDamage(AttackPower);
            ReportDealtDamage(dealt);
        }
    }

    

    #endregion

    #region TargetingAndMatching

    public void CollectTargets(List<TargetView> buffer)
    {
        buffer.Clear();

        foreach(var match in _matches)
        {
            if (match.IsFinished) continue;
            if (match.GetOpponent(this) is Combatant opponent && !opponent.IsDead)
                buffer.Add(new TargetView(opponent, false));
        }
        foreach(var target in _provisionalTargets)
        {
            if (!target.IsDead) buffer.Add(new TargetView(target, true));
        }
    }

    private Combatant GetPrimaryTarget()
    {
        return _slotOrder.Count > 0 ? _slotOrder[0] : null;
    }

    private void SyncSlots()
    {
        foreach(var match in _matches.ToList())
        {
            if (match.IsFinished) continue;

            var opponent = match.GetOpponent(this) as Combatant;
            if (opponent == null || opponent.IsDead) match.Withdraw();
        }

        _slotOrder.RemoveAll(o => o == null || o.IsDead || !IsEngagedWith(o));

        foreach(var match in _matches)
        {
            var opponent = match.GetOpponent(this) as Combatant;
            if (opponent != null && !opponent.IsDead && !_slotOrder.Contains(opponent))
                _slotOrder.Add(opponent);
        }
        foreach(var target in _provisionalTargets)
        {
            if (target != null && !target.IsDead && !_slotOrder.Contains(target))
                _slotOrder.Add(target);
        }
    }

    private bool IsEngagedWith(Combatant opponent)
    {
        if (_provisionalTargets.Contains(opponent)) return true;

        foreach(var match in _matches)
        {
            if (!match.IsFinished && ReferenceEquals(match.GetOpponent(this), opponent)) return true;
        }
        return false;
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
        if (!_slotOrder.Contains(target)) _slotOrder.Add(target);
    }
    private void ClearProvisionalTarget(Combatant target)
    {
        if (!_provisionalTargets.Remove(target)) return;
        target._waitingOnMe.Remove(this);
    }

    private void TrimProvisionalOverCapacity()
    {
        while (_matches.Count + _provisionalTargets.Count > MatchCapacity && _provisionalTargets.Count > 0)
        {
            var drop = _provisionalTargets.FindLast(t => t.IsTrain)
                       ?? _provisionalTargets[_provisionalTargets.Count - 1];
            ClearProvisionalTarget(drop);
            _slotOrder.Remove(drop);
        }
    }

    public void NotifyMatchStarted(Match match)
    {
        _matches.Add(match);

        var opponent = match.GetOpponent(this);
        var opponentCombatant = opponent as Combatant;
        if (opponentCombatant != null && !_slotOrder.Contains(opponentCombatant))
            _slotOrder.Add(opponentCombatant);
        var provisional = _provisionalTargets.FirstOrDefault(t => ReferenceEquals(t, opponent));
        if(provisional != null)
        {
            ClearProvisionalTarget(provisional);
        }
        TrimProvisionalOverCapacity();
    }
    public void NotifyMatchEnded(Match match)
    {
        _matches.Remove(match);

        var opponent = match.GetOpponent(this) as Combatant;
        if (opponent != null && !_provisionalTargets.Contains(opponent))
            _slotOrder.Remove(opponent);

        TryPullWaiter();
    }
    private void TryPullWaiter()
    {
        if (CurrentMatchCount >= MatchCapacity) return;

        _waitingOnMe.RemoveAll(w => w== null || w.IsDead);
        if (_waitingOnMe.Count == 0) return;

        var chosen = SelectFromCandidates(_waitingOnMe);
        if (chosen == null) return;

        _waitingOnMe.Remove(chosen);
        _matchManager?.RequestMatchPriority(chosen, this);
    }

    #endregion

    #region Damage
    public void ApplyDamage(int amount)
    {
        int before = CurrentHP;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        int actual = before - CurrentHP;

        if (_hpBar != null) _hpBar.Refrash(CurrentHP, MaxHp);

        if(actual > 0 && amount != int.MaxValue && VisualSettings.showDamagePopups)
        {
            OnAnyDamaged?.Invoke(this, actual);
        }
        if(actual > 0 && amount != int.MaxValue)
        {
            OnActualDamageTaken?.Invoke(actual);
        }
        OnDamageTaken?.Invoke(amount);
        if (before > 0 && CurrentHP <= 0) OnDied?.Invoke(this);
    }

    private void ReportDealtDamage(int amount)
    {
        if (amount > 0) OnDealtDamage?.Invoke(amount);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHP = Mathf.Min(MaxHp, CurrentHP + amount);
        if (_hpBar != null) _hpBar.Refrash(CurrentHP, MaxHp);
    }
    /// <summary>Skillによるダメージ。</summary>
    public void DealSkillDamage(Combatant target, int amount)
    {
        if (target == null || target.IsDead || amount <= 0) return;

        int dealt = Mathf.Min(amount, target.CurrentHP);
        target.ApplyDamage(amount);
        ReportDealtDamage(dealt);
    }
    /// <summary>
    /// Skillの攻撃対象
    /// </summary>
    public Combatant GetSkillTarget()
    {
        var primary = GetPrimaryTarget();
        if (primary != null && !primary.IsDead && primary.IsActive) return primary;

        var candidates = _all.Where(c => c != this & c.IsActive && !c.IsDead && c.Affiliation != Affiliation);
        return SelectFromCandidates(candidates);
    }

    public void AddTimedBuff(float attakMultiplier,float speedMultiplier,float duration)
    {
        if (duration <= 0f) return;
        _buffs.Add(new TimedBuff
        {
            attackMultiplier = attakMultiplier,
            speedMultiplier = speedMultiplier,
            remaining = duration,
        });
    }

    private void TickBuffs(float dt)
    {
        for(int i = _buffs.Count -1;i >= 0;i--)
        {
            var b = _buffs[i];
            b.remaining -= dt;
            if (b.remaining <= 0f) _buffs.RemoveAt(i);
            else _buffs[i] = b;
        }
    }

    #endregion

    #region DebugDisplay
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

    #endregion

    #region StaticHelpers
    public static void DestroyDead()
    {
        foreach(var combatant in _all.Where(c => c.IsDead).ToList())
        {
            Destroy(combatant.gameObject);
        }
    }
    public static void DestroyDead(Base_Item.Affiliation affiliation)
    {
        foreach (var combatant in _all.Where(c => c.IsDead && c.Affiliation == affiliation).ToList())
        {
            Destroy(combatant.gameObject);
        }
    }
    public static void DestroyAll()
    {
        foreach(var combatant in _all.ToList())
        {
            Destroy(combatant.gameObject);
        }
    }

    #endregion
}
