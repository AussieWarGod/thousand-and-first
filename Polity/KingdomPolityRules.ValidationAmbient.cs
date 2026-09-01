namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidAmbientTransaction(KingdomPolityCohortPlan C,
			bool AllowsMigratedUnresolved)
		{
			bool weekly = KingdomPolityDispatchRules.IsScheduled(C);
			KingdomPolityAmbientTransaction t = C.AmbientTransaction;
			if (!weekly) return t == null;
			// A weekly visit with no frozen semantic transaction is legal and simply
			// non-interactive; a carried transaction must be exact.
			if (t == null) return true;
			if (t != null && t.Version == 0)
				return AllowsMigratedUnresolved && Text(t.Fault, true) &&
					string.IsNullOrEmpty(t.TransactionId) &&
					t.Purpose == KingdomPolityCohortPurpose.None &&
					string.IsNullOrEmpty(t.SourcePolityId) &&
					string.IsNullOrEmpty(t.SourceSettlementId) &&
					string.IsNullOrEmpty(t.SourceSettlementName) &&
					string.IsNullOrEmpty(t.SourceZoneId) &&
					string.IsNullOrEmpty(t.DestinationSettlementId) &&
					string.IsNullOrEmpty(t.DestinationSettlementName) &&
					string.IsNullOrEmpty(t.DestinationZoneId) &&
					string.IsNullOrEmpty(t.LocalLocusRef) && t.FactRefs != null &&
					t.FactRefs.Count == 0 && t.ManifestRefs != null &&
					t.ManifestRefs.Count == 0 && t.PhysicalStockObjectIds != null &&
					t.PhysicalStockObjectIds.Count == 0 &&
					string.IsNullOrEmpty(t.SafeDetail) && string.IsNullOrEmpty(t.NewsRef) &&
					t.PreparedTick == 0L && string.IsNullOrEmpty(t.FrozenDigest) &&
					t.TerminalChoice == KingdomPolityAmbientTerminalChoice.None &&
					t.TerminalTick == 0L && string.IsNullOrEmpty(t.TerminalReceiptId) &&
					t.AdmissionHandoff == null;
			if (!KingdomPolityAmbientTransactionRules.Valid(t, C.CohortId, out _)) return false;
			if (t.Purpose != C.Purpose || t.SourcePolityId != C.PolityId ||
				t.DestinationSettlementId != C.SurfaceRef) return false;
			bool terminal = t.TerminalChoice != KingdomPolityAmbientTerminalChoice.None;
			return terminal == (C.Phase == KingdomPolityCohortPhase.Concluded &&
				C.RewardEventId == t.TerminalReceiptId) || !terminal;
		}
	}
}
