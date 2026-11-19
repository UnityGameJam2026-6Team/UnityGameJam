using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGameJam.Cleaning;
using UnityGameJam.Contract;
using UnityGameJam.Data;
using UnityGameJam.Facility;
using UnityGameJam.Match;
using UnityGameJam.Minion;

namespace UnityGameJam.Test
{
    public class MatchSimulationTest
    {
        private const string Folder = "Assets/@UnityGameJam/3_Data/ScriptableObject/";
        private static readonly EZoneId[] _zones = (EZoneId[])Enum.GetValues(typeof(EZoneId));
        private static readonly Vector3 _mopRack = new Vector3(50f, 0f, 0f);

        [Test]
        public void 공유된_방_정보는_새로운_관찰을_반영하고_같은_내용을_다시_알리지_않는다()
        {
            const EZoneId zone = EZoneId.Storage;
            var layout = new FacilityLayout();
            layout.SpawnPoints.Add(new SpawnPoint { Zone = zone, Position = Vector3.right * 8f });
            var map = new ZoneMap(layout);
            var item = new ContaminationItem(100, EContaminationType.HeavyDebris, 4, zone, Vector3.right * 8f);
            var dirty = new ContaminationRegistry(map, new[] { item });
            var clean = new ContaminationRegistry(map, Array.Empty<ContaminationItem>());
            var reporter = new MinionMemory();
            var listener = new MinionMemory();

            listener.Observe(zone, item.Position, 2f, dirty, layout, 1);
            reporter.Observe(zone, Vector3.zero, 2f, clean, layout, 2);
            reporter.FinishPatrol(zone, layout, 2);
            Assert.That(reporter.ShareWith(listener), Is.False);

            reporter.Observe(zone, item.Position, 2f, clean, layout, 3);
            reporter.FinishPatrol(zone, layout, 3);
            Assert.That(reporter.ShareWith(listener), Is.True);
            Assert.That(listener.IsKnownClean(zone), Is.True);
            reporter.Observe(zone, item.Position, 2f, clean, layout, 4);
            Assert.That(reporter.ShareWith(listener), Is.False);

            listener.Observe(zone, item.Position, 2f, dirty, layout, 5);
            Assert.That(listener.ShareWith(reporter), Is.True);
            Assert.That(reporter.IsKnownClean(zone), Is.False);
        }

        [Test]
        public void 로비와_먼_같은_방에서는_기억과_통보가_없고_가까워져야_정보를_교환한다()
        {
            var sender = new WorkerBody();
            var receiver = new WorkerBody();
            using var match = CreateMatch(new WorkerBody(), slime: sender, minionType: EWorkerType.Orc, goblin: receiver);
            var brains = (Dictionary<EWorkerType, MinionBrain>)typeof(MatchSimulation)
                .GetField("_minions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            var senderMemory = brains[EWorkerType.Orc].Memory;
            var receiverMemory = brains[EWorkerType.Goblin].Memory;
            const EZoneId observedZone = EZoneId.Storage;
            var observedLayout = new FacilityLayout();
            var clean = new ContaminationRegistry(new ZoneMap(observedLayout), Array.Empty<ContaminationItem>());
            senderMemory.Observe(observedZone, Vector3.zero, 1f, clean, observedLayout, 1);
            senderMemory.FinishPatrol(observedZone, observedLayout, 1);
            Assert.That(senderMemory.IsKnownClean(observedZone), Is.True);

            match.StartMatch();
            foreach (EZoneId lobby in _zones.Where(ZoneDisplayName.IsLobby))
            {
                sender.Floor = receiver.Floor = ZoneDisplayName.GetFloor(lobby);
                sender.Position = receiver.Position = Vector3.right * (20f * (int)lobby);
                match.Tick(.25f);
                Assert.That(receiverMemory.IsKnownClean(observedZone), Is.False);
                Assert.That(match.ActiveMessages, Is.Empty);
            }

            Vector3 roomCenter = Vector3.right * (20f * (int)EZoneId.Lounge);
            sender.Floor = receiver.Floor = EFloor.Floor1;
            sender.Position = roomCenter - Vector3.forward;
            receiver.Position = roomCenter + Vector3.forward;
            match.Tick(.25f);
            Assert.That(receiverMemory.IsKnownClean(observedZone), Is.False);
            Assert.That(match.ActiveMessages, Is.Empty);

            receiver.Position = sender.Position + Vector3.forward * Load<GameRuleSO>("GameRule").InteractionDistance;
            match.Tick(.25f);
            Assert.That(receiverMemory.IsKnownClean(observedZone), Is.True);
            MessageEntry report = match.ActiveMessages.Single();
            Assert.That(report.Kind, Is.EqualTo(EMessageKind.Notice));
            Assert.That(report.Minion, Is.EqualTo(EWorkerType.Orc));
        }

        [Test]
        public void 남은_작업과_동료가_있으면_다른_층이_같은_층_미확인_방보다_앞선다()
        {
            int unexplored = ProposalCandidateFinder.Rank(EFloor.Floor1, EFloor.Floor1, false, false);
            int support = ProposalCandidateFinder.Rank(EFloor.Floor1, EFloor.Floor3, true, true);
            Assert.That(support, Is.LessThan(unexplored));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 내려놓거나_선점을_풀면_방을_다시_탐색할_수_있다(bool releaseClaim)
        {
            const EZoneId target = EZoneId.Storage;
            Vector3 room = RoomCenter(target);
            var player = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.Trash, 1,
                releaseClaim ? target : EZoneId.Floor1Lobby, releaseClaim ? room : Vector3.zero);
            using var match = CreateMatch(player, waste, goblin: new WorkerBody());
            match.StartMatch();
            Assert.That(Assign(match, EWorkerType.Goblin, EZoneId.MultipurposeRoom), Is.True);
            ContaminationRegistry registry = ReadMatchField<ContaminationRegistry>(match, "_registry");
            FacilityLayout layout = ReadMatchField<MatchSetup>(match, "_setup").Layout;
            int stainId = (int)target + 1;
            Assert.That(registry.TryClaim(stainId, EWorkerType.Player), Is.True);
            Assert.That(registry.TryCompleteMop(stainId, EWorkerType.Player), Is.True);
            if (releaseClaim) { Assert.That(registry.TryClaim(waste.Id, EWorkerType.Player), Is.True); }

            var brains = ReadMatchField<Dictionary<EWorkerType, MinionBrain>>(match, "_minions");
            MinionBrain brain = brains[EWorkerType.Goblin];
            brain.Memory.Observe(target, room, 5f, registry, layout, 1);
            Assert.That(brain.Memory.HasCompletedSurvey(target), Is.True);
            Assert.That(brain.Memory.KnownTaskCount(target, EContaminationType.Trash), Is.Zero);

            if (releaseClaim) { Assert.That(registry.TryRelease(waste.Id, EWorkerType.Player), Is.True); }
            else
            {
                Assert.That(match.TryPickUp(waste.Id), Is.True);
                player.Position = room;
                Assert.That(match.TryDrop(), Is.True);
            }

            var capability = ReadMatchField<WorkerCapability>(match, "_capability");
            var candidates = ProposalCandidateFinder.Find(brain, layout, capability, brains.Values, true);
            Assert.That(candidates.Zones, Does.Contain(target));
            Assert.That(brain.Memory.KnownTaskCount(target, EContaminationType.Trash), Is.Zero);
        }

        [Test]
        public void 같은_미확인_방을_제안하면_첫_승인_뒤_다른_졸개는_후보를_다시_고른다()
        {
            using var match = CreateMatch(new WorkerBody(), slime: new WorkerBody(),
                minionType: EWorkerType.Orc, goblin: new WorkerBody());
            match.StartMatch();
            ContaminationRegistry registry = ReadMatchField<ContaminationRegistry>(match, "_registry");
            Assert.That(registry.TryClaim(1, EWorkerType.Player), Is.True);
            Assert.That(registry.TryCompleteMop(1, EWorkerType.Player), Is.True);
            Assert.That(Assign(match, EWorkerType.Goblin, EZoneId.Floor1Lobby), Is.True);
            Assert.That(Assign(match, EWorkerType.Orc, EZoneId.Floor1Lobby), Is.True);
            match.Tick(.1f);
            var proposed = match.ActiveMessages.Where(message => message.Kind == EMessageKind.Proposal).ToArray();
            Assert.That(proposed.Length, Is.EqualTo(2));
            Assert.That(proposed[0].ProposedZone, Is.EqualTo(proposed[1].ProposedZone));
            Assert.That(match.ApproveOldestProposal(), Is.True);
            Assert.That(match.ApproveOldestProposal(), Is.False);
            match.Tick(.1f);
            Assert.That(match.ActiveMessages.Single(message => message.Kind == EMessageKind.Proposal).ProposedZone,
                Is.Not.EqualTo(proposed[0].ProposedZone));
        }

        [TestCase(1f, EWorkerType.Skeleton)]
        [TestCase(50f, EWorkerType.Goblin)]
        public void 같은_얼룩을_고르면_걸레까지_왕복하고_더_빨리_끝낼_졸개가_맡는다(float rackDistance, EWorkerType expected)
        {
            using var match = CreateMatch(new WorkerBody(), slime: new WorkerBody(),
                minionType: EWorkerType.Skeleton, goblin: new WorkerBody());
            ReadMatchField<MatchSetup>(match, "_setup").Layout.Toilets[0].MopPickupPosition = Vector3.right * rackDistance;
            match.StartMatch();
            Assert.That(Assign(match, EWorkerType.Goblin, EZoneId.Floor1Lobby), Is.True);
            Assert.That(Assign(match, EWorkerType.Skeleton, EZoneId.Floor1Lobby), Is.True);
            match.Tick(.1f);
            var registry = ReadMatchField<ContaminationRegistry>(match, "_registry");
            Assert.That(registry.FindItem(1).ClaimedBy, Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 쓰레기_도움은_걸레를_들면_제안하거나_승인하지_않는다(bool borrowAfterProposal)
        {
            const EZoneId zone = EZoneId.Lounge;
            var player = new WorkerBody { Position = _mopRack };
            var skeleton = new WorkerBody { Position = _mopRack };
            var bin = new WasteBin { Floor = EFloor.Floor1, Position = RoomCenter(zone) };
            var waste = new ContaminationItem(100, EContaminationType.Trash, 1, zone, bin.Position);
            using var match = CreateMatch(player, waste, skeleton, EWorkerType.Skeleton, wasteBin: bin);
            match.StartMatch();
            // 테스트 화장실은 라운지에 닿는 위치에 둔다.
            ReadMatchField<MatchSetup>(match, "_setup").Layout.Toilets[0].MopPickupPosition = bin.Position;
            player.Position = skeleton.Position = bin.Position;
            if (!borrowAfterProposal) { Assert.That(match.TryBorrowMop(EFloor.Floor1), Is.True); }
            Assert.That(Assign(match, EWorkerType.Skeleton, zone), Is.True);
            match.Tick(.1f);
            bool hasHelp = match.ActiveMessages.Any(message => message.ProposalKind == EProposalKind.PlayerAssistance);
            Assert.That(hasHelp, Is.EqualTo(borrowAfterProposal));
            if (borrowAfterProposal)
            {
                Assert.That(match.TryBorrowMop(EFloor.Floor1), Is.True);
                Assert.That(match.ApproveOldestProposal(), Is.False);
                Assert.That(match.Items.Single(item => item.ItemId == waste.Id).State, Is.EqualTo(EItemState.OnFloor));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 분업은_가까운_쓰레기통_처리를_우선하고_남은_시간이_줄면_다시_판단한다(bool approveNearDeadline)
        {
            const EZoneId zone = EZoneId.MultipurposeRoom;
            Vector3 room = RoomCenter(zone);
            var player = new WorkerBody { Position = room };
            var orc = new WorkerBody { Position = room };
            var goblin = new WorkerBody { Position = room };
            var heavy = new ContaminationItem(100, EContaminationType.HeavyDebris, 4, zone, room);
            var trash = new ContaminationItem(101, EContaminationType.Trash, 1, zone, room);
            var bin = new WasteBin { Floor = EFloor.Floor1, Position = room + Vector3.right };
            using var match = CreateMatch(player, heavy, orc, EWorkerType.Orc, goblin: goblin,
                wasteBin: bin, hound: new WorkerBody(), additionalItem: trash);
            match.StartMatch();
            if (approveNearDeadline) { match.Tick(Load<GameRuleSO>("GameRule").MatchSeconds - 18f); }
            Assert.That(Assign(match, EWorkerType.Orc, zone), Is.True);
            player.Position = Vector3.zero;
            match.Tick(.1f);
            Assert.That(match.ActiveMessages.Any(message => message.ProposalKind == EProposalKind.MinionCollaboration), Is.True);
            if (approveNearDeadline)
            {
                match.Tick(3f);
                Assert.That(match.ApproveOldestProposal(), Is.False);
                return;
            }
            Assert.That(match.ApproveOldestProposal(), Is.True);
            var registry = ReadMatchField<ContaminationRegistry>(match, "_registry");
            Assert.That(registry.FindItem(trash.Id).ClaimedBy, Is.EqualTo(EWorkerType.Orc));
            Assert.That(registry.FindItem(heavy.Id).ClaimedBy, Is.Null);
        }

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

        [TestCase(false)]
        [TestCase(true)]
        public void 슬라임의_소화_스냅샷과_증진에_따른_소화_시간을_반영한다(bool boost)
        {
            var player = new WorkerBody();
            var slime = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.Trash, 3, EZoneId.Floor1Lobby, Vector3.zero);
            MatchSimulation match = CreateMatch(player, waste, slime);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Slime), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Slime, EZoneId.Floor1Lobby), Is.True);
            player.Position = Vector3.left * 10f; // 혼자 소화하는 경로에서는 플레이어 도움 제안을 만들지 않는다.

            match.Tick(0.1f);
            match.Tick(0.1f);
            ItemSnapshot item = match.Items.Single(snapshot => snapshot.ItemId == 100);
            Assert.That(item.State, Is.EqualTo(EItemState.Digesting));
            Assert.That(item.Carrier, Is.EqualTo(EWorkerType.Slime));
            Assert.That(slime.LastDestinationFloor, Is.EqualTo(EFloor.Floor1));

            player.Position = Vector3.zero;
            if (boost) { Assert.That(match.TryCastSkill(ECommandSkill.Surge), Is.True); }
            float seconds = Load<WorkerStatsSO>("WorkerStats_Slime").DigestSeconds;
            match.Tick(seconds * .75f);
            EItemState expected = boost ? EItemState.Resolved : EItemState.Digesting;
            Assert.That(match.Items.Single(snapshot => snapshot.ItemId == 100).State, Is.EqualTo(expected));
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

        [TestCase(false)]
        [TestCase(true)]
        public void 같은_방에서_한_명만_마지막_얼룩을_맡고_다른_졸개는_가능하면_잔해를_처리한다(bool hasDebris)
        {
            const EZoneId zone = EZoneId.ExecutiveRoom;
            const EFloor floor = EFloor.Floor3;
            const int stainId = (int)zone + 1;
            Vector3 room = Vector3.right * (20f * (int)zone);
            var player = new WorkerBody();
            var goblin = new WorkerBody();
            var orc = new WorkerBody();
            var debris = hasDebris
                ? new ContaminationItem(100, EContaminationType.HeavyDebris, 4, zone, room + Vector3.forward)
                : null;
            using var match = CreateMatch(player, debris, orc, EWorkerType.Orc, goblin: goblin);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Goblin), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Goblin, zone), Is.True);
            Assert.That(match.BeginTalk(EWorkerType.Orc), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Orc, zone), Is.True);
            player.Floor = floor;
            player.Position = room;

            match.Tick(.1f);
            Assert.That(goblin.Position, Is.EqualTo(_mopRack));
            Assert.That(orc.Position, Is.Not.EqualTo(_mopRack));

            match.Tick(.1f);
            Assert.That(match.Minions.Single(minion => minion.Minion == EWorkerType.Goblin).IsHoldingMop, Is.True);
            MinionStatus orcStatus = match.Minions.Single(minion => minion.Minion == EWorkerType.Orc);
            Assert.That(orcStatus.IsHoldingMop, Is.False);
            if (debris != null) { Assert.That(orcStatus.CarriedItemId, Is.EqualTo(debris.Id)); }
            match.Tick(.1f);
            match.Tick(Load<WorkerStatsSO>("WorkerStats_Goblin").MopSeconds);
            Assert.That(match.Items.Single(item => item.ItemId == stainId).State, Is.EqualTo(EItemState.Resolved));
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

        [Test]
        public void 반납_경로가_막히면_한번_다시_시도하고_걸레를_놓은_뒤_새_방으로_이동한다()
        {
            var player = new WorkerBody();
            var goblin = new WorkerBody();
            using MatchSimulation match = CreateMatch(player, goblin: goblin);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Goblin), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Goblin, EZoneId.Floor1Lobby), Is.True);
            match.Tick(.1f);
            match.Tick(.1f);
            match.Tick(.1f);
            Assert.That(match.Minions.Single().IsHoldingMop, Is.True);

            goblin.BlockedDestination = _mopRack;
            Assert.That(match.BeginTalk(EWorkerType.Goblin), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Goblin, EZoneId.Workshop), Is.True);
            int initialMoves = goblin.MoveCount;
            match.Tick(.1f);
            Assert.That(goblin.MoveCount, Is.EqualTo(initialMoves + 1));
            Assert.That(match.Minions.Single().IsHoldingMop, Is.True);

            match.Tick(.1f);
            Assert.That(match.Minions.Single().IsHoldingMop, Is.False);
            Assert.That(goblin.LastDestinationFloor, Is.EqualTo(EFloor.Floor2));
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.MovingToRoom));
        }

        [Test]
        public void 헬하운드는_배출품을_출발할_때_제거하고_왕복_후_대기한다()
        {
            Vector3 exit = Vector3.right * 2f;
            var bin = new Bounds(exit + Vector3.up, new Vector3(1f, 2f, 1f));
            var hound = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.Trash, 1, EZoneId.Floor1Lobby, exit);
            MatchSimulation match = CreateMatch(new WorkerBody(), waste, hound, EWorkerType.Hellhound, bin);
            match.StartMatch();

            Assert.That(match.BeginTalk(EWorkerType.Hellhound), Is.True);
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.Talking));
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.InDischargeArea));
            Assert.That(match.AssignRoom(EWorkerType.Hellhound, EZoneId.Floor1Lobby), Is.False);
            match.CancelTalk(EWorkerType.Hellhound);
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.LobbyIdle));
            Assert.That(match.BeginTalk(EWorkerType.Hellhound), Is.True);
            Assert.That(match.DispatchHellhound(), Is.True);
            Assert.That(hound.Position, Is.EqualTo(exit));
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.Dispatched));
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.Dispatching));
            float halfTrip = Load<WorkerStatsSO>("WorkerStats_Hellhound").DispatchSeconds * 0.5f;
            match.Tick(halfTrip);
            Assert.That(hound.Position, Is.EqualTo(Vector3.zero));
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.ReturningToLobby));
            match.Tick(halfTrip);
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.LobbyIdle));
        }

        [Test]
        public void 플레이어는_같은_층_쓰레기통에서만_봉투를_즉시_처리한다()
        {
            Vector3 position = Vector3.right * (20f * (int)EZoneId.Workshop);
            var player = new WorkerBody { Floor = EFloor.Floor2, Position = position };
            var trash = new ContaminationItem(100, EContaminationType.Trash, 1, EZoneId.Workshop, position);
            var bin = new WasteBin { Floor = EFloor.Floor2, Position = position + Vector3.right * 5f };
            MatchSimulation match = CreateMatch(player, trash, wasteBin: bin);
            match.StartMatch();

            Assert.That(match.TryPickUp(100), Is.True);
            Assert.That(match.TryDrop(), Is.True);
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(match.TryPickUp(100), Is.True);
            player.Position = bin.Position;
            bin.Floor = EFloor.Floor3;
            Assert.That(match.TryDrop(), Is.True);
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.OnFloor));
            Assert.That(match.TryPickUp(100), Is.True);
            bin.Floor = EFloor.Floor2;
            Assert.That(match.TryDrop(), Is.True);
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.Resolved));
            Assert.That(match.PlayerCarriedItemId, Is.Null);
        }

        [Test]
        public void 고블린은_일반_쓰레기를_같은_층_쓰레기통으로_운반한다()
        {
            Vector3 position = Vector3.right * (20f * (int)EZoneId.Workshop);
            var player = new WorkerBody();
            var goblin = new WorkerBody();
            var trash = new ContaminationItem(100, EContaminationType.Trash, 1, EZoneId.Workshop, position);
            var bin = new WasteBin { Floor = EFloor.Floor2, Position = position + Vector3.right * 5f };
            MatchSimulation match = CreateMatch(player, trash, goblin, EWorkerType.Goblin, wasteBin: bin);
            match.StartMatch();

            Assert.That(match.BeginTalk(EWorkerType.Goblin), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Goblin, EZoneId.Workshop), Is.True);
            for (int i = 0; i < 10 && match.Items.Single(item => item.ItemId == 100).State != EItemState.Carried; i++)
            {
                match.Tick(.1f);
            }
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.Carried));
            Assert.That(goblin.Floor, Is.EqualTo(EFloor.Floor2));
            Assert.That(goblin.Position, Is.EqualTo(bin.Position));
            match.Tick(.1f);
            Assert.That(match.Items.Single(item => item.ItemId == 100).State, Is.EqualTo(EItemState.Resolved));
        }

        [Test]
        public void 호통에_모인_졸개는_새_지시까지_대기하고_헬하운드는_출발하지_않는다()
        {
            Vector3 meeting = Vector3.right * (20f * (int)EZoneId.Workshop);
            var player = new WorkerBody { Floor = EFloor.Floor2, Position = meeting };
            var goblin = new WorkerBody();
            var orc = new WorkerBody { Floor = EFloor.Floor2, Position = meeting };
            var hound = new WorkerBody();
            using MatchSimulation match = CreateMatch(player, slime: orc, minionType: EWorkerType.Orc, goblin: goblin, hound: hound);
            match.StartMatch();
            Assert.That(match.BeginTalk(EWorkerType.Orc), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Orc, EZoneId.Workshop), Is.True);
            Assert.That(match.TryCastSkill(ECommandSkill.Shout), Is.True);
            Assert.That(match.SkillCooldownRemaining(ECommandSkill.Shout), Is.EqualTo(60f));
            Assert.That(match.TryCastSkill(ECommandSkill.Shout), Is.False);
            Assert.That(match.Minions.Single(x => x.Minion == EWorkerType.Orc).Activity, Is.EqualTo(EMinionActivity.Recalling));
            Assert.That(match.ActiveMessages.Count(x => x.Kind == EMessageKind.Notice), Is.EqualTo(2));
            Assert.That(match.ActiveMessages.Any(x => x.Minion == EWorkerType.Hellhound), Is.False);
            Assert.That(hound.MoveCount, Is.Zero);

            Vector3 destination = orc.Position;
            player.Position += Vector3.forward * 5f;
            match.Tick(20f);
            Assert.That(orc.Position, Is.EqualTo(destination));
            Assert.That(goblin.Floor, Is.EqualTo(EFloor.Floor2));
            Assert.That(match.Minions.Where(x => x.Minion != EWorkerType.Hellhound)
                .All(x => x.Activity == EMinionActivity.LobbyIdle && !x.AssignedZone.HasValue), Is.True);
            player.Position = destination;
            Assert.That(match.BeginTalk(EWorkerType.Orc), Is.True);
            Assert.That(match.AssignRoom(EWorkerType.Orc, EZoneId.Workshop), Is.True);
        }

        [Test]
        public void 증진은_같은_방의_대상만_선정하고_방을_나가도_십오_초를_유지한다()
        {
            Vector3 meeting = Vector3.right * (20f * (int)EZoneId.Workshop);
            var player = new WorkerBody { Floor = EFloor.Floor2, Position = meeting };
            var orc = new WorkerBody { Floor = EFloor.Floor2, Position = meeting + Vector3.left };
            var goblin = new WorkerBody { Floor = EFloor.Floor2, Position = meeting + Vector3.right * 9f };
            using MatchSimulation match = CreateMatch(player, slime: orc, minionType: EWorkerType.Orc, goblin: goblin);
            match.StartMatch();
            Assert.That(match.TryCastSkill(ECommandSkill.Surge), Is.True);
            Assert.That(match.SkillCooldownRemaining(ECommandSkill.Surge), Is.EqualTo(90f));
            Assert.That(match.SkillCooldownRemaining(ECommandSkill.Shout), Is.Zero);
            Assert.That(match.BoostRemaining(EWorkerType.Orc), Is.EqualTo(15f));
            Assert.That(orc.ActionRate, Is.EqualTo(1.5f));
            Assert.That(goblin.ActionRate, Is.EqualTo(1f));
            Assert.That(match.ActiveMessages.Single().Minion, Is.EqualTo(EWorkerType.Orc));

            orc.Position += Vector3.forward * 9f;
            goblin.Position = meeting;
            match.Tick(14f);
            Assert.That(orc.ActionRate, Is.EqualTo(1.5f));
            Assert.That(goblin.ActionRate, Is.EqualTo(1f));
            match.Tick(1f);
            Assert.That(orc.ActionRate, Is.EqualTo(1f));
            Assert.That(match.SkillCooldownRemaining(ECommandSkill.Surge), Is.EqualTo(75f));
        }

        [Test]
        public void 같은_로비의_헬하운드는_배출_지점에서_대기_중에도_증진된다()
        {
            Vector3 exit = Vector3.right;
            var hound = new WorkerBody();
            var waste = new ContaminationItem(100, EContaminationType.Trash, 1, EZoneId.Floor1Lobby, exit);
            using MatchSimulation match = CreateMatch(new WorkerBody(), waste, hound,
                EWorkerType.Hellhound, new Bounds(exit + Vector3.up, new Vector3(1f, 2f, 1f)));
            match.StartMatch();
            Assert.That(match.DispatchHellhound(), Is.True);
            Assert.That(hound.HasArrived, Is.True);
            Assert.That(match.Minions.Single().Activity, Is.EqualTo(EMinionActivity.Dispatching));

            Assert.That(match.TryCastSkill(ECommandSkill.Surge), Is.True);
            Assert.That(match.BoostRemaining(EWorkerType.Hellhound), Is.EqualTo(match.BoostDuration));
        }

        [Test]
        public void 증진_대상이_없으면_쿨다운과_알림을_소비하지_않는다()
        {
            Vector3 meeting = Vector3.right * (20f * (int)EZoneId.Workshop);
            var player = new WorkerBody { Floor = EFloor.Floor2, Position = meeting };
            var goblin = new WorkerBody { Floor = EFloor.Floor1, Position = meeting };
            using MatchSimulation match = CreateMatch(player, goblin: goblin);
            match.StartMatch();
            Assert.That(match.TryCastSkill(ECommandSkill.Surge), Is.False);
            Assert.That(match.SkillCooldownRemaining(ECommandSkill.Surge), Is.Zero);
            Assert.That(match.ActiveMessages, Is.Empty);
        }

        private static Vector3 RoomCenter(EZoneId zone) => Vector3.right * (20f * (int)zone);

        private static bool Assign(MatchSimulation match, EWorkerType worker, EZoneId zone) =>
            match.BeginTalk(worker) && match.AssignRoom(worker, zone);

        private static T ReadMatchField<T>(MatchSimulation match, string name) =>
            (T)typeof(MatchSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);

        private static MatchSimulation CreateMatch(WorkerBody player, ContaminationItem extra = null, WorkerBody slime = null,
            EWorkerType minionType = EWorkerType.Slime, Bounds? dischargeArea = null, WorkerBody goblin = null,
            WasteBin wasteBin = null, WorkerBody hound = null, ContaminationItem additionalItem = null)
        {
            var layout = new FacilityLayout { DischargeArea = dischargeArea ?? new Bounds(Vector3.right * 80f, Vector3.one) };
            if (wasteBin != null) { layout.WasteBins.Add(wasteBin); }
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
            if (goblin != null) { setup.MinionBodies[EWorkerType.Goblin] = goblin; }
            if (hound != null) { setup.MinionBodies[EWorkerType.Hellhound] = hound; }
            var items = _zones.Select((zone, index) =>
                new ContaminationItem(index + 1, EContaminationType.FloorStain, 1, zone,
                    Vector3.right * (20f * index))).ToList();
            if (extra != null) { items.Add(extra); }
            if (additionalItem != null) { items.Add(additionalItem); }
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
            public float ActionRate { get; private set; } = 1f;
            public bool HasArrived => !HasMoveFailed;
            public bool HasMoveFailed { get; private set; }
            public Vector3? BlockedDestination { get; set; }

            public void MoveTo(EFloor floor, Vector3 destination)
            {
                MoveCount++;
                LastDestinationFloor = floor;
                HasMoveFailed = destination == BlockedDestination;
                if (HasMoveFailed) { return; }
                Floor = floor;
                Position = destination;
            }

            public void Stop() { }
            public void SetMoveSpeed(float speed) { }
            public void SetActionRate(float rate) => ActionRate = rate;
        }
    }
}
