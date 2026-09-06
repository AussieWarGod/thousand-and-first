using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		internal static bool TryAssociate(KingdomSystem system, string stepId,
			KingdomResidentDepartureOperation departure, out string failure)
		{
			failure = "subsidence departure association is not exact";
			if (!TryMatch(system, departure, out Snapshot owner, out bool associated)) return false;
			if (stepId == null)
			{
				if (associated) return false;
				failure = null; return true;
			}
			if (owner.Step.Active?.Id != stepId
				|| !KingdomSubsidenceStepRules.TryAssociate(owner.Step, departure,
					out KingdomSubsidenceStepBook next) || !Publish(system, owner, next)) return false;
			failure = null; return true;
		}

		internal static bool TryCredit(KingdomSystem system, GameObject body,
			KingdomResidentDepartureOperation departure, out string failure)
		{
			failure = "subsidence departure credit lacks exact removed carriers";
			if (!TryMatch(system, departure, out Snapshot owner, out bool associated)) return false;
			if (!associated) { failure = null; return true; }
			if (!ReferenceEquals(system.ResidentDeparture, departure) || !GameObject.Validate(body)
				|| body.IDIfAssigned != departure.BodyObjectId || body.CurrentZone?.ZoneID != departure.ZoneId
				|| body.GetIntProperty(KingdomResidents.ResidentIdProperty) != departure.ResidentId
				|| body.GetPart<r_KingdomResidentDeparture>()?.Matches(departure, body) != true
				|| !KingdomResidentDepartureRuntime.ExactRemovedCitizenship(system, body, departure)
				|| !KingdomResidents.DepartureCarriersAbsent(system, owner.City, departure.ResidentId)
				|| !owner.City.TryReadExact(out KingdomCityState state, out KingdomCityFault _)
				|| !KingdomResidentRules.TryProject(state, out KingdomResidentRollProjection roll)) return false;
			KingdomSubsidenceStepOperation active = owner.Step.Active;
			GrowthStage reached = active.ReachedStage;
			if (!active.PendingCredited)
			{
				GrowthStage measured = KingdomSubsidenceRules.StageWithHysteresis(
					active.FromStage, roll.Population, active.StorageCapacity);
				if (measured < reached) reached = measured;
			}
			if (!KingdomSubsidenceStepRules.TryCredit(owner.Step, departure.OperationId, reached,
				out KingdomSubsidenceStepBook next) || !Publish(system, owner, next)) return false;
			failure = null; return true;
		}

		internal static bool CanRetire(KingdomSystem system, KingdomResidentDepartureOperation departure,
			bool rolledBack)
		{
			return TryMatch(system, departure, out Snapshot owner, out bool associated)
				&& (!associated || owner.Step.Active.PendingCredited != rolledBack);
		}

		internal static bool TryRetireJournal(KingdomSystem system,
			KingdomResidentDepartureOperation departure, bool rolledBack, out string failure)
		{
			failure = "departure cannot retire before its exact subsidence acknowledgement";
			if (!ReferenceEquals(system?.ResidentDeparture, departure)
				|| !TryMatch(system, departure, out Snapshot owner, out bool associated)
				|| associated && owner.Step.Active.PendingCredited == rolledBack
				|| !rolledBack && !KingdomResidentDepartureRuntime.ExactTerminalAbsence(system, departure)) return false;
			system.ResidentDeparture = KingdomResidentDepartureRules.Empty();
			if (associated)
			{
				KingdomSubsidenceStepBook next;
				bool released = rolledBack
					? KingdomSubsidenceStepRules.TryReleaseRolledBack(owner.Step, departure.OperationId, out next)
					: KingdomSubsidenceStepRules.TryReleaseRetired(owner.Step, departure.OperationId, out next);
				if (!released || !Publish(system, owner, next)) return false;
			}
			failure = null; return true;
		}

		internal static bool TryRecoverOrphan(KingdomSystem system, Zone zone, out string failure)
		{
			failure = "subsidence association requires its exact journal or restored resident";
			if (!KingdomResidentDepartureRules.IsEmpty(system?.ResidentDeparture)
				|| !TryReadOwned(system, out List<Snapshot> books)) return false;
			foreach (Snapshot owner in books)
			{
				KingdomSubsidenceStepOperation active = owner.Step.Active;
				if (active == null || active.PendingDepartureId == "") continue;
				if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> objects)
					|| objects == null) return false;
				GameObject body = null; int matches = 0;
				foreach (GameObject item in objects)
				{
					if (!GameObject.Validate(item)) continue;
					r_KingdomResidentDeparture marker = item.GetPart<r_KingdomResidentDeparture>();
					if (marker != null && (marker.RealmId == owner.Step.RealmId
						|| marker.OperationId == active.PendingDepartureId)) return false;
					if (item.IDIfAssigned != active.PendingIdentity.BodyObjectId) continue;
					body = item; matches++;
				}
				KingdomSubsidenceStepBook next;
				if (active.PendingCredited)
				{
					if (matches != 0 || !KingdomResidents.DepartureCarriersAbsent(system, owner.City,
						active.PendingIdentity.ResidentId) || !KingdomSubsidenceStepRules.TryReleaseRetired(owner.Step,
						active.PendingDepartureId, out next)) return false;
				}
				else
				{
					KingdomSubsidenceDepartureIdentity held = active.PendingIdentity;
					if (zone?.ZoneID != held.ZoneId
						|| system.SettlementIdForOwnedZone(held.ZoneId) != owner.City.SettlementId) return false;
					if (matches != 1 || body.CurrentZone?.ZoneID != held.ZoneId
						|| body.GetPart<r_KingdomResidentDeparture>() != null
						|| !KingdomCitizenship.BelongsTo(system, body)
						|| !KingdomResidentTransitionAuthority.CanPrepareResidentBodyDestruction(
							system, body, held.ResidentId, default(KingdomResidentDestructionAuthorization))
						|| !KingdomSubsidenceStepRules.TryReleaseRolledBack(owner.Step,
							active.PendingDepartureId, out next)) return false;
				}
				if (!Publish(system, owner, next)) return false;
			}
			failure = null; return true;
		}
	}
}
