namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		/// <summary>
		/// In-place work never surrenders or shifts standing building ground. The comparison uses
		/// the common main-relative frame so a declared lot expansion remains deterministic.
		/// Additive work also never weakens aggregate shelter; renovation may change it.
		/// </summary>
		private static bool TryValidateFootprintTransition(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, ArchitectureTransitionMode Mode, out string Failure)
		{
			Failure = null;
			int beforeX1 = Before.FootprintX - Before.MainX;
			int beforeY1 = Before.FootprintY - Before.MainY;
			int beforeX2 = beforeX1 + Before.FootprintWidth - 1;
			int beforeY2 = beforeY1 + Before.FootprintHeight - 1;
			int afterX1 = After.FootprintX - After.MainX;
			int afterY1 = After.FootprintY - After.MainY;
			int afterX2 = afterX1 + After.FootprintWidth - 1;
			int afterY2 = afterY1 + After.FootprintHeight - 1;
			if (afterX1 > beforeX1 || afterY1 > beforeY1
				|| afterX2 < beforeX2 || afterY2 < beforeY2)
				return Fail("in-place transition shrinks or shifts the frozen building footprint; "
					+ "replacement requires strike and restake", out Failure);

			bool beforeRoof = KnownRoof(Before.BaseRoof);
			bool afterRoof = KnownRoof(After.BaseRoof);
			if (beforeRoof != afterRoof)
				return Fail("layout delta mixes legacy and current frozen roof authority", out Failure);
			if (beforeRoof && KingdomArchitectureTransitionRules.PreservesStandingFabric(Mode)
				&& KingdomPlotRules.ShelterRank(After.BaseRoof)
					< KingdomPlotRules.ShelterRank(Before.BaseRoof))
				return Fail("additive transition weakens the frozen catalogue roof", out Failure);
			return true;
		}
	}
}
