using System;

using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The only place the harness asks the engine whether a durable key EXISTS.
	/// <para>
	/// Qud exposes <c>HasStringGameState</c>, <c>HasIntGameState</c>, <c>HasInt64GameState</c>,
	/// <c>HasObjectGameState</c>, and <c>HasBooleanGameState</c>. Those are presence.
	/// <c>GetIntGameState(name, 0)</c> and <c>GetStringGameState(name)</c> are not: they answer
	/// identically for a key that was never written and for a key explicitly holding zero or the
	/// empty string, so a corrupt save would read as a fresh one and could found ordinary-play
	/// anchor evidence from a scenario-built state.
	/// </para>
	/// <para>
	/// This reader takes no decisions. It fills the observation and hands it to the pure
	/// <see cref="KingdomScenarioStateShape"/> classifier, so every shape it can produce is one the
	/// test assembly can execute without a live game.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioDurableState
	{
		/// <summary>
		/// One key's presence across every durable type table. Null when there is no game, which the
		/// classifier reads as torn rather than as absence.
		/// </summary>
		internal static KingdomDurableKeyObservation Observe(string Key)
		{
			XRLGame game = The.Game;
			if (game == null || string.IsNullOrEmpty(Key)) return null;
			bool hasString = game.HasStringGameState(Key);
			bool hasInt = game.HasIntGameState(Key);
			return new KingdomDurableKeyObservation
			{
				HasString = hasString,
				String = hasString ? game.GetStringGameState(Key) : null,
				HasInt = hasInt,
				Int = hasInt ? game.GetIntGameState(Key, 0) : 0,
				HasInt64 = game.HasInt64GameState(Key),
				HasObject = game.HasObjectGameState(Key),
				HasBoolean = game.HasBooleanGameState(Key)
			};
		}

		/// <summary>
		/// Re-proves a key's exact table shape after a write. A write that lands beside an existing
		/// key of another type produces a torn shape, and this is where that is caught rather than at
		/// the next boot.
		/// </summary>
		internal static bool ProvesExactInt(string Key, int Expected)
		{
			string detail;
			KingdomDurableKeyObservation observed = Observe(Key);
			return KingdomScenarioStateShape.Classify(observed, out detail)
					== KingdomDurableKeyShape.ExactInt
				&& observed.Int == Expected;
		}

		/// <summary>
		/// The one fail-closed proof that this game may found ORDINARY-PLAY anchor evidence.
		/// <para>
		/// Used by the capture command and by the operator-facing anchor status, so the two can
		/// never disagree about whether a save is eligible. An absent stamp alone is not eligibility:
		/// a scenario profile whose stamp was deleted or torn still carries the transaction marker
		/// and the request key it was prepared with.
		/// </para>
		/// </summary>
		internal static bool OrdinaryAnchorEligible(out string Refusal)
		{
			return KingdomScenarioStateShape.OrdinaryAnchorEligible(
				Observe(KingdomScenarioProvenanceRules.ProvenanceState),
				Observe(KingdomScenarioRealizer.StampedState),
				Observe(KingdomScenarioTransactionMarker.TransactionState),
				Observe(KingdomScenarioTransactionMarker.RealizedState),
				Observe(KingdomScenarioNewGameGate.RequestState), out Refusal);
		}

		/// <summary>Re-proves a text key's exact table shape and exact value after a write.</summary>
		internal static bool ProvesExactText(string Key, string Expected)
		{
			string detail;
			KingdomDurableKeyObservation observed = Observe(Key);
			return KingdomScenarioStateShape.Classify(observed, out detail)
					== KingdomDurableKeyShape.ExactString
				&& string.Equals(observed.String, Expected, StringComparison.Ordinal);
		}
	}
}
