#if TAF_TESTS
using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomExternalOwnershipSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[Test]
		public void ManifestKeepsExactBridgeOutsideCommonRecursivePaths()
		{
			using JsonDocument document = JsonDocument.Parse(Read("manifest.json"));
			JsonElement directories = document.RootElement.GetProperty("Directories");
			Assert.AreEqual(2, directories.GetArrayLength());
			JsonElement common = directories[0].GetProperty("Paths");
			for (int i = 0; i < common.GetArrayLength(); i++)
			{
				string path = common[i].GetString();
				Assert.IsFalse(path.StartsWith("/Integrations", StringComparison.Ordinal));
			}
			StringAssert.Contains("/RuntimeData/", common.GetRawText());
			StringAssert.DoesNotContain("/Textures/", common.GetRawText(),
				"vanilla-only runtime art must not advertise an absent local texture tree");
			JsonElement bridge = directories[1];
			Assert.AreEqual("/Integrations/Hearthpyre223/",
				bridge.GetProperty("Path").GetString());
			Assert.AreEqual("2.2.3", bridge.GetProperty("Dependencies")
				.GetProperty("Hearthpyre").GetString());
		}

		[Test]
		public void RootRuntimeXmlMovedBehindDeclaredCommonBoundary()
		{
			string[] names = { "Books.xml", "EmbarkModules.xml", "KingdomBuildings.xml",
				"KingdomDeals.xml", "KingdomProcedures.xml", "KingdomRaidProfiles.xml",
				"KingdomResearch.xml", "KingdomYardWorks.xml", "ObjectBlueprints.xml",
				"Options.xml", "PopulationTables.xml", "Worlds.xml" };
			foreach (string name in names)
			{
				Assert.IsFalse(File.Exists(Path.Combine(TestMain.RepositoryRoot, name)), name);
				Assert.IsTrue(File.Exists(Path.Combine(TestMain.RepositoryRoot,
					"RuntimeData", name)), name);
			}
		}

		[Test]
		public void TypedShardUsesOnlyReviewedReadSurfaces()
		{
			string bridge = Read("Integrations/Hearthpyre223/KingdomHearthpyreOwnershipProvider.cs");
			StringAssert.Contains("using Hearthpyre;", bridge);
			StringAssert.Contains("RealmSystem.SettlementsByCellID", bridge);
			StringAssert.Contains("RealmSystem.SectorsByZoneID", bridge);
			StringAssert.Contains("settlement.SectorsByZoneID", bridge);
			foreach (string banned in new[] { "AddLiminal", "NewSettlement", "PartyLeader",
				"ZoneManager", "GetZone(", "Notitia", "Catalog" })
				StringAssert.DoesNotContain(banned, bridge);
		}

		[Test]
		public void HearthpyreHomesAreExactReadOnlyFootprintsNotCivicAuthority()
		{
			string bridge = Read(
				"Integrations/Hearthpyre223/KingdomHearthpyreFootprintProvider.cs");
			string custody = Read(
				"Integrations/Hearthpyre223/KingdomHearthpyreFootprintCustody.cs");
			string body = bridge + custody;
			foreach (string proof in new[] { "[KingdomForeignFootprintProvider]",
				"ProviderId => \"Hearthpyre\"", "ProviderVersion => \"2.2.3\"",
				"KingdomHearthpyreFootprintScanBudget budget",
				"ReferenceEquals(The.ZoneManager.ActiveZone, ActiveZone)",
				"RealmSystem.SectorsByZoneID.TryGetValue", "RealmSystem.Sectors.TryGetValue",
				"RealmSystem.Settlements.TryGetValue", "RealmSystem.SettlementsByCellID.TryGetValue",
				"RealmSystem.Homes.TryGetValue", "ReferenceEquals(Home.Sector, Sector)",
				"settlement.SectorsByZoneID.TryGetValue", "CountValue", "CountReference",
				"Sector.Homes", "Home.Count", "Home.Origin", "TrySnapshotHomes(",
				"ReferenceEquals(pair.Value?.Sector, Sector)",
				"!members.Contains(pair.Value)", "HomeReferenceComparer.Instance",
				"SameRoster(homes, checkHomes)",
				"foreach (Location2D location in Home)",
				"TryObserveHome(ActiveZone, sector, homes[i], budget",
				"Budget.Exhausted", "Budget.TryCharge(Home.Count)",
				"Row = DiagnosticRow(ActiveZone, Sector, Home, cellFailure)",
				"if (!owned) row.Refusal = BoundFault(custodyFailure)",
				"DeclaredCount = 0, Cells = null", "Refusal = \"\"",
				"Cells.Sort(Compare)", "Revision = Digest", "SameRow(rows[i], check)" })
				StringAssert.Contains(proof, body);
			foreach (string banned in new[] { "GetZone(", "NewHome(",
				"RemoveHome(", "Home.Add(", "Home.Remove(", "Sector.Flush(",
				"[KingdomDesignationProvider]", "KingdomBenefitDesignation", "TryFaultRow",
				"new Candidate", "Dictionary<long, Guid> owners",
				"Home footprints overlap and are ambiguous", "ExactHomeRoster" })
				StringAssert.DoesNotContain(banned, body);
			StringAssert.Contains("Footprints = null; Failure = null;", bridge);
			StringAssert.Contains("Footprints = rows.ToArray(); return true;", bridge);
			string scan = Read(
				"Integrations/Hearthpyre223/KingdomHearthpyreFootprintScanBudget.cs");
			StringAssert.Contains("MaxInspectionWork", scan);
			StringAssert.Contains("if (Exhausted || Entries < 0", scan);
			StringAssert.Contains("LimitFailure", scan);

			string adoption = Read("Growth/KingdomAdoptionDesignation.cs");
			StringAssert.Contains("KingdomForeignFootprints.TryMatchExact", adoption);
			Assert.Less(adoption.IndexOf("KingdomForeignFootprints.TryMatchExact",
				StringComparison.Ordinal), adoption.IndexOf("TryCreate(Z.ZoneID",
				StringComparison.Ordinal));
			string designation = Read("Growth/KingdomDesignationSources.Adopted.cs");
			StringAssert.Contains("KingdomForeignFootprints.TryReprove", designation);
			StringAssert.Contains("adoption designation paused", designation);
			string registry = Read("Growth/KingdomForeignFootprints.cs")
				+ Read("Growth/KingdomForeignFootprints.Normalization.cs")
				+ Read("Growth/KingdomForeignFootprintBudgetRules.cs");
			string matching = Read("Growth/KingdomForeignFootprintSnapshotRules.cs");
			StringAssert.Contains("ClassifyCall", registry);
			StringAssert.Contains("MaxRows", registry);
			StringAssert.Contains("MaxCells", registry);
			StringAssert.Contains("RefuseProviderContradictions", registry);
			StringAssert.Contains("DescribeRowFault(Sources[i], rowFault)", registry);
			StringAssert.Contains("TryProviderPreflight", registry);
			Assert.Less(registry.IndexOf("TryProviderPreflight", StringComparison.Ordinal),
				registry.IndexOf("TryNormalize(Provider, Sources[i]", StringComparison.Ordinal));
			StringAssert.DoesNotContain("Math.Min(Sources.Length", registry);
			StringAssert.Contains("KingdomForeignFootprintBudgetRules.Apply", registry);
			StringAssert.DoesNotContain("RegistrationFaults.Count > 0", registry);
			StringAssert.Contains("room intersects refused foreign footprint", matching);
			StringAssert.Contains("room partially intersects a foreign footprint", matching);
			StringAssert.Contains("bound foreign footprint provider is faulted", matching);
			StringAssert.Contains("bound foreign footprint is refused", matching);
		}

		[Test]
		public void ReceiptBindsExternalDecisionBeforeDebitAndPublication()
		{
			string preflight = Read("Core/KingdomFoundingTransaction.10ExternalOwnership.cs");
			foreach (string evidence in new[] { "ProviderVersion", "OwnerGuid", "SectorGuid",
				"Evidence", "ZoneId", "ParasangId" })
				StringAssert.Contains(evidence, preflight);
			string stage = Read("Core/KingdomFoundingTransaction.10Staging.cs");
			string begin = Read("Core/KingdomFoundingTransaction.10Begin.cs");
			StringAssert.Contains("Basin.PendingExternalBinding = externalBinding", stage);
			StringAssert.Contains("PayloadDigestWithExternalBinding", begin);
			Assert.Less(begin.IndexOf("TryStageFoundingReceipt", StringComparison.Ordinal),
				begin.IndexOf("KingdomLiquids.Drain", StringComparison.Ordinal));
			StringAssert.Contains("TryPassExternalPourBarrier", begin);
			string first = Read("Core/KingdomFoundingTransaction.12PublishFirst.cs");
			Assert.Less(first.IndexOf("CommitExternalBinding", StringComparison.Ordinal),
				first.IndexOf("KingdomFounding.Found", StringComparison.Ordinal));
			string second = Read("Core/KingdomFoundingTransaction.14PublishSecondCore.cs");
			Assert.Less(second.IndexOf("CommitExternalBinding", StringComparison.Ordinal),
				second.IndexOf("TryFreezeSecondIdentity", StringComparison.Ordinal));
			string binding = Read("Core/KingdomExternalOwnership.Binding.cs");
			StringAssert.Contains("Site.SetZoneProperty(BindingProperty, Encoded)", binding);
			Assert.Less(binding.IndexOf("Site.SetZoneProperty(BindingProperty, Encoded)",
					StringComparison.Ordinal),
				binding.IndexOf("Site.SetZoneProperty(BindingAuthorityProperty, Authority)",
					StringComparison.Ordinal));
			StringAssert.Contains("PairAbsentOrExact", binding);
			StringAssert.Contains("Site, formatted, Encoded, out Failure", preflight);
		}

		[Test]
		public void VisitedGroundPausesBeforeAnySemanticPassMutation()
		{
			string source = Read("Core/KingdomSystem.z21.SemanticPass.cs");
			Assert.Less(source.IndexOf("CanOperate", StringComparison.Ordinal),
				source.IndexOf("PrepareSemanticPass", StringComparison.Ordinal));
			StringAssert.Contains("FinishPublishedClaimStage", source);
			string events = Read("Core/KingdomSystem.z20.Events.cs");
			Assert.Less(events.LastIndexOf("ExternalOwnershipAllows(E.Zone)",
					StringComparison.Ordinal),
				events.IndexOf("if (TrySeat(E.Zone))", StringComparison.Ordinal));
			Assert.Less(events.IndexOf("ExternalOwnershipAllows(The.ZoneManager?.ActiveZone)",
					StringComparison.Ordinal),
				events.IndexOf("KingdomConstruction.OnGlobalRecoveryPass", StringComparison.Ordinal));
			string registry = Read("Core/KingdomExternalOwnership.cs");
			StringAssert.Contains("requiredCount", registry);
			StringAssert.DoesNotContain("providers.FindAll", registry);
			StringAssert.Contains("More than one external owner", registry);
		}

		[Test]
		public void OrdinaryClaimRecoveryAndDebugResetKeepExactAuthority()
		{
			string claim = Read("Core/KingdomCharterPart.Ground.cs");
			Assert.Less(claim.IndexOf("ResumeExternalClaimIfNeeded", StringComparison.Ordinal),
				claim.IndexOf("JudgeClaim", StringComparison.Ordinal));
			StringAssert.Contains("ExternalClaimPublicationObserved", claim);
			string runtime = Read("Core/KingdomExternalOwnership.Binding.cs");
			StringAssert.Contains("ClaimAuthorityPrefix + Realm.Length", runtime);
			string reset = Read("Core/KingdomFoundingTransaction.01DebugReset.cs");
			Assert.Less(reset.IndexOf("CanResetForRealms", StringComparison.Ordinal),
				reset.IndexOf("Everything above is read-only", StringComparison.Ordinal));
			Assert.Greater(reset.LastIndexOf("TryClearForRealmReset", StringComparison.Ordinal),
				reset.IndexOf("Founding or claim cleanup did not retain", StringComparison.Ordinal));
		}
	}
}
#endif
