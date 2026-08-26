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
			string source = Source(Path.Combine("Growth", "KingdomPurpose.cs"));
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
			string gate = Source(Path.Combine("Growth", "KingdomMirrorGate.cs"));
			StringAssert.Contains("destinationAmbiguous", gate);
		}

		[Test]
		public void PurposeCommitConsumesOnlyFrozenCargoAndQuarantinesInterruptedDebit()
		{
			string plot = Source(Path.Combine("Growth", "KingdomPlot2.cs"));
			Ordered(plot, "KingdomPurpose.ResolveCommitCargo", "ReservePaymentWithRequiredItem",
				"job.PhysicalReceipt = purposeReceipt", "KingdomConstruction.TryFundNew");
			StringAssert.Contains("Retry remains bound to its exact cargo object", plot);

			string debit = Source(Path.Combine("Growth", "KingdomMaterialDebit.cs"));
			StringAssert.Contains("ReferenceEquals(item, RequiredItem)", debit);
			StringAssert.Contains("RequiredSourceWasConsumed", debit);
			StringAssert.Contains("RequiredItem.ID != RequiredItemId", debit);

			string construction = Source(Path.Combine("Growth", "KingdomConstruction.cs"));
			Ordered(construction, "KingdomPurpose.RequiresExactFunding(job)",
				"KingdomPurpose.TryRequiredFundingItem", "TryResumeFunding(job, Z, Survey,",
				"required, out job, out fault)");
			StringAssert.Contains("ReserveCompositeWithRequiredItem", construction);
		}

		[Test]
		public void PreviewDisclosesRouteSiteExactIdentityAndRecoveryBeforeCommission()
		{
			string charter = Source(Path.Combine("Core", "KingdomCharterPart.cs"));
			Ordered(charter, "KingdomPlots.TryQuoteCommission", "KingdomArchitecturePreview.TryRender",
				"KingdomPurpose.AppendPreview(preview, quote.PurposeReceipt)",
				"Commission this exact plan", "KingdomCommission.Commission");
			string purpose = Source(Path.Combine("Growth", "KingdomPurpose.cs"));
			StringAssert.Contains("Exact cross-city input: 1 ", purpose);
			StringAssert.Contains("delivered through the live mirror-gate", purpose);
			StringAssert.Contains("Site: ", purpose);
			StringAssert.Contains("never substitutes or charges it twice", purpose);
			StringAssert.Contains("cannot be left as an unattended survey stake", purpose);
		}

		[Test]
		public void PurposeReceiptsKeepPlainNamesAndPreviewAndOutboxEscapeThem()
		{
			string purpose = Source(Path.Combine("Growth", "KingdomPurpose.cs"));
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
			string zoning = Source(Path.Combine("Growth", "KingdomZoning.cs"));
			StringAssert.Contains("KingdomConstruction.TryRead", zoning);
			StringAssert.Contains("job.Route != KingdomConstructionRoute.PlotCommission", zoning);
			StringAssert.Contains("job.Route != KingdomConstructionRoute.PlotPlan", zoning);
			Assert.IsFalse(zoning.Contains("if (KeptCacheTick == now"));
		}

		[Test]
		public void CatalogueDeclaresDifferentPhysicalSitesAndHonestArcologyFoundation()
		{
			string buildings = Source("KingdomBuildings.xml");
			StringAssert.Contains("Purpose=\"flesh\" PurposeSite=\"living-surgery\"", buildings);
			StringAssert.Contains("PurposeProducers=\"vathouse|graftinghall\"", buildings);
			StringAssert.Contains("Purpose=\"chrome\" PurposeSite=\"ruin-enrollment\"", buildings);
			StringAssert.Contains("PurposeProducers=\"smelter,chargingpost\"", buildings);
			StringAssert.Contains("arcology foundation (a monumental shell awaiting hosted streets)",
				buildings);

			string objects = Source("ObjectBlueprints.xml");
			StringAssert.Contains("DisplayName=\"arcology foundation\"", objects);
			StringAssert.Contains("hosts no interior plots, separate zones, or city-scale population yet",
				objects);
			StringAssert.Contains("DisplayName=\"stacked surface ward\"", objects);
			StringAssert.Contains("DisplayName=\"raised surface lamp-terrace\"", objects);
			Assert.IsFalse(objects.Contains("it has weather of its own"));
			Assert.IsFalse(objects.Contains("nothing above this but more building"));

			string testing = Source("TESTING.md");
			string testingWords = string.Join(" ", testing.Split((char[])null,
				StringSplitOptions.RemoveEmptyEntries));
			StringAssert.Contains("Pass 37 — Purposeful cities, exact cargo, and the honest arcology foundation", testingWords);
			StringAssert.Contains("Returning the same object restores the exact preview", testingWords);
			StringAssert.Contains("an ordinary worked-metal item cannot substitute", testingWords);
			StringAssert.Contains("present scope is deliberately the two body purposes", testingWords);

			string changelog = Source("CHANGELOG.md");
			StringAssert.Contains("arcology foundation", changelog);
			StringAssert.Contains("surface-prototype records", changelog);
			StringAssert.Contains("hosted interior and zone-spanning ground remain deferred", changelog);
		}
	}
}
#endif
