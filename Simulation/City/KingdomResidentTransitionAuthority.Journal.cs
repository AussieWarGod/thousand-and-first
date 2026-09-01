using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		/// <summary>Lets only the exact write-ahead owner cross its own marker while roles are
		/// prepared or rolled back. Generic transitions remain blocked by that same marker.</summary>
		internal static bool CanPrepareJournaledRoles(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation,
			KingdomResidentDestructionAuthorization Authorization, bool RolesPrepared)
		{
			if (!JournalIdentityMatches(System, Body, Operation)
				|| Operation.Phase != (int)KingdomResidentDeparturePhase.Prepared
					&& Operation.Phase != (int)KingdomResidentDeparturePhase.RolesPrepared
				|| !AuthorizationMatches(Operation, Authorization)
				|| !TryClaims(System, Body, Operation.ResidentId, InspectObjectGraph: true,
					Authorization, out KingdomResidentTransitionClaim claims,
					out bool exactLab)) return false;
			claims &= ~KingdomResidentTransitionClaim.ResidentDeparture;
			return RolesPrepared
				? KingdomResidentTransitionRules.CanDestroy(claims, exactLab)
				: KingdomResidentTransitionRules.CanPrepareDestroy(claims, exactLab);
		}

		/// <summary>Rechecks every non-carrier claim after citizenship removal and before a
		/// row or binding cut. The carrier owner has already proved the surviving carrier shapes.</summary>
		internal static bool CanContinueJournaledCarrierRemoval(KingdomSystem System,
			GameObject Body, KingdomResidentDepartureOperation Operation,
			KingdomResidentDestructionAuthorization Authorization)
		{
			if (!JournalIdentityMatches(System, Body, Operation)
				|| Operation.Phase !=
					(int)KingdomResidentDeparturePhase.CitizenshipRemoved
				|| !AuthorizationMatches(Operation, Authorization)
				|| !KingdomResidentDepartureRuntime.ExactRemovedCitizenship(
					System, Body, Operation)) return false;
			return TryProjectJournalClaims(System, Body, Operation, Authorization,
				out KingdomResidentTransitionClaim claims, out bool exactLab)
				&& KingdomResidentTransitionRules.CanDestroy(claims, exactLab);
		}

		/// <summary>Final destructive proof after the exact journal has removed row and binding.
		/// It reprojects every non-carrier claim; the journal cannot excuse a newly acquired role.</summary>
		internal static bool CanCompleteJournaledBodyDestruction(KingdomSystem System,
			GameObject Body, KingdomResidentDepartureOperation Operation)
		{
			if (!JournalIdentityMatches(System, Body, Operation)
				|| Operation.Phase < (int)KingdomResidentDeparturePhase.CarriersRemoved
				|| !KingdomResidentDepartureRuntime.ExactRemovedCitizenship(
					System, Body, Operation)
				|| !KingdomResidents.DepartureCarriersAbsent(System,
					FindBook(System, Operation.SettlementId), Operation.ResidentId)) return false;
			return TryProjectJournalClaims(System, Body, Operation,
				default(KingdomResidentDestructionAuthorization),
				out KingdomResidentTransitionClaim claims, out bool exactLab)
				&& KingdomResidentTransitionRules.CanDestroy(claims, exactLab);
		}

		private static bool JournalIdentityMatches(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation)
		{
			return KingdomResidentDepartureRules.Valid(Operation)
				&& System != null && System.CurrentRealmId == Operation.RealmId
				&& GameObject.Validate(Body) && Body.IsAlive
				&& Body.IDIfAssigned == Operation.BodyObjectId
				&& Body.CurrentZone?.ZoneID == Operation.ZoneId
				&& Body.GetIntProperty(KingdomResidents.ResidentIdProperty)
					== Operation.ResidentId
				&& Body.GetPart<r_KingdomResidentDeparture>()?.Matches(Operation, Body) == true;
		}

		private static bool AuthorizationMatches(KingdomResidentDepartureOperation Operation,
			KingdomResidentDestructionAuthorization Authorization)
		{
			return Operation.AuthorizationKind == (int)Authorization.Kind
				&& Operation.AuthorizationEventId == (Authorization.EventId ?? "")
				&& Operation.AuthorizationOwnerObjectId ==
					(Authorization.OwnerObjectId ?? "")
				&& Operation.AuthorizationCauseDigest ==
					(Authorization.CauseDigest ?? "");
		}

		private static bool TryProjectJournalClaims(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation,
			KingdomResidentDestructionAuthorization Authorization,
			out KingdomResidentTransitionClaim Claims, out bool ExactLab)
		{
			Claims = KingdomResidentTransitionClaim.None; ExactLab = false;
			try
			{
				KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
				KingdomSuccessionResidentAuthority authority =
					default(KingdomSuccessionResidentAuthority);
				bool protectedResident = false;
				if (succession != null && !succession.TryProjectResidentTransitionAuthority(
					System, Body, Operation.ResidentId,
					out authority, out protectedResident)) return false;
				if (authority.AccessionOwner)
					Claims |= KingdomResidentTransitionClaim.SuccessionAccessionOwner;
				if (protectedResident)
					Claims |= KingdomResidentTransitionClaim.SuccessionProtectedResident;
				ProjectBodyClaims(System, Body, ref Claims);
				// Only this exact journal may consume its matching local marker. Every generic
				// transition sees the same marker as a hard blocker.
				Claims &= ~KingdomResidentTransitionClaim.ResidentDeparture;
				if (!TryProjectDurableClaims(System, Body, Operation.ResidentId, ref Claims)
					|| !TryProjectLoadedClaims(System, Body, Operation.ResidentId,
						Authorization, ref Claims, out ExactLab)) return false;
				if (!KingdomPolityRules.TryProjectResidentTransitionClaim(System.PolityLedger,
					System.RealmId, Operation.SettlementId, Operation.ResidentId,
					Operation.ResidentName, out bool polity)) return false;
				if (polity) Claims |= KingdomResidentTransitionClaim.PolityResidentBridge;
				return TryProjectObjectGraphClaims(Body, ref Claims);
			}
			catch { return false; }
		}

		private static KingdomCityBook FindBook(KingdomSystem System, string SettlementId)
		{
			if (System == null || !System.TryFindSettlement(SettlementId,
				out bool seated, out KingdomSettlement settlement)) return null;
			return seated ? System.City : settlement?.City;
		}
	}
}
