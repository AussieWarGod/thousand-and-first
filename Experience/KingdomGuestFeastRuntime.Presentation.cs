#if !TAF_TESTS
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestFeastRuntime
	{
		internal static string CharterLabel(KingdomSystem system)
		{
			return "{{W|Review the Guest's Feast record}}" +
				(KingdomMaster.NewWorkAllowed(system) ? ""
					: " {{K|(simulation paused; read-only)}}");
		}

		internal static void OpenRecord(KingdomSystem system, GameObject founder)
		{
			if (!KingdomFirstFeastRuntime.TryCurrentCity(system, founder,
				out KingdomFirstFeastRuntime.CityContext context, out string failure))
			{
				Popup.Show(failure); return;
			}
			if (!TryDescribe(system, context.SettlementId, out string description))
				description = "No optional Guest's Feast presentation record exists for this city. "
					+ "Growth remains complete and authoritative.";
			if (!KingdomMaster.NewWorkAllowed(system))
				description = "Settlement simulation is paused; this record is read-only.\n\n"
					+ description;
			Popup.Show(description);
		}

		internal static bool TryDescribe(KingdomSystem system, string settlementId,
			out string description)
		{
			description = null;
			if (!TryRead(system, out KingdomGuestFeastBook book, out string _)
				|| !book.IdentityBound || !KingdomGuestFeastRules.TryFind(book, settlementId,
					out KingdomGuestFeastReceipt row) || row == null) return false;
			switch (row.Phase)
			{
			case KingdomGuestFeastPhase.AwaitingGuestChoice:
				description = "Optional Guest's Feast presentation is active; Growth still owns the choice.";
				break;
			case KingdomGuestFeastPhase.AwaitingGuestResult:
				description = "Growth owns the decision. Its exact terminal person/result/outbox receipt "
					+ "has not yet been copied into this optional record; retry remains safe.";
				break;
			case KingdomGuestFeastPhase.AwaitingPractice:
				description = row.GuestName + " completed Growth admission. An independent founding "
					+ "First Feast practice has not been chosen.";
				break;
			case KingdomGuestFeastPhase.AwaitingLocus:
				description = row.GuestName + " and the independent founding practice are recorded; "
					+ "an exact staffed social locus remains pending.";
				break;
			case KingdomGuestFeastPhase.Cycling:
				description = "The Guest's Feast has accompanied " + row.HomeCycles
					+ " of three proven journeys home" + (row.AwayArmed
						? "; the founder is presently away" : "") + ".";
				break;
			case KingdomGuestFeastPhase.Exhausted:
				description = "After three journeys home, the Guest's Feast is complete and quiet.";
				break;
			case KingdomGuestFeastPhase.GuestDeclined:
				description = "Growth declined " + row.GuestName
					+ "; this optional record is complete without penalty.";
				break;
			case KingdomGuestFeastPhase.GuestCouldNotJoin:
				description = row.GuestName + " could not be housed. Growth closed the arrival "
					+ "without inventing citizenship or a feast.";
				break;
			case KingdomGuestFeastPhase.GuestDeparted:
				description = row.GuestName + " left the settlement or died before citizenship. "
					+ "Growth closed the guest phase without inventing a feast or reward.";
				break;
			case KingdomGuestFeastPhase.PracticeRefused:
				description = "The First Feast practice was refused; this branch is complete.";
				break;
			case KingdomGuestFeastPhase.PracticeArchived:
				description = "The unaccepted First Feast offer was archived when civic stories were "
					+ "disabled; no re-enable backlog remains.";
				break;
			default:
				description = "A historical coordination row is retained without inventing a cycle or reward.";
				break;
			}
			return true;
		}

		internal static bool TryTrace(KingdomSystem system, string settlementId,
			KingdomFirstFeastReceipt practice, out string trace)
		{
			trace = null;
			return TryRead(system, out KingdomGuestFeastBook book, out string _)
				&& book.IdentityBound && KingdomGuestFeastRules.TryTrace(book, settlementId,
					practice, out trace);
		}
	}
}
#endif
