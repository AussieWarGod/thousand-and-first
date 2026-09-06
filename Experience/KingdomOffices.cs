using System;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Exact citizen-death intake and attended reconciliation for civic office and
	/// remembrance authority. It never appoints an oldest resident or binds a death to a cairn
	/// automatically; both optional projections require an explicit Charter action.</summary>
	public static partial class KingdomOffices
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionMemory") != "No";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			KingdomMaterials.HeadedProbe = KingdomReach.IsHeaded;
			if (System == null || !System.Founded || Z == null || Survey == null
				|| !System.OwnedZone(Z.ZoneID)) return;
			TagCitizens(System, Survey);
			if (!KingdomOfficeRuntime.TryReconcile(System, Z, Survey, out string officeFailure))
				KingdomLog.Log("office: exact reconciliation waits ("
					+ (officeFailure ?? "unknown failure") + ")");
			if (!KingdomRemembranceRuntime.TryReconcile(System, Z, Survey,
				out string remembranceFailure))
				KingdomLog.Log("remembrance: exact reconciliation waits ("
					+ (remembranceFailure ?? "unknown failure") + ")");
		}

		/// <summary>The engine death callback is the only authority that creates a terminal resident
		/// row. Optional remembrance reads that row later; it never promotes cache absence to death.</summary>
		public static void RecordDeath(GameObject Citizen, GameObject Killer)
		{
			KingdomSystem.Guard("citizen death", delegate
			{
				if (Citizen == null || The.Game == null) return;
				KingdomSystem system = The.Game.GetSystem<KingdomSystem>();
				if (system == null || !system.Founded) return;
				KingdomOfficeRules.DeathCause cause = KingdomOfficeRules.ClassifyCause(
					KillerIsPlayer: Killer != null && Killer.IsPlayer(),
					KillerIsRaider: Killer != null && Killer.GetIntProperty("KingdomRaider") == 1,
					KillerKnown: Killer != null);
				KingdomResidentDeathRuntime.Record(system, Citizen,
					(Simulation.City.KingdomStandingCause)((int)cause + (int)Simulation.City.KingdomStandingCause.Unwitnessed));
			});
		}

		private static void TagCitizens(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.CitizenBodies.Count; i++)
			{
				GameObject item = Survey.CitizenBodies[i];
				if (item.GetIntProperty("KingdomBorn") == 1)
					item.RequirePart<r_KingdomCitizenLegacy>();
				if (item.GetPart<r_KingdomCitizenship>() == null)
					KingdomCitizenship.ObserveLegacy(System, item, out string _);
			}
		}
	}
}
