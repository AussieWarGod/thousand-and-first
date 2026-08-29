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
			int reserve = runtime.IndexOf("TryReserveAmbientPlan", assign);
			int plan = runtime.IndexOf("KingdomPolityCohortRules.TryPlan", reserve);
			Assert.GreaterOrEqual(assign, 0); Assert.Greater(reserve, assign);
			Assert.Greater(plan, reserve);
			StringAssert.Contains("PolityId = assignment.PolityId", runtime);
			StringAssert.Contains("Present(S, cohort, loadedSettlementId)", runtime);
		}

		[Test]
		public void ExternalTrafficHasFreshIdsAndCannotBecomeWarOrRemoteSimulation()
		{
			string rules = Read("KingdomPolityRivalTrafficRules.cs");
			string runtime = Read("KingdomPolitySchedulerRuntime.cs");
			StringAssert.Contains("polity-external-traffic-cohort-v1", rules);
			StringAssert.Contains("polity-external-traffic-event-v1", rules);
			StringAssert.Contains("HasFactionProjection", rules);
			StringAssert.Contains("Purpose == KingdomPolityCohortPurpose.Guard", rules);
			StringAssert.Contains("Due.Purpose != KingdomPolityCohortPurpose.Warband", rules);
			StringAssert.DoesNotContain("TryOpenGrievance", rules + runtime);
			StringAssert.DoesNotContain("TryPlanTerms", rules + runtime);
			StringAssert.DoesNotContain("ZoneManager", rules + runtime);
			StringAssert.DoesNotContain("GetZone", rules + runtime);
			StringAssert.DoesNotContain("GameObject", rules);
		}

		[Test]
		public void PresentationNamesExternalOwnerWithoutInventingOutcome()
		{
			string runtime = Read("KingdomPolitySchedulerRuntime.cs");
			StringAssert.Contains("KingdomPresentation.Rich(polity.DisplayName)", runtime);
			StringAssert.Contains("is sighted at the boundary", runtime);
			StringAssert.DoesNotContain("winner", runtime.ToLowerInvariant());
			StringAssert.DoesNotContain("conquest", runtime.ToLowerInvariant());
		}
	}
}
#endif
