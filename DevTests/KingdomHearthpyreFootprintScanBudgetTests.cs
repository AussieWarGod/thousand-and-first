#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Integrations.Hearthpyre223;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomHearthpyreFootprintScanBudgetTests
	{
		[Test]
		public void ExactWorkBoundaryIsUsableAndExhaustionIsSticky()
		{
			int groups = KingdomHearthpyreFootprintScanBudget.MaxInspectionWork
				/ KingdomHearthpyreFootprintScanBudget.MaxRegistryEntries;
			int[] exact = new int[groups];
			Array.Fill(exact,
				KingdomHearthpyreFootprintScanBudget.MaxRegistryEntries);
			Assert.That(KingdomHearthpyreFootprintScanBudget.TryAccount(exact,
				out int used, out string failure), Is.True, failure);
			Assert.That(used,
				Is.EqualTo(KingdomHearthpyreFootprintScanBudget.MaxInspectionWork));

			KingdomHearthpyreFootprintScanBudget budget =
				new KingdomHearthpyreFootprintScanBudget();
			Assert.That(budget.TryCharge(
				KingdomHearthpyreFootprintScanBudget.MaxRegistryEntries + 1), Is.False);
			Assert.That(budget.Exhausted, Is.True);
			Assert.That(budget.TryCharge(0), Is.False);
		}

		[Test]
		public void MaxRegistryAndSectorCompositionFailsIdenticallyWhenReversed()
		{
			int maximum = KingdomHearthpyreFootprintScanBudget.MaxRegistryEntries;
			int groups = KingdomHearthpyreFootprintScanBudget.MaxInspectionWork / maximum;
			int[] forward = new int[groups + 2];
			for (int i = 0; i < groups - 1; i++) forward[i] = maximum;
			forward[groups - 1] = maximum - 1;
			forward[groups] = 1;
			forward[groups + 1] = 1;
			int[] reverse = (int[])forward.Clone(); Array.Reverse(reverse);

			Assert.That(KingdomHearthpyreFootprintScanBudget.TryAccount(forward,
				out _, out string first), Is.False);
			Assert.That(KingdomHearthpyreFootprintScanBudget.TryAccount(reverse,
				out _, out string second), Is.False);
			Assert.That(first, Is.EqualTo(
				KingdomHearthpyreFootprintScanBudget.LimitFailure));
			Assert.That(second, Is.EqualTo(first));
		}
	}
}
#endif
