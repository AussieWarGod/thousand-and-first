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

	}
}
