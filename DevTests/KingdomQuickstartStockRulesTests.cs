#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomQuickstartStockRulesTests
	{
		private sealed class Child
		{
			internal readonly int Count;
			internal Child(int Count = 1) { this.Count = Count; }
			public override bool Equals(object Other)
			{ throw new InvalidOperationException("Physical identity must not invoke value equality."); }
			public override int GetHashCode()
			{ throw new InvalidOperationException("Physical identity must not invoke object hashing."); }
		}

		[Test]
		public void AbsentListRefuses()
		{ Assert.That(KingdomQuickstartStockRules.HasDistinctChildren<Child>(null), Is.False); }

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(3)]
		[TestCase(12)]
		public void DistinctReferencesPassWithoutValueEqualityOrMutation(int Count)
		{
			var children = new List<Child>();
			for (int i = 0; i < Count; i++) children.Add(new Child());
			Check(children, true);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		[TestCase(7)]
		[TestCase(8)]
		[TestCase(9)]
		[TestCase(10)]
		[TestCase(11)]
		public void RepeatedReferenceCannotRepresentTwelveMeals(int DuplicateIndex)
		{
			var children = new List<Child>();
			for (int i = 0; i < 12; i++) children.Add(new Child());
			children[DuplicateIndex] = children[(DuplicateIndex + 1) % 12];
			Check(children, false);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void MissingChildRefusesWithoutChangingRows(int MissingIndex)
		{
			var children = new List<Child> { new Child(), new Child(), new Child() };
			children[MissingIndex] = null;
			Check(children, false);
		}

		[Test]
		public void DistinctPhysicalStacksRetainTheirFullQuantity()
		{
			Check(new List<Child> { new Child(12) }, true);
			Check(new List<Child> { new Child(6), new Child(6) }, true);
			Check(new List<Child> { new Child(1), new Child(3), new Child(4) }, true);
		}

		[Test]
		public void RepeatedHalfTimberStackCannotSupplyFourTimber()
		{
			var timber = new Child(2);
			Check(new List<Child> { new Child(1), new Child(3), timber, timber }, false);
		}

		private static void Check(List<Child> Children, bool Expected)
		{
			Child[] before = Children.ToArray();
			int[] counts = new int[before.Length];
			for (int i = 0; i < before.Length; i++) counts[i] = before[i]?.Count ?? 0;
			Assert.That(KingdomQuickstartStockRules.HasDistinctChildren(Children), Is.EqualTo(Expected));
			Assert.That(Children.Count, Is.EqualTo(before.Length));
			for (int i = 0; i < before.Length; i++)
			{
				Assert.That(ReferenceEquals(Children[i], before[i]), Is.True);
				Assert.That(Children[i]?.Count ?? 0, Is.EqualTo(counts[i]));
			}
		}
	}
}
#endif
