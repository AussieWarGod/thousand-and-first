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
		/// Refuses outright for a Housing or Storage role: see <see cref="AdoptExisting"/> for
		/// those.
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
			if (role != KingdomAdoptRules.RoleKind.Work)
			{
				Failure = "A " + entry.Name + " is adopted from a single thing built for it, not from the room around you.";
				return false;
			}
			bool alreadyServing = HasAdoptionMarker(Anchor);
			bool belowStage = System.Stage < entry.MinStage;
			KingdomAdoptRules.EnclosureMeasurement enclosure = KingdomAdoptRules.MeasureEnclosure(Anchor.X, Anchor.Y, (X, Y) => CellKindAt(Z, X, Y));
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(role, alreadyServing, belowStage, false, false, enclosure);
			if (verdict != KingdomAdoptRules.AdoptionVerdict.Adopted)
			{
				Failure = RefusalMessage(verdict, entry, null, System);
				return false;
			}
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
			marker.SetIntProperty(BuiltProperty, 1);
			KingdomGovernanceScope.Commit("adopt building");
			marker.SetIntProperty(AdoptedProperty, 1);
			marker.SetStringProperty(AdoptedKeyProperty, Key);
			ApplyRoleFixtures(marker, entry);
			AnnounceAdoption(System, entry, marker);
			KingdomPlots.StampAdopted(marker, entry);
			return true;
		}
	}
}
