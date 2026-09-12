#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>VALUE tests for the crew-availability verdict the teardown fixture consults
	/// (run 39 retry 2: a crew body staged for the completed fire's raising happening was refused
	/// as if it had left). Pure and engine-free; the source pins over how RequireAvailable calls
	/// this class live in DevTests/KingdomTeardownScenarioSourceTests.cs.</summary>
	[TestFixture]
	public sealed class KingdomTeardownCrewAvailabilityRulesTests
	{
		[Test]
		public void AStagedResidentGroundedBodyIsAccepted()
		{
			ClassicAssert.AreEqual(KingdomTeardownCrewAvailabilityRules.Verdict.Accepted,
				KingdomTeardownCrewAvailabilityRules.Judge(true, true, true),
				"staged for a physical happening is healthy crew work");
			ClassicAssert.AreEqual(KingdomTeardownCrewAvailabilityRules.Verdict.Accepted,
				KingdomTeardownCrewAvailabilityRules.Judge(true, true, false));
			ClassicAssert.IsNull(KingdomTeardownCrewAvailabilityRules.Refusal(
				KingdomTeardownCrewAvailabilityRules.Verdict.Accepted, 2, 2));
		}

		[Test]
		public void ANotResidentBodyIsRefusedWhetherOrNotStaged()
		{
			foreach (bool staged in new[] { false, true })
				foreach (bool grounded in new[] { false, true })
					ClassicAssert.AreEqual(KingdomTeardownCrewAvailabilityRules.Verdict.RefusedNotResident,
						KingdomTeardownCrewAvailabilityRules.Judge(false, grounded, staged),
						"staged=" + staged + " grounded=" + grounded);
			StringAssert.Contains("crew body 2 of 2 is not present in the production AvailableSettlers projection because it is not Resident standing",
				KingdomTeardownCrewAvailabilityRules.Refusal(
					KingdomTeardownCrewAvailabilityRules.Verdict.RefusedNotResident, 2, 2));
		}

		[Test]
		public void AnUngroundedResidentIsRefusedWhetherOrNotStaged()
		{
			foreach (bool staged in new[] { false, true })
				ClassicAssert.AreEqual(KingdomTeardownCrewAvailabilityRules.Verdict.RefusedUngrounded,
					KingdomTeardownCrewAvailabilityRules.Judge(true, false, staged), "staged=" + staged);
			StringAssert.Contains("not grounded on this zone", KingdomTeardownCrewAvailabilityRules.Refusal(
				KingdomTeardownCrewAvailabilityRules.Verdict.RefusedUngrounded, 1, 2));
		}

		[Test]
		public void TheJournalClauseNamesStagedAndStandingPerBody()
		{
			ClassicAssert.AreEqual("staged=True standing=Resident grounded=True available=False",
				KingdomTeardownCrewAvailabilityRules.Describe(true, true, true, false));
			ClassicAssert.AreEqual("staged=False standing=NotResident grounded=True available=False",
				KingdomTeardownCrewAvailabilityRules.Describe(false, true, false, false));
		}
	}
}
#endif
