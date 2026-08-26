using System;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomCitizenship
	{
		public static bool CanRemove(KingdomSystem System, GameObject Citizen,
			out string Failure)
		{
			return TryRemovalAction(System, Citizen, Mutate: false,
				KingdomCitizenshipRemovalReason.ForeignTransfer, out Failure);
		}

		public static bool TryRemove(KingdomSystem System, GameObject Citizen,
			KingdomCitizenshipRemovalReason Reason, out string Failure)
		{
			return TryRemovalAction(System, Citizen, Mutate: true, Reason, out Failure);
		}

		/// <summary>Exact, narrow rollback when emigration's resident carrier refused cleanly.</summary>
		internal static bool TryRestoreEmigrationAfterCleanRefusal(KingdomSystem System,
			GameObject Citizen, out string Failure)
		{
			Failure = null;
			r_KingdomCitizenship receipt = Citizen?.GetPart<r_KingdomCitizenship>();
			if (!ReceiptSelfMatches(Citizen, receipt, out Failure)
				|| receipt.Phase != KingdomCitizenshipPhase.Removed
				|| receipt.RemovalReason != (int)KingdomCitizenshipRemovalReason.Emigration
				|| System == null
				|| Citizen.IsPlayer()
				|| !string.Equals(receipt.OwnerRealmId ?? "", System.CurrentRealmId ?? "",
					StringComparison.Ordinal)
				|| !string.Equals(receipt.FactionId ?? "", System.KingdomFactionName ?? "",
					StringComparison.Ordinal))
			{
				Failure = Failure ?? "the removed citizenship receipt is not owned by this realm";
				return false;
			}
			Simulation.City.KingdomCityBook stillBook;
			int stillResidentId;
			if (!Simulation.City.KingdomResidents.TryLocate(System, Citizen,
				out stillBook, out stillResidentId) || stillBook == null || stillResidentId == 0)
			{
				Failure = "the cleanly refused emigrant no longer has its exact resident row";
				return false;
			}
			AllegianceSet baseSet = Citizen.Brain.GetBaseAllegiance();
			int current = 0;
			bool present = baseSet != null
				&& baseSet.TryGetValue(receipt.FactionId, out current);
			if (baseSet == null || !KingdomCitizenshipRules.MatchesRemovalPost(receipt.PriorKind,
				receipt.PriorValue, present, current))
			{
				Failure = "the post-removal slot changed before civic rollback";
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
			try
			{
				baseSet[receipt.FactionId] = receipt.AppliedValue;
				if (!baseSet.TryGetValue(receipt.FactionId, out current)
					|| current != receipt.AppliedValue)
				{
					Failure = "the civic rollback did not restore its exact owned value";
					Diverge(System, Citizen, receipt, Failure);
					return false;
				}
				receipt.Phase = receipt.PriorKind == KingdomCitizenshipPriorKind.Unknown
					? KingdomCitizenshipPhase.LegacyPriorUnknown
					: KingdomCitizenshipPhase.Applied;
				receipt.RemovalReason = 0;
				receipt.RemovedTick = 0L;
				receipt.Fault = "";
				Citizen.SetIntProperty("KingdomCitizen", 1);
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the civic rollback threw " + ex.GetType().Name;
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
		}

		/// <summary>Exact realm-membership read. A global marker alone is never authority.</summary>
		public static bool BelongsTo(KingdomSystem System, GameObject Citizen)
		{
			if (System == null || Citizen == null || Citizen.Brain == null
				|| Citizen.GetIntProperty("KingdomCitizen") != 1) return false;
			r_KingdomCitizenship receipt = Citizen.GetPart<r_KingdomCitizenship>();
			if (receipt == null || receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion
				|| !KingdomCitizenshipRules.ValidReceiptShape(receipt.Phase, receipt.PriorKind,
					receipt.AppliedValue, receipt.EnrollmentReason, receipt.RemovalReason,
					receipt.AppliedTick, receipt.RemovedTick)
				|| (receipt.Phase != KingdomCitizenshipPhase.Applied
					&& receipt.Phase != KingdomCitizenshipPhase.LegacyPriorUnknown)
				|| !string.Equals(receipt.BodyObjectId ?? "", Citizen.IDIfAssigned ?? "",
					StringComparison.Ordinal)
				|| !string.Equals(receipt.OwnerRealmId ?? "", System.CurrentRealmId ?? "",
					StringComparison.Ordinal)
				|| !string.Equals(receipt.FactionId ?? "", System.KingdomFactionName ?? "",
					StringComparison.Ordinal)) return false;
			AllegianceSet baseSet = Citizen.Brain.GetBaseAllegiance();
			return baseSet != null && baseSet.TryGetValue(receipt.FactionId, out int value)
				&& value == receipt.AppliedValue
				&& receipt.AppliedValue == KingdomCitizenshipRules.RealmMembership;
		}

		private static bool TryRemovalAction(KingdomSystem System, GameObject Citizen, bool Mutate,
			KingdomCitizenshipRemovalReason Reason, out string Failure)
		{
			Failure = null;
			if (!KingdomCitizenshipRules.ValidReceiptShape(KingdomCitizenshipPhase.Removed,
				KingdomCitizenshipPriorKind.Absent, KingdomCitizenshipRules.RealmMembership,
				(int)KingdomCitizenshipEnrollmentReason.Arrival, (int)Reason, 0L, 0L))
			{
				Failure = "the citizenship removal reason is invalid";
				return false;
			}
			if (Citizen == null || Citizen.Brain == null)
			{
				Failure = "the citizenship body or brain is absent";
				return false;
			}
			r_KingdomCitizenship receipt = Citizen.GetPart<r_KingdomCitizenship>();
			if (receipt == null)
			{
				if (Citizen.GetIntProperty("KingdomCitizen") != 1) return true;
				if (!ObserveLegacy(System, Citizen, out Failure)) return false;
				receipt = Citizen.GetPart<r_KingdomCitizenship>();
			}
			if (receipt == null)
			{
				Failure = "the citizenship receipt is absent";
				return false;
			}
			if (!ReceiptSelfMatches(Citizen, receipt, out Failure))
			{
				KingdomLog.Log("citizenship: removal refused without changing its receipt ("
					+ (Failure ?? "unknown owner failure") + ")");
				return false;
			}
			if (receipt.Phase == KingdomCitizenshipPhase.Removed)
			{
				if (Mutate) Citizen.RemoveIntProperty("KingdomCitizen");
				return true;
			}
			AllegianceSet baseSet = Citizen.Brain.GetBaseAllegiance();
			if (baseSet == null)
			{
				Failure = "the Brain has no exact base allegiance";
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
			bool present = baseSet.TryGetValue(receipt.FactionId, out int value);
			KingdomCitizenshipMutation action = KingdomCitizenshipRules.JudgeRemove(
				receipt.Phase, receipt.PriorKind, receipt.PriorValue, present, value,
				receipt.AppliedValue);
			if (action == KingdomCitizenshipMutation.Quarantine)
			{
				Failure = "the realm value changed; removal will not overwrite foreign allegiance";
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
			if (!Mutate) return true;
			try
			{
				if (action == KingdomCitizenshipMutation.RestorePriorValue)
					baseSet[receipt.FactionId] = receipt.PriorValue;
				else if (action == KingdomCitizenshipMutation.RemoveOwnedValue)
					baseSet.Remove(receipt.FactionId);

				present = baseSet.TryGetValue(receipt.FactionId, out value);
				if (receipt.PriorKind == KingdomCitizenshipPriorKind.Present)
				{
					if (!present || value != receipt.PriorValue)
					{
						Failure = "the prior realm-slot value was not restored exactly";
						Diverge(System, Citizen, receipt, Failure);
						return false;
					}
				}
				else if (present)
				{
					Failure = "the realm slot was not relinquished exactly";
					Diverge(System, Citizen, receipt, Failure);
					return false;
				}
				receipt.Phase = KingdomCitizenshipPhase.Removed;
				receipt.RemovalReason = (int)Reason;
				receipt.RemovedTick = Tick();
				receipt.Fault = "";
				Citizen.RemoveIntProperty("KingdomCitizen");
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the citizenship removal callback threw " + ex.GetType().Name;
				Diverge(System, Citizen, receipt, Failure);
				return false;
			}
		}

	}
}
