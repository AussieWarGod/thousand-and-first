#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomBenefitAccessRulesTests
	{
		[Test]
		public void ExplicitIngressOpensWholeZoneDesignation()
		{
			List<KingdomBenefitCell> cells = Square(3, 3, 1, 0);
			Assert.IsTrue(KingdomBenefitAccessRules.TryReachable(cells,
				(x, y) => x >= 0 && x < 3 && y >= 0 && y < 3, out HashSet<long> reached));
			Assert.AreEqual(9, reached.Count);
		}

		[Test]
		public void BlockedIngressAndBlockedOutsideFailClosed()
		{
			List<KingdomBenefitCell> cells = Square(3, 3, 1, 0);
			Assert.IsTrue(KingdomBenefitAccessRules.TryReachable(cells,
				(x, y) => x >= 0 && x < 3 && y >= 0 && y < 3 && !(x == 1 && y == 0),
				out HashSet<long> reached));
			Assert.AreEqual(0, reached.Count);
		}

		[Test]
		public void SolidDividerLeavesProviderSideUnreachable()
		{
			List<KingdomBenefitCell> cells = Square(3, 3, 0, 1);
			Assert.IsTrue(KingdomBenefitAccessRules.TryReachable(cells,
				(x, y) => x >= 0 && x < 3 && y >= 0 && y < 3 && x != 1,
				out HashSet<long> reached));
			Assert.IsTrue(reached.Contains(KingdomDesignationRules.Pack(0, 1)));
			Assert.IsFalse(reached.Contains(KingdomDesignationRules.Pack(2, 1)));
		}

		[Test]
		public void DuplicateCellsAndThrowingPassabilityAreRejected()
		{
			List<KingdomBenefitCell> duplicate = Square(2, 2, 0, 0);
			duplicate.Add(duplicate[0]);
			Assert.IsFalse(KingdomBenefitAccessRules.TryReachable(duplicate,
				(x, y) => true, out HashSet<long> ignored));
			Assert.IsFalse(KingdomBenefitAccessRules.TryReachable(Square(2, 2, 0, 0),
				(x, y) => throw new InvalidOperationException(), out ignored));
		}

		private static List<KingdomBenefitCell> Square(int Width, int Height,
			int IngressX, int IngressY)
		{
			List<KingdomBenefitCell> cells = new List<KingdomBenefitCell>();
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
				{
					KingdomBenefitCellUse use = KingdomBenefitCellUse.Plot
						| KingdomBenefitCellUse.Building;
					if (x == IngressX && y == IngressY) use |= KingdomBenefitCellUse.Ingress;
					cells.Add(new KingdomBenefitCell(x, y, use));
				}
			return cells;
		}
	}
}
#endif
