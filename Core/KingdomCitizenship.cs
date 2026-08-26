using System;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>
	/// Exact receipt for TAF's one namespaced base-allegiance slot. This part never owns the
	/// Brain, its temporary allegiance chain, flags, leader, conversation, quests or lifecycle.
	/// </summary>
	[Serializable]
	public sealed class r_KingdomCitizenship : IPart
	{
		public int ReceiptVersion;
		public KingdomCitizenshipPhase Phase;
		public KingdomCitizenshipPriorKind PriorKind;
		public int PriorValue;
		public int AppliedValue;
		public string OwnerRealmId = "";
		public string OwnerSettlementId = "";
		public string FactionId = "";
		public string BodyObjectId = "";
		public int EnrollmentReason;
		public int RemovalReason;
		public long AppliedTick;
		public long RemovedTick;
		public bool NoticePublished;
		public string Fault = "";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID
				|| ID == BeforeDeathRemovalEvent.ID;
		}

		public override bool HandleEvent(BeforeDeathRemovalEvent E)
		{
			try
			{
				// Couple resident authority to allegiance cleanup before either receipt can
				// advance. The legacy reporter may fire first or second; both paths are
				// idempotent. Direct removal below also covers a foreign/no-current realm.
				KingdomOffices.RecordDeath(ParentObject, E.Killer);
				string failure;
				KingdomCitizenship.TryRemove(The.Game?.GetSystem<KingdomSystem>(), ParentObject,
					KingdomCitizenshipRemovalReason.Death, out failure);
			}
			catch (Exception ex)
			{
				// Death belongs to the engine. A civic cleanup may fail closed, never veto it.
				KingdomLog.Log("citizenship: death hook left its exact receipt pending ("
					+ ex.GetType().Name + ")");
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (Phase == KingdomCitizenshipPhase.LegacyPriorUnknown)
			{
				E.Postfix.Append("\n{{rules|Citizenship receipt: the legacy writer may already have "
					+ "erased the native base-faction mixture and changed allegiance flags. Those "
					+ "facts are irrecoverable and are not guessed; leaving only relinquishes the "
					+ "exact realm slot still proved here.}}");
			}
			else if (Phase == KingdomCitizenshipPhase.Diverged)
			{
				E.Postfix.Append("\n{{R|Citizenship receipt diverged: the realm no longer owns the "
					+ "allegiance value it recorded, so it will not overwrite the body's live state.}}");
			}
			return base.HandleEvent(E);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomCitizenship));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomCitizenship));
			OwnerRealmId = OwnerRealmId ?? "";
			OwnerSettlementId = OwnerSettlementId ?? "";
			FactionId = FactionId ?? "";
			BodyObjectId = BodyObjectId ?? "";
			Fault = Fault ?? "";
		}
	}
}

namespace ThousandAndFirst
{
	/// <summary>Engine edge for the reversible base-slot contract.</summary>
	public static class KingdomCitizenship
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
