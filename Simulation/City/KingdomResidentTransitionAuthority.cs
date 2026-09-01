using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Single read-only gate for resident identity transfer and destructive departure.
	/// It joins body projections to every durable resident-scoped authority before callers mutate
	/// citizenship, resident rows, bindings, or player control.</summary>
	internal static partial class KingdomResidentTransitionAuthority
	{
		internal static bool CanAccede(KingdomSystem System, GameObject Body, int ResidentId)
		{
			return KingdomCitizenRite.CanRetireAccedingHost(System, Body)
				&& TryClaims(System, Body, ResidentId, InspectObjectGraph: false,
				default(KingdomResidentDestructionAuthorization),
				out KingdomResidentTransitionClaim claims, out bool _)
				&& KingdomResidentTransitionRules.CanAccede(claims);
		}

		internal static bool CanDestroyResidentBody(KingdomSystem System, GameObject Body,
			int ResidentId, KingdomResidentDestructionAuthorization Authorization =
				default(KingdomResidentDestructionAuthorization))
		{
			return TryClaims(System, Body, ResidentId, InspectObjectGraph: true,
				Authorization, out KingdomResidentTransitionClaim claims,
				out bool exactLabAuthorization)
				&& KingdomResidentTransitionRules.CanDestroy(claims, exactLabAuthorization);
		}

		/// <summary>Preflights a destructive transition before its exact cook/office vacancy
		/// receipts are prepared. It is read-only and never grants final destruction authority.</summary>
		internal static bool CanPrepareResidentBodyDestruction(KingdomSystem System,
			GameObject Body, int ResidentId,
			KingdomResidentDestructionAuthorization Authorization =
				default(KingdomResidentDestructionAuthorization))
		{
			return TryClaims(System, Body, ResidentId, InspectObjectGraph: true,
				Authorization, out KingdomResidentTransitionClaim claims,
				out bool exactLabAuthorization)
				&& KingdomResidentTransitionRules.CanPrepareDestroy(claims,
					exactLabAuthorization);
		}

		private static bool TryClaims(KingdomSystem System, GameObject Body, int ResidentId,
			bool InspectObjectGraph, KingdomResidentDestructionAuthorization Authorization,
			out KingdomResidentTransitionClaim Claims, out bool ExactLabAuthorization)
		{
			Claims = KingdomResidentTransitionClaim.None;
			ExactLabAuthorization = false;
			if (System == null || !GameObject.Validate(Body) || !Body.IsAlive || ResidentId <= 0
				|| KingdomResidents.IdOf(Body) != ResidentId
				|| string.IsNullOrEmpty(Body.IDIfAssigned)) return false;
			try
			{
				KingdomSuccessionResidentAuthority successionAuthority =
					default(KingdomSuccessionResidentAuthority);
				KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
				bool successionProtected = false;
				if (succession != null && !succession.TryProjectResidentTransitionAuthority(
					System, Body, ResidentId, out successionAuthority,
					out successionProtected)) return false;
				if (successionAuthority.AccessionOwner)
					Claims |= KingdomResidentTransitionClaim.SuccessionAccessionOwner;
				if (successionProtected)
					Claims |= KingdomResidentTransitionClaim.SuccessionProtectedResident;
				if (!TryProveExactIdentity(System, Body, ResidentId, InspectObjectGraph,
					successionAuthority)) return false;
				ProjectBodyClaims(System, Body, ref Claims);
				if (!TryProjectDurableClaims(System, Body, ResidentId, ref Claims)
					|| !TryProjectLoadedClaims(System, Body, ResidentId, Authorization,
						ref Claims, out ExactLabAuthorization)) return false;
				if (!KingdomPolityRules.TryProjectResidentTransitionClaim(
					System.PolityLedger, System.RealmId,
					System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID), ResidentId,
					Body.GetStringProperty("KingdomName"),
					out bool polityClaim)) return false;
				if (polityClaim)
					Claims |= KingdomResidentTransitionClaim.PolityResidentBridge;
				if (InspectObjectGraph
					&& !TryProjectObjectGraphClaims(Body, ref Claims)) return false;
				return true;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("resident transition: authority inspection threw "
					+ ex.GetType().Name);
				return false;
			}
		}

		private static void ProjectBodyClaims(KingdomSystem System, GameObject Body,
			ref KingdomResidentTransitionClaim Claims)
		{
			if (Body.GetPart<r_KingdomResidentDeparture>() != null)
				Claims |= KingdomResidentTransitionClaim.ResidentDeparture;
			if (Body.GetPart<r_KingdomNamedCook>() != null)
				Claims |= KingdomResidentTransitionClaim.NamedCook;
			if (Body.GetPart<r_KingdomAssentingMootMember>() != null)
				Claims |= KingdomResidentTransitionClaim.AssentingMoot;
			if (KingdomPhysicalHappenings.IsStaged(Body))
				Claims |= KingdomResidentTransitionClaim.PhysicalHappening;
			string lodge = Body.GetStringProperty(KingdomGuestbook.LodgeReceiptProperty);
			if (!string.IsNullOrEmpty(lodge)
				&& lodge.StartsWith("intent:", StringComparison.Ordinal))
				Claims |= KingdomResidentTransitionClaim.OpenLodge;
			if (Body.GetIntProperty(KingdomExpeditions.ResidentJobProperty) != 0
				|| Body.HasStringProperty(KingdomExpeditions.DebitReceiptProperty))
				Claims |= KingdomResidentTransitionClaim.Expedition;
			if (Body.GetIntProperty("KingdomKeeper") == 1
				|| Body.GetIntProperty("KingdomKeeperMood") != 0)
				Claims |= KingdomResidentTransitionClaim.Keeper;
			if (Body.GetPart<r_KingdomStasisCustody>() != null)
				Claims |= KingdomResidentTransitionClaim.StasisCustody;
			if (Body.GetPart<r_KingdomOfficeProjection>() != null)
				Claims |= KingdomResidentTransitionClaim.CivicOffice;
			if (Body.GetPart<r_KingdomMarketHandoffSourceProjection>() != null)
				Claims |= KingdomResidentTransitionClaim.PreparedMarketHandoff;

			r_KingdomLegendaryMarketProjection legendary =
				Body.GetPart<r_KingdomLegendaryMarketProjection>();
			if (legendary == null) return;
			if (legendary.HandoffPrepared != 0)
			{
				Claims |= KingdomResidentTransitionClaim.PreparedMarketHandoff;
				return;
			}
			Claims |= KingdomResidentTransitionClaim.CompletedLegendaryMarket;
			if (!KingdomMarketRemoval.CanRetireLegendary(System, Body,
				out bool retires, out string _) || !retires)
				Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
		}
	}
}
