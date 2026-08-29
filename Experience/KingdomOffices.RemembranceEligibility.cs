using ThousandAndFirst.Simulation.City;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomOffices
	{
		private sealed class RemembranceWitnessContext
		{
			internal KingdomCityBook Book;
			internal int ResidentId;
			internal string SettlementId;
			internal string SettlementName;
		}

		/// <summary>Freezes the resident's exact owning city before the terminal transition
		/// retires its live binding. No zone is loaded and no death is inferred from absence.</summary>
		private static bool TryCaptureRemembranceWitness(KingdomSystem System,
			GameObject Citizen, out RemembranceWitnessContext Context, out string Failure)
		{
			Context = null; Failure = null;
			KingdomCityBook book; int residentId;
			if (!KingdomResidents.TryLocate(System, Citizen, out book, out residentId)
				&& !KingdomResidents.TryEnsureRow(System, Citizen, out book, out residentId))
			{
				Failure = "the dying citizen has no exact owned resident row"; return false;
			}
			if (!System.TryFindSettlement(book, out bool seated, out KingdomSettlement settlement))
			{
				Failure = "the dying citizen's exact city is not owned"; return false;
			}
			string settlementId = book.SettlementId;
			string settlementName = seated ? System.SeatName : settlement?.SettlementName;
			if (residentId <= 0 || string.IsNullOrEmpty(settlementId)
				|| string.IsNullOrEmpty(settlementName))
			{
				Failure = "the remembrance witness identity is incomplete"; return false;
			}
			Context = new RemembranceWitnessContext { Book = book, ResidentId = residentId,
				SettlementId = settlementId, SettlementName = settlementName };
			return true;
		}

		/// <summary>Publishes at most one permanent remembrance opportunity per city. Existing
		/// terminal civic rows consume that city's bounded opportunity without being overwritten.</summary>
		private static bool TryRecordRemembranceEligibility(KingdomSystem System,
			RemembranceWitnessContext Context, KingdomResidentRow Former, long Tick,
			out string Failure)
		{
			Failure = null;
			if (System?.Experience == null || Context == null
				|| Context.ResidentId != Former.ResidentId)
			{
				Failure = "the exact remembrance witness carrier is absent"; return false;
			}
			if (!KingdomExperienceRules.TryGetRemembrance(System.Experience,
				Context.SettlementId, out KingdomRemembranceReceipt existing, out Failure))
				return false;
			if (existing != null) return true;
			return KingdomExperienceRules.TryCreateRemembranceEligibility(System.Experience,
				System.Experience.Revision, Context.SettlementId, Context.SettlementName,
				Former.ResidentId, Former.Name, Tick, out Failure);
		}

		private static void ReportRemembranceWitnessFailure(string Failure)
		{
			string detail = Failure ?? "unknown exact-authority failure";
			KingdomLog.Log("remembrance: directly witnessed eligibility waits (" + detail + ")");
			MessageQueue.AddPlayerMessage("{{R|The death was recorded, but its optional remembrance "
				+ "receipt needs exact recovery: " + detail + ".}} ");
		}
	}
}
