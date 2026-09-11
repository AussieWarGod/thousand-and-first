#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Rect = ThousandAndFirst.KingdomPlotRules.PlotRect;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPlotPoseSitingRulesTests
	{
		[Test]
		public void RectangularLotsEnumerateBothPosesInAStableOrder()
		{
			List<KingdomPlotPoseCandidate> candidates =
				KingdomPlotPoseSitingRules.Enumerate(new Rect(0, 0, 3, 2), 3, 2);
			ClassicAssert.AreEqual(7, candidates.Count);
			AssertRect(candidates[0], 0, 0, 2, 1, false);
			AssertRect(candidates[1], 0, 0, 1, 2, true);
			AssertRect(candidates[2], 1, 0, 3, 1, false);
			AssertRect(candidates[3], 1, 0, 2, 2, true);
			AssertRect(candidates[4], 2, 0, 3, 2, true);
			AssertRect(candidates[5], 0, 1, 2, 2, false);
			AssertRect(candidates[6], 1, 1, 3, 2, false);
		}

		[Test]
		public void SquarePoseIsNotDuplicated()
		{
			List<KingdomPlotPoseCandidate> candidates =
				KingdomPlotPoseSitingRules.Enumerate(new Rect(4, 7, 6, 8), 2, 2);
			ClassicAssert.AreEqual(2, candidates.Count);
			AssertRect(candidates[0], 4, 7, 5, 8, false);
			AssertRect(candidates[1], 5, 7, 6, 8, false);
		}

		[Test]
		public void InvalidOrOversizeEnvelopeProducesNoCandidate()
		{
			Rect interior = new Rect(0, 0, 2, 1);
			ClassicAssert.AreEqual(0, KingdomPlotPoseSitingRules.Enumerate(interior, 0, 2).Count);
			ClassicAssert.AreEqual(0, KingdomPlotPoseSitingRules.Enumerate(interior, 4, 3).Count);
		}

		[Test]
		public void GrowthEnumeratesBothContainingPosesInOrdinaryStableOrder()
		{
			List<KingdomPlotPoseCandidate> candidates =
				KingdomPlotPoseSitingRules.EnumerateContaining(
					new Rect(2, 2, 3, 3), new Rect(0, 0, 5, 5), 4, 3);
			ClassicAssert.AreEqual(12, candidates.Count);
			AssertRect(candidates[0], 1, 0, 3, 3, true);
			AssertRect(candidates[1], 2, 0, 4, 3, true);
			AssertRect(candidates[2], 0, 1, 3, 3, false);
			AssertRect(candidates[3], 1, 1, 4, 3, false);
			AssertRect(candidates[4], 1, 1, 3, 4, true);
			AssertRect(candidates[5], 2, 1, 5, 3, false);
			AssertRect(candidates[6], 2, 1, 4, 4, true);
			AssertRect(candidates[7], 0, 2, 3, 4, false);
			AssertRect(candidates[8], 1, 2, 4, 4, false);
			AssertRect(candidates[9], 1, 2, 3, 5, true);
			AssertRect(candidates[10], 2, 2, 5, 4, false);
			AssertRect(candidates[11], 2, 2, 4, 5, true);
		}

		[Test]
		public void GrowthNeverReturnsNonContainingOrOutOfInteriorEnvelope()
		{
			Rect oldRect = new Rect(4, 4, 5, 5);
			Rect interior = new Rect(3, 3, 7, 7);
			List<KingdomPlotPoseCandidate> candidates =
				KingdomPlotPoseSitingRules.EnumerateContaining(oldRect, interior, 3, 2);
			ClassicAssert.Greater(candidates.Count, 0);
			for (int i = 0; i < candidates.Count; i++)
			{
				Rect rect = candidates[i].Rect;
				ClassicAssert.IsTrue(rect.Contains(oldRect.X1, oldRect.Y1));
				ClassicAssert.IsTrue(rect.Contains(oldRect.X2, oldRect.Y2));
				ClassicAssert.IsTrue(interior.Contains(rect.X1, rect.Y1));
				ClassicAssert.IsTrue(interior.Contains(rect.X2, rect.Y2));
			}
		}

		[Test]
		public void GrowthSquarePoseIsDeduplicatedAndImpossibleGrowthIsEmpty()
		{
			List<KingdomPlotPoseCandidate> candidates =
				KingdomPlotPoseSitingRules.EnumerateContaining(
					new Rect(2, 2, 2, 2), new Rect(0, 0, 3, 3), 2, 2);
			ClassicAssert.AreEqual(4, candidates.Count);
			for (int i = 0; i < candidates.Count; i++)
				ClassicAssert.IsFalse(candidates[i].Transposed);
			ClassicAssert.AreEqual(0, KingdomPlotPoseSitingRules.EnumerateContaining(
				new Rect(0, 0, 3, 3), new Rect(0, 0, 4, 4), 3, 3).Count);
			ClassicAssert.IsTrue(KingdomPlotPoseSitingRules.IsStrictContainingEnvelope(
				new Rect(1, 1, 2, 2), new Rect(0, 0, 3, 3)));
			ClassicAssert.IsFalse(KingdomPlotPoseSitingRules.IsStrictContainingEnvelope(
				new Rect(1, 1, 2, 2), new Rect(1, 1, 2, 2)));
			ClassicAssert.IsFalse(KingdomPlotPoseSitingRules.IsStrictContainingEnvelope(
				new Rect(1, 1, 2, 2), new Rect(2, 1, 4, 3)));
		}

		private static void AssertRect(KingdomPlotPoseCandidate Candidate,
			int X1, int Y1, int X2, int Y2, bool Transposed)
		{
			ClassicAssert.AreEqual(X1, Candidate.Rect.X1);
			ClassicAssert.AreEqual(Y1, Candidate.Rect.Y1);
			ClassicAssert.AreEqual(X2, Candidate.Rect.X2);
			ClassicAssert.AreEqual(Y2, Candidate.Rect.Y2);
			ClassicAssert.AreEqual(Transposed, Candidate.Transposed);
		}
	}

	[TestFixture]
	public sealed class KingdomPoseAwareSitingSourceTests
	{
		[Test]
		public void OrdinarySitingFiltersExactAuthoredPosesBeforeLayoutScoring()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.08.Siting.cs");
			AssertOrdered(source,
				"KingdomPlotPoseSitingRules.Enumerate(",
				"KingdomPlotRules.CrowdsExisting(rect, laid)",
				"Grid.AnyRefusal(rect)",
				"KingdomArchitectureRuntime.TryCreateSitingProbe(",
				"KingdomPlotSelectionRules.TrySelect(groundCandidates,",
				"KingdomPlotRules.ChooseRect(");
			StringAssert.Contains("groundCandidates[0], Entry.Key, Entry.Category", source);
			// The nearest-rejected-failure selection itself moved into
			// Growth/KingdomPlotSelectionRules.cs (engine-free, value-tested there); this file
			// now only wires the real Zone-coupled resolver into that shard.
			StringAssert.Contains("candidate => ResolveArchitecture(probe, candidate, Z, occupiedCells)", source);
		}

		[Test]
		public void ProbeIsReadOnlyCachedAndRequiresPositiveAuthoredIngress()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureRuntime.SitingProbe.cs");
			AssertOrdered(source,
				"TryRectLotSize(SampleRect, out actualSize)",
				"KingdomArchitecture.TryGetMapping(BuildKey, LotType, actualSize, out mapping)",
				"TrySelectionContext(System, Z, out context",
				"Probe = new SitingProbe(");
			StringAssert.Contains("snapshotAttempted[index]", source);
			AssertOrdered(source, "KingdomArchitecture.TryResolve(buildKey, mapping.TypeKey,",
				"TryValidateFrozenSnapshot(resolved, out failure)",
				"snapshots[index] = resolved");
			StringAssert.Contains("ReadWornEvidence(Zone)", source);
			StringAssert.Contains("TryPhysicalRoadIngressLanes(zone, Rect, Snapshot, lanes",
				source);
			StringAssert.Contains("TryRoadEvidenceAt(lanes[i].X, lanes[i].Y", source);
			StringAssert.DoesNotContain("TryWorldAnchor(Snapshot, Rect, anchor", source);
			StringAssert.Contains("if (bestScore <= 0)", source);
			StringAssert.Contains(
				"has no authored public entrance connected to existing road evidence", source);
			StringAssert.DoesNotContain("ReservePayment", source);
			StringAssert.DoesNotContain("ReserveExactWater", source);
			StringAssert.DoesNotContain("TryFundNew", source);
			StringAssert.DoesNotContain("SetIntProperty", source);
			StringAssert.DoesNotContain("SetStringProperty", source);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int offset = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], offset, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered term: " + Terms[i]);
				offset = found + Terms[i].Length;
			}
		}
	}
}
#endif
