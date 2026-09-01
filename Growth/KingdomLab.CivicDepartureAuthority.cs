using System;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		/// <summary>Mints the narrow capability used only by this exact warned roof-brink.
		/// An ordinary roof departure returns an empty capability; a torn lab marker refuses.</summary>
		internal static bool TryAuthorizeDeparture(KingdomSystem System, Zone Z,
			GameObject Resident, out KingdomResidentDestructionAuthorization Authorization)
		{
			Authorization = default(KingdomResidentDestructionAuthorization);
			string eventId = Resident?.GetStringProperty(RefusalEventProperty);
			string ownerId = Resident?.GetStringProperty(RefusalOwnerProperty);
			string digest = Resident?.GetStringProperty(RefusalDigestProperty);
			bool any = !string.IsNullOrEmpty(eventId) || !string.IsNullOrEmpty(ownerId)
				|| !string.IsNullOrEmpty(digest);
			if (!any) return true;
			if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(ownerId)
				|| string.IsNullOrEmpty(digest) || System == null || Z == null
				|| !GameObject.Validate(Resident) || !ReferenceEquals(Resident.CurrentZone, Z))
				return false;
			GameObject owner = null;
			int owners = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				if (GameObject.Validate(item) && string.Equals(item.IDIfAssigned, ownerId,
					StringComparison.Ordinal)) { owner = item; owners++; }
			if (owners != 1) return false;
			r_KingdomLabCivicFriction part = owner.GetPart<r_KingdomLabCivicFriction>();
			KingdomLabCivicReceipt receipt = part?.RefusalDeparture;
			KingdomResidentDestructionAuthorization proposed =
				new KingdomResidentDestructionAuthorization(
					KingdomResidentDestructionAuthorizationKind.LabRefusalDeparture,
					eventId, ownerId, digest);
			if (!ReadOnlyDepartureAuthorizationMatches(System, owner, Resident, receipt,
				proposed)) return false;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			if (!ExactDepartureCause(System, Z, survey, owner, Resident, receipt,
				out GameObject _, out string _)) return false;
			Authorization = proposed;
			return true;
		}

		/// <summary>Re-proves a previously minted capability against current durable owner,
		/// source receipt, and resident marker state. No reconciliation or publication occurs.</summary>
		internal static bool ReadOnlyDepartureAuthorizationMatches(KingdomSystem System,
			GameObject Owner, GameObject Resident, KingdomLabCivicReceipt Receipt,
			KingdomResidentDestructionAuthorization Authorization)
		{
			if (Authorization.Kind !=
					KingdomResidentDestructionAuthorizationKind.LabRefusalDeparture
				|| !KingdomLabCivicRules.Valid(Receipt, out string _)
				|| Receipt.Kind != KingdomLabCivicKind.RefusalDeparture
				|| Receipt.Phase != KingdomLabCivicPhase.Active
				|| !GameObject.Validate(Owner) || !GameObject.Validate(Resident)
				|| !string.Equals(Owner.IDIfAssigned, Receipt.OwnerObjectId,
					StringComparison.Ordinal)
				|| !string.Equals(Owner.CurrentZone?.ZoneID, Receipt.ZoneId,
					StringComparison.Ordinal)
				|| !string.Equals(Resident.IDIfAssigned, Receipt.SubjectObjectId,
					StringComparison.Ordinal)
				|| KingdomResidentsId(Resident) != Receipt.SubjectResidentId
				|| !string.Equals(Resident.GetStringProperty("KingdomName"),
					Receipt.SubjectName, StringComparison.Ordinal)
				|| !MarkerMatches(Resident, Receipt)
				|| !string.Equals(Authorization.EventId, Receipt.EventId,
					StringComparison.Ordinal)
				|| !string.Equals(Authorization.OwnerObjectId, Receipt.OwnerObjectId,
					StringComparison.Ordinal)
				|| !string.Equals(Authorization.CauseDigest, Receipt.CauseDigest,
					StringComparison.Ordinal)
				|| !string.Equals(System?.CurrentRealmId, Receipt.RealmId,
					StringComparison.Ordinal)
				|| !string.Equals(System?.SettlementIdForOwnedZone(Receipt.ZoneId),
					Receipt.SettlementId, StringComparison.Ordinal)) return false;
			if (!TryReadOwners(out string _, out KingdomLabCivicOwnerBook book,
				out string _)) return false;
			KingdomLabCivicOwnerRow held = KingdomLabCivicOwnerRules.Find(book,
				Receipt.SettlementId);
			return held != null && held.RealmId == Receipt.RealmId
				&& held.SettlementId == Receipt.SettlementId
				&& held.ZoneId == Receipt.ZoneId
				&& held.OwnerObjectId == Receipt.OwnerObjectId;
		}

		/// <summary>Closes or re-proves the exact roof-brink source before its departed body is
		/// destroyed. An ordinary departure proves that no laboratory marker appeared meanwhile.</summary>
		internal static bool TryCompleteAuthorizedDeparture(KingdomSystem System, Zone Z,
			GameObject Resident, int ResidentId,
			KingdomResidentDestructionAuthorization Authorization, out string Failure)
		{
			Failure = null;
			if (Authorization.Kind == KingdomResidentDestructionAuthorizationKind.None)
			{
				bool clear = string.IsNullOrEmpty(Resident?.GetStringProperty(RefusalEventProperty))
					&& string.IsNullOrEmpty(Resident?.GetStringProperty(RefusalOwnerProperty))
					&& string.IsNullOrEmpty(Resident?.GetStringProperty(RefusalDigestProperty));
				if (!clear) Failure = "ordinary departure acquired a laboratory claim";
				return clear;
			}
			if (Authorization.Kind !=
				KingdomResidentDestructionAuthorizationKind.LabRefusalDeparture
				|| System == null || Z == null || !GameObject.Validate(Resident)
				|| KingdomResidentsId(Resident) != ResidentId)
			{
				Failure = "laboratory departure authorization identity diverged"; return false;
			}
			GameObject owner = null; int owners = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				if (GameObject.Validate(item) && item.IDIfAssigned
					== Authorization.OwnerObjectId) { owner = item; owners++; }
			if (owners != 1)
			{
				Failure = "laboratory departure owner is absent or non-unique"; return false;
			}
			r_KingdomLabCivicFriction part = owner.GetPart<r_KingdomLabCivicFriction>();
			KingdomLabCivicReceipt receipt = part?.RefusalDeparture;
			if (receipt?.Phase == KingdomLabCivicPhase.Active)
			{
				if (!ReadOnlyDepartureAuthorizationMatches(System, owner, Resident, receipt,
					Authorization))
				{
					Failure = "laboratory departure authorization no longer matches"; return false;
				}
				ObserveDeparture(System, Z, Resident, ResidentId);
				receipt = part.RefusalDeparture;
			}
			bool exact = KingdomLabCivicRules.Valid(receipt, out string failure)
				&& receipt.Kind == KingdomLabCivicKind.RefusalDeparture
				&& receipt.Phase == KingdomLabCivicPhase.Closed
				&& receipt.Closure == KingdomLabCivicClosure.Departed
				&& receipt.SubjectResidentId == ResidentId
				&& receipt.SubjectObjectId == Resident.IDIfAssigned
				&& receipt.EventId == Authorization.EventId
				&& receipt.OwnerObjectId == Authorization.OwnerObjectId
				&& receipt.CauseDigest == Authorization.CauseDigest
				&& receipt.RealmId == System.CurrentRealmId
				&& receipt.ZoneId == Z.ZoneID;
			if (!exact) Failure = failure ?? "laboratory departure did not close exactly";
			return exact;
		}
	}
}
