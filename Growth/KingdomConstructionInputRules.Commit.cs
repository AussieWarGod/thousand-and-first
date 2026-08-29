namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		/// <summary>Builds the one exact claim projection authorized by a physically closed routed
		/// receipt. No live catalogue, source, or equivalent object participates in this fold.</summary>
		public static bool TryCommittedClaims(KingdomConstructionInputReceipt Receipt,
			KingdomConstructionClaims Current, out KingdomConstructionClaims Next)
		{
			Next = null;
			KingdomConstructionInputFault fault;
			if (Receipt == null || Current == null || !TryValidate(Receipt, out fault)
				|| (Receipt.TxPhase != KingdomConstructionInputTxPhase.Closing
					&& Receipt.TxPhase != KingdomConstructionInputTxPhase.Committed)
				|| !ClaimsBeforeExact(Receipt, Current.WaterSpent, Current.WaterOutstanding,
					Current.WaterLost, Current.MaterialSpent, Current.MaterialOutstanding,
					Current.MaterialLost)) return false;

			long waterSpent = (long)Receipt.PriorWaterSpent + Receipt.WaterRequested;
			long waterLost = (long)Receipt.PriorWaterLost + Receipt.WaterRequested;
			KingdomMaterialDebitCost priorSpent;
			KingdomMaterialDebitCost requested;
			KingdomMaterialDebitCost priorLost;
			KingdomMaterialDebitCost physical;
			KingdomMaterialDebitCost spent;
			KingdomMaterialDebitCost lost;
			if (waterSpent > int.MaxValue || waterLost > int.MaxValue
				|| !TryParseMaterialClaim(Receipt.PriorMaterialSpentClaim, out priorSpent)
				|| !TryParseMaterialClaim(Receipt.MaterialRequestedClaim, out requested)
				|| !TryParseMaterialClaim(Receipt.PriorMaterialLostClaim, out priorLost)
				|| !TryPhysicalLoss(Receipt, out physical)
				|| !TryAdd(priorSpent, requested, out spent)
				|| !TryAdd(priorLost, physical, out lost)) return false;

			Next = Current.Copy();
			Next.WaterSpent = (int)waterSpent;
			Next.WaterOutstanding = 0;
			Next.WaterLost = (int)waterLost;
			Next.MaterialSpent = spent.ToClaimString();
			Next.MaterialOutstanding = new KingdomMaterialDebitCost().ToClaimString();
			Next.MaterialLost = lost.ToClaimString();
			Next.Exact = true;
			return Receipt.TxPhase == KingdomConstructionInputTxPhase.Closing
				|| CommittedClaimsExact(Receipt, Next.WaterSpent, Next.WaterOutstanding,
					Next.WaterLost, Next.MaterialSpent, Next.MaterialOutstanding,
					Next.MaterialLost);
		}
	}
}
