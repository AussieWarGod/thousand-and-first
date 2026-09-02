#if TAF_TESTS
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityRivalTrafficSourceTests
	{
		private static string Read(string Name)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Polity", Name));
		}

		[Test]
		public void SchedulerUsesTypedAssignmentBeforePlanAndKeepsSharedCapacity()
		{
			string runtime = Read("KingdomPolitySchedulerRuntime.cs");
			int assign = runtime.IndexOf("KingdomPolityRivalTrafficRules.TryAssign");
			int freeze = runtime.IndexOf("KingdomPolityAmbientTransactionRules.TryFreeze", assign);
			int reserve = runtime.IndexOf("TryReserveAmbientPlan", freeze);
			int plan = runtime.IndexOf("KingdomPolityCohortRules.TryPlan", reserve);
			Assert.GreaterOrEqual(assign, 0); Assert.Greater(freeze, assign);
			Assert.Greater(reserve, freeze);
			Assert.Greater(plan, reserve);
			StringAssert.Contains("PolityId = assignment.PolityId", runtime);
			StringAssert.Contains("Present(S, cohort, loadedSettlementId)", runtime);
		}

		[Test]
		public void ExternalTrafficIsUnavailableWithoutExactSettlementProvenance()
		{
			string rules = Read("KingdomPolityRivalTrafficRules.cs");
			string runtime = Read("KingdomPolitySchedulerRuntime.cs");
			StringAssert.Contains("V8 has no external settlement/zone carrier", rules);
			StringAssert.Contains("return !Value.External", rules);
			StringAssert.DoesNotContain("polity-external-traffic-cohort-v1", rules);
			StringAssert.DoesNotContain("polity-external-traffic-event-v1", rules);
			StringAssert.Contains("Due.Purpose != KingdomPolityCohortPurpose.Warband", rules);
			StringAssert.DoesNotContain("TryOpenGrievance", rules + runtime);
			StringAssert.DoesNotContain("TryPlanTerms", rules + runtime);
			StringAssert.DoesNotContain("ZoneManager", rules + runtime);
			StringAssert.DoesNotContain("GetZone", rules + runtime);
			StringAssert.DoesNotContain("GameObject", rules);
		}

		[Test]
		public void PresentationUsesFrozenEndpointNamesWithoutInventingOutcome()
		{
			string runtime = Read("KingdomPolitySchedulerRuntime.cs") +
				Read("KingdomPolityArrivalPresentationRules.cs");
			StringAssert.Contains("t.SourceSettlementName", runtime);
			StringAssert.Contains("t.DestinationSettlementName", runtime);
			StringAssert.DoesNotContain("is sighted at the boundary", runtime);
			StringAssert.DoesNotContain("winner", runtime.ToLowerInvariant());
			StringAssert.DoesNotContain("conquest", runtime.ToLowerInvariant());
		}
	}
}
#endif
