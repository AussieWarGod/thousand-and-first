using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's answer to a building the founder raised without it: recognises a
	/// structure the founder already built as serving a civic role, or says plainly what it is
	/// missing. Follows the <see cref="KingdomSalvage"/>/<c>DedicateVessel</c> idiom &mdash; a
	/// single call that does its own eligibility check and its own success messaging, surfacing
	/// only a decline through <c>Failure</c>. A refusal changes nothing: the structure stays
	/// exactly where the founder put it, untouched. Adoption is a MARK, never a transfer, and it
	/// is always reversible through <see cref="Release"/> &mdash; nothing the founder built is
	/// ever consumed, moved, or destroyed by adopting or releasing it.
	/// </summary>
	/// <remarks>
	/// This checks the requirements of the SPACE, never who made it. A Hearthpyre house, a
	/// cleared vanilla ruin, and four walls and a door laid up by hand are read exactly alike;
	/// nothing here so much as knows whether Hearthpyre is installed. See
	/// <see cref="KingdomAdoptRules"/> for the pure eligibility math this reads real objects and
	/// cells into.
	/// </remarks>
	public static class KingdomAdopt
	{
		/// <summary>Shared with every other kind of raised building &mdash; a commissioned work,
		/// an adopted one, and a previously-adopted one all read as "built" alike. Set by this
		/// file only on a structure that did not already carry it (see
		/// <see cref="KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing"/>).</summary>
		public const string BuiltProperty = "KingdomBuilt";

		/// <summary>Set on a structure only by this file, and cleared only by
		/// <see cref="Release"/>. Distinguishes an adopted structure from a commissioned one so
		/// releasing an adoption can never touch a building the settlement actually raised
		/// itself.</summary>
		public const string AdoptedProperty = "KingdomAdopted";

		/// <summary>The <c>BuildEntry.Key</c> a structure was adopted as, so
		/// <see cref="Release"/> can name what is being given up and this file never has to
		/// re-derive it from anything that might have changed since.</summary>
		public const string AdoptedKeyProperty = "KingdomAdoptedKey";

		/// <summary>
		/// Which single property (<see cref="StoresProperty"/> or <see cref="LarderProperty"/>)
		/// this file itself switched on for a <see cref="KingdomAdoptRules.RoleKind.Storage"/>
		/// adoption, or empty for every other role. A founder can dedicate a vessel through the
		/// Charter's own dedication action either before or after adopting it as storage;
		/// releasing the adoption must undo only what the adoption itself set, never a
		/// dedication the founder made independently and would expect to survive.
		/// </summary>
		public const string AdoptedMarkProperty = "KingdomAdoptedMark";

		public const string StoresProperty = "KingdomStores";

		public const string LarderProperty = "KingdomLarder";

		public const string StaffNeededProperty = "KingdomStaffNeeded";

		public const string ThresholdManningProperty = "KingdomThresholdManning";

		public const string HandCrankedProperty = "KingdomHandCranked";

		public const string DefenceProperty = "KingdomDefence";

		/// <summary>
		/// The marker a <see cref="KingdomAdoptRules.RoleKind.Work"/> adoption sets down. A work
		/// role has no single existing object to mark &mdash; a shrine, a workshop, a charging
		/// post are rooms, not things &mdash; so adoption places one small, inert object inside
		/// the room to carry the settlement's mark, the literal "marker down in a building" the
		/// design calls for. It exists only to carry the mark: <see cref="Release"/> destroys it
		/// outright rather than leaving an empty plaque behind, because it was never something
		/// the founder made.
		/// </summary>
		public const string WorkMarkerBlueprint = "r_KingdomAdoptionMarker";

		/// <summary>
		/// Adopts an existing object &mdash; a bed the founder built, a vessel or larder they
		/// stocked &mdash; into a <see cref="KingdomAdoptRules.RoleKind.Housing"/> or
		/// <see cref="KingdomAdoptRules.RoleKind.Storage"/> role. Refuses outright for any other
		/// role: a work has no single object to mark, and guessing at one would be marking the
		/// wrong thing. See <see cref="AdoptWork"/> for that case.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the candidate stands in; must be the kingdom's own claimed
		/// ground.</param>
		/// <param name="Candidate">The object to adopt.</param>
		/// <param name="Key">A <c>KingdomBuildings</c> registry key naming what type of building
		/// this counts as; its <c>Category</c> decides which test applies.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false. Nothing
		/// is spent or marked when it does.</param>
		/// <returns>True once the candidate has actually been marked.</returns>
		public static bool AdoptExisting(KingdomSystem System, Zone Z, GameObject Candidate, string Key, out string Failure)
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
			if (Candidate == null || !GameObject.Validate(Candidate) || Candidate.CurrentZone == null || Candidate.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing here to adopt.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				Failure = "No such design.";
				return false;
			}
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(entry.Category);
			if (role == KingdomAdoptRules.RoleKind.Work)
			{
				Failure = "A " + entry.Name + " is adopted as the room around you, not as a single thing standing in it.";
				return false;
			}
			bool alreadyServing = Candidate.GetIntProperty(BuiltProperty) == 1;
			bool belowStage = System.Stage < entry.MinStage;
			bool hasBed = Candidate.HasPart("Bed");
			LiquidVolume liquidVolume = Candidate.GetPart<LiquidVolume>();
			bool isVessel = liquidVolume != null && liquidVolume.MaxVolume > 0;
			bool isLarder = liquidVolume == null && Candidate.Inventory != null;
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(role, alreadyServing, belowStage, hasBed, isVessel || isLarder, default);
			if (verdict != KingdomAdoptRules.AdoptionVerdict.Adopted)
			{
				Failure = RefusalMessage(verdict, entry, Candidate, System);
				return false;
			}
			Candidate.SetIntProperty(BuiltProperty, 1);
			Candidate.SetIntProperty(AdoptedProperty, 1);
			Candidate.SetStringProperty(AdoptedKeyProperty, Key);
			string mark = "";
			if (role == KingdomAdoptRules.RoleKind.Storage)
			{
				string storageProperty = isVessel ? StoresProperty : LarderProperty;
				if (Candidate.GetIntProperty(storageProperty) != 1)
				{
					// Only track it as ours to undo if it was not already set - a vessel the
					// founder dedicated through the Charter before adopting it keeps that
					// dedication when the adoption is later released.
					Candidate.SetIntProperty(storageProperty, 1);
					mark = storageProperty;
				}
			}
			Candidate.SetStringProperty(AdoptedMarkProperty, mark);
			ApplyRoleFixtures(Candidate, entry);
			AnnounceAdoption(System, entry, Candidate);
			// A plot-sized design speaks for a rect of ground, not one cell. Nothing the founder
			// built is touched; the settlement only learns how much ground is spoken for.
			KingdomPlots.StampAdopted(Candidate, entry);
			return true;
		}

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
			marker.SetIntProperty(BuiltProperty, 1);
			marker.SetIntProperty(AdoptedProperty, 1);
			marker.SetStringProperty(AdoptedKeyProperty, Key);
			ApplyRoleFixtures(marker, entry);
			AnnounceAdoption(System, entry, marker);
			KingdomPlots.StampAdopted(marker, entry);
			return true;
		}

		/// <summary>
		/// Releases a structure or marker previously accepted by <see cref="AdoptExisting"/> or
		/// <see cref="AdoptWork"/>. Undoes exactly what adoption itself set &mdash; the built
		/// mark, the adoption mark, any staffing/manning/hand-cranked/defence values adoption
		/// applied, and (only if adoption itself was the one to set it)
		/// <see cref="StoresProperty"/> or <see cref="LarderProperty"/> &mdash; and destroys the
		/// object only when it was <see cref="WorkMarkerBlueprint"/>, which existed solely to
		/// carry the mark. Everything the founder actually built stands exactly where it stood.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the object stands in; must be the kingdom's own claimed ground.</param>
		/// <param name="Adopted">The object to release.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false.</param>
		/// <returns>True once the object's civic standing has actually changed.</returns>
		public static bool Release(KingdomSystem System, Zone Z, GameObject Adopted, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is released on the kingdom's own ground, not in other people's houses.";
				return false;
			}
			if (Adopted == null || !GameObject.Validate(Adopted) || Adopted.CurrentZone == null || Adopted.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing here to release.";
				return false;
			}
			if (Adopted.GetIntProperty(AdoptedProperty) != 1)
			{
				Failure = Adopted.ShortDisplayName + " was never adopted; there is nothing to release.";
				return false;
			}
			string key = Adopted.GetStringProperty(AdoptedKeyProperty);
			string mark = Adopted.GetStringProperty(AdoptedMarkProperty);
			string name = Adopted.ShortDisplayName;
			Adopted.SetIntProperty(BuiltProperty, 0);
			Adopted.SetIntProperty(AdoptedProperty, 0);
			Adopted.SetStringProperty(AdoptedKeyProperty, null, RemoveIfNull: true);
			Adopted.SetStringProperty(AdoptedMarkProperty, null, RemoveIfNull: true);
			Adopted.SetIntProperty(StaffNeededProperty, 0);
			Adopted.SetIntProperty(ThresholdManningProperty, 0);
			Adopted.SetIntProperty(HandCrankedProperty, 0);
			Adopted.SetIntProperty(DefenceProperty, 0);
			if (mark == StoresProperty || mark == LarderProperty)
			{
				Adopted.SetIntProperty(mark, 0);
			}
			bool wasMarker = Adopted.Blueprint == WorkMarkerBlueprint;
			if (wasMarker)
			{
				// The marker existed only to carry the mark; nothing else about it was ever the
				// founder's. Destroying it is the mirror of AdoptWork placing it, not a wound to
				// anything the founder made.
				Adopted.Destroy(null, Silent: true);
			}
			MessageQueue.AddPlayerMessage("{{K|" + name + " is released" + (wasMarker ? "." : " from " + System.SeatName + "'s standing.") + "}}"
				+ (wasMarker ? "" : " It stands exactly where it stood."));
			KingdomLog.Log("adopt: released " + name + " (" + key + ") at " + System.SeatName);
			return true;
		}

		/// <summary>
		/// Sets the same staffing, manning, hand-cranked, and defence marks
		/// <c>r_KingdomScaffold.Complete</c> applies to a commissioned building, from the same
		/// registry entry, so an adopted work behaves exactly like a built one to every system
		/// that reads those properties (crew assignment, the defence tally, the growth survey).
		/// </summary>
		private static void ApplyRoleFixtures(GameObject Target, KingdomRules.BuildEntry Entry)
		{
			if (Entry.Staff > 0)
			{
				Target.SetIntProperty(StaffNeededProperty, Entry.Staff);
				if (KingdomRules.IsThresholdManning(Entry.Manning))
				{
					Target.SetIntProperty(ThresholdManningProperty, 1);
				}
				if (Target.GetPart<Capacitor>() != null)
				{
					Target.SetIntProperty(HandCrankedProperty, 1);
				}
			}
			if (Entry.Defence > 0)
			{
				Target.SetIntProperty(DefenceProperty, Entry.Defence);
			}
		}

		private static void AnnounceAdoption(KingdomSystem System, KingdomRules.BuildEntry Entry, GameObject Target)
		{
			MessageQueue.AddPlayerMessage("{{G|" + Target.ShortDisplayName + " is adopted into " + System.SeatName + " as " + XRL.Language.Grammar.A(Entry.Name) + ".}}");
			KingdomChronicle.Record(System, Target.ShortDisplayName + " was adopted into " + System.KingdomDisplayName + " as " + XRL.Language.Grammar.A(Entry.Name));
			System.RecordDeed(Target.ShortDisplayName + " adopted into " + System.KingdomDisplayName + " as " + XRL.Language.Grammar.A(Entry.Name));
			KingdomLog.Log("adopt: " + Target.ShortDisplayName + " as " + Entry.Key + " at " + System.SeatName);
		}

		/// <summary>Whether any object already at this cell carries the adoption mark. A cheap,
		/// deliberately local check &mdash; it does not walk the whole room &mdash; so adopting
		/// the same room as two different works fails loudly only once a marker already stands
		/// where the new one would go, not before.</summary>
		private static bool HasAdoptionMarker(Cell Anchor)
		{
			foreach (GameObject item in Anchor.GetObjects())
			{
				if (item.GetIntProperty(AdoptedProperty) == 1)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Reads a real cell into the pure enclosure test's vocabulary. A door reads as
		/// <see cref="KingdomAdoptRules.CellKind.Door"/> before a wall is even checked, because
		/// <c>Cell.HasWall()</c> only asks whether anything here is a wall and a door on a wall
		/// tile would otherwise be read as one; ground off the edge of the zone reads as a wall,
		/// so a room can never claim the map's own boundary as one of its own.
		/// </summary>
		private static KingdomAdoptRules.CellKind CellKindAt(Zone Z, int X, int Y)
		{
			Cell cell = Z.GetCell(X, Y);
			if (cell == null)
			{
				return KingdomAdoptRules.CellKind.Wall;
			}
			foreach (GameObject item in cell.GetObjects())
			{
				if (item.IsDoor())
				{
					return KingdomAdoptRules.CellKind.Door;
				}
			}
			if (cell.HasWall())
			{
				return KingdomAdoptRules.CellKind.Wall;
			}
			return KingdomAdoptRules.CellKind.Open;
		}

		/// <summary>Composes the player-facing sentence for a refusal, in the settlement's own
		/// voice. Never called for <see cref="KingdomAdoptRules.AdoptionVerdict.Adopted"/>.</summary>
		private static string RefusalMessage(KingdomAdoptRules.AdoptionVerdict Verdict, KingdomRules.BuildEntry Entry, GameObject Candidate, KingdomSystem System)
		{
			string name = Entry.Name;
			switch (Verdict)
			{
			case KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing:
				return ((Candidate != null) ? Candidate.ShortDisplayName : "Something here") + " already stands for " + System.SeatName + ". Release it before adopting it again.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage:
				return System.SeatName + " hasn't grown enough yet to keep a " + name + " standing. Come back once it has.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedNoBed:
				return "It wants a bed, and there is none here.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedNotStorageCapable:
				return "It wants something that can hold water or food, and this isn't it.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedUnbounded:
				return "A " + name + " wants walls all the way around it, and this ground runs open past where they should be.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedTooSmall:
				return "A " + name + " wants more room than this, enough for a settler to actually work in, not just stand.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedNoDoor:
				return "A " + name + " wants a door as well as walls. This is sealed all the way around; nobody could have built it from inside.";
			default:
				return name + " cannot be adopted right now.";
			}
		}
	}
}
