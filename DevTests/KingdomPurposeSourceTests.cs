#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposeSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		[Test]
		public void PurposeProductionRootsExactObjectBeforePublishingItsIdentity()
		{
			string source = KingdomPurposeLogicalSource.Read();
			Ordered(source, "cargo = CreateCargo(live, manifest)", "RootCargo(live, cargo)",
				"KingdomConstruction.UpdateOutput(ref live, cargo.ID)",
				"CargoOutputPending", "sourceInventory.AddObject(cargo",
				"CargoOutputSettled", "CargoTransferPending", "CargoDelivered");
			StringAssert.Contains("ReferenceEquals(rooted, Cargo)", source);
			StringAssert.Contains("Cargo.GetStringProperty(CargoManifestProperty) != encoded", source);
			StringAssert.Contains("Cargo.GetStringProperty(CargoConsignmentProperty) != Job.Id", source);
			StringAssert.Contains("sourceState == KingdomPhysicalLookupState.Ambiguous", source);
			StringAssert.Contains("destinationState == KingdomPhysicalLookupState.Ambiguous", source);
			Ordered(source, "out bool requiresInspection", "if (requiresInspection)",
				"KingdomConstruction.Quarantine(ref live");
			string gate = KingdomMirrorGateLogicalSource.Read();
			StringAssert.Contains("destinationAmbiguous", gate);
		}

		[Test]
		public void PurposeCommitConsumesOnlyFrozenCargoAndQuarantinesInterruptedDebit()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			Ordered(plot, "KingdomPurpose.ResolveCommitCargo",
				"KingdomPurpose.ResolveCommitReciprocalCargo",
				"job.PhysicalReceipt = purposeReceipt",
				"KingdomPurpose.TryRequiredFundingObjectIds",
				"KingdomConstruction.TryFundNewRouted");
			StringAssert.Contains("Retry remains bound to its exact cargo object", plot);

			string purposeFunding = Source(Path.Combine("Growth",
				"KingdomPurposePortfolio.Funding.cs"));
			StringAssert.Contains("RequiredObjectIds.Add(commitment.CargoItemId)", purposeFunding);
			StringAssert.Contains("RequiredObjectIds.Add(commitment.ReciprocalCargoItemId)",
				purposeFunding);
			StringAssert.Contains("expected.Contains(Item.IDIfAssigned)", purposeFunding);
			StringAssert.Contains("ExactPortfolioCargoIdentity", purposeFunding);

			string construction = KingdomConstructionLogicalSource.Read();
			Ordered(construction, "KingdomPurpose.RequiresExactFunding(job)",
				"KingdomPurpose.TryRequiredFundingItems", "KingdomPurpose.TryRequiredFundingObjectIds",
				"TryResumeRoutedFunding(job, requiredIds");
			string routed = Source(Path.Combine("Growth",
				"KingdomConstruction.InputDrive.Open.cs"));
			StringAssert.Contains("TryPrepareRoutedInputReceiptWithRequiredObjects", routed);
		}

		[Test]
		public void PreviewDisclosesRouteSiteExactIdentityAndRecoveryBeforeCommission()
		{
			string charter = KingdomCharterPartLogicalSource.Read();
			Ordered(charter, "KingdomPlots.TryQuoteCommission", "KingdomArchitecturePreview.TryRender",
				"KingdomPurpose.AppendPreview(preview, quote.PurposeReceipt)",
				"Commission this exact plan", "KingdomCommission.Commission");
			string purpose = KingdomPurposeLogicalSource.Read();
			StringAssert.Contains("Exact cross-city input: 1 ", purpose);
			StringAssert.Contains("delivered through the live mirror-gate", purpose);
			StringAssert.Contains("Site: ", purpose);
			StringAssert.Contains("never substitutes or charges twice", purpose);
			StringAssert.Contains("cannot be left as an unattended survey stake", purpose);
		}

		[Test]
		public void PurposeReceiptsKeepPlainNamesAndPreviewAndOutboxEscapeThem()
		{
			string purpose = KingdomPurposeLogicalSource.Read();
			StringAssert.Contains("OriginCity = connection.SourceCity", purpose);
			StringAssert.Contains("DestinationCity = connection.DestinationCity", purpose);
			StringAssert.Contains("specialist.BaseDisplayNameStripped", purpose);
			StringAssert.Contains(
				"KingdomPresentation.Rich(commitment.SpecialistName)", purpose);
			StringAssert.Contains(
				"KingdomPresentation.Rich(Manifest.OriginCity)", purpose);
			StringAssert.Contains(
				"KingdomPresentation.Rich(Manifest.DestinationCity)", purpose);
			StringAssert.DoesNotContain(
				"OriginCity = KingdomPresentation.Rich", purpose);
			StringAssert.DoesNotContain(
				"SpecialistName = KingdomPresentation.Rich", purpose);
		}

		[Test]
		public void SameTickConstructionReceiptSpendsTheOnlyBodyPurposeSlot()
		{
			string zoning = KingdomZoningLogicalSource.Read();
			StringAssert.Contains("KingdomConstruction.TryRead", zoning);
			StringAssert.Contains("job.Route != KingdomConstructionRoute.PlotCommission", zoning);
			StringAssert.Contains("job.Route != KingdomConstructionRoute.PlotPlan", zoning);
			Assert.IsFalse(zoning.Contains("if (KeptCacheTick == now"));
		}

		[Test]
		public void CatalogueDeclaresDifferentPhysicalSitesAndHostedArcology()
		{
			string buildings = Source("KingdomBuildings.xml");
			StringAssert.Contains("Purpose=\"flesh\" PurposeSite=\"living-surgery\"", buildings);
			StringAssert.Contains("PurposeProducers=\"vathouse|graftinghall\"", buildings);
			StringAssert.Contains("Purpose=\"chrome\" PurposeSite=\"ruin-enrollment\"", buildings);
			StringAssert.Contains("PurposeProducers=\"smelter,chargingpost\"", buildings);
			StringAssert.Contains("the hosted arcology (the great court within a city-shell)",
				buildings);

			string objects = Source("ObjectBlueprints.xml");
			StringAssert.Contains("Name=\"r_KingdomRealmGranary\" Inherits=\"r_KingdomGranary\"", objects);
			StringAssert.Contains("Name=\"r_KingdomLarderCapacity\" Value=\"384\"", objects);
			StringAssert.Contains("DisplayName=\"hosted arcology\"", objects);
			StringAssert.Contains("part Name=\"Interior\" Cell=\"TAFArcologyAtrium\"", objects);
			StringAssert.Contains("DisplayName=\"vertical lodging ward works\"", objects);
			StringAssert.Contains("DisplayName=\"hydroponic terrace works\"", objects);
			Assert.IsFalse(objects.Contains("it has weather of its own"));
			Assert.IsFalse(objects.Contains("nothing above this but more building"));

			string testing = Source("TESTING.md");
			string testingWords = string.Join(" ", testing.Split((char[])null,
				StringSplitOptions.RemoveEmptyEntries));
			StringAssert.Contains("Pass 37 — Purposeful cities and exact cargo; arcology review hold",
				testingWords);
			StringAssert.Contains("Returning the same object restores the exact preview", testingWords);
			StringAssert.Contains("an ordinary worked-metal item cannot substitute", testingWords);
			StringAssert.Contains("Deep-Bore, Great Foundry, Granary-Colossus", testingWords);
			StringAssert.Contains("exact five-work reciprocal portfolio is implemented", testingWords);

			string changelog = Source("CHANGELOG.md");
			StringAssert.Contains("arcology is now a hosted gameplay system, not a surface prop", changelog);
			StringAssert.Contains("persistent Interior", changelog);
			StringAssert.Contains("bounded ward and terrace lifts", changelog);
		}

		[Test]
		public void PortfolioRuntimeHasOneCanonicalCasRegisterAndNoAutomaticWorkLoop()
		{
			string source = KingdomPurposeLogicalSource.Read();
			Assert.AreEqual(1, Occurrences(source,
				"internal const string PortfolioStateKey = \"r_TAF_PurposePortfolioPair\""));
			Ordered(source, "string current = The.Game.GetStringGameState(PortfolioStateKey",
				"current != expected", "ValidTransition(Before, After",
				"The.Game.SetStringGameState(PortfolioStateKey, next)",
				"GetStringGameState(PortfolioStateKey, \"\") != next");
			StringAssert.Contains("Each city needs two distinct dedicated material stockpiles", source);
			StringAssert.Contains("FindLocalConnection(System, zone", source);
			StringAssert.Contains("KingdomPurposePortfolioRules.Partners(First)", source);
			StringAssert.Contains("Phase != KingdomPurposePairPhase.Frozen", source);
			StringAssert.Contains("No operation runs in the background", source);
			StringAssert.Contains("TryStartPortfolioOperation", source);
			StringAssert.Contains("AcceptPortfolioCredit", source);
			StringAssert.Contains("KingdomPurposeOperationPhase.PickupComplete", source);
			StringAssert.Contains("KingdomPurposeOperationPhase.LandingPending", source);
			StringAssert.Contains("KingdomPurposeBodyAuthorityRules.TryDecode", source);
			StringAssert.Contains("if (Pair == null) return true;", source);
			StringAssert.Contains("InitialBuildKey = pair == null && definition.PortfolioOnly", source);
			StringAssert.Contains("TryExactSettlementIds(RequirePublishedClaims: true", source);
			StringAssert.Contains("TryReconcilePortfolioTopology(ref Pair", source);
			StringAssert.DoesNotContain("WantTurnTick", source);
			StringAssert.DoesNotContain("TurnTick(", source);

			string objects = Source("ObjectBlueprints.xml");
			Assert.AreEqual(5, Occurrences(objects, "<part Name=\"r_KingdomPurposeWork\" />"));
		}

		private static int Occurrences(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at,
				StringComparison.Ordinal)) >= 0; at += token.Length) count++;
			return count;
		}
	}
}
#endif
