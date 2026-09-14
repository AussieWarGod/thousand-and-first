using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		internal static bool TryNewBlockingCells(Zone Z, KingdomArchitectureIntent Before,
			KingdomArchitectureIntent After, out Dictionary<int, ArchitecturePassability> Cells,
			out string Failure)
		{
			Cells = null;
			if (!TryPlacementPassability(Before, Z, out var oldSlots, out Failure)
				|| !TryPlacementPassability(After, Z, out var newSlots, out Failure)) return false;
			Cells = new Dictionary<int, ArchitecturePassability>();
			foreach (var slot in newSlots)
			{
				bool known = oldSlots.TryGetValue(slot.Key, out var before);
				if (KingdomPlotRules.NewBlockingUpgradeCell(
					Before.Rect.Contains(slot.Key % Z.Width, slot.Key / Z.Width), known, before, slot.Value))
					Cells.Add(slot.Key, slot.Value);
			}
			return true;
		}

		internal static bool TryProveRenovationOccupants(KingdomSystem System, Zone Z,
			KingdomArchitectureIntent Before, KingdomArchitectureIntent After, bool TolerateMovable,
			out Dictionary<int, ArchitecturePassability> NewlyBlocked, out string Failure)
		{
			if (!TryNewBlockingCells(Z, Before, After, out NewlyBlocked, out Failure)) return false;
			foreach (var slot in NewlyBlocked)
			{
				int x = slot.Key % Z.Width, y = slot.Key / Z.Width;
				if (!Before.Rect.Contains(x, y)) continue;
				Cell cell = Z.GetCell(x, y);
				if (cell == null) return Fail("renovation ground is outside the zone", out Failure);
				foreach (var item in cell.GetObjects())
				{
					if (!GameObject.Validate(item) || !item.IsCreature && !item.IsPlayer()) continue;
					if (TolerateMovable && KingdomPlots.IsMovableEnvelopeOccupant(System, Z, item)) continue;
					KingdomPlots.NameEnvelopeOccupant(System, Z, item, slot.Value);
					return Fail("a living occupant stands on renovation ground at " + Coordinate(x, y), out Failure);
				}
			}
			return true;
		}
	}
}
