using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 複数のMatchインスタンスで保持し、生成・破棄を管理する。
/// マッチ申請はPush型で溜め込み、FixedStepのタイミングでまとめて承認する
/// 仮マッチ状態の分岐は持たない
/// </summary>
public class MatchManager 
{
    private struct MatchRequest
    {
        public ICombatant Requester;
        public ICombatant Target;
    }

    private readonly List<Match> _activeMatches = new();
    public IReadOnlyList<Match> ActiveMatches => _activeMatches;

    private readonly List<MatchRequest> _requests = new();
    private readonly List<MatchRequest> _priorityRequests = new();

    ///<summary>
    ///エンティティ側から能動的に呼ばれる申請の受け口
    ///空いて選定はエンティティ側の責務で済ませる
    /// </summary>
    public void RequestMatch(ICombatant requester,ICombatant target)
    {
        if (requester == null || target == null) return;
        _requests.Add(new MatchRequest { Requester = requester, Target = target });
    }

    public void RequestMatchPriority(ICombatant requester,ICombatant target)
    {
        if (requester == null || target == null) return;
        _priorityRequests.Add(new MatchRequest { Requester = requester, Target = target });
    }
    

    ///<summary>
    ///GameLoopManagerのFixedStepから呼ばれる想定
    ///request.Count &gt;0の時だけMoveSpeed降順でソートしてから承認処理し、最後にClear()する
    /// </summary>
    public void ProcessRequests()
    {
        if(_priorityRequests.Count > 0)
        {
            var priority = _priorityRequests.OrderByDescending(r => r.Requester.MoveSpeed).ToList();
            _priorityRequests.Clear();
            foreach (var request in priority) TryApprove(request);
        }

        if (_requests.Count == 0) return;

        var sorted = _requests.OrderByDescending(r => r.Requester.MoveSpeed).ToList();
        _requests.Clear();

        foreach(var request in sorted)
        {
            TryApprove(request);
        }
    }
    
    private void TryApprove(MatchRequest request)
    {
        var requester = request.Requester;
        var target = request.Target;

        if (requester.IsDead || target.IsDead) return;
        if (!HasCapacity(requester) || !HasCapacity(target)) return;

        var match = new Match(requester, target);
        match.OnFinished += HandleMatchFinished;
        _activeMatches.Add(match);

        requester.NotifyMatchStarted(match);
        target.NotifyMatchStarted(match);
    }

    private bool HasCapacity(ICombatant combatant)
    {
        return combatant.CurrentMatchCount < combatant.MatchCapacity;
    }

    private void HandleMatchFinished(Match match)
    {
        _activeMatches.Remove(match);
        match.OnFinished -= HandleMatchFinished;

        match.Left.NotifyMatchEnded(match);
        match.Right.NotifyMatchEnded(match);
    }
}
