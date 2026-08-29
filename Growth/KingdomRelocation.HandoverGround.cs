using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool DestinationReady(Zone Zone, KingdomRelocationReceipt Receipt,
			KingdomRelocationMove Move, out string Blocker)
		{
			Blocker = null; HashSet<string> allowed = new HashSet<string>();
			allowed.Add(Move.FrameId);
			for (int i = 0; i < Move.StakeIds.Length; i++) allowed.Add(Move.StakeIds[i]);
			for (int i = 0; i < Move.Rows.Count; i++) allowed.Add(Move.Rows[i].ObjectId);
			for (int i = 0; i < Move.Clearance.Count; i++) allowed.Add(Move.Clearance[i].ObjectId);
			for (int y = Move.Destination.Y1; y <= Move.Destination.Y2; y++)
				for (int x = Move.Destination.X1; x <= Move.Destination.X2; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null) { Blocker = "the destination reaches beyond the zone"; return false; }
					foreach (GameObject item in cell.GetObjects())
					{
						if (!GameObject.Validate(item)) continue;
						if (item.IsCreature || item.IsPlayer())
						{
							Blocker = (item.IsPlayer() ? "you are" : item.ShortDisplayNameStripped + " is")
								+ " standing in the receiving frame"; return false;
						}
						if (allowed.Contains(item.IDIfAssigned)
							|| KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare) continue;
						Blocker = KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped)
							+ " now stands at " + x + "," + y; return false;
					}
				}
			return true;
		}

		private static bool MoveClearance(Zone Zone, KingdomRelocationReceipt Receipt,
			KingdomRelocationClearRow Row, ref string Expected, out string Failure)
		{
			Failure = null;
			if (Row.State == KingdomRelocationClearState.Removed) return true;
			if (Row.State == KingdomRelocationClearState.Standing)
			{
				Row.State = KingdomRelocationClearState.RemovalPending;
				if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			}
			GameObject exact = Escrow(Receipt, Row.ObjectId, true);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Zone,
				Row.ObjectId, out GameObject physical);
			if (state == KingdomPhysicalLookupState.Ambiguous
				|| (exact != null && physical != null && !ReferenceEquals(exact, physical)))
			{ Failure = "Exact natural destination clearance is duplicated."; return false; }
			if (exact == null) exact = physical;
			if (!GameObject.Validate(exact) || exact.Blueprint != Row.Blueprint)
			{ Failure = "Exact natural destination clearance is absent."; return false; }
			bool standing = state == KingdomPhysicalLookupState.Exact
				&& ExactAt(physical, Zone, Row.Blueprint, Row.X, Row.Y);
			if (state == KingdomPhysicalLookupState.Exact && !standing)
			{ Failure = "Exact natural destination clearance was displaced."; return false; }
			if (state == KingdomPhysicalLookupState.Absent && exact.CurrentCell != null)
			{ Failure = "Rooted destination clearance escaped its receipt escrow."; return false; }
			if (!RootEscrow(Receipt, exact, true, out Failure)) return false;
			if (standing)
			{
				Cell cell = exact.CurrentCell;
				try
				{
					if (cell == null || !cell.RemoveObject(exact, Forced: true, System: true,
						IgnoreGravity: true, Silent: true, NoStack: true))
					{ Failure = "Natural destination clearance did not leave its cell."; return false; }
					KingdomSurvey.ObserveRemovedFromActive(Zone, exact);
				}
				catch (System.Exception exception)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Zone, exact);
					Failure = "Natural clearance callback interrupted: " + exception.Message; return false;
				}
			}
			Row.State = KingdomRelocationClearState.Removed;
			return TryPublish(Zone, Expected, Receipt, out Expected, out Failure);
		}

		private static void ReleaseClearanceEscrow(KingdomRelocationReceipt Receipt,
			KingdomRelocationMove Move)
		{
			for (int i = 0; i < Move.Clearance.Count; i++)
			{
				KingdomRelocationClearRow row = Move.Clearance[i];
				GameObject rooted = Escrow(Receipt, row.ObjectId, true);
				ClearEscrow(Receipt, row.ObjectId, true, rooted);
			}
		}
	}
}
