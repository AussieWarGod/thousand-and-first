using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static bool TryProjectionEvidence(GameObject Root, Zone Z,
			KingdomGatehousePlan Plan, IPart Part, int Index,
			string ExpectedId, out KingdomGatehouseSlotEvidence Evidence,
			out GameObject Item, out string Failure)
		{
			Evidence = KingdomGatehouseSlotEvidence.Foreign;
			Item = null; Failure = null;
			r_KingdomGatehouseProjectionV2 v2 = GameObject.Validate(Root)
				? Root.GetPart<r_KingdomGatehouseProjectionV2>() : null;
			r_KingdomGatehouseProjectionV1Pending v1 = GameObject.Validate(Root)
				? Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() : null;
			bool exactPart = Plan != null && (Plan.ReceiptVersion == 2
				? v1 == null && v2 != null && ReferenceEquals(Part, v2)
				: Plan.ReceiptVersion == 1 && v2 == null
					&& (v1 == null ? Part == null : ReferenceEquals(Part, v1)));
			if (!GameObject.Validate(Root) || Z == null || !exactPart
				|| string.IsNullOrEmpty(ExpectedId)
				|| !KingdomGatehouseRules.TrySatellite(Plan, Index,
					out KingdomGatehouseCell spec)) return false;

			List<GameObject> relevant = new List<GameObject>();
			List<GameObject> pending = LoadedZoneRoots(Z);
			if (pending == null)
			{
				Failure = "The loaded gatehouse zone graph cannot be enumerated exactly.";
				return false;
			}
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			bool duplicate = false; int visited = 0;
			while (pending.Count > 0)
			{
				GameObject current = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (!GameObject.Validate(current)) continue;
				bool first = expanded.Add(current);
				bool matches = ProjectionRelevant(current, Root.IDIfAssigned,
					Index, spec.Slot, ExpectedId);
				if (matches)
				{
					if (!first) duplicate = true;
					else relevant.Add(current);
				}
				if (!first) continue;
				if (++visited > MaxProjectionScanObjects)
				{
					Failure = "The loaded gatehouse custody graph exceeds its bounded proof.";
					return false;
				}
				List<GameObject> children = current.GetInventoryDirectAndEquipment();
				if (children != null) for (int i = 0; i < children.Count; i++)
					pending.Add(children[i]);
			}
			GameObject custody = ProjectionCustody(Part, Index);
			if (GameObject.Validate(custody))
			{
				if (!ProjectionRelevant(custody, Root.IDIfAssigned, Index,
					spec.Slot, ExpectedId))
				{
					Evidence = KingdomGatehouseSlotEvidence.Foreign;
					Item = custody;
					return true;
				}
				if (!expanded.Contains(custody))
				{
					expanded.Add(custody); relevant.Add(custody);
				}
				for (int i = 0; Part != null
					&& i < KingdomGatehouseRules.SatelliteCount; i++)
					if (i != Index && ReferenceEquals(
						ProjectionCustody(Part, i), custody))
						duplicate = true;
			}

			if (duplicate || relevant.Count > 1)
			{
				Evidence = KingdomGatehouseSlotEvidence.Duplicate;
				return true;
			}
			if (relevant.Count == 0)
			{
				Evidence = KingdomGatehouseSlotEvidence.Absent;
				return true;
			}
			Item = relevant[0];
			if (Root.GetStringProperty(SatelliteIdProperty(Index)) != ExpectedId
				|| !ExactProjectionMarks(Item, Root, Plan, Index, spec, ExpectedId))
			{
				Evidence = KingdomGatehouseSlotEvidence.Foreign;
				return true;
			}
			if (Item.CurrentCell == null && Item.InInventory == null && Item.Equipped == null)
			{
				Evidence = ReferenceEquals(Item, custody)
					? KingdomGatehouseSlotEvidence.Staged
					: KingdomGatehouseSlotEvidence.Foreign;
				return true;
			}
			Evidence = Item.CurrentZone == Z && Item.CurrentCell == Z.GetCell(spec.X, spec.Y)
				&& ReferenceCount(Item.CurrentCell.GetObjects(), Item) == 1
				&& (custody == null || ReferenceEquals(custody, Item))
					? KingdomGatehouseSlotEvidence.ExactPlacement
					: KingdomGatehouseSlotEvidence.Foreign;
			return true;
		}

		private static bool ProjectionRelevant(GameObject Item, string RootId, int Index,
			string Slot, string ExpectedId)
		{
			return GameObject.Validate(Item) && (Item.IDIfAssigned == ExpectedId
				|| (Item.GetIntProperty(SatelliteProperty) == 1
					&& Item.GetStringProperty(OwnerProperty) == RootId
					&& (Item.GetIntProperty(IndexProperty) == Index
						|| Item.GetStringProperty(SlotProperty) == Slot)));
		}

		private static bool ExactProjectionMarks(GameObject Item, GameObject Root,
			KingdomGatehousePlan Plan, int Index, KingdomGatehouseCell Spec, string ExpectedId)
		{
			if (!GameObject.Validate(Item) || Item.IDIfAssigned != ExpectedId
				|| Item.Blueprint != Spec.Blueprint
				|| !ExactSatellitePalette(Item, Plan, Index)
				|| Item.HasStringProperty(SatelliteProperty)
				|| Item.GetIntProperty(SatelliteProperty) != 1
				|| Item.HasIntProperty(OwnerProperty)
				|| Item.GetStringProperty(OwnerProperty) != Root.IDIfAssigned
				|| Item.HasStringProperty(IndexProperty)
				|| Item.GetIntProperty(IndexProperty) != Index
				|| Item.HasIntProperty(SlotProperty)
				|| Item.GetStringProperty(SlotProperty) != Spec.Slot
				|| Item.HasStringProperty(KingdomPlots.PlotPartProperty)
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty) != 0) return false;
			if (Index == 0)
				return !Item.HasStringProperty(ReservationProperty)
					&& Item.GetIntProperty(ReservationProperty) == Schema
					&& ExactPlotRectMarks(Item, Plan);
			return !Item.HasIntProperty(ReservationProperty)
				&& !Item.HasStringProperty(ReservationProperty)
				&& !HasPlotRectMark(Item);
		}

		private static bool HasPlotRectMark(GameObject Item)
		{
			return Item.HasIntProperty(KingdomPlots.PlotX1Property)
				|| Item.HasIntProperty(KingdomPlots.PlotY1Property)
				|| Item.HasIntProperty(KingdomPlots.PlotX2Property)
				|| Item.HasIntProperty(KingdomPlots.PlotY2Property)
				|| Item.HasStringProperty(KingdomPlots.PlotX1Property)
				|| Item.HasStringProperty(KingdomPlots.PlotY1Property)
				|| Item.HasStringProperty(KingdomPlots.PlotX2Property)
				|| Item.HasStringProperty(KingdomPlots.PlotY2Property);
		}

		private static int ReferenceCount(IList<GameObject> Values, GameObject Item)
		{
			int count = 0;
			if (Values != null) for (int i = 0; i < Values.Count; i++)
				if (ReferenceEquals(Values[i], Item)) count++;
			return count;
		}

		internal static bool LoadedIdentityAbsent(Zone Z, string Id)
		{
			return CountLoadedIdentity(Z, Id, out _) == 0;
		}

		internal static bool TryStrikeReceipt(Zone Z, KingdomStrikeIntent Intent,
			out GameObject Root)
		{
			Root = null;
			if (Z == null || Intent == null || Intent.Targets == null
				|| !KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey, Intent.HasPlot,
					Intent.X1, Intent.Y1, Intent.X2, Intent.Y2,
					Intent.PlotId, Intent.Targets.Count)
				|| CountLoadedIdentity(Z, Intent.PlotId, out Root) != 1
				|| !GameObject.Validate(Root) || Root.CurrentZone != Z
				|| !TryReadPlan(Root, out KingdomGatehousePlan plan, out _)
				|| plan.X1 != Intent.X1 || plan.Y1 != Intent.Y1
				|| plan.X2 != Intent.X2 || plan.Y2 != Intent.Y2
				|| !TryExactSatelliteReceipts(Root, plan, out string encoded)
				|| !ProjectionStateReceiptExact(Root, plan,
					Root.GetPart<r_KingdomGatehouseProjectionV2>())) return false;
			HashSet<string> expectedIds = new HashSet<string>();
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				KingdomStrikeTarget target = Intent.Targets[i];
				string storedId = Root.GetStringProperty(SatelliteIdProperty(i));
				if (target == null || !KingdomGatehouseRules.TrySatellite(plan, i,
					out KingdomGatehouseCell spec)
					|| target.Id != storedId
					|| !KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
						plan.ReceiptVersion == 2, Intent.PlotId, encoded, i, target.Id)
					|| target.Blueprint != spec.Blueprint
					|| target.X != spec.X || target.Y != spec.Y
					|| !expectedIds.Add(target.Id)) return false;
			}
			return NoExtraOwnedSatellites(Z, Intent.PlotId, expectedIds);
		}

		internal static bool TryResolveStrikeSatellite(Zone Z, string OwnerId, int Index,
			string Id, string Blueprint, int X, int Y, out GameObject Item)
		{
			Item = null;
			r_KingdomGatehouseProjectionV2 part;
			if (CountLoadedIdentity(Z, OwnerId, out GameObject root) != 1
				|| !GameObject.Validate(root) || root.CurrentZone != Z
				|| !TryReadPlan(root, out KingdomGatehousePlan plan, out _)
				|| !TryExactSatelliteReceipts(root, plan, out string encoded)
				|| root.GetStringProperty(SatelliteIdProperty(Index)) != Id
				|| !KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
					plan.ReceiptVersion == 2, OwnerId, encoded, Index, Id)
				|| !KingdomGatehouseRules.TrySatellite(plan, Index,
					out KingdomGatehouseCell spec)
				|| spec.Blueprint != Blueprint || spec.X != X || spec.Y != Y
				|| !ProjectionStateReceiptExact(root, plan,
					part = root.GetPart<r_KingdomGatehouseProjectionV2>())
				|| !TryProjectionEvidence(root, Z, plan, part, Index, Id,
					out KingdomGatehouseSlotEvidence evidence, out Item, out _)
				|| evidence != KingdomGatehouseSlotEvidence.ExactPlacement) return false;
			return ExactProjectionMarks(Item, root, plan, Index, spec, Id);
		}

		private static int CountLoadedIdentity(Zone Z, string Id, out GameObject Exact)
		{
			Exact = null;
			if (Z == null || string.IsNullOrEmpty(Id)) return -1;
			List<GameObject> pending = LoadedZoneRoots(Z);
			if (pending == null) return -1;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			int count = 0;
			while (pending.Count > 0)
			{
				GameObject current = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (current == null) continue;
				if (!expanded.Add(current))
				{
					if (GameObject.Validate(current) && current.IDIfAssigned == Id) return -1;
					continue;
				}
				if (expanded.Count > MaxProjectionScanObjects) return -1;
				if (!GameObject.Validate(current)) continue;
				if (current.IDIfAssigned == Id)
				{
					count++;
					if (count == 1) Exact = current;
				}
				List<GameObject> children = current.GetInventoryDirectAndEquipment();
				if (children != null) for (int i = 0; i < children.Count; i++)
					pending.Add(children[i]);
			}
			if (count != 1) Exact = null;
			return count;
		}

		private static List<GameObject> LoadedZoneRoots(Zone Primary)
		{
			if (Primary == null) return null;
			List<GameObject> roots = new List<GameObject>(Primary.GetObjects());
			if (The.Game?.ZoneManager?.CachedZones == null) return roots;
			HashSet<Zone> scanned = new HashSet<Zone> { Primary };
			foreach (Zone loaded in The.Game.ZoneManager.CachedZones.Values)
			{
				if (loaded == null || !scanned.Add(loaded)) continue;
				roots.AddRange(loaded.GetObjects());
				if (roots.Count > MaxProjectionScanObjects) return null;
			}
			return roots;
		}
	}
}
