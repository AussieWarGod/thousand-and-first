using System;
using ThousandAndFirst.Simulation.City;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		private static bool HasRole(SocialRoles Roles, string Role)
		{
			return Roles != null && Roles.RoleList != null && Roles.RoleList.Contains(Role);
		}

		private static bool EnsureProjection(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt, GameObject Body, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Body) || Body.IDIfAssigned != Receipt.HolderObjectId
				|| KingdomResidents.IdOf(Body) != Receipt.HolderResidentId)
			{
				Failure = "the exact office body is absent"; return false;
			}
			r_KingdomOfficeProjection marker = Body.GetPart<r_KingdomOfficeProjection>();
			if (marker != null && !marker.Matches(System, Receipt, Body))
			{
				Failure = "another office projection already marks the exact body"; return false;
			}
			string role = RoleFor(Receipt);
			SocialRoles roles = Body.GetPart<SocialRoles>();
			if (marker == null)
			{
				bool already = HasRole(roles, role);
				if (Receipt.OwnsRole == already)
				{
					Failure = Receipt.OwnsRole
						? "a foreign same-text title appeared before office projection"
						: "the borrowed pre-existing office title is absent";
					return false;
				}
				marker = new r_KingdomOfficeProjection { RealmId = System.RealmId,
					SettlementId = Receipt.SettlementId, Generation = Receipt.Generation,
					ResidentId = Receipt.HolderResidentId, BodyObjectId = Receipt.HolderObjectId,
					RoleText = role, OwnsRole = Receipt.OwnsRole };
				Body.AddPart(marker);
			}
			if (!HasRole(roles, role))
			{
				if (!Receipt.OwnsRole)
				{
					Failure = "the foreign pre-existing office title was removed"; return false;
				}
				Body.RequirePart<SocialRoles>().RequireRole(role);
			}
			if (!HasRole(Body.GetPart<SocialRoles>(), role))
			{
				Failure = "the exact office title did not project"; return false;
			}
			return true;
		}

		private static bool CleanupProjection(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt, GameObject Body, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Body) || Body.IDIfAssigned != Receipt.HolderObjectId)
			{
				Failure = "the exact former office body is not loaded"; return false;
			}
			string role = RoleFor(Receipt);
			r_KingdomOfficeProjection marker = Body.GetPart<r_KingdomOfficeProjection>();
			if (marker == null || !marker.Matches(System, Receipt, Body))
			{
				Failure = "the former office marker diverged"; return false;
			}
			if (!KingdomGrowth.TryCleanupMarketService(System, Receipt, Body, marker,
				out Failure)) return false;
			SocialRoles roles = Body.GetPart<SocialRoles>();
			if (Receipt.OwnsRole && HasRole(roles, role)) roles.RemoveRole(role);
			if (Receipt.OwnsRole && HasRole(Body.GetPart<SocialRoles>(), role))
			{
				Failure = "the exact office title resisted removal"; return false;
			}
			Body.RemovePart(marker);
			if (Body.GetPart<r_KingdomOfficeProjection>() == marker)
			{
				Failure = "the exact office marker resisted removal"; return false;
			}
			return true;
		}

		private static void MarkDeathResidue(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt, GameObject Body)
		{
			r_KingdomOfficeProjection marker = Body?.GetPart<r_KingdomOfficeProjection>();
			if (marker != null && marker.Matches(System, Receipt, Body))
				marker.DeathResidue = true;
		}

		private static bool TryCleanupDeathProjection(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt, GameObject Body, out string Failure)
		{
			try { return CleanupProjection(System, Receipt, Body, out Failure); }
			catch (Exception ex)
			{
				Failure = "dead-holder projection cleanup threw " + ex.GetType().Name;
				return false;
			}
		}

		private static bool CleanOrphanMarkers(KingdomSystem System, KingdomSurvey Survey,
			KingdomCityState State, string SettlementId, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject body = Survey.Objects[i];
				r_KingdomOfficeProjection marker = body?.GetPart<r_KingdomOfficeProjection>();
				if (marker == null) continue;
				if (marker.RealmId != System.RealmId)
				{
					Failure = "a foreign-realm office marker is quarantined in place"; return false;
				}
				if (string.IsNullOrEmpty(marker.BodyObjectId)
					|| string.IsNullOrEmpty(body.IDIfAssigned))
				{
					Failure = "an office marker has no exact body identity"; return false;
				}
				if (marker.BodyObjectId != body.IDIfAssigned)
				{
					// A true clone copied the marker, never its source body's identity. Remove only
					// the copied title proof; a same-text role may belong to another source. Market
					// fields are different: the same marker proves their exact copied ownership.
					if (!KingdomGrowth.TryCleanupMarketService(body, marker, out Failure))
						return false;
					body.RemovePart(marker); continue;
				}
				KingdomCivicOfficeReceipt receipt;
				bool exact = KingdomExperienceRules.TryGetOffice(System.Experience,
					marker.SettlementId, out receipt, out string _)
					&& receipt != null && marker.Matches(System, receipt, body)
					&& receipt.Phase != KingdomCivicOfficePhase.Vacant;
				if (exact) continue;
				if (RetiredDeathMarker(System, receipt, marker, State, SettlementId))
				{
					marker.DeathResidue = true;
					if (!TryCleanDeathResidue(System, body, marker,
						out string residueFailure))
						KingdomLog.Log("office: dead-holder projection remains quarantined ("
							+ (residueFailure ?? "exact cleanup unavailable") + ")");
					continue;
				}
				Failure = "an exact-body office marker diverged from its source receipt";
				return false;
			}
			return true;
		}

		private static bool RetiredDeathMarker(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt, r_KingdomOfficeProjection Marker,
			KingdomCityState State, string SettlementId)
		{
			if (System == null || Receipt == null || Marker == null || State == null
				|| Marker.RealmId != System.RealmId || Marker.SettlementId != SettlementId
				|| Receipt.SettlementId != SettlementId || State.SettlementId != SettlementId
				|| Marker.Generation < 1 || Receipt.Generation < Marker.Generation
				|| Marker.ResidentId <= 0
				|| !State.TryResidentIndex(Marker.ResidentId, out int residentIndex)
				|| !State.TryResident(residentIndex, out KingdomResidentRow resident)
				|| resident.ResidentId != Marker.ResidentId
				|| resident.Standing != KingdomResidentStanding.Dead) return false;
			if (Receipt.Generation > Marker.Generation) return true;
			return Receipt.VacancyCause == KingdomCivicOfficeVacancyCause.Death
				&& (Receipt.Phase == KingdomCivicOfficePhase.VacancyPrepared
					&& Receipt.HolderResidentId == Marker.ResidentId
					|| Receipt.Phase == KingdomCivicOfficePhase.Vacant
					&& Receipt.PredecessorResidentId == Marker.ResidentId);
		}

		private static bool TryCleanDeathResidue(KingdomSystem System, GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Body) || Marker == null
				|| Body.GetPart<r_KingdomOfficeProjection>() != Marker
				|| Marker.BodyObjectId != Body.IDIfAssigned || !Marker.DeathResidue)
			{
				Failure = "dead-holder residue lacks exact body identity"; return false;
			}
			if (!KingdomGrowth.TryCompleteTransferredMarketService(System, Body, Marker,
				out Failure)) return false;
			SocialRoles roles = Body.GetPart<SocialRoles>();
			if (Marker.OwnsRole && HasRole(roles, Marker.RoleText))
				roles.RemoveRole(Marker.RoleText);
			if (Marker.OwnsRole && HasRole(Body.GetPart<SocialRoles>(), Marker.RoleText))
			{
				Failure = "dead-holder owned title resisted exact removal"; return false;
			}
			Body.RemovePart(Marker);
			if (Body.GetPart<r_KingdomOfficeProjection>() == Marker)
			{
				Failure = "dead-holder marker resisted exact removal"; return false;
			}
			return true;
		}
	}
}
