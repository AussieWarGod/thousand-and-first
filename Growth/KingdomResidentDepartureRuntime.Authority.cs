using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureRuntime
	{
		private static bool TryCaptureRoles(KingdomSystem System, GameObject Body,
			int ResidentId, string SettlementId, out KingdomNamedCookReceipt Cook,
			out KingdomCivicOfficeReceipt Office, out KingdomPolityNamedFigureRecord Polity,
			out string PolityConclusion, out string Failure)
		{
			Cook = null; Office = null; Polity = null; PolityConclusion = null; Failure = null;
			List<KingdomCityBook> books = System.OwnedCityBooks();
			for (int i = 0; books != null && i < books.Count; i++)
			{
				KingdomNamedCookReceipt row = books[i]?.NamedCook;
				if (row == null || row.Phase == KingdomNamedCookPhase.None
					|| KingdomNamedCookRules.IsVacant(row.Phase)
					|| row.ResidentId != ResidentId && row.BodyObjectId != Body.IDIfAssigned)
					continue;
				if (Cook != null || row.Phase != KingdomNamedCookPhase.Applied
					|| !KingdomNamedCookRules.Validate(row, out Failure)
					|| row.ResidentId != ResidentId || row.BodyObjectId != Body.IDIfAssigned
					|| row.SettlementId != SettlementId) return false;
				Cook = row.Copy();
			}
			if (System.Experience?.Offices == null) return false;
			for (int i = 0; i < System.Experience.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt row = System.Experience.Offices[i];
				if (row == null || row.Phase == KingdomCivicOfficePhase.None
					|| row.Phase == KingdomCivicOfficePhase.Vacant
					|| row.HolderResidentId != ResidentId
						&& row.HolderObjectId != Body.IDIfAssigned) continue;
				if (Office != null || !KingdomExperienceRules.ValidOffice(row)
					|| row.Phase != KingdomCivicOfficePhase.Held
						&& row.Phase != KingdomCivicOfficePhase.AppointmentPrepared
					|| row.HolderResidentId != ResidentId
					|| row.HolderObjectId != Body.IDIfAssigned
					|| row.SettlementId != SettlementId) return false;
				Office = KingdomExperienceRules.CopyOffice(row);
			}
			return KingdomPolityRules.TryCaptureDeedResident(System.PolityLedger,
				System.CurrentRealmId, SettlementId, ResidentId,
				Body.GetStringProperty("KingdomName"), out Polity, out PolityConclusion);
		}

		private static bool SamePreparation(KingdomResidentDepartureOperation Operation,
			KingdomResidentDeparturePreparation Preparation)
		{
			return SameCook(Operation.PriorCook, Preparation.PriorCook)
				&& SameOffice(Operation.PriorOffice, Preparation.PriorOffice)
				&& SamePolity(Operation.PriorPolity, Preparation.PriorPolity.Prior)
				&& (Operation.PriorPolity == null || Operation.PolityConclusionRef
					== Preparation.PriorPolity.ConclusionRef);
		}

		private static bool SameCook(KingdomNamedCookReceipt A, KingdomNamedCookReceipt B)
		{
			if (A == null || B == null) return A == null && B == null;
			return A.Version == B.Version && A.Generation == B.Generation
				&& A.RealmId == B.RealmId && A.SettlementId == B.SettlementId
				&& A.ResidentId == B.ResidentId && A.BodyObjectId == B.BodyObjectId
				&& A.RecipeId == B.RecipeId && A.GraphFingerprint == B.GraphFingerprint
				&& A.DesignatedTick == B.DesignatedTick && A.Phase == B.Phase;
		}

		private static bool SameOffice(KingdomCivicOfficeReceipt A,
			KingdomCivicOfficeReceipt B)
		{
			if (A == null || B == null) return A == null && B == null;
			return A.Version == B.Version && A.Generation == B.Generation
				&& A.SettlementId == B.SettlementId && A.WorkId == B.WorkId
				&& A.HolderResidentId == B.HolderResidentId
				&& A.HolderObjectId == B.HolderObjectId && A.HolderName == B.HolderName
				&& A.OwnsRole == B.OwnsRole && A.Phase == B.Phase
				&& A.ChangedTick == B.ChangedTick;
		}

		private static bool SamePolity(KingdomPolityNamedFigureRecord A,
			KingdomPolityNamedFigureRecord B)
		{
			if (A == null || B == null) return A == null && B == null;
			return A.FigureId == B.FigureId && A.PolityId == B.PolityId
				&& A.DisplayName == B.DisplayName && A.RoleKey == B.RoleKey
				&& A.Origin == B.Origin && A.Phase == B.Phase && A.CauseRef == B.CauseRef
				&& A.DeedSummary == B.DeedSummary && A.ChronicleRef == B.ChronicleRef
				&& A.ConclusionRef == B.ConclusionRef && A.ResidentId == B.ResidentId
				&& A.ResidentSettlementId == B.ResidentSettlementId;
		}

		internal static KingdomResidentDestructionAuthorization AuthorizationOf(
			KingdomResidentDepartureOperation Operation)
		{
			return new KingdomResidentDestructionAuthorization(
				(KingdomResidentDestructionAuthorizationKind)Operation.AuthorizationKind,
				Operation.AuthorizationEventId, Operation.AuthorizationOwnerObjectId,
				Operation.AuthorizationCauseDigest);
		}

		internal static bool ExactRemovedCitizenship(KingdomSystem System, GameObject Body,
			KingdomResidentDepartureOperation Operation)
		{
			r_KingdomCitizenship receipt = Body?.GetPart<r_KingdomCitizenship>();
			if (receipt == null || Body.Brain == null
				|| receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion
				|| receipt.Phase != KingdomCitizenshipPhase.Removed
				|| receipt.RemovalReason != (int)KingdomCitizenshipRemovalReason.Emigration
				|| !KingdomCitizenshipRules.ValidReceiptShape(receipt.Phase,
					receipt.PriorKind, receipt.AppliedValue, receipt.EnrollmentReason,
					receipt.RemovalReason, receipt.AppliedTick, receipt.RemovedTick)
				|| receipt.BodyObjectId != Operation.BodyObjectId
				|| receipt.OwnerRealmId != Operation.RealmId
				|| receipt.OwnerSettlementId != Operation.SettlementId
				|| receipt.FactionId != System.KingdomFactionName
				|| Body.GetIntProperty("KingdomCitizen") == 1) return false;
			var allegiance = Body.Brain.GetBaseAllegiance();
			int value = 0;
			bool present = allegiance != null
				&& allegiance.TryGetValue(receipt.FactionId, out value);
			return allegiance != null && KingdomCitizenshipRules.MatchesRemovalPost(
				receipt.PriorKind, receipt.PriorValue, present, value);
		}
	}
}
