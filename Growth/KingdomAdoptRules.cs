using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free eligibility rules for the settlement's third way of raising a building: the
	/// founder built it themselves &mdash; in a Hearthpyre house, a cleared vanilla ruin, or four
	/// walls and a door laid by hand &mdash; and asks the settlement to recognise it. Sibling to
	/// <c>KingdomCommission</c> (the plan's own choice of where to build) and
	/// <c>r_KingdomScaffold</c> (a commission realised over time): here nothing is built and
	/// nothing is paid for, because the founder already did that part. The settlement checks only
	/// what the SPACE offers &mdash; never who built it, and never whether Hearthpyre exists,
	/// which is why this file has no notion of either. The engine-coupled read of a real zone
	/// lives in <c>KingdomAdopt</c>, in the same folder.
	/// </summary>
	public static class KingdomAdoptRules
	{
		/// <summary>
		/// The shape of requirement a building's <c>Category</c> reduces to. Reuses
		/// <see cref="KingdomRules.BuildEntry.Category"/> as the mod's own purpose taxonomy
		/// (see <see cref="ClassifyRole"/>) instead of inventing a parallel one, so a category a
		/// third-party design registers is judged by one of these three tests with no new code.
		/// </summary>
		public enum RoleKind
		{
			/// <summary>Somewhere a settler will actually sleep. Tested by
			/// <see cref="Assess"/>'s <c>HasBed</c> &mdash; the vanilla <c>Bed</c> part is the
			/// honest marker, the same one <c>KingdomSurvey</c> already counts beds by.</summary>
			Housing = 0,

			/// <summary>A container the settlement can keep water or food in. Tested by
			/// <c>IsStorageCapable</c> &mdash; a liquid vessel with real capacity, or an
			/// inventory-holding container with no liquid part of its own, exactly the split
			/// <c>KingdomCharterPart.DedicateVessel</c> already draws between a vessel and a
			/// larder.</summary>
			Storage = 1,

			/// <summary>Everything else: civic, faith, craft, knowledge, power, defense, and any
			/// category this mod does not specifically name. A work needs a building around it
			/// rather than a patch of ground, so the test is enclosure &mdash; walls and a door
			/// around enough floor to work in. See <see cref="MeasureEnclosure"/>.</summary>
			Work = 2
		}

		/// <summary>
		/// Why a structure was, or was not, accepted into a civic role. Ordered the way
		/// <see cref="Assess"/> checks them: whether the settlement already counts this structure
		/// comes first, because adoption must never re-mark something it already has; then
		/// whether the settlement has grown enough to support the role at all; only then the one
		/// physical test the role's <see cref="RoleKind"/> demands. Every enclosure failure gets
		/// its own name rather than a single generic "not enclosed", because a room that leaks,
		/// a room that is too small, and a room with no door are three different things for a
		/// founder to go fix.
		/// </summary>
		public enum AdoptionVerdict
		{
			Adopted = 0,
			RefusedAlreadyServing = 1,
			RefusedBelowStage = 2,
			RefusedNoBed = 3,
			RefusedNotStorageCapable = 4,
			RefusedUnbounded = 5,
			RefusedTooSmall = 6,
			RefusedNoDoor = 7
		}

		/// <summary>
		/// Reads a <c>BuildEntry.Category</c> as one of the three requirement shapes adoption
		/// tests for. Case-insensitive. An empty, null, or unrecognised category reads as
		/// <see cref="RoleKind.Work"/> &mdash; the safest default: every category this mod does
		/// not know by name still names something the founder built inside four walls sooner
		/// than it names a single bed or a single cask, and Work is the only one of the three
		/// tests that never risks marking the wrong kind of object (see
		/// <c>KingdomAdopt.AdoptExisting</c>, which refuses Work roles outright rather than
		/// guessing at a candidate object for them).
		/// </summary>
		/// <param name="Category">A <see cref="KingdomRules.BuildEntry.Category"/> value.</param>
		public static RoleKind ClassifyRole(string Category)
		{
			if (string.IsNullOrEmpty(Category))
			{
				return RoleKind.Work;
			}
			switch (Category.Trim().ToLowerInvariant())
			{
			case "housing":
				return RoleKind.Housing;
			case "storage":
				return RoleKind.Storage;
			default:
				return RoleKind.Work;
			}
		}

		// ---- Enclosure: the honest, bounded proxy for "a building rather than a patch of
		// ground" ----------------------------------------------------------------------------

		/// <summary>
		/// What a single cell is, for the purpose of flood-filling the room the founder is
		/// standing in. A door stops the flood fill from leaking past it &mdash; the room ends
		/// at the door, whatever lies beyond it &mdash; but is still counted as the opening the
		/// space needs; a wall stops it and is not counted at all. Everything else is floor.
		/// </summary>
		public enum CellKind
		{
			Open = 0,
			Wall = 1,
			Door = 2
		}

		/// <summary>
		/// Reads one cell's kind. The engine-coupled caller supplies this from a real zone (a
		/// vanilla wall reads <see cref="CellKind.Wall"/>, a vanilla door
		/// <see cref="CellKind.Door"/>, ground off the edge of the zone reads
		/// <see cref="CellKind.Wall"/> too, so a room can never claim the map's own edge as one
		/// of its walls); the flood fill itself never touches a real cell or zone.
		/// </summary>
		public delegate CellKind CellLookup(int X, int Y);

		/// <summary>
		/// Floor cells a work must enclose to count as a building rather than a patch of ground.
		/// A hut, honestly: four walls with barely room to turn around is still a building.
		/// </summary>
		public const int MinEnclosedRoomCells = 4;

		/// <summary>
		/// The flood fill's budget, and the honest cheaper proxy this file uses in place of a
		/// true unbounded reachability test: rather than walking however much of the zone it
		/// takes to prove a room is closed, the fill gives up once it has grown past a hall this
		/// size and reports itself unbounded. A real walled room this mod can plausibly ask a
		/// founder to have built by hand never approaches this many cells; open ground does,
		/// almost immediately, because open ground has nowhere to stop. The check runs once, at
		/// the moment the founder asks to adopt &mdash; never on a zone pass &mdash; so this
		/// budget is generous rather than stingy.
		/// </summary>
		public const int MaxEnclosedRoomCells = 200;

		/// <summary>What the flood fill from <see cref="MeasureEnclosure"/> found.</summary>
		public struct EnclosureMeasurement
		{
			/// <summary>False if the fill ran past <see cref="MaxEnclosedRoomCells"/> before
			/// closing on its own, or the start cell was not open floor. Every other field is
			/// still the true count reached before giving up, for a message that can say how
			/// far the leak got rather than just that there was one.</summary>
			public bool Bounded;

			/// <summary>Open floor cells reached, the start cell included.</summary>
			public int RoomCells;

			/// <summary>Distinct door cells that stopped the fill from leaking further.</summary>
			public int DoorCells;
		}

		/// <summary>
		/// Flood-fills the room the given cell stands in, treating a wall and a door alike as
		/// the edge of the room &mdash; a door stops the fill exactly like a wall does, but is
		/// counted separately, because "walls and a door" is what makes a room a room rather
		/// than either a walled-off void nobody could have built from inside (no door) or open
		/// ground with no room at all (no walls). Bounded by
		/// <see cref="MaxEnclosedRoomCells"/>; see that constant for why running past it, rather
		/// than continuing, is the honest answer to give.
		/// </summary>
		/// <param name="StartX">Where the founder stands, in <paramref name="Lookup"/>'s own
		/// coordinate space.</param>
		/// <param name="StartY">See <paramref name="StartX"/>.</param>
		/// <param name="Lookup">Reads a cell's kind. Called at most once per cell reached.</param>
		public static EnclosureMeasurement MeasureEnclosure(int StartX, int StartY, CellLookup Lookup)
		{
			EnclosureMeasurement measurement = default;
			if (Lookup == null || Lookup(StartX, StartY) != CellKind.Open)
			{
				// The founder must be standing on open floor, not on the wall or the door
				// itself; a start point the caller could not even stand on proves nothing.
				return measurement;
			}
			HashSet<long> visited = new HashSet<long>();
			Queue<long> frontier = new Queue<long>();
			visited.Add(PackCell(StartX, StartY));
			frontier.Enqueue(PackCell(StartX, StartY));
			int roomCells = 0;
			int doorCells = 0;
			while (frontier.Count > 0)
			{
				if (roomCells > MaxEnclosedRoomCells)
				{
					measurement.Bounded = false;
					measurement.RoomCells = roomCells;
					measurement.DoorCells = doorCells;
					return measurement;
				}
				long packed = frontier.Dequeue();
				int x = (int)(packed >> 32);
				int y = (int)packed;
				roomCells++;
				VisitNeighbor(x + 1, y, Lookup, visited, frontier, ref doorCells);
				VisitNeighbor(x - 1, y, Lookup, visited, frontier, ref doorCells);
				VisitNeighbor(x, y + 1, Lookup, visited, frontier, ref doorCells);
				VisitNeighbor(x, y - 1, Lookup, visited, frontier, ref doorCells);
			}
			measurement.Bounded = true;
			measurement.RoomCells = roomCells;
			measurement.DoorCells = doorCells;
			return measurement;
		}

		private static void VisitNeighbor(int X, int Y, CellLookup Lookup, HashSet<long> Visited, Queue<long> Frontier, ref int DoorCells)
		{
			long key = PackCell(X, Y);
			if (!Visited.Add(key))
			{
				return;
			}
			switch (Lookup(X, Y))
			{
			case CellKind.Wall:
				return;
			case CellKind.Door:
				DoorCells++;
				return;
			default:
				Frontier.Enqueue(key);
				return;
			}
		}

		/// <summary>
		/// Packs a coordinate pair into one collision-free key for the visited set. Both axes
		/// round-trip exactly, including negative coordinates, because the low 32 bits store
		/// <paramref name="Y"/>'s own bit pattern rather than a range-limited encoding of it.
		/// </summary>
		private static long PackCell(int X, int Y)
		{
			return ((long)X << 32) | (uint)Y;
		}

		/// <summary>
		/// The settlement's verdict on adopting a structure into the given role. Checked in an
		/// order that never lets a later refusal hide behind an earlier one: already-serving
		/// first, because adoption must never re-mark something the settlement already counts;
		/// then growth stage; only then the one physical test the role demands.
		/// </summary>
		/// <param name="Role">What kind of requirement this role reduces to.</param>
		/// <param name="AlreadyServing">True if the candidate already carries the settlement's
		/// own built-marker &mdash; commissioned or previously adopted, either way already
		/// counted, and adoption is never a second mark on the same thing.</param>
		/// <param name="BelowMinStage">True if the settlement has not yet reached the role's
		/// minimum growth stage.</param>
		/// <param name="HasBed">Read only for <see cref="RoleKind.Housing"/>: true if the
		/// candidate carries a vanilla <c>Bed</c> part.</param>
		/// <param name="IsStorageCapable">Read only for <see cref="RoleKind.Storage"/>: true if
		/// the candidate is a liquid vessel with real capacity, or an inventory-holding
		/// container with no liquid part of its own.</param>
		/// <param name="Enclosure">Read only for <see cref="RoleKind.Work"/>: the flood fill
		/// from the candidate room's anchor cell.</param>
		public static AdoptionVerdict Assess(RoleKind Role, bool AlreadyServing, bool BelowMinStage, bool HasBed, bool IsStorageCapable, EnclosureMeasurement Enclosure)
		{
			if (AlreadyServing)
			{
				return AdoptionVerdict.RefusedAlreadyServing;
			}
			if (BelowMinStage)
			{
				return AdoptionVerdict.RefusedBelowStage;
			}
			switch (Role)
			{
			case RoleKind.Housing:
				return HasBed ? AdoptionVerdict.Adopted : AdoptionVerdict.RefusedNoBed;
			case RoleKind.Storage:
				return IsStorageCapable ? AdoptionVerdict.Adopted : AdoptionVerdict.RefusedNotStorageCapable;
			default:
				return ClassifyEnclosure(Enclosure);
			}
		}

		private static AdoptionVerdict ClassifyEnclosure(EnclosureMeasurement Enclosure)
		{
			if (!Enclosure.Bounded)
			{
				return AdoptionVerdict.RefusedUnbounded;
			}
			if (Enclosure.RoomCells < MinEnclosedRoomCells)
			{
				return AdoptionVerdict.RefusedTooSmall;
			}
			if (Enclosure.DoorCells < 1)
			{
				return AdoptionVerdict.RefusedNoDoor;
			}
			return AdoptionVerdict.Adopted;
		}

		/// <summary>True for every verdict except <see cref="AdoptionVerdict.Adopted"/>.</summary>
		public static bool IsRefusal(AdoptionVerdict Verdict)
		{
			return Verdict != AdoptionVerdict.Adopted;
		}
	}
}
