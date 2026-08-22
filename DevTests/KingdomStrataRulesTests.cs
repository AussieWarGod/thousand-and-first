#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Addendum 15: the catalogue has more than one set, a design says which one it lives in and
	/// which others it may stand in, and the sky is a filtered subset of the surface rather than a
	/// set of its own.
	/// <para>
	/// The first section is the one that matters most and is deliberately first: <b>an entry that
	/// declares no stratum loses no ground</b>. Every design in the shipped catalogue and every
	/// design in every third party's file was written before this attribute existed, and the day it
	/// landed must not have been the day the weep-tap stopped being cut into rock.
	/// </para>
	/// </summary>
	public class KingdomStrataRulesTests
	{
		// ==================================================================================
		// Back-compatibility. STANDARDS 6: an absent attribute gates nothing.
		// ==================================================================================

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void ADesignThatNamesNoStratumStandsInEveryOneOfThem(string strata)
		{
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumSurface));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumDeep));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumSky));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumArcology));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, "somebody else's seabed"));
		}

		[Test]
		public void AnUngatedDesignIsStillJudgedPermittedUnderTheRock()
		{
			// The weep-tap's whole existence: it declares nothing and it is the only water a deep
			// settlement has. If this ever fails, the strata wave took the deep's water away.
			ZoningJudgement judgement = KingdomZoningRules.Judge(ZoneGate.Open, null, "storage", 1, null,
				Underground: true, RequiresSky: false, Roll: BuilderRoll.Unknown,
				Stratum: KingdomZoningRules.StratumDeep);
			Assert.IsTrue(judgement.Permitted);
			Assert.IsNull(judgement.Detail);
			Assert.IsNull(judgement.Note);
		}

		[Test]
		public void TheOpenGateIsStillOpenNowThatItCarriesAStratum()
		{
			Assert.IsTrue(ZoneGate.Open.IsOpen);
			Assert.IsNull(ZoneGate.Open.Strata);
		}

		[Test]
		public void ADesignThatDeclaresOnlyAStratumIsNotAnOpenGate()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("carvedcell", null, null, null, null,
				null, null, null, "deep", out string error);
			Assert.IsNull(error);
			Assert.IsFalse(gate.IsOpen, "a design that belongs to one stratum has something to refuse");
			Assert.AreEqual("deep", gate.Strata);
		}

		[Test]
		public void EveryOlderOverloadStillParsesAndJudgesWithoutAStratum()
		{
			// The published shapes are not re-cut under a third party already calling them
			// (STANDARDS 9): each older overload is the newest one with a null on the end.
			Assert.IsNull(KingdomZoningRules.ParseGateAttributes("k", null, null, null, null, out _).Strata);
			Assert.IsNull(KingdomZoningRules.ParseGateAttributes("k", null, null, null, null, null, null, null, out _).Strata);
			Assert.IsTrue(KingdomZoningRules.Judge(ZoneGate.Open, null, "housing", 1, null).Permitted);
			Assert.IsTrue(KingdomZoningRules.Judge(ZoneGate.Open, null, "housing", 1, null, true, false).Permitted);
			Assert.IsTrue(KingdomZoningRules.Judge(ZoneGate.Open, null, "housing", 1, null, true, false, BuilderRoll.Unknown).Permitted);
		}

		// ==================================================================================
		// Home stratum and share-tags: a design LIVES in one set and STANDS in several.
		// ==================================================================================

		[TestCase("deep", "deep")]
		[TestCase("deep,surface", "deep")]
		[TestCase("surface,deep", "surface")]
		[TestCase(" DEEP , Surface ", "deep")]
		[TestCase("all,deep", "deep")]
		[TestCase("!sky,deep", "deep")]
		[TestCase(null, "surface")]
		[TestCase("", "surface")]
		[TestCase("all", "surface")]
		[TestCase("!deep", "surface")]
		public void TheFirstWelcomedTokenIsWhereTheDesignLives(string strata, string expected)
		{
			Assert.AreEqual(expected, KingdomZoningRules.HomeStratum(strata));
		}

		[Test]
		public void EverythingAfterTheHomeIsAShareTag()
		{
			CollectionAssert.AreEqual(new List<string> { "surface" }, KingdomZoningRules.StrataShared("deep,surface"));
			CollectionAssert.AreEqual(new List<string> { "surface", "sky" }, KingdomZoningRules.StrataShared("deep,surface,sky"));
			CollectionAssert.IsEmpty(KingdomZoningRules.StrataShared("deep"));
			CollectionAssert.IsEmpty(KingdomZoningRules.StrataShared("all,!deep"));
		}

		[Test]
		public void ADesignThatDeclaresNothingSharesNothingRatherThanEverything()
		{
			// It stands everywhere by DEFAULT, not by sharing. Reading an absent attribute as a
			// share into every set would put the whole catalogue in the deep set's roster.
			CollectionAssert.IsEmpty(KingdomZoningRules.StrataShared(null));
			Assert.AreEqual("surface", KingdomZoningRules.HomeStratum(null));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits(null, "deep"));
		}

		// ==================================================================================
		// Admission: which strata a design may stand in.
		// ==================================================================================

		[TestCase("deep", "deep", true)]
		[TestCase("deep", "surface", false)]
		[TestCase("deep", "arcology", false)]
		[TestCase("surface", "surface", true)]
		[TestCase("surface", "deep", false)]
		[TestCase("deep,surface", "deep", true)]
		[TestCase("deep,surface", "surface", true)]
		[TestCase("deep,surface", "arcology", false)]
		[TestCase("all", "deep", true)]
		[TestCase("all", "arcology", true)]
		[TestCase("all,!deep", "deep", false)]
		[TestCase("all,!deep", "surface", true)]
		[TestCase("all,!deep", "arcology", true)]
		[TestCase("!deep", "deep", false)]
		[TestCase("!deep", "surface", true)]
		[TestCase("DEEP", "deep", true)]
		[TestCase("deep", "DEEP", true)]
		[TestCase(" deep , surface ", "surface", true)]
		public void StrataAdmitsReadsTheListTheWayEveryOtherTagListIsRead(string strata, string stratum, bool admitted)
		{
			Assert.AreEqual(admitted, KingdomZoningRules.StrataAdmits(strata, stratum));
		}

		[Test]
		public void GroundNobodyCouldNameNeverRefusesADesign()
		{
			// The same asymmetry BuilderRoll.Unknown makes: a gate that cannot see where it stands
			// must never be the reason a founder cannot build.
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("deep", null));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("deep", ""));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("deep", "  "));
		}

		[Test]
		public void AStratumThisBuildDoesNotShipStillGatesAndStillNamesItself()
		{
			// The strata set is OPEN, exactly as the style set is: a third party's seabed refuses
			// and describes itself rather than reading as a blank (STANDARDS 6).
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("seabed", "seabed"));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("seabed", "deep"));
			Assert.AreEqual("seabed", KingdomZoningRules.StratumName("seabed"));
			Assert.AreEqual("seabed", KingdomZoningRules.HomeStratum("seabed"));
		}

		// ==================================================================================
		// Addendum 15's second ruling: THE SKY IS A FILTERED SUBSET OF THE SURFACE.
		// ==================================================================================

		[Test]
		public void ASurfaceDesignStandsInTheSkyWithoutSayingSo()
		{
			// The whole of what "filtered subset" means. The sky set is never enumerated: it is
			// whatever the surface set is, minus what filters itself out.
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("surface", KingdomZoningRules.StratumSky));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("all", KingdomZoningRules.StratumSky));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("surface,deep", KingdomZoningRules.StratumSky));
		}

		[Test]
		public void ASurfaceDesignThatFiltersItselfOutOfTheSkyIsRefusedThere()
		{
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("surface,!sky", KingdomZoningRules.StratumSky));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("surface,!sky", KingdomZoningRules.StratumSurface));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("all,!sky", KingdomZoningRules.StratumSky));
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("all,!sky", KingdomZoningRules.StratumDeep));
		}

		[Test]
		public void ADeepDesignIsNotASkyDesign()
		{
			// The subset is of the SURFACE, so a set the surface does not hold does not reach the
			// sky through it. A fungal vault is not raised on somebody's roof.
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("deep", KingdomZoningRules.StratumSky));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("arcology", KingdomZoningRules.StratumSky));
		}

		[Test]
		public void ADesignThatNamesTheSkyIsJudgedOnWhatItNamedAndNotOnTheSurface()
		{
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("sky", KingdomZoningRules.StratumSky));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("sky", KingdomZoningRules.StratumSurface),
				"a design that lives on a roof does not stand on open ground");
			Assert.IsTrue(KingdomZoningRules.StrataAdmits("deep,sky", KingdomZoningRules.StratumSky));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("deep,sky", KingdomZoningRules.StratumSurface));
		}

		// ==================================================================================
		// Parsing. A typo in a gate never deletes a design from the catalogue.
		// ==================================================================================

		[TestCase("all")]
		[TestCase("ALL")]
		[TestCase(" all ")]
		public void AllOnItsOwnIsHowAStratumListSpellsNoRestriction(string strata)
		{
			ZoneGate gate = Parse(strata, out string error);
			Assert.IsNull(error);
			Assert.IsNull(gate.Strata);
			Assert.IsTrue(gate.IsOpen);
		}

		[Test]
		public void AllWithARefusalIsKeptBecauseItRestrictsSomething()
		{
			// "all" alone is no restriction; "all except the deep" is one, and dropping it for the
			// word it happens to start with would silently un-gate the design.
			ZoneGate gate = Parse("all,!deep", out string error);
			Assert.IsNull(error);
			Assert.AreEqual("all,!deep", gate.Strata);
			Assert.IsFalse(KingdomZoningRules.StrataAdmits(gate.Strata, "deep"));
		}

		[TestCase(",,")]
		[TestCase(" , ")]
		public void AListWithNothingUsableInItIsDroppedAndNamed(string strata)
		{
			ZoneGate gate = Parse(strata, out string error);
			Assert.IsNull(gate.Strata);
			Assert.IsNotNull(error);
			StringAssert.Contains("Strata", error);
		}

		[Test]
		public void AStrataListIsFoldedAndDeduplicatedLikeEveryOtherList()
		{
			ZoneGate gate = Parse(" Deep , SURFACE , deep ", out string error);
			Assert.IsNull(error);
			Assert.AreEqual("deep,surface", gate.Strata);
		}

		[Test]
		public void AFaultInTheStratumDoesNotCostTheDesignItsOtherGates()
		{
			// The asymmetry the gate parser has always kept: a gate is a restriction ON a design,
			// so a typo in one narrows nothing and deletes nothing.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("post", "craft", "3", "machine:Solar Still", "workshop",
				null, null, null, ",,", out string error);
			Assert.IsNotNull(error);
			Assert.AreEqual("craft", gate.Districts);
			Assert.AreEqual(3, gate.MinZones);
			Assert.AreEqual(TechLevel.Workshop, gate.MinTech);
			Assert.IsNull(gate.Strata);
		}

		[Test]
		public void EveryGateAttributeParsesTogetherWithNoComplaint()
		{
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("vaultgalleries", " agrarian ", "3", " machine:Solar Still ", "workshop",
				"origin:the rust wells", "Barathrumites", "25", " deep , surface ", out string error);
			Assert.IsNull(error);
			Assert.AreEqual("deep,surface", gate.Strata);
			Assert.AreEqual("Barathrumites", gate.Creed);
			Assert.AreEqual(25, gate.CreedShare);
		}

		// ==================================================================================
		// The refusal, and the order it is asked in (STANDARDS 7b).
		// ==================================================================================

		[Test]
		public void ADeepDesignOnOpenGroundIsRefusedAndTheRefusalNamesTheDeep()
		{
			ZoningJudgement judgement = JudgeIn("deep", KingdomZoningRules.StratumSurface, Underground: false);
			Assert.AreEqual(ZoningVerdict.RefusedStratum, judgement.Verdict);
			Assert.AreEqual("the deep", judgement.Detail, "the refusal names the stratum that WOULD take it");
			Assert.AreEqual("wants the deep", judgement.Note);
		}

		[Test]
		public void ASurfaceDesignUnderTheRockIsRefusedAndTheRefusalNamesOpenGround()
		{
			ZoningJudgement judgement = JudgeIn("surface", KingdomZoningRules.StratumDeep, Underground: true);
			Assert.AreEqual(ZoningVerdict.RefusedStratum, judgement.Verdict);
			Assert.AreEqual("open ground", judgement.Detail);
			Assert.AreEqual("wants open ground", judgement.Note);
		}

		[Test]
		public void AShareTaggedDesignIsPermittedInBothItsStrata()
		{
			// The weep-tap's declaration: it lives in the deep and a surface city may still cut one.
			Assert.IsTrue(JudgeIn("deep,surface", KingdomZoningRules.StratumDeep, Underground: true).Permitted);
			Assert.IsTrue(JudgeIn("deep,surface", KingdomZoningRules.StratumSurface, Underground: false).Permitted);
		}

		[Test]
		public void ARefusalThatNamesSeveralStrataNamesAllOfThem()
		{
			ZoningJudgement judgement = JudgeIn("deep,arcology", KingdomZoningRules.StratumSurface, Underground: false);
			Assert.AreEqual(ZoningVerdict.RefusedStratum, judgement.Verdict);
			Assert.AreEqual("the deep or the arcology", judgement.Detail);
			Assert.AreEqual("wants the deep", judgement.Note, "the short tag names where it lives");
		}

		[Test]
		public void TheWeatherRefusalIsAskedBeforeTheSetRefusal()
		{
			// A design that wants sun AND belongs to the deep is contradictory authoring, and the
			// founder is told the half nothing can answer first rather than being sent a parasang
			// down to ground that would still refuse it.
			ZoneGate gate = Parse("deep", out _);
			ZoningJudgement judgement = KingdomZoningRules.Judge(gate, null, "power", 1, null,
				Underground: true, RequiresSky: true, Roll: BuilderRoll.Unknown, Stratum: KingdomZoningRules.StratumDeep);
			Assert.AreEqual(ZoningVerdict.RefusedStratum, judgement.Verdict);
			Assert.AreEqual("wants open sky", judgement.Note, "the weather rule fired, not the set rule");
		}

		[Test]
		public void TheSetIsAskedAfterTerritoryAndBeforeGround()
		{
			// The order the founder is taught in, unchanged by this wave: the realm being too small
			// outranks the stratum, and the stratum outranks the district.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("x", "agrarian", "3", null, null,
				null, null, null, "deep", out _);
			Assert.AreEqual(ZoningVerdict.RefusedTerritory,
				JudgeWith(gate, "shrine", 1, KingdomZoningRules.StratumSurface).Verdict);
			Assert.AreEqual(ZoningVerdict.RefusedStratum,
				JudgeWith(gate, "shrine", 3, KingdomZoningRules.StratumSurface).Verdict);
			Assert.AreEqual(ZoningVerdict.RefusedDistrict,
				JudgeWith(gate, "shrine", 3, KingdomZoningRules.StratumDeep).Verdict);
			Assert.IsTrue(JudgeWith(gate, "agrarian", 3, KingdomZoningRules.StratumDeep).Permitted);
		}

		[Test]
		public void TheStratumIsDerivedFromTheDepthWhenNobodyNamesIt()
		{
			// The overload every shipped caller uses. A deep design is refused on surface ground and
			// permitted under the rock without anybody passing a stratum at all.
			ZoneGate gate = Parse("deep", out _);
			Assert.AreEqual(ZoningVerdict.RefusedStratum,
				KingdomZoningRules.Judge(gate, null, "housing", 1, null, false, false, BuilderRoll.Unknown).Verdict);
			Assert.IsTrue(KingdomZoningRules.Judge(gate, null, "housing", 1, null, true, false, BuilderRoll.Unknown).Permitted);
		}

		// ==================================================================================
		// Naming the ground.
		// ==================================================================================

		[TestCase(true, "deep")]
		[TestCase(false, "surface")]
		public void TheGroundNamesTheTwoStrataItCanName(bool underground, string expected)
		{
			Assert.AreEqual(expected, KingdomZoningRules.StratumOfGround(underground));
		}

		[TestCase("surface", "open ground")]
		[TestCase("deep", "the deep")]
		[TestCase("sky", "the sky")]
		[TestCase("arcology", "the arcology")]
		[TestCase(" DEEP ", "the deep")]
		[TestCase(null, "open ground")]
		[TestCase("", "open ground")]
		public void EveryStratumHasAWordTheFounderReads(string stratum, string expected)
		{
			Assert.AreEqual(expected, KingdomZoningRules.StratumName(stratum));
		}

		[Test]
		public void TheOlderWeatherNamingIsUntouched()
		{
			// Two functions, two questions: one names the weather a design was refused for want of,
			// the other names the ground a design belongs to. The shipped sentence is not re-cut.
			Assert.AreEqual("under the rock", KingdomZoningRules.StratumName(Underground: true));
			Assert.AreEqual("open sky", KingdomZoningRules.StratumName(Underground: false));
		}

		[TestCase("deep", "the deep")]
		[TestCase("deep,surface", "the deep or open ground")]
		[TestCase("deep,surface,sky", "the deep, open ground or the sky")]
		[TestCase("all,!deep", "any stratum but the deep")]
		[TestCase("!deep,!sky", "any stratum but the deep or the sky")]
		[TestCase("surface,!sky", "open ground, but never the sky")]
		[TestCase("all,deep", "any stratum, or the deep")]
		[TestCase(null, null)]
		[TestCase("", null)]
		public void AStrataListReadsBackAsASentence(string strata, string expected)
		{
			Assert.AreEqual(expected, KingdomZoningRules.DescribeStrata(strata));
		}

		// ==================================================================================
		// The deep set as it ships: six records, and every one of them gated the same way.
		// ==================================================================================

		[TestCase("carvedcell")]
		[TestCase("carvedgallery")]
		[TestCase("fungalvault")]
		[TestCase("vaultgalleries")]
		[TestCase("deepcut")]
		[TestCase("nichetomb")]
		public void EveryDeepRecordLivesInTheDeepAndIsRefusedAboveIt(string key)
		{
			// The catalogue's own strings are not readable from a pure test, so the gate each deep
			// record declares is asserted here as the contract the XML must keep. A record that
			// stops saying `Strata="deep"` breaks this and says so.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes(key, null, null, null, null,
				null, null, null, "deep", out string error);
			Assert.IsNull(error);
			Assert.AreEqual(KingdomZoningRules.StratumDeep, KingdomZoningRules.HomeStratum(gate.Strata));
			CollectionAssert.IsEmpty(KingdomZoningRules.StrataShared(gate.Strata), "the starter set shares nowhere");
			Assert.IsTrue(JudgeIn(gate.Strata, KingdomZoningRules.StratumDeep, Underground: true).Permitted);
			Assert.AreEqual(ZoningVerdict.RefusedStratum,
				JudgeIn(gate.Strata, KingdomZoningRules.StratumSurface, Underground: false).Verdict);
			Assert.AreEqual(ZoningVerdict.RefusedStratum,
				JudgeIn(gate.Strata, KingdomZoningRules.StratumSky, Underground: false).Verdict);
		}

		[Test]
		public void NoDeepRecordMayEverWantWeather()
		{
			// The deep register's first line: there is no sky under the rock, so a deep design that
			// declared Sky="yes" would be a design nobody could raise anywhere. Held here as an
			// arithmetic fact rather than a promise: the two gates cannot both be satisfied.
			Assert.IsFalse(KingdomZoningRules.StratumAccepts(Underground: true, RequiresSky: true));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("deep", KingdomZoningRules.StratumSurface));
		}

		[Test]
		public void TheSeepLaneLivesInTheDeepAndSharesIntoTheSurface()
		{
			// The share-tag on a shipped record, and the reason the tag exists at all: a seep is a
			// deep thing, and a surface city that cuts down to one is not doing something else.
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("weeptap", null, null, null, null,
				null, null, null, "deep,surface", out string error);
			Assert.IsNull(error);
			Assert.AreEqual(KingdomZoningRules.StratumDeep, KingdomZoningRules.HomeStratum(gate.Strata));
			CollectionAssert.AreEqual(new List<string> { KingdomZoningRules.StratumSurface },
				KingdomZoningRules.StrataShared(gate.Strata));
			Assert.IsTrue(JudgeIn(gate.Strata, KingdomZoningRules.StratumDeep, Underground: true).Permitted);
			Assert.IsTrue(JudgeIn(gate.Strata, KingdomZoningRules.StratumSurface, Underground: false).Permitted);
		}

		[Test]
		public void ASurfaceRecordCanRefuseTheDeepAndHandTheSetItsOwnAnswer()
		{
			// The third shape of the attribute, and the file's own idiom told across strata rather
			// than styles: the cairn is STACKED fieldstone and the mason's yard is a spoil heap in
			// the open, so both refuse the deep — and both are refused in the same pass that gives
			// the deep the niche tomb and the deep cut. A stratum is a place, not a penalty.
			foreach (string strata in new string[1] { "all,!deep" })
			{
				Assert.IsFalse(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumDeep));
				Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumSurface));
				Assert.IsTrue(KingdomZoningRules.StrataAdmits(strata, KingdomZoningRules.StratumSky));
				Assert.AreEqual(KingdomZoningRules.StratumSurface, KingdomZoningRules.HomeStratum(strata));
			}
			ZoningJudgement judgement = JudgeIn("all,!deep", KingdomZoningRules.StratumDeep, Underground: true);
			Assert.AreEqual(ZoningVerdict.RefusedStratum, judgement.Verdict);
			Assert.AreEqual("any stratum but the deep", judgement.Detail);
		}

		[Test]
		public void TheCarveBargainIsTheGroundsAndNotTheDesignsToDeclare()
		{
			// Why every deep record below copies its surface precedent's Cost and Ticks instead of
			// discounting them: the discount is ALREADY paid by the ground. Carving costs double the
			// clearing (UndergroundClearPercent) and gives the enclosure back for nothing, and
			// RaiseTicks applies both. A deep record that also shaved its authored Ticks would be
			// spending the same bargain twice.
			// Corners, so this is the 5x4 footprint the stone house's tier stands on.
			KingdomPlotRules.PlotRect footprint = new KingdomPlotRules.PlotRect(0, 0, 4, 3);
			Assert.AreEqual(0L, KingdomPlotRules.EnclosureTicks(footprint, KingdomPlotRules.RoofState.Carved),
				"the rock the carving left is the wall");
			Assert.Greater(KingdomPlotRules.EnclosureTicks(footprint, KingdomPlotRules.RoofState.Walled), 0L,
				"and above ground the settlement raises it itself");
			Assert.AreEqual(KingdomPlotRules.RoofState.Carved,
				KingdomPlotRules.RoofOnGround(KingdomPlotRules.DefaultRoof(Open: false), Underground: true),
				"which is why no deep record declares a roof: the ground already did");
			Assert.AreEqual(200, KingdomPlotRules.UndergroundClearPercent, "and the price of it is the clearing");
		}

		[Test]
		public void TheDeepSetAndTheArcologySetAreNotOneSet()
		{
			// Addendum 15's first ruling, held as a fact about the vocabulary: the arcology token
			// exists, it is not the deep, and nothing the deep set declares reaches it.
			Assert.AreNotEqual(KingdomZoningRules.StratumDeep, KingdomZoningRules.StratumArcology);
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("deep", KingdomZoningRules.StratumArcology));
			Assert.IsFalse(KingdomZoningRules.StrataAdmits("arcology", KingdomZoningRules.StratumDeep));
		}

		private static ZoneGate Parse(string Strata, out string Error)
		{
			return KingdomZoningRules.ParseGateAttributes("x", null, null, null, null, null, null, null, Strata, out Error);
		}

		private static ZoningJudgement JudgeIn(string Strata, string Stratum, bool Underground)
		{
			return KingdomZoningRules.Judge(Parse(Strata, out _), null, "housing", 1, null,
				Underground, RequiresSky: false, Roll: BuilderRoll.Unknown, Stratum: Stratum);
		}

		private static ZoningJudgement JudgeWith(ZoneGate Gate, string TileDistrict, int ClaimedZones, string Stratum)
		{
			return KingdomZoningRules.Judge(Gate, TileDistrict, "power", ClaimedZones, null,
				Underground: Stratum == KingdomZoningRules.StratumDeep, RequiresSky: false,
				Roll: BuilderRoll.Unknown, Stratum: Stratum);
		}
	}
}
#endif
