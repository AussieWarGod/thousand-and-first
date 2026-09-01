using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureRuntime
	{
		internal static bool TryRecoverPending(KingdomSystem System, Zone Zone,
			out string Failure)
		{
			Failure = null;
			if (System == null) return false;
			System.ResidentDeparture = KingdomResidentDepartureRules.NormalizeOldDefault(
				System.ResidentDeparture);
			KingdomResidentDepartureOperation operation = System.ResidentDeparture;
			if (KingdomResidentDepartureRules.IsEmpty(operation)) return true;
			if (!KingdomResidentDepartureRules.Valid(operation))
			{
				Failure = "pending resident departure is malformed"; return false;
			}
			if (Zone == null || Zone.ZoneID != operation.ZoneId)
			{
				Failure = "pending resident departure requires its exact source zone";
				return false;
			}
			GameObject body = null; int bodies = 0, markers = 0;
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(
				out IList<GameObject> objects))
			{
				Failure = "loaded-object authority is unavailable for departure recovery";
				return false;
			}
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				GameObject item = objects[i];
				bool live = GameObject.Validate(item);
				r_KingdomResidentDeparture marker =
					item?.GetPart<r_KingdomResidentDeparture>();
				if (live && marker != null && (marker.OperationId == operation.OperationId
					|| marker.RealmId == operation.RealmId))
				{
					markers++;
					if (!marker.Matches(operation, item))
					{
						Failure = "departure found a mixed realm journal marker"; return false;
					}
				}
				if (!live || item.IDIfAssigned != operation.BodyObjectId) continue;
				if (marker != null && !marker.Matches(operation, item))
				{
					Failure = "departure body carries a mixed or foreign journal marker";
					return false;
				}
				body = item; bodies++;
			}
			if (bodies == 0 && markers == 0
				&& operation.Phase == (int)KingdomResidentDeparturePhase.EffectsPublished)
			{
				System.ResidentDeparture = KingdomResidentDepartureRules.Empty(); return true;
			}
			if (bodies != 1 || !GameObject.Validate(body))
			{
				Failure = "departure body identity is absent or non-unique"; return false;
			}
			r_KingdomResidentDeparture exact = body.GetPart<r_KingdomResidentDeparture>();
			if (exact == null && markers == 0
				&& (operation.Phase == (int)KingdomResidentDeparturePhase.Prepared
					|| operation.Phase == (int)KingdomResidentDeparturePhase.RolesPrepared))
				return TryRollbackPrepared(System, body, operation, out Failure,
					RequireMarker: false);
			if (markers != 1 || exact?.Matches(operation, body) != true)
			{
				Failure = "departure marker identity is absent or non-unique"; return false;
			}
			return TryContinue(System, body, out KingdomResidentRow _, out Failure);
		}

		private static bool TryContinue(KingdomSystem System, GameObject Body,
			out KingdomResidentRow FormerRow, out string Failure)
		{
			FormerRow = default(KingdomResidentRow); Failure = null;
			KingdomResidentDepartureOperation operation = System?.ResidentDeparture;
			if (!KingdomResidentDepartureRules.Valid(operation)
				|| Body?.GetPart<r_KingdomResidentDeparture>()?.Matches(operation, Body) != true)
				return false;
			if (operation.Phase == (int)KingdomResidentDeparturePhase.Prepared
				|| operation.Phase == (int)KingdomResidentDeparturePhase.RolesPrepared)
			{
				if (KingdomCitizenship.BelongsTo(System, Body))
					return TryRollbackPrepared(System, Body, operation, out Failure);
				if (operation.Phase != (int)KingdomResidentDeparturePhase.RolesPrepared
					|| !ExactRemovedCitizenship(System, Body, operation)
					|| !KingdomResidentDepartureRules.Advance(operation,
						KingdomResidentDeparturePhase.RolesPrepared,
						KingdomResidentDeparturePhase.CitizenshipRemoved)) return false;
			}
			if (!ExactRemovedCitizenship(System, Body, operation)) return false;
			KingdomResidentDestructionAuthorization authorization = AuthorizationOf(operation);
			if (operation.Phase == (int)KingdomResidentDeparturePhase.CitizenshipRemoved)
			{
				if (!KingdomResidents.TryCompleteDepartureCarriers(System, Body, operation,
					authorization, out FormerRow, out Failure)
					|| !KingdomResidentDepartureRules.Advance(operation,
						KingdomResidentDeparturePhase.CitizenshipRemoved,
						KingdomResidentDeparturePhase.CarriersRemoved)) return false;
			}
			if (operation.Phase == (int)KingdomResidentDeparturePhase.CarriersRemoved)
			{
				if (!TryCloseRoles(System, Body, operation, authorization, out Failure)
					|| !KingdomResidentTransitionAuthority
						.CanCompleteJournaledBodyDestruction(System, Body, operation)
					|| !KingdomResidentDepartureRules.Advance(operation,
						KingdomResidentDeparturePhase.CarriersRemoved,
						KingdomResidentDeparturePhase.RolesClosed)) return false;
			}
			if (operation.Phase == (int)KingdomResidentDeparturePhase.RolesClosed)
			{
				if (!KingdomResidentTransitionAuthority.CanCompleteJournaledBodyDestruction(
					System, Body, operation) || !TryPublishEffects(System, operation, out Failure)
					|| !KingdomResidentDepartureRules.Advance(operation,
						KingdomResidentDeparturePhase.RolesClosed,
						KingdomResidentDeparturePhase.EffectsPublished)) return false;
			}
			if (operation.Phase != (int)KingdomResidentDeparturePhase.EffectsPublished
				|| !KingdomResidentTransitionAuthority.CanCompleteJournaledBodyDestruction(
					System, Body, operation)) return false;
			return TryDestroyBody(System, Body, operation, out Failure);
		}
	}
}
