using System.Collections.Generic;

namespace UnityGameJam.Contract
{
    /// <summary>Bootstrap(B 레인)이 들고 매 프레임 Tick을 부르는 판 하나.</summary>
    public interface IMatchSession : IMatchCommands, IMatchReadModel
    {
        void StartMatch();
        void Tick(float deltaTime);
    }

    public enum ECommandSkill { Shout, Surge }

    public readonly struct CommandSkillCast
    {
        public ECommandSkill Skill { get; }
        public UnityEngine.Vector3 Position { get; }
        public EFloor Floor { get; }
        public int Sequence { get; }

        public CommandSkillCast(ECommandSkill skill, UnityEngine.Vector3 position, EFloor floor, int sequence)
        {
            Skill = skill;
            Position = position;
            Floor = floor;
            Sequence = sequence;
        }
    }

    public interface ICommandSkillSession
    {
        CommandSkillCast LastSkillCast { get; }
        bool TryCastSkill(ECommandSkill skill);
        float SkillCooldown(ECommandSkill skill);
        float SkillCooldownRemaining(ECommandSkill skill);
        float BoostDuration { get; }
        float BoostRemaining(EWorkerType worker);
    }

    /// <summary>판을 만들 때 B 레인이 넘기는 씬 쪽 정보. 수치 데이터는 팩토리(A 레인 SO)가 들고 있다.</summary>
    public sealed class MatchSetup
    {
        public FacilityLayout Layout { get; set; }
        public int Seed { get; set; }
        public IWorkerBody PlayerBody { get; set; }
        internal IReadOnlyDictionary<(EFloor From, EFloor To), UnityEngine.Vector3> StairEntries { get; set; }
        internal float StairTransitSeconds { get; set; }
        public Dictionary<EWorkerType, IMinionBody> MinionBodies { get; } = new Dictionary<EWorkerType, IMinionBody>();
    }
}
