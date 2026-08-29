using XRL.UI;

namespace ThousandAndFirst
{
	/// <summary>Observes the global presentation choice into the save-local no-backlog latch.</summary>
	public static class KingdomPolityPresentationRuntime
	{
		public const string OptionId = "r_TAF_OptionPolityPresentation";

		public static KingdomPolityPresentationState ConfiguredState
		{
			get { return Options.GetOption(OptionId, "Yes") == "No"
				? KingdomPolityPresentationState.Disabled
				: KingdomPolityPresentationState.Enabled; }
		}

		public static bool TryObserve(KingdomSystem System, long Tick,
			out bool EnabledForNewCauses, out string Failure)
		{
			EnabledForNewCauses = false; Failure = null;
			if (System?.PolityLedger == null || Tick < 0L)
			{
				Failure = "polity presentation has no valid realm authority"; return false;
			}
			KingdomPolityPresentationState desired = ConfiguredState;
			if (System.PolityLedger.Options.Presentation != desired &&
				!KingdomPolityRules.TryObservePresentation(System.PolityLedger,
					desired, Tick, out Failure)) return false;
			EnabledForNewCauses = KingdomPolityRules.CanEmitOptionalProjection(
				System.PolityLedger, Tick);
			return true;
		}
	}
}
