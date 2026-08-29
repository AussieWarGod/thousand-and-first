using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool BeginPurposeEffect(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			if (!TryOperationGround(Pair?.Operation, out _, out GameObject work, out _, out _,
				out _, out _, out Failure)) return false;
			KingdomPurposeOperationReceipt next = Pair.Operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.EffectPending;
			next.EffectBeforeDigest = EffectWitness(work);
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static bool DrivePurposeEffect(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (!TryOperationGround(operation, out Zone zone, out GameObject work, out _, out _,
				out _, out _, out Failure)) return false;
			if (operation.SourceKind == KingdomPurposeKind.Flesh
				|| operation.SourceKind == KingdomPurposeKind.Chrome)
			{
				KingdomPurposeBodyDriveState body = DriveBodyAuthority(
					XRL.The.Game?.GetSystem<KingdomSystem>(), work, operation, out Failure);
				if (body == KingdomPurposeBodyDriveState.Invalid)
					return QuarantinePortfolio(Pair, Failure, out Published, out Failure);
				if (body != KingdomPurposeBodyDriveState.Applied) return false;
			}
			else if (KingdomPurposePortfolioRules.EffectIsOwed(operation.SourceKind)
				&& operation.EffectStep != KingdomPurposePortfolioRules.PurposeEffectExempt)
			{
				if (!KingdomPurposePortfolioRules.TryEffectTerminalStep(operation.SourceKind,
					out int terminal))
					return QuarantinePortfolio(Pair, "The bounded purpose kind has no terminal step.",
						out Published, out Failure);
				if (operation.EffectStep != terminal)
				{
					if (!TryPurposeEffectContext(XRL.The.Game?.GetSystem<KingdomSystem>(), operation,
						out KingdomPurposeEffectRuntimeContext context, out Failure)) return false;
					KingdomPurposeBodyDriveState manual = DriveManualPurposeEffect(context,
						operation, out int nextStep, out Failure);
					if (manual == KingdomPurposeBodyDriveState.Invalid)
						return QuarantinePortfolio(Pair, Failure, out Published, out Failure);
					if (manual != KingdomPurposeBodyDriveState.Applied) return false;
					if (nextStep != operation.EffectStep + 1)
						return QuarantinePortfolio(Pair,
							"The bounded purpose effect did not advance exactly one step.",
							out Published, out Failure);
					if (nextStep != terminal)
					{
						KingdomPurposeOperationReceipt stepped = operation.Copy();
						stepped.EffectStep = nextStep;
						stepped.Revision++;
						return TryPublishOperation(Pair, stepped, Pair.Phase,
							out Published, out Failure);
					}
					operation = operation.Copy();
					operation.EffectStep = nextStep;
				}
			}
			string token = PurposeDigest(operation.PairId, operation.PairEpoch.ToString(),
				operation.OperationId, operation.SourceKind.ToString(),
				operation.DestinationKind.ToString(), "purpose-effect");
			string after = EffectWitness(work, token, operation.Ordinal);
			string observed = EffectWitness(work);
			if (observed != operation.EffectBeforeDigest && observed != after)
				return QuarantinePortfolio(Pair,
					"The purpose-effect owner is neither at its frozen before nor after state.",
					out Published, out Failure);
			if (observed == operation.EffectBeforeDigest)
			{
				work.SetStringProperty(PortfolioEffectReceiptProperty, token);
				work.SetIntProperty(PortfolioEffectOrdinalProperty, operation.Ordinal);
				KingdomSurvey.ObserveChangedInActive(zone, work);
				observed = EffectWitness(work);
			}
			if (observed != after)
				return QuarantinePortfolio(Pair,
					"The purpose-effect callback reached an ambiguous aftermath.",
					out Published, out Failure);
			KingdomPurposeOperationReceipt next = operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.EffectApplied;
			next.EffectAfterDigest = after;
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static string EffectWitness(GameObject Work)
		{
			return EffectWitness(Work, Work.GetStringProperty(PortfolioEffectReceiptProperty),
				Work.GetIntProperty(PortfolioEffectOrdinalProperty));
		}

		private static string EffectWitness(GameObject Work, string Receipt, int Ordinal)
		{
			return PurposeDigest("purpose-effect", Work.ID, Receipt ?? "", Ordinal.ToString());
		}
	}
}
