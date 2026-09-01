namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>Read-only proof for recovery. Unlike the freeze APIs, this never upgrades a
		/// legacy or partially written receipt while deciding whether destructive cleanup may begin.</summary>
		internal static bool ExactLodgeMarketSourceReceipt(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Open)
		{
			KingdomLifecycleLodgeTerminalReceipt receipt = Open?.LodgeTerminal;
			return ExactOperationAuthority(Book, Open)
				&& Open.Action == KingdomLifecycleAction.Lodge && receipt != null
				&& receipt.MarketSourcePrepared
					>= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
				&& receipt.MarketSourcePrepared
					<= KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				&& LodgeTerminalShape(Open, false);
		}

		internal static bool TryCommitLodgeMarketSource(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, string sourceBodyObjectId, int sourceResidentId,
			int marketTier, string intent, bool sourceDead)
		{
			if (!ExactOperationAuthority(book, op) || op.Action != KingdomLifecycleAction.Lodge
				|| op.Phase != KingdomLifecyclePhase.DomainIntent || op.LodgeTerminal == null
				|| op.LodgeTerminal.State != KingdomLifecycleLodgeTerminalState.ResidentSourceProved)
				return false;
			KingdomLifecycleLodgeTerminalReceipt r = op.LodgeTerminal;
			int terminal = sourceDead ? KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				: KingdomLifecycleLodgeTerminalReceipt.MarketCommitted;
			if (r.MarketSourcePrepared == terminal) return r.MarketSourceBodyObjectId
				== sourceBodyObjectId && r.MarketSourceResidentId == sourceResidentId
				&& r.MarketTier == marketTier && r.MarketIntent == intent
				&& LodgeTerminalShape(op, false);
			if (r.MarketSourcePrepared != KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
				|| r.MarketSourceBodyObjectId != sourceBodyObjectId
				|| r.MarketSourceResidentId != sourceResidentId || r.MarketTier != marketTier
				|| r.MarketIntent != intent || !LodgeTerminalShape(op, false)) return false;
			r.MarketSourcePrepared = terminal;
			r.MarketSourceProofId = LodgeMarketSourceProof(op, r);
			return ExactOperationAuthority(book, op);
		}
	}
}
