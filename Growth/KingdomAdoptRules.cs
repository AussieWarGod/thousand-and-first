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
	public static partial class KingdomAdoptRules
	{
		/// <summary>
		/// The shape of requirement a building's <c>Category</c> reduces to. Reuses
		/// <see cref="KingdomRules.BuildEntry.Category"/> as the mod's own purpose taxonomy
		/// (see <see cref="ClassifyRole"/>) instead of inventing a parallel one, so a category a
		/// third-party design registers is judged by one of these three tests with no new code.
		/// </summary>
		public enum RoleKind
		{
			/// <summary>An exact bounded room. Beds inside later supply physical capacity;
			/// designation itself supplies none.</summary>
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
			RefusedNotStorageCapable = 3,
			RefusedUnbounded = 4,
			RefusedTooSmall = 5,
			RefusedNoDoor = 6
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

		public static bool TryParseAdoptable(string Source, out bool Adoptable,
			out string Failure)
		{
			Adoptable = false; Failure = null;
			if (string.IsNullOrWhiteSpace(Source)) return true;
			string value = Source.Trim().ToLowerInvariant();
			if (value == "yes") { Adoptable = true; return true; }
			if (value == "no") return true;
			Failure = "has malformed Adoptable (expected yes or no)";
			return false;
		}

		/// <summary>Minimum exact usable floor by spatial role and declared plot tier. Storage is
		/// one exact container; enclosed housing and work share the current room ladder. Keeping
		/// role in this law prevents a future target kind from silently borrowing room geometry.</summary>
		public static int MinimumUsableCells(RoleKind Role, KingdomPlotRules.PlotSize Size)
		{
			if (Role == RoleKind.Storage) return 1;
			switch (Size)
			{
			case KingdomPlotRules.PlotSize.Medium: return 12;
			case KingdomPlotRules.PlotSize.Large: return 24;
			case KingdomPlotRules.PlotSize.Huge: return 40;
			default: return MinEnclosedRoomCells;
			}
		}

		public static int MinimumUsableCells(KingdomPlotRules.PlotSize Size)
		{
			return MinimumUsableCells(RoleKind.Work, Size);
		}

		/// <summary>Compatibility-created measurements have no usable list; exact runtime
		/// measurements always do. New admission and reproof therefore consume live usable truth,
		/// while existing pure callers that construct the old three-field value remain meaningful.</summary>
		public static int UsableCellCount(EnclosureMeasurement Enclosure)
		{
			return Enclosure.UsableFloorCells == null ? Enclosure.RoomCells : Enclosure.UsableCells;
		}

		public static bool MeetsMinimumUsable(RoleKind Role, KingdomPlotRules.PlotSize Size,
			EnclosureMeasurement Enclosure)
		{
			return UsableCellCount(Enclosure) >= MinimumUsableCells(Role, Size);
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
		/// <param name="IsStorageCapable">Read only for <see cref="RoleKind.Storage"/>: true if
		/// the candidate is a liquid vessel with real capacity, or an inventory-holding
		/// container with no liquid part of its own.</param>
		/// <param name="Enclosure">Read only for <see cref="RoleKind.Work"/>: the flood fill
		/// from the candidate room's anchor cell.</param>
		public static AdoptionVerdict Assess(RoleKind Role, bool AlreadyServing,
			bool BelowMinStage, bool IsStorageCapable, EnclosureMeasurement Enclosure)
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
				return ClassifyEnclosure(Enclosure);
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
