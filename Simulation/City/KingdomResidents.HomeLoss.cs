using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		internal static bool TryRecordHomeLoss(KingdomSystem system, Zone ground, GameObject home,
			long atTick, out int recorded)
		{
			recorded = 0;
			KingdomCityBook book = ResidenceBook(system, ground);
			if (book == null || !GameObject.Validate(home) || home.CurrentZone != ground
				|| !book.HasValidSubsidenceStorage() || !book.TryReadExact(out KingdomCityState state, out _)) return false;
			string plot = home.GetStringProperty(KingdomPlots.PlotIdProperty);
			int homeId = KingdomCityRules.StableId(home.IDIfAssigned);
			if (string.IsNullOrEmpty(plot) || homeId == 0 || atTick < 0) return false;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				if (!state.TryResident(i, out KingdomResidentRow row)) return false;
				if (row.Standing != KingdomResidentStanding.Resident || string.IsNullOrEmpty(row.Name)
					|| !KingdomResidenceRules.TryDecode(row.Residence, out KingdomResidence residence)
					|| !KingdomResidenceRules.SameHome(residence, ground.ZoneID, plot)) continue;
				if (row.HomeWorkId != homeId) return false;
				if (row.RoofBrink.Stands) continue;
				if (!book.TryWriteBrink(row.ResidentId, BrinkKind.Roof, true, atTick,
					KingdomBrinkRules.Unwarned, null, 0)) return false;
				recorded++;
			}
			return true;
		}
	}
}
