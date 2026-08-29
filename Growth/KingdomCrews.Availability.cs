using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomCrews
	{
		/// <summary>Grounded, unstaged labour bodies in authoritative resident-roll order.</summary>
		public static List<GameObject> AvailableSettlers(KingdomSystem System, KingdomSurvey Survey)
		{
			List<GameObject> available = new List<GameObject>();
			if (System == null || Survey == null) return available;
			Dictionary<int, GameObject> grounded = new Dictionary<int, GameObject>();
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				int residentId = Simulation.City.KingdomResidents.IdOf(settler);
				if (residentId > 0 && !Simulation.City.KingdomPhysicalHappenings.IsStaged(settler)
					&& !grounded.ContainsKey(residentId)) grounded.Add(residentId, settler);
			}
			List<Simulation.City.KingdomResidentRow> labour =
				Simulation.City.KingdomResidents.RollRows(System, true);
			for (int i = 0; i < labour.Count; i++)
				if (grounded.TryGetValue(labour[i].ResidentId, out GameObject settler))
					available.Add(settler);
			return available;
		}

		/// <summary>Prefix length left after the water detail spends hands once.</summary>
		public static int WorkHandCount(KingdomSystem System, IList<GameObject> Available)
		{
			int count = Available != null ? Available.Count : 0;
			return Math.Max(0, count - (System != null ? Math.Max(0, System.WaterCrew) : 0));
		}
	}
}
