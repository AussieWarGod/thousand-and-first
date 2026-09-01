using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static readonly int[] AccessDx = { 1, -1, 0, 0 };
		private static readonly int[] AccessDy = { 0, 0, 1, -1 };

		private bool Accessible(Aggregate Row, KingdomDesignationMatch Match, Zone Z)
		{
			if (!Row.AccessRead) BuildReachable(Row, Z);
			if (Row.Reachable == null) return false;
			long own = KingdomDesignationRules.Pack(Match.X, Match.Y);
			if (Row.Reachable.Contains(own)) return true;
			for (int i = 0; i < 4; i++)
				if (Row.Reachable.Contains(KingdomDesignationRules.Pack(
					Match.X + AccessDx[i], Match.Y + AccessDy[i]))) return true;
			return false;
		}

		private static void BuildReachable(Aggregate Row, Zone Z)
		{
			Row.AccessRead = true;
			KingdomBenefitAccessRules.TryReachable(Row.Reading.Designation.Cells,
				delegate(int x, int y) {
					Cell live = Z.GetCell(x, y);
					return live != null && live.IsPassable(null, false);
				}, out Row.Reachable);
		}
	}
}
