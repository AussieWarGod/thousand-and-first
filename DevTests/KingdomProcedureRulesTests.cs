#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomProcedureRulesTests
	{
		// --- Helpers ---------------------------------------------------------------------------

		private const int Animal = 1;
		private const int Arthropod = 2;
		private const int Mechanical = 7;
		private const int Protoplasmic = 5;

		private static LabProcedure Parse(string key, string cls, string grants, string slots,
			string categories = null, string source = "part", string attach = "body",
			string minRung = null, string magnitude = null)
		{
			LabProcedure procedure;
			string error;
			Assert.IsTrue(KingdomProcedureRules.TryParseProcedureAttributes(key, null, cls, grants, slots,
				categories, source, attach, minRung, "20", "002", "6", "1", null, null, magnitude,
				out procedure, out error), error);
			return procedure;
		}

		private static LabSlot Slot(string type, int category = Animal, bool extrinsic = false,
			bool bears = false, string grafted = null)
		{
			return new LabSlot(type, category, extrinsic, bears, grafted);
		}

		/// <summary>
		/// Three bodies, and no genotype named anywhere. This is the fixture that proves the
		/// DIVERSITY §3.4 hard rule 3 claim: a True Kin, a robot player and a slime player get
		/// different legal sets FOR FREE, out of what their anatomy already is.
		/// </summary>
		private static List<LabSlot> TrueKin()
		{
			return new List<LabSlot>
			{
				Slot("Head"), Slot("Face"), Slot("Body"), Slot("Back"),
				Slot("Arm"), Slot("Hand", Animal, false, bears: true), Slot("Feet")
			};
		}

		private static List<LabSlot> Robot()
		{
			return new List<LabSlot>
			{
				Slot("Head", Mechanical), Slot("Face", Mechanical), Slot("Body", Mechanical),
				Slot("Manipulator", Mechanical, false, bears: true), Slot("Tread", Mechanical)
			};
		}

		private static List<LabSlot> Slime()
		{
			return new List<LabSlot>
			{
				Slot("Body", Protoplasmic),
				Slot("Pseudopod", Protoplasmic, false, bears: true),
				Slot("Pseudopod", Protoplasmic, false, bears: true)
			};
		}

		// --- Schema parse ----------------------------------------------------------------------

		[TestCase("I", LabClass.Rider)]
		[TestCase("ii", LabClass.Defence)]
		[TestCase("III", LabClass.Limb)]
		[TestCase("IV", LabClass.Named)]
		[TestCase("2", LabClass.Defence)]
		[TestCase("named", LabClass.Named)]
		public void ParseClass_ReadsTheLadderInEveryFormAFileMightWriteIt(string source, LabClass expected)
		{
			LabClass cls;
			Assert.IsTrue(KingdomProcedureRules.TryParseClass(source, out cls));
			Assert.AreEqual(expected, cls);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("V")]
		[TestCase("rider-ish")]
		public void ParseClass_RefusesALadderRungThisBuildDoesNotHave(string source)
		{
			LabClass cls;
			Assert.IsFalse(KingdomProcedureRules.TryParseClass(source, out cls));
		}

		[Test]
		public void Parse_RefusesARecordThatNamesNoPartClass()
		{
			LabProcedure procedure;
			string error;
			Assert.IsFalse(KingdomProcedureRules.TryParseProcedureAttributes("x", null, "I", null, "Arm",
				null, "part", "body", null, null, null, null, null, null, null, null, out procedure, out error));
			StringAssert.Contains("Grants", error);
		}

		[Test]
		public void Parse_RefusesARecordThatNamesNowhereOnABodyToPutIt()
		{
			LabProcedure procedure;
			string error;
			Assert.IsFalse(KingdomProcedureRules.TryParseProcedureAttributes("x", null, "I", "PoisonOnHit", null,
				null, "part", "body", null, null, null, null, null, null, null, null, out procedure, out error));
			StringAssert.Contains("Slots", error);
		}

		[Test]
		public void Parse_RefusesAnAttachBitItCannotRead()
		{
			// Not a default-to-body: a record whose attach point is a typo is a record that would
			// silently graft an inert part onto a torso, which is the audit's whole lesson.
			LabProcedure procedure;
			string error;
			Assert.IsFalse(KingdomProcedureRules.TryParseProcedureAttributes("x", null, "I", "PoisonOnHit", "Arm",
				null, "part", "torso", null, null, null, null, null, null, null, null, out procedure, out error));
			StringAssert.Contains("Attach", error);
		}

		[TestCase("Invisibility")]
		[TestCase("WallWalker")]
		[TestCase("Metamorphosis")]
		[TestCase("OldElectricalGeneration")]
		[TestCase("Cloneling")]
		[TestCase("SplitOnDeath")]
		[TestCase("Spawner")]
		[TestCase("invisibility")]
		public void Parse_RefusesTheBlocklistAtLoadRatherThanAtCommission(string grants)
		{
			// Addendum 22 D1. Enforced at LOAD so a third party's file naming one fails on the day
			// it ships rather than on the day somebody clicks.
			LabProcedure procedure;
			string error;
			Assert.IsFalse(KingdomProcedureRules.TryParseProcedureAttributes("x", null, "II", grants, "Body",
				null, "part", "body", null, null, null, null, null, null, null, null, out procedure, out error));
			StringAssert.Contains("blocklist", error);
		}

		[Test]
		public void Parse_AdmitsAModdedPartClassNobodyHasEverHeardOf()
		{
			// Hard rule 1's whole payoff: the whitelist is a CONTRACT, not a list. A modded
			// creature's part is a lawful grant the day that mod ships, with no entry of ours.
			LabProcedure procedure = Parse("someothermod", "I", "SomeOtherModsRider", "Arm");
			Assert.AreEqual("SomeOtherModsRider", procedure.Grants);
		}

		[TestCase("-1")]
		[TestCase("4")]
		[TestCase("two")]
		public void Parse_RefusesARungTheLadderDoesNotHave(string rung)
		{
			LabProcedure procedure;
			string error;
			Assert.IsFalse(KingdomProcedureRules.TryParseProcedureAttributes("x", null, "I", "PoisonOnHit", "Arm",
				null, "part", "body", rung, null, null, null, null, null, null, null, out procedure, out error));
			StringAssert.Contains("MinRung", error);
		}

		[Test]
		public void Parse_DefaultsTheRungToWhereThatClassOfWorkIsActuallyDone()
		{
			Assert.AreEqual(KingdomProcedureRules.RungHall, Parse("a", "I", "PoisonOnHit", "Arm").MinRung);
			Assert.AreEqual(KingdomProcedureRules.RungHall, Parse("b", "II", "GasImmunity", "Body").MinRung);
			Assert.AreEqual(KingdomProcedureRules.RungTheatre, Parse("c", "III", "Limb", "Arm", source: "limb").MinRung);
			Assert.AreEqual(KingdomProcedureRules.RungTheatre, Parse("d", "IV", "LiquidFont", "Back").MinRung);
		}

		// --- Registry validation ---------------------------------------------------------------

		[Test]
		public void Validate_FlagsAClassOfWorkSittingBelowItsOwnRung()
		{
			LabProcedure procedure = Parse("lowlimb", "III", "Limb", "Arm", source: "limb", minRung: "2");
			List<string> findings = KingdomProcedureRules.Validate(new List<LabProcedure> { procedure });
			Assert.AreEqual(1, findings.Count);
			StringAssert.Contains("below the rung", findings[0]);
		}

		[Test]
		public void Validate_LetsANamedProcedureSitWhereItsOwnRulingPutIt()
		{
			// The four do not all sit at the same height. The Lantern Rib is hall work at rung 2
			// because it does not change what a founder IS, only what they are carrying — and a
			// validator that flagged that would be flagging the design (DIVERSITY §3.7).
			LabProcedure rib = Parse("lanternrib", "IV", "ActiveLightSource", "Body", minRung: "2");
			CollectionAssert.IsEmpty(KingdomProcedureRules.Validate(new List<LabProcedure> { rib }));
		}

		[Test]
		public void Validate_StillFlagsALimbBelowTheTheatre()
		{
			// The exemption is for Class IV only: a limb is theatre work by definition, and a Class
			// III record at the hall would be a body opened by people who cannot do it.
			LabProcedure limb = Parse("limb", "III", "Arm", "Arm", source: "limb", minRung: "2");
			CollectionAssert.IsNotEmpty(KingdomProcedureRules.Validate(new List<LabProcedure> { limb }));
		}

		[Test]
		public void Validate_FlagsAWeaponAttachRecordThatDoesNotGrantAPart()
		{
			LabProcedure procedure = Parse("odd", "III", "Limb", "Arm", source: "limb", attach: "weapon");
			List<string> findings = KingdomProcedureRules.Validate(new List<LabProcedure> { procedure });
			CollectionAssert.IsNotEmpty(findings);
		}

		[Test]
		public void Validate_FlagsTwoRecordsOverOneClassThatNothingTellsApart()
		{
			// The QB-10 shape is lawful, but only when something distinguishes them: without bands
			// the cheaper record is simply the better buy and the dearer one is unpickable.
			List<LabProcedure> registry = new List<LabProcedure>
			{
				Parse("hide", "II", "ReflectDamage", "Body"),
				Parse("carapace", "II", "ReflectDamage", "Body", minRung: "3")
			};
			List<string> findings = KingdomProcedureRules.Validate(registry);
			CollectionAssert.IsNotEmpty(findings);
			StringAssert.Contains("tells the two apart", findings[0]);
		}

		[Test]
		public void Validate_AcceptsTwoRecordsOverOneClassWhenBandsTellThemApart()
		{
			List<LabProcedure> registry = new List<LabProcedure>
			{
				Parse("hide", "II", "ReflectDamage", "Body", magnitude: "ReflectPercentage:1-25"),
				Parse("carapace", "II", "ReflectDamage", "Body", minRung: "3", magnitude: "ReflectPercentage:26-100")
			};
			CollectionAssert.IsEmpty(KingdomProcedureRules.Validate(registry));
		}

		[Test]
		public void Validate_SaysNothingAboutAWellFormedRegistry()
		{
			CollectionAssert.IsEmpty(KingdomProcedureRules.Validate(new List<LabProcedure>
			{
				Parse("a", "I", "StickOnHit", "Arm,Hand"),
				Parse("b", "II", "GasImmunity", "Body"),
				Parse("c", "III", "Arm", "Arm", source: "limb")
			}));
		}

		[Test]
		public void Validate_IsTotalOverNullAndEmpty()
		{
			CollectionAssert.IsEmpty(KingdomProcedureRules.Validate(null));
			CollectionAssert.IsEmpty(KingdomProcedureRules.Validate(new List<LabProcedure>()));
		}

		// --- Anatomy-slot refusals (hard rule 2) ------------------------------------------------

		[Test]
		public void JudgeSlot_RefusesAPlaceThatIsNotOnThisBodyAtAll()
		{
			LabProcedure tail = Parse("tail", "I", "StickOnHit", "Tail");
			Assert.AreEqual(LabVerdict.RefusedNoSlot, KingdomProcedureRules.JudgeSlot(tail, Slot("Arm"), null));
		}

		[Test]
		public void JudgeSlot_RefusesAPlaceThatIsAlreadySpokenFor()
		{
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Arm");
			Assert.AreEqual(LabVerdict.RefusedSlotTaken,
				KingdomProcedureRules.JudgeSlot(sting, Slot("Arm", Animal, false, false, "somethingelse"), null));
		}

		[Test]
		public void JudgeSlot_RefusesWornScaffoldingEvenWhenTheTypeMatches()
		{
			// Vanilla's own disqualifier, and it leads because it is true about the PLACE rather
			// than about the record (BodyPart.CanReceiveCyberneticImplant refuses on exactly this).
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Arm");
			Assert.AreEqual(LabVerdict.RefusedCategory,
				KingdomProcedureRules.JudgeSlot(sting, Slot("Arm", Animal, extrinsic: true), null));
		}

		[Test]
		public void JudgeSlot_AllowsAPlainMatch()
		{
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Arm,Hand,Tail");
			Assert.AreEqual(LabVerdict.Allowed, KingdomProcedureRules.JudgeSlot(sting, Slot("Hand"), null));
		}

		[Test]
		public void JudgeSlot_MatchesSlotTypesWithoutCaringAboutCase()
		{
			LabProcedure graft = Parse("outcrop", "IV", "ActiveLightSource", "Icy Outcrop");
			Assert.AreEqual(LabVerdict.Allowed, KingdomProcedureRules.JudgeSlot(graft, Slot("icy outcrop"), null));
		}

		[Test]
		public void BestRefusal_NamesTheNearestTrueThingRatherThanTheBluntestOne()
		{
			// A founder whose only arm is taken must hear "already spoken for", not "there is
			// nowhere on you" — the second is a lie and it sends them the wrong way.
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Arm");
			List<LabSlot> body = new List<LabSlot> { Slot("Head"), Slot("Arm", Animal, false, false, "already") };
			Assert.AreEqual(LabVerdict.RefusedSlotTaken, KingdomProcedureRules.BestRefusal(sting, body, null));
		}

		[Test]
		public void BestRefusal_PrefersTheWeaponAnswerOverTheTakenOne()
		{
			LabProcedure fang = Parse("fang", "I", "DrunkOnHit", "Hand", attach: "weapon");
			List<LabSlot> body = new List<LabSlot>
			{
				Slot("Hand", Animal, false, false, "already"),
				Slot("Hand", Animal, false, bears: false)
			};
			Assert.AreEqual(LabVerdict.RefusedNoWeapon, KingdomProcedureRules.BestRefusal(fang, body, null));
		}

		[Test]
		public void LegalSlots_ListsEveryPlaceInAnatomyOrder()
		{
			LabProcedure grip = Parse("grip", "I", "StickOnHit", "Hand");
			List<LabSlot> body = new List<LabSlot> { Slot("Head"), Slot("Hand"), Slot("Body"), Slot("Hand") };
			CollectionAssert.AreEqual(new List<int> { 1, 3 }, KingdomProcedureRules.LegalSlots(grip, body, null));
		}

		// --- Category gating (hard rule 3) -------------------------------------------------------

		[Test]
		public void Categories_GiveThreeBodiesThreeDifferentLegalSetsWithNoGenotypeAnywhere()
		{
			// The claim DIVERSITY §3.4 hard rule 3 makes, asserted directly. Nothing in this test
			// or in the code it exercises knows the words "True Kin", "robot" or "slime".
			LabProcedure flesh = Parse("flesh", "I", "PoisonOnHit", "Arm,Hand,Manipulator,Pseudopod",
				categories: "Animal,Arthropod");
			LabProcedure ooze = Parse("ooze", "I", "StickOnHit", "Arm,Hand,Manipulator,Pseudopod",
				categories: "Protoplasmic");
			List<int> fleshCodes = new List<int> { Animal, Arthropod };
			List<int> oozeCodes = new List<int> { Protoplasmic };

			CollectionAssert.IsNotEmpty(KingdomProcedureRules.LegalSlots(flesh, TrueKin(), fleshCodes));
			CollectionAssert.IsEmpty(KingdomProcedureRules.LegalSlots(flesh, Robot(), fleshCodes));
			CollectionAssert.IsEmpty(KingdomProcedureRules.LegalSlots(flesh, Slime(), fleshCodes));

			CollectionAssert.IsEmpty(KingdomProcedureRules.LegalSlots(ooze, TrueKin(), oozeCodes));
			CollectionAssert.IsEmpty(KingdomProcedureRules.LegalSlots(ooze, Robot(), oozeCodes));
			CollectionAssert.IsNotEmpty(KingdomProcedureRules.LegalSlots(ooze, Slime(), oozeCodes));
		}

		[Test]
		public void Categories_OmittedAdmitsAnyKindOfBody()
		{
			LabProcedure any = Parse("any", "II", "NoKnockdown", "Body");
			CollectionAssert.IsEmpty(KingdomProcedureRules.SlotCategoryNames(any));
			Assert.AreEqual(LabVerdict.Allowed, KingdomProcedureRules.JudgeSlot(any, Slot("Body", Mechanical), null));
			Assert.AreEqual(LabVerdict.Allowed, KingdomProcedureRules.JudgeSlot(any, Slot("Body", Protoplasmic), null));
		}

		[Test]
		public void Categories_KeepTheCaseTheFileWroteBecauseTheEngineSwitchesOnExactStrings()
		{
			// BodyPartCategory.GetCode switches on "Animal", not on "animal", and answers zero for
			// anything else. Folding these would silently drop every category gate in the registry.
			LabProcedure procedure = Parse("x", "I", "PoisonOnHit", "Arm", categories: " Animal , Arthropod ");
			CollectionAssert.AreEqual(new List<string> { "Animal", "Arthropod" },
				KingdomProcedureRules.SlotCategoryNames(procedure));
		}

		// --- Attach semantics --------------------------------------------------------------------

		[Test]
		public void Attach_WeaponRecordIsRefusedAtAPlaceThatBearsNoNaturalWeapon()
		{
			// The audit's whole lesson: a part registering only "WeaponHit" is inert on a torso,
			// because Combat.cs fires that event on the weapon object and never on the bearer.
			LabProcedure leech = Parse("leech", "I", "LifeDrainOnHit", "Arm", attach: "weapon");
			Assert.AreEqual(LabVerdict.RefusedNoWeapon,
				KingdomProcedureRules.JudgeSlot(leech, Slot("Arm", Animal, false, bears: false), null));
			Assert.AreEqual(LabVerdict.Allowed,
				KingdomProcedureRules.JudgeSlot(leech, Slot("Arm", Animal, false, bears: true), null));
		}

		[Test]
		public void Attach_BodyRecordDoesNotCareWhetherThePlaceBites()
		{
			LabProcedure sap = Parse("sap", "I", "SapOnPenetration", "Face", attach: "body");
			Assert.AreEqual(LabVerdict.Allowed,
				KingdomProcedureRules.JudgeSlot(sap, Slot("Face", Animal, false, bears: false), null));
		}

		[TestCase("body", LabAttach.Body)]
		[TestCase("weapon", LabAttach.Weapon)]
		[TestCase("natural", LabAttach.Weapon)]
		[TestCase("bearer", LabAttach.Body)]
		[TestCase(null, LabAttach.Body)]
		[TestCase("", LabAttach.Body)]
		public void ParseAttach_ReadsBothPointsAndDefaultsToTheBearer(string source, LabAttach expected)
		{
			LabAttach attach;
			Assert.IsTrue(KingdomProcedureRules.TryParseAttach(source, out attach));
			Assert.AreEqual(expected, attach);
		}

		// --- The stamp grammar --------------------------------------------------------------------

		[Test]
		public void Stamp_RoundTripsAClassAndItsFields()
		{
			string stamp = KingdomProcedureRules.FormatStamp("ReflectDamage",
				new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("ReflectPercentage", "5") });
			Assert.IsTrue(KingdomProcedureRules.StampCarries(stamp, "ReflectDamage"));
			Assert.AreEqual("5", KingdomProcedureRules.StampedField(stamp, "ReflectDamage", "ReflectPercentage"));
		}

		[Test]
		public void Stamp_RoundTripsAWholeCarcassWorthOfClasses()
		{
			string stamp = KingdomProcedureRules.FormatStamps(new List<string>
			{
				KingdomProcedureRules.FormatStamp("PoisonOnHit", null),
				KingdomProcedureRules.FormatStamp("ReflectDamage",
					new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("ReflectPercentage", "100") }),
				KingdomProcedureRules.FormatStamp("StickOnHit",
					new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("SaveTarget", "15") })
			});
			CollectionAssert.AreEqual(new List<string> { "PoisonOnHit", "ReflectDamage", "StickOnHit" },
				KingdomProcedureRules.StampedClasses(stamp));
			Assert.AreEqual("100", KingdomProcedureRules.StampedField(stamp, "ReflectDamage", "ReflectPercentage"));
			Assert.AreEqual("15", KingdomProcedureRules.StampedField(stamp, "StickOnHit", "SaveTarget"));
			Assert.IsNull(KingdomProcedureRules.StampedField(stamp, "PoisonOnHit", "SaveTarget"));
		}

		[TestCase("Reflect;Damage")]
		[TestCase("Reflect@Damage")]
		[TestCase("Reflect,Damage")]
		[TestCase("Reflect=Damage")]
		[TestCase("")]
		[TestCase(null)]
		public void Stamp_RefusesAClassNameTheStampWouldGiveBackWrong(string name)
		{
			// Refused rather than escaped, which is the posture the realm's own register keeps:
			// a value that cannot be stored whole would come back out as two columns.
			Assert.IsFalse(KingdomProcedureRules.Stampable(name));
			Assert.IsNull(KingdomProcedureRules.FormatStamp(name, null));
		}

		[Test]
		public void Stamp_DropsOneUnwritableFieldAndKeepsTheClass()
		{
			string stamp = KingdomProcedureRules.FormatStamp("StickOnHit", new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("Rules", "sticks;fast"),
				new KeyValuePair<string, string>("SaveTarget", "15")
			});
			Assert.IsTrue(KingdomProcedureRules.StampCarries(stamp, "StickOnHit"));
			Assert.IsNull(KingdomProcedureRules.StampedField(stamp, "StickOnHit", "Rules"));
			Assert.AreEqual("15", KingdomProcedureRules.StampedField(stamp, "StickOnHit", "SaveTarget"));
		}

		[Test]
		public void Stamp_AFieldlessPartStampsAsItselfAndNothingElse()
		{
			Assert.AreEqual("NoKnockdown", KingdomProcedureRules.FormatStamp("NoKnockdown", null));
			Assert.IsTrue(KingdomProcedureRules.StampCarries("NoKnockdown", "NoKnockdown"));
		}

		[Test]
		public void StampCarries_IsFalseForAClassTheCarcassNeverHad()
		{
			Assert.IsFalse(KingdomProcedureRules.StampCarries("PoisonOnHit", "StickOnHit"));
			Assert.IsFalse(KingdomProcedureRules.StampCarries(null, "StickOnHit"));
			Assert.IsFalse(KingdomProcedureRules.StampCarries("PoisonOnHit", null));
		}

		// --- The magnitude band (QB-10) -----------------------------------------------------------

		[TestCase("ReflectPercentage:1-25", "ReflectPercentage", 1, 25)]
		[TestCase(" ReflectPercentage : 26 - 100 ", "ReflectPercentage", 26, 100)]
		[TestCase("Level:3-3", "Level", 3, 3)]
		public void ParseMagnitude_ReadsABand(string source, string field, int low, int high)
		{
			string readField;
			int readLow;
			int readHigh;
			string error;
			Assert.IsTrue(KingdomProcedureRules.TryParseMagnitude(source, out readField, out readLow, out readHigh, out error));
			Assert.AreEqual(field, readField);
			Assert.AreEqual(low, readLow);
			Assert.AreEqual(high, readHigh);
		}

		[TestCase("ReflectPercentage")]
		[TestCase("ReflectPercentage:")]
		[TestCase(":1-25")]
		[TestCase("ReflectPercentage:many-more")]
		[TestCase("ReflectPercentage:100-1")]
		public void ParseMagnitude_RefusesABandItCannotRead(string source)
		{
			string field;
			int low;
			int high;
			string error;
			Assert.IsFalse(KingdomProcedureRules.TryParseMagnitude(source, out field, out low, out high, out error));
			Assert.IsNotNull(error);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void ParseMagnitude_AnAbsentBandIsTheOrdinaryStateAndTakesAnything(string source)
		{
			string field;
			int low;
			int high;
			string error;
			Assert.IsTrue(KingdomProcedureRules.TryParseMagnitude(source, out field, out low, out high, out error));
			Assert.IsNull(field);
		}

		[Test]
		public void Magnitude_SplitsOneClassIntoTwoProductsAtTwoPrices()
		{
			// The QB-10 mechanism, end to end: one class, two records, told apart by what the
			// source itself was carrying — and nothing anywhere names a creature.
			LabProcedure hide = Parse("hide", "II", "ReflectDamage", "Body", magnitude: "ReflectPercentage:1-25");
			LabProcedure carapace = Parse("carapace", "II", "ReflectDamage", "Body", minRung: "3",
				magnitude: "ReflectPercentage:26-100");
			string modest = KingdomProcedureRules.FormatStamp("ReflectDamage",
				new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("ReflectPercentage", "5") });
			string fierce = KingdomProcedureRules.FormatStamp("ReflectDamage",
				new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("ReflectPercentage", "100") });

			Assert.IsTrue(KingdomProcedureRules.MagnitudeAdmits(hide, modest));
			Assert.IsFalse(KingdomProcedureRules.MagnitudeAdmits(hide, fierce));
			Assert.IsFalse(KingdomProcedureRules.MagnitudeAdmits(carapace, modest));
			Assert.IsTrue(KingdomProcedureRules.MagnitudeAdmits(carapace, fierce));
		}

		[Test]
		public void Magnitude_RefusesASourceWhoseNumberCouldNotBeRead()
		{
			// Admitting a number nobody could read is exactly how a rung-2 price buys a rung-3
			// product, so an unreadable field is a refusal and never a shrug.
			LabProcedure carapace = Parse("carapace", "II", "ReflectDamage", "Body",
				magnitude: "ReflectPercentage:26-100");
			Assert.IsFalse(KingdomProcedureRules.MagnitudeAdmits(carapace, "ReflectDamage"));
			Assert.IsFalse(KingdomProcedureRules.MagnitudeAdmits(carapace, null));
		}

		[Test]
		public void Magnitude_ARecordWithNoBandTakesAnythingOfTheClass()
		{
			LabProcedure any = Parse("any", "II", "ReflectDamage", "Body");
			Assert.IsTrue(KingdomProcedureRules.MagnitudeAdmits(any, "ReflectDamage"));
			Assert.IsTrue(KingdomProcedureRules.MagnitudeAdmits(any, null));
		}

		// --- The knowledge gate --------------------------------------------------------------------

		[Test]
		public void KnowledgeMet_WantsEveryTokenAndNotJustOne()
		{
			List<string> roster = new List<string> { "node:vat" };
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(roster, "node:vat"));
			Assert.IsFalse(KingdomProcedureRules.KnowledgeMet(roster, "node:vat,node:graft"));
			roster.Add("node:graft");
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(roster, "node:vat,node:graft"));
		}

		[Test]
		public void KnowledgeMet_AllowsDeclaredAlternativesWithinOneRequiredToken()
		{
			// Commas remain ALL. A bar inside one token is the roster's declared OR grammar, shared
			// with research visibility and source resolution rather than reimplemented here.
			List<string> roster = new List<string> { "rite:Girsh" };
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(roster, "rite:Girsh"));
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(roster,
				"rite:Girsh|machine:Regeneration Tank"));
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(
				new List<string> { "machine:Regeneration Tank" },
				"rite:Girsh|machine:Regeneration Tank"));
			Assert.IsFalse(KingdomProcedureRules.KnowledgeMet(
				new List<string> { "machine:Solar Condenser" },
				"rite:Girsh|machine:Regeneration Tank"));
		}

		[Test]
		public void KnowledgeMet_MatchesTheWayARosterIsReadAndNotTheWayAFileWroteIt()
		{
			List<string> roster = new List<string> { "Node:Graft" };
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(roster, "node:graft"));
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(roster, "  node:graft  "));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void KnowledgeMet_ARecordThatAsksNothingIsSatisfiedByACityThatKnowsNothing(string knowledge)
		{
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(new List<string>(), knowledge));
			Assert.IsTrue(KingdomProcedureRules.KnowledgeMet(null, knowledge));
		}

		[Test]
		public void KnowledgeMet_ACityThatKnowsNothingIsRefusedByAnyGate()
		{
			Assert.IsFalse(KingdomProcedureRules.KnowledgeMet(null, "node:graft"));
			Assert.IsFalse(KingdomProcedureRules.KnowledgeMet(new List<string>(), "node:graft"));
		}

		// --- Preservation arithmetic (§3.5) --------------------------------------------------------

		[TestCase(5, 1, 5)]
		[TestCase(10, 1, 10)]
		[TestCase(5, 3, 15)]
		[TestCase(1, 1, 1)]
		[TestCase(0, 4, 4)]
		[TestCase(-3, 2, 2)]
		public void PreservedYield_IsVanillasOwnNumberTimesTheStackAndNothingElse(int number, int count, int expected)
		{
			// Campfire.PerformPreserve (D/…/Campfire.cs:543-557): PreservableItem.Number OVERWRITES
			// the seed and the charges, then `num3 *= go.Count`. Result is the blueprint handed
			// over, not a third factor — so the design note's "Result x Number x Count" is a product
			// of two, and this table is the correction.
			Assert.AreEqual(expected, KingdomProcedureRules.PreservedYield(number, count));
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void PreservedYield_NothingGoingInIsNothingComingOut(int count)
		{
			Assert.AreEqual(0, KingdomProcedureRules.PreservedYield(5, count));
		}

		[Test]
		public void PreservedYield_SaturatesRatherThanWrapping()
		{
			Assert.AreEqual(int.MaxValue, KingdomProcedureRules.PreservedYield(int.MaxValue, 3));
		}

		[TestCase(5)]
		[TestCase(10)]
		public void PreservedYield_VanillasShippedCalibrationLandsInTheBandTheDesignPredicted(int number)
		{
			// Bear meat gives 5, a dawnglider tail 10, a psychal gland 5. Three to eight per carcass
			// is the vanilla-shaped band §3.5 names; a single-stack carcass sits inside it or just
			// above, which is what makes "one creature, one limb" read correctly.
			int yield = KingdomProcedureRules.PreservedYield(number, 1);
			Assert.GreaterOrEqual(yield, 3);
			Assert.LessOrEqual(yield, 10);
		}

		[Test]
		public void VatWorked_IsZeroWheneverAnyTermIsZero()
		{
			// An idle vat preserves nothing, by arithmetic rather than by a special case: no grant
			// anywhere can make an unstaffed work produce (Addendum 8 clause 2).
			Assert.AreEqual(0, KingdomProcedureRules.VatWorked(10000L, 0, 100));
			Assert.AreEqual(0, KingdomProcedureRules.VatWorked(10000L, 100, 0));
			Assert.AreEqual(0, KingdomProcedureRules.VatWorked(0L, 100, 100));
			Assert.AreEqual(0, KingdomProcedureRules.VatWorked(-5L, 100, 100));
		}

		[Test]
		public void VatWorked_AFullyCrewedSoundVatWorksTheWholeElapsedTime()
		{
			Assert.AreEqual(1200, KingdomProcedureRules.VatWorked(1200L, 100, 100));
		}

		[Test]
		public void VatWorked_HalfACrewWorksLessThanAWholeOne()
		{
			// Mutation-resistant: inverting or dropping either term breaks this ordering.
			int whole = KingdomProcedureRules.VatWorked(1200L, 100, 100);
			int halfCrew = KingdomProcedureRules.VatWorked(1200L, 50, 100);
			int halfSound = KingdomProcedureRules.VatWorked(1200L, 100, 50);
			Assert.Less(halfCrew, whole);
			Assert.Less(halfSound, whole);
			Assert.AreEqual(halfCrew, halfSound);
		}

		[Test]
		public void StaffDayTicks_CountsStaffDaysAtTheSettlementsOwnDay()
		{
			Assert.AreEqual(6 * KingdomRules.TicksPerDay, KingdomProcedureRules.StaffDayTicks(6));
			Assert.AreEqual((int)KingdomRules.TicksPerDay, KingdomProcedureRules.StaffDayTicks(0));
			Assert.AreEqual((int)KingdomRules.TicksPerDay, KingdomProcedureRules.StaffDayTicks(-4));
		}

		// --- The mutation cap ------------------------------------------------------------------------

		[TestCase(0, 1)]
		[TestCase(1, 1)]
		[TestCase(2, 2)]
		[TestCase(3, 3)]
		[TestCase(4, 3)]
		[TestCase(10, 3)]
		[TestCase(-7, 1)]
		public void GrantedMutationLevel_IsNeverTheSourcesOwnLevel(int source, int expected)
		{
			// The single most load-bearing balance number in the wave. The mod this design learned
			// from is remembered for granting at the source's strength, and its own author wrote
			// down that it ruined the combat design.
			Assert.AreEqual(expected, KingdomProcedureRules.GrantedMutationLevel(source));
		}

		[Test]
		public void GrantedMutationLevel_NeverGrantsNothingAtAll()
		{
			for (int level = -5; level <= 20; level++)
			{
				int granted = KingdomProcedureRules.GrantedMutationLevel(level);
				Assert.GreaterOrEqual(granted, KingdomProcedureRules.MinMutationLevel);
				Assert.LessOrEqual(granted, KingdomProcedureRules.MaxMutationLevel);
			}
		}

		// --- Once, ever --------------------------------------------------------------------------------

		[Test]
		public void Latch_HoldsANamedProcedureForever()
		{
			string latch = KingdomProcedureRules.Latch("", "weepinggraft");
			Assert.IsTrue(KingdomProcedureRules.Latched(latch, "weepinggraft"));
			Assert.IsFalse(KingdomProcedureRules.Latched(latch, "coldregard"));
		}

		[Test]
		public void Latch_IsIdempotentSoNothingHasToRememberWhetherItAlreadyAsked()
		{
			string once = KingdomProcedureRules.Latch("", "coldregard");
			Assert.AreEqual(once, KingdomProcedureRules.Latch(once, "coldregard"));
			Assert.AreEqual(once, KingdomProcedureRules.Latch(once, "COLDREGARD"));
		}

		[Test]
		public void Latch_HoldsSeveralAndTellsThemApart()
		{
			string latch = KingdomProcedureRules.Latch(KingdomProcedureRules.Latch("", "weepinggraft"), "lanternrib");
			Assert.IsTrue(KingdomProcedureRules.Latched(latch, "weepinggraft"));
			Assert.IsTrue(KingdomProcedureRules.Latched(latch, "lanternrib"));
			Assert.IsFalse(KingdomProcedureRules.Latched(latch, "chimericconfession"));
		}

		[Test]
		public void Latch_MatchesTheWayAFounderWouldReadItAndNotTheWayAFileWroteIt()
		{
			string latch = KingdomProcedureRules.Latch("", "  ColdRegard  ");
			Assert.IsTrue(KingdomProcedureRules.Latched(latch, "coldregard"));
			Assert.IsTrue(KingdomProcedureRules.Latched(latch, "COLDREGARD"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("has|a|separator")]
		public void Latch_RefusesAKeyItCouldNotGiveBackWhole(string key)
		{
			Assert.AreEqual("weepinggraft", KingdomProcedureRules.Latch("weepinggraft", key));
		}

		[Test]
		public void Latched_IsFalseAgainstAnEmptyRecord()
		{
			Assert.IsFalse(KingdomProcedureRules.Latched(null, "weepinggraft"));
			Assert.IsFalse(KingdomProcedureRules.Latched("", "weepinggraft"));
			Assert.IsFalse(KingdomProcedureRules.Latched("weepinggraft", null));
		}

		// --- The whole verdict --------------------------------------------------------------------------

		[Test]
		public void Judge_RefusesANamedProcedureNobodyHasFoundWithoutNamingIt()
		{
			LabProcedure named = Parse("weepinggraft", "IV", "LiquidFont", "Back");
			Assert.AreEqual(LabVerdict.RefusedUndiscovered,
				KingdomProcedureRules.Judge(named, TrueKin(), null, 3, 4, Discovered: false, AlreadyDone: false));
			// And the refusal says nothing at all, because saying it would say the thing exists.
			Assert.AreEqual("", KingdomProcedureRules.RefusalLine(LabVerdict.RefusedUndiscovered, named));
		}

		[Test]
		public void Judge_RefusesANamedProcedureAlreadyPerformed()
		{
			LabProcedure named = Parse("coldregard", "IV", "NephalChord", "Face");
			Assert.AreEqual(LabVerdict.RefusedOnceEver,
				KingdomProcedureRules.Judge(named, TrueKin(), null, 3, 4, Discovered: true, AlreadyDone: true));
		}

		[Test]
		public void Judge_RefusesWorkTheHallIsNotBuiltHighEnoughFor()
		{
			LabProcedure limb = Parse("limb", "III", "Arm", "Arm", source: "limb");
			Assert.AreEqual(LabVerdict.RefusedRung,
				KingdomProcedureRules.Judge(limb, TrueKin(), null, 2, 9, true, false));
			Assert.AreEqual(LabVerdict.Allowed,
				KingdomProcedureRules.Judge(limb, TrueKin(), null, 3, 9, true, false));
		}

		[Test]
		public void Judge_RefusesWhatTheVatsAreNotKeeping()
		{
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Arm");
			sting.Preserved = 3;
			Assert.AreEqual(LabVerdict.RefusedUnkept,
				KingdomProcedureRules.Judge(sting, TrueKin(), null, 2, 2, true, false));
			Assert.AreEqual(LabVerdict.Allowed,
				KingdomProcedureRules.Judge(sting, TrueKin(), null, 2, 3, true, false));
		}

		[Test]
		public void Judge_AsksDiscoveryBeforeEverythingElseSoNothingLeaksThroughAnEarlierRefusal()
		{
			// A hall too low for an undiscovered procedure must still answer "undiscovered": the
			// rung refusal names the procedure, and naming it is the leak.
			LabProcedure named = Parse("weepinggraft", "IV", "LiquidFont", "Tail");
			Assert.AreEqual(LabVerdict.RefusedUndiscovered,
				KingdomProcedureRules.Judge(named, TrueKin(), null, 0, 0, Discovered: false, AlreadyDone: false));
		}

		// --- The words -------------------------------------------------------------------------------------

		[TestCase(LabVerdict.RefusedNoSlot)]
		[TestCase(LabVerdict.RefusedSlotTaken)]
		[TestCase(LabVerdict.RefusedCategory)]
		[TestCase(LabVerdict.RefusedRung)]
		[TestCase(LabVerdict.RefusedNoWeapon)]
		[TestCase(LabVerdict.RefusedUnkept)]
		[TestCase(LabVerdict.RefusedOnceEver)]
		[TestCase(LabVerdict.RefusedMagnitude)]
		public void RefusalLine_EveryRefusalSaysSomething(LabVerdict verdict)
		{
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Tail");
			string line = KingdomProcedureRules.RefusalLine(verdict, sting);
			Assert.IsNotEmpty(line);
			// STANDARDS 7b: a refusal names the thing, never the failure.
			StringAssert.DoesNotContain("failed", line.ToLowerInvariant());
			StringAssert.DoesNotContain("error", line.ToLowerInvariant());
		}

		[Test]
		public void RefusalLine_SaysNothingAboutTheAbsenceOfAProblem()
		{
			LabProcedure sting = Parse("sting", "I", "PoisonOnHit", "Arm");
			Assert.AreEqual("", KingdomProcedureRules.RefusalLine(LabVerdict.Allowed, sting));
		}

		[Test]
		public void RefusalLine_TheNoSlotRefusalNamesThePlaceTheFounderDoesNotHave()
		{
			LabProcedure tail = Parse("tail", "I", "StickOnHit", "Tail");
			StringAssert.Contains("tail", KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, tail));
		}

		[Test]
		public void RefusalLine_IsTotalOverANullRecord()
		{
			Assert.IsNotEmpty(KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, null));
			Assert.IsNotEmpty(KingdomProcedureRules.RefusalLine(LabVerdict.RefusedRung, null));
		}

		// --- The one sanctioned draw -----------------------------------------------------------------------

		[Test]
		public void ChooseChimericSlot_IsTheSameLimbOnTheSameSaveEveryTime()
		{
			// The gamble is taken once by the founder, not re-taken by the save file every time it
			// is opened. This is the whole reason it draws through the kernel.
			int first = KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", 41200uL, 19);
			for (int i = 0; i < 32; i++)
			{
				Assert.AreEqual(first, KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", 41200uL, 19));
			}
		}

		[Test]
		public void ChooseChimericSlot_TheTICKAloneChangesTheLimb()
		{
			// Asserted on its own rather than as one arm of an "or", because a draw that quietly
			// stopped reading the tick would hand every confession in one city the same limb — and
			// an "or" against the settlement id would still pass while that happened.
			int changes = 0;
			int first = KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", 0uL, 19);
			for (ulong ordinal = 1uL; ordinal <= 40uL; ordinal++)
			{
				if (KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", ordinal, 19) != first)
				{
					changes++;
				}
			}
			Assert.Greater(changes, 20, "the tick is not reaching the draw");
		}

		[Test]
		public void ChooseChimericSlot_TheSETTLEMENTAloneChangesTheLimb()
		{
			int changes = 0;
			int first = KingdomProcedureRules.ChooseChimericSlot("taf:settlement:aa", 41200uL, 19);
			string[] elsewhere = new string[6]
			{
				"taf:settlement:ab", "taf:settlement:ac", "taf:settlement:ad",
				"taf:settlement:ae", "taf:settlement:af", "taf:settlement:ag"
			};
			for (int i = 0; i < elsewhere.Length; i++)
			{
				if (KingdomProcedureRules.ChooseChimericSlot(elsewhere[i], 41200uL, 19) != first)
				{
					changes++;
				}
			}
			Assert.Greater(changes, 2, "the settlement is not reaching the draw");
		}

		[Test]
		public void ChooseChimericSlot_SpreadsOverTheWholeOfWhatWasOffered()
		{
			// A gamble that always answered three would be a gamble in name only.
			List<int> seen = new List<int>();
			for (ulong ordinal = 0uL; ordinal < 400uL; ordinal++)
			{
				int drawn = KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", ordinal, 19);
				if (!seen.Contains(drawn))
				{
					seen.Add(drawn);
				}
			}
			Assert.AreEqual(19, seen.Count);
		}

		[Test]
		public void ChooseChimericSlot_StaysInsideWhatTheGameOffered()
		{
			for (ulong ordinal = 0uL; ordinal < 200uL; ordinal++)
			{
				int drawn = KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", ordinal, 7);
				Assert.GreaterOrEqual(drawn, 0);
				Assert.Less(drawn, 7);
			}
		}

		[TestCase(0, -1)]
		[TestCase(-3, -1)]
		[TestCase(1, 0)]
		public void ChooseChimericSlot_HasNothingToChooseFromAndSaysSo(int candidates, int expected)
		{
			Assert.AreEqual(expected, KingdomProcedureRules.ChooseChimericSlot("taf:settlement:kavvat", 1uL, candidates));
		}

		[Test]
		public void ChooseChimericSlot_FallsBackToALimbRatherThanACrashWhenTheKernelRefuses()
		{
			// A settlement id the kernel's grammar will not accept must still end in a limb: the
			// founder paid for one.
			int drawn = KingdomProcedureRules.ChooseChimericSlot("NOT A LAWFUL ID", 1uL, 9);
			Assert.GreaterOrEqual(drawn, 0);
			Assert.Less(drawn, 9);
		}

		// --- The rung ladder -----------------------------------------------------------------------------------

		[TestCase(LabClass.Rider, KingdomProcedureRules.RungHall)]
		[TestCase(LabClass.Defence, KingdomProcedureRules.RungHall)]
		[TestCase(LabClass.Limb, KingdomProcedureRules.RungTheatre)]
		[TestCase(LabClass.Named, KingdomProcedureRules.RungTheatre)]
		public void RungForClass_PutsEachClassOfWorkWhereItIsDone(LabClass cls, int rung)
		{
			Assert.AreEqual(rung, KingdomProcedureRules.RungForClass(cls));
		}

		[TestCase(0, "the slab's")]
		[TestCase(1, "the vat-house's")]
		[TestCase(2, "the grafting hall's")]
		[TestCase(3, "the chimeric theatre's")]
		public void RungName_NamesEachRungTheWayAFounderWould(int rung, string expected)
		{
			Assert.AreEqual(expected, KingdomProcedureRules.RungName(rung));
		}
	}
}
#endif
