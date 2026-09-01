#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomForeignFootprintSnapshotRulesTests
	{
		private static ArchitecturePoint P(int X, int Y) => new ArchitecturePoint(X, Y);

		private static KingdomForeignFootprintEvidence Row(string Provider, string Identity,
			string Refusal, params ArchitecturePoint[] Cells)
		{
			return new KingdomForeignFootprintEvidence { ProviderId = Provider,
				ProviderVersion = "1", Identity = Identity, Revision = "rev-" + Identity,
				Refusal = Refusal ?? "", ZoneId = "zone", OriginX = Cells[0].X,
				OriginY = Cells[0].Y, Cells = new List<ArchitecturePoint>(Cells) };
		}

		private static KingdomForeignProviderSnapshot Observed(string Provider,
			params KingdomForeignFootprintEvidence[] Rows)
		{
			KingdomForeignProviderSnapshot result = new KingdomForeignProviderSnapshot {
				ProviderId = Provider, ProviderVersion = "1",
				Status = KingdomForeignProviderStatus.Observed };
			result.Rows.AddRange(Rows); return result;
		}

		private static KingdomForeignProviderSnapshot Status(string Provider,
			KingdomForeignProviderStatus Status, string Fault = null)
		{
			return new KingdomForeignProviderSnapshot { ProviderId = Provider,
				ProviderVersion = "1", Status = Status, Fault = Fault };
		}

		[TestCase(false, false, false, 0)]
		[TestCase(false, true, false, 2)]
		[TestCase(false, false, true, 2)]
		[TestCase(false, true, true, 2)]
		[TestCase(true, false, false, 2)]
		[TestCase(true, true, false, 1)]
		[TestCase(true, false, true, 2)]
		[TestCase(true, true, true, 2)]
		public void OnlyFalseNullNullMeansAbsence(bool Returned, bool RowsPresent,
			bool FailurePresent, int Expected)
		{
			Assert.AreEqual((KingdomForeignProviderStatus)Expected,
				KingdomForeignFootprintSnapshotRules.ClassifyCall(
					Returned, RowsPresent, FailurePresent));
		}

		[Test]
		public void ProviderPreflightRejectsOverBoundRosterWithoutReadingRows()
		{
			var hostile = new HostileCounts(
				KingdomForeignFootprintSnapshotRules.MaxRowsPerProvider + 1);
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryProviderPreflight(
				hostile.Count, hostile, out var failure));
			StringAssert.Contains("row budget", failure);
			Assert.AreEqual(0, hostile.Reads);
		}

		[Test]
		public void ProviderPreflightBoundsCellsBeforeExactCellEnumeration()
		{
			int rows = KingdomForeignFootprintSnapshotRules.MaxRowsPerProvider;
			int[] crowded = new int[rows];
			for (int i = 0; i < crowded.Length; i++)
				crowded[i] = KingdomDesignationRules.MaxCellsPerDesignation;
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryProviderPreflight(
				rows, crowded, out var failure));
			StringAssert.Contains("cell budget", failure);

			System.Array.Reverse(crowded);
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryProviderPreflight(
				rows, crowded, out var reversed));
			Assert.AreEqual(failure, reversed);
		}

		[Test]
		public void InvalidCellListSiblingRemainsRowLocalDuringPreflight()
		{
			int overRow = KingdomDesignationRules.MaxCellsPerDesignation + 1;
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryProviderPreflight(4,
				new[] { -1, 0, overRow, 2 }, out var failure), failure);
		}

		[Test]
		public void ExactMatchBindsAndDisjointOrAbsentGroundRemainsOrdinary()
		{
			List<ArchitecturePoint> wanted = new List<ArchitecturePoint> { P(1, 1), P(2, 1) };
			var exact = Row("one", "home", "", P(1, 1), P(2, 1));
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryMatch(
				new[] { Observed("one", exact), Status("two",
					KingdomForeignProviderStatus.Absent) }, wanted, out var match, out var failure), failure);
			Assert.AreSame(exact, match);

			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryMatch(
				new[] { Observed("one", Row("one", "far", "", P(9, 9))) }, wanted,
				out match, out failure), failure);
			Assert.IsNull(match);
		}

		[TestCase("subset")]
		[TestCase("superset")]
		[TestCase("partial")]
		public void EveryNonExactIntersectionFailsClosed(string Case)
		{
			var row = Row("one", "home", "", P(1, 1), P(2, 1));
			ArchitecturePoint[] wanted = Case == "subset" ? new[] { P(1, 1) }
				: Case == "superset" ? new[] { P(1, 1), P(2, 1), P(3, 1) }
				: new[] { P(2, 1), P(3, 1) };
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryMatch(
				new[] { Observed("one", row) }, wanted, out _, out var failure), Case);
			StringAssert.Contains("partially intersects", failure);
		}

		[Test]
		public void RefusalCanCoexistLocallyButAnyRefusedIntersectionBlocks()
		{
			var accepted = Row("one", "home", "", P(1, 1), P(2, 1));
			var refused = Row("one", "uncertain", "foreign room is uncertain", P(8, 8));
			var snapshots = new[] { Observed("one", accepted, refused) };
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryMatch(snapshots,
				new[] { P(1, 1), P(2, 1) }, out var match, out var failure), failure);
			Assert.AreSame(accepted, match);
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryMatch(snapshots,
				new[] { P(8, 8), P(9, 8) }, out _, out failure));
			StringAssert.Contains("refused", failure);
		}

		[Test]
		public void UnrelatedProviderFaultIsQuarantinedFromOrdinaryAdoption()
		{
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryMatch(new[] {
				Status("broken", KingdomForeignProviderStatus.Faulted, "snapshot failed") },
				new[] { P(1, 1) }, out var match, out var failure), failure);
			Assert.IsNull(match);
		}

		[Test]
		public void ObservedRowFaultDoesNotEraseHealthySibling()
		{
			var healthy = Row("one", "home", "", P(1, 1), P(2, 1));
			var snapshot = Observed("one", healthy);
			snapshot.RowFaults.Add("malformed sibling had no bounded exact cells");
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryValidate(
				new[] { snapshot }, out var failure), failure);
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryMatch(
				new[] { snapshot }, healthy.Cells, out var match, out failure), failure);
			Assert.AreSame(healthy, match);
		}

		[Test]
		public void ReproofChecksBoundProviderStateThenExactEvidenceAndGlobalOverlap()
		{
			var bound = Row("one", "home", "", P(1, 1), P(2, 1));
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryReprove(new[] {
				Status("one", KingdomForeignProviderStatus.Faulted, "read failed") },
				"one", "1", "home", "rev-home", bound.Cells, out var failure));
			StringAssert.StartsWith("bound foreign footprint provider is faulted", failure);

			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryReprove(new[] {
				Observed("one", bound), Observed("two", Row("two", "intruder", "", P(2, 1))) },
				"one", "1", "home", "rev-home", bound.Cells, out failure));
			StringAssert.Contains("intersects other foreign ground", failure);

			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryReprove(new[] {
				Observed("one", bound), Observed("two", Row("two", "far", "", P(9, 9))),
				Status("broken", KingdomForeignProviderStatus.Faulted, "read failed") },
				"one", "1", "home", "rev-home", bound.Cells, out failure), failure);
		}

		[Test]
		public void RowFaultsAreStrictlyBoundedAndSanitized()
		{
			var snapshot = Observed("one", Row("one", "home", "", P(1, 1)));
			for (int i = 0; i <= KingdomForeignFootprintSnapshotRules.MaxFaultsPerProvider; i++)
				snapshot.RowFaults.Add("fault-" + i);
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryValidate(
				new[] { snapshot }, out var failure));
			StringAssert.Contains("status is inconsistent", failure);

			snapshot.RowFaults.Clear(); snapshot.RowFaults.Add("bad\nfault");
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryValidate(
				new[] { snapshot }, out failure));
			StringAssert.Contains("row fault is malformed", failure);
		}

		[Test]
		public void GlobalAdmissionIsFairAndIndependentOfProviderRosterOrder()
		{
			List<KingdomForeignProviderSnapshot> first = Crowded("zulu", "alpha");
			List<KingdomForeignProviderSnapshot> second = Crowded("alpha", "zulu");
			KingdomForeignFootprintBudgetRules.Apply(first);
			KingdomForeignFootprintBudgetRules.Apply(second);
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryValidate(first,
				out var failure), failure);
			Assert.IsTrue(KingdomForeignFootprintSnapshotRules.TryValidate(second,
				out failure), failure);
			Assert.AreEqual(KingdomForeignFootprintSnapshotRules.MaxRows,
				first[0].Rows.Count + first[1].Rows.Count);
			Assert.AreEqual(256, first[0].Rows.Count);
			Assert.AreEqual(256, first[1].Rows.Count);
			CollectionAssert.AreEqual(Identities(first[0]), Identities(second[0]));
			CollectionAssert.AreEqual(Identities(first[1]), Identities(second[1]));
			Assert.That(first[0].RowFaults, Is.Not.Empty);
			Assert.That(first[1].RowFaults, Is.Not.Empty);
		}

		private static List<KingdomForeignProviderSnapshot> Crowded(string First, string Second)
		{
			var one = Observed(First); var two = Observed(Second);
			for (int i = 0; i < 300; i++)
			{
				one.Rows.Add(Row(First, "row-" + i, "", P(i, 0)));
				two.Rows.Add(Row(Second, "row-" + i, "", P(i, 1)));
			}
			return new List<KingdomForeignProviderSnapshot> { one, two };
		}

		private static string[] Identities(KingdomForeignProviderSnapshot Snapshot)
		{
			string[] result = new string[Snapshot.Rows.Count];
			for (int i = 0; i < result.Length; i++) result[i] = Snapshot.Rows[i].Identity;
			return result;
		}

		private sealed class HostileCounts : IReadOnlyList<int>
		{
			private readonly int Length;
			internal int Reads;

			internal HostileCounts(int Length) { this.Length = Length; }
			public int Count => Length;
			public int this[int Index]
			{
				get { Reads++; throw new System.InvalidOperationException("count row read"); }
			}
			public IEnumerator<int> GetEnumerator()
			{
				throw new System.InvalidOperationException("count rows enumerated");
			}
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		[Test]
		public void ProviderSnapshotsRejectInconsistentStatusAndDuplicateIdentity()
		{
			var absentWithRows = Status("one", KingdomForeignProviderStatus.Absent);
			absentWithRows.Rows.Add(Row("one", "home", "", P(1, 1)));
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryValidate(
				new[] { absentWithRows }, out var failure));
			StringAssert.Contains("status is inconsistent", failure);

			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryValidate(new[] {
				Observed("one", Row("one", "same", "", P(1, 1)),
					Row("one", "same", "", P(2, 1))) }, out failure));
			StringAssert.Contains("malformed or duplicated", failure);
		}

		[Test]
		public void AggregateRowAndCellBudgetsAreGlobalAndExact()
		{
			var tooManyRows = Observed("one");
			for (int i = 0; i <= KingdomForeignFootprintSnapshotRules.MaxRows; i++)
				tooManyRows.Rows.Add(Row("one", "row-" + i, "", P(0, i)));
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryValidate(
				new[] { tooManyRows }, out var failure));
			StringAssert.Contains("row budget", failure);

			var tooManyCells = Observed("one");
			for (int row = 0; row < 33; row++)
			{
				ArchitecturePoint[] cells = new ArchitecturePoint[2000];
				for (int x = 0; x < cells.Length; x++) cells[x] = P(x, row);
				tooManyCells.Rows.Add(Row("one", "wide-" + row, "", cells));
			}
			Assert.IsFalse(KingdomForeignFootprintSnapshotRules.TryValidate(
				new[] { tooManyCells }, out failure));
			StringAssert.Contains("cell budget", failure);
		}
	}
}
#endif
