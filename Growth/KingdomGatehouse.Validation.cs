using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private static bool TryExactSatellites(GameObject Root, Zone Z,
			KingdomGatehousePlan Plan, out List<GameObject> Satellites, out string Failure)
		{
			Satellites = new List<GameObject>(KingdomGatehouseRules.SatelliteCount);
			Failure = null;
			IPart part = null;
			if (GameObject.Validate(Root) && Plan != null)
				part = Plan.ReceiptVersion == 2
					? (IPart)Root.GetPart<XRL.World.Parts.r_KingdomGatehouseProjectionV2>()
					: Root.GetPart<XRL.World.Parts.r_KingdomGatehouseProjectionV1Pending>();
			if (Z == null || Plan == null || !GameObject.Validate(Root)
				|| Root.GetPart<XRL.World.Parts.r_KingdomGatehouse>() == null
				|| (Plan.ReceiptVersion == 2
					? !ProjectionPartMatches(Root, Plan, part, false)
					: Plan.ReceiptVersion != 1
						|| Root.GetPart<XRL.World.Parts.r_KingdomGatehouseProjectionV2>() != null)
				|| !TryExactSatelliteReceipts(Root, Plan, out string encoded))
			{
				Failure = "The gatehouse's frozen root receipt is absent or malformed.";
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				string id = Root.GetStringProperty(SatelliteIdProperty(i));
				if (!ids.Add(id)
					|| !KingdomGatehouseRules.TrySatellite(Plan, i, out KingdomGatehouseCell spec))
				{
					Failure = "The gatehouse's exact satellite receipt is absent or changed.";
					return false;
				}
				if (!TryProjectionEvidence(Root, Z, Plan, part, i, id,
					out KingdomGatehouseSlotEvidence evidence, out GameObject item,
					out Failure) || evidence != KingdomGatehouseSlotEvidence.ExactPlacement
					|| !ExactProjectionMarks(item, Root, Plan, i, spec, id)
					|| ProjectionCustody(part, i) != null)
				{
					Failure = Failure
						?? "A gatehouse satellite was removed, moved, duplicated, replaced, or changed.";
					return false;
				}
				Satellites.Add(item);
			}
			if (!NoExtraOwnedSatellites(Z, Root.IDIfAssigned, ids))
			{
				Failure = "A new or replacement satellite entered the gatehouse receipt.";
				return false;
			}
			return true;
		}

		private static bool NoExtraOwnedSatellites(Zone Z, string RootId,
			HashSet<string> ExpectedIds)
		{
			List<GameObject> pending = LoadedZoneRoots(Z);
			if (pending == null || string.IsNullOrEmpty(RootId) || ExpectedIds == null)
				return false;
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			while (pending.Count > 0)
			{
				GameObject item = pending[pending.Count - 1];
				pending.RemoveAt(pending.Count - 1);
				if (!GameObject.Validate(item)) continue;
				bool owned = IsOwnedSatellite(item, RootId);
				if (!expanded.Add(item))
				{
					if (owned) return false;
					continue;
				}
				if (expanded.Count > MaxProjectionScanObjects
					|| (owned && !ExpectedIds.Contains(item.IDIfAssigned))) return false;
				List<GameObject> children = item.GetInventoryDirectAndEquipment();
				if (children != null) for (int i = 0; i < children.Count; i++)
					pending.Add(children[i]);
			}
			return true;
		}

		private static bool TryExactSatelliteReceipts(GameObject Root,
			KingdomGatehousePlan Plan, out string Encoded)
		{
			Encoded = null;
			if (!GameObject.Validate(Root) || string.IsNullOrEmpty(Root.IDIfAssigned)
				|| Root.HasIntProperty(PlanProperty)
				|| !KingdomGatehouseRules.TryEncode(Plan, out Encoded)
				|| Root.GetStringProperty(PlanProperty) != Encoded) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				string key = SatelliteIdProperty(i);
				string id = Root.GetStringProperty(key);
				if (Root.HasIntProperty(key)
					|| !KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
						Plan.ReceiptVersion == 2, Root.IDIfAssigned, Encoded, i, id)
					|| !ids.Add(id)) return false;
			}
			return true;
		}

		private static GameObject FindExactScaffold(Cell Cell, KingdomConstructionJob Job)
		{
			if (Cell == null || Job == null || string.IsNullOrEmpty(Job.SubjectId)) return null;
			GameObject found = null;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (GameObject.Validate(item) && item.IDIfAssigned == Job.SubjectId
					&& item.HasPart("r_KingdomScaffold")
					&& !item.HasIntProperty(KingdomConstruction.ReceiptProperty)
					&& KingdomConstruction.HasReceipt(item, Job))
				{
					if (found != null) return null;
					found = item;
				}
			}
			return found;
		}

		private static bool AuditFootprintCell(Cell Cell, GameObject Root, GameObject Scaffold,
			KingdomGatehousePlan Plan, Zone Z, out string Blocker)
		{
			Blocker = null;
			if (Cell == null)
			{
				Blocker = "the edge of the zone";
				return false;
			}
			bool hasExpected = false;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (ReferenceEquals(item, Root) || ReferenceEquals(item, Scaffold)
					|| RecognizedProjectionSatellite(item, Root, Plan, Z))
				{
					hasExpected = true;
					continue;
				}
				if (!GameObject.Validate(item)) continue;
				if (item.IsPlayer() || item.IsCreature)
				{
					Blocker = item.IsPlayer() ? "the founder" : item.ShortDisplayNameStripped;
					return false;
				}
				if (KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
				{
					Blocker = item.ShortDisplayNameStripped ?? item.Blueprint;
					return false;
				}
			}
			if (!hasExpected && (!Cell.IsPassable() || Cell.HasObjectWithPart("LiquidVolume")))
			{
				Blocker = "impassable ground";
				return false;
			}
			return true;
		}

		private static bool RecognizedProjectionSatellite(GameObject Item, GameObject Root,
			KingdomGatehousePlan Plan, Zone Z)
		{
			if (!GameObject.Validate(Item) || !GameObject.Validate(Root) || Z == null
				|| Item.CurrentZone != Z) return false;
			int index = Item.GetIntProperty(IndexProperty, -1);
			int state = Root.GetIntProperty(SatelliteStateProperty(index));
			if ((state != (int)KingdomGatehouseSlotState.Pending
				&& state != (int)KingdomGatehouseSlotState.Settled)
				|| !KingdomGatehouseRules.TrySatellite(Plan, index,
					out KingdomGatehouseCell spec)
				|| !KingdomGatehouseRules.TryEncode(Plan, out string encoded)) return false;
			string expectedId = Root.GetStringProperty(SatelliteIdProperty(index));
			return KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
					Plan.ReceiptVersion == 2, Root.IDIfAssigned, encoded, index, expectedId)
				&& ExactProjectionMarks(Item, Root, Plan, index, spec, expectedId)
				&& Item.CurrentCell == Z.GetCell(spec.X, spec.Y);
		}

		private static bool SameRect(KingdomPlotRules.PlotRect Rect, KingdomGatehousePlan Plan)
		{
			return Plan != null && Rect.X1 == Plan.X1 && Rect.Y1 == Plan.Y1
				&& Rect.X2 == Plan.X2 && Rect.Y2 == Plan.Y2;
		}

		private static bool ExactPlotRectMarks(GameObject Item, KingdomGatehousePlan Plan)
		{
			return GameObject.Validate(Item)
				&& Item.HasIntProperty(KingdomPlots.PlotX1Property)
				&& Item.HasIntProperty(KingdomPlots.PlotY1Property)
				&& Item.HasIntProperty(KingdomPlots.PlotX2Property)
				&& Item.HasIntProperty(KingdomPlots.PlotY2Property)
				&& !Item.HasStringProperty(KingdomPlots.PlotX1Property)
				&& !Item.HasStringProperty(KingdomPlots.PlotY1Property)
				&& !Item.HasStringProperty(KingdomPlots.PlotX2Property)
				&& !Item.HasStringProperty(KingdomPlots.PlotY2Property)
				&& KingdomPlots.TryReadRect(Item, out KingdomPlotRules.PlotRect observed)
				&& SameRect(observed, Plan);
		}
	}
}
