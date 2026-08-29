#if TAF_TESTS
using System;
using NUnit.Framework;

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
			Assert.IsTrue(KingdomSuccessionConfiguration.TryCreate("realm:one", choice, id,
				cost, revision, out KingdomSuccessionConfiguration value));
			return value;
		}

		[Test]
		public void FrozenOrdinalsAndDefaultWireAreCanonical()
		{
			Assert.AreEqual(0, (int)HeirChoice.Law);
			Assert.AreEqual(1, (int)HeirChoice.Chosen);
			Assert.AreEqual(2, (int)HeirChoice.Groomed);
			Assert.AreEqual(0, (int)SuccessionSelectionReason.Seniority);
			Assert.AreEqual(5, (int)SuccessionSelectionReason.ChosenAgreesWithLaw);
			Assert.AreEqual(10, (int)SuccessionSelectionReason.GroomedUnready);
			Assert.IsTrue(KingdomSuccessionConfiguration.TryDefault("realm:one", out var value));
			Assert.AreEqual("v2|cmVhbG06b25l|0|0|1|0",
				KingdomSuccessionConfiguration.Encode(value));
			Assert.IsTrue(KingdomSuccessionConfiguration.TryDecode(
				KingdomSuccessionConfiguration.Encode(value), out var decoded));
			Assert.AreEqual("realm:one", decoded.RealmId);
			Assert.AreEqual(HeirChoice.Law, decoded.Choice);
			Assert.AreEqual(0, decoded.ChosenResidentId);
			Assert.IsTrue(decoded.SeatCostEnabled);
			Assert.AreEqual(0, decoded.Revision);
		}

		[Test]
		public void ConfigurationCodecRoundTripsUnicodeChosenIdentity()
		{
			Assert.IsTrue(KingdomSuccessionConfiguration.TryCreate("realm:çavuş",
				HeirChoice.Chosen, 42, false, 17, out var value));
			string wire = KingdomSuccessionConfiguration.Encode(value);
			Assert.IsTrue(KingdomSuccessionConfiguration.TryDecode(wire, out var decoded));
			Assert.AreEqual("realm:çavuş", decoded.RealmId);
			Assert.AreEqual(42, decoded.ChosenResidentId);
			Assert.IsFalse(decoded.SeatCostEnabled);
			Assert.AreEqual(17, decoded.Revision);
			Assert.AreEqual(wire, KingdomSuccessionConfiguration.Encode(decoded));
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
			Assert.IsFalse(KingdomSuccessionConfiguration.TryDecode(wire, out _));
		}

		[Test]
		public void ConfigurationBoundsAndRevisionAreStrict()
		{
			Assert.IsFalse(KingdomSuccessionConfiguration.TryCreate("", HeirChoice.Law,
				0, true, 0, out _));
			Assert.IsFalse(KingdomSuccessionConfiguration.TryCreate(new string('r', 257),
				HeirChoice.Law, 0, true, 0, out _));
			var current = Config(HeirChoice.Law, 0);
			Assert.IsFalse(KingdomSuccessionConfiguration.TryRevise(current, HeirChoice.Law,
				0, true, out _));
			Assert.IsTrue(KingdomSuccessionConfiguration.TryRevise(current, HeirChoice.Chosen,
				9, true, out var next));
			Assert.AreEqual(1, next.Revision);
			var full = Config(HeirChoice.Law, 0, true, int.MaxValue);
			Assert.IsFalse(KingdomSuccessionConfiguration.TryRevise(full, HeirChoice.Chosen,
				9, true, out _));
		}

		[Test]
		public void VersionOneConfigurationMigratesWithoutInventingGrooming()
		{
			Assert.IsTrue(KingdomSuccessionConfiguration.TryDecode(
				"v1|cmVhbG06b25l|0|0|1|4", out var law));
			Assert.AreEqual("v2|cmVhbG06b25l|0|0|1|4",
				KingdomSuccessionConfiguration.Encode(law));
			Assert.IsTrue(KingdomSuccessionConfiguration.TryDecode(
				"v1|cmVhbG06b25l|1|7|0|5", out var chosen));
			Assert.AreEqual(HeirChoice.Chosen, chosen.Choice);
			Assert.IsFalse(KingdomSuccessionConfiguration.TryDecode(
				"v1|cmVhbG06b25l|2|7|1|5", out _));
			Assert.IsTrue(KingdomSuccessionConfiguration.TryCreate("realm:one",
				HeirChoice.Groomed, 7, true, 6, out var groomed));
			Assert.AreEqual("v2|cmVhbG06b25l|2|7|1|6",
				KingdomSuccessionConfiguration.Encode(groomed));
			Assert.IsFalse(KingdomSuccessionConfiguration.TryCreate("realm:one",
				HeirChoice.Groomed, 7, false, 6, out _));
		}

		[Test]
		public void SeniorityAndChosenIdentityResolveIndependently()
		{
			var candidates = new[] { H("Bela", 10, 1), H("Ari", 20, 2), H("Bela", 5, 3) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Law, 0), out var law));
			Assert.AreEqual(2, law.HeirIndex);
			Assert.AreEqual(HeirChoice.Law, law.Choice);
			Assert.IsFalse(law.CostsTheSeat);
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 2), out var chosen));
			Assert.AreEqual(1, chosen.HeirIndex);
			Assert.AreEqual(2, chosen.LawHeirIndex);
			Assert.AreEqual(HeirChoice.Chosen, chosen.Choice);
			Assert.IsTrue(chosen.CostsTheSeat);
			Assert.AreEqual(SuccessionSelectionReason.Chosen, chosen.Reason);
		}

		[Test]
		public void ChosenSeatToggleAndLawAgreementNeverMisprice()
		{
			var candidates = new[] { H("Senior", 1, 1), H("Junior", 2, 2) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 2, false), out var free));
			Assert.AreEqual(HeirChoice.Chosen, free.Choice);
			Assert.IsFalse(free.CostsTheSeat);
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 1), out var agrees));
			Assert.AreEqual(HeirChoice.Law, agrees.Choice);
			Assert.AreEqual(SuccessionSelectionReason.ChosenAgreesWithLaw, agrees.Reason);
			Assert.IsFalse(agrees.CostsTheSeat);
		}

		[TestCase(99, SuccessionSelectionReason.ChosenMissing)]
		[TestCase(2, SuccessionSelectionReason.ChosenIneligible)]
		public void MissingOrDepartedChosenIdentityFallsBackOnlyToSeniority(int id,
			SuccessionSelectionReason reason)
		{
			var candidates = new[] { H("Senior", 1, 1), H("Departed", 0, 2, false) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, id), out var result));
			Assert.AreEqual(0, result.HeirIndex);
			Assert.AreEqual(HeirChoice.Law, result.Choice);
			Assert.AreEqual(reason, result.Reason);
			Assert.IsFalse(result.CostsTheSeat);
		}

		[Test]
		public void DuplicateChosenIdentityIsAmbiguousEvenWhenOneRowIsEligible()
		{
			var candidates = new[] { H("Senior", 1, 1), H("Exact", 2, 7),
				H("Old record", 3, 7, false) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 7), out var result));
			Assert.AreEqual(0, result.HeirIndex);
			Assert.AreEqual(SuccessionSelectionReason.ChosenAmbiguous, result.Reason);
			Assert.AreEqual(HeirChoice.Law, result.Choice);
		}

		[Test]
		public void DuplicateNamesDoNotSubstituteForExactResidentId()
		{
			var candidates = new[] { H("Same", 1, 10), H("Same", 2, 11) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Chosen, 11), out var result));
			Assert.AreEqual(1, result.HeirIndex);
			Assert.AreEqual(10, candidates[result.LawHeirIndex].ResidentId);
		}

		[Test]
		public void EmptyEligibleRollCannotResolve()
		{
			Assert.IsFalse(KingdomSuccessionRules.TryResolveConfiguredHeir(
				new[] { H("Dead", 1, 1, false) }, Config(HeirChoice.Law, 0), out _));
		}

		[Test]
		public void ReadyGroomedIdentityInheritsLawfullyWithoutChosenSeatCost()
		{
			var candidates = new[] { H("Senior", 1, 1), H("Student", 2, 7) };
			var config = Config(HeirChoice.Groomed, 7);
			Assert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Student",
				10L, 2, 2, 3, out var grooming));
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				config, grooming, true, out var result));
			Assert.AreEqual(1, result.HeirIndex);
			Assert.AreEqual(0, result.LawHeirIndex);
			Assert.AreEqual(HeirChoice.Groomed, result.Choice);
			Assert.AreEqual(SuccessionSelectionReason.Groomed, result.Reason);
			Assert.IsFalse(result.CostsTheSeat);
		}

		[TestCase(2, 1, true, SuccessionSelectionReason.GroomedUnready)]
		[TestCase(2, 2, false, SuccessionSelectionReason.GroomedMissing)]
		public void UnreadyOrAbsentGroomingFallsBackToSeniorityWithoutCost(int service,
			int study, bool present, SuccessionSelectionReason reason)
		{
			var candidates = new[] { H("Senior", 1, 1), H("Student", 2, 7) };
			Assert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Student",
				10L, service, study, 0, out var grooming));
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(candidates,
				Config(HeirChoice.Groomed, 7), grooming, present, out var result));
			Assert.AreEqual(0, result.HeirIndex);
			Assert.AreEqual(HeirChoice.Law, result.Choice);
			Assert.AreEqual(reason, result.Reason);
			Assert.IsFalse(result.CostsTheSeat);
		}

		[Test]
		public void GroomingStillRequiresExactRealmResidentAndUniqueEligibleRow()
		{
			Assert.IsTrue(KingdomGroomingRecord.TryCreate("realm:other", 7, "Student",
				10L, 2, 2, 0, out var foreign));
			var baseCandidates = new[] { H("Senior", 1, 1), H("Student", 2, 7) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(baseCandidates,
				Config(HeirChoice.Groomed, 7), foreign, true, out var mismatch));
			Assert.AreEqual(SuccessionSelectionReason.GroomedMissing, mismatch.Reason);
			Assert.IsTrue(KingdomGroomingRecord.TryCreate("realm:one", 7, "Student",
				10L, 2, 2, 0, out var exact));
			var duplicate = new[] { H("Senior", 1, 1), H("Student", 2, 7),
				H("Old row", 3, 7, false) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(duplicate,
				Config(HeirChoice.Groomed, 7), exact, true, out var ambiguous));
			Assert.AreEqual(SuccessionSelectionReason.GroomedAmbiguous, ambiguous.Reason);
			var departed = new[] { H("Senior", 1, 1), H("Student", 2, 7, false) };
			Assert.IsTrue(KingdomSuccessionRules.TryResolveConfiguredHeir(departed,
				Config(HeirChoice.Groomed, 7), exact, true, out var ineligible));
			Assert.AreEqual(SuccessionSelectionReason.GroomedIneligible, ineligible.Reason);
		}

		[Test]
		public void SelectionReceiptRoundTripsAndRejectsContradictions()
		{
			string death = KingdomSuccessionRules.FounderDeathToken(1, 42L, "body:founder");
			Assert.IsTrue(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 3,
				2, "Chosen", 1, "Senior", HeirChoice.Chosen, true,
				SuccessionSelectionReason.Chosen, out var receipt));
			string wire = KingdomSuccessionSelectionReceipt.Encode(receipt);
			Assert.IsTrue(KingdomSuccessionSelectionReceipt.TryDecode(wire, out var decoded));
			Assert.AreEqual(2, decoded.HeirResidentId);
			Assert.AreEqual(1, decoded.LawHeirResidentId);
			Assert.IsTrue(decoded.CostsTheSeat);
			Assert.AreEqual(wire, KingdomSuccessionSelectionReceipt.Encode(decoded));
			Assert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 3,
				1, "Same", 1, "Same", HeirChoice.Chosen, true,
				SuccessionSelectionReason.Chosen, out _));
			Assert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 3,
				1, "Senior", 1, "Senior", HeirChoice.Law, true,
				SuccessionSelectionReason.Seniority, out _));
			Assert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", "not-a-death",
				3, 1, "Senior", 1, "Senior", HeirChoice.Law, false,
				SuccessionSelectionReason.Seniority, out _));
			Assert.IsFalse(KingdomSuccessionSelectionReceipt.TryDecode(
				wire.Replace("|1|1", "|01|1"), out _));
		}

		[Test]
		public void GroomedReceiptIsDistinctFromChosenLifeAndCannotCostSeat()
		{
			string death = KingdomSuccessionRules.FounderDeathToken(2, 84L, "body:founder");
			Assert.IsTrue(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				7, "Student", 1, "Senior", HeirChoice.Groomed, false,
				SuccessionSelectionReason.Groomed, out var receipt));
			string wire = KingdomSuccessionSelectionReceipt.Encode(receipt);
			Assert.IsTrue(KingdomSuccessionSelectionReceipt.TryDecode(wire, out var decoded));
			Assert.AreEqual(HeirChoice.Groomed, decoded.Choice);
			Assert.IsFalse(decoded.CostsTheSeat);
			Assert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				7, "Student", 1, "Senior", HeirChoice.Groomed, true,
				SuccessionSelectionReason.Groomed, out _));
			Assert.IsFalse(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				7, "Student", 1, "Senior", HeirChoice.Groomed, false,
				SuccessionSelectionReason.Chosen, out _));
			Assert.IsTrue(KingdomSuccessionSelectionReceipt.TryCreate("realm:one", death, 8,
				1, "Senior", 1, "Senior", HeirChoice.Law, false,
				SuccessionSelectionReason.GroomedUnready, out var fallback));
			Assert.IsTrue(KingdomSuccessionSelectionReceipt.TryDecode(
				KingdomSuccessionSelectionReceipt.Encode(fallback), out _));
		}

		[Test]
		public void ChronicleIdentityAndChosenSeatThresholdAreStable()
		{
			string a = KingdomSuccessionRules.ConfigurationEventId("realm:one", 1);
			Assert.IsNotEmpty(a);
			Assert.AreEqual(a, KingdomSuccessionRules.ConfigurationEventId("realm:one", 1));
			Assert.AreNotEqual(a, KingdomSuccessionRules.ConfigurationEventId("realm:one", 2));
			Assert.AreEqual(KingdomExileRules.RegardLiked,
				KingdomSuccessionRules.ChosenSeatReturnRegard);
			Assert.IsFalse(KingdomSuccessionRules.ChosenSeatMayReturn(true, 249));
			Assert.IsTrue(KingdomSuccessionRules.ChosenSeatMayReturn(true, 250));
			Assert.IsTrue(KingdomSuccessionRules.ChosenSeatMayReturn(false, -1000));
		}
	}
}
#endif
