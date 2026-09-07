#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomSuccessionConfigurationRulesTests
	{
		private static KingdomHeir H(string name, long tick, int id, bool eligible = true)
		{
			return new KingdomHeir(name, tick, null, null, eligible, "zone:" + id, id);
		}

		private static KingdomSuccessionConfiguration Config(HeirChoice choice, int id,
			bool cost = true, int revision = 0)
		{
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryCreate("realm:one", choice, id,
				cost, revision, out KingdomSuccessionConfiguration value));
			return value;
		}

		[Test]
		public void FrozenOrdinalsAndDefaultWireAreCanonical()
		{
			ClassicAssert.AreEqual(0, (int)HeirChoice.Law);
			ClassicAssert.AreEqual(1, (int)HeirChoice.Chosen);
			ClassicAssert.AreEqual(2, (int)HeirChoice.Groomed);
			ClassicAssert.AreEqual(0, (int)SuccessionSelectionReason.Seniority);
			ClassicAssert.AreEqual(5, (int)SuccessionSelectionReason.ChosenAgreesWithLaw);
			ClassicAssert.AreEqual(10, (int)SuccessionSelectionReason.GroomedUnready);
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryDefault("realm:one", out var value));
			ClassicAssert.AreEqual("v2|cmVhbG06b25l|0|0|1|0",
				KingdomSuccessionConfiguration.Encode(value));
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryDecode(
				KingdomSuccessionConfiguration.Encode(value), out var decoded));
			ClassicAssert.AreEqual("realm:one", decoded.RealmId);
			ClassicAssert.AreEqual(HeirChoice.Law, decoded.Choice);
			ClassicAssert.AreEqual(0, decoded.ChosenResidentId);
			ClassicAssert.IsTrue(decoded.SeatCostEnabled);
			ClassicAssert.AreEqual(0, decoded.Revision);
		}

		[Test]
		public void ConfigurationCodecRoundTripsUnicodeChosenIdentity()
		{
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryCreate("realm:çavuş",
				HeirChoice.Chosen, 42, false, 17, out var value));
			string wire = KingdomSuccessionConfiguration.Encode(value);
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryDecode(wire, out var decoded));
			ClassicAssert.AreEqual("realm:çavuş", decoded.RealmId);
			ClassicAssert.AreEqual(42, decoded.ChosenResidentId);
			ClassicAssert.IsFalse(decoded.SeatCostEnabled);
			ClassicAssert.AreEqual(17, decoded.Revision);
			ClassicAssert.AreEqual(wire, KingdomSuccessionConfiguration.Encode(decoded));
		}

		[TestCase("")]
		[TestCase("v1|cmVhbG06b25l|00|0|1|0")]
		[TestCase("v1|cmVhbG06b25l|0|00|1|0")]
		[TestCase("v1|cmVhbG06b25l|0|0|2|0")]
		[TestCase("v1|cmVhbG06b25l|0|0|0|0")]
		[TestCase("v1|cmVhbG06b25l|2|0|1|0")]
		[TestCase("v2|cmVhbG06b25l|2|0|1|0")]
		[TestCase("v2|cmVhbG06b25l|2|7|0|0")]
		[TestCase("v1|@@@|0|0|1|0")]
		[TestCase("v1|cmVhbG06b25l|0|1|1|0")]
		[TestCase("v1|cmVhbG06b25l|1|0|1|0")]
		[TestCase("v1|cmVhbG06b25l|0|0|1|-1")]
		public void ConfigurationCodecRejectsMalformedOrNoncanonicalWire(string wire)
		{
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryDecode(wire, out _));
		}

		[Test]
		public void ConfigurationBoundsAndRevisionAreStrict()
		{
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryCreate("", HeirChoice.Law,
				0, true, 0, out _));
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryCreate(new string('r', 257),
				HeirChoice.Law, 0, true, 0, out _));
			var current = Config(HeirChoice.Law, 0);
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryRevise(current, HeirChoice.Law,
				0, true, out _));
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryRevise(current, HeirChoice.Chosen,
				9, true, out var next));
			ClassicAssert.AreEqual(1, next.Revision);
			var full = Config(HeirChoice.Law, 0, true, int.MaxValue);
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryRevise(full, HeirChoice.Chosen,
				9, true, out _));
		}

		[Test]
		public void VersionOneConfigurationMigratesWithoutInventingGrooming()
		{
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryDecode(
				"v1|cmVhbG06b25l|0|0|1|4", out var law));
			ClassicAssert.AreEqual("v2|cmVhbG06b25l|0|0|1|4",
				KingdomSuccessionConfiguration.Encode(law));
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryDecode(
				"v1|cmVhbG06b25l|1|7|0|5", out var chosen));
			ClassicAssert.AreEqual(HeirChoice.Chosen, chosen.Choice);
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryDecode(
				"v1|cmVhbG06b25l|2|7|1|5", out _));
			ClassicAssert.IsTrue(KingdomSuccessionConfiguration.TryCreate("realm:one",
				HeirChoice.Groomed, 7, true, 6, out var groomed));
			ClassicAssert.AreEqual("v2|cmVhbG06b25l|2|7|1|6",
				KingdomSuccessionConfiguration.Encode(groomed));
			ClassicAssert.IsFalse(KingdomSuccessionConfiguration.TryCreate("realm:one",
				HeirChoice.Groomed, 7, false, 6, out _));
		}

		[Test]
		public void SeniorityAndChosenIdentityResolveIndependently()
		{
			var candidates = new[] { H("Bela", 10, 1), H("Ari", 20, 2), H("Bela", 5, 3) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Law, 0), out var law));
			ClassicAssert.AreEqual(2, law.HeirIndex);
			ClassicAssert.AreEqual(HeirChoice.Law, law.Choice);
			ClassicAssert.IsFalse(law.CostsTheSeat);
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 2), out var chosen));
			ClassicAssert.AreEqual(1, chosen.HeirIndex);
			ClassicAssert.AreEqual(2, chosen.LawHeirIndex);
			ClassicAssert.AreEqual(HeirChoice.Chosen, chosen.Choice);
			ClassicAssert.IsTrue(chosen.CostsTheSeat);
			ClassicAssert.AreEqual(SuccessionSelectionReason.Chosen, chosen.Reason);
		}

		[Test]
		public void ChosenSeatToggleAndLawAgreementNeverMisprice()
		{
			var candidates = new[] { H("Senior", 1, 1), H("Junior", 2, 2) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 2, false), out var free));
			ClassicAssert.AreEqual(HeirChoice.Chosen, free.Choice);
			ClassicAssert.IsFalse(free.CostsTheSeat);
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 1), out var agrees));
			ClassicAssert.AreEqual(HeirChoice.Law, agrees.Choice);
			ClassicAssert.AreEqual(SuccessionSelectionReason.ChosenAgreesWithLaw, agrees.Reason);
			ClassicAssert.IsFalse(agrees.CostsTheSeat);
		}

		[TestCase(99, SuccessionSelectionReason.ChosenMissing)]
		[TestCase(2, SuccessionSelectionReason.ChosenIneligible)]
		public void MissingOrDepartedChosenIdentityFallsBackOnlyToSeniority(int id,
			SuccessionSelectionReason reason)
		{
			var candidates = new[] { H("Senior", 1, 1), H("Departed", 0, 2, false) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, id), out var result));
			ClassicAssert.AreEqual(0, result.HeirIndex);
			ClassicAssert.AreEqual(HeirChoice.Law, result.Choice);
			ClassicAssert.AreEqual(reason, result.Reason);
			ClassicAssert.IsFalse(result.CostsTheSeat);
		}

		[Test]
		public void DuplicateChosenIdentityIsAmbiguousEvenWhenOneRowIsEligible()
		{
			var candidates = new[] { H("Senior", 1, 1), H("Exact", 2, 7),
				H("Old record", 3, 7, false) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 7), out var result));
			ClassicAssert.AreEqual(0, result.HeirIndex);
			ClassicAssert.AreEqual(SuccessionSelectionReason.ChosenAmbiguous, result.Reason);
			ClassicAssert.AreEqual(HeirChoice.Law, result.Choice);
		}

		[Test]
		public void DuplicateNamesDoNotSubstituteForExactResidentId()
		{
			var candidates = new[] { H("Same", 1, 10), H("Same", 2, 11) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 11), out var result));
			ClassicAssert.AreEqual(1, result.HeirIndex);
			ClassicAssert.AreEqual(10, candidates[result.LawHeirIndex].ResidentId);
		}

		[Test]
		public void EmptyEligibleRollCannotResolve()
		{
			ClassicAssert.IsFalse(KingdomSuccessionRules.TryResolveConfiguredHeir(
				new[] { H("Dead", 1, 1, false) }, Config(HeirChoice.Law, 0), out _));
		}

		[Test]
		public void ReadyGroomedIdentityInheritsLawfullyWithoutChosenSeatCost()
		{
			var candidates = new[] { H("Senior", 1, 1), H("Student", 2, 7) };
			var config = Config(HeirChoice.Groomed, 7);
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Student",
				10L, 2, 2, 3, out var grooming));
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				config, grooming, true, out var result));
			ClassicAssert.AreEqual(1, result.HeirIndex);
			ClassicAssert.AreEqual(0, result.LawHeirIndex);
			ClassicAssert.AreEqual(HeirChoice.Groomed, result.Choice);
			ClassicAssert.AreEqual(SuccessionSelectionReason.Groomed, result.Reason);
			ClassicAssert.IsFalse(result.CostsTheSeat);
		}

		[TestCase(2, 1, true, SuccessionSelectionReason.GroomedUnready)]
		[TestCase(2, 2, false, SuccessionSelectionReason.GroomedMissing)]
		public void UnreadyOrAbsentGroomingFallsBackToSeniorityWithoutCost(int service,
			int study, bool present, SuccessionSelectionReason reason)
		{
			var candidates = new[] { H("Senior", 1, 1), H("Student", 2, 7) };
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Student",
				10L, service, study, 0, out var grooming));
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Groomed, 7), grooming, present, out var result));
			ClassicAssert.AreEqual(0, result.HeirIndex);
			ClassicAssert.AreEqual(HeirChoice.Law, result.Choice);
			ClassicAssert.AreEqual(reason, result.Reason);
			ClassicAssert.IsFalse(result.CostsTheSeat);
		}

		[Test]
		public void GroomingStillRequiresExactRealmResidentAndUniqueEligibleRow()
		{
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryCreate("realm:other", 7, "Student",
				10L, 2, 2, 0, out var foreign));
			var baseCandidates = new[] { H("Senior", 1, 1), H("Student", 2, 7) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(baseCandidates,
				Config(HeirChoice.Groomed, 7), foreign, true, out var mismatch));
			ClassicAssert.AreEqual(SuccessionSelectionReason.GroomedMissing, mismatch.Reason);
			ClassicAssert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Student",
				10L, 2, 2, 0, out var exact));
			var duplicate = new[] { H("Senior", 1, 1), H("Student", 2, 7),
				H("Old row", 3, 7, false) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(duplicate,
				Config(HeirChoice.Groomed, 7), exact, true, out var ambiguous));
			ClassicAssert.AreEqual(SuccessionSelectionReason.GroomedAmbiguous, ambiguous.Reason);
			var departed = new[] { H("Senior", 1, 1), H("Student", 2, 7, false) };
			ClassicAssert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(departed,
				Config(HeirChoice.Groomed, 7), exact, true, out var ineligible));
			ClassicAssert.AreEqual(SuccessionSelectionReason.GroomedIneligible, ineligible.Reason);
		}

		[Test]
		public void SelectionReceiptRoundTripsAndRejectsContradictions()
		{
			string death = KingdomSuccessionRules.FounderDeathToken(1, 42L, "body:founder");
			ClassicAssert.IsTrue(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 3,
				2, "Chosen", 1, "Senior", HeirChoice.Chosen, true,
				SuccessionSelectionReason.Chosen, out var receipt));
			string wire = KingdomSuccessionSelectionReceipt.Encode(receipt);
			ClassicAssert.IsTrue(KingdomSuccessionSelectionReceipt.TryDecode(wire, out var decoded));
			ClassicAssert.AreEqual(2, decoded.HeirResidentId);
			ClassicAssert.AreEqual(1, decoded.LawHeirResidentId);
			ClassicAssert.IsTrue(decoded.CostsTheSeat);
			ClassicAssert.AreEqual(wire, KingdomSuccessionSelectionReceipt.Encode(decoded));
			ClassicAssert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 3,
				1, "Same", 1, "Same", HeirChoice.Chosen, true,
				SuccessionSelectionReason.Chosen, out _));
			ClassicAssert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 3,
				1, "Senior", 1, "Senior", HeirChoice.Law, true,
				SuccessionSelectionReason.Seniority, out _));
			ClassicAssert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", "not-a-death",
				3, 1, "Senior", 1, "Senior", HeirChoice.Law, false,
				SuccessionSelectionReason.Seniority, out _));
			ClassicAssert.IsFalse(KingdomSuccessionSelectionReceipt.TryDecode(
				wire.Replace("|1|1", "|01|1"), out _));
		}

		[Test]
		public void GroomedReceiptIsDistinctFromChosenLifeAndCannotCostSeat()
		{
			string death = KingdomSuccessionRules.FounderDeathToken(2, 84L, "body:founder");
			ClassicAssert.IsTrue(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				7, "Student", 1, "Senior", HeirChoice.Groomed, false,
				SuccessionSelectionReason.Groomed, out var receipt));
			string wire = KingdomSuccessionSelectionReceipt.Encode(receipt);
			ClassicAssert.IsTrue(KingdomSuccessionSelectionReceipt.TryDecode(wire, out var decoded));
			ClassicAssert.AreEqual(HeirChoice.Groomed, decoded.Choice);
			ClassicAssert.IsFalse(decoded.CostsTheSeat);
			ClassicAssert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				7, "Student", 1, "Senior", HeirChoice.Groomed, true,
				SuccessionSelectionReason.Groomed, out _));
			ClassicAssert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				7, "Student", 1, "Senior", HeirChoice.Groomed, false,
				SuccessionSelectionReason.Chosen, out _));
			ClassicAssert.IsTrue(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				1, "Senior", 1, "Senior", HeirChoice.Law, false,
				SuccessionSelectionReason.GroomedUnready, out var fallback));
			ClassicAssert.IsTrue(KingdomSuccessionSelectionReceipt.TryDecode(
				KingdomSuccessionSelectionReceipt.Encode(fallback), out _));
		}

		[Test]
		public void ChronicleIdentityAndChosenSeatThresholdAreStable()
		{
			string a = KingdomSuccessionRules.ConfigurationEventId("realm:one", 1);
			ClassicAssert.IsNotEmpty(a);
			ClassicAssert.AreEqual(a, KingdomSuccessionRules.ConfigurationEventId("realm:one", 1));
			ClassicAssert.AreNotEqual(a, KingdomSuccessionRules.ConfigurationEventId("realm:one", 2));
			ClassicAssert.AreEqual(KingdomExileRules.RegardLiked,
				KingdomSuccessionRules.ChosenSeatReturnRegard);
			ClassicAssert.IsFalse(KingdomSuccessionRules.ChosenSeatMayReturn(true, 249));
			ClassicAssert.IsTrue(KingdomSuccessionRules.ChosenSeatMayReturn(true, 250));
			ClassicAssert.IsTrue(KingdomSuccessionRules.ChosenSeatMayReturn(false, -1000));
		}
	}
}
#endif
