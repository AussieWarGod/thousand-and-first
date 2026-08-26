using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSemanticSelectionRulesTests
	{
		private static readonly KernelSeed128 Seed =
			new KernelSeed128(0x1020304050607080UL, 0x8877665544332211UL);
		private const string Settlement = "taf:settlement:semantic-test";
		private const string Stream = "taf:semantic:test:v1";

		[Test]
		public void SemanticAdapterAndFrozenPersonPlanKeepExactInternalAbiAndDefaults()
		{
			string source = KingdomSemanticSelectionLogicalSource.Read();
			StringAssert.Contains("namespace ThousandAndFirst", source);
			StringAssert.Contains("internal static partial class KingdomSemanticSelection", source);
			StringAssert.Contains("internal sealed class KingdomSemanticPersonPlan", source);

			string[] declarations =
			{
				"internal int RulesVersion;",
				"internal long Sequence;",
				"internal string StreamId;",
				"internal uint EventKind;",
				"internal string Blueprint;",
				"internal string Origin;",
				"internal string Creed;",
				"internal string Name;",
				"internal string Title;",
				"internal string Arrived;",
				"internal int X = -1;",
				"internal int Y = -1;"
			};
			int previous = -1;
			for (int i = 0; i < declarations.Length; i++)
			{
				int at = source.IndexOf(declarations[i], StringComparison.Ordinal);
				Assert.Greater(at, previous, "person plan field order/default " + i);
				Assert.AreEqual(at, source.LastIndexOf(declarations[i], StringComparison.Ordinal),
					"person plan field declaration must remain unique: " + declarations[i]);
				previous = at;
			}
		}

		[Test]
		public void CanonicalCatalogueFoldsDuplicateWeightsAndSortsStableKeys()
		{
			List<KingdomSemanticWeightedEntry> input = Rows(
				("zeta", 3UL), ("alpha", 2UL), ("zeta", 5UL), ("middle", 7UL));
			List<KingdomSemanticWeightedEntry> canonical;
			ulong total;
			KingdomSemanticSelectionFault fault;
			Assert.IsTrue(KingdomSemanticSelectionRules.TryCanonicalize(input,
				out canonical, out total, out fault));
			Assert.AreEqual(3, canonical.Count);
			Assert.AreEqual("alpha", canonical[0].StableKey);
			Assert.AreEqual("middle", canonical[1].StableKey);
			Assert.AreEqual("zeta", canonical[2].StableKey);
			Assert.AreEqual(8UL, canonical[2].Weight);
			Assert.AreEqual(17UL, total);
		}

		[Test]
		public void MergeOrderRetryAndReloadCannotChangeSelection()
		{
			List<KingdomSemanticWeightedEntry> first = Rows(
				("snapjaw", 5UL), ("human", 25UL), ("tinker", 10UL));
			List<KingdomSemanticWeightedEntry> reordered = Rows(
				("tinker", 4UL), ("human", 25UL), ("snapjaw", 5UL), ("tinker", 6UL));
			for (ulong ordinal = 1UL; ordinal <= 64UL; ordinal++)
			{
				string a = Choose(first, ordinal, 0U, 1);
				string retry = Choose(first, ordinal, 0U, 1);
				string reload = Choose(reordered, ordinal, 0U, 1);
				Assert.AreEqual(a, retry, "retry " + ordinal);
				Assert.AreEqual(a, reload, "merge order " + ordinal);
			}
		}

		[Test]
		public void RulesVersionSequenceAndDrawIndexAreIndependentCoordinates()
		{
			List<KingdomSemanticWeightedEntry> rows = Rows(
				("a", 1UL), ("b", 1UL), ("c", 1UL), ("d", 1UL));
			HashSet<string> sequenceResults = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> versionResults = new HashSet<string>(StringComparer.Ordinal);
			for (ulong ordinal = 1UL; ordinal <= 32UL; ordinal++)
			{
				sequenceResults.Add(Choose(rows, ordinal, 0U, 1));
				versionResults.Add(Choose(rows, ordinal, 0U, 2));
			}
			Assert.Greater(sequenceResults.Count, 1);
			Assert.Greater(versionResults.Count, 1);
			bool differs = false;
			for (ulong ordinal = 1UL; ordinal <= 32UL; ordinal++)
				if (Choose(rows, ordinal, 0U, 1) != Choose(rows, ordinal, 0U, 2))
					differs = true;
			Assert.IsTrue(differs, "rules version must enter the preimage");
		}

		[Test]
		public void UnrelatedCadenceCannotAdvanceOrPerturbAnEventDraw()
		{
			List<KingdomSemanticWeightedEntry> rows = Rows(
				("a", 1UL), ("b", 2UL), ("c", 3UL), ("d", 5UL));
			string frozen = Choose(rows, 27UL, 0U, 1);
			for (ulong ordinal = 1UL; ordinal <= 128UL; ordinal++)
			{
				Choose(rows, ordinal, 7U, 1);
				Choose(rows, ordinal, 9U, 1);
			}
			Assert.AreEqual(frozen, Choose(rows, 27UL, 0U, 1));
		}

		[Test]
		public void VersionedNamesAreBoundedStableAndQudLiterate()
		{
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			Assert.IsTrue(SemanticEventKey.TryCreate(1, Settlement, Stream, 1U, 77UL,
				out key, out kernelFault));
			string first;
			string again;
			KingdomSemanticSelectionFault fault;
			Assert.IsTrue(KingdomSemanticSelectionRules.TryName(Seed, key, 2U,
				out first, out fault));
			Assert.IsTrue(KingdomSemanticSelectionRules.TryName(Seed, key, 2U,
				out again, out fault));
			Assert.AreEqual(first, again);
			Assert.That(first.Length, Is.InRange(2, 16));
			Assert.IsTrue(char.IsUpper(first[0]));
			StringAssert.DoesNotContain("{", first);
		}

		[Test]
		public void FixedProbeVisitsEveryCoordinateExactlyOnce()
		{
			const int width = 7;
			const int height = 5;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			Assert.IsTrue(SemanticEventKey.TryCreate(1, Settlement, Stream, 1U, 91UL,
				out key, out kernelFault));
			int start;
			KingdomSemanticSelectionFault fault;
			Assert.IsTrue(KingdomSemanticSelectionRules.TryProbeStart(Seed, key, 6U,
				width, height, out start, out fault));
			HashSet<int> visited = new HashSet<int>();
			for (int offset = 0; offset < width * height; offset++)
				Assert.IsTrue(visited.Add(KingdomSemanticSelectionRules.ProbeIndex(start,
					offset, width * height)));
			Assert.AreEqual(width * height, visited.Count);
		}

		[Test]
		public void DurableOwnerStreamIsStableAdmittedAndDomainSeparated()
		{
			string a;
			string again;
			string other;
			Assert.IsTrue(KingdomSemanticSelectionRules.TryOwnerStreamId("furnish",
				"hut@3.4.9000", out a));
			Assert.IsTrue(KingdomSemanticSelectionRules.TryOwnerStreamId("furnish",
				"hut@3.4.9000", out again));
			Assert.IsTrue(KingdomSemanticSelectionRules.TryOwnerStreamId("other",
				"hut@3.4.9000", out other));
			Assert.AreEqual(a, again);
			Assert.AreNotEqual(a, other);
			Assert.IsTrue(KernelSemanticId.IsValid(a));
		}

		[Test]
		public void InvalidAndOversizedCataloguesFailClosed()
		{
			List<KingdomSemanticWeightedEntry> zero = Rows(("a", 0UL));
			List<KingdomSemanticWeightedEntry> canonical;
			ulong total;
			KingdomSemanticSelectionFault fault;
			Assert.IsFalse(KingdomSemanticSelectionRules.TryCanonicalize(zero,
				out canonical, out total, out fault));
			List<KingdomSemanticWeightedEntry> large = new List<KingdomSemanticWeightedEntry>();
			for (int i = 0; i <= KingdomSemanticSelectionRules.MaxCatalogueEntries; i++)
				large.Add(new KingdomSemanticWeightedEntry("row-" + i, 1UL));
			Assert.IsFalse(KingdomSemanticSelectionRules.TryCanonicalize(large,
				out canonical, out total, out fault));
			Assert.AreEqual(KingdomSemanticSelectionFault.CatalogueTooLarge, fault);
		}

		private static string Choose(IList<KingdomSemanticWeightedEntry> rows,
			ulong ordinal, uint draw, int version)
		{
			string selected;
			KingdomSemanticSelectionFault fault;
			Assert.IsTrue(KingdomSemanticSelectionRules.TryChoose(Seed, version,
				Settlement, Stream, 1U, ordinal, draw, rows, out selected, out fault),
				fault.ToString());
			return selected;
		}

		private static List<KingdomSemanticWeightedEntry> Rows(
			params (string Key, ulong Weight)[] rows)
		{
			List<KingdomSemanticWeightedEntry> result =
				new List<KingdomSemanticWeightedEntry>(rows.Length);
			for (int i = 0; i < rows.Length; i++)
				result.Add(new KingdomSemanticWeightedEntry(rows[i].Key, rows[i].Weight));
			return result;
		}
	}
}
