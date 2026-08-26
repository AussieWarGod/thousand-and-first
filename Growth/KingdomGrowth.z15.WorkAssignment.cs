using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		/// <summary>
		/// Crews the settlement's works from its citizens, in placement order. A work without
		/// its crew is idle, not broken: it keeps its charge and its contents and simply does
		/// not run, and the settlement says which works want hands.
		/// </summary>
		public static void AssignWork(KingdomSystem System, KingdomSurvey Survey)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return;
			int[] demands = new int[Survey.Works.Count];
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				demands[i] = Survey.Works[i].GetIntProperty("KingdomStaffNeeded");
			}
			// Resident rows choose who may labour. Survey bodies are only execution endpoints;
			// a body absent from the authoritative roll cannot acquire a post.
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
			List<GameObject> available = new List<GameObject>();
			for (int i = 0; i < labour.Count; i++)
				if (grounded.TryGetValue(labour[i].ResidentId, out GameObject settler))
					available.Add(settler);
			// Water hands are spent before works: one resident cannot carry and mill at once.
			int forWorks = Math.Max(0, available.Count - System.WaterCrew);
			// Addendum 7: capability-aware, ablest-first, deterministic (KingdomCrewRules /
			// KingdomCrews). The pool is exactly the forWorks-many settlers hands-spent-once has
			// left for these works; who is capable of what is read off them, never assigned by the
			// founder. Threshold manning is read per work inside AssignWorks, off the same
			// KingdomThresholdManning property the old int[] path passed along beside it.
			KingdomCrewRules.SettlerCapability[] pool = KingdomCrews.CapabilitiesOf(available,
				forWorks);
			KingdomCrewRules.CrewOutcome[] outcomes = KingdomCrews.AssignWorks(Survey.Works, pool,
				available);
			int idle = 0;
			int shorthanded = 0;
			// LIVING-CITY-ARCHITECTURE §3.2(b) needs a settler's day to be a fact about the PERSON,
			// and until this wave crewing was only ever a fact about the work: every resident row
			// read JobWorkId = 0 and every day shape derived honestly, and uselessly, to the hearth.
			// The stamp is cleared for everybody first so that a settler taken off a mill this pass
			// does not keep walking to it.
			for (int i = 0; i < available.Count; i++)
			{
				Simulation.City.KingdomStations.Post(available[i], 0,
					Simulation.City.KingdomWorkKind.Other);
			}
			for (int j = 0; j < Survey.Works.Count; j++)
			{
				GameObject work = Survey.Works[j];
				KingdomCrewRules.CrewOutcome outcome = outcomes[j];
				// The pool is CapabilitiesOf(Survey.Settlers, forWorks), built index-for-index off
				// the survey's own list, so an outcome's SettlerIndices name settlers directly.
				int postId = Simulation.City.KingdomCityRules.StableId(work.ID);
				Simulation.City.KingdomWorkKind postKind = Simulation.City.KingdomStations.KindOf(work);
				for (int k = 0; outcome.SettlerIndices != null && k < outcome.SettlerIndices.Length; k++)
				{
					int at = outcome.SettlerIndices[k];
					if (at >= 0 && at < available.Count)
					{
						Simulation.City.KingdomStations.Post(available[at], postId, postKind);
					}
				}
				int headcountEffectiveness = KingdomRules.CrewEffectiveness(outcome.Assigned, demands[j]);
				int capabilityEffectiveness = KingdomCrewRules.CapabilityEffectiveness(outcome.BestCapability, outcome.CapabilityThreshold);
				int effectiveness = KingdomCrewRules.CombinedEffectiveness(headcountEffectiveness, capabilityEffectiveness);
				work.SetIntProperty("KingdomStaffed", (effectiveness > 0) ? 1 : 0);
				work.SetIntProperty("KingdomEffectiveness", effectiveness);
				work.SetIntProperty(KingdomCrews.IdentityAffinityProperty,
					outcome.IdentityAffinity);
				if (effectiveness <= 0)
				{
					idle++;
				}
				else
				{
					if (effectiveness < 100)
					{
						shorthanded++;
					}
					// STANDARDS 7b: a capability shortfall is named once, and unsaid the moment a
					// later pass draws a crew that meets it.
					if (outcome.CapabilityThreshold > 0 && capabilityEffectiveness < 100)
					{
						KingdomCrews.AnnounceShortfall(work, work.ShortDisplayName, outcome.CapabilityKind, outcome.BestCapability, outcome.CapabilityThreshold);
					}
					else
					{
						KingdomCrews.ClearShortfall(work);
					}
					if (work.GetIntProperty("KingdomHandCranked") == 1)
					{
						Capacitor capacitor = work.GetPart<Capacitor>();
						if (capacitor != null)
						{
							int target = capacitor.MaxCharge * effectiveness / 100;
							if (capacitor.Charge < target)
							{
								capacitor.Charge = target;
							}
						}
					}
				}
			}
			System.ShorthandedWorks = shorthanded;
			System.IdleWorks = idle;
			// Hands are spent once. Whatever is crewing a work this pass is not available to walk
			// to the water next pass, which is what turns staffing into a real choice rather than
			// a free bonus. ConstructionPresence then draws one real, named raising gang from the
			// bodies whose posts remain empty and adds that gang to the same spent-hands mirror.
			int crewed = 0;
			for (int i = 0; i < outcomes.Length; i++)
			{
				crewed += outcomes[i].Assigned;
			}
			System.AssignedCrew = crewed + System.WaterCrew;
			KingdomConstructionPresence.Assign(System, Survey);
			if (idle > 0 && !System.IdleWorksAnnounced)
			{
				System.IdleWorksAnnounced = true;
				MessageQueue.AddPlayerMessage("{{r|" + idle + " of the works of " + KingdomPresentation.Rich(System.KingdomDisplayName) + " stand idle for want of hands.}}");
			}
			else if (idle == 0)
			{
				System.IdleWorksAnnounced = false;
			}
		}
	}
}
