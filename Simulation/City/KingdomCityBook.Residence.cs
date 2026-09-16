using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		private static bool ValidResidence(string Wire)
			=> Wire != null && (Wire.Length == 0 || KingdomResidenceRules.TryDecode(Wire, out _));

		private bool ValidResidenceColumns()
		{
			if (ResidentIds == null || ResidentResidences == null
				|| ResidentResidences.Count != ResidentIds.Count) return false;
			HashSet<string> beds = null;
			for (int i = 0; i < ResidentResidences.Count; i++)
			{
				string wire = ResidentResidences[i];
				if (wire == "") continue;
				if (!KingdomResidenceRules.TryDecode(wire, out KingdomResidence home)) return false;
				if (home.BedId.Length == 0) continue;
				if (ResidentStandings == null || i >= ResidentStandings.Count) return false;
				if (ResidentStandings[i] != (int)KingdomResidentStanding.Dead
					&& !KingdomResidenceRules.TryReserveBed(home, ref beds)) return false;
			}
			return true;
		}

		internal bool TryMigrateResidenceStorage()
		{
			if (SchemaVersion < 1 || SchemaVersion > KingdomCityRules.SchemaVersion
				|| ResidentIds == null || ResidentIds.Count > KingdomCityState.MaxResidents) return false;
			if (SchemaVersion < 5)
			{
				if (ResidentResidences != null && ResidentResidences.Count != 0) return false;
				ResidentResidences = new List<string>();
				for (int i = 0; i < ResidentIds.Count; i++) ResidentResidences.Add("");
				return true;
			}
			return ValidResidenceColumns();
		}
	}
}
