using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomAdopt
	{
		// Qud's ordinary navigable-adjacent ceiling; mines and pull-down hazards exceed it.
		private const int MaxUsableNavigationWeight = 5;

		private static void AnnounceAdoption(KingdomSystem System, KingdomRules.BuildEntry Entry, GameObject Target)
		{
			string seat = KingdomPresentation.Rich(System.SeatName);
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage("{{G|" + Target.ShortDisplayName + " is adopted into " + seat + " as " + XRL.Language.Grammar.A(Entry.Name) + ".}}");
			KingdomChronicle.Record(System, Target.ShortDisplayName + " was adopted into " + realm + " as " + XRL.Language.Grammar.A(Entry.Name));
			System.RecordDeed(Target.ShortDisplayName + " adopted into " + realm + " as " + XRL.Language.Grammar.A(Entry.Name));
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

		/// <summary>Reads structural membership independently from live usable floor. Occupants,
		/// dropped items, and furniture cannot rewrite a room's signed cells. Permanent solids,
		/// pits, gas/no-autowalk hazards, and open liquid only remove current usable floor.</summary>
		private static KingdomAdoptRules.CellObservation CellObservationAt(Zone Z, int X, int Y)
		{
			Cell cell = Z.GetCell(X, Y);
			if (cell == null)
				return new KingdomAdoptRules.CellObservation(
					KingdomAdoptRules.EnclosureRegion.Outside);
			bool door = false;
			foreach (GameObject item in cell.GetObjects())
				if (GameObject.Validate(item) && item.IsDoor()) door = true;
			if (door)
				return new KingdomAdoptRules.CellObservation(
					UsableCell(cell, true) ? KingdomAdoptRules.EnclosureRegion.Ingress
						: KingdomAdoptRules.EnclosureRegion.Shell);
			if (cell.HasWall())
				return new KingdomAdoptRules.CellObservation(
					KingdomAdoptRules.EnclosureRegion.Shell);
			return new KingdomAdoptRules.CellObservation(
				KingdomAdoptRules.EnclosureRegion.Membership, UsableCell(cell));
		}

		private static bool UsableCell(Cell Cell, bool Doorway = false)
		{
			if (Cell == null || Cell.HasOpenLiquidVolume()) return false;
			bool permanentBlocker = false;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (!GameObject.Validate(item) || item.IsPlayer() || item.IsCreature) continue;
				StairsDown down = item.GetPart<StairsDown>();
				Tinkering_Mine mine = item.GetPart<Tinkering_Mine>();
				if ((down != null && down.PullDown) || (mine != null && mine.Armed)
					|| item.HasPart("Gas")
					|| item.HasTagOrProperty("Pit") || item.HasTagOrProperty("NoAutowalk"))
					return false;
				Door door = item.GetPart<Door>();
				if (door != null && door.Locked) permanentBlocker = true;
				if (item.Physics != null && item.Physics.Takeable && door == null) continue;
				if (item.ConsiderSolid() && (door == null || door.Locked))
					permanentBlocker = true;
			}
			// The native answer handles safely openable doors. A false answer caused solely by a
			// resident or dropped item is deliberately overridden by the proof above.
			bool passable = Cell.IsPassable(null, false);
			if (!passable) return !permanentBlocker;
			return Doorway || Cell.NavigationWeight(null, Smart: true,
				IgnoreCreatures: true) <= MaxUsableNavigationWeight;
		}

		internal static KingdomAdoptRules.CellObservation ReadCellObservation(
			Zone Z, int X, int Y)
		{
			return CellObservationAt(Z, X, Y);
		}

		internal static KingdomAdoptRules.EnclosureMeasurement MeasureExactRoom(
			Zone Z, int X, int Y)
		{
			return KingdomAdoptRules.MeasureExactEnclosure(X, Y,
				(a, b) => CellObservationAt(Z, a, b));
		}

		/// <summary>Composes the player-facing sentence for a refusal, in the settlement's own
		/// voice. Never called for <see cref="KingdomAdoptRules.AdoptionVerdict.Adopted"/>.</summary>
		private static string RefusalMessage(KingdomAdoptRules.AdoptionVerdict Verdict, KingdomRules.BuildEntry Entry, GameObject Candidate, KingdomSystem System)
		{
			string name = Entry.Name;
			switch (Verdict)
			{
			case KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing:
				return ((Candidate != null) ? Candidate.ShortDisplayName : "Something here") + " already stands for " + KingdomPresentation.Rich(System.SeatName) + ". Release it before adopting it again.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage:
				return KingdomPresentation.Rich(System.SeatName) + " hasn't grown enough yet to keep a " + name + " standing. Come back once it has.";
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
