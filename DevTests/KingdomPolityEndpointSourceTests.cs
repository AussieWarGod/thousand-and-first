#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityEndpointSourceTests
	{
		private static string Read(string Name)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Polity", Name));
		}

		[Test]
		public void PreparedReceiptAndObjectIdsExistBeforeAnyFreshBodyOrPlacement()
		{
			string runtime = Read("KingdomPolityEndpointRuntime.cs");
			int prepare = runtime.IndexOf("TryPrepareEndpointManifestation", StringComparison.Ordinal);
			int create = runtime.IndexOf("KingdomPolityNpcRuntime.TryCreate", StringComparison.Ordinal);
			int assign = runtime.IndexOf("created.ID = objectId", StringComparison.Ordinal);
			int place = runtime.IndexOf("cell.AddObject(created)", StringComparison.Ordinal);
			Assert.GreaterOrEqual(prepare, 0); Assert.Greater(create, prepare);
			Assert.Greater(assign, create); Assert.Greater(place, assign);
			string authority = Read("KingdomPolityCohortRules.Manifestation.cs");
			StringAssert.Contains("ObjectIds = objects", authority);
			StringAssert.Contains("Phase = KingdomPolityProjectionPhase.Prepared", authority);
		}

		[Test]
		public void AdapterAdmitsOnlyExactLoadedOwnedSettlementAndNeverLoadsRemoteZones()
		{
			string helpers = Read("KingdomPolityEndpointRuntime.Helpers.cs");
			string runtime = Read("KingdomPolityEndpointRuntime.cs");
			string loaded = Read("KingdomPolityLoadedEndpointRuntime.cs");
			StringAssert.Contains("The.Player?.CurrentZone", loaded);
			StringAssert.Contains("System.TryExactSettlementIds", loaded);
			StringAssert.Contains("System.NonSeatSettlements()", loaded);
			StringAssert.Contains("System.SettlementIdForOwnedZone(Zone.ZoneID)", loaded);
			StringAssert.Contains("KingdomWord.StandsIn(Zone)", loaded);
			StringAssert.Contains("KingdomPolityLoadedEndpointRuntime.TryObserve", helpers);
			StringAssert.Contains("Cohort.SurfaceRef", helpers);
			StringAssert.DoesNotContain("ZoneManager", runtime + helpers + loaded);
			StringAssert.DoesNotContain("GetZone", runtime + helpers + loaded);
			StringAssert.DoesNotContain("GameObject.Create", runtime + helpers + loaded);
			StringAssert.Contains("KingdomPolityNpcRuntime.TryCreate", runtime);
			StringAssert.Contains("CausedConfrontation(ledger, cohort)", runtime);
			StringAssert.Contains("created.Brain.Allegiance[\"Player\"] = -100", runtime);
		}

		[Test]
		public void CommittedMissingBodiesAreNeverRemintedAndCleanupTouchesExactMarkersOnly()
		{
			string runtime = Read("KingdomPolityEndpointRuntime.cs");
			string helpers = Read("KingdomPolityEndpointRuntime.Helpers.cs");
			string custody = Read("KingdomPolityEndpointRuntime.CustodyTransfer.cs");
			string proof = Read("KingdomPolityEndpointRuntime.CustodyProof.cs");
			int committed = runtime.IndexOf(
				"receipt.Phase == KingdomPolityProjectionPhase.Committed", StringComparison.Ordinal);
			int create = runtime.IndexOf("TryCreatePreparedMember", committed, StringComparison.Ordinal);
			Assert.GreaterOrEqual(committed, 0); Assert.Greater(create, committed);
			StringAssert.Contains("cohort.Phase == KingdomPolityCohortPhase.Materialized",
				runtime.Substring(committed,
				create - committed));
			StringAssert.Contains("resurrection is forbidden", runtime.Substring(committed,
				create - committed));
			StringAssert.Contains("expectedId && !marked", helpers);
			StringAssert.Contains("CohortOwnerProperty", helpers);
			int remove = runtime.IndexOf("TryRemoveExactBody", StringComparison.Ordinal);
			int commit = runtime.IndexOf("TryCommitEndpointCleanup", remove, StringComparison.Ordinal);
			Assert.Greater(remove, 0); Assert.Greater(commit, remove);
			StringAssert.Contains("TryReleaseFrozenCustody", runtime);
			StringAssert.Contains("KingdomPolityNpcRuntime.ExactGear", custody);
			StringAssert.Contains("TryMoveForeignObject", custody);
			StringAssert.Contains("Cell.AddObject(Item, Silent: true, NoStack: true)", custody);
			StringAssert.Contains("TryBuildCustodyNode(children[i], Item", proof);
			string npc = Read("KingdomPolityNpcRuntime.cs") +
				Read("KingdomPolityNpcRuntime.Gear.cs");
			StringAssert.Contains("SuppressCorpseDrops", npc);
			StringAssert.Contains("bodyCommerce.Value = 0.0", npc);
			StringAssert.Contains("commerce.Value = 0.0", npc);
			StringAssert.Contains("item.Physics.Takeable = false", npc);
		}

		[Test]
		public void ClashConclusionHasNoOffscreenPublicFoldAndStandingNeverCausesDiplomacy()
		{
			string pure = Read("KingdomPolityClashRules.cs");
			string adapter = Read("KingdomPolityEndpointRuntime.Clash.cs");
			string diplomacy = Read("KingdomPolityDiplomacyRules.cs") +
				Read("KingdomPolityDiplomacyRules.Answer.cs");
			StringAssert.Contains("internal static partial class KingdomPolityClashRules", pure);
			StringAssert.Contains("TryAdmit(System", adapter);
				StringAssert.Contains("TryObserve(zone, admitted.RealmId, cohort, receipt", adapter);
			StringAssert.Contains("clash participant projection is physically incomplete", adapter);
			StringAssert.DoesNotContain("GetStanding", diplomacy);
			StringAssert.DoesNotContain("Standings", diplomacy);
			StringAssert.Contains("StartsWith(\"taf:event:\"", diplomacy);
			StringAssert.Contains("StartsWith(\"taf:fact:witnessed:\"", diplomacy);
		}

		[Test]
		public void FirstContactConsumerSchedulesBodiesChoicesAndOnlyLoadedClashes()
		{
			string visit = Read("KingdomPolityVisitRuntime.cs") +
				Read("KingdomPolityVisitRuntime.Dispute.cs") +
				Read("KingdomPolityVisitRuntime.Endpoints.cs");
			string presentation = Read("KingdomPolityPresentationRuntime.cs");
			string interaction = Read("KingdomPolityVisitInteraction.cs");
			string body = Read("r_KingdomPolityCohortBody.cs");
			StringAssert.Contains("KingdomPolityRouteRules.TryPlan", visit);
			StringAssert.Contains("KingdomPolityRouteRules.TryDepart", visit);
			StringAssert.Contains("KingdomPolityRouteRules.TryAdvance", visit);
			StringAssert.Contains("KingdomPolityCohortRules.TryPlan", visit);
			StringAssert.Contains("KingdomPolityDiplomacyRules.TryPlanTerms", visit);
			StringAssert.Contains("TryManifestCurrentEndpoint", visit);
			StringAssert.Contains("KingdomPolityRules.CanEmitOptionalProjection", visit);
			StringAssert.Contains("TryObservePresentation", presentation);
			StringAssert.Contains("r_TAF_OptionPolityPresentation", presentation);
			StringAssert.Contains("Popup.PickOption", interaction);
			StringAssert.Contains("KingdomPolityDiplomacyRules.TryAnswerTerms", interaction);
			StringAssert.Contains("TryConcludeCurrentEndpointClash", interaction);
			StringAssert.Contains("EarlyBeforeDeathRemovalEvent.ID", body);
			StringAssert.Contains("BeforeDestroyObjectEvent.ID", body);
			StringAssert.Contains("OnDestroyObjectEvent.ID", body);
			StringAssert.DoesNotContain("WitnessCohortDeath(", body);
			StringAssert.Contains("CanBeReplicatedEvent.ID", body);
			StringAssert.Contains("FinalizeCopy", body);
			StringAssert.DoesNotContain("ZoneManager", visit + interaction + body);
			StringAssert.DoesNotContain("GetZone", visit + interaction + body);
		}
	}
}
#endif
