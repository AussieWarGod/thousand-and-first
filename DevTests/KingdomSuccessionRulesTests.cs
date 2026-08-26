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
		public void SaveCarriedSuccessionEnumsKeepIntLayoutAndExactValues()
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(InterregnumPhase)));
			CollectionAssert.AreEqual(new[] { "None", "WordOnTheRoad", "RiteDue", "Reigning" },
				Enum.GetNames(typeof(InterregnumPhase)));
			Assert.AreEqual(0, (int)InterregnumPhase.None);
			Assert.AreEqual(1, (int)InterregnumPhase.WordOnTheRoad);
			Assert.AreEqual(2, (int)InterregnumPhase.RiteDue);
			Assert.AreEqual(3, (int)InterregnumPhase.Reigning);

			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(NewsRoad)));
			CollectionAssert.AreEqual(new[] { "Seat", "Road", "Arch", "Rumour" },
				Enum.GetNames(typeof(NewsRoad)));
			Assert.AreEqual(0, (int)NewsRoad.Seat);
			Assert.AreEqual(1, (int)NewsRoad.Road);
			Assert.AreEqual(2, (int)NewsRoad.Arch);
			Assert.AreEqual(3, (int)NewsRoad.Rumour);

			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(MourningRiteStage)));
			CollectionAssert.AreEqual(new[] { "None", "Frozen", "WordArrived",
				"ProcessionComplete", "ShrinePlaced", "BodyCrossed", "Complete" },
				Enum.GetNames(typeof(MourningRiteStage)));
			Assert.AreEqual(0, (int)MourningRiteStage.None);
			Assert.AreEqual(1, (int)MourningRiteStage.Frozen);
			Assert.AreEqual(2, (int)MourningRiteStage.WordArrived);
			Assert.AreEqual(3, (int)MourningRiteStage.ProcessionComplete);
			Assert.AreEqual(4, (int)MourningRiteStage.ShrinePlaced);
			Assert.AreEqual(5, (int)MourningRiteStage.BodyCrossed);
			Assert.AreEqual(6, (int)MourningRiteStage.Complete);
		}

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
		public void QuestInheritanceIsFlavorOnlyAndClassifiesTheDocumentedPersonalSet()
		{
			Assert.IsTrue(KingdomSuccessionRules.PersonalQuest(
				"Fetch Argyve a Knickknack", "renamed by another display layer"));
			Assert.IsTrue(KingdomSuccessionRules.PersonalQuest(null,
				"Pax Klanq, I Presume? (inherited)"));
			Assert.IsTrue(KingdomSuccessionRules.PersonalQuest(
				"If, Then, Else", "If, Then, Else"));
			Assert.IsFalse(KingdomSuccessionRules.PersonalQuest(
				"What's Eating the Watervine?", "What's Eating the Watervine?"));
			Assert.IsFalse(KingdomSuccessionRules.PersonalQuest(
				"Tomb of the Eaters", "Tomb of the Eaters"));
			Assert.IsFalse(KingdomSuccessionRules.PersonalQuest(
				"dynamic village quest:1", "Find a snapjaw fort"));

			string inherited = KingdomSuccessionRules.InheritedQuestName("The Assessment");
			Assert.AreEqual("The Assessment (inherited)", inherited);
			Assert.AreEqual(inherited, KingdomSuccessionRules.InheritedQuestName(inherited));
			Assert.AreEqual("The Assessment",
				KingdomSuccessionRules.WithoutInheritedSuffix(inherited));
		}

		[Test]
		public void QuestInheritanceIdsAreBoundedStableAndDomainSeparated()
		{
			string death = KingdomSuccessionRules.FounderDeathToken(2, 12345L, "founder:id");
			string chronicle = KingdomSuccessionRules.InheritedQuestEventId(death,
				"The Assessment");
			string same = KingdomSuccessionRules.InheritedQuestEventId(death,
				"The Assessment");
			string map = KingdomSuccessionRules.QuestOriginSecretId(death,
				"The Assessment");
			string other = KingdomSuccessionRules.InheritedQuestEventId(death,
				"A Signal in the Noise");
			string accession = KingdomSuccessionRules.AccessionRiteEventId(death);

			Assert.AreEqual(chronicle, same);
			Assert.AreNotEqual(chronicle, map);
			Assert.AreNotEqual(chronicle, other);
			Assert.AreNotEqual(chronicle, accession);
			Assert.Less(chronicle.Length, 128);
			Assert.Less(accession.Length, 128);
			StringAssert.StartsWith("taf:succession:unfinished:v1:", chronicle);
			StringAssert.StartsWith("taf:succession:quest-origin:v1:", map);
			StringAssert.StartsWith("taf:succession:accession-rite:v1:", accession);
			Assert.IsNull(KingdomSuccessionRules.InheritedQuestEventId(death,
				new string('q', KingdomSuccessionRules.MaxQuestIdentityChars + 1)));
		}

		[Test]
		public void QuestInheritanceTellingsStripDisplaySuffixAndBoundThirdPartyText()
		{
			string line = KingdomSuccessionRules.InheritedQuestChronicle("Nara",
				"The Assessment (inherited)");
			Assert.AreEqual("Nara died with The Assessment undone, and the heir inherited the undertaking",
				line);
			Assert.IsFalse(line.Contains("(inherited)"));
			Assert.Less(KingdomSuccessionRules.QuestMarkNote(
				new string('x', 5000), new string('y', 5000)).Length, 1100);
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

		[Test]
		public void MourningRiteCheckpointsRetryButNeverSkipPhysicalEvidence()
		{
			for (int i = 0; i <= (int)MourningRiteStage.Complete; i++)
			{
				MourningRiteStage stage = (MourningRiteStage)i;
				Assert.IsTrue(KingdomSuccessionRules.MayAdvanceRite(stage, stage));
				if (i < (int)MourningRiteStage.Complete)
				{
					Assert.IsTrue(KingdomSuccessionRules.MayAdvanceRite(stage,
						(MourningRiteStage)(i + 1)));
				}
				if (i + 2 <= (int)MourningRiteStage.Complete)
				{
					Assert.IsFalse(KingdomSuccessionRules.MayAdvanceRite(stage,
						(MourningRiteStage)(i + 2)));
				}
			}
		}

		[Test]
		public void RiteManifestRoundTripsExactBodiesHomesPostsAndCells()
		{
			KingdomRiteAttendee[] rows =
			{
				new KingdomRiteAttendee(7, "body:7", "A|sha", "JoppaWorld.1.1.1.1.10",
					2, 3, "9/4", "plot:a", 20, 11),
				new KingdomRiteAttendee(8, "body:8", "B\nren", "JoppaWorld.1.1.1.1.10",
					4, 5, "0/0", "", 21, 11)
			};
			string encoded = KingdomSuccessionRules.EncodeRiteManifest(rows);
			const string golden = "v1|7|Ym9keTo3|QXxzaGE=|Sm9wcGFXb3JsZC4xLjEuMS4xLjEw|2|3|OS80|cGxvdDph|20|11\n"
				+ "v1|8|Ym9keTo4|QgpyZW4=|Sm9wcGFXb3JsZC4xLjEuMS4xLjEw|4|5|MC8w||21|11";
			Assert.AreEqual(golden, encoded);
			KingdomRiteAttendee[] read;
			Assert.IsTrue(KingdomSuccessionRules.TryDecodeRiteManifest(encoded, out read));
			Assert.AreEqual(golden, KingdomSuccessionRules.EncodeRiteManifest(read));
			Assert.AreEqual(2, read.Length);
			Assert.AreEqual("A|sha", read[0].Name);
			Assert.AreEqual("plot:a", read[0].Home);
			Assert.AreEqual(21, read[1].RiteX);
			Assert.IsFalse(KingdomSuccessionRules.TryDecodeRiteManifest(
				encoded + "\n" + encoded.Split('\n')[0], out read),
				"a duplicate resident/body row must never be accepted after a cut");
		}

		[Test]
		public void RiteManifestAdmitsEveryResidentInALegalFullCityAndNoSixtyFirst()
		{
			Assert.AreEqual(KingdomRules.MaxPopulation,
				KingdomSuccessionRules.MaxRiteAttendees);
			KingdomRiteAttendee[] full = new KingdomRiteAttendee[KingdomRules.MaxPopulation];
			for (int i = 0; i < full.Length; i++)
			{
				full[i] = new KingdomRiteAttendee(i + 1, "body:" + (i + 1),
					"resident " + (i + 1), "JoppaWorld.1.1.1.1.10", i % 80,
					i % 25, i + "/1", "plot:" + i, (i + 10) % 80, (i + 5) % 25);
			}
			string encoded = KingdomSuccessionRules.EncodeRiteManifest(full);
			Assert.IsNotEmpty(encoded);
			Assert.LessOrEqual(encoded.Length, KingdomSuccessionRules.MaxRiteManifestChars);
			Assert.IsTrue(KingdomSuccessionRules.TryDecodeRiteManifest(encoded,
				out KingdomRiteAttendee[] decoded));
			Assert.AreEqual(KingdomRules.MaxPopulation, decoded.Length);

			KingdomRiteAttendee[] tooMany = new KingdomRiteAttendee[full.Length + 1];
			Array.Copy(full, tooMany, full.Length);
			tooMany[full.Length] = new KingdomRiteAttendee(1000, "body:1000", "extra",
				"JoppaWorld.1.1.1.1.10", 1, 1, "0/0", "plot:extra", 2, 2);
			Assert.AreEqual("", KingdomSuccessionRules.EncodeRiteManifest(tooMany));
		}

		[Test]
		public void FounderShrinePlacementIsStrictCheckBeforeMint()
		{
			Assert.AreEqual(FounderShrinePlacementVerdict.Create,
				KingdomSuccessionRules.JudgeFounderShrinePlacement(false, false, true, 0));
			Assert.AreEqual(FounderShrinePlacementVerdict.AdoptExact,
				KingdomSuccessionRules.JudgeFounderShrinePlacement(true, true, false, 4));
			Assert.AreEqual(FounderShrinePlacementVerdict.Refuse,
				KingdomSuccessionRules.JudgeFounderShrinePlacement(true, false, true, 0));
			Assert.AreEqual(FounderShrinePlacementVerdict.Refuse,
				KingdomSuccessionRules.JudgeFounderShrinePlacement(false, false, true, 1));
		}

		[Test]
		public void RuntimeOrdersRealProcessionAndShrineBeforeBodyBoundary()
		{
			string succession = KingdomSuccessionLogicalSource.Read();
			string rite = SuccessionRiteSource();
			string rules = TestMain.ReadRepositoryText("Experience/KingdomSuccessionRules.cs");
			int freeze = succession.IndexOf("Checkpoint(MourningRiteStage.Frozen)",
				StringComparison.Ordinal);
			int time = succession.IndexOf("game.TimeTicks = dueTick", freeze,
				StringComparison.Ordinal);
			int procession = succession.IndexOf("TryHoldProcession", time,
				StringComparison.Ordinal);
			int shrine = succession.IndexOf("TryEnsureFounderShrine", procession,
				StringComparison.Ordinal);
			int body = succession.IndexOf("SetPlayerBodyAndRebindAll(game, founder", shrine,
				StringComparison.Ordinal);
			Assert.Greater(time, freeze);
			Assert.Greater(procession, time);
			Assert.Greater(shrine, procession);
			Assert.Greater(body, shrine);
			StringAssert.Contains("InjectedCheckpoint?.Invoke(Stage)", succession);
			StringAssert.Contains("System.Away?.ClaimedZones", succession,
				"either owned city's ground must be immediate local news");
			Assert.IsFalse(succession.Contains("MessageQueue.AddPlayerMessage"),
				"the rite popup is the one successful semantic message");
			StringAssert.Contains("Brain.PushGoal(new MoveTo", rite);
			StringAssert.Contains("body.Move(path.Directions[i]", rite);
			StringAssert.Contains("UnchangedPosts", rite);
			StringAssert.Contains("MaxRiteAttendees = KingdomRules.MaxPopulation", rules);
			StringAssert.Contains("for (int i = 0; i < bodies.Count; i++)", rite);
			StringAssert.Contains("state.ResidentCount", rite);
			StringAssert.Contains("result.Count >= needed", rite);
			Assert.IsFalse(rite.Contains("radius <= 3"));
			Assert.IsFalse(rite.Contains(
				"rows.Count < KingdomSuccessionRules.MaxRiteAttendees"));
			Assert.IsFalse(rite.Contains("SystemLongDistanceMoveTo"));
			Assert.IsFalse(rite.Contains("Teleport"));
			Assert.IsFalse(rite.Contains("CreateObject(\"Creature"));
		}

		[Test]
		public void SuccessionLogicalSourceKeepsSerializedShapeAndDeathMutationOrder()
		{
			string source = KingdomSuccessionLogicalSource.Read();
			Assert.AreEqual(12, Count(source, "public sealed partial class KingdomSuccession"));
			Assert.AreEqual(1, Count(source,
				"public sealed partial class KingdomSuccession : IPlayerSystem"));
			Assert.AreEqual(1, Count(source, "[Serializable]"));
			Assert.AreEqual(4, Count(source, "[NonSerialized]"));
			Assert.AreEqual(1, Count(source, "private sealed class HeirRuntime"));
			Assert.AreEqual(1, Count(source, "private sealed class JournalSnapshot"));
			AssertOrdered(source,
				"private int SerializationVersion = CurrentSerializationVersion;",
				"private int SuccessionOrdinal;", "private string PendingDeathToken;",
				"private string CompletedDeathToken;", "private InterregnumPhase PendingPhase;",
				"private long PendingDueTick;", "private NewsRoad PendingRoad;",
				"private int PendingDays;", "private string PendingSealAccessionToken;",
				"private string PendingSealRiteChronicle;",
				"private bool PendingSealAccessionReady;",
				"private int PendingAccessionRepairResidentId;",
				"private string PendingAccessionRepairFounderName;",
				"private string PendingAccessionRepairHeirName;",
				"private bool PendingAccessionRepairSeated;",
				"private long PendingAccessionRepairArrivedTick;",
				"private string PendingAccessionRepairKeptCreeds;",
				"private MourningRiteStage PendingRiteStage;",
				"private string PendingFounderName;", "private string PendingFounderObjectId;",
				"private string PendingFounderCause;", "private int PendingHeirResidentId;",
				"private string PendingHeirObjectId;", "private string PendingHeirName;",
				"private string PendingHeirZoneId;", "private string PendingRiteZoneId;",
				"private string PendingRiteCityName;",
				"private string PendingRiteFixtureObjectId;",
				"private string PendingRiteFixtureName;", "private int PendingShrineX;",
				"private int PendingShrineY;", "private string PendingRiteAttendeeManifest;",
				"private string PendingShrineObjectId;", "private string CompletedShrineToken;",
				"private string CompletedShrineObjectId;", "private string CompletedShrineZoneId;",
				"private bool LegacyPhysicalRiteUnavailable;", "private bool SuccessionDisabled;",
				"private bool LoadFailed;", "private bool DeathChroniclePublished;",
				"private bool AccessionOwnershipCommitted;",
				"internal static Action<MourningRiteStage> InjectedCheckpoint = null;");

			Assert.AreEqual(1, Count(source, "CarryFounderSuccession(E, game, founder, system"));
			Assert.AreEqual(1, Count(source, "private void CarryFounderSuccession("));
			AssertOrdered(source,
				"private void HandleFounderDeath(AfterDieEvent E)",
				"KingdomSuccessionRite.TryFreeze(system, heirBook, heirBody, riteCityName",
				"CarryFounderSuccession(E, game, founder, system",
				"private void CarryFounderSuccession(AfterDieEvent E");

			int execution = source.IndexOf("private void CarryFounderSuccession(AfterDieEvent E",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(execution, 0);
			AssertOrdered(source.Substring(execution),
				"PendingDeathToken = token;", "PendingPhase = InterregnumPhase.WordOnTheRoad;",
				"Checkpoint(MourningRiteStage.Frozen);", "founder.AddPart(remains);",
				"game.TimeTicks = dueTick;", "Checkpoint(MourningRiteStage.WordArrived);",
				"KingdomSuccessionRite.TryHoldProcession(",
				"Checkpoint(MourningRiteStage.ProcessionComplete);",
				"KingdomSuccessionRite.TryEnsureFounderShrine(",
				"PendingShrineObjectId = founderShrine.IDIfAssigned;",
				"Checkpoint(MourningRiteStage.ShrinePlaced);",
				"KingdomCitizenship.CanRemove(system, heirBody",
				"KingdomPlayerBodyTransfer forward = SetPlayerBodyAndRebindAll(game, founder,",
				"Checkpoint(MourningRiteStage.BodyCrossed);",
				"accession = KingdomResidents.TryAccede(system, heirBody,",
				"CompleteAccession(game, system, heirBody, founderName, formerRow, token");

			AssertOrdered(source,
				"Writer.Write(SerializationMagic);", "Writer.Write(CurrentSerializationVersion);",
				"Writer.WriteNamedFields(this, typeof(KingdomSuccession)",
				"int magic = Reader.ReadInt32();", "int version = Reader.ReadInt32();",
				"Reader.ReadNamedFields(this, typeof(KingdomSuccession)",
				"MigrateSavedState(version);", "ValidateSavedState();");
		}

		[Test]
		public void SuccessionRiteLogicalSourceKeepsNestedAbiAndExactEngineOrder()
		{
			string rite = SuccessionRiteSource();
			Assert.AreEqual(5, Count(rite, "internal static partial class KingdomSuccessionRite"));
			Assert.AreEqual(1, Count(rite, "internal sealed class Plan"));
			Assert.AreEqual(1, Count(rite, "private sealed class Walker"));
			AssertOrdered(rite,
				"internal string ZoneId;", "internal string CityName;",
				"internal string FixtureObjectId;", "internal string FixtureName;",
				"internal int ShrineX;", "internal int ShrineY;", "internal string Manifest;");
			AssertOrdered(rite,
				"internal static bool TryFreeze(", "internal static bool TryHoldProcession(",
				"internal static bool ProcessionEvidence(", "internal static bool TryEnsureFounderShrine(",
				"private static bool Walk(", "private static bool CanWalk(",
				"private static void ReturnAll(", "private static bool UnchangedPosts(",
				"private static string PostReceipt(", "private static bool TryExactResidentsIn(",
				"private static List<Cell> OpenRiteCells(", "private static GameObject FindFixture(",
				"private static GameObject FindByAssignedId(", "private static Zone ExactLoadedZone(",
				"private static bool OwnedGround(", "private sealed class Walker");
			AssertOrdered(rite,
				"internal readonly GameObject Body;", "internal readonly KingdomRiteAttendee Row;",
				"internal readonly Cell RiteCell;", "internal readonly Cell OriginalCell;");

			int tokenReceipt = rite.IndexOf(
				"fixture.SetStringProperty(\"KingdomLastMourningRiteToken\"", StringComparison.Ordinal);
			Assert.GreaterOrEqual(tokenReceipt, 0);
			AssertOrdered(rite.Substring(tokenReceipt),
				"fixture.SetStringProperty(\"KingdomLastMourningRiteToken\"",
				"fixture.SetStringProperty(\"KingdomLastMourningAttendees\"",
				"for (int i = walkers.Count - 1; i >= 1; i--)",
				"heir = walkers[0].Body;");
			AssertOrdered(rite,
				"created = GameObject.Create(ShrineBlueprint);", "part.Stamp(token, founderName",
				"created.SetIntProperty(\"KingdomFounderShrine\", 1);",
				"created.SetStringProperty(\"KingdomFounderDeathToken\", token);",
				"cell.AddObject(created);", "string id = created.ID;");
			AssertOrdered(rite,
				"body.Brain.PushGoal(new MoveTo", "FindPath path = new FindPath(",
				"body.Move(path.Directions[i]", "body.Brain.Goals.Items.Clear();",
				"body.Brain.Goals.Items.AddRange(goals);", "body.Brain.StartingCell = anchor;",
				"body.Brain.Staying = staying;");
		}

		[Test]
		public void RuntimeInheritsOpenQuestFlavorWithoutChangingQuestState()
		{
			string succession = KingdomSuccessionLogicalSource.Read();
			string remains = TestMain.ReadRepositoryText("Experience/KingdomFounderRemains.cs");
			StringAssert.Contains("TryInheritOpenQuests(Game, System, Token, FounderName)",
				succession);
			StringAssert.Contains("KingdomChronicle.RecordOnce", succession);
			StringAssert.Contains("quest.QuestGiverLocationZoneID", succession);
			StringAssert.Contains("JournalAPI.AddMapNote", succession);
			StringAssert.Contains("QuestOriginSecretId", succession);
			int pending = succession.IndexOf("private bool TryPublishPendingAccessionRite",
				StringComparison.Ordinal);
			int bound = succession.IndexOf("private static string BoundPendingRite", pending,
				StringComparison.Ordinal);
			string pendingRite = succession.Substring(pending, bound - pending);
			StringAssert.Contains("AccessionRiteEventId", pendingRite);
			StringAssert.Contains("KingdomChronicle.RecordOnce", pendingRite);
			StringAssert.DoesNotContain("KingdomChronicle.Record(system", pendingRite);
			StringAssert.Contains("quest.SetProperty(KingdomSuccessionRules.InheritedQuestMarker",
				succession);
			StringAssert.Contains("out revealed, out questMarks", remains);
			StringAssert.Contains("revealed, questMarks", remains);
			Assert.IsFalse(succession.Contains("FailQuest("));
			Assert.IsFalse(succession.Contains("FinishQuest("));
			Assert.IsFalse(succession.Contains("Quests.Remove("));
			Assert.IsFalse(succession.Contains("FinishedQuests.Add("));
		}

		[Test]
		public void FounderShrineIsDistinctVersionedHistoryWithoutResourceParts()
		{
			string part = TestMain.ReadRepositoryText("Experience/KingdomFounderShrine.cs");
			string xml = TestMain.ReadRepositoryText("ObjectBlueprints.xml");
			StringAssert.Contains("CurrentSerializationVersion = 1", part);
			StringAssert.Contains("DeathToken", part);
			StringAssert.Contains("FounderName", part);
			StringAssert.Contains("DeathTick", part);
			StringAssert.Contains("Cause", part);
			StringAssert.Contains("History", part);
			StringAssert.Contains("Name=\"r_KingdomFounderShrine\"", xml);
			Assert.IsFalse(xml.Substring(xml.IndexOf("Name=\"r_KingdomFounderShrine\"",
				StringComparison.Ordinal), 700).Contains("LiquidVolume"));
		}

		private static string SuccessionRiteSource()
		{
			return string.Join("\n", new[]
			{
				TestMain.ReadRepositoryText("Experience/KingdomSuccessionRite.cs"),
				TestMain.ReadRepositoryText("Experience/KingdomSuccessionRite.Procession.cs"),
				TestMain.ReadRepositoryText("Experience/KingdomSuccessionRite.Shrine.cs"),
				TestMain.ReadRepositoryText("Experience/KingdomSuccessionRite.Movement.cs"),
				TestMain.ReadRepositoryText("Experience/KingdomSuccessionRite.Attendance.cs")
			});
		}

		private static void AssertOrdered(string source, params string[] terms)
		{
			int at = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, terms[i]);
				at = next;
			}
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
#endif
