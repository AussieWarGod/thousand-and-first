using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static string PurposeEffectDisclosure(KingdomPurposeKind Kind)
		{
			if (Kind == KingdomPurposeKind.Deep)
				return "\nBounded effect: {{C|2 stone}} dressed into {{C|1 shaped stone}} at this work's own input store; {{C|1}} lost as spoil.";
			if (Kind == KingdomPurposeKind.Forge)
				return "\nBounded effect: {{C|2 scrap}} smelted into {{C|1 worked metal}} at this work's own input store; {{C|1}} lost as slag.";
			if (Kind != KingdomPurposeKind.Harvest) return "";
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string crop = KingdomCrops.CropName(KingdomData.CropForStyle(system?.Style));
			string seed = KingdomCrops.CropName(KingdomData.SeedForStyle(system?.Style));
			string staple = KingdomCrops.CropName(KingdomRules.PreservedStapleFor(
				KingdomData.CropForStyle(system?.Style)));
			return "\nBounded effect: {{C|3 " + crop
				+ "}} from this granary's own shelves become {{C|1 " + seed
				+ "}} and {{C|6 preserved measures}} of " + staple
				+ "; one crop pays the seed return and two mill at three measures each.";
		}

		private static string PurposeEffectState(KingdomPurposePairReceipt Pair)
		{
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (operation == null || !KingdomPurposePortfolioRules.EffectIsOwed(
				operation.SourceKind)) return "";
			if (operation.EffectStep == KingdomPurposePortfolioRules.PurposeEffectExempt)
				return "\nBounded effect: this operation predates the bounded effect and owes none.";
			if (operation.SourceKind == KingdomPurposeKind.Deep
				|| operation.SourceKind == KingdomPurposeKind.Forge)
				return RefinePurposeEffectState(operation);
			return HarvestPurposeEffectState(operation);
		}

		private static string RefinePurposeEffectState(KingdomPurposeOperationReceipt Operation)
		{
			string raw = Operation.SourceKind == KingdomPurposeKind.Deep ? "stone" : "scrap";
			string product = Operation.SourceKind == KingdomPurposeKind.Deep
				? "shaped stone" : "worked metal";
			if (Operation.EffectStep == (int)KingdomPurposeEffectRefineStep.Made)
				return "\nBounded effect: {{C|2 " + raw + "}} spent; {{C|1 "
					+ product + "}} made at this work's own input store.";
			if (Operation.EffectStep == (int)KingdomPurposeEffectRefineStep.None)
				return "\nBounded effect: {{C|not yet run}}; {{C|2 " + raw
					+ "}} to spend for {{C|1 " + product + "}}.";
			if (Operation.EffectStep == (int)KingdomPurposeEffectRefineStep.SecondRawSpent)
			{
				if (TryPurposeEffectHighWater(Operation,
					out KingdomPurposeEffectProductRecord record))
					return "\nBounded effect: {{C|2 " + raw + "}} spent; {{C|"
						+ record.Refined + " of 1 " + product
						+ "}} made and released; semantic publication may still be pending.";
				return "\nBounded effect: {{C|2 " + raw
					+ "}} spent; exact product high-water is unavailable until local custody returns.";
			}
			return "\nBounded effect: {{C|" + Operation.EffectStep + " of 2 " + raw
				+ "}} spent; the " + product + " is not yet made.";
		}

		private static string HarvestPurposeEffectState(KingdomPurposeOperationReceipt Operation)
		{
			if (Operation.EffectStep == (int)KingdomPurposeEffectHarvestStep.Milled)
				return "\nBounded effect: {{C|3 crops}} drawn; {{C|1 seed}} and {{C|6 preserved measures}} made in this granary's own store.";
			if (Operation.EffectStep == (int)KingdomPurposeEffectHarvestStep.SeedMade)
			{
				bool known = TryPurposeEffectHighWater(Operation,
					out KingdomPurposeEffectProductRecord record);
				return known
					? "\nBounded effect: {{C|3 crops}} drawn and {{C|1 seed}} made; {{C|"
						+ record.Staple + " of 6 preserved measures}} are made and released from this granary's own store."
					: "\nBounded effect: {{C|3 crops}} drawn and {{C|1 seed}} made; preserved-measure progress is unavailable until exact local custody returns.";
			}
			if (Operation.EffectStep == (int)KingdomPurposeEffectHarvestStep.ThirdCropSpent)
			{
				if (TryPurposeEffectHighWater(Operation,
					out KingdomPurposeEffectProductRecord record))
					return "\nBounded effect: {{C|3 crops}} drawn; {{C|" + record.Seed
						+ " of 1 seed}} made and released; semantic publication may still be pending.";
				return "\nBounded effect: {{C|3 crops}} drawn; exact seed high-water is unavailable until local custody returns.";
			}
			if (Operation.EffectStep == (int)KingdomPurposeEffectHarvestStep.None)
				return "\nBounded effect: {{C|not yet run}}; {{C|3 crops}} to draw for {{C|1 seed}} and {{C|6 preserved measures}}.";
			return "\nBounded effect: {{C|" + Operation.EffectStep
				+ " of 3 crops}} drawn; no product is yet released.";
		}

		private static bool TryPurposeEffectHighWater(
			KingdomPurposeOperationReceipt Operation,
			out KingdomPurposeEffectProductRecord Record)
		{
			Record = new KingdomPurposeEffectProductRecord();
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (Operation == null || system == null
				|| !TryPurposeEffectContext(system, Operation,
					out KingdomPurposeEffectRuntimeContext context, out _)
				|| !PurposeEffectEvidenceOnlyOnWorkOrProducts(context, out _, out _)
				|| PurposeEffectIsFaulted(context.Work)
				|| !TryPurposeEffectScope(Operation, out string receipt, out _)
				|| !TryReadPurposeEffectProducts(context.Work, receipt, out Record)) return false;
			return true;
		}
	}
}
