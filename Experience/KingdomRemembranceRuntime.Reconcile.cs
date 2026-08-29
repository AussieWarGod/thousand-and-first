using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRemembranceRuntime
	{
		public static bool TryReconcile(KingdomSystem System, Zone Zone, KingdomSurvey Survey,
			out string Failure)
		{
			Failure = null;
			if (!TryContext(System, Zone, Survey, out CityContext context, out Failure)) return false;
			if (System.Experience == null
				|| !KingdomExperienceRules.TryValidate(System.Experience, out Failure)) return false;
			if (!CleanOrphanMarkers(System, Survey, out Failure)) return false;
			if (!KingdomExperienceRules.TryGetRemembrance(System.Experience,
				context.SettlementId, out KingdomRemembranceReceipt receipt, out Failure)) return false;
			if (receipt == null || receipt.Phase == KingdomRemembrancePhase.Eligible
				|| receipt.Phase == KingdomRemembrancePhase.Declined
				|| receipt.Phase == KingdomRemembrancePhase.Quarantined) return true;
			if (receipt.CarrierZoneId != Zone.ZoneID) return true;
			if (!TryFindExact(Survey, receipt.CarrierObjectId, out GameObject carrier,
				out bool ambiguous))
			{
				if (ambiguous)
				{
					Failure = "More than one object claims the remembrance carrier identity.";
					return false;
				}
				if (receipt.Phase == KingdomRemembrancePhase.Lost) return true;
				return KingdomExperienceRules.TryMarkRemembranceLost(System.Experience,
					System.Experience.Revision, receipt.SettlementId, receipt.CarrierObjectId,
					out Failure);
			}
			if (receipt.Phase == KingdomRemembrancePhase.Lost)
			{
				r_KingdomRemembranceProjection marker =
					carrier.GetPart<r_KingdomRemembranceProjection>();
				if (marker == null) return true;
				if (!marker.MatchesAuthority(System, receipt, carrier))
				{
					Failure = "The lost remembrance marker diverged from its exact receipt.";
					return false;
				}
				return TryRestoreProjection(carrier, marker, out Failure);
			}
			if (!TryExactDeath(context, receipt.SubjectResidentId, out DeathChoice subject)
				|| subject.Row.Name != receipt.SubjectName)
			{
				Failure = "The remembrance's exact terminal resident row is absent."; return false;
			}
			if (!EnsureProjection(System, context, receipt, carrier, subject, out Failure))
				return false;
			if (receipt.Phase == KingdomRemembrancePhase.ProjectionPrepared)
			{
				if (!KingdomExperienceRules.TryCompleteRemembranceProjection(System.Experience,
					System.Experience.Revision, receipt.SettlementId, receipt.Generation,
					out Failure)) return false;
				KingdomExperienceRules.TryGetRemembrance(System.Experience, receipt.SettlementId,
					out receipt, out string _);
			}
			TellProjection(System, receipt); return true;
		}

		private static bool CleanOrphanMarkers(KingdomSystem System, KingdomSurvey Survey,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				r_KingdomRemembranceProjection marker =
					item?.GetPart<r_KingdomRemembranceProjection>();
				if (marker == null) continue;
				if (marker.RealmId != System.RealmId)
				{
					Failure = "A foreign-realm remembrance marker is quarantined in place.";
					return false;
				}
				if (string.IsNullOrEmpty(marker.CarrierObjectId)
					|| string.IsNullOrEmpty(item.IDIfAssigned))
				{
					Failure = "A remembrance marker has no exact carrier identity."; return false;
				}
				if (marker.CarrierObjectId != item.IDIfAssigned)
				{
					// Clones inherit prose and marker bytes but never source identity. Removing the
					// copied proof grants no authority to rewrite copied display fields.
					item.RemovePart(marker); continue;
				}
				bool unique = TryFindExact(Survey, marker.CarrierObjectId,
					out GameObject exactBody, out bool ambiguous) && ReferenceEquals(item, exactBody);
				if (ambiguous)
				{
					Failure = "Duplicate remembrance carrier identity is quarantined in place.";
					return false;
				}
				KingdomRemembranceReceipt receipt;
				bool read = KingdomExperienceRules.TryGetRemembrance(System.Experience,
					marker.SettlementId, out receipt, out string _)
					&& receipt != null;
				bool active = unique && read
					&& receipt != null && (receipt.Phase ==
						KingdomRemembrancePhase.ProjectionPrepared
						|| receipt.Phase == KingdomRemembrancePhase.Projected)
					&& marker.MatchesAuthority(System, receipt, item)
					&& receipt.CarrierZoneId == item.CurrentZone?.ZoneID;
				if (active) continue;
				bool lost = unique && read && receipt.Phase == KingdomRemembrancePhase.Lost
					&& marker.MatchesAuthority(System, receipt, item);
				if (lost)
				{
					if (!TryRestoreProjection(item, marker, out Failure)) return false;
					continue;
				}
				Failure = "An exact-carrier remembrance marker diverged from its source receipt.";
				return false;
			}
			return true;
		}

		private static bool TryFindExact(KingdomSurvey Survey, string ObjectId,
			out GameObject Found, out bool Ambiguous)
		{
			Found = null; Ambiguous = false;
			for (int i = 0; Survey != null && i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				if (item?.IDIfAssigned != ObjectId) continue;
				if (Found != null) { Found = null; Ambiguous = true; return false; }
				Found = item;
			}
			return Found != null;
		}

		private static void TellProjection(KingdomSystem System,
			KingdomRemembranceReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase != KingdomRemembrancePhase.Projected) return;
			KingdomChronicle.RecordOnce(System, "taf:experience:remembrance:"
				+ Receipt.SettlementId + ":" + Receipt.Generation, "a remembrance was dedicated at "
				+ Receipt.SettlementName + " to " + Receipt.SubjectName + " at the word of "
				+ Receipt.MournerName);
		}
	}
}
