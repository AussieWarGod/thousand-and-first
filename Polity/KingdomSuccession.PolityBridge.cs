using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		/// <summary>Reads one exact groomed successor without copying resident authority.</summary>
		internal bool TryPolitySuccessorBridge(KingdomSystem System, out int ResidentId,
			out string SettlementId, out string Name, out int Revision, out bool Present,
			out string Failure)
		{
			ResidentId = 0; SettlementId = null; Name = null; Revision = 0;
			Present = false; Failure = null;
			if (!TryGetCurrentConfiguration(System,
				out KingdomSuccessionConfiguration config, out Failure)) return false;
			Revision = config.Revision;
			if (config.Choice != HeirChoice.Groomed) return true;
			if (!TryReadRealmGrooming(System, out KingdomGroomingRecord grooming,
				out bool stored, out Failure) || !stored ||
				grooming.ResidentId != config.ChosenResidentId)
			{
				Failure = Failure ?? "groomed successor authority is incomplete"; return false;
			}
			if (!TryReadHeirs(System, out List<HeirRuntime> heirs))
			{
				Failure = "resident authority could not be read for polity bridge"; return false;
			}
			if (!TryUniqueHeir(heirs, grooming.ResidentId, true, out HeirRuntime nominee))
				return true;
			if (!TryFindResidentSettlement(System, grooming.ResidentId,
				nominee.Rule.Name, out SettlementId, out Failure)) return false;
			ResidentId = grooming.ResidentId; Name = nominee.Rule.Name; Present = true;
			return true;
		}

		private static bool TryFindResidentSettlement(KingdomSystem System, int ResidentId,
			string Name, out string SettlementId, out string Failure)
		{
			SettlementId = null; Failure = null; int matches = 0;
			if (!FindInBook(System.City, ResidentId, Name, ref matches,
				ref SettlementId, out Failure)) return false;
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; i < others.Count; i++)
				if (!FindInBook(others[i]?.City, ResidentId, Name, ref matches,
					ref SettlementId, out Failure)) return false;
			if (matches != 1)
			{
				Failure = "groomed successor does not bind one exact resident settlement"; return false;
			}
			return true;
		}

		private static bool FindInBook(KingdomCityBook Book, int ResidentId, string Name,
			ref int Matches, ref string SettlementId, out string Failure)
		{
			Failure = null; if (Book == null) return true;
			if (!Book.TryRead(out KingdomCityState state, out KingdomCityFault fault))
			{
				Failure = "resident book is unreadable for polity bridge: " + fault; return false;
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				if (!state.TryResident(i, out KingdomResidentRow row) ||
					row.ResidentId != ResidentId) continue;
				if (row.Name != Name || row.Standing != KingdomResidentStanding.Resident)
				{
					Failure = "groomed successor resident evidence changed"; return false;
				}
				Matches++; SettlementId = state.SettlementId;
			}
			return true;
		}
	}
}
