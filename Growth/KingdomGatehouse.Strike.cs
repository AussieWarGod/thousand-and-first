using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		/// <summary>Read the final root's typed footprint without treating it as a plot design.</summary>
		public static bool TryReadPlan(GameObject Root, out KingdomGatehousePlan Plan,
			out string Failure)
		{
			Plan = null;
			Failure = null;
			if (!GameObject.Validate(Root)
				|| Root.Blueprint != KingdomGatehouseRules.RootBlueprint
				|| Root.GetPart<r_KingdomGatehouse>() == null || Root.GetPart<Door>() == null
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != Schema
				|| Root.HasIntProperty(PlanProperty)
				|| Root.HasIntProperty(KingdomUpgrade.BuildKeyProperty)
				|| !KingdomGatehouseRules.IsGatehouse(
					Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)))
			{
				Failure = "The gatehouse typed-network marker is absent or malformed.";
				return false;
			}
			if (!KingdomGatehouseRules.TryDecode(Root.GetStringProperty(PlanProperty), out Plan)
				|| (Plan.ReceiptVersion == 2
					? Root.GetPart<r_KingdomGatehouseProjectionV2>() == null
						|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null
					: Root.GetPart<r_KingdomGatehouseProjectionV2>() != null
						|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null)
				|| Root.CurrentCell == null || Root.CurrentCell.X != Plan.GateX
				|| Root.CurrentCell.Y != Plan.GateY || !ExactRootPalette(Root, Plan))
			{
				Failure = "The gatehouse's frozen road footprint cannot be read exactly.";
				Plan = null;
				return false;
			}
			return true;
		}

		/// <summary>Freeze the six exact owned satellite IDs for the non-plot strike receipt.</summary>
		public static bool TryFreezeStrikeTargets(GameObject Root, Zone Z,
			out KingdomGatehousePlan Plan, out List<KingdomStrikeTarget> Targets,
			out string Failure)
		{
			Targets = null;
			if (!TryReadPlan(Root, out Plan, out Failure) || Root.CurrentZone != Z)
				return false;
			if (!TryExactSatellites(Root, Z, Plan, out List<GameObject> satellites, out Failure))
				return false;
			if (!ProjectionStateReceiptExact(Root, Plan,
				Root.GetPart<XRL.World.Parts.r_KingdomGatehouseProjectionV2>()))
			{
				Failure = "The gatehouse's completed six-slot state receipt is malformed.";
				return false;
			}
			Targets = new List<KingdomStrikeTarget>(KingdomGatehouseRules.SatelliteCount);
			for (int i = 0; i < satellites.Count; i++)
			{
				GameObject item = satellites[i];
				Targets.Add(new KingdomStrikeTarget
				{
					Id = item.ID,
					Blueprint = item.Blueprint,
					X = item.CurrentCell.X,
					Y = item.CurrentCell.Y
				});
			}
			return true;
		}

		public static bool IsOwnedSatellite(GameObject Item, string OwnerId, string Blueprint,
			int X, int Y, Zone Z)
		{
			if (!GameObject.Validate(Item) || Z == null || string.IsNullOrEmpty(OwnerId)
				|| Item.CurrentZone != Z || Item.CurrentCell != Z.GetCell(X, Y)
				|| Item.Blueprint != Blueprint || Item.GetIntProperty(SatelliteProperty) != 1
				|| Item.GetStringProperty(OwnerProperty) != OwnerId
				|| Item.GetIntProperty(IndexProperty) < 0
				|| Item.GetIntProperty(IndexProperty) >= KingdomGatehouseRules.SatelliteCount
				|| string.IsNullOrEmpty(Item.GetStringProperty(SlotProperty))
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty) != 0) return false;
			// Once the root's schema-last receipt exists, index and slot are immutable physical
			// facts too. During the live projection callback the root intentionally has no schema
			// yet, so the raw checks above are the only facts available until final verification.
			if (CountLoadedIdentity(Z, OwnerId, out GameObject root) != 1
				|| !GameObject.Validate(root) || root.CurrentZone != Z) return false;
			if (root.GetIntProperty(SchemaProperty) == Schema)
			{
				int index = Item.GetIntProperty(IndexProperty);
				if (!TryReadPlan(root, out KingdomGatehousePlan plan, out _)
					|| !KingdomGatehouseRules.TrySatellite(plan, index,
						out KingdomGatehouseCell expected)
					|| expected.X != X || expected.Y != Y || expected.Blueprint != Blueprint
					|| expected.Slot != Item.GetStringProperty(SlotProperty)) return false;
			}
			return true;
		}

		public static bool IsOwnedSatellite(GameObject Item, string OwnerId)
		{
			return GameObject.Validate(Item) && Item.GetIntProperty(SatelliteProperty) == 1
				&& Item.GetStringProperty(OwnerProperty) == OwnerId;
		}

	}
}
