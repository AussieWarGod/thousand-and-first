namespace ThousandAndFirst
{
	/// <summary>
	/// Immutable report a durable job can inspect. <see cref="Spent"/> is the part of the requested
	/// price actually answered; <see cref="Lost"/> is the full physical value removed, including
	/// surplus bits. <see cref="Outstanding"/> is safe to retry only when
	/// <see cref="MeasurementExact"/> is true; otherwise callback damage must be quarantined.
	/// </summary>
	public sealed class KingdomMaterialDebitResult
	{
		public readonly KingdomMaterialDebitOutcome Outcome;
		public readonly KingdomMaterialDebitFault Fault;
		public readonly KingdomMaterialDebitCost Requested;
		public readonly KingdomMaterialDebitCost Spent;
		public readonly KingdomMaterialDebitCost Outstanding;
		public readonly KingdomMaterialDebitCost Lost;
		public readonly int FinalizedSources;
		public readonly string Failure;
		public readonly bool MeasurementExact;

		public KingdomBitTally LostBitYield => Lost.Bits.Copy();

		public bool Exact => Outcome == KingdomMaterialDebitOutcome.ExactCommit;

		public bool Clean => Outcome == KingdomMaterialDebitOutcome.CleanRefusal
			|| Outcome == KingdomMaterialDebitOutcome.CompensatedExact
			|| Outcome == KingdomMaterialDebitOutcome.Cancelled;

		public bool Partial => Outcome == KingdomMaterialDebitOutcome.RecoverablePartial
			|| Outcome == KingdomMaterialDebitOutcome.IrreversiblePartial;

		internal KingdomMaterialDebitResult(KingdomMaterialDebitOutcome Outcome,
			KingdomMaterialDebitFault Fault, KingdomMaterialDebitCost Requested,
			KingdomMaterialDebitCost Spent, KingdomMaterialDebitCost Outstanding,
			KingdomMaterialDebitCost Lost, int FinalizedSources, string Failure,
			bool MeasurementExact = true)
		{
			this.Outcome = Outcome;
			this.Fault = Fault;
			this.Requested = (Requested == null) ? new KingdomMaterialDebitCost() : Requested.Copy();
			this.Spent = (Spent == null) ? new KingdomMaterialDebitCost() : Spent.Copy();
			this.Outstanding = (Outstanding == null) ? new KingdomMaterialDebitCost() : Outstanding.Copy();
			this.Lost = (Lost == null) ? new KingdomMaterialDebitCost() : Lost.Copy();
			this.FinalizedSources = (FinalizedSources > 0) ? FinalizedSources : 0;
			this.Failure = Failure;
			this.MeasurementExact = MeasurementExact;
		}
	}
}
