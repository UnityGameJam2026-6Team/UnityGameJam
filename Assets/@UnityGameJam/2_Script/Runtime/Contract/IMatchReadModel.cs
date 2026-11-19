using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGameJam.Contract
{
    /// <summary>
    /// UI와 연출(B 레인)이 읽는 판 상태. 규칙(A 레인)이 구현한다.
    /// 원격 정보 제한(GDD §2, §9)은 UI가 지킨다. 이 모델은 모든 값을 제공하지만,
    /// HUD는 플레이어가 있는 구역의 청결도만 보여 준다.
    /// </summary>
    public interface IMatchReadModel
    {
        EMatchState MatchState { get; }
        float RemainingTime { get; }

        float ZoneThreshold { get; }     // 구역 기준 (%)
        float FacilityThreshold { get; } // 시설 기준 (%)
        float FacilityCleanliness { get; }
        float GetZoneCleanliness(EZoneId zone);

        /// <summary>판정 구역 밖(화장실·계단)이면 null.</summary>
        EZoneId? PlayerZone { get; }
        int? PlayerCarriedItemId { get; }
        bool IsPlayerHoldingMop { get; }

        /// <summary>
        /// 플레이어 몸이 매 프레임 적용할 이동 속도(m/s). 규칙이 계산한다.
        /// 무거운 잔해를 들면 기본 속도의 절반이다(U20, 배율은 WorkerStatsSO).
        /// </summary>
        float PlayerMoveSpeed { get; }

        /// <summary>플레이어가 시간이 걸리는 작업(물걸레질 등)을 하는 중인지.</summary>
        bool IsPlayerWorking { get; }

        /// <summary>플레이어 작업 진행도 0~100(%). 작업 중이 아니면 0(U21).</summary>
        float PlayerWorkProgress { get; }

        IReadOnlyList<MinionStatus> Minions { get; }
        IReadOnlyList<MessageEntry> ActiveMessages { get; }
        IReadOnlyList<ItemSnapshot> Items { get; }

        /// <summary>MatchState가 Resolved가 되기 전에는 null.</summary>
        MatchResult Result { get; }

        event Action<ItemSnapshot> ItemChanged;
        event Action MessagesChanged;
        event Action<EMatchState> MatchStateChanged;
    }

    public readonly struct MinionStatus
    {
        public EWorkerType Minion { get; }
        public EMinionActivity Activity { get; }
        public EZoneId? AssignedZone { get; }
        public int? CarriedItemId { get; }
        public bool IsHoldingMop { get; }

        public MinionStatus(EWorkerType minion, EMinionActivity activity, EZoneId? assignedZone, int? carriedItemId, bool isHoldingMop)
        {
            Minion = minion;
            Activity = activity;
            AssignedZone = assignedZone;
            CarriedItemId = carriedItemId;
            IsHoldingMop = isHoldingMop;
        }
    }

    public enum EProposalKind { Move, MinionCollaboration, PlayerAssistance }

    public readonly struct MessageEntry
    {
        public EProposalKind ProposalKind { get; }
        public int MessageId { get; }
        public EMessageKind Kind { get; }
        public EWorkerType Minion { get; }
        public string Text { get; }
        public EZoneId? ProposedZone { get; }
        public float ShownAt { get; }       // 표시 시점 (판 경과 시간)
        public float RemainingTime { get; } // 응답·표시 남은 시간
        public bool IsInputTarget { get; }  // 지금 승인·보류 입력이 적용될 제안인지

        public MessageEntry(int messageId, EMessageKind kind, EWorkerType minion, string text, EZoneId? proposedZone,
            float shownAt, float remainingTime, bool isInputTarget, EProposalKind proposalKind = EProposalKind.Move)
        {
            ProposalKind = proposalKind;
            MessageId = messageId;
            Kind = kind;
            Minion = minion;
            Text = text;
            ProposedZone = proposedZone;
            ShownAt = shownAt;
            RemainingTime = remainingTime;
            IsInputTarget = isInputTarget;
        }
    }

    public readonly struct ItemSnapshot
    {
        public int ItemId { get; }
        public EContaminationType Type { get; }
        public EItemState State { get; }
        public Vector3 Position { get; }     // 운반 중이면 마지막으로 놓였던 위치. 화면에서는 운반자를 따라간다.
        public EWorkerType? Carrier { get; } // 운반 중이거나 소화 중일 때의 작업자
        public int? HomeSlotId { get; }      // 어질러진 작은 물건의 제자리 슬롯(U3). 화면에서 물건 종류(Book 등)를 고르는 데 쓴다. 다른 유형은 null

        public ItemSnapshot(int itemId, EContaminationType type, EItemState state, Vector3 position, EWorkerType? carrier, int? homeSlotId = null)
        {
            ItemId = itemId;
            Type = type;
            State = state;
            Position = position;
            Carrier = carrier;
            HomeSlotId = homeSlotId;
        }
    }

    public sealed class MatchResult
    {
        public bool IsSuccess { get; }
        public float FacilityCleanliness { get; }
        public IReadOnlyDictionary<EZoneId, float> ZoneCleanliness { get; }

        public MatchResult(bool isSuccess, float facilityCleanliness, IReadOnlyDictionary<EZoneId, float> zoneCleanliness)
        {
            IsSuccess = isSuccess;
            FacilityCleanliness = facilityCleanliness;
            ZoneCleanliness = zoneCleanliness;
        }
    }
}
