using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLabCivicRulesTests
	{
		[Test]
		public void WireEnumsAndTasteMappingArePinned()
		{
			Assert.AreEqual(1, KingdomLabCivicRules.CurrentVersion);
			Assert.AreEqual(1, (byte)KingdomLabCivicKind.SavantPrice);
			Assert.AreEqual(2, (byte)KingdomLabCivicKind.RefusalDeparture);
			Assert.AreEqual(5, (byte)KingdomLabCivicPhase.Quarantined);
			Assert.AreEqual(3, (byte)KingdomLabCivicRequest.RoofRefusal);
			Assert.AreEqual(2, (byte)KingdomLabCivicChoice.Refused);
			Assert.AreEqual(5, (byte)KingdomLabCivicClosure.OwnerGone);
			for (int i = 0; i < 10; i++)
				Assert.AreEqual(i == 5 || i == 6
					? KingdomLabCivicRequest.ShrineUnconsecrated
					: KingdomLabCivicRequest.NeighbourRehoused,
					KingdomLabCivicRules.RequestForTaste(i), "taste " + i);
		}

		[Test]
		public void SavantIdentityFreezesEveryAuthoredCauseFact()
		{
			KingdomLabCivicReceipt first = Savant(false);
			KingdomLabCivicReceipt again = Savant(false);
			Assert.NotNull(first);
			Assert.That(KingdomLabCivicRules.Valid(first, out string failure),
				Is.True, failure);
			Assert.AreEqual(first.CauseDigest, again.CauseDigest);
			Assert.AreEqual(first.EventId, again.EventId);
			Assert.AreEqual(KingdomLabCivicRules.TasteOrdinalSource, first.TasteSource);
			Assert.AreEqual(27L, first.TasteOrdinal);
			Assert.AreEqual(LodgeReceipt, first.NotableLodgeReceiptId);

			KingdomLabCivicReceipt tampered = first.Copy();
			tampered.TargetHomeObjectId = "another-home";
			Assert.That(KingdomLabCivicRules.Valid(tampered, out _), Is.False);
			tampered = first.Copy(); tampered.TasteTag = "taf:faith";
			Assert.That(KingdomLabCivicRules.Valid(tampered, out _), Is.False);
			tampered = first.Copy(); tampered.CreatedTick++;
			Assert.That(KingdomLabCivicRules.Valid(tampered, out _), Is.False);
			tampered = first.Copy(); tampered.CloseRecorded = true;
			Assert.That(KingdomLabCivicRules.Valid(tampered, out _), Is.False);
		}

		[Test]
		public void MalformedOrInferredSavantEvidenceNeverPrepares()
		{
			Assert.IsNull(PrepareSavant("Creed A", "Creed A", LodgeReceipt,
				"plot-a", "plot-b"));
			Assert.IsNull(PrepareSavant("Creed A", "Creed B", "intent:" + LodgeReceipt,
				"plot-a", "plot-b"));
			Assert.IsNull(PrepareSavant("Creed A", "Creed B", LodgeReceipt,
				"plot-a", "plot-a"));
			Assert.IsNull(PrepareSavant("Creed A", "Creed B", "not-a-lodge-receipt",
				"plot-a", "plot-b"));
		}

		[Test]
		public void SavantChoiceAndClosureAreMonotoneAndRetryExact()
		{
			KingdomLabCivicReceipt prepared = Savant(false);
			Assert.That(KingdomLabCivicRules.TryClose(prepared,
				KingdomLabCivicClosure.Rehoused, 30L, out _, out _), Is.False,
				"the move cannot close before the founder grants it");
			Assert.That(KingdomLabCivicRules.TryChoose(prepared, true, 30L,
				out KingdomLabCivicReceipt promised, out string failure), Is.True, failure);
			Assert.AreEqual(KingdomLabCivicPhase.ChoicePrepared, promised.Phase);
			Assert.That(KingdomLabCivicRules.TryClose(promised,
				KingdomLabCivicClosure.Rehoused, 31L,
				out KingdomLabCivicReceipt closed, out failure), Is.True, failure);
			Assert.AreEqual(KingdomLabCivicPhase.Closed, closed.Phase);
			Assert.AreEqual(KingdomLabCivicChoice.Granted, closed.Choice);
			Assert.That(KingdomLabCivicRules.TryClose(closed,
				KingdomLabCivicClosure.Rehoused, 99L,
				out KingdomLabCivicReceipt retry, out failure), Is.True, failure);
			Assert.AreEqual(closed.ClosedTick, retry.ClosedTick);
			Assert.That(KingdomLabCivicRules.TryClose(closed,
				KingdomLabCivicClosure.CauseGone, 99L, out _, out _), Is.False);

			Assert.That(KingdomLabCivicRules.TryChoose(Savant(false), false, 30L,
				out KingdomLabCivicReceipt refused, out failure), Is.True, failure);
			Assert.AreEqual(KingdomLabCivicClosure.Refused, refused.Closure);
			Assert.That(KingdomLabCivicRules.TryChoose(refused, false, 99L,
				out retry, out failure), Is.True, failure);
			Assert.AreEqual(refused.ClosedTick, retry.ClosedTick);
			Assert.That(KingdomLabCivicRules.TryChoose(refused, true, 99L,
				out _, out _), Is.False);
		}

		[Test]
		public void ShrineAndDepartureHaveOnlyTheirLawfulTerminalPaths()
		{
			Assert.That(KingdomLabCivicRules.TryChoose(Savant(true), true, 30L,
				out KingdomLabCivicReceipt shrine, out string failure), Is.True, failure);
			Assert.AreEqual(KingdomLabCivicPhase.Active, shrine.Phase);
			Assert.That(KingdomLabCivicRules.TryClose(shrine,
				KingdomLabCivicClosure.Rehoused, 31L, out _, out _), Is.False);
			Assert.That(KingdomLabCivicRules.TryClose(shrine,
				KingdomLabCivicClosure.CauseGone, 31L,
				out KingdomLabCivicReceipt causeGone, out failure), Is.True, failure);
			Assert.AreEqual(KingdomLabCivicChoice.Granted, causeGone.Choice);

			KingdomLabCivicReceipt leaving = Departure();
			Assert.That(KingdomLabCivicRules.Valid(leaving, out failure), Is.True, failure);
			Assert.That(KingdomLabCivicRules.TryChoose(leaving, true, 30L,
				out _, out _), Is.False);
			Assert.That(KingdomLabCivicRules.TryClose(leaving,
				KingdomLabCivicClosure.Departed, 40L,
				out KingdomLabCivicReceipt departed, out failure), Is.True, failure);
			Assert.AreEqual(KingdomLabCivicClosure.Departed, departed.Closure);
			Assert.That(KingdomLabCivicRules.TryClose(Departure(),
				KingdomLabCivicClosure.Refused, 40L, out _, out _), Is.False);
		}

		[Test]
		public void DepartureProjectionCutsRecoverOnlyEmptyOrExactEvidence()
		{
			KingdomLabCivicReceipt receipt = Departure();
			string[] eventCuts = { null, receipt.EventId };
			string[] ownerCuts = { null, receipt.OwnerObjectId };
			string[] digestCuts = { null, receipt.CauseDigest };
			for (int e = 0; e < eventCuts.Length; e++)
				for (int o = 0; o < ownerCuts.Length; o++)
					for (int d = 0; d < digestCuts.Length; d++)
						Assert.AreEqual(KingdomLabDepartureProjection.RecoverableAtSource,
							Projection(receipt, receipt.SourcePlotId, eventCuts[e], ownerCuts[o],
								digestCuts[d]), "every empty-or-exact marker-field cut recovers");
			Assert.AreEqual(KingdomLabDepartureProjection.Active,
				Projection(receipt, null, receipt.EventId, receipt.OwnerObjectId,
					receipt.CauseDigest), "a fully published marker plus cleared home is active");
			Assert.AreEqual(KingdomLabDepartureProjection.Diverged,
				Projection(receipt, null, receipt.EventId, null, receipt.CauseDigest),
				"home cannot clear before every exact marker field");
			Assert.AreEqual(KingdomLabDepartureProjection.Diverged,
				Projection(receipt, receipt.SourcePlotId, "foreign", null, null));
			Assert.AreEqual(KingdomLabDepartureProjection.Diverged,
				Projection(receipt, "third-plot", receipt.EventId, receipt.OwnerObjectId,
					receipt.CauseDigest));

			KingdomLabCivicClosure[] cuts =
			{
				KingdomLabCivicClosure.Rehoused,
				KingdomLabCivicClosure.CauseGone,
				KingdomLabCivicClosure.OwnerGone
			};
			for (int closure = 0; closure < cuts.Length; closure++)
			{
				Assert.That(KingdomLabCivicRules.TryClose(receipt, cuts[closure], 40L,
					out KingdomLabCivicReceipt closed, out string failure), Is.True, failure);
				for (int e = 0; e < eventCuts.Length; e++)
					for (int o = 0; o < ownerCuts.Length; o++)
						for (int d = 0; d < digestCuts.Length; d++)
							Assert.That(KingdomLabCivicRules.ClosedMarkerCleanupAllowed(closed,
								eventCuts[e], ownerCuts[o], digestCuts[d]), Is.True,
								"terminal-first cleanup accepts every partial exact clear cut");
				Assert.That(KingdomLabCivicRules.ClosedMarkerCleanupAllowed(closed,
					"foreign", null, null), Is.False);
			}
		}

		[Test]
		public void TargetCardinalityAndReadableProseAreExact()
		{
			Assert.AreEqual(KingdomLabObjectMatch.Missing,
				KingdomLabCivicRules.ClassifyObjectMatches(0));
			Assert.AreEqual(KingdomLabObjectMatch.Unique,
				KingdomLabCivicRules.ClassifyObjectMatches(1));
			Assert.AreEqual(KingdomLabObjectMatch.Duplicate,
				KingdomLabCivicRules.ClassifyObjectMatches(2));
			string shrine = KingdomLabCivicRules.RequestLine(Savant(true));
			StringAssert.Contains("the salt shrine", shrine);
			StringAssert.DoesNotContain("shrine-a", shrine);
			string neighbour = KingdomLabCivicRules.RequestLine(Savant(false));
			StringAssert.Contains("Kese", neighbour);
			StringAssert.Contains("the shared house", neighbour);
			StringAssert.Contains("the open dormitory", neighbour);
			StringAssert.DoesNotContain("plot-a", neighbour);
			StringAssert.DoesNotContain("plot-b", neighbour);
		}

		[Test]
		public void OwnerBookIsCanonicalBoundedAndExact()
		{
			Assert.That(KingdomLabCivicOwnerRules.TryDecode("", out KingdomLabCivicOwnerBook book),
				Is.True);
			KingdomLabCivicOwnerRow second = Owner("settlement-b", "zone-b", "hall-b");
			KingdomLabCivicOwnerRow first = Owner("settlement-a", "zone-a", "hall-a");
			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book, second, out book), Is.True);
			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book, first, out book), Is.True);
			Assert.AreEqual("settlement-a", book.Rows[0].SettlementId);
			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book, first, out KingdomLabCivicOwnerBook same),
				Is.True);
			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book,
				Owner("settlement-a", "zone-a", "other-hall"), out _), Is.False);

			string wire = KingdomLabCivicOwnerRules.Encode(same);
			Assert.IsNotNull(wire);
			Assert.That(KingdomLabCivicOwnerRules.TryDecode(wire, out KingdomLabCivicOwnerBook decoded),
				Is.True);
			Assert.AreEqual(wire, KingdomLabCivicOwnerRules.Encode(decoded));
			Assert.That(KingdomLabCivicOwnerRules.TryDecode(wire + "A", out _), Is.False);
			Assert.That(KingdomLabCivicOwnerRules.TryRelease(decoded,
				Owner("settlement-a", "zone-a", "other-hall"), out _), Is.False);
			Assert.That(KingdomLabCivicOwnerRules.TryRelease(decoded, first, out book), Is.True);
			Assert.IsNull(KingdomLabCivicOwnerRules.Find(book, "settlement-a"));

			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book,
				Owner("settlement-c", "zone-c", "hall-c"), out book), Is.True);
			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book,
				Owner("settlement-d", "zone-d", "hall-d"), out book), Is.True);
			Assert.AreEqual(KingdomLabCivicOwnerRules.MaxRows, book.Rows.Count);
			Assert.That(KingdomLabCivicOwnerRules.TryClaim(book,
				Owner("settlement-e", "zone-e", "hall-e"), out _), Is.False);
		}

		private static KingdomLabCivicReceipt Savant(bool Shrine)
		{
			return Shrine
				? KingdomLabCivicRules.PrepareSavant("realm-1", "settlement-a", "zone-a",
					"hall-a", "savant-body", 1, "Nara", "Creed A", "Creed B",
					LodgeReceipt, 27L, 5, "taf:faith", "shrine-a", null,
					0, "the salt shrine", "plot-a", "the shared house", null,
					null, 29L)
				: PrepareSavant("Creed A", "Creed B", LodgeReceipt,
					"plot-a", "plot-b");
		}

		private static KingdomLabCivicReceipt PrepareSavant(string SubjectCreed,
			string CityCreed, string LodgeReceipt, string SourcePlot, string TargetPlot)
		{
			return KingdomLabCivicRules.PrepareSavant("realm-1", "settlement-a", "zone-a",
				"hall-a", "savant-body", 1, "Nara", SubjectCreed, CityCreed,
				LodgeReceipt, 27L, 0, "taf:food", "neighbour-body", "home-b",
				2, "Kese", SourcePlot, "the shared house", TargetPlot,
				"the open dormitory", 29L);
		}

		private static KingdomLabDepartureProjection Projection(
			KingdomLabCivicReceipt Receipt, string Plot, string EventId,
			string OwnerId, string Digest)
		{
			return KingdomLabCivicRules.ClassifyDepartureProjection(Receipt,
				Plot, EventId, OwnerId, Digest);
		}

		private static KingdomLabCivicReceipt Departure()
		{
			return KingdomLabCivicRules.PrepareDeparture("realm-1", "settlement-a",
				"zone-a", "hall-a", "resident-body", 3, "Mara", "plot-c",
				"taf:offal", 35L);
		}

		private static KingdomLabCivicOwnerRow Owner(string Settlement, string Zone,
			string Object)
		{
			return new KingdomLabCivicOwnerRow
			{
				RealmId = "realm-1", SettlementId = Settlement,
				ZoneId = Zone, OwnerObjectId = Object
			};
		}

		private static readonly string LodgeReceipt = "taf:operation:"
			+ new string('a', 64);
	}
}
