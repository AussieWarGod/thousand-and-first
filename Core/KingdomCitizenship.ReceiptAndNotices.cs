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
		private static void Initialize(r_KingdomCitizenship Receipt, GameObject Citizen,
			string RealmId, string SettlementId, string FactionId,
			KingdomCitizenshipEnrollmentReason Reason, KingdomCitizenshipPriorKind PriorKind,
			int PriorValue)
		{
			Receipt.ReceiptVersion = KingdomCitizenshipRules.CurrentReceiptVersion;
			Receipt.Phase = KingdomCitizenshipPhase.Prepared;
			Receipt.PriorKind = PriorKind;
			Receipt.PriorValue = PriorValue;
			Receipt.AppliedValue = KingdomCitizenshipRules.RealmMembership;
			Receipt.OwnerRealmId = RealmId;
			Receipt.OwnerSettlementId = SettlementId;
			Receipt.FactionId = FactionId;
			Receipt.BodyObjectId = Citizen.ID ?? "";
			Receipt.EnrollmentReason = (int)Reason;
			Receipt.RemovalReason = 0;
			Receipt.AppliedTick = 0L;
			Receipt.RemovedTick = 0L;
			Receipt.NoticePublished = false;
			Receipt.Fault = "";
		}

		private static bool TryLiveContext(KingdomSystem System, GameObject Citizen,
			bool RequireNonPlayer, out AllegianceSet Base, out string RealmId,
			out string SettlementId, out string FactionId, out string Failure)
		{
			Base = null;
			RealmId = null;
			SettlementId = null;
			FactionId = null;
			Failure = null;
			if (System == null || !System.Founded || Citizen == null || Citizen.Brain == null
				|| (RequireNonPlayer && Citizen.IsPlayer()))
			{
				Failure = "the realm or eligible non-player Brain is absent";
				return false;
			}
			RealmId = System.CurrentRealmId;
			SettlementId = System.CurrentSettlementId;
			FactionId = System.KingdomFactionName;
			if (string.IsNullOrEmpty(RealmId) || string.IsNullOrEmpty(SettlementId)
				|| string.IsNullOrEmpty(FactionId) || Factions.GetIfExists(FactionId) == null)
			{
				Failure = "the exact realm, settlement, or runtime faction identity is unavailable";
				return false;
			}
			Base = Citizen.Brain.GetBaseAllegiance();
			if (Base == null)
			{
				Failure = "the Brain has no exact base allegiance";
				return false;
			}
			return true;
		}

		private static bool ReceiptMatches(r_KingdomCitizenship Receipt, GameObject Citizen,
			string RealmId, string SettlementId, string FactionId, out string Failure)
		{
			Failure = null;
			if (Receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion)
				Failure = "the citizenship receipt version is unknown";
			else if (!string.Equals(Receipt.BodyObjectId ?? "", Citizen.IDIfAssigned ?? "",
				StringComparison.Ordinal)) Failure = "the citizenship receipt names another body";
			else if (!string.Equals(Receipt.OwnerRealmId ?? "", RealmId ?? "",
				StringComparison.Ordinal)) Failure = "the citizenship receipt names another realm";
			else if (!string.Equals(Receipt.FactionId ?? "", FactionId ?? "",
				StringComparison.Ordinal)) Failure = "the citizenship receipt names another faction";
			else if (Receipt.AppliedValue != KingdomCitizenshipRules.RealmMembership)
				Failure = "the citizenship receipt names an unknown applied value";
			else if (!KingdomCitizenshipRules.ValidReceiptShape(Receipt.Phase,
				Receipt.PriorKind, Receipt.AppliedValue, Receipt.EnrollmentReason,
				Receipt.RemovalReason, Receipt.AppliedTick, Receipt.RemovedTick))
				Failure = "the citizenship receipt shape is invalid";
			return Failure == null;
		}

		private static bool ReceiptSelfMatches(GameObject Citizen,
			r_KingdomCitizenship Receipt, out string Failure)
		{
			Failure = null;
			if (Citizen == null || Citizen.Brain == null || Receipt == null)
				Failure = "the receipt body is absent";
			else if (Receipt.ReceiptVersion != KingdomCitizenshipRules.CurrentReceiptVersion)
				Failure = "the citizenship receipt version is unknown";
			else if (!string.Equals(Receipt.BodyObjectId ?? "", Citizen.IDIfAssigned ?? "",
				StringComparison.Ordinal)) Failure = "the citizenship receipt names another body";
			else if (string.IsNullOrEmpty(Receipt.OwnerRealmId))
				Failure = "the citizenship receipt has no realm owner";
			else if (string.IsNullOrEmpty(Receipt.FactionId))
				Failure = "the citizenship receipt has no owned faction slot";
			else if (Receipt.AppliedValue != KingdomCitizenshipRules.RealmMembership)
				Failure = "the citizenship receipt names an unknown applied value";
			else if (!KingdomCitizenshipRules.ValidReceiptShape(Receipt.Phase,
				Receipt.PriorKind, Receipt.AppliedValue, Receipt.EnrollmentReason,
				Receipt.RemovalReason, Receipt.AppliedTick, Receipt.RemovedTick))
				Failure = "the citizenship receipt shape is invalid";
			return Failure == null;
		}

		private static void Diverge(KingdomSystem System, GameObject Citizen,
			r_KingdomCitizenship Receipt, string Failure)
		{
			if (Receipt != null)
			{
				Receipt.Phase = KingdomCitizenshipPhase.Diverged;
				Receipt.Fault = Failure ?? "citizenship receipt diverged";
			}
			string display = Display(Citizen);
			KingdomLog.Log("citizenship: " + display + " quarantined ("
				+ (Failure ?? "unknown divergence") + ")");
			if (Receipt != null && !Receipt.NoticePublished)
			{
				Receipt.NoticePublished = true;
				System?.Ledger?.Note("{{R|The citizenship receipt for "
					+ KingdomPresentation.Rich(display)
					+ " diverged. The realm left the body's live allegiance untouched.}}");
			}
		}

		private static void PublishLegacyNotice(KingdomSystem System, GameObject Citizen,
			r_KingdomCitizenship Receipt)
		{
			if (Receipt == null || Receipt.NoticePublished) return;
			Receipt.NoticePublished = true;
			string display = Display(Citizen);
			string line = "The old citizenship record for " + display
				+ " may already have erased its native base factions or changed allegiance flags. "
				+ "Those facts are not guessed; its exact realm slot is marked legacy-unknown.";
			string shown = "The old citizenship record for " + KingdomPresentation.Rich(display)
				+ " may already have erased its native base factions or changed allegiance flags. "
				+ "Those facts are not guessed; its exact realm slot is marked legacy-unknown.";
			System?.Ledger?.Note("{{K|" + shown + "}}");
			KingdomLog.Log("citizenship: " + line);
		}

		private static void PublishUnownedLegacyNotice(KingdomSystem System, GameObject Citizen,
			string Failure)
		{
			const string noticeProperty = "r_TAF_CitizenshipUnownedNotice";
			if (Citizen == null || Citizen.GetIntProperty(noticeProperty) == 1) return;
			Citizen.SetIntProperty(noticeProperty, 1);
			string display = Display(Citizen);
			string line = "The old citizenship marker for " + display
				+ " cannot prove which former realm wrote it. No current-realm receipt was "
				+ "created, and its live allegiance was left untouched.";
			string shown = "The old citizenship marker for " + KingdomPresentation.Rich(display)
				+ " cannot prove which former realm wrote it. No current-realm receipt was "
				+ "created, and its live allegiance was left untouched.";
			System?.Ledger?.Note("{{K|" + shown + "}}");
			KingdomLog.Log("citizenship: " + line + " (" + (Failure ?? "unowned legacy") + ")");
		}

		private static string Display(GameObject Citizen)
		{
			if (Citizen == null) return "an absent citizen";
			string named = Citizen.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(named) ? Citizen.BaseDisplayNameStripped : named;
		}

		private static long Tick()
		{
			return The.Game == null ? 0L : The.Game.TimeTicks;
		}
	}
}
