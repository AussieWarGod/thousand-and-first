namespace ThousandAndFirst
{
	/// <summary>Farthest physical boundary completed by a Deep or Forge effect.</summary>
	internal enum KingdomPurposeEffectRefineStep : byte
	{
		None = 0,
		FirstRawSpent = 1,
		SecondRawSpent = 2,
		Made = 3,
		Exempt = 255
	}

	/// <summary>Farthest physical boundary completed by a Harvest effect.</summary>
	internal enum KingdomPurposeEffectHarvestStep : byte
	{
		None = 0,
		FirstCropSpent = 1,
		SecondCropSpent = 2,
		ThirdCropSpent = 3,
		SeedMade = 4,
		Milled = 5,
		Exempt = 255
	}

	public static partial class KingdomPurposePortfolioRules
	{
		public const int PurposeEffectRawUnits = 2;
		public const int PurposeEffectRefinedUnits = 1;
		public const int PurposeEffectCropUnits = 3;
		public const int PurposeEffectSeedUnits = 1;
		public const int PurposeEffectMilledCrops = 2;
		public const int PurposeEffectStaplesPerCrop = 3;
		public const int PurposeEffectStapleUnits =
			PurposeEffectMilledCrops * PurposeEffectStaplesPerCrop;
		public const int PurposeEffectNone = 0;
		public const int PurposeEffectExempt = 255;

		public static bool EffectIsOwed(KingdomPurposeKind Kind)
		{
			return Kind == KingdomPurposeKind.Deep || Kind == KingdomPurposeKind.Forge
				|| Kind == KingdomPurposeKind.Harvest;
		}

		public static bool TryEffectRefine(KingdomPurposeKind Kind,
			out KingdomMaterial Raw, out KingdomMaterial Product)
		{
			Raw = KingdomMaterial.Mud;
			Product = KingdomMaterial.Mud;
			if (Kind == KingdomPurposeKind.Deep)
			{
				Raw = KingdomMaterial.Stone;
				Product = KingdomMaterial.ShapedStone;
				return true;
			}
			if (Kind != KingdomPurposeKind.Forge) return false;
			Raw = KingdomMaterial.Scrap;
			Product = KingdomMaterial.WorkedMetal;
			return true;
		}

		public static bool TryEffectHarvest(string Crop, string Seed, string Staple,
			out int Crops, out int Seeds, out int Staples)
		{
			Crops = 0;
			Seeds = 0;
			Staples = 0;
			if (string.IsNullOrEmpty(Crop) || string.IsNullOrEmpty(Seed)
				|| string.IsNullOrEmpty(Staple)) return false;
			Crops = PurposeEffectCropUnits;
			Seeds = PurposeEffectSeedUnits;
			Staples = PurposeEffectStapleUnits;
			return true;
		}

		internal static bool EffectStepIsLegalFor(KingdomPurposeKind Kind, int Value)
		{
			if (Value == PurposeEffectExempt) return Kind >= KingdomPurposeKind.Flesh
				&& Kind <= KingdomPurposeKind.Harvest;
			if (Kind == KingdomPurposeKind.Deep || Kind == KingdomPurposeKind.Forge)
				return Value >= (int)KingdomPurposeEffectRefineStep.None
					&& Value <= (int)KingdomPurposeEffectRefineStep.Made;
			if (Kind == KingdomPurposeKind.Harvest)
				return Value >= (int)KingdomPurposeEffectHarvestStep.None
					&& Value <= (int)KingdomPurposeEffectHarvestStep.Milled;
			return (Kind == KingdomPurposeKind.Flesh || Kind == KingdomPurposeKind.Chrome)
				&& Value == 0;
		}

		internal static bool TryEffectTerminalStep(KingdomPurposeKind Kind, out int Step)
		{
			Step = Kind == KingdomPurposeKind.Deep || Kind == KingdomPurposeKind.Forge
				? (int)KingdomPurposeEffectRefineStep.Made
				: Kind == KingdomPurposeKind.Harvest
					? (int)KingdomPurposeEffectHarvestStep.Milled : 0;
			return EffectIsOwed(Kind);
		}

		internal static bool TryEffectPenultimateStep(KingdomPurposeKind Kind, out int Step)
		{
			Step = Kind == KingdomPurposeKind.Deep || Kind == KingdomPurposeKind.Forge
				? (int)KingdomPurposeEffectRefineStep.SecondRawSpent
				: Kind == KingdomPurposeKind.Harvest
					? (int)KingdomPurposeEffectHarvestStep.SeedMade : 0;
			return EffectIsOwed(Kind);
		}

		internal static bool EffectStepMonotone(KingdomPurposeOperationReceipt Before,
			KingdomPurposeOperationReceipt After)
		{
			if (Before == null || After == null || Before.SourceKind != After.SourceKind
				|| !EffectStepIsLegalFor(Before.SourceKind, Before.EffectStep)
				|| !EffectStepIsLegalFor(After.SourceKind, After.EffectStep)) return false;
			if (Before.EffectStep == PurposeEffectExempt)
				return After.EffectStep == PurposeEffectExempt;
			if (After.EffectStep == PurposeEffectExempt
				|| After.EffectStep < Before.EffectStep) return false;
			if (Before.Phase == After.Phase)
			{
				if (!TryEffectTerminalStep(Before.SourceKind, out int samePhaseTerminal))
					return After.EffectStep <= Before.EffectStep + 1;
				return After.EffectStep <= Before.EffectStep + 1
					&& (After.EffectStep != samePhaseTerminal
						|| Before.EffectStep == samePhaseTerminal);
			}
			if (After.EffectStep == Before.EffectStep) return true;
			return Before.Phase == KingdomPurposeOperationPhase.EffectPending
				&& After.Phase == KingdomPurposeOperationPhase.EffectApplied
				&& TryEffectPenultimateStep(Before.SourceKind, out int penultimate)
				&& TryEffectTerminalStep(Before.SourceKind, out int terminal)
				&& Before.EffectStep == penultimate && After.EffectStep == terminal;
		}

		internal static bool EffectPhaseCoherent(KingdomPurposeOperationReceipt Operation)
		{
			if (Operation == null || !EffectStepIsLegalFor(
				Operation.SourceKind, Operation.EffectStep)) return false;
			if (Operation.EffectStep == PurposeEffectExempt) return true;
			if (!EffectIsOwed(Operation.SourceKind))
				return Operation.EffectStep == PurposeEffectNone;
			if (Operation.Phase < KingdomPurposeOperationPhase.EffectPending)
				return Operation.EffectStep == PurposeEffectNone;
			if (!TryEffectTerminalStep(Operation.SourceKind, out int terminal)) return false;
			if (Operation.Phase == KingdomPurposeOperationPhase.EffectPending)
				return Operation.EffectStep < terminal;
			return Operation.EffectStep == terminal;
		}
	}
}
