using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryRetireCompletedPurposeEffect(KingdomPurposePairReceipt Pair,
			out string Failure)
		{
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (operation == null || !KingdomPurposePortfolioRules.EffectIsOwed(
				operation.SourceKind)
				|| operation.EffectStep == KingdomPurposePortfolioRules.PurposeEffectExempt)
				return true;
			if (operation.Phase != KingdomPurposeOperationPhase.EffectApplied
				|| !KingdomPurposePortfolioRules.TryEffectTerminalStep(operation.SourceKind,
					out int terminal) || operation.EffectStep != terminal)
				return Fail("Only a completed bounded effect may retire its high-water record.",
					out Failure);
			if (!ExactPublishedPortfolioPair(Pair))
				return Fail("The purpose-pair register changed before effect retirement.",
					out Failure);
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!TryPurposeEffectContext(system, operation,
				out KingdomPurposeEffectRuntimeContext context, out Failure)
				|| !PurposeEffectEvidenceOnlyOnWorkOrProducts(context,
					out IList<GameObject> loaded, out Failure)
				|| !TryPurposeEffectScope(operation, out string receipt, out _)
				|| !TryReadPurposeEffectAttempt(context.Work, receipt, out _, out bool attemptPresent)
				|| !TryReadPurposeEffectProducts(context.Work, receipt,
					out KingdomPurposeEffectProductRecord record)) return false;
			bool anyEvidence = false;
			for (int i = 0; i < loaded.Count; i++) anyEvidence |= AnyPurposeEffectField(loaded[i]);
			if (!anyEvidence) return true;
			if (attemptPresent || OwnedFieldPresent(context.Work, PortfolioEffectReadyProperty)
				|| OwnedFieldPresent(context.Work, PortfolioEffectOfferProperty)
				|| PurposeEffectIsFaulted(context.Work))
				return Fail("A completed bounded effect still has active attempt or fault evidence.",
					out Failure);
			for (int i = 0; i < loaded.Count; i++)
				if (!ReferenceEquals(loaded[i], context.Work)
					&& AnyPurposeEffectField(loaded[i]))
					return Fail("A completed bounded effect still has a protected physical product.",
						out Failure);
			if (!CompletedPurposeEffectProductCount(operation, record))
				return Fail("The completed effect's durable product high-water is incomplete.",
					out Failure);
			if (!ClearPurposeEffectProducts(context.Work, receipt))
				return Fail("The bounded-effect high-water record could not retire.", out Failure);
			if (!PurposeEffectEvidenceOnlyOnWorkOrProducts(context, out loaded, out Failure))
				return false;
			for (int i = 0; i < loaded.Count; i++)
				if (AnyPurposeEffectField(loaded[i]))
					return Fail("Bounded-effect evidence survived terminal retirement.", out Failure);
			return true;
		}

		private static bool CompletedPurposeEffectProductCount(
			KingdomPurposeOperationReceipt Operation,
			KingdomPurposeEffectProductRecord Record)
		{
			if (Operation.SourceKind == KingdomPurposeKind.Deep
				|| Operation.SourceKind == KingdomPurposeKind.Forge)
				return Record.Refined == KingdomPurposePortfolioRules.PurposeEffectRefinedUnits
					&& Record.Seed == 0 && Record.Staple == 0;
			return Operation.SourceKind == KingdomPurposeKind.Harvest && Record.Refined == 0
				&& Record.Seed == KingdomPurposePortfolioRules.PurposeEffectSeedUnits
				&& Record.Staple == KingdomPurposePortfolioRules.PurposeEffectStapleUnits;
		}
	}
}
