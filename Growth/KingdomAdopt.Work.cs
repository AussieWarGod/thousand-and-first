using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomAdopt
	{
		/// <summary>
		/// Adopts the room the given cell stands in as a <see cref="KingdomAdoptRules.RoleKind.Work"/>
		/// role &mdash; civic, faith, craft, knowledge, power, defense, or any category this mod
		/// does not specifically name. There is no existing object to mark for a role like this,
		/// so on success this places one small marker object (<see cref="WorkMarkerBlueprint"/>)
		/// in the anchor cell and marks that instead; nothing the founder built is touched.
		/// Housing and ordinary work both designate the exact bounded room. Storage alone uses
		/// <see cref="AdoptExisting"/>, because its designation root is the exact vessel.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the anchor cell belongs to; must be the kingdom's own claimed
		/// ground.</param>
		/// <param name="Anchor">Where the founder stands; the flood fill that proves the room is
		/// enclosed starts here, so the founder must be standing on the room's own floor.</param>
		/// <param name="Key">A <c>KingdomBuildings</c> registry key naming what type of building
		/// this room counts as.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false. Nothing
		/// is placed or marked when it does.</param>
		/// <returns>True once the marker has actually been placed and marked.</returns>
		public static bool AdoptWork(KingdomSystem System, Zone Z, Cell Anchor, string Key, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is adopted on the kingdom's own ground, not in other people's houses.";
				return false;
			}
			if (Anchor == null || Anchor.ParentZone == null || Anchor.ParentZone.ZoneID != Z.ZoneID)
			{
				Failure = "Stand inside the room before adopting it.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				Failure = "No such design.";
				return false;
			}
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(entry.Category);
			if (!entry.Adoptable)
			{
				Failure = "A " + entry.Name + " needs its authored construction and cannot be assigned to a player-built room.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec)
				|| !KingdomAdoptabilityRules.TryClassify(entry.Key, entry.Category,
					spec.Size, spec.Open, out KingdomAdoptionTargetKind target, out Failure))
			{
				if (Failure == null) Failure = "That design has no exact adoption geometry.";
				return false;
			}
			if (target != KingdomAdoptionTargetKind.Room)
			{
				Failure = "A " + entry.Name + " is adopted from its exact container, not the room around you.";
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out Failure)) return false;
			bool alreadyServing = HasAdoptionMarker(Anchor);
			bool belowStage = System.Stage < entry.MinStage;
			KingdomAdoptRules.EnclosureMeasurement enclosure = MeasureExactRoom(
				Z, Anchor.X, Anchor.Y);
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(role,
				alreadyServing, belowStage, false, enclosure);
			if (verdict != KingdomAdoptRules.AdoptionVerdict.Adopted)
			{
				Failure = RefusalMessage(verdict, entry, null, System);
				return false;
			}
			if (!KingdomAdoptRules.MeetsMinimumUsable(role, spec.Size, enclosure))
			{
				Failure = "A " + entry.Name + " needs more usable floor than this room provides.";
				return false;
			}
			if (!RoomIsUnclaimed(Z, enclosure, out Failure)) return false;
			GameObject marker = GameObject.Create(WorkMarkerBlueprint);
			if (marker == null)
			{
				Failure = "The marker could not be set down.";
				return false;
			}
			Anchor.AddObject(marker);
			if (marker.CurrentCell != Anchor)
			{
				marker.Obliterate(null, Silent: true);
				Failure = "The marker could not be set down.";
				return false;
			}
			if (!BeginPending(marker, Key, true, out Failure))
			{
				if (GameObject.Validate(marker)) marker.Obliterate(null, Silent: true);
				return false;
			}
			KingdomAdoptionDesignationReceipt receipt;
			if (!KingdomAdoptionDesignation.TryStamp(marker, Z, Key, enclosure,
				out receipt, out Failure)
				|| !AdvancePending(marker, Key, 2, out Failure)
				|| !KingdomPlots.StampAdoptedExact(marker, entry, enclosure.FloorCells)
				|| !AdvancePending(marker, Key, 3, out Failure)
				|| !ReproveRoomForCommit(System, Z, marker, entry, receipt, out Failure)
				|| !AdvancePending(marker, Key, 4, out Failure)
				|| !FinalizePending(marker, Key, "", out Failure))
			{
				RollbackPending(marker);
				if (Failure == null) Failure = "The room's exact designation could not be recorded.";
				return false;
			}
			AnnounceAdoption(System, entry, marker);
			return true;
		}

		private static bool ReproveRoomForCommit(KingdomSystem System, Zone Z,
			GameObject Marker, KingdomRules.BuildEntry Entry,
			KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Marker) || Marker.CurrentCell == null
				|| Marker.CurrentZone != Z || Receipt == null
				|| !KingdomZoning.Permits(System, Z.ZoneID, Entry, out Failure)) return false;
			KingdomAdoptRules.EnclosureMeasurement live = MeasureExactRoom(Z,
				Marker.CurrentCell.X, Marker.CurrentCell.Y);
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(Entry.Category);
			if (!KingdomPlots.TryGetSpec(Entry.Key, out KingdomPlotRules.PlotSpec spec)
				|| !live.Bounded || live.DoorCells < 1 || live.FloorCells == null
				|| !KingdomAdoptRules.MeetsMinimumUsable(role, spec.Size, live)
				|| !KingdomAdoptRules.SameMembership(live.FloorCells, Receipt.Cells))
			{
				Failure = "The room changed before its designation committed."; return false;
			}
			return RoomIsUnclaimed(Z, live, out Failure);
		}

		private static bool RoomIsUnclaimed(Zone Z,
			KingdomAdoptRules.EnclosureMeasurement Enclosure, out string Failure)
		{
			return CellsAreUnclaimed(Z, Enclosure.FloorCells, out Failure);
		}
	}
}
