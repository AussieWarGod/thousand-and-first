#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomBenefitDeterminismTests
	{
		private static KingdomBenefitAllocationClaim Claim(string Key, int Roof,
			params string[] Tags)
		{
			KingdomBenefitAllocationClaim claim = new KingdomBenefitAllocationClaim {
				StableKey = Key, DesignationIdentity = "building" };
			if (Roof > 0) claim.ActiveAmounts.Add(new KindAmount("roof", Roof));
			claim.ActiveTags.AddRange(Tags); return claim;
		}

		[Test]
		public void ReversedClaimsKeepExactCapAndTagAttribution()
		{
			var a1 = Claim("a", 2, "quiet"); var b1 = Claim("b", 2, "quiet");
			Assert.IsTrue(Allocate(new[] { a1, b1 }, out var failure), failure);
			var a2 = Claim("a", 2, "quiet"); var b2 = Claim("b", 2, "quiet");
			Assert.IsTrue(Allocate(new[] { b2, a2 }, out failure), failure);
			Assert.AreEqual(2, a1.Credited[0].Amount);
			Assert.AreEqual(1, b1.Credited[0].Amount);
			Assert.AreEqual(2, a2.Credited[0].Amount);
			Assert.AreEqual(1, b2.Credited[0].Amount);
			CollectionAssert.AreEqual(new[] { "quiet" }, a1.CreditedTags);
			Assert.That(b1.CreditedTags, Is.Empty);
			CollectionAssert.AreEqual(a1.CreditedTags, a2.CreditedTags);
		}

		[Test]
		public void AllocationSeparatesWrongRoleFromAcceptedSaturation()
		{
			var first = Claim("a", 2, "quiet", "wrong");
			var second = Claim("b", 2, "quiet");
			Assert.That(Allocate(new[] { first, second }, out string failure), Is.True, failure);
			Assert.That(first.OutsideContract, Is.True);
			Assert.That(first.Saturated, Is.False);
			Assert.That(second.OutsideContract, Is.False);
			Assert.That(second.Saturated, Is.True);
			Assert.That(first.Limited, Is.True);
			Assert.That(second.Limited, Is.True);
		}

		[Test]
		public void AnonymousTagClaimsUseOperationEvidenceAsStableTieBreak()
		{
			var lowA = Claim("same|operation|050", 0, "quiet");
			var highA = Claim("same|operation|100", 0, "quiet");
			Assert.IsTrue(Allocate(new[] { highA, lowA }, out var failure), failure);
			var lowB = Claim("same|operation|050", 0, "quiet");
			var highB = Claim("same|operation|100", 0, "quiet");
			Assert.IsTrue(Allocate(new[] { lowB, highB }, out failure), failure);
			CollectionAssert.AreEqual(new[] { "quiet" }, lowA.CreditedTags);
			CollectionAssert.AreEqual(lowA.CreditedTags, lowB.CreditedTags);
			Assert.That(highA.CreditedTags, Is.Empty);
			Assert.That(highB.CreditedTags, Is.Empty);
		}

		[Test]
		public void ReversedAmountAndTagRowsNormalizeToOneOrderKey()
		{
			KingdomBenefitProviderDeclaration first = Declaration(
				new KindAmount("spirit", 1), new KindAmount("roof", 2));
			first.Provides.AddRange(new[] { "quiet", "dark" });
			KingdomBenefitProviderDeclaration second = Declaration(
				new KindAmount("roof", 2), new KindAmount("spirit", 1));
			second.Provides.AddRange(new[] { "dark", "quiet" });
			Assert.IsTrue(KingdomBenefitProviderRules.TryNormalize(first, out first, out _));
			Assert.IsTrue(KingdomBenefitProviderRules.TryNormalize(second, out second, out _));
			Assert.AreEqual(KingdomBenefitAllocationRules.DeclarationKey(first),
				KingdomBenefitAllocationRules.DeclarationKey(second));
		}

		[Test]
		public void AnonymousCollocatedRowsReceiveStableVisibleInstances()
		{
			string identity = "<anonymous:bed@4,5>#provider:taf:bed:type";
			var lowA = Inspection(identity, "same", 1);
			var highA = Inspection(identity, "same", 2);
			Assert.IsTrue(KingdomBenefitAllocationRules.TryOrderInspections(
				new[] { highA, lowA }, out var orderedA, out var failure), failure);
			var lowB = Inspection(identity, "same", 1);
			var highB = Inspection(identity, "same", 2);
			Assert.IsTrue(KingdomBenefitAllocationRules.TryOrderInspections(
				new[] { lowB, highB }, out var orderedB, out failure), failure);
			CollectionAssert.AreEqual(Snapshot(orderedA), Snapshot(orderedB));
			CollectionAssert.AreEqual(new[] { identity + ":instance-0001",
				identity + ":instance-0002" }, new[] {
				orderedA[0].Inspection.ProviderIdentity,
				orderedA[1].Inspection.ProviderIdentity });
		}

		[Test]
		public void OversizeRosterReturnsBeforeIndexOrEnumeration()
		{
			var hostile = new OverBoundClaims();
			Assert.IsFalse(KingdomBenefitAllocationRules.TryAllocate(
				new[] { new KindAmount("roof", 3) }, Array.Empty<string>(), hostile,
				out _, out var failure));
			StringAssert.Contains("over-bound", failure);
			Assert.AreEqual(0, hostile.Reads);
			Assert.IsFalse(hostile.Enumerated);
		}

		[Test]
		public void BoundedThrowingRosterFailsClosedWithoutEnumeration()
		{
			var hostile = new ThrowingClaims();
			Assert.IsFalse(KingdomBenefitAllocationRules.TryAllocate(
				new[] { new KindAmount("roof", 3) }, Array.Empty<string>(), hostile,
				out _, out var failure));
			StringAssert.Contains("InvalidOperationException", failure);
			Assert.AreEqual(1, hostile.Reads);
			Assert.IsFalse(hostile.Enumerated);
		}

		[Test]
		public void MalformedClaimCannotPartiallyRewritePriorOutputs()
		{
			var sound = Claim("a", 1);
			sound.Credited.Add(new KindAmount("sentinel", 7));
			var malformed = Claim("b", 1);
			malformed.ActiveAmounts.Add(new KindAmount("roof", 2));
			Assert.IsFalse(KingdomBenefitAllocationRules.TryAllocate(
				new[] { new KindAmount("roof", 3) }, Array.Empty<string>(),
				new[] { sound, malformed },
				out _, out var failure));
			StringAssert.Contains("duplicated", failure);
			Assert.AreEqual("sentinel", sound.Credited[0].Kind);
			Assert.AreEqual(7, sound.Credited[0].Amount);
			Assert.AreEqual("roof", sound.ActiveAmounts[0].Kind);
		}

		[Test]
		public void RuntimeCollectsThenNormalizesThenEvaluatesThenAllocates()
		{
			string build = Read("Growth/KingdomBenefitIndex.Build.cs");
			AssertOrdered(build, "TryCollectProviders", "candidates.Sort", "result.Evaluate",
				"result.AllocatePending", "FinalizeInspectionOrder");
			string collect = Read("Growth/KingdomBenefitIndex.Collect.cs");
			AssertOrdered(collect, "long explicitCount = 0L; long nativeOnlyCount = 0L",
				"CountProviderParts(item", "NativeProviderCount(item, false)",
				"List<ProviderObjectBatch> batches", "batches.Sort", "Describe(batch, Result)",
				"AddNative(batches[i]");
			AssertOrdered(collect, "private static void Describe",
				"ObserveDescription", "Batch.Candidates.Add");
			string declaration = Read("Growth/KingdomBenefitIndex.DeclarationReproof.cs");
			AssertOrdered(declaration, "private static string ObserveDescription",
				"TryDescribeKingdomBenefits", "KingdomBenefitProviderRules.TryNormalize");
			StringAssert.Contains("KingdomBenefitAdmissionRules.TryAdmitWholeGroup", collect);
			StringAssert.Contains("KingdomBenefitAdmissionRules.NativePrefix", collect);
			StringAssert.Contains("KingdomBenefitFault.ObservationLimit", collect);
			StringAssert.Contains("reproofExplicit != explicitCount", collect);
			StringAssert.DoesNotContain(
				"physical provider inspection exceeded its bounded zone rows", collect);
			StringAssert.DoesNotContain("SnapshotOrdinal", collect);
			string evaluate = Read("Growth/KingdomBenefitIndex.Evaluate.cs");
			StringAssert.Contains("aggregate.Pending.Add", evaluate);
			StringAssert.Contains("operation.ToString(\"D3\"", evaluate);
			StringAssert.DoesNotContain("CreditAmount(aggregate", evaluate);
			StringAssert.DoesNotContain("CreditTag(aggregate", evaluate);
			string identity = Read("Growth/KingdomBenefitIndex.Identity.cs");
			StringAssert.Contains("<anonymous:", identity);
			StringAssert.DoesNotContain("SnapshotOrdinal", identity);
		}

		private static bool Allocate(IReadOnlyList<KingdomBenefitAllocationClaim> Claims,
			out string Failure)
		{
			return KingdomBenefitAllocationRules.TryAllocate(
				new[] { new KindAmount("roof", 3) }, new[] { "quiet" }, Claims,
				out _, out Failure);
		}

		private static KingdomBenefitProviderDeclaration Declaration(params KindAmount[] Rows)
		{
			KingdomBenefitProviderDeclaration result = new KingdomBenefitProviderDeclaration {
				Key = "test:key", Scope = KingdomBenefitScope.Building,
				Operation = KingdomBenefitOperation.Present };
			result.Carries.AddRange(Rows); return result;
		}

		private static KingdomBenefitInspectionOrderRow Inspection(string Identity,
			string Anchor, int Credited)
		{
			KingdomBenefitInspection row = new KingdomBenefitInspection {
				ProviderKey = "taf:bed", DesignationIdentity = "building",
				OperationPercent = 100 };
			row.Offered.Add(new KindAmount("roof", 2));
			row.Credited.Add(new KindAmount("roof", Credited));
			return new KingdomBenefitInspectionOrderRow { Inspection = row,
				IdentityBase = Identity, StableAnchor = Anchor };
		}

		private static string[] Snapshot(List<KingdomBenefitInspectionOrderRow> Rows)
		{
			string[] result = new string[Rows.Count];
			for (int i = 0; i < Rows.Count; i++) result[i] = Rows[i].Inspection.ProviderIdentity
				+ "=" + Rows[i].Inspection.Credited[0].Amount;
			return result;
		}

		private static string Read(string Path) => TestMain.ReadRepositoryText(Path);
		private static void AssertOrdered(string Text, params string[] Terms)
		{
			int prior = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int at = Text.IndexOf(Terms[i], StringComparison.Ordinal);
				Assert.Greater(at, prior, Terms[i]); prior = at;
			}
		}

		private sealed class OverBoundClaims : IReadOnlyList<KingdomBenefitAllocationClaim>
		{
			internal int Reads;
			internal bool Enumerated;
			public int Count => KingdomBenefitEmbodimentRules.MaxProvidersPerZone + 1;
			public KingdomBenefitAllocationClaim this[int Index]
			{
				get { Reads++; throw new InvalidOperationException("read past cap"); }
			}
			public IEnumerator<KingdomBenefitAllocationClaim> GetEnumerator()
			{
				Enumerated = true; throw new InvalidOperationException("enumerated past cap");
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		private sealed class ThrowingClaims : IReadOnlyList<KingdomBenefitAllocationClaim>
		{
			internal int Reads;
			internal bool Enumerated;
			public int Count => 1;
			public KingdomBenefitAllocationClaim this[int Index]
			{
				get { Reads++; throw new InvalidOperationException("hostile read"); }
			}
			public IEnumerator<KingdomBenefitAllocationClaim> GetEnumerator()
			{
				Enumerated = true; throw new InvalidOperationException("hostile enumeration");
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
#endif
