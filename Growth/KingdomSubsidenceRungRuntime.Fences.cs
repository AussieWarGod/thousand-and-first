using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceRungRuntime
	{
		internal static bool BlocksRoof(GameObject subject)
		{
			if (!GameObject.Validate(subject)) return true;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded) return false;
			int id = KingdomResidents.IdOf(subject);
			foreach (KingdomCityBook book in system.OwnedCityBooks())
				if (book == null || !book.HasValidSubsidenceStorage()
					|| KingdomSubsidenceRungRules.BlocksRoof(book.SubsidenceModel, id)) return true;
			return false;
		}

		/// <summary>An open part receipt or frozen parent obligation excludes competing writers.</summary>
		internal static bool BlocksWork(GameObject work)
		{
			if (!GameObject.Validate(work)) return true;
			r_KingdomWear wear = work.GetPart<r_KingdomWear>();
			if (wear != null && wear.IncidentCause == (int)KingdomWearRules.WearCause.Subsidence
				&& wear.IncidentPhase != (int)KingdomWearIncidentPhase.None) return true;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded) return false;
			foreach (KingdomCityBook book in system.OwnedCityBooks())
				if (book == null || !book.HasValidSubsidenceStorage()
					|| KingdomSubsidenceRungRules.BlocksWork(book.SubsidenceModel, work.IDIfAssigned)) return true;
			return false;
		}
	}
}
