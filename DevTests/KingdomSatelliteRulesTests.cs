#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The satellites: Anno's local departments transferred (END-STATE-CITIES-RESEARCH &sect;5.5),
	/// which is one outpost per city per great work, carrying a slice and never the ceremony.
	/// <para>
	/// The asymmetry is the design and is what every case here is really about: the PARENT is asked
	/// for realm-wide, so the capital's projects are felt in cities that did not undertake them, and
	/// the OUTPOST is counted city-wide, so a satellite city never accumulates a chore (&sect;5.6).
	/// </para>
	/// </summary>
	public class KingdomSatelliteRulesTests
	{
		private const string Office = KingdomSatelliteRules.RegistryOfficeKey;

		private const string Surgery = KingdomSatelliteRules.SurgeryKey;

		private const string Annexe = KingdomAnnexeRules.AnnexeKey;

		// --- The declaration ----------------------------------------------------------------------

		[TestCase(null, false)]
		[TestCase("", false)]
		[TestCase("   ", false)]
		[TestCase("becomingannexe", true)]
		public void ADesignIsOrdinaryUntilItNamesAParent(string declared, bool expected)
		{
			Assert.AreEqual(expected, KingdomSatelliteRules.IsSatellite(declared));
		}

		[Test]
		public void TheParentIsAKeyRatherThanAFlagSoAThirdPartyCanDeclareItsOwn()
		{
			// STANDARDS §6: a third-party file ships an outpost of its own megastructure without a
			// line of our code changing, which a boolean could not express.
			Assert.AreEqual("somebodyelsesgreatwork", KingdomSatelliteRules.ParentOf("  somebodyelsesgreatwork  "));
			Assert.IsNull(KingdomSatelliteRules.ParentOf(""));
			Assert.IsNull(KingdomSatelliteRules.ParentOf(null));
		}

		// --- The gate -----------------------------------------------------------------------------

		[Test]
		public void AnOutpostWantsItsGreatWorkStandingSomewhereInTheRealm()
		{
			Assert.AreEqual(KingdomSatelliteVerdict.RefusedNoParent,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: false, CityKeeps: null, Key: Office));
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: null, Key: Office));
		}

		[Test]
		public void TheParentNeedNotBeInThisCityAndThatIsTheWholePoint()
		{
			// §5.5's transfer: the capital's structures project outward, so an outpost is judged
			// against the realm rather than against the ground it stands on. There is no city
			// argument in this rule at all, and that absence is the assertion.
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: null, Key: Surgery));
		}

		[Test]
		public void OneOutpostPerCityPerGreatWork()
		{
			Assert.AreEqual(KingdomSatelliteVerdict.RefusedCityKeeps,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: "someoneelsesoffice", Key: Office));
		}

		[Test]
		public void ReRaisingTheOneAlreadyKeptIsNotASecondOne()
		{
			// The purpose gate's own bargain: mending, re-siting or re-staking the one you have is
			// not choosing again.
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: Office, Key: Office));
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: "RegistryOffice", Key: Office),
				"matched the way the registry writes it");
		}

		[Test]
		public void ACityMayKeepOneOfEachRatherThanOneAltogether()
		{
			// Per-parent, not per-city-total: a city choosing between a surgery and a registry
			// office would be choosing between two great works it did not raise.
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: null, Key: Surgery),
				"the office this city keeps is an outpost of a different parent, so it is not passed here at all");
		}

		[Test]
		public void AnOrdinaryDesignIsNeverAskedAnyOfThis()
		{
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: false, RealmKeepsParent: false, CityKeeps: "smithy", Key: "smithy"));
		}

		[Test]
		public void TheGateFailsOpenWhenNothingCouldRead()
		{
			// The purpose gate's bargain again: a cardinality rule that cannot see must let the
			// founder build, or a realm is bricked by a book nobody can open.
			Assert.AreEqual(KingdomSatelliteVerdict.Allowed,
				KingdomSatelliteRules.Judge(Satellite: true, RealmKeepsParent: true, CityKeeps: null, Key: Office));
		}

		// --- The verbs the outposts carry, and the ones they do not --------------------------------

		[Test]
		public void TheSurgeryTopsOutAtTheVatsAndNeverReachesTheTable()
		{
			// Addendum 22 A2: lower rungs may sit anywhere; top rungs and once-ever ceremonies stay
			// sited. Rung 1 is the vat-house, which is the last rung before anything is opened.
			Assert.AreEqual(KingdomProcedureRules.RungVat, KingdomSatelliteRules.SurgeryCeilingRung);
			Assert.Less(KingdomSatelliteRules.SurgeryCeilingRung, KingdomProcedureRules.RungHall);
			Assert.Less(KingdomSatelliteRules.SurgeryCeilingRung, KingdomProcedureRules.RungTheatre);
		}

		[Test]
		public void TheRegistryOfficeNeverHoldsTheCeremony()
		{
			// A ruling, not a knob. Enrolment rewrites what a body is allowed to be for the rest of
			// a run, which is the most once-ever act the mod has.
			Assert.IsFalse(KingdomSatelliteRules.OfficeEnrols);
		}

		// --- The prose contracts (STANDARDS 7b, §1.5) -----------------------------------------------

		[Test]
		public void TheNoParentRefusalNamesTheGreatWorkAndSaysItNeedNotBeHere()
		{
			string line = KingdomSatelliteRules.NoParentRefusalLine("the becoming annexe");
			StringAssert.Contains("the becoming annexe", line);
			StringAssert.Contains("need not be this one", line);
			Assert.IsFalse(line.Contains("Satellite=\""), line);
		}

		[Test]
		public void TheOneToACityRefusalNamesTheBuildingInTheWayRatherThanTheRule()
		{
			string line = KingdomSatelliteRules.CityKeepsRefusalLine("the registry office");
			StringAssert.Contains("the registry office", line);
			Assert.IsFalse(line.Contains("cardinality"), line);
		}

		[Test]
		public void AnOutpostSaysWhatItWillNotDoBeforeAFounderWantsIt()
		{
			// §1.5's lesson: what players will not forgive is the consequence nobody told them
			// about. The door is closed out loud, in the description, at the moment they read it.
			string office = KingdomSatelliteRules.DescriptionLine(
				KingdomSatelliteRules.OfficeSlice(), KingdomSatelliteRules.OfficeWithheld(), "Kavvat");
			StringAssert.Contains("read", office);
			StringAssert.Contains("entered", office);
			StringAssert.Contains("Kavvat", office);
			string surgery = KingdomSatelliteRules.DescriptionLine(
				KingdomSatelliteRules.SurgerySlice(), KingdomSatelliteRules.SurgeryWithheld(), null);
			StringAssert.Contains("kept", surgery);
			StringAssert.Contains("grafting", surgery);
			StringAssert.Contains("the city that raised the great work", surgery,
				"a sentence that could not name a place says so honestly rather than saying 'somewhere'");
		}

		[Test]
		public void AGreatWorkNothingNamedIsStillSpokenOfHonestly()
		{
			Assert.AreEqual("the great work", KingdomSatelliteRules.Named(null));
			Assert.AreEqual("the great work", KingdomSatelliteRules.Named(""));
			Assert.AreEqual("the annexe", KingdomSatelliteRules.Named("  the annexe  "));
		}

		[Test]
		public void TheTwoShippedOutpostsAreNotTheSameRecord()
		{
			Assert.AreNotEqual(KingdomSatelliteRules.SurgeryKey, KingdomSatelliteRules.RegistryOfficeKey);
			Assert.AreNotEqual(KingdomSatelliteRules.RegistryOfficeKey, Annexe,
				"an outpost is never its own parent");
			Assert.AreNotEqual(KingdomSatelliteRules.SurgeryKey, KingdomLabRules.TheatreKey);
		}
	}
}
#endif
