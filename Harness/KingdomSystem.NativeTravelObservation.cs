using ThousandAndFirst.Harness;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		// SemanticPassActive is a retained durable receipt, not a live call-stack flag.
		// Reuse the production completion/publication law and its private required mask.
		internal bool NativeTravelSemanticPauseReady()
			=> !KingdomSurvey.HasBoundPass && KingdomScenarioTravelRules.SemanticPauseReady(
				SemanticPassActive, SemanticPassStartedTick, SemanticPassZoneId,
				SemanticPassCompletedMask, SemanticRequiredMask, LastSemanticTick,
				SemanticPassZoneId);
	}
}
