using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool MovePlotRow(Zone Zone, KingdomRelocationReceipt Receipt,
			KingdomRelocationMove Move, KingdomRelocationRow Row, ref string Expected,
			out string Failure)
		{
			Failure = null;
			if (Row.State == KingdomRelocationRowState.Destination)
				return VerifyDestinationRow(Zone, Move, Row, out Failure);
			if (Row.State == KingdomRelocationRowState.Source)
			{
				Row.State = KingdomRelocationRowState.Rooted;
				if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			}
			GameObject item = Escrow(Receipt, Row.ObjectId, false);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Zone,
				Row.ObjectId, out GameObject standing);
			if (state == KingdomPhysicalLookupState.Ambiguous
				|| (item != null && standing != null && !ReferenceEquals(item, standing)))
			{ Failure = "A whole-lot object identity duplicated during handover."; return false; }
			if (item == null) item = standing;
			if (!GameObject.Validate(item) || item.Blueprint != Row.Blueprint)
			{ Failure = "A whole-lot object disappeared during handover."; return false; }
			bool atSource = state == KingdomPhysicalLookupState.Exact
				&& ExactAt(standing, Zone, Row.Blueprint,
					Move.Source.X1 + Row.OffsetX, Move.Source.Y1 + Row.OffsetY);
			bool atDestination = state == KingdomPhysicalLookupState.Exact
				&& ExactAt(standing, Zone, Row.Blueprint,
					Move.Destination.X1 + Row.OffsetX,
					Move.Destination.Y1 + Row.OffsetY);
			if (state == KingdomPhysicalLookupState.Exact && !atSource && !atDestination)
			{ Failure = "A whole-lot object moved outside both receipt cells."; return false; }
			if (state == KingdomPhysicalLookupState.Absent && item.CurrentCell != null)
			{ Failure = "A rooted whole-lot object escaped its receipt escrow."; return false; }
			if (atDestination)
			{
				Row.State = KingdomRelocationRowState.Destination;
				if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
				ClearEscrow(Receipt, Row.ObjectId, false, item); return true;
			}
			if (!RootEscrow(Receipt, item, false, out Failure)) return false;
			if (atSource && !RemoveForHandover(Zone, item, out Failure)) return false;
			Cell destination = Zone.GetCell(Move.Destination.X1 + Row.OffsetX,
				Move.Destination.Y1 + Row.OffsetY);
			if (destination == null) { Failure = "A destination lot cell is absent."; return false; }
			GameObject accepted = null;
			try { accepted = destination.AddObject(item, NoStack: true, Silent: true); }
			catch (System.Exception exception)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Zone, item);
				Failure = "Whole-lot placement callback interrupted: " + exception.Message; return false;
			}
			KingdomSurvey.ObserveAddResultInActive(Zone, item, accepted);
			if (!ReferenceEquals(accepted, item)
				|| !ExactAt(item, Zone, Row.Blueprint, destination.X, destination.Y))
			{ Failure = "The engine replaced or displaced a whole-lot object."; return false; }
			Row.State = KingdomRelocationRowState.Destination;
			if (!TryPublish(Zone, Expected, Receipt, out Expected, out Failure)) return false;
			if (!ClearEscrow(Receipt, Row.ObjectId, false, item))
			{ Failure = "Whole-lot escrow would not retire."; return false; }
			return true;
		}

		private static bool RemoveForHandover(Zone Zone, GameObject Item, out string Failure)
		{
			Failure = null; Cell source = Item.CurrentCell;
			try
			{
				if (source == null || !source.RemoveObject(Item, Forced: true, System: true,
					IgnoreGravity: true, Silent: true, NoStack: true))
				{ Failure = "A whole-lot object would not leave its source cell."; return false; }
				KingdomSurvey.ObserveRemovedFromActive(Zone, Item); return true;
			}
			catch (System.Exception exception)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Zone, Item);
				Failure = "Whole-lot removal callback interrupted: " + exception.Message; return false;
			}
		}

		private static bool VerifyDestinationRow(Zone Zone, KingdomRelocationMove Move,
			KingdomRelocationRow Row, out string Failure)
		{
			Failure = null;
			if (KingdomConstruction.FindExactId(Zone, Row.ObjectId, out GameObject exact)
				== KingdomPhysicalLookupState.Exact && ExactAt(exact, Zone, Row.Blueprint,
					Move.Destination.X1 + Row.OffsetX,
					Move.Destination.Y1 + Row.OffsetY)) return true;
			Failure = "A handed-over whole-lot object is absent from its exact destination.";
			return false;
		}
	}
}
