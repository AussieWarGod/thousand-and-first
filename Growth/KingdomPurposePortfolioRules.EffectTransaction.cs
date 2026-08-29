namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>A callback report never outranks the measured physical aftermath. A throw is
		/// still ambiguous because arbitrary engine code ran after the mutation.</summary>
		internal static KingdomPurposeEffectCallbackAftermath ClassifyEffectProductAftermath(
			bool Offered, bool Threw, bool PhysicallySettled, bool SafelyUnowned)
		{
			if (!Offered) return KingdomPurposeEffectCallbackAftermath.Unavailable;
			if (Threw) return KingdomPurposeEffectCallbackAftermath.Ambiguous;
			if (PhysicallySettled) return KingdomPurposeEffectCallbackAftermath.Settled;
			return SafelyUnowned ? KingdomPurposeEffectCallbackAftermath.Unavailable
				: KingdomPurposeEffectCallbackAftermath.Ambiguous;
		}

		internal static KingdomPurposeEffectCallbackAftermath ClassifyEffectDebitAftermath(
			bool Offered, bool Threw, bool BeforeStillExact, bool AfterExactlyOne)
		{
			if (!Offered) return KingdomPurposeEffectCallbackAftermath.Unavailable;
			if (Threw) return KingdomPurposeEffectCallbackAftermath.Ambiguous;
			if (AfterExactlyOne) return KingdomPurposeEffectCallbackAftermath.Settled;
			return BeforeStillExact ? KingdomPurposeEffectCallbackAftermath.Unavailable
				: KingdomPurposeEffectCallbackAftermath.Ambiguous;
		}

		internal static KingdomPurposeEffectAttemptState ClassifyEffectAttempt(bool Present,
			bool Ours, bool BeforeStillExact, bool AfterExactlyOne, bool FaultPresent)
		{
			if (FaultPresent) return KingdomPurposeEffectAttemptState.Ambiguous;
			if (!Present) return KingdomPurposeEffectAttemptState.Clear;
			if (!Ours) return KingdomPurposeEffectAttemptState.Ambiguous;
			if (AfterExactlyOne) return KingdomPurposeEffectAttemptState.Settled;
			return BeforeStillExact ? KingdomPurposeEffectAttemptState.Before
				: KingdomPurposeEffectAttemptState.Ambiguous;
		}

		internal static bool EffectRecipeConserves(KingdomPurposeKind Kind,
			int RawOrCrops, int RefinedOrSeeds, int Staples)
		{
			if (Kind == KingdomPurposeKind.Deep || Kind == KingdomPurposeKind.Forge)
				return RawOrCrops == PurposeEffectRawUnits
					&& RefinedOrSeeds == PurposeEffectRefinedUnits && Staples == 0;
			return Kind == KingdomPurposeKind.Harvest
				&& RawOrCrops == PurposeEffectCropUnits
				&& RefinedOrSeeds == PurposeEffectSeedUnits
				&& Staples == PurposeEffectStapleUnits;
		}
	}
}
