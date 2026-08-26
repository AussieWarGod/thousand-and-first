using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	/// <summary>Live projection and exact ownership for the typed gatehouse network.</summary>
	public static partial class KingdomGatehouse
	{
		public const int Schema = 1;
		public const string SchemaProperty = "KingdomGatehouseSchema";
		public const string PlanProperty = "KingdomGatehousePlan";
		public const string ReservationProperty = "KingdomGatehouseReservation";
		public const string SatelliteProperty = "KingdomGatehouseSatellite";
		public const string OwnerProperty = "KingdomGatehouseOwner";
		public const string IndexProperty = "KingdomGatehouseIndex";
		public const string SlotProperty = "KingdomGatehouseSlot";
		private const string SatelliteIdPrefix = "KingdomGatehouseSatelliteId";

		public static string SatelliteIdProperty(int Index)
		{
			return SatelliteIdPrefix + Index;
		}

		/// <summary>Resolve road/frontier grammar and audit every owned/path cell before debit.</summary>
		public static bool TryPlan(Zone Z, KingdomSystem System, out KingdomGatehousePlan Plan,
			out string Failure)
		{
			Plan = null;
			Failure = null;
			if (Z == null || System == null)
			{
				Failure = "The gatehouse needs claimed ground to measure its road and frontier.";
				return false;
			}
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID,
				System.ClaimedZones);
			if (edges == KingdomRules.Frontier.None)
			{
				Failure = "This ground has no frontier edge for a gatehouse to cross.";
				return false;
			}
			bool hasRite = KingdomPlots.TryRiteGround(Z, out int riteX, out int riteY);
			if (!KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY,
				out int heartX, out int heartY))
			{
				Failure = "The settlement has no heart from which to measure a road to the frontier.";
				return false;
			}
			if (!KingdomGatehouseRules.TryPlan(Z.Width, Z.Height, edges, heartX, heartY,
				out Plan))
			{
				Failure = "The road reaches the frontier too near the zone edge for a gatehouse and its approaches.";
				return false;
			}
			return TryAudit(Z, Plan, null, null, out Failure);
		}

		/// <summary>Reserve the entire frozen footprint while the paid scaffold is standing.</summary>
		public static bool TryStageScaffold(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			if (!GameObject.Validate(Scaffold)
				|| !KingdomGatehouseRules.TryEncode(Plan, out string receipt)) return false;
			Scaffold.SetStringProperty(PlanProperty, receipt);
			Scaffold.SetIntProperty(ReservationProperty, Schema);
			KingdomPlots.StampRect(Scaffold, new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2));
			return Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& KingdomPlots.TryReadRect(Scaffold, out KingdomPlotRules.PlotRect observed)
				&& SameRect(observed, Plan);
		}

		public static bool ScaffoldMatches(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			return GameObject.Validate(Scaffold)
				&& Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& KingdomGatehouseRules.TryEncode(Plan, out string receipt)
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& KingdomPlots.TryReadRect(Scaffold, out KingdomPlotRules.PlotRect observed)
				&& SameRect(observed, Plan);
		}

		/// <summary>Re-audit immediately before projection; allows only its exact root/scaffold.</summary>
		public static bool TryAudit(Zone Z, KingdomGatehousePlan Plan, GameObject Root,
			GameObject Scaffold, out string Failure)
		{
			Failure = null;
			if (Z == null || Plan == null || !KingdomGatehouseRules.TryEncode(Plan, out _))
			{
				Failure = "The frozen gatehouse footprint cannot be read.";
				return false;
			}
			KingdomPlotRules.PlotRect proposed = new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.PlotRoots)
			{
				if (!GameObject.Validate(item) || ReferenceEquals(item, Root)
					|| ReferenceEquals(item, Scaffold)) continue;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect laid)
					&& KingdomPlotRules.Overlaps(proposed, laid))
				{
					Failure = "The frozen gatehouse footprint overlaps another reserved work at "
						+ item.CurrentCell.X + "," + item.CurrentCell.Y + ".";
					return false;
				}
			}
			for (int y = Plan.Y1; y <= Plan.Y2; y++)
			{
				for (int x = Plan.X1; x <= Plan.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (!AuditFootprintCell(cell, Root, Scaffold, out string blocker))
					{
						Failure = "The gatehouse footprint is blocked at " + x + "," + y
							+ (string.IsNullOrEmpty(blocker) ? "." : (" by " + blocker + "."));
						return false;
					}
				}
			}
			for (int i = 0; i < 2; i++)
			{
				if (!KingdomGatehouseRules.TryApproach(Plan, i, out KingdomGatehouseCell approach))
					return false;
				Cell cell = Z.GetCell(approach.X, approach.Y);
				if (cell == null || !cell.IsPassable() || cell.HasObjectWithPart("LiquidVolume"))
				{
					Failure = "The " + approach.Slot + " is not passable at "
						+ approach.X + "," + approach.Y + ".";
					return false;
				}
			}
			return true;
		}

		/// <summary>Read the final root's typed footprint without treating it as a plot design.</summary>
		public static bool TryReadPlan(GameObject Root, out KingdomGatehousePlan Plan,
			out string Failure)
		{
			Plan = null;
			Failure = null;
			if (!GameObject.Validate(Root) || Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != Schema
				|| !KingdomGatehouseRules.IsGatehouse(
					Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)))
			{
				Failure = "The gatehouse typed-network marker is absent or malformed.";
				return false;
			}
			if (!KingdomGatehouseRules.TryDecode(Root.GetStringProperty(PlanProperty), out Plan)
				|| Root.CurrentCell == null || Root.CurrentCell.X != Plan.GateX
				|| Root.CurrentCell.Y != Plan.GateY)
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
			GameObject root = GameObject.FindByID(OwnerId);
			if (GameObject.Validate(root) && root.GetIntProperty(SchemaProperty) == Schema)
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
