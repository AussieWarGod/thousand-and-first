using System;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	/// <summary>Engine edge for the reversible base-slot contract.</summary>
	public static partial class KingdomCitizenship
	{
		public static bool TryEnroll(KingdomSystem System, GameObject Citizen,
			KingdomCitizenshipEnrollmentReason Reason, out string Failure)
		{
			return TryEnroll(System, Citizen, Reason, Tick(), out Failure);
		}

		public static bool TryEnroll(KingdomSystem System, GameObject Citizen,
			KingdomCitizenshipEnrollmentReason Reason, long FrozenAppliedTick,
			out string Failure)
		{
			Failure = null;
			if (!KingdomCitizenshipRules.ValidReceiptShape(
				KingdomCitizenshipPhase.Prepared, KingdomCitizenshipPriorKind.Absent,
				KingdomCitizenshipRules.RealmMembership, (int)Reason, 0,
				FrozenAppliedTick, 0L))
			{
				Failure = "the frozen citizenship provenance is invalid";
				return false;
			}
			AllegianceSet baseSet;
			string realmId;
			string settlementId;
			string factionId;
			if (!TryLiveContext(System, Citizen, RequireNonPlayer: true, out baseSet,
				out realmId, out settlementId, out factionId, out Failure)) return false;

			r_KingdomCitizenship receipt = Citizen.GetPart<r_KingdomCitizenship>();
			if (receipt == null && Citizen.GetIntProperty("KingdomCitizen") == 1)
			{
				return ObserveLegacy(System, Citizen, out Failure);
			}
			if (receipt == null)
			{
				bool priorPresent = baseSet.TryGetValue(factionId, out int priorValue);
				receipt = Citizen.RequirePart<r_KingdomCitizenship>();
				Initialize(receipt, Citizen, realmId, settlementId, factionId, Reason,
					priorPresent ? KingdomCitizenshipPriorKind.Present
						: KingdomCitizenshipPriorKind.Absent, priorValue);
			}
			else if (receipt.Phase == KingdomCitizenshipPhase.Removed)
			{
				if (!ReceiptSelfMatches(Citizen, receipt, out Failure)) return false;
				bool priorPresent = baseSet.TryGetValue(factionId, out int priorValue);
				Initialize(receipt, Citizen, realmId, settlementId, factionId, Reason,
					priorPresent ? KingdomCitizenshipPriorKind.Present
						: KingdomCitizenshipPriorKind.Absent, priorValue);
			}
			else if (!ReceiptMatches(receipt, Citizen, realmId, settlementId, factionId,
				out Failure))
			{
				// A foreign/current realm is not owner of this receipt. Refuse without changing
				// it so its recorded realm can still perform exact self-authenticated removal.
				KingdomLog.Log("citizenship: enrolment refused without changing a foreign receipt ("
					+ (Failure ?? "unknown ownership mismatch") + ")");
				return false;
			}

			bool currentPresent = baseSet.TryGetValue(factionId, out int currentValue);
			bool firstApplication = receipt.Phase == KingdomCitizenshipPhase.Prepared;
			KingdomCitizenshipMutation mutation = KingdomCitizenshipRules.JudgeApply(
				receipt.Phase, receipt.PriorKind, receipt.PriorValue, currentPresent,
				currentValue, receipt.AppliedValue);
			if (mutation == KingdomCitizenshipMutation.Quarantine)
			{
				Failure = "the owned base-allegiance slot changed outside its citizenship receipt";
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
			try
			{
				if (mutation == KingdomCitizenshipMutation.ApplyOwnedValue)
					baseSet[factionId] = receipt.AppliedValue;
				if (!baseSet.TryGetValue(factionId, out currentValue)
					|| currentValue != receipt.AppliedValue)
				{
					Failure = "the owned base-allegiance slot did not accept its prepared value";
					Diverge(System, Citizen, receipt, Failure);
					return false;
				}
				receipt.Phase = receipt.PriorKind == KingdomCitizenshipPriorKind.Unknown
					? KingdomCitizenshipPhase.LegacyPriorUnknown
					: KingdomCitizenshipPhase.Applied;
				if (firstApplication) receipt.AppliedTick = FrozenAppliedTick;
				receipt.Fault = "";
				Citizen.SetIntProperty("KingdomCitizen", 1);
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the citizenship base-slot callback threw " + ex.GetType().Name;
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
		}

		/// <summary>
		/// Adopts old saves without pretending the destructive legacy writer retained facts it
		/// did not. It writes no allegiance. The unknown receipt is shown on the body and logged.
		/// </summary>
		public static bool ObserveLegacy(KingdomSystem System, GameObject Citizen,
			out string Failure)
		{
			Failure = null;
			if (Citizen == null || Citizen.GetIntProperty("KingdomCitizen") != 1)
			{
				Failure = "the body is not marked as a legacy citizen";
				return false;
			}
			AllegianceSet baseSet;
			string realmId;
			string settlementId;
			string factionId;
			if (!TryLiveContext(System, Citizen, RequireNonPlayer: false, out baseSet,
				out realmId, out settlementId, out factionId, out Failure)) return false;
			r_KingdomCitizenship receipt = Citizen.GetPart<r_KingdomCitizenship>();
			bool present = baseSet.TryGetValue(factionId, out int value);
			// A global v0 marker cannot identify which historical realm wrote it. Prove the
			// current realm's exact slot before attaching current ownership; otherwise a
			// refounded realm would orphan the old faction slot and misattribute the body.
			if (receipt == null && (!present
				|| value != KingdomCitizenshipRules.RealmMembership))
			{
				Failure = "the legacy marker cannot prove ownership by this realm faction";
				PublishUnownedLegacyNotice(System, Citizen, Failure);
				return false;
			}
			if (receipt == null)
			{
				receipt = Citizen.RequirePart<r_KingdomCitizenship>();
				receipt.ReceiptVersion = KingdomCitizenshipRules.CurrentReceiptVersion;
				receipt.Phase = KingdomCitizenshipPhase.LegacyPriorUnknown;
				receipt.PriorKind = KingdomCitizenshipPriorKind.Unknown;
				receipt.PriorValue = 0;
				receipt.AppliedValue = KingdomCitizenshipRules.RealmMembership;
				receipt.OwnerRealmId = realmId;
				receipt.OwnerSettlementId = settlementId;
				receipt.FactionId = factionId;
				receipt.BodyObjectId = Citizen.ID ?? "";
				receipt.EnrollmentReason = (int)KingdomCitizenshipEnrollmentReason.LegacyObservation;
				receipt.AppliedTick = Tick();
			}
			else if (!ReceiptMatches(receipt, Citizen, realmId, settlementId, factionId,
				out Failure))
			{
				KingdomLog.Log("citizenship: legacy observation refused without changing a foreign "
					+ "receipt (" + (Failure ?? "unknown ownership mismatch") + ")");
				return false;
			}
			if (receipt.Phase != KingdomCitizenshipPhase.LegacyPriorUnknown
				&& receipt.Phase != KingdomCitizenshipPhase.Applied)
			{
				Failure = "the legacy marker conflicts with its durable citizenship phase";
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
			if (!present || value != KingdomCitizenshipRules.RealmMembership)
			{
				Failure = "the legacy citizen no longer carries the realm value its old marker implies";
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
			PublishLegacyNotice(System, Citizen, receipt);
			return true;
		}

	}
}
