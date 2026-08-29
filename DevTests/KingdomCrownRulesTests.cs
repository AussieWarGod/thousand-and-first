#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The crown, the capital it makes, and the two cardinality lanes that meet at it.
	/// <para>
	/// The claim under every case here is Addendum 22 A4's: the capital is a fact about a BUILDING
	/// the founder raised, never about which city the founder is standing in. So the one thing this
	/// file never does is pass a seat, an Away record, or anything else that exchanges when a
	/// founder walks through a door &mdash; END-STATE-CITIES-RESEARCH &sect;5.1's collision, held
	/// as a property of the rules rather than as a comment.
	/// </para>
	/// </summary>
	public class KingdomCrownRulesTests
	{
		[Test]
		public void CrownVerdictKeepsByteAbiAndExactValues()
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomCrownVerdict)));
			CollectionAssert.AreEqual(new[] { "Crowns", "Moves", "AlreadyHere",
				"RefusedUnfounded", "RefusedNotOurGround", "RefusedNotOurWork", "RefusedNamed" },
				Enum.GetNames(typeof(KingdomCrownVerdict)));
			CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, Array.ConvertAll(
				(KingdomCrownVerdict[])Enum.GetValues(typeof(KingdomCrownVerdict)),
				value => (byte)value));
		}

		private const string Kavvat = "Kavvat";

		private const string Ozym = "Ozymandia";

		private const string Sheba = "Sheba Hagadias";

		private static List<string> Cities(params string[] names)
		{
			return new List<string>(names);
		}

		// --- The record: written, read back, and refused when it could not be written -------------

		[Test]
		public void TheRecordRoundTripsACityAndItsHall()
		{
			string text = KingdomCrownRules.FormatCrown(Kavvat, "r_TAF_Crown_JoppaWorld.11.22.1.1.10_20,10");
			string city;
			string key;
			Assert.IsTrue(KingdomCrownRules.TryParseCrown(text, out city, out key));
			Assert.AreEqual(Kavvat, city);
			Assert.AreEqual("r_TAF_Crown_JoppaWorld.11.22.1.1.10_20,10", key);
		}

		[Test]
		public void AnEmptyRecordIsARealmWithNoCapitalRatherThanADamagedOne()
		{
			// The ordinary state of every realm until the first crown hall goes up, so it must not
			// read as a repair and must not say anything to anybody.
			string city;
			string key;
			Assert.IsTrue(KingdomCrownRules.TryParseCrown(null, out city, out key));
			Assert.IsNull(city);
			Assert.IsTrue(KingdomCrownRules.TryParseCrown("", out city, out key));
			Assert.IsNull(city);
		}

		[TestCase("^")]
		[TestCase("^Kavvat")]
		[TestCase("Kavvat^key^extra")]
		public void ARecordThatCannotBeReadIsReportedRatherThanGuessedAt(string text)
		{
			string city;
			string key;
			Assert.IsFalse(KingdomCrownRules.TryParseCrown(text, out city, out key));
		}

		[TestCase(null, false)]
		[TestCase("", false)]
		[TestCase("Kav^vat", false)]
		[TestCase("Kavvat", true)]
		[TestCase("Sheba Hagadias", true)]
		public void ANameTheRecordCouldNotCarryIsRefusedRatherThanEscaped(string city, bool storable)
		{
			Assert.AreEqual(storable, KingdomCrownRules.Storable(city));
			string written = KingdomCrownRules.FormatCrown(city, "");
			string read;
			string key;
			Assert.IsTrue(KingdomCrownRules.TryParseCrown(written, out read, out key));
			Assert.AreEqual(storable ? city : null, read,
				"a name the record could not carry writes nothing at all rather than half of one");
		}

		[Test]
		public void TheHallsGroundNamesItAndNamesItTheSameWayTwice()
		{
			Assert.AreEqual(
				KingdomCrownRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 20, 10),
				KingdomCrownRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 20, 10));
			Assert.AreNotEqual(
				KingdomCrownRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 20, 10),
				KingdomCrownRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 10, 20));
			Assert.IsNull(KingdomCrownRules.ComposeLocationKey("Joppa^World.1.1.1.1.10", 1, 1));
			Assert.IsNull(KingdomCrownRules.ComposeLocationKey(null, 1, 1));
			Assert.IsNull(KingdomCrownRules.ComposeLocationKey("JoppaWorld.1.1.1.1.10", -1, 1));
		}

		// --- Resolve: the world outranks the record ----------------------------------------------

		[Test]
		public void TheRecordIsBelievedWhileTheHallItNamesIsStanding()
		{
			string capital;
			Assert.IsTrue(KingdomCrownRules.Resolve(Kavvat, Cities(Kavvat, Ozym), out capital));
			Assert.AreEqual(Kavvat, capital);
		}

		[Test]
		public void TheRecordIsBelievedEvenWhenItIsNotTheFirstCityByName()
		{
			// The tie-break must never overrule a record that is still true, or moving the crown to
			// a city late in the alphabet would silently undo itself.
			string capital;
			Assert.IsTrue(KingdomCrownRules.Resolve(Ozym, Cities(Kavvat, Ozym), out capital));
			Assert.AreEqual(Ozym, capital);
		}

		[Test]
		public void ACityIsMatchedTheWayAFounderReadsIt()
		{
			string capital;
			Assert.IsTrue(KingdomCrownRules.Resolve("KAVVAT", Cities(Kavvat), out capital));
			Assert.AreEqual(Kavvat, capital, "the answer is spelled the way the city is, not the way the record was");
		}

		[Test]
		public void StrikingTheCrownHallDownLeavesTheRealmWithNoCapitalAndSaysSo()
		{
			// The protection law never stops a founder taking apart a thing they built, so this is
			// a state the realm has to be able to be in, and it must not go on naming a capital
			// with nothing in it.
			string capital;
			Assert.IsFalse(KingdomCrownRules.Resolve(Kavvat, Cities(), out capital),
				"a record naming a city with no hall must be repaired, and repairs are told");
			Assert.IsNull(capital);
		}

		[Test]
		public void ARealmThatNeverHadACapitalIsNotARepair()
		{
			string capital;
			Assert.IsTrue(KingdomCrownRules.Resolve(null, Cities(), out capital));
			Assert.IsNull(capital);
			Assert.IsTrue(KingdomCrownRules.Resolve("", null, out capital));
			Assert.IsNull(capital);
		}

		[Test]
		public void WhenTheRecordIsNoHelpTheStandingHallsDecideAndTheAnswerDoesNotFlicker()
		{
			// §5.1's whole warning, held as a property: the answer must depend only on the cities
			// keeping halls, never on the order they were handed over or on who is standing where.
			string first;
			string second;
			Assert.IsFalse(KingdomCrownRules.Resolve(null, Cities(Kavvat, Ozym, Sheba), out first));
			Assert.IsFalse(KingdomCrownRules.Resolve("a city that seceded", Cities(Kavvat, Ozym, Sheba), out second));
			Assert.AreEqual(first, second);
			Assert.AreEqual(Kavvat, first, "the first in the order the caller passed, which is name order");
		}

		// --- JudgeTakeUp: refusals first, then the two events -------------------------------------

		[Test]
		public void TheCrownIsRefusedBeforeThereIsARealmToBeTheCapitalOf()
		{
			Assert.AreEqual(KingdomCrownVerdict.RefusedUnfounded,
				KingdomCrownRules.JudgeTakeUp(Founded: false, OurGround: true, OurWork: true, Crowned: null, Here: Kavvat));
		}

		[Test]
		public void TheCrownIsRefusedOnGroundTheRealmDoesNotHoldAndOnWorkItDidNotRaise()
		{
			Assert.AreEqual(KingdomCrownVerdict.RefusedNotOurGround,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: false, OurWork: true, Crowned: null, Here: Kavvat));
			Assert.AreEqual(KingdomCrownVerdict.RefusedNotOurWork,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: false, Crowned: null, Here: Kavvat));
		}

		[Test]
		public void ACityTheRecordCouldNotCarryIsRefusedRatherThanCrowned()
		{
			Assert.AreEqual(KingdomCrownVerdict.RefusedNamed,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: true, Crowned: null, Here: "Kav^vat"));
			Assert.AreEqual(KingdomCrownVerdict.RefusedNamed,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: true, Crowned: null, Here: ""));
		}

		[Test]
		public void RaisingTheFirstCrownHallCrownsTheCityAndRaisingASecondMovesTheCrown()
		{
			Assert.AreEqual(KingdomCrownVerdict.Crowns,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: true, Crowned: null, Here: Kavvat));
			Assert.AreEqual(KingdomCrownVerdict.Moves,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: true, Crowned: Kavvat, Here: Ozym));
		}

		[Test]
		public void SettingTheCrownDownWhereItAlreadyIsIsNotAQuestionWorthAsking()
		{
			Assert.AreEqual(KingdomCrownVerdict.AlreadyHere,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: true, Crowned: Kavvat, Here: Kavvat));
			Assert.AreEqual(KingdomCrownVerdict.AlreadyHere,
				KingdomCrownRules.JudgeTakeUp(Founded: true, OurGround: true, OurWork: true, Crowned: "KAVVAT", Here: Kavvat));
		}

		// --- The prose contracts (STANDARDS 7b) ---------------------------------------------------

		[Test]
		public void NothingInThisLaneIsEverCalledTheSeat()
		{
			// §5.1, held as a fact about the shipped strings: the seat is the settlement the founder
			// is standing in and it exchanges on TrySeat. A capital that used that word would teach
			// the founder the wrong lifetime for it.
			string[] said = new string[]
			{
				KingdomCrownRules.CrownPrompt(Kavvat),
				KingdomCrownRules.MovePrompt(Kavvat, Ozym),
				KingdomCrownRules.CrownedLine(Kavvat),
				KingdomCrownRules.MovedLine(Kavvat, Ozym),
				KingdomCrownRules.FormerCrownLine(Kavvat),
				KingdomCrownRules.StruckLine(Kavvat),
				KingdomCrownRules.RepairedLine(Kavvat),
				KingdomCrownRules.AlreadyHereLine(Kavvat),
				KingdomCrownRules.DescriptionLine(true, Kavvat),
				KingdomCrownRules.DescriptionLine(false, Kavvat),
				KingdomCrownRules.CapitalLine(false, Kavvat),
				KingdomCrownRules.RefusalLine(KingdomCrownVerdict.RefusedNotOurGround)
			};
			for (int i = 0; i < said.Length; i++)
			{
				Assert.IsFalse(said[i].ToLowerInvariant().Contains("seat"), said[i]);
			}
		}

		[Test]
		public void TheMovePromptDisclosesTheReKeyBeforeAnythingIsCommitted()
		{
			// §1.5's lesson and the whole cost of a move: the second hall is already built by the
			// time this is read, and the crossings are what is still unspent.
			string prompt = KingdomCrownRules.MovePrompt(Kavvat, Ozym);
			StringAssert.Contains(Kavvat, prompt);
			StringAssert.Contains(Ozym, prompt);
			StringAssert.Contains("re-keyed", prompt);
			Assert.IsTrue(prompt.Contains("arch"), "a founder with a crossing must hear about it before they answer");
		}

		[Test]
		public void TheOldHallIsDesignatedRatherThanDestroyedAndTheSentenceSaysSo()
		{
			// The protection law, in the one place this wave could have broken it.
			string line = KingdomCrownRules.FormerCrownLine(Kavvat);
			StringAssert.Contains(Kavvat, line);
			Assert.IsTrue(line.Contains("stands"), line);
			Assert.IsFalse(line.ToLowerInvariant().Contains("destroy"), line);
			Assert.IsFalse(KingdomCrownRules.MovePrompt(Kavvat, Ozym).ToLowerInvariant().Contains("torn down"));
			StringAssert.Contains("nothing is taken down", KingdomCrownRules.MovePrompt(Kavvat, Ozym));
		}

		[Test]
		public void EveryRefusalNamesTheActThatLiftsItAndNoneOfThemSaysThatFailed()
		{
			KingdomCrownVerdict[] refusals = new KingdomCrownVerdict[]
			{
				KingdomCrownVerdict.RefusedUnfounded,
				KingdomCrownVerdict.RefusedNotOurGround,
				KingdomCrownVerdict.RefusedNotOurWork,
				KingdomCrownVerdict.RefusedNamed
			};
			for (int i = 0; i < refusals.Length; i++)
			{
				string line = KingdomCrownRules.RefusalLine(refusals[i]);
				Assert.IsTrue(line.Length > 0, refusals[i].ToString());
				Assert.IsFalse(line.Contains("failed"), line);
			}
			// 7b forbids telling somebody about the absence of a problem.
			Assert.AreEqual("", KingdomCrownRules.RefusalLine(KingdomCrownVerdict.Crowns));
			Assert.AreEqual("", KingdomCrownRules.RefusalLine(KingdomCrownVerdict.Moves));
			Assert.AreEqual("", KingdomCrownRules.RefusalLine(KingdomCrownVerdict.AlreadyHere));
		}

		[Test]
		public void TheHallReadsItsOwnStateThreeWaysAndNeverTheSameWayTwice()
		{
			string holds = KingdomCrownRules.DescriptionLine(Holds: true, Capital: Kavvat);
			string empty = KingdomCrownRules.DescriptionLine(Holds: false, Capital: null);
			string former = KingdomCrownRules.DescriptionLine(Holds: false, Capital: Ozym);
			Assert.AreNotEqual(holds, empty);
			Assert.AreNotEqual(empty, former);
			StringAssert.Contains("capital", holds);
			StringAssert.Contains("former", former);
			StringAssert.Contains(Ozym, former);
		}

		[Test]
		public void TheLabelSaysWhatPressingItWouldDo()
		{
			StringAssert.Contains("already here", KingdomCrownRules.TakeUpLabel(Holds: true, Capital: Kavvat));
			Assert.AreEqual("set the crown down here", KingdomCrownRules.TakeUpLabel(Holds: false, Capital: null));
			Assert.AreEqual("move the crown here", KingdomCrownRules.TakeUpLabel(Holds: false, Capital: Kavvat));
		}

		[Test]
		public void ACityNothingNamedIsStillSpokenOfHonestly()
		{
			Assert.AreEqual("the city", KingdomCrownRules.Named(null));
			Assert.AreEqual("the city", KingdomCrownRules.Named(""));
			Assert.AreEqual(Kavvat, KingdomCrownRules.Named("  Kavvat  "));
		}

		// --- The capital gate: the second cardinality lane -----------------------------------------

		[Test]
		public void ACapitalSpecificDesignWantsTheCrownAndNothingElse()
		{
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: false, CapitalOnly: true, Crowned: true, Kept: null, Key: "arcology"));
			Assert.AreEqual(KingdomPurposeVerdict.RefusedUncrowned,
				KingdomLabRules.JudgePurpose(Megastructure: false, CapitalOnly: true, Crowned: false, Kept: null, Key: "arcology"));
		}

		[Test]
		public void TheCapitalsExtrasDoNotEatTheCapitalsOnePurpose()
		{
			// The capital ruling's exact words -- "a couple of EXTRA capital-specific megastructures
			// BEYOND its one" -- which only means anything if the extras never contend for the slot.
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: false, CapitalOnly: true, Crowned: true,
					Kept: KingdomLabRules.TheatreKey, Key: "arcology"),
				"a crowned flesh-city may still raise the arcology");
			// The precedence stated on JudgePurpose, in the case that actually tests it: a record
			// declaring BOTH attributes is judged against the crown and never against the slot. A
			// gate that asked the slot afterwards would refuse the capital its own extras the day a
			// modder wrote the arcology the way the fiction describes it.
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, CapitalOnly: true, Crowned: true,
					Kept: KingdomLabRules.TheatreKey, Key: "arcology"));
		}

		[Test]
		public void ACapitalStillMayNotStackBothBodyMegastructures()
		{
			// Addendum 22 A3, and the capital is judged for it exactly as every other city is: the
			// theatre and the annexe are megastructures and neither is capital-specific.
			Assert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, CapitalOnly: false, Crowned: true,
					Kept: KingdomLabRules.TheatreKey, Key: KingdomAnnexeRules.AnnexeKey));
			Assert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, CapitalOnly: false, Crowned: true,
					Kept: KingdomAnnexeRules.AnnexeKey, Key: KingdomLabRules.TheatreKey));
		}

		[Test]
		public void TheCrownGateFailsClosedAndThePurposeGateFailsOpen()
		{
			// Two unknowns, two directions, both deliberate. A purpose nothing could read must not
			// brick the catalogue; a crown nothing set down must not hand every realm the capital's.
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, CapitalOnly: false, Crowned: false, Kept: null, Key: "chimerictheatre"));
			Assert.AreEqual(KingdomPurposeVerdict.RefusedUncrowned,
				KingdomLabRules.JudgePurpose(Megastructure: true, CapitalOnly: true, Crowned: false, Kept: null, Key: "arcology"));
		}

		[Test]
		public void TheOldThreeArgumentGateStillAnswersExactlyAsItDid()
		{
			// The published surface is not allowed to move under a third party (STANDARDS §9).
			Assert.AreEqual(KingdomPurposeVerdict.RefusedKept,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: "arcology", Key: "chimerictheatre"));
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: true, Kept: null, Key: "chimerictheatre"));
			Assert.AreEqual(KingdomPurposeVerdict.Allowed,
				KingdomLabRules.JudgePurpose(Megastructure: false, Kept: "arcology", Key: "smithy"));
		}

		[TestCase("yes", true)]
		[TestCase("Yes", true)]
		[TestCase("true", true)]
		[TestCase("1", true)]
		[TestCase("no", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		[TestCase("maybe", false)]
		public void ADesignStandsInAnyCityUntilItSaysOtherwise(string declared, bool expected)
		{
			Assert.AreEqual(expected, KingdomLabRules.IsCapitalOnly(declared));
		}

		[Test]
		public void TheUncrownedRefusalNamesWhereTheCrownIsRatherThanTheRule()
		{
			string named = KingdomLabRules.UncrownedRefusalLine(Kavvat);
			StringAssert.Contains(Kavvat, named);
			Assert.IsFalse(named.Contains("Capital=\""), named);
			string none = KingdomLabRules.UncrownedRefusalLine(null);
			StringAssert.Contains("crown hall", none);
			Assert.AreNotEqual(named, none, "a realm with no capital is told a different thing to do");
		}

		// --- The two lanes end to end, through the real zoning path --------------------------------

		private static ZoneGate Gate(string key, string megastructure, string capital)
		{
			string error;
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes(key, null, null, null, null,
				null, null, null, null, megastructure, capital, out error);
			Assert.IsNull(error);
			return gate;
		}

		private static ZoningJudgement JudgeGround(ZoneGate gate, string key, string cityKeeps, bool crowned,
			string capitalName, KingdomSatelliteVerdict satellite, string satelliteDetail)
		{
			return KingdomZoningRules.Judge(gate, null, "craft", 0, null,
				Underground: false, RequiresSky: false, Roll: BuilderRoll.Unknown, Stratum: null,
				Key: key, CityKeeps: cityKeeps, Crowned: crowned, CapitalName: capitalName,
				Satellite: satellite, SatelliteDetail: satelliteDetail);
		}

		private static ZoningJudgement JudgeArcology(bool crowned, string capitalName, string cityKeeps)
		{
			return JudgeGround(Gate("arcology", null, "yes"), "arcology", cityKeeps, crowned, capitalName,
				KingdomSatelliteVerdict.Allowed, null);
		}

		[Test]
		public void ParseGateAttributes_ReadsCapitalAndLeavesEveryOtherDesignAlone()
		{
			Assert.IsTrue(Gate("arcology", null, "yes").Capital);
			Assert.IsFalse(Gate("arcology", null, "no").Capital);
			Assert.IsFalse(Gate("smithy", null, null).Capital);
			// A malformed value makes a design un-special rather than unbuildable, and reports no
			// fault -- the safe direction, and the same call the Megastructure attribute makes.
			string error;
			Assert.IsFalse(KingdomZoningRules.ParseGateAttributes("arcology", null, null, null, null,
				null, null, null, null, null, "perhaps", out error).Capital);
			Assert.IsNull(error);
		}

		[Test]
		public void Zoning_RefusesACapitalOnlyDesignInAnUncrownedRealm()
		{
			ZoningJudgement judgement = JudgeArcology(crowned: false, capitalName: null, cityKeeps: null);
			Assert.IsFalse(judgement.Permitted);
			Assert.AreEqual(ZoningVerdict.RefusedUncrowned, judgement.Verdict);
			Assert.IsNull(judgement.Detail, "a realm with no capital has no city to name");
			Assert.IsNotEmpty(judgement.Note);
		}

		[Test]
		public void Zoning_RefusesACapitalOnlyDesignInACityThatIsNotTheCapital()
		{
			ZoningJudgement judgement = JudgeArcology(crowned: false, capitalName: Kavvat, cityKeeps: null);
			Assert.AreEqual(ZoningVerdict.RefusedUncrowned, judgement.Verdict);
			Assert.AreEqual(Kavvat, judgement.Detail,
				"the Detail is already prose: a city's name is the founder's own word for it");
		}

		[Test]
		public void Zoning_AllowsTheArcologyOnlyWhereTheCrownStands()
		{
			Assert.IsTrue(JudgeArcology(crowned: true, capitalName: Kavvat, cityKeeps: null).Permitted);
			Assert.IsFalse(JudgeArcology(crowned: false, capitalName: Kavvat, cityKeeps: null).Permitted);
		}

		[Test]
		public void Zoning_TheCapitalsExtrasDoNotEatTheCapitalsOnePurposeEndToEnd()
		{
			// The crowned flesh-city raises its arcology with the theatre still standing. The whole
			// of the capital ruling's "its one PLUS extras", asserted through the real gate.
			Assert.IsTrue(JudgeArcology(crowned: true, capitalName: Kavvat, cityKeeps: KingdomLabRules.TheatreKey).Permitted);
			// And a record that declares BOTH attributes is still judged against the crown alone.
			Assert.IsTrue(JudgeGround(Gate("arcology", "yes", "yes"), "arcology", KingdomLabRules.TheatreKey,
				crowned: true, capitalName: Kavvat, satellite: KingdomSatelliteVerdict.Allowed, satelliteDetail: null).Permitted);
		}

		[Test]
		public void Zoning_A3TheTheatreCapitalRefusesTheAnnexeThroughThePURPOSEVerdict()
		{
			// Addendum 22 A3, pinned end to end AND pinned to the right lane: the capital is judged
			// for the body-megastructures exactly as any city is, so the founder is told which
			// building is in the way -- never that they are in the wrong city, which would be a
			// sentence they could act on and be wrong.
			ZoningJudgement judgement = JudgeGround(Gate(KingdomAnnexeRules.AnnexeKey, "yes", null),
				KingdomAnnexeRules.AnnexeKey, KingdomLabRules.TheatreKey,
				crowned: true, capitalName: Kavvat,
				satellite: KingdomSatelliteVerdict.Allowed, satelliteDetail: null);
			Assert.AreEqual(ZoningVerdict.RefusedMegastructure, judgement.Verdict);
			Assert.AreNotEqual(ZoningVerdict.RefusedUncrowned, judgement.Verdict);
			Assert.AreEqual(KingdomLabRules.TheatreKey, judgement.Detail);
			// And the other way round, in the same crowned city.
			Assert.AreEqual(ZoningVerdict.RefusedMegastructure,
				JudgeGround(Gate(KingdomLabRules.TheatreKey, "yes", null), KingdomLabRules.TheatreKey,
					KingdomAnnexeRules.AnnexeKey, crowned: true, capitalName: Kavvat,
					KingdomSatelliteVerdict.Allowed, null).Verdict);
		}

		[Test]
		public void Zoning_ThePurposeSlotStillFailsOpenAndTheCrownStillFailsClosed()
		{
			// Unchanged in the one direction and deliberate in the other. A purpose nothing could
			// read must not brick the catalogue; a crown nobody set down must not open it.
			Assert.IsTrue(JudgeGround(Gate(KingdomLabRules.TheatreKey, "yes", null), KingdomLabRules.TheatreKey,
				cityKeeps: null, crowned: false, capitalName: null,
				satellite: KingdomSatelliteVerdict.Allowed, satelliteDetail: null).Permitted);
			Assert.IsFalse(JudgeArcology(crowned: false, capitalName: null, cityKeeps: null).Permitted);
		}

		[Test]
		public void Zoning_TheOlderJudgeOverloadChainsFailClosedOnTheCrownAndChangesNothingElse()
		{
			// No design declared Capital the day before this landed, so every existing caller is
			// unmoved; the one that would move is a caller judging a capital-only design without
			// knowing capitals exist, and it must not be handed the capital's catalogue.
			Assert.IsTrue(KingdomZoningRules.Judge(Gate("smithy", null, null), null, "craft", 0, null,
				Underground: false, RequiresSky: false, Roll: BuilderRoll.Unknown, Stratum: null,
				Key: "smithy", CityKeeps: null).Permitted);
			Assert.AreEqual(ZoningVerdict.RefusedUncrowned,
				KingdomZoningRules.Judge(Gate("arcology", null, "yes"), null, "craft", 0, null,
					Underground: false, RequiresSky: false, Roll: BuilderRoll.Unknown, Stratum: null,
					Key: "arcology", CityKeeps: null).Verdict);
		}

		[Test]
		public void Zoning_RefusesAnOutpostWhoseGreatWorkStandsNowhereInTheRealm()
		{
			ZoningJudgement judgement = JudgeGround(Gate(KingdomSatelliteRules.RegistryOfficeKey, null, null),
				KingdomSatelliteRules.RegistryOfficeKey, cityKeeps: null, crowned: false, capitalName: null,
				satellite: KingdomSatelliteVerdict.RefusedNoParent, satelliteDetail: KingdomAnnexeRules.AnnexeKey);
			Assert.AreEqual(ZoningVerdict.RefusedSatellite, judgement.Verdict);
			Assert.AreEqual(KingdomAnnexeRules.AnnexeKey, judgement.Detail,
				"the Detail is the PARENT's key, so the refusal can name the great work that is missing");
		}

		[Test]
		public void Zoning_RefusesASecondOutpostOfTheSameGreatWorkInOneCity()
		{
			ZoningJudgement judgement = JudgeGround(Gate("theirmod_office", null, null),
				"theirmod_office", cityKeeps: null, crowned: false, capitalName: null,
				satellite: KingdomSatelliteVerdict.RefusedCityKeeps, satelliteDetail: KingdomSatelliteRules.RegistryOfficeKey);
			Assert.AreEqual(ZoningVerdict.RefusedSatelliteKept, judgement.Verdict);
			Assert.AreEqual(KingdomSatelliteRules.RegistryOfficeKey, judgement.Detail,
				"the Detail is the KEPT outpost's key, which is a different key from the one above");
		}

		[Test]
		public void TheTwoOutpostRefusalsAreTwoVERDICTSBecauseTheirDetailsAreTwoDifferentThings()
		{
			// The ambiguity this wave was told to resolve, pinned. One verdict carrying both
			// meanings would leave the composer guessing which key its Detail held, and it would
			// guess wrong the first time somebody named an outpost after its parent.
			Assert.AreNotEqual(ZoningVerdict.RefusedSatellite, ZoningVerdict.RefusedSatelliteKept);
			ZoningJudgement noParent = JudgeGround(Gate("k", null, null), "k", null, false, null,
				KingdomSatelliteVerdict.RefusedNoParent, "becomingannexe");
			ZoningJudgement kept = JudgeGround(Gate("k", null, null), "k", null, false, null,
				KingdomSatelliteVerdict.RefusedCityKeeps, "becomingannexe");
			Assert.AreNotEqual(noParent.Verdict, kept.Verdict,
				"same Detail, two meanings, and the verdict is what tells them apart");
			Assert.AreNotEqual(noParent.Note, kept.Note);
		}

		[Test]
		public void Zoning_NeverStandsInTheWayOfADesignThatIsNeitherOfThese()
		{
			// Both lanes inert for every design in the catalogue that declares neither attribute,
			// which is what keeps two attributes and two checks the whole of the vocabulary.
			Assert.IsTrue(JudgeGround(Gate("smithy", null, null), "smithy", "arcology", crowned: false,
				capitalName: null, satellite: KingdomSatelliteVerdict.Allowed, satelliteDetail: null).Permitted);
		}

		[Test]
		public void Zoning_TheOutpostGateIsAskedWithTheTERRITORYGatesAndTheCrownGateLAST()
		{
			// The ordering is the ruling. A lack the founder can answer by walking or building is
			// told before one that is a decision they already made about another city; and among
			// the lacks, the older ones still come first.
			string error;
			ZoneGate reachable = KingdomZoningRules.ParseGateAttributes("registryoffice", null, "4", null, "Arclight",
				null, null, null, null, null, null, out error);
			Assert.IsNull(error);
			// Tech is a lack; the outpost gate is a lack; tech is the older and nearer one.
			Assert.AreEqual(ZoningVerdict.RefusedTechLevel,
				KingdomZoningRules.Judge(reachable, null, "craft", 0, null, false, false, BuilderRoll.Unknown, null,
					"registryoffice", null, false, null, KingdomSatelliteVerdict.RefusedNoParent, "becomingannexe").Verdict);
			// District is the LAST of the lacks and the outpost gate sits above it, because "raise
			// the annexe somewhere" is a bigger errand than "walk to the forgeworks".
			ZoneGate districted = KingdomZoningRules.ParseGateAttributes("registryoffice", "market", null, null, null,
				null, null, null, null, null, null, out error);
			Assert.IsNull(error);
			Assert.AreEqual(ZoningVerdict.RefusedSatellite,
				KingdomZoningRules.Judge(districted, null, "craft", 0, null, false, false, BuilderRoll.Unknown, null,
					"registryoffice", null, false, null, KingdomSatelliteVerdict.RefusedNoParent, "becomingannexe").Verdict);
			// And the crown gate is told after the district gate, which is the purpose gate's own
			// position and for the purpose gate's own reason.
			ZoneGate capitalOnly = KingdomZoningRules.ParseGateAttributes("arcology", "market", null, null, null,
				null, null, null, null, null, "yes", out error);
			Assert.IsNull(error);
			Assert.AreEqual(ZoningVerdict.RefusedDistrict,
				KingdomZoningRules.Judge(capitalOnly, null, "craft", 0, null, false, false, BuilderRoll.Unknown, null,
					"arcology", null, false, null, KingdomSatelliteVerdict.Allowed, null).Verdict);
		}

		[Test]
		public void TheShippedVerdictOrdinalsAreNeverRenumbered()
		{
			// STANDARDS §9: these ordinals are published and a third party may already switch on
			// them. Appending is additive; renumbering moves every value under somebody's feet.
			Assert.AreEqual(9, (int)ZoningVerdict.RefusedMegastructure);
			Assert.AreEqual(10, (int)ZoningVerdict.RefusedSatellite);
			Assert.AreEqual(11, (int)ZoningVerdict.RefusedSatelliteKept);
			Assert.AreEqual(12, (int)ZoningVerdict.RefusedUncrowned);
		}

		// --- Hosted arcology lots: private stratum, never surface plots ----------------------------

		[Test]
		public void HostedArcologyLotsNeverLeakOntoSurfaceGround()
		{
			const string Interior = "arcology";
			Assert.AreEqual(KingdomZoningRules.StratumArcology, KingdomZoningRules.HomeStratum(Interior));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits(Interior, KingdomZoningRules.StratumSurface),
				"hosted lots are commissioned through the exact shell, never a surface offer");
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(Interior, KingdomZoningRules.StratumArcology),
				"and it moves indoors without a schema change the day the stratum exists");
			Assert.IsFalse(KingdomZoningRules.StrataAdmits(Interior, KingdomZoningRules.StratumDeep),
				"the arcology set is not the deep set (Addendum 15's first ruling)");
		}

		[Test]
		public void TheArcologyItselfFillsTheWholeOfTheLargestPlotAndNoMore()
		{
			// §5.5's capital-clutter warning wanted the arcology to have a footprint of its own so
			// it never crowds the capital's ordinary economy. XL is as far as the shipped plot
			// vocabulary reaches, so the arcology takes ALL of it -- and the zone-spanning the
			// research describes is a carrier this wave deliberately did not build.
			Assert.IsTrue(KingdomPlotRules.FootprintFits(KingdomPlotRules.PlotSize.Huge, 20, 14));
			Assert.IsFalse(KingdomPlotRules.FootprintFits(KingdomPlotRules.PlotSize.Huge, 24, 16),
				"a record wanting more ground than this needs machinery that does not exist yet");
		}

		// --- The hub re-key: QUESTION-BACKLOG QB-1, cashed in ---------------------------------------

		private const string KeyA = "r_TAF_MirrorGate_JoppaWorld.11.22.1.1.10_20,10";

		private const string KeyB = "r_TAF_MirrorGate_JoppaWorld.14.19.2.0.10_5,7";

		private const string KeyC = "r_TAF_MirrorGate_JoppaWorld.09.31.0.2.10_31,4";

		private static KingdomGateRow[] Register(params KingdomGateRow[] rows)
		{
			return rows;
		}

		[Test]
		public void TheHubPointsEveryOtherArchAtTheCapitalAndTheCapitalBack()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, KeyB),
				new KingdomGateRow(KeyB, Ozym, KeyA),
				new KingdomGateRow(KeyC, Sheba, ""));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			Assert.AreEqual(KingdomGateVerdict.Joined, KingdomMirrorGateRules.TryHub(rows, Sheba, out next, out rekeyed, out hub));
			Assert.AreEqual(KeyC, hub);
			Assert.AreEqual(KeyC, KingdomMirrorGateRules.PartnerOf(next, KeyA));
			Assert.AreEqual(KeyC, KingdomMirrorGateRules.PartnerOf(next, KeyB));
			Assert.AreEqual(KeyA, KingdomMirrorGateRules.PartnerOf(next, KeyC),
				"the hub answers the first spoke in register order, deterministically and without a draw");
			Assert.AreEqual(3, rekeyed);
		}

		[Test]
		public void NoRowIsLostAndNoRowIsAddedAndNothingButThePartnerColumnMoves()
		{
			// The invariant QB-1's provisional promised in so many words: "a gate re-dedication,
			// not a data loss". Asserted column by column so it cannot be weakened by accident.
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, KeyB),
				new KingdomGateRow(KeyB, Ozym, KeyA),
				new KingdomGateRow(KeyC, Sheba, ""));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			KingdomMirrorGateRules.TryHub(rows, Ozym, out next, out rekeyed, out hub);
			Assert.AreEqual(rows.Length, next.Length);
			for (int i = 0; i < rows.Length; i++)
			{
				Assert.AreEqual(rows[i].Key, next[i].Key, "row " + i + " kept its arch");
				Assert.AreEqual(rows[i].City, next[i].City, "row " + i + " kept its city");
			}
		}

		[Test]
		public void TheRegisterHandedInIsNeverEdited()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, ""),
				new KingdomGateRow(KeyB, Ozym, ""));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			KingdomMirrorGateRules.TryHub(rows, Kavvat, out next, out rekeyed, out hub);
			Assert.AreEqual("", rows[0].Partner, "copy-on-write: the register handed in is untouched");
			Assert.AreEqual("", rows[1].Partner);
			Assert.AreEqual(KeyB, next[0].Partner);
		}

		[Test]
		public void ReHubbingARealmThatIsAlreadyHubbedThereChangesNothingAndSaysNothing()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, KeyB),
				new KingdomGateRow(KeyB, Ozym, KeyA));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			Assert.AreEqual(KingdomGateVerdict.Joined, KingdomMirrorGateRules.TryHub(rows, Kavvat, out next, out rekeyed, out hub));
			Assert.AreEqual(0, rekeyed);
			Assert.AreEqual("", KingdomMirrorGateRules.HubbedLine(Kavvat, 0),
				"7b's first kind: nothing happened, so nothing is said");
		}

		[Test]
		public void ACapitalThatKeepsNoArchLeavesEveryCrossingExactlyAsItWas()
		{
			// Cities without arches are untouched, and the realm's existing crossings survive a
			// crowning that could not hub them. The founder is told, because they asked for a thing.
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, KeyB),
				new KingdomGateRow(KeyB, Ozym, KeyA));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			Assert.AreEqual(KingdomGateVerdict.RefusedUnkeyed, KingdomMirrorGateRules.TryHub(rows, Sheba, out next, out rekeyed, out hub));
			Assert.AreSame(rows, next);
			Assert.AreEqual(0, rekeyed);
			Assert.AreEqual("", hub);
			StringAssert.Contains(Sheba, KingdomMirrorGateRules.NoArchAtCapitalLine(Sheba));
			StringAssert.Contains("left exactly as they were", KingdomMirrorGateRules.NoArchAtCapitalLine(Sheba));
		}

		[Test]
		public void ACapitalWhoseArchIsTheOnlyOneIsKeyedAndWaitingRatherThanJoined()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, Kavvat, ""));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			Assert.AreEqual(KingdomGateVerdict.Offered, KingdomMirrorGateRules.TryHub(rows, Kavvat, out next, out rekeyed, out hub));
			Assert.AreEqual(KeyA, hub);
			Assert.AreEqual("", next[0].Partner, "an arch that answered itself would land a founder where they stand");
			Assert.AreEqual(0, rekeyed);
		}

		[Test]
		public void AnArchLeftAnsweringSomebodyWhoIsNoLongerAnsweringItCannotSurviveAHub()
		{
			// The register's standing invariant: no row may point at an arch that points elsewhere.
			// A hub rewrites every row, so it is the one act that could not break it -- asserted
			// anyway, because the arithmetic that guarantees it is a loop with a continue in it.
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, KeyC),
				new KingdomGateRow(KeyB, Ozym, ""),
				new KingdomGateRow(KeyC, Sheba, KeyA));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			KingdomMirrorGateRules.TryHub(rows, Ozym, out next, out rekeyed, out hub);
			for (int i = 0; i < next.Length; i++)
			{
				if (next[i].Partner.Length == 0)
				{
					continue;
				}
				string back = KingdomMirrorGateRules.PartnerOf(next, next[i].Partner);
				Assert.IsTrue(
					string.Equals(back, next[i].Key) || string.Equals(next[i].Partner, hub),
					"row " + i + " answers " + next[i].Partner + " which answers " + back);
			}
		}

		[Test]
		public void ACityIsMatchedForTheHubTheWayAFounderReadsIt()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, Kavvat, ""),
				new KingdomGateRow(KeyB, Ozym, ""));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			Assert.AreEqual(KingdomGateVerdict.Joined, KingdomMirrorGateRules.TryHub(rows, "kavvat", out next, out rekeyed, out hub));
			Assert.AreEqual(KeyA, hub);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("Kav|vat")]
		[TestCase("Kav^vat")]
		public void ACapitalTheRegisterCouldNotStoreRefusesRatherThanHubs(string city)
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, Kavvat, ""));
			KingdomGateRow[] next;
			int rekeyed;
			string hub;
			Assert.AreEqual(KingdomGateVerdict.RefusedNamed, KingdomMirrorGateRules.TryHub(rows, city, out next, out rekeyed, out hub));
			Assert.AreSame(rows, next);
		}

		[Test]
		public void TheHubbedTellingNamesTheCapitalAndTheLineNamesTheCount()
		{
			StringAssert.Contains(Kavvat, KingdomMirrorGateRules.HubbedTelling(Kavvat));
			StringAssert.Contains("2 arches", KingdomMirrorGateRules.HubbedLine(Kavvat, 2));
			StringAssert.Contains("1 arch is", KingdomMirrorGateRules.HubbedLine(Kavvat, 1));
			Assert.IsFalse(KingdomMirrorGateRules.HubbedLine(Kavvat, 2).Contains("dark"),
				"a re-key is not a brownout, and Addendum 8 forbids a timer of our own wearing one's clothes");
		}
	}
}
#endif
