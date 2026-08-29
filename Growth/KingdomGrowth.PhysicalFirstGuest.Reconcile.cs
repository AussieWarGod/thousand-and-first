using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryReconcilePhysicalFirstGuest(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomGrowthArrivalCandidate candidate, long tick,
			bool allowConsumption, out ArrivalResult result)
		{
			result = ArrivalResult.Failed;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (x?.RulesVersion != 2) return false;
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed
				&& x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Preparing)
			{
				string failure = null;
				if (!TryExactPhysicalFirstGuestEscrow(candidate, out GameObject escrowed)
					|| !TryHardenPhysicalFirstGuest(escrowed, candidate, out failure)
					|| !KingdomLifecycleRules.TryHostGrowthFirstGuest(growth, candidate, tick))
				{
					KingdomLog.Log("first guest hosting remains pending: "
						+ (failure ?? "durable host transition refused"));
					result = ArrivalResult.Deferred; return true;
				}
			}
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed
				&& x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared)
			{
				if (!TryExactPhysicalFirstGuestEscrow(candidate, out GameObject citizen))
				{
					result = ArrivalResult.Deferred; return true;
				}
				r_KingdomFirstGuestBody part = citizen.GetPart<r_KingdomFirstGuestBody>();
				if (part != null && !RestorePhysicalFirstGuest(citizen, candidate,
					out string restoreFailure))
				{
					KingdomLog.Log("first guest citizenship restore retained: " + restoreFailure);
					result = ArrivalResult.Deferred; return true;
				}
				return false;
			}
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted)
			{
				if (!TryProjectPhysicalFirstGuest(candidate, zone, out GameObject hosted,
					out string projectionFailure))
				{
					KingdomLog.Log("first guest remains in escrow: " + projectionFailure);
					result = ArrivalResult.Deferred; return true;
				}
				ContinueCommittedPhysicalFirstGuestAction(system, zone, candidate, hosted);
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed
					&& x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared)
					return TryReconcilePhysicalFirstGuest(system, zone, survey, candidate, tick,
						allowConsumption, out result);
				result = ArrivalResult.Deferred; return true;
			}
			if (candidate.Phase != KingdomGrowthArrivalCandidatePhase.GuestTerminal)
				return false;
			if (!ReleaseFirstGuestBodyAfterCitizenship(system, growth, candidate, tick,
				out string releaseFailure))
			{
				KingdomLog.Log("terminal first guest retained body capacity: " + releaseFailure);
				result = ArrivalResult.Deferred; return true;
			}
			if (growth.ArrivalOp == null && (!allowConsumption || growth.WorkPaused))
			{
				result = ArrivalResult.Deferred; return true;
			}
			if (growth.ArrivalOp == null && !PrepareDepartedFirstGuestOperation(system,
				candidate, tick))
				return true;
			result = CompleteArrivalOperation(system, zone, survey, growth.ArrivalOp,
				candidate, tick, out ArrivalRefusal _);
			return true;
		}

		private static void ContinueCommittedPhysicalFirstGuestAction(KingdomSystem system,
			Zone zone, KingdomGrowthArrivalCandidate candidate, GameObject body)
		{
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			long tick = The.Game?.TimeTicks ?? -1L;
			if (growth == null || x == null || tick < 0L) return;
			if (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent)
			{
				string failure = null;
				if (!TryRetractPhysicalFirstGuest(candidate, zone, out body, out failure)
					|| !KingdomLifecycleRules.TryPrepareGrowthFirstGuestCitizenship(growth,
						candidate, tick))
				{
					KingdomLog.Log("first guest welcome recovery retained: "
						+ (failure ?? "citizenship transition refused")); return;
				}
				if (!RestorePhysicalFirstGuest(body, candidate, out failure))
					KingdomLog.Log("first guest welcome restore retained: " + failure);
			}
			else if (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.DepartureIntent
				&& TryExactLoadedPhysicalFirstGuest(candidate, zone, out GameObject exact)
				&& ReferenceEquals(exact, body))
			{
				r_KingdomFirstGuestBody part = body.GetPart<r_KingdomFirstGuestBody>();
				if (!ExactPhysicalFirstGuestHardening(body, candidate, part)) return;
				part.AuthorizedDeparture = true;
				try { body.Obliterate(null, Silent: true); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(zone, body); }
			}
		}

		public static bool ObservePhysicalFirstGuestRemoval(GameObject body,
			string candidateId, string opportunityId, bool authorizedDeparture)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			KingdomGrowthArrivalCandidate candidate = growth?.ArrivalCandidate;
			Zone zone = body?.CurrentZone; long tick = The.Game?.TimeTicks ?? -1L;
			if (candidate?.Id != candidateId || candidate.FirstGuest?.OpportunityId != opportunityId
				|| tick < 0L || !TryExactLoadedPhysicalFirstGuest(candidate, zone,
					out GameObject exact) || !ReferenceEquals(exact, body)) return false;
			r_KingdomFirstGuestBody part = body.GetPart<r_KingdomFirstGuestBody>();
			if (!ExactPhysicalFirstGuestHardening(body, candidate, part)
				|| authorizedDeparture != part.AuthorizedDeparture) return false;
			KingdomGrowthFirstGuestTerminalState terminal = authorizedDeparture
				? KingdomGrowthFirstGuestTerminalState.Departed
				: KingdomGrowthFirstGuestTerminalState.Died;
			if (!KingdomLifecycleRules.TryObserveGrowthFirstGuestTerminal(growth, candidate,
				body.IDIfAssigned, body.GetStringProperty(ArrivalMarkerProperty), zone.ZoneID,
				terminal, tick)) return false;
			if (!ReleaseFirstGuestBodyAfterCitizenship(system, growth, candidate, tick,
				out string failure)) KingdomLog.Log("observed first-guest terminal retained capacity: "
					+ failure);
			return true;
		}
	}
}
