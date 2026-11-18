using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Match;

namespace UnityGameJam.Test
{
    public class MatchSimulationTest
    {
        private const string Folder = "Assets/@UnityGameJam/3_Data/ScriptableObject/";
        private static readonly EZoneId[] _zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
        private static readonly Vector3 _mopRack = new Vector3(50f, 0f, 0f);

        [Test]
        public void 작업_완료와_마감이_같은_틱이면_완료를_먼저_반영한다()
        {
            GameRuleSO rules = Load<GameRuleSO>("GameRule");
            ContaminationTypeSO types = Load<ContaminationTypeSO>("ContaminationType");
            var body = new WorkerBody { Position = _mopRack };
            MatchSimulation match = CreateMatch(body);
            var events = new List<EMatchState>();
            match.MatchStateChanged += events.Add;
            match.StartMatch();
            match.StartMatch();
            match.Tick(rules.MatchSeconds - types.PlayerMopSeconds);

            Assert.That(match.TryBorrowMop(EFloor.Floor1), Is.True);
            body.Position = Vector3.zero;
            Assert.That(match.TryStartMop(1), Is.True);
            match.Tick(types.PlayerMopSeconds + 1f);
            match.Tick(1f);

            Assert.That(match.Items.Single(item => item.ItemId == 1).State, Is.EqualTo(EItemState.Resolved));
            Assert.That(match.Result.ZoneCleanliness[EZoneId.Floor1Lobby], Is.EqualTo(100f));
            Assert.That(match.Result.IsSuccess, Is.False);
            Assert.That(events, Is.EqualTo(new[] { EMatchState.Running, EMatchState.DemonKingArrived, EMatchState.Resolved }));
        }

        [Test]
        public void 마감_뒤에_완료될_작업은_남은_시간만_진행한다()
        {
            GameRuleSO rules = Load<GameRuleSO>("GameRule");
            ContaminationTypeSO types = Load<ContaminationTypeSO>("ContaminationType");
            var body = new WorkerBody { Position = _mopRack };
            MatchSimulation match = CreateMatch(body);
            match.StartMatch();
            match.Tick(rules.MatchSeconds - types.PlayerMopSeconds / 2f);

            Assert.That(match.TryBorrowMop(EFloor.Floor1), Is.True);
            body.Position = Vector3.zero;
            Assert.That(match.TryStartMop(1), Is.True);
            match.Tick(types.PlayerMopSeconds * 2f);

            Assert.That(match.RemainingTime, Is.Zero);
            Assert.That(match.Items.Single(item => item.ItemId == 1).State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(match.IsPlayerWorking, Is.False);
            Assert.That(match.TryStartMop(1), Is.False);
        }

        [Test]
        public void 운반물을_든_채로_마감하면_현재_층_로비에_다시_집계한다()
        {
            GameRuleSO rules = Load<GameRuleSO>("GameRule");
            var body = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.HeavyDebris, 4, EZoneId.Floor1Lobby, Vector3.zero);
            MatchSimulation match = CreateMatch(body, waste);
            match.StartMatch();

            Assert.That(match.TryPickUp(100), Is.True);
            Assert.That(match.FacilityCleanliness, Is.GreaterThan(0f));
            Assert.That(match.PlayerMoveSpeed, Is.EqualTo(Load<WorkerStatsSO>("WorkerStats_Player").MoveSpeed * 0.5f));
            body.Floor = EFloor.Floor2;
            body.Position = Vector3.right * 1000f;
            match.Tick(rules.MatchSeconds);

            ItemSnapshot item = match.Items.Single(snapshot => snapshot.ItemId == 100);
            Assert.That(item.State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(item.Position, Is.EqualTo(body.Position));
            Assert.That(match.PlayerCarriedItemId, Is.Null);
            Assert.That(match.Result.FacilityCleanliness, Is.Zero);
        }

        [Test]
        public void 관찰한_쓰레기를_슬라임이_먹으면_계약_스냅샷에_소화_중으로_표시한다()
        {
            var player = new WorkerBody();
            var slime = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, Vector3.zero);
            MatchSimulation match = CreateMatch(player, waste, slime);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Slime, EZoneId.Floor1Lobby), Is.True);

            match.Tick(0.1f);
            match.Tick(0.1f);
            ItemSnapshot item = match.Items.Single(snapshot => snapshot.ItemId == 100);
            Assert.That(item.State, Is.EqualTo(EItemState.Digesting));
            Assert.That(item.Carrier, Is.EqualTo(EWorkerType.Slime));
            Assert.That(slime.LastDestinationFloor, Is.EqualTo(EFloor.Floor1));
        }

        [Test]
        public void 배정_대화_중에도_제안은_만료되고_마감_이벤트_전에_사라진다()
        {
            GameRuleSO rules = Load<GameRuleSO>("GameRule");
            var player = new WorkerBody();
            var slime = new WorkerBody();
            MatchSimulation match = CreateMatch(player, slime: slime);
            match.StartMatch();
            match.Tick(rules.MatchSeconds - rules.ProposalSeconds - 2f);
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Slime, EZoneId.Floor1Lobby), Is.True);
            match.Tick(1f);
            match.Tick(1f);
            Assert.That(match.ActiveMessages.Count, Is.EqualTo(1));
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            Assert.That(match.ApproveOldestProposal(), Is.False);
            Assert.That(match.HoldOldestProposal(), Is.False);

            bool expiredBeforeArrival = false;
            match.MatchStateChanged += state =>
            {
                if (state == EMatchState.DemonKingArrived) { expiredBeforeArrival = match.ActiveMessages.Count == 0; }
            };
            match.Tick(rules.ProposalSeconds);

            Assert.That(expiredBeforeArrival, Is.True);
            Assert.That(match.MatchState, Is.EqualTo(EMatchState.Resolved));
        }

        [Test]
        public void 졸개가_다른_층으로_배정되면_목표_층을_몸에_전달한다()
        {
            var player = new WorkerBody();
            var slime = new WorkerBody();
            MatchSimulation match = CreateMatch(player, slime: slime);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Slime, EZoneId.Workshop), Is.True);

            Assert.That(slime.LastDestinationFloor, Is.EqualTo(EFloor.Floor2));
            Assert.That(match.Minions.Single().AssignedZone, Is.EqualTo(EZoneId.Workshop));
        }

        [Test]
        public void 대화_취소로_정지했던_졸개는_기존_목적지로_이동을_재개한다()
        {
            var player = new WorkerBody();
            var slime = new WorkerBody();
            MatchSimulation match = CreateMatch(player, slime: slime);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Slime, EZoneId.Workshop), Is.True);

            player.Floor = slime.Floor;
            player.Position = slime.Position;
            int moves = slime.MoveCount;
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            match.Tick(0.1f);
            Assert.That(slime.MoveCount, Is.EqualTo(moves));
            match.CancelTalk(EWorkerType.Slime);
            Assert.That(slime.MoveCount, Is.EqualTo(moves + 1));
            Assert.That(slime.LastDestinationFloor, Is.EqualTo(EFloor.Floor2));
        }

        [Test]
        public void 폐기물_운반_중_재배정되면_현재_위치에_내려놓고_새_방으로_이동한다()
        {
            var player = new WorkerBody();
            var orc = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.HeavyDebris, 4, EZoneId.Floor1Lobby, Vector3.zero);
            MatchSimulation match = CreateMatch(player, waste, orc, EWorkerType.Orc);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Orc), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Orc, EZoneId.Floor1Lobby), Is.True);
            match.Tick(0.1f);
            match.Tick(0.1f);
            Assert.That(match.Minions.Single().CarriedItemId, Is.EqualTo(100));

            player.Position = orc.Position;
            Vector3 dropPosition = orc.Position;
            Assert.That(match.BeginTalk(EWorkerType.Orc), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Orc, EZoneId.Workshop), Is.True);

            ItemSnapshot item = match.Items.Single(snapshot => snapshot.ItemId == 100);
            Assert.That(item.State, Is.EqualTo(EItemState.InDischargeArea));
            Assert.That(item.Position, Is.EqualTo(dropPosition));
            Assert.That(orc.LastDestinationFloor, Is.EqualTo(EFloor.Floor2));
        }

        [Test]
        public void 작은_물건의_제자리_슬롯이_초기_목록과_운반_이벤트에_유지된다()
        {
            var clutter = new ContaminationItem(100, EContaminationType.ClutterItem, 2, EZoneId.Floor1Lobby, Vector3.zero, 17);
            MatchSimulation match = CreateMatch(new WorkerBody(), clutter);
            match.StartMatch();
            Assert.That(match.Items.Single(item => item.ItemId == clutter.Id).HomeSlotId, Is.EqualTo(17));
            var changes = new List<ItemSnapshot>();
            match.ItemChanged += changes.Add;

            Assert.That(match.TryPickUp(clutter.Id), Is.True);
            ItemSnapshot carried = changes.Single(item => item.ItemId == clutter.Id && item.State == EItemState.Carried);
            Assert.That(carried.HomeSlotId, Is.EqualTo(17));
            Assert.That(match.Items.Single(item => item.ItemId == clutter.Id).HomeSlotId, Is.EqualTo(17));
        }

        private static MatchSimulation CreateMatch(WorkerBody player, ContaminationItem extra = null, WorkerBody slime = null, EWorkerType minionType = EWorkerType.Slime)
        {
            var layout = new FacilityLayout { DischargeArea = new Bounds(Vector3.right * 80f, Vector3.one) };
            foreach (EFloor floor in Enum.GetValues(typeof(EFloor)))
            {
                layout.Toilets.Add(new ToiletStation { Floor = floor, MopPickupPosition = _mopRack });
            }
            foreach (EZoneId zone in _zones)
            {
                var route = new PatrolRoute { Zone = zone };
                route.Points.Add(Vector3.right * (20f * (int)zone));
                layout.PatrolRoutes.Add(route);
                var area = new ZoneArea { Zone = zone, Floor = ZoneDisplayName.GetFloor(zone) };
                area.Areas.Add(new Bounds(Vector3.right * (20f * (int)zone), Vector3.one * 4f));
                layout.Zones.Add(area);
            }
            layout.StartPositions[minionType] = Vector3.zero;
            var setup = new MatchSetup { Layout = layout, PlayerBody = player };
            if (slime != null) { setup.MinionBodies[minionType] = slime; }
            var items = _zones.Select((zone, index) =>
                new ContaminationItem(index + 1, EContaminationType.FloorStain, 1, zone,
                    Vector3.right * (20f * index))).ToList();
            if (extra != null) { items.Add(extra); }
            WorkerStatsSO[] stats = Enum.GetValues(typeof(EWorkerType)).Cast<EWorkerType>()
                .Select(worker => Load<WorkerStatsSO>("WorkerStats_" + worker)).ToArray();
            return new MatchSimulation(setup, Load<GameRuleSO>("GameRule"), Load<ContaminationTypeSO>("ContaminationType"),
                stats, items);
        }

        private static T Load<T>(string name) where T : ScriptableObject
        {
            return AssetDatabase.LoadAssetAtPath<T>(Folder + name + ".asset");
        }

        private sealed class WorkerBody : IMinionBody
        {
            public Vector3 Position { get; set; }
            public EFloor Floor { get; set; }
            public EFloor LastDestinationFloor { get; private set; }
            public int MoveCount { get; private set; }
            public bool HasArrived => true;

            public void MoveTo(EFloor floor, Vector3 destination)
            {
                MoveCount++;
                LastDestinationFloor = floor;
                Floor = floor;
                Position = destination;
            }

            public void Stop() { }
            public void SetMoveSpeed(float speed) { }
        }
    }
}
