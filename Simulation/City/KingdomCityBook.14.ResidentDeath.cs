using System;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		internal bool TryPublishWitnessedDeath(KingdomResidentDeathReceipt receipt, string stepWire,
			Func<bool> exactReceipt)
		{
			if (receipt == null || exactReceipt == null || receipt.Settlement != SettlementId
				|| !KingdomResidentDeathRules.Valid(receipt) || receipt.Phase > KingdomResidentDeathPhase.StandingWritten
				|| SubsidenceModel != stepWire || receipt.StepWire != stepWire || !HasValidSubsidenceStorage() || !TryReadExact(out var state, out _)
				|| !state.TryResidentIndex(receipt.Before.ResidentId, out int index) || !state.TryResident(index, out var row)) return false;
			int cut = KingdomResidentDeathRules.RowCut(receipt, row);
			if (cut < 0 || receipt.Phase == KingdomResidentDeathPhase.StandingWritten && cut != 1) return false;
			var ids = ResidentIds; var standings = ResidentStandings; var causes = ResidentCauses;
			if (!exactReceipt() || SubsidenceModel != stepWire || !ReferenceEquals(ids, ResidentIds)
				|| !ReferenceEquals(standings, ResidentStandings) || !ReferenceEquals(causes, ResidentCauses)
				|| !TryReadExact(out var current, out _) || !current.TryResident(index, out var exact)
				|| KingdomResidentDeathCodec.Row(exact) != KingdomResidentDeathCodec.Row(row)) return false;
			// All indices and carriers are proved. No allocation or callback separates these writes.
			if (cut == 0) { standings[index] = (int)KingdomResidentStanding.Dead; causes[index] = (int)receipt.Cause; }
			return SubsidenceModel == stepWire && exactReceipt() && ReferenceEquals(ids, ResidentIds)
				&& ReferenceEquals(standings, ResidentStandings) && ReferenceEquals(causes, ResidentCauses)
				&& TryReadExact(out var after, out _) && after.TryResident(index, out var dead)
				&& KingdomResidentDeathRules.RowCut(receipt, dead) == 1;
		}
	}
}
