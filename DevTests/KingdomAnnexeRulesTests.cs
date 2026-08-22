#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The becoming annexe: the rolls, the ceremony's judgment, the answer the rolls give to
	/// vanilla's own question, and every sentence the register speaks.
	/// <para>
	/// The container tests are driven through <c>ReadFrom</c>/<c>WriteTo</c> directly rather than
	/// through the secession or exile code, for the reason <c>KingdomKnowledgeSitingTests</c>
	/// states about the keepers' roster and which is the whole claim being made here too: the
	/// rolls ride the container, so secession and exile are already correct, and if they did not,
	/// no amount of code in either file would fix it.
	/// </para>
	/// </summary>
	public class KingdomAnnexeRulesTests
	{
		private const string Founder = "41";

		private const string Citizen = "77";

		private const string Stranger = "99";

		private static string Rolls(params string[] Who)
		{
			List<string> keys = new List<string>();
			for (int i = 0; i < Who.Length; i++)
			{
				keys.Add(KingdomAnnexeRules.EnrolmentKey(Who[i]));
			}
			return KingdomZoningRules.EncodeRoster(keys);
		}

		private static List<string> Decoded(string Encoded)
		{
			return KingdomZoningRules.DecodeRoster(Encoded);
		}

		// --- The roll itself: composed, matched, round-tripped ------------------------------------

		[Test]
		public void ARollIsAnOrdinaryRosterKeyOfItsOwnKind()
		{
			Assert.AreEqual("enrolled:41", KingdomAnnexeRules.EnrolmentKey(Founder));
			Assert.AreEqual(KingdomAnnexeRules.EnrolmentKind,
				KingdomZoningRules.KindOf(KingdomAnnexeRules.EnrolmentKey(Founder)));
			Assert.AreEqual(Founder, KingdomZoningRules.NameOf(KingdomAnnexeRules.EnrolmentKey(Founder)));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("4|1")]
		public void AnIdentityThatCouldNotSurviveTheStoreIsRefusedRatherThanWritten(string id)
		{
			// Hostile-input discipline (STANDARDS 9): a bad identity disables one roll, never the
			// city's whole roster. The pipe case is the one that matters -- it is the store's own
			// separator, and a key carrying it would corrupt every roll after it.
			Assert.IsNull(KingdomAnnexeRules.EnrolmentKey(id));
			Assert.IsFalse(KingdomAnnexeRules.Enrolled(Decoded(Rolls(Founder)), id));
		}

		[Test]
		public void EnrolmentRoundTripsThroughTheStoreExactly()
		{
			string stored = Rolls(Founder, Citizen);
			List<string> roster = Decoded(stored);
			Assert.AreEqual(stored, KingdomZoningRules.EncodeRoster(roster));
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(roster, Founder));
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(roster, Citizen));
			Assert.IsFalse(KingdomAnnexeRules.Enrolled(roster, Stranger));
		}

		[Test]
		public void EnrolledReadsAQualifiedKeyAndNeverABareName()
		{
			// The one collision the shared roster could have: Knows matches an unqualified
			// requirement against any kind. Every read here is qualified, so a design gated on a
			// bare name can never enrol anybody, whatever an author writes.
			List<string> roster = Decoded("node:41|disk:41");
			Assert.IsFalse(KingdomAnnexeRules.Enrolled(roster, Founder),
				"a node and a disk that happen to share the id's spelling are not a roll");
		}

		[Test]
		public void RollsListsOnlyRollsAndKeepsTheOrderTheyWereWrittenIn()
		{
			List<string> roster = Decoded("node:notes|" + Rolls(Founder, Citizen) + "|machine:solar still");
			List<string> rolls = KingdomAnnexeRules.Rolls(roster);
			Assert.AreEqual(2, rolls.Count);
			Assert.AreEqual(Founder, rolls[0]);
			Assert.AreEqual(Citizen, rolls[1]);
		}

		[Test]
		public void RollsIsEmptyRatherThanNullForACityThatKeepsNone()
		{
			Assert.AreEqual(0, KingdomAnnexeRules.Rolls(null).Count);
			Assert.AreEqual(0, KingdomAnnexeRules.Rolls(Decoded("node:notes")).Count);
		}

		// --- The rolls cost the city nothing it did not mean to spend ------------------------------

		[Test]
		public void ARollIsWorthNoCraftAtAll()
		{
			// The rolls share the keepers' roster, which is what buys secession for free -- so they
			// must not buy anything ELSE for free. A city that enrolled seven people must not read
			// as a city that certified seven machines.
			Assert.AreEqual(0, KingdomZoningRules.PointsForKind(KingdomAnnexeRules.EnrolmentKind));
			List<string> roster = Decoded(Rolls("1", "2", "3", "4", "5", "6", "7", "8"));
			Assert.AreEqual(0, KingdomZoningRules.TechPoints(roster));
			Assert.AreEqual(TechLevel.Hands, KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(roster)));
		}

		[Test]
		public void ARollSatisfiesNoBuildingsKnowledgeGate()
		{
			List<string> roster = Decoded(Rolls(Founder, Citizen));
			Assert.IsFalse(KingdomZoningRules.Knows(roster, "node:chimerism"));
			Assert.IsFalse(KingdomZoningRules.Knows(roster, "machine:Solar Still"));
			Assert.IsFalse(KingdomProcedureRules.KnowledgeMet(roster, "node:graft"));
		}

		// --- The answer to vanilla's own question (END-STATE §2.3) ---------------------------------

		[Test]
		public void AnEnrolledMutantReadsAsTrueKin()
		{
			Assert.IsTrue(KingdomAnnexeRules.AnswersTrueKin(KinByBirth: false, Roster: Decoded(Rolls(Founder)), Who: Founder));
		}

		[Test]
		public void AnUnenrolledMutantDoesNot()
		{
			Assert.IsFalse(KingdomAnnexeRules.AnswersTrueKin(KinByBirth: false, Roster: Decoded(Rolls(Citizen)), Who: Founder));
			Assert.IsFalse(KingdomAnnexeRules.AnswersTrueKin(KinByBirth: false, Roster: null, Who: Founder));
		}

		[Test]
		public void TrueKinAreUnaffectedInBothDirections()
		{
			// The property the whole override rests on: IsTrueKinEvent.Check hands each handler
			// the running answer to REWRITE, so a handler that could write false would be able to
			// un-Kin somebody born to it. Ours ORs, so it cannot -- and a True Kin carrying a
			// LAPSED roll is the case that would catch a regression.
			Assert.IsTrue(KingdomAnnexeRules.AnswersTrueKin(KinByBirth: true, Roster: null, Who: Founder));
			Assert.IsTrue(KingdomAnnexeRules.AnswersTrueKin(KinByBirth: true, Roster: Decoded(""), Who: Founder));
			Assert.IsTrue(KingdomAnnexeRules.AnswersTrueKin(Seeded: true, Held: false));
			Assert.IsTrue(KingdomAnnexeRules.AnswersTrueKin(Seeded: true, Held: true));
		}

		[TestCase(false, false, false)]
		[TestCase(false, true, true)]
		[TestCase(true, false, true)]
		[TestCase(true, true, true)]
		public void TheAnswerRaisesAndNeverLowers(bool seeded, bool held, bool expected)
		{
			Assert.AreEqual(expected, KingdomAnnexeRules.AnswersTrueKin(seeded, held));
		}

		// --- The ceremony's judgment ---------------------------------------------------------------

		private static KingdomEnrolVerdict Judge(bool founded = true, bool annexe = true, bool staffed = true,
			bool ours = true, bool kin = false, bool enrolled = false, int water = 999)
		{
			return KingdomAnnexeRules.Judge(founded, annexe, staffed, ours, kin, enrolled, water);
		}

		[Test]
		public void AWholeCityAndAKeeperAndTheWaterMeansYes()
		{
			Assert.AreEqual(KingdomEnrolVerdict.Allowed, Judge());
		}

		[Test]
		public void EveryRefusalIsReachableAndInItsOwnOrder()
		{
			// Each case turns off exactly one thing, with everything nearer the top already true,
			// so a reordering of the frozen ladder fails here rather than in play.
			Assert.AreEqual(KingdomEnrolVerdict.Unfounded, Judge(founded: false));
			Assert.AreEqual(KingdomEnrolVerdict.NoAnnexe, Judge(annexe: false));
			Assert.AreEqual(KingdomEnrolVerdict.Unstaffed, Judge(staffed: false));
			Assert.AreEqual(KingdomEnrolVerdict.NotOurs, Judge(ours: false));
			Assert.AreEqual(KingdomEnrolVerdict.Kin, Judge(kin: true));
			Assert.AreEqual(KingdomEnrolVerdict.Enrolled, Judge(enrolled: true));
			Assert.AreEqual(KingdomEnrolVerdict.Unpaid, Judge(water: KingdomAnnexeRules.EnrolmentDrams - 1));
		}

		[Test]
		public void TheLadderIsFrozenWhereTwoRefusalsAreTrueAtOnce()
		{
			// The order only MEANS anything where two things are wrong together, and that is
			// exactly where the earlier test cannot see it. Each pair below turns off two rungs and
			// asserts the nearer one is what the founder is told about, so a reordering fails here
			// rather than by telling somebody to fill their stores when they have no annexe.
			Assert.AreEqual(KingdomEnrolVerdict.Unfounded, Judge(founded: false, annexe: false, staffed: false, ours: false, kin: true, enrolled: true, water: 0));
			Assert.AreEqual(KingdomEnrolVerdict.NoAnnexe, Judge(annexe: false, staffed: false, ours: false, kin: true, enrolled: true, water: 0));
			Assert.AreEqual(KingdomEnrolVerdict.Unstaffed, Judge(staffed: false, ours: false, kin: true, enrolled: true, water: 0));
			Assert.AreEqual(KingdomEnrolVerdict.NotOurs, Judge(ours: false, kin: true, enrolled: true, water: 0));
			Assert.AreEqual(KingdomEnrolVerdict.Kin, Judge(kin: true, enrolled: true, water: 0),
				"born True Kin is the truer thing to say than 'already on the rolls', and it is said first");
			Assert.AreEqual(KingdomEnrolVerdict.Enrolled, Judge(enrolled: true, water: 0),
				"a person already on the rolls is told so rather than told to fill the stores for a ceremony they do not need");
		}

		[Test]
		public void TheCeremonyIsOnceEverPerPerson()
		{
			// The whole of Addendum 22 A2's "once-ever ceremony" for this building: a person on
			// the rolls is refused, and refused by name rather than by silence.
			Assert.AreEqual(KingdomEnrolVerdict.Enrolled, Judge(enrolled: true));
			StringAssert.Contains("already on the rolls",
				KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Enrolled, "Vaan", "Sotham's Rest", 999));
		}

		[Test]
		public void ABirthTrueKinIsRefusedBecauseThereIsNothingToGiveThem()
		{
			Assert.AreEqual(KingdomEnrolVerdict.Kin, Judge(kin: true));
		}

		[Test]
		public void ExactlyTheCeremonysWaterIsEnoughAndOneDramLessIsNot()
		{
			// The boundary, because an off-by-one here is a founder standing at a full store being
			// told to fill it.
			Assert.AreEqual(KingdomEnrolVerdict.Allowed, Judge(water: KingdomAnnexeRules.EnrolmentDrams));
			Assert.AreEqual(KingdomEnrolVerdict.Unpaid, Judge(water: KingdomAnnexeRules.EnrolmentDrams - 1));
		}

		// --- Megastructure cardinality is the LAB'S, consumed rather than forked --------------------

		[Test]
		public void ACityThatAlreadyKeepsTheTheatreMayNotRaiseTheAnnexe()
		{
			// Addendum 22 A1, Design B: a chrome-city and a flesh-city are one doctrine's two
			// answers, and the same city never stacks both.
			Assert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: KingdomLabRules.TheatreKey, Key: KingdomAnnexeRules.AnnexeKey));
			Assert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: KingdomAnnexeRules.AnnexeKey, Key: KingdomLabRules.TheatreKey));
		}

		[Test]
		public void ACityWithNoPurposeYetMayRaiseTheAnnexeAndRekeyingItIsNotASecondPurpose()
		{
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: null, Key: KingdomAnnexeRules.AnnexeKey));
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: KingdomAnnexeRules.AnnexeKey, Key: KingdomAnnexeRules.AnnexeKey));
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: "BecomingAnnexe", Key: KingdomAnnexeRules.AnnexeKey),
				"the kept key is matched case-insensitively, as the registry writes it");
		}

		[Test]
		public void TheRefusalNamesTheBuildingInTheWayRatherThanTheRule()
		{
			string line = KingdomLabRules.PurposeRefusalLine("the chimeric theatre");
			StringAssert.Contains("the chimeric theatre", line);
			Assert.IsFalse(line.Contains("megastructure"),
				"7b: a founder told a rule has learned a rule; one told what is standing in the way has learned what to do");
		}

		// --- The rolls ride the container (Addendum 22 B1/B6) ---------------------------------------

		[Test]
		public void SecessionTakesTheRollsWithTheLeaver()
		{
			// The fiction's teeth, and they are not a feature of ours: the leaving city's whole
			// container moves, and nothing about enrolment appears in either step.
			KingdomSettlement seat = Chrome();
			KingdomSettlement seceded = new KingdomSettlement();
			seceded.ReadFrom(seat);
			new KingdomSettlement().WriteTo(seat);

			Assert.IsTrue(KingdomAnnexeRules.Enrolled(Decoded(seceded.KeepersRoster), Founder));
			Assert.IsFalse(KingdomAnnexeRules.Enrolled(Decoded(seat.KeepersRoster), Founder),
				"the realm no longer keeps a book it no longer has the city for");
		}

		[Test]
		public void RejoinRestoresTheRollsWholeAndFree()
		{
			// B6, and the §1.5 promise underneath it: coming back costs nothing and re-enrols
			// nobody, because the book was never rewritten.
			KingdomSettlement seat = Chrome();
			string before = seat.KeepersRoster;
			KingdomSettlement seceded = new KingdomSettlement();
			seceded.ReadFrom(seat);
			new KingdomSettlement().WriteTo(seat);

			seceded.WriteTo(seat);

			Assert.AreEqual(before, seat.KeepersRoster);
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(Decoded(seat.KeepersRoster), Founder));
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(Decoded(seat.KeepersRoster), Citizen));
		}

		[Test]
		public void ThreeQuarrelsAndThreeReconciliationsCostTheRollsNothing()
		{
			KingdomSettlement seat = Chrome();
			string before = seat.KeepersRoster;
			for (int i = 0; i < 3; i++)
			{
				KingdomSettlement away = new KingdomSettlement();
				away.ReadFrom(seat);
				new KingdomSettlement().WriteTo(seat);
				away.WriteTo(seat);
			}
			Assert.AreEqual(before, seat.KeepersRoster);
		}

		[Test]
		public void ExileTakesTheRollsWithTheRealmAndLeavesTheFounderOffThem()
		{
			KingdomSettlement seat = Chrome();
			KingdomSettlement exiled = new KingdomSettlement();
			exiled.ReadFrom(seat);
			new KingdomSettlement().WriteTo(seat);

			Assert.IsFalse(KingdomAnnexeRules.Enrolled(Decoded(seat.KeepersRoster), Founder));
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(Decoded(exiled.KeepersRoster), Founder));
		}

		[Test]
		public void RefoundingAfterExileStartsFromNobodyEnrolled()
		{
			// "Doors, never rooms" (B3): a blank city counts nobody, and a founder who walks away
			// from their realm walks away from the claim it made about them.
			KingdomSettlement refounded = new KingdomSettlement();
			Assert.AreEqual(0, KingdomAnnexeRules.Rolls(Decoded(refounded.KeepersRoster)).Count);
		}

		[Test]
		public void TheOtherCitysBookIsADifferentBook()
		{
			// Two cities, two registers, and enrolment at one is not enrolment at the other. What
			// makes a founder still count is the REALM holding one of the books, which is a read
			// over both and lives in KingdomAnnexe.HeldBy.
			KingdomSettlement seat = Chrome();
			KingdomSettlement away = new KingdomSettlement();
			away.SettlementName = "Kavvat";
			away.KeepersRoster = Rolls(Stranger);
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(Decoded(seat.KeepersRoster), Founder));
			Assert.IsFalse(KingdomAnnexeRules.Enrolled(Decoded(away.KeepersRoster), Founder));
			Assert.IsTrue(KingdomAnnexeRules.Enrolled(Decoded(away.KeepersRoster), Stranger));
		}

		// --- The price (R4: cost, never refusal) ----------------------------------------------------

		[Test]
		public void TheCeremonyCostsStandingWithExactlyThePeopleItOffends()
		{
			List<KeyValuePair<string, int>> cost = KingdomAnnexeRules.StandingCost();
			Assert.AreEqual(1, cost.Count);
			Assert.AreEqual("Templar", cost[0].Key);
			Assert.AreEqual(-KingdomAnnexeRules.StandingPerCreed, cost[0].Value);
			Assert.Less(cost[0].Value, 0, "a cost is a cost; a positive delta here would be a reward");
		}

		[Test]
		public void TheCeremonyIsPricedAtTheTopOfTheLabsOwnBandAndNotBelowIt()
		{
			// §1.7 R-D: chrome exclusivity is the last standing reason to pick True Kin, so a rung
			// of the annexe priced cheap deletes a genotype. This is the assertion that notices
			// somebody quietly lowering it.
			Assert.GreaterOrEqual(KingdomAnnexeRules.EnrolmentDrams, 180);
			Assert.Greater(KingdomAnnexeRules.StandingPerCreed, KingdomLabRules.StandingPerCreed);
		}

		[Test]
		public void TheCeremonyGrantsTheGenotypesOwnShareOfLicensesAndNoCastesShare()
		{
			// Genotypes.xml:20 gives True Kin CyberneticsLicensePoints="2"; a caste adds its own on
			// top. A city can put you on the rolls; it cannot make you an aristocrat.
			Assert.AreEqual(2, KingdomAnnexeRules.EnrolmentLicenses);
			Assert.Greater(KingdomAnnexeRules.EnrolmentLicenses, 0,
				"the event opens the door and the licenses are the room: zero here is an open door onto nothing");
		}

		[Test]
		public void ThePriceLineStatesTheWholeCostInOneSentence()
		{
			string line = KingdomAnnexeRules.PriceLine();
			StringAssert.Contains(KingdomAnnexeRules.EnrolmentDrams.ToString(), line);
			StringAssert.Contains("drams", line);
			StringAssert.Contains("Templar", line);
			StringAssert.Contains((-KingdomAnnexeRules.StandingPerCreed).ToString(), line);
		}

		// --- STANDARDS 7b: nothing stalls in silence -------------------------------------------------

		[Test]
		public void EveryRefusalSaysSomethingAndAllowedSaysNothing()
		{
			Assert.IsNull(KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Allowed, "Vaan", "Sotham's Rest", 999),
				"7b forbids telling somebody about the absence of a problem");
			foreach (KingdomEnrolVerdict verdict in System.Enum.GetValues(typeof(KingdomEnrolVerdict)))
			{
				if (verdict == KingdomEnrolVerdict.Allowed)
				{
					continue;
				}
				string line = KingdomAnnexeRules.RefusalLine(verdict, "Vaan", "Sotham's Rest", 12);
				Assert.IsFalse(string.IsNullOrEmpty(line), verdict + " refuses in silence");
				Assert.Greater(line.Length, 40, verdict + " refuses without saying what would fix it");
			}
		}

		[Test]
		public void TheUnpaidRefusalNamesBothWhatIsThereAndWhatIsWanted()
		{
			string line = KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Unpaid, "Vaan", "Sotham's Rest", 12);
			StringAssert.Contains("12", line);
			StringAssert.Contains(KingdomAnnexeRules.EnrolmentDrams.ToString(), line);
			StringAssert.Contains("Sotham's Rest", line);
		}

		[Test]
		public void AnUnstaffedAnnexeSaysSoRatherThanWritingNothingQuietly()
		{
			StringAssert.Contains("nobody", KingdomAnnexeRules.RefusalLine(KingdomEnrolVerdict.Unstaffed, "Vaan", "Sotham's Rest", 999).ToLowerInvariant());
			StringAssert.Contains("Nobody is at the register", KingdomAnnexeRules.RegisterIntro(null, 0));
			Assert.IsFalse(KingdomAnnexeRules.RegisterIntro("Vaan", 3).Contains("Nobody is at the register"));
		}

		[Test]
		public void ARefusalWithNoNameToUseStillReadsAsASentence()
		{
			// Every one of these degrades rather than printing an empty hole, because a founder
			// reading "  is already on the rolls" has been handed a bug.
			foreach (KingdomEnrolVerdict verdict in System.Enum.GetValues(typeof(KingdomEnrolVerdict)))
			{
				if (verdict == KingdomEnrolVerdict.Allowed)
				{
					continue;
				}
				string line = KingdomAnnexeRules.RefusalLine(verdict, null, null, 0);
				Assert.IsFalse(string.IsNullOrEmpty(line));
				Assert.IsFalse(line.Contains("  is"), verdict + " leaves a hole where a name should be");
			}
		}

		// --- Disclosure: the whole cost, before consent ----------------------------------------------

		[Test]
		public void TheDisclosureStatesThePriceTheMeaningTheReachAndTheLapse()
		{
			string text = KingdomAnnexeRules.DisclosureLines("Sotham's Rest");
			// The price.
			StringAssert.Contains(KingdomAnnexeRules.EnrolmentDrams.ToString(), text);
			StringAssert.Contains("Templar", text);
			// What a roll IS, and the reward that pre-exists the building (F2, the unspendable wedge).
			StringAssert.Contains("becoming nook", text);
			StringAssert.Contains("water ritual", text);
			// The reach past the nook -- the finding nothing else in the game would ever tell them.
			StringAssert.Contains("Tonics", text);
			// What would take it away, and the promise that nothing is taken OUT of them (§1.5).
			StringAssert.Contains("walks out", text);
			StringAssert.Contains("Nothing already fitted to you is touched", text);
		}

		[Test]
		public void EveryDisclosedLineIsMarkedAsAConsequence()
		{
			string[] lines = KingdomAnnexeRules.DisclosureLines("Sotham's Rest").Split('\n');
			Assert.AreEqual(4, lines.Length);
			for (int i = 0; i < lines.Length; i++)
			{
				StringAssert.StartsWith(KingdomAnnexeRules.EffectPrefix, lines[i]);
			}
		}

		[Test]
		public void ConsentIsTwoAnswersAndTheFirstIsTheOneThatActs()
		{
			Assert.AreEqual(2, KingdomAnnexeRules.ConsentOptions.Length);
			StringAssert.Contains("rolls", KingdomAnnexeRules.ConsentOptions[0]);
			StringAssert.Contains("Not", KingdomAnnexeRules.ConsentOptions[1]);
		}

		// --- The lapse: the one sentence a founder whose nooks closed is owed --------------------------

		[Test]
		public void TheLapseNamesTheCityAndPromisesNothingWasTakenOut()
		{
			// The §1.5 failure mode, avoided out loud: the thing players will not forgive is a
			// permanent consequence attached to a reversible act. Losing the rolls closes a door;
			// it never reaches into a body.
			string line = KingdomAnnexeRules.LapseLine("Sotham's Rest");
			StringAssert.Contains("Sotham's Rest", line);
			StringAssert.Contains("Nothing was taken out", line);
			StringAssert.Contains("stays fitted", line);
		}

		[Test]
		public void TheLapseAndTheEnrolmentBothLeaveAStoryBehind()
		{
			StringAssert.Contains("Sotham's Rest", KingdomAnnexeRules.LapseTelling("Sotham's Rest"));
			StringAssert.Contains("Vaan", KingdomAnnexeRules.DoneTelling("Vaan", "Sotham's Rest"));
			StringAssert.Contains("Sotham's Rest", KingdomAnnexeRules.DoneTelling("Vaan", "Sotham's Rest"));
			StringAssert.Contains("Vaan", KingdomAnnexeRules.DoneLine("Vaan", "Sotham's Rest"));
		}

		// --- The register screen ------------------------------------------------------------------------

		[Test]
		public void TheRegisterSaysWhoKeepsTheBookAndHowManyNamesAreInIt()
		{
			string intro = KingdomAnnexeRules.RegisterIntro("Vaan", 3);
			StringAssert.Contains("Vaan", intro);
			StringAssert.Contains("3", intro);
			StringAssert.Contains(KingdomAnnexeRules.Charter, intro);
			StringAssert.Contains("none", KingdomAnnexeRules.RegisterIntro("Vaan", 0));
		}

		[Test]
		public void TheRegistersHeadingAndItsCharterAreTheBuildingsOwnWords()
		{
			StringAssert.Contains("Sotham's Rest", KingdomAnnexeRules.RegisterTitle("Sotham's Rest"));
			StringAssert.Contains("becoming nook", KingdomAnnexeRules.Charter);
			StringAssert.Contains("its own", KingdomAnnexeRules.Charter);
		}

		[Test]
		public void ARowSaysWhetherTheBookStillHoldsThem()
		{
			StringAssert.Contains("Vaan", KingdomAnnexeRules.RegisterRow("Vaan", Held: true));
			StringAssert.Contains("on the rolls", KingdomAnnexeRules.RegisterRow("Vaan", Held: true));
			StringAssert.Contains("gone", KingdomAnnexeRules.RegisterRow("Vaan", Held: false));
		}

		[TestCase(0, "keeps no rolls")]
		[TestCase(1, "1")]
		[TestCase(4, "4")]
		public void TheCitysBookSaysWhatItsRollsAmountTo(int count, string expected)
		{
			StringAssert.Contains(expected, KingdomAnnexeRules.RollsLine(count));
		}

		[Test]
		public void OneNameReadsAsOneNameRatherThanOneNames()
		{
			StringAssert.Contains("is {{C|1}} name", KingdomAnnexeRules.RollsLine(1));
			StringAssert.Contains("are {{C|2}} names", KingdomAnnexeRules.RollsLine(2));
		}

		// --- F4: the debt, and who comes to collect ------------------------------------------------------

		[Test]
		public void TheCreditorsAreTheCreedThatActuallyHoldsChromeAsADebt()
		{
			Assert.AreEqual("Mechanimists", KingdomAnnexeRules.Creditors);
			Assert.AreNotEqual(KingdomAnnexeRules.Creditors, "Templar",
				"the people the ceremony offends and the people it owes are not the same people");
		}

		[Test]
		public void ThePetitionRoundTripsSubjectSpeechAndDeed()
		{
			string subject = KingdomAnnexeRules.SpokenAboutSubject();
			string speech = KingdomAnnexeRules.SpokenAboutSpeech(KingdomAnnexeRules.Creditors);
			string deed = KingdomAnnexeRules.SpokenAboutDeed("Sotham's Rest");
			Assert.IsFalse(string.IsNullOrEmpty(subject));
			StringAssert.Contains("chrome", subject);
			StringAssert.Contains(KingdomAnnexeRules.Creditors, speech);
			StringAssert.StartsWith("\"", speech);
			StringAssert.EndsWith("\"", speech);
			StringAssert.Contains("Sotham's Rest", deed);
		}

		[Test]
		public void ThePetitionerSpeaksWithoutACreedNameRatherThanWithAHole()
		{
			string speech = KingdomAnnexeRules.SpokenAboutSpeech(null);
			StringAssert.Contains("my people", speech);
			Assert.IsFalse(speech.Contains("  "), "a missing creed leaves a gap the founder can see");
		}

		[Test]
		public void TheDebtIsSpokenAboutByAMinorityOnceAndNeverByAMajority()
		{
			// Consumed rather than forked: the arithmetic is the lab's, so the two body-buildings
			// cannot drift into two different ideas of what a petition threshold is.
			Assert.IsTrue(KingdomLabRules.SpeaksAgainstHall(Offended: 2, People: 20, AlreadySpoken: false));
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 1, People: 20, AlreadySpoken: false),
				"below a tenth is one person's objection, which is a conversation");
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 12, People: 20, AlreadySpoken: false),
				"a city where the creed is dominant never gets this petition");
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 2, People: 20, AlreadySpoken: true),
				"once is the whole of it");
			Assert.IsFalse(KingdomLabRules.SpeaksAgainstHall(Offended: 0, People: 0, AlreadySpoken: false));
		}

		// --- Fixtures ---------------------------------------------------------------------------------

		private static KingdomSettlement Chrome()
		{
			KingdomSettlement city = new KingdomSettlement();
			city.SettlementName = "Sotham's Rest";
			city.KeepersRoster = "node:arclight|" + Rolls(Founder, Citizen);
			return city;
		}
	}
}
#endif
