using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomAdopt
	{
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
				return ((Candidate != null) ? Candidate.ShortDisplayName : "Something here") + " already stands for " + KingdomPresentation.Rich(System.SeatName) + ". Release it before adopting it again.";
			case KingdomAdoptRules.AdoptionVerdict.RefusedBelowStage:
				return KingdomPresentation.Rich(System.SeatName) + " hasn't grown enough yet to keep a " + name + " standing. Come back once it has.";
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
