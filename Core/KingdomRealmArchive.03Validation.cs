using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		public bool Validate(out string Failure)
		{
			Failure = null;
			if (!ValidateEnvelope(out Failure)) return false;
			if (Phase == KingdomRealmArchivePhase.None ||
				Phase == KingdomRealmArchivePhase.Quarantined || Quarantined)
				return Refuse("archive phase or quarantine state grants no authority", out Failure);
			KingdomIdentityFault identityFault;
			if (!KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
				RealmIdentityOrigin, RealmIdentityTransactionId, RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick, RealmIdentitySeedHigh, RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone, out identityFault))
				return Refuse("archived realm provenance cannot be reproved (" + identityFault + ")",
					out Failure);
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out identityFault) || !StrictlySorted(SettlementIds))
				return Refuse("archived settlement topology cannot be reproved (" + identityFault + ")",
					out Failure);
			if (SeatOpaque != null || AwayOpaque != null || SecededOpaque != null ||
				SeatWireVersion != KingdomArchivedSettlementCodec.CurrentVersion ||
				AwayWireVersion != KingdomArchivedSettlementCodec.CurrentVersion ||
				SecededWireVersion != KingdomArchivedSettlementCodec.CurrentVersion ||
				Seat == null || Standings == null || RealmPolicyToward == null ||
				RegardSpilloverRemainders == null ||
				RegardSpilloverObservedReputation == null || SettlementTopology == null ||
				!CanonicalTopologyReferences(Seat, SettlementTopology,
					ReadLegacyAwayProjection(), Seceded) ||
				!ExactArchivedSettlements(RealmId, Seat, SettlementTopology, SettlementIds))
				return Refuse("archived settlement graph is opaque, aliased, or lacks exact topology",
					out Failure);
			List<KingdomChronicleReceipt> receipts;
			bool migrated;
			KingdomChronicleRegistryFault registryFault;
			if (!KingdomChronicleReceiptRules.TryParseRegistry(ChronicleRegistry,
				out receipts, out migrated, out registryFault) || migrated)
				return Refuse("archive chronicle receipt graph is not canonical (" + registryFault + ")",
					out Failure);
			string directionalFailure = null;
			if (DirectionalStandingSchemaVersion != 1 ||
				(CallbackAuthoritySchemaVersion != 1 && CallbackAuthoritySchemaVersion != 2) ||
				!DirectionalStandingDigestMatches(out directionalFailure) ||
				!ValidDirectionalStandings(FactionName, Standings, RealmPolicyToward,
				RegardSpilloverRemainders, RegardSpilloverObservedReputation))
				return Refuse("archive directional standings are reserved or malformed" +
					(directionalFailure == null ? "" : " (" + directionalFailure + ")"), out Failure);
			if (CarryBook == null || CarryBook.LegacyIdentity ||
				!string.Equals(CarryBook.RealmId, RealmId, StringComparison.Ordinal) ||
				!KingdomLifecycleRules.CanOwnAuthority(CarryBook) ||
				!TryArchivedRetainedIds(RealmId, Seat, SettlementTopology, Seceded,
					out List<string> retainedIds) ||
				!ExactCarrySettlementIds(CarryBook, retainedIds))
				return Refuse("archive carry authority does not match exact realm identity", out Failure);
			if (!ValidHaulAuthority(Haul))
				return Refuse("archive haul has malformed value or immutable destination evidence",
					out Failure);
			if (!ValidCallback(ExileChronicle) || !ValidCallback(ExileAbility) ||
				!ValidCallback(ReturnChronicle) || !ValidCallback(ReturnReputation) ||
				!ValidCallback(ReturnFeelings) || !ValidCallback(ReturnSeat) ||
				!ValidCallback(ReturnAbility))
				return Refuse("archive callback receipt graph is malformed", out Failure);
			return true;
		}

		/// <summary>Codec safety independent of authority. Quarantined evidence must remain
		/// serializable, otherwise fail-closing one transition would make the whole save unwritable.</summary>
		internal bool ValidateEnvelope(out string Failure)
		{
			Failure = null;
			if (Version != CurrentVersion || DirectionalStandingSchemaVersion < 0 ||
				DirectionalStandingSchemaVersion > 1 ||
				(CallbackAuthoritySchemaVersion != 1 && CallbackAuthoritySchemaVersion != 2) ||
				!Enum.IsDefined(typeof(KingdomRealmArchivePhase), Phase) ||
				(Quarantined != (Phase == KingdomRealmArchivePhase.Quarantined)))
				return Refuse("archive version, phase, or quarantine flag is noncanonical",
					out Failure);
			if (ClosedTick < 0L || ResidentCounter < 0 || LastSliceTick < 0L ||
				DedicationCounter < 0 ||
				ChronicleEntries == null || OutsiderEntries == null ||
				ChronicleEntries.Count > KingdomChronicle.MaxEntries ||
				OutsiderEntries.Count > KingdomChronicle.MaxEntries ||
				!BoundedStrings(ChronicleEntries, KingdomChronicleReceiptRules.MaxEntryChars) ||
				!BoundedStrings(OutsiderEntries, KingdomChronicleReceiptRules.MaxEntryChars) ||
				!BoundedUtf8(RealmId, 256, 1024) ||
				!BoundedUtf8(FactionName, 512, 2048) ||
				!BoundedUtf8(DisplayName, 512, 2048) || !BoundedText(ExileDeed) ||
				SettlementIds == null ||
				SettlementIds.Count > KingdomIdentityRules.MaxSettlements ||
				!BoundedStrings(SettlementIds, 256) ||
				!BoundedUtf8(RealmIdentityTransactionId, 64, 256) ||
				!BoundedUtf8(RealmIdentityLegacyFaction, 512, 2048) ||
				!BoundedUtf8(RealmIdentityFirstClaimedZone, 512, 2048) ||
				ChronicleRegistry == null ||
				!BoundedUtf8(ChronicleRegistry,
					KingdomChronicleReceiptRules.MaxRegistryChars,
					KingdomChronicleReceiptRules.MaxRegistryChars * 4) ||
				!BoundedUtf8(ChronicleRegistryFault, 160, 640) ||
				!BoundedText(Fault) || !BoundedText(DeclaredCreed) || !BoundedText(DishName) ||
				!BoundedText(DishText) || !BoundedText(DishStaple) || !BoundedText(DishSource) ||
				!BoundedOpaque(SeatOpaque) || !BoundedOpaque(AwayOpaque) ||
				!BoundedOpaque(SecededOpaque) || !BoundedStandings(Standings) ||
				!BoundedStandings(RealmPolicyToward) ||
				!BoundedRemainders(RegardSpilloverRemainders) ||
				!BoundedStandings(RegardSpilloverObservedReputation) ||
				!BoundedUtf8(DirectionalStandingDigest, 64, 64) ||
				SettlementTopology == null ||
				CarryBook == null || CarryBook.WireRejected ||
				!ValidBindings(Bindings) || !ValidJobs(Jobs) || !BoundedHaul(Haul) ||
				!ValidCallbackEnvelope(ExileChronicle) || !ValidCallbackEnvelope(ExileAbility) ||
				!ValidCallbackEnvelope(ReturnChronicle) ||
				!ValidCallbackEnvelope(ReturnReputation) ||
				!ValidCallbackEnvelope(ReturnFeelings) ||
				!ValidCallbackEnvelope(ReturnSeat) ||
				!ValidCallbackEnvelope(ReturnAbility))
				return Refuse("archive payload is ragged or exceeds codec bounds", out Failure);
			return true;
		}

	}
}
