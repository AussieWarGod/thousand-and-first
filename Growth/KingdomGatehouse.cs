using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;
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
		public const string ProjectionFaultProperty = "KingdomGatehouseProjectionFault";
		public const string RootOpenRenderString = "/";
		private const string SatelliteIdPrefix = "KingdomGatehouseSatelliteId";
		private const string SatelliteStatePrefix = "KingdomGatehouseSatelliteState";

		public static string SatelliteIdProperty(int Index)
		{
			return SatelliteIdPrefix + Index;
		}

		public static string SatelliteStateProperty(int Index)
		{
			return SatelliteStatePrefix + Index;
		}

		/// <summary>A paid typed root survives every callback cut before its six outputs settle.</summary>
		public static bool HasProjectionCustody(GameObject Root)
		{
			if (!GameObject.Validate(Root)
				|| Root.CurrentCell == null || Root.CurrentZone == null
				|| !KingdomGatehouseRules.IsGatehouse(
					Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty))
				|| Root.GetPart<XRL.World.Parts.r_KingdomGatehouse>() == null)
				return false;
			r_KingdomGatehouseProjectionV2 v2 =
				Root.GetPart<r_KingdomGatehouseProjectionV2>();
			r_KingdomGatehouseProjectionV1Pending v1 =
				Root.GetPart<r_KingdomGatehouseProjectionV1Pending>();
			if (v2 == null && v1 == null)
				return ProjectionComplete(Root, Root.CurrentZone)
					|| SettledV1PendingRootEnvelope(Root);
			if (v2 != null && v1 != null) return false;
			if (!string.IsNullOrEmpty(Root.GetStringProperty(KingdomConstruction.ReceiptProperty))
				|| Root.GetIntProperty(SchemaProperty) == Schema
				|| !string.IsNullOrEmpty(Root.GetStringProperty(PlanProperty))) return true;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				if (!string.IsNullOrEmpty(Root.GetStringProperty(SatelliteIdProperty(i)))
					|| Root.GetIntProperty(SatelliteStateProperty(i)) != 0) return true;
			return false;
		}

		private static bool SettledV1PendingRootEnvelope(GameObject Root)
		{
			if (!GameObject.Validate(Root) || Root.CurrentCell == null
				|| Root.Blueprint != KingdomGatehouseRules.RootBlueprint
				|| Root.GetPart<r_KingdomGatehouse>() == null
				|| Root.GetPart<Door>() == null
				|| Root.GetPart<r_KingdomGatehouseProjectionV2>() != null
				|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null
				|| Root.HasIntProperty(SchemaProperty)
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.HasIntProperty(PlanProperty)
				|| Root.HasIntProperty(KingdomConstruction.ReceiptProperty)
				|| string.IsNullOrEmpty(
					Root.GetStringProperty(KingdomConstruction.ReceiptProperty))
				|| Root.HasIntProperty(KingdomUpgrade.BuildKeyProperty)
				|| Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
					!= KingdomGatehouseRules.BuildKey
				|| !KingdomGatehouseRules.TryDecode(Root.GetStringProperty(PlanProperty),
					out KingdomGatehousePlan plan)
				|| plan.ReceiptVersion != 1
				|| !KingdomGatehouseRules.TryEncode(plan, out string encoded)
				|| Root.GetStringProperty(PlanProperty) != encoded
				|| Root.CurrentCell.X != plan.GateX || Root.CurrentCell.Y != plan.GateY
				|| !TryProjectionStateCounts(Root, out int stateFields,
					out int settledStates)
				|| !TryExactSatelliteReceipts(Root, plan, out _))
				return false;
			return KingdomGatehouseProjectionRules.MustRetainLegacyOwnerAcrossSchemaCut(
				false, false, false, false, stateFields, settledStates,
				CanonicalPlan: true, SixUniqueStoredIds: true);
		}

		private static bool TryProjectionStateCounts(GameObject Root,
			out int StateFields, out int SettledStates)
		{
			StateFields = 0;
			SettledStates = 0;
			if (!GameObject.Validate(Root)) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				string key = SatelliteStateProperty(i);
				if (Root.HasStringProperty(key)) return false;
				if (!Root.HasIntProperty(key)) continue;
				StateFields++;
				int state = Root.GetIntProperty(key);
				if (state < (int)KingdomGatehouseSlotState.Empty
					|| state > (int)KingdomGatehouseSlotState.Contested) return false;
				if (state == (int)KingdomGatehouseSlotState.Settled) SettledStates++;
			}
			return true;
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
				System.Style, out Plan))
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
			if (!TryStageRootPalette(Scaffold, Plan)) return false;
			Scaffold.SetStringProperty(PlanProperty, receipt);
			Scaffold.SetIntProperty(ReservationProperty, Schema);
			KingdomPlots.StampRect(Scaffold, new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2));
			return Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& !Scaffold.HasStringProperty(ReservationProperty)
				&& !Scaffold.HasIntProperty(PlanProperty)
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& ExactStagedRootPalette(Scaffold, Plan)
				&& ExactPlotRectMarks(Scaffold, Plan);
		}

		public static bool ScaffoldMatches(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			return GameObject.Validate(Scaffold)
				&& !Scaffold.HasStringProperty(ReservationProperty)
				&& Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& !Scaffold.HasIntProperty(PlanProperty)
				&& KingdomGatehouseRules.TryEncode(Plan, out string receipt)
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& ExactStagedRootPalette(Scaffold, Plan)
				&& ExactPlotRectMarks(Scaffold, Plan);
		}

		private static bool TryStageRootPalette(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			if (Plan == null || Plan.ReceiptVersion != 2) return true;
			if (!KingdomGatehouseRules.TryRootRender(Plan, out string render,
				out string color, out _, out string detail, out string closedTile, out _)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedColorStringProperty)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedDetailColorProperty)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedRenderStringProperty)
				|| Scaffold.HasIntProperty(KingdomDesign.StagedTileProperty)) return false;
			Scaffold.SetStringProperty(KingdomDesign.StagedColorStringProperty, color);
			Scaffold.SetStringProperty(KingdomDesign.StagedDetailColorProperty, detail);
			Scaffold.SetStringProperty(KingdomDesign.StagedRenderStringProperty, render);
			Scaffold.SetStringProperty(KingdomDesign.StagedTileProperty, closedTile);
			return ExactStagedRootPalette(Scaffold, Plan);
		}

		private static bool ExactStagedRootPalette(GameObject Scaffold,
			KingdomGatehousePlan Plan)
		{
			if (!GameObject.Validate(Scaffold) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			return KingdomGatehouseRules.TryRootRender(Plan, out string render,
					out string color, out _, out string detail, out string closedTile, out _)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedColorStringProperty)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedDetailColorProperty)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedRenderStringProperty)
				&& !Scaffold.HasIntProperty(KingdomDesign.StagedTileProperty)
				&& Scaffold.GetStringProperty(KingdomDesign.StagedColorStringProperty) == color
				&& Scaffold.GetStringProperty(KingdomDesign.StagedDetailColorProperty) == detail
				&& Scaffold.GetStringProperty(KingdomDesign.StagedRenderStringProperty) == render
				&& Scaffold.GetStringProperty(KingdomDesign.StagedTileProperty) == closedTile;
		}

		private static bool ExactRootPalette(GameObject Root, KingdomGatehousePlan Plan)
		{
			if (!GameObject.Validate(Root) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			Render render = Root.GetPart<Render>();
			Door door = Root.GetPart<Door>();
			return render != null && door != null
				&& KingdomGatehouseRules.TryRootRender(Plan, out string glyph,
					out string color, out string tileColor, out string detail,
					out string closedTile, out string openTile)
				&& render.ColorString == color
				&& render.TileColor == tileColor && render.DetailColor == detail
				&& KingdomGatehouseProjectionRules.ExactLiveDoorRender(door.Open,
					door.SyncRender, render.RenderString, render.Tile,
					door.ClosedDisplay, door.OpenDisplay, door.ClosedTile, door.OpenTile,
					glyph, RootOpenRenderString, closedTile, openTile);
		}

		/// <summary>Apply paid v2 root truth before EnteredCell can begin output projection.</summary>
		internal static bool TryApplyRootForm(GameObject Root, string PlanReceipt)
		{
			if (!GameObject.Validate(Root) || Root.Blueprint != KingdomGatehouseRules.RootBlueprint
				|| Root.GetPart<r_KingdomGatehouse>() == null
				|| !KingdomGatehouseRules.TryDecode(PlanReceipt,
					out KingdomGatehousePlan plan)) return false;
			if (plan.ReceiptVersion == 1)
				return TryAttachV1PendingProjectionCustody(Root, out _);
			if (plan.ReceiptVersion != 2) return false;
			if (!TryAttachV2ProjectionCustody(Root,
				out r_KingdomGatehouseProjectionV2 custody)) return false;
			Render render = Root.GetPart<Render>();
			Door door = Root.GetPart<Door>();
			if (render == null || door == null
				|| !KingdomGatehouseRules.TryRootRender(plan, out string glyph,
					out string color, out string tileColor, out string detail,
					out string closedTile, out string openTile)) return false;
			render.ColorString = color;
			render.TileColor = tileColor;
			render.DetailColor = detail;
			door.ClosedDisplay = glyph;
			door.OpenDisplay = RootOpenRenderString;
			door.ClosedTile = closedTile;
			door.OpenTile = openTile;
			door.SyncRender = true;
			render.RenderString = door.Open ? RootOpenRenderString : glyph;
			render.Tile = door.Open ? openTile : closedTile;
			return ReferenceEquals(Root.GetPart<r_KingdomGatehouseProjectionV2>(), custody)
				&& ExactRootPalette(Root, plan);
		}

		private static bool TryAttachV2ProjectionCustody(GameObject Root,
			out r_KingdomGatehouseProjectionV2 Custody)
		{
			if (!GameObject.Validate(Root)
				|| Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() != null)
			{
				Custody = null;
				return false;
			}
			Custody = GameObject.Validate(Root)
				? Root.GetPart<r_KingdomGatehouseProjectionV2>() : null;
			if (Custody != null) return true;
			r_KingdomGatehouseProjectionV2 staged =
				new r_KingdomGatehouseProjectionV2();
			try
			{
				if (!ReferenceEquals(Root.AddPart(staged), staged)) return false;
			}
			catch (Exception) { return false; }
			Custody = Root.GetPart<r_KingdomGatehouseProjectionV2>();
			return ReferenceEquals(Custody, staged);
		}

		private static bool TryAttachV1PendingProjectionCustody(GameObject Root,
			out r_KingdomGatehouseProjectionV1Pending Custody)
		{
			if (!GameObject.Validate(Root)
				|| Root.GetPart<r_KingdomGatehouseProjectionV2>() != null)
			{
				Custody = null;
				return false;
			}
			Custody = Root.GetPart<r_KingdomGatehouseProjectionV1Pending>();
			if (Custody != null) return true;
			r_KingdomGatehouseProjectionV1Pending staged =
				new r_KingdomGatehouseProjectionV1Pending();
			try
			{
				if (!ReferenceEquals(Root.AddPart(staged), staged)) return false;
			}
			catch (Exception) { return false; }
			Custody = Root.GetPart<r_KingdomGatehouseProjectionV1Pending>();
			return ReferenceEquals(Custody, staged);
		}

		private static bool ProjectionPartMatches(GameObject Root,
			KingdomGatehousePlan Plan, IPart Part, bool AllowSettledV1WithoutPart)
		{
			if (!GameObject.Validate(Root) || Plan == null) return false;
			r_KingdomGatehouseProjectionV2 v2 =
				Root.GetPart<r_KingdomGatehouseProjectionV2>();
			r_KingdomGatehouseProjectionV1Pending v1 =
				Root.GetPart<r_KingdomGatehouseProjectionV1Pending>();
			if (Plan.ReceiptVersion == 2)
				return v1 == null && v2 != null && ReferenceEquals(Part, v2);
			if (Plan.ReceiptVersion != 1 || v2 != null) return false;
			if (v1 != null) return ReferenceEquals(Part, v1);
			if (!AllowSettledV1WithoutPart || Part != null) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				if (Root.HasStringProperty(SatelliteStateProperty(i))
					|| !Root.HasIntProperty(SatelliteStateProperty(i))
					|| Root.GetIntProperty(SatelliteStateProperty(i))
						!= (int)KingdomGatehouseSlotState.Settled) return false;
			return true;
		}

		private static GameObject ProjectionCustody(IPart Part, int Index)
		{
			r_KingdomGatehouseProjectionV2 v2 =
				Part as r_KingdomGatehouseProjectionV2;
			if (v2 != null) return v2.ProjectionCustody(Index);
			r_KingdomGatehouseProjectionV1Pending v1 =
				Part as r_KingdomGatehouseProjectionV1Pending;
			return v1?.ProjectionCustody(Index);
		}

		private static bool SetProjectionCustody(IPart Part, int Index, GameObject Value)
		{
			r_KingdomGatehouseProjectionV2 v2 =
				Part as r_KingdomGatehouseProjectionV2;
			if (v2 != null) return v2.SetProjectionCustody(Index, Value);
			r_KingdomGatehouseProjectionV1Pending v1 =
				Part as r_KingdomGatehouseProjectionV1Pending;
			return v1 != null && v1.SetProjectionCustody(Index, Value);
		}

		private static bool TryRetireV1PendingProjectionCustody(GameObject Root,
			KingdomGatehousePlan Plan, IPart Part)
		{
			if (Plan == null || Plan.ReceiptVersion != 1) return true;
			r_KingdomGatehouseProjectionV1Pending pending =
				Part as r_KingdomGatehouseProjectionV1Pending;
			if (pending == null)
				return Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() == null;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				if (pending.ProjectionCustody(i) != null) return false;
			try { Root.RemovePart(pending); }
			catch (Exception) { }
			return Root.GetPart<r_KingdomGatehouseProjectionV1Pending>() == null;
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
					|| ReferenceEquals(item, Scaffold)
					|| RecognizedProjectionSatellite(item, Root, Plan, Z)) continue;
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
					if (!AuditFootprintCell(cell, Root, Scaffold, Plan, Z,
						out string blocker))
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
