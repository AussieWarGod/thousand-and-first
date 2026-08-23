#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSuccessionRulesTests
	{
		[Test]
		public void BodyFlipThenPreEventThrowRestoresFounderWithoutPublishingAccession()
		{
			object founder = new object();
			object heir = new object();
			object current = founder;
			List<FakePlayerSystem> systems = NewPlayerSystems(3, founder);
			bool accessionPublished = false;

			KingdomPlayerBodyTransfer forward = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				founder, heir,
				body =>
				{
					current = body;
					// One system saw the body-change event before a later handler threw.
					systems[0].Bodies.Remove(founder);
					systems[0].Bodies.Add(heir);
					throw new InvalidOperationException("injected before body-change event");
				},
				() => current,
				systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) => system.Bodies.Add(body));
			if (forward.MayPublishAccession)
			{
				accessionPublished = true;
			}

			KingdomPlayerBodyTransfer rollback = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				heir, founder, body => current = body, () => current, systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) => system.Bodies.Add(body));

			Assert.IsFalse(accessionPublished);
			Assert.IsFalse(forward.SetBodyReturnedClean);
			Assert.IsTrue(forward.TargetControls);
			Assert.IsInstanceOf<InvalidOperationException>(forward.Failure);
			Assert.IsTrue(rollback.TargetControls);
			Assert.IsTrue(rollback.RegistrationsExact);
			Assert.AreSame(founder, current);
			AssertEverySystemHasOnly(systems, founder);
		}

		[Test]
		public void ShortCircuitedBodyChangeDispatchCannotLeaveLaterPlayerSystemsOnFounder()
		{
			object founder = new object();
			object heir = new object();
			object current = founder;
			List<FakePlayerSystem> systems = NewPlayerSystems(4, founder);

			KingdomPlayerBodyTransfer forward = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				founder, heir,
				body =>
				{
					current = body;
					// Dispatch reached one IPlayerSystem, whose false result stopped later handlers.
					systems[0].Bodies.Remove(founder);
					systems[0].Bodies.Add(heir);
				},
				() => current,
				systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) => system.Bodies.Add(body));

			Assert.IsTrue(forward.SetBodyReturnedClean);
			Assert.IsTrue(forward.TargetControls);
			Assert.IsTrue(forward.RegistrationsExact);
			Assert.IsTrue(forward.MayPublishAccession);
			AssertEverySystemHasOnly(systems, heir);
		}

		[TestCase(true, true, true)]
		[TestCase(false, true, false)]
		[TestCase(true, false, false)]
		[TestCase(false, false, false)]
		public void RepairTokenRequiresExactHeirAndProvedGlobalRegistrations(
			bool HeirControls, bool RegistrationsExact, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomSuccessionRules.MayQueueAccessionRepair(
				HeirControls, RegistrationsExact));
		}

		[Test]
		public void ThirdControllerNeverQualifiesForChosenHeirRepair()
		{
			object founder = new object();
			object heir = new object();
			object third = new object();
			object current = founder;
			List<FakePlayerSystem> systems = NewPlayerSystems(3, founder);

			KingdomPlayerBodyTransfer transfer = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				founder, heir, body => current = third, () => current, systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) => system.Bodies.Add(body));

			Assert.IsFalse(transfer.OriginalControls);
			Assert.IsFalse(transfer.TargetControls);
			Assert.IsFalse(transfer.MayPublishAccession);
			Assert.IsFalse(KingdomSuccessionRules.MayQueueAccessionRepair(
				ReferenceEquals(current, heir), transfer.RegistrationsExact));
			AssertEverySystemHasOnly(systems, third);
		}

		[Test]
		public void RegistrationFailurePreventsBothAccessionAndRepairQualification()
		{
			object founder = new object();
			object heir = new object();
			object current = founder;
			List<FakePlayerSystem> systems = NewPlayerSystems(3, founder);

			KingdomPlayerBodyTransfer transfer = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				founder, heir, body => current = body, () => current, systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) =>
				{
					if (ReferenceEquals(system, systems[1]))
					{
						throw new InvalidOperationException("injected registration refusal");
					}
					system.Bodies.Add(body);
				});

			Assert.IsTrue(transfer.TargetControls);
			Assert.IsFalse(transfer.RegistrationsExact);
			Assert.Greater(transfer.RegistrationFailures, 0);
			Assert.IsFalse(transfer.MayPublishAccession);
			Assert.IsFalse(KingdomSuccessionRules.MayQueueAccessionRepair(true,
				transfer.RegistrationsExact));
		}

		[Test]
		public void RefusedCleanRollbackToThirdCannotQueueChosenHeirRepair()
		{
			object founder = new object();
			object heir = new object();
			object third = new object();
			object current = heir;
			List<FakePlayerSystem> systems = NewPlayerSystems(3, heir);

			KingdomPlayerBodyTransfer rollback = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				heir, founder, body => current = third, () => current, systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) => system.Bodies.Add(body));

			Assert.IsFalse(rollback.TargetControls);
			Assert.IsFalse(rollback.OriginalControls);
			Assert.IsFalse(KingdomSuccessionRules.MayQueueAccessionRepair(
				ReferenceEquals(current, heir), rollback.RegistrationsExact));
			AssertEverySystemHasOnly(systems, third);
		}

		[Test]
		public void RefusedCleanRollbackStuckOnHeirWithRegistrationFailureCannotQueueRepair()
		{
			object founder = new object();
			object heir = new object();
			object current = heir;
			List<FakePlayerSystem> systems = NewPlayerSystems(3, heir);

			KingdomPlayerBodyTransfer rollback = KingdomSuccessionRules.TrySetBodyAndRebindPlayerSystems(
				heir, founder, body => current = heir, () => current, systems,
				(system, body) => system.Bodies.Remove(body),
				(system, body) =>
				{
					if (ReferenceEquals(system, systems[2]))
					{
						throw new InvalidOperationException("injected rollback registration refusal");
					}
					system.Bodies.Add(body);
				});

			Assert.IsTrue(rollback.OriginalControls);
			Assert.IsFalse(rollback.TargetControls);
			Assert.IsFalse(rollback.RegistrationsExact);
			Assert.IsFalse(KingdomSuccessionRules.MayQueueAccessionRepair(
				ReferenceEquals(current, heir), rollback.RegistrationsExact));
		}

		private sealed class FakePlayerSystem
		{
			internal readonly HashSet<object> Bodies = new HashSet<object>();
		}

		private static List<FakePlayerSystem> NewPlayerSystems(int Count, object Body)
		{
			List<FakePlayerSystem> systems = new List<FakePlayerSystem>();
			for (int i = 0; i < Count; i++)
			{
				FakePlayerSystem system = new FakePlayerSystem();
				system.Bodies.Add(Body);
				systems.Add(system);
			}
			return systems;
		}

		private static void AssertEverySystemHasOnly(List<FakePlayerSystem> Systems, object Body)
		{
			for (int i = 0; i < Systems.Count; i++)
			{
				CollectionAssert.AreEquivalent(new[] { Body }, Systems[i].Bodies,
					"player system " + i + " retained a stale body registration");
			}
		}

		[TestCase(true, true, true)]
		[TestCase(false, true, false)]
		[TestCase(true, false, false)]
		[TestCase(false, false, false)]
		public void AccessionFailureTerminalizesOnlyWithExactOriginalCarriersAndFounderControl(
			bool CarriersExactlyOriginal, bool FounderControls, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomSuccessionRules.MayTerminalAfterAccessionFailure(
				CarriersExactlyOriginal, FounderControls));
		}

		[TestCase(false, false, true)]
		[TestCase(true, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, true, false)]
		public void CorruptReadOrPersistedDisableMarkerKeepsSuccessionFailClosedAcrossResave(
			bool CurrentReadFailed, bool PersistedDisabled, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomSuccessionRules.SuccessionEnabled(
				CurrentReadFailed, PersistedDisabled));
		}

		[Test]
		public void SavedSuccessionStateAcceptsOnlyCoherentLifecycleTuples()
		{
			string completed = KingdomSuccessionRules.FounderDeathToken(1, 100L, "founder-one");
			string pending = KingdomSuccessionRules.FounderDeathToken(2, 200L, "founder-two");
			string failure;
			Assert.IsTrue(KingdomSuccessionRules.TryValidateSavedState(0, "", "",
				InterregnumPhase.None, 0L, NewsRoad.Seat, 0, false, "", out failure), failure);
			Assert.IsTrue(KingdomSuccessionRules.TryValidateSavedState(1, pending, completed,
				InterregnumPhase.RiteDue,
				KingdomSuccessionRules.NewsDueTick(200L, 2), NewsRoad.Road, 2,
				true, "", out failure), failure);
			Assert.IsTrue(KingdomSuccessionRules.TryValidateSavedState(1, "", completed,
				InterregnumPhase.Reigning, 0L, NewsRoad.Road, 0, false, completed,
				out failure), failure);

			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(-1, "", "",
				InterregnumPhase.None, 0L, NewsRoad.Seat, 0, false, "", out failure));
			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(1, pending, completed,
				(InterregnumPhase)99, 202L, NewsRoad.Road, 2, true, "", out failure));
			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(1, pending, completed,
				InterregnumPhase.RiteDue, 201L, NewsRoad.Road, 2, true, "", out failure));
			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(1, pending, completed,
				InterregnumPhase.RiteDue, 202L, (NewsRoad)99, 2, true, "", out failure));
			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(1, "", completed,
				InterregnumPhase.RiteDue, 0L, NewsRoad.Seat, 0, false, "", out failure));
			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(1, "", completed,
				InterregnumPhase.Reigning, 0L, NewsRoad.Seat, 0, true, "", out failure));
			Assert.IsFalse(KingdomSuccessionRules.TryValidateSavedState(1, "", completed,
				InterregnumPhase.Reigning, 0L, NewsRoad.Seat, 0, false,
				KingdomSuccessionRules.FounderDeathToken(1, 101L, "other"), out failure));
		}

		[Test]
		public void FounderDeathTokenParserRejectsMalformedAndOversizedValuesWithoutThrowing()
		{
			string valid = KingdomSuccessionRules.FounderDeathToken(3, 400L, "object:id");
			int ordinal;
			long tick;
			Assert.IsTrue(KingdomSuccessionRules.TryReadDeathToken(valid, out ordinal, out tick));
			Assert.AreEqual(3, ordinal);
			Assert.AreEqual(400L, tick);
			string[] invalid = new[] { null, "", "v1:0:0:YQ==", "v1:1:-1:YQ==",
				"v1:1:0:not base64", "v2:1:0:YQ==", new string('x', 513) };
			for (int i = 0; i < invalid.Length; i++)
			{
				Assert.DoesNotThrow(delegate
				{
					Assert.IsFalse(KingdomSuccessionRules.TryReadDeathToken(invalid[i],
						out ordinal, out tick));
				});
			}
		}

		[Test]
		public void ModeOnIsTrueForFlagOrModeId()
		{
			Assert.IsTrue(KingdomSuccessionRules.ModeOn("Classic", true));
			Assert.IsTrue(KingdomSuccessionRules.ModeOn(KingdomSuccessionRules.ModeId, false));
			Assert.IsFalse(KingdomSuccessionRules.ModeOn("Classic", false));
		}

		[Test]
		public void JudgeNewsChoosesRoadInPriorityOrder()
		{
			int days;
			NewsRoad road;

			KingdomSuccessionRules.JudgeNews(ArchAnswers: true, SameWorld: true, DX: 9, DY: 9, DZ: 2, out days, out road);
			Assert.AreEqual(NewsRoad.Arch, road);
			Assert.AreEqual(0, days);

			KingdomSuccessionRules.JudgeNews(ArchAnswers: false, SameWorld: false, DX: 0, DY: 0, DZ: 0, out days, out road);
			Assert.AreEqual(NewsRoad.Rumour, road);
			Assert.AreEqual(KingdomSuccessionRules.RumourDays, days);

			KingdomSuccessionRules.JudgeNews(ArchAnswers: false, SameWorld: true, DX: 0, DY: 0, DZ: 0, out days, out road);
			Assert.AreEqual(NewsRoad.Seat, road);
			Assert.AreEqual(0, days);
		}

		[Test]
		public void NewsArithmeticSaturatesAtHostileIntegerEdges()
		{
			Assert.AreEqual(int.MaxValue,
				KingdomSuccessionRules.NewsSteps(int.MinValue, int.MaxValue, int.MinValue));
			Assert.AreEqual(KingdomSuccessionRules.RumourDays,
				KingdomSuccessionRules.NewsDays(int.MaxValue));

			long nearEnd = long.MaxValue - KingdomRules.TicksPerDay;
			Assert.AreEqual(long.MaxValue, KingdomSuccessionRules.NewsDueTick(nearEnd, 2));
			Assert.AreEqual(0L, KingdomSuccessionRules.NewsDueTick(-100L, -2));
			long exactDue = KingdomSuccessionRules.NewsDueTick(500L, 3);
			Assert.AreEqual(500L + 3L * KingdomRules.TicksPerDay, exactDue);
			Assert.AreEqual(3L * KingdomRules.TicksPerDay,
				KingdomSuccessionRules.WorldTicksUntilDue(500L, exactDue));
			Assert.AreEqual(5L, KingdomSuccessionRules.WorldTicksUntilDue(-10L, 5L));
			Assert.AreEqual(0L, KingdomSuccessionRules.WorldTicksUntilDue(long.MaxValue, long.MaxValue));
		}

		[Test]
		public void InterregnumCrossesOnlyAtTheDueTickOrAfterRite()
		{
			Assert.AreEqual(InterregnumPhase.None,
				KingdomSuccessionRules.Phase(false, false, 100L, 200L));
			Assert.AreEqual(InterregnumPhase.WordOnTheRoad,
				KingdomSuccessionRules.Phase(true, false, 199L, 200L));
			Assert.AreEqual(InterregnumPhase.RiteDue,
				KingdomSuccessionRules.Phase(true, false, 200L, 200L));
			Assert.AreEqual(InterregnumPhase.Reigning,
				KingdomSuccessionRules.Phase(true, true, 0L, long.MaxValue));
		}

		[Test]
		public void TryChooseHeirUsesSeniorityThenTieBreaks()
		{
			KingdomHeir[] candidates = new KingdomHeir[]
			{
				new KingdomHeir("Bela", 50, null, null, true, false, null, 2),
				new KingdomHeir("Ari", 50, null, null, true, false, null, 1),
				new KingdomHeir("Zen", 10, null, null, false, false, null, 3)
			};

			int index;
			Assert.IsTrue(KingdomSuccessionRules.TryChooseHeir(candidates, SuccessionLaw.Seniority, null, out index));
			Assert.AreEqual(1, index, "same arrival tick should break by lexical name");

			Assert.IsTrue(KingdomSuccessionRules.TryChooseHeir(candidates, SuccessionLaw.Designee, "Bela", out index));
			Assert.AreEqual(0, index);
		}

		[Test]
		public void LawNeverSilentlySkipsItsSeniorForAReachableLookingJunior()
		{
			KingdomHeir[] candidates = new KingdomHeir[]
			{
				new KingdomHeir("Senior", 1L, null, null, true, false, null, 7),
				new KingdomHeir("Junior", 2L, null, null, true, false, "JoppaWorld.1.1.1.1.10", 8)
			};

			int index;
			Assert.IsTrue(KingdomSuccessionRules.TryChooseHeir(
				candidates, SuccessionLaw.Seniority, null, out index));
			Assert.AreEqual(0, index,
				"body reachability is a verdict on the chosen heir, never a reason to choose another");
		}

		[Test]
		public void JudgeAndDynastyEndRulesMatchVerdictTable()
		{
			Assert.AreEqual(SuccessionVerdict.NotKingdomMode, KingdomSuccessionRules.Judge(false, true, true, true));
			Assert.AreEqual(SuccessionVerdict.Unfounded, KingdomSuccessionRules.Judge(true, false, true, true));
			Assert.AreEqual(SuccessionVerdict.NoHeir, KingdomSuccessionRules.Judge(true, true, false, true));
			Assert.AreEqual(SuccessionVerdict.HeirUnreachable, KingdomSuccessionRules.Judge(true, true, true, false));
			Assert.AreEqual(SuccessionVerdict.Succeeds, KingdomSuccessionRules.Judge(true, true, true, true));

			Assert.IsTrue(KingdomSuccessionRules.DynastyEnds(SuccessionVerdict.NoHeir));
			Assert.IsTrue(KingdomSuccessionRules.DynastyEnds(SuccessionVerdict.HeirUnreachable));
			Assert.IsFalse(KingdomSuccessionRules.DynastyEnds(SuccessionVerdict.Succeeds));
		}

		[Test]
		public void AccessionRegardIsBoundedAndMonthBased()
		{
			long now = KingdomRules.TicksPerDay * KingdomSuccessionRules.DaysPerMonth * 20L;
			int regard = KingdomSuccessionRules.AccessionRegard(
				ArrivedTick: 1,
				NowTick: now,
				CreedMatchesRealm: true,
				OnceLeftRealmCreed: false,
				HoldsOffice: true);

			Assert.LessOrEqual(regard, KingdomSuccessionRules.AccessionRegardCeiling);
			Assert.GreaterOrEqual(regard, KingdomSuccessionRules.AccessionRegardFloor);
			Assert.AreEqual(KingdomSuccessionRules.MonthsServedCap, KingdomSuccessionRules.MonthsServed(1, now));
		}

		[Test]
		public void ForgetsExemptsMapAndAccomplishments()
		{
			Assert.IsFalse(KingdomSuccessionRules.Forgets(JournalKind.MapNote, true));
			Assert.IsFalse(KingdomSuccessionRules.Forgets(JournalKind.Accomplishment, true));
			Assert.IsTrue(KingdomSuccessionRules.Forgets(JournalKind.GeneralNote, true));
			Assert.IsFalse(KingdomSuccessionRules.Forgets(JournalKind.GeneralNote, false));
		}

		[Test]
		public void FounderDeathTokensAreExactAndAttributesDoNotAlias()
		{
			string first = KingdomSuccessionRules.FounderDeathToken(1, 1200L, "object:one");
			string same = KingdomSuccessionRules.FounderDeathToken(1, 1200L, "object:one");
			string otherObject = KingdomSuccessionRules.FounderDeathToken(1, 1200L, "object:two");
			string otherTick = KingdomSuccessionRules.FounderDeathToken(1, 1201L, "object:one");

			Assert.AreEqual(first, same);
			Assert.AreNotEqual(first, otherObject);
			Assert.AreNotEqual(first, otherTick);
			string stamp = KingdomSuccessionRules.FounderAttribute(first);
			Assert.IsTrue(KingdomSuccessionRules.StampedBy(stamp,
				KingdomSuccessionRules.FounderAttribute(same)));
			Assert.IsFalse(KingdomSuccessionRules.StampedBy(stamp,
				KingdomSuccessionRules.FounderAttribute(otherObject)));
		}

		[Test]
		public void AttemptGatePreventsEveryDuplicateAndConflictingReplay()
		{
			Assert.AreEqual(SuccessionAttemptVerdict.Invalid,
				KingdomSuccessionRules.JudgeAttempt(null, null, null));
			Assert.AreEqual(SuccessionAttemptVerdict.Begin,
				KingdomSuccessionRules.JudgeAttempt("a", null, null));
			Assert.AreEqual(SuccessionAttemptVerdict.DuplicatePending,
				KingdomSuccessionRules.JudgeAttempt("a", "a", null));
			Assert.AreEqual(SuccessionAttemptVerdict.Conflict,
				KingdomSuccessionRules.JudgeAttempt("b", "a", null));
			Assert.AreEqual(SuccessionAttemptVerdict.AlreadyCompleted,
				KingdomSuccessionRules.JudgeAttempt("a", null, "a"));
		}

		[Test]
		public void CostsSeatOnlyForChosenHeirWhenEnabled()
		{
			Assert.IsTrue(KingdomSuccessionRules.CostsTheSeat(HeirChoice.Chosen, true));
			Assert.IsFalse(KingdomSuccessionRules.CostsTheSeat(HeirChoice.Law, true));
			Assert.IsFalse(KingdomSuccessionRules.CostsTheSeat(HeirChoice.Chosen, false));
		}
	}
}
#endif
