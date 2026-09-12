namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free verdict on whether a teardown case may order its strike THIS check, proved by
	/// value in DevTests/KingdomTeardownStrikeReadinessTests.cs (both public projects).
	/// <para>
	/// RUN 45 (28a4451). With the case now reading the final building, checkpoint 1 refused
	/// "That building carries another construction receipt." That is production's OrderStrike
	/// refusal (Growth/KingdomMaterials.08.StrikeOrdering.cs:70-92): the building carries its OWN
	/// completed construction receipt, and KingdomConstruction.CanSupersedeTerminalReceipt ->
	/// KingdomConstructionRules.CanSupersedeTerminal (OutboxCas.cs:108-117) answers false while
	/// the terminal row is neither Compacted nor TerminalClosureSettled - exactly the window
	/// right after `plot complete` where the physical closure is still pending (#175: stage=Done
	/// applied=Walls). The fixture's Require turned the refusal into the InvalidOperationException
	/// in the journal. The synthetic bill is stock only (MintBill adds units to the chest; it
	/// stamps no receipt), so no second receipt was ever minted by the harness.
	/// </para>
	/// <para>
	/// RULE. Not built -> keep waiting for the build. Built with no receipt -> strike (nothing to
	/// supersede). Built with a receipt whose row is gone (compacted) -> strike. Built with a
	/// terminal receipt: supersedable -> strike; otherwise -> wait for closure (journaled, never a
	/// refusal - the checkpoint budget is the bound). Built with a NON-terminal receipt that is
	/// not this case's own active strike -> a foreign job holds the building: refuse by name.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownStrikeReadiness
	{
		internal enum Verdict
		{
			WaitBuilt = 0,
			Strike = 1,
			WaitClosure = 2,
			RefuseForeignReceipt = 3,
		}

		internal static Verdict Judge(bool Built, bool ReceiptPresent, bool RowFound,
			bool RowTerminal, bool Supersedable)
		{
			if (!Built) return Verdict.WaitBuilt;
			if (!ReceiptPresent || !RowFound) return Verdict.Strike;
			if (!RowTerminal) return Verdict.RefuseForeignReceipt;
			return Supersedable ? Verdict.Strike : Verdict.WaitClosure;
		}

		/// <summary>Which receipt the strike leg rests on: the building's real one when a row
		/// stands behind it, else none. The fixture never mints a receipt of its own.</summary>
		internal static string ReceiptSource(bool ReceiptPresent, bool RowFound)
		{
			return ReceiptPresent && RowFound ? "real" : (ReceiptPresent ? "real-row-compacted" : "none");
		}
	}
}
