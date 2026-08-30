using System;

using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The pre-mutation poison cut.
	/// <para>
	/// Gallery staging rolls back the objects it created but does not journal ground-layer cells, so
	/// a crash, throw, or save cut between ground mutation and the owner's creation can leave an
	/// altered zone with no owner to find. Writing the attempt marker BEFORE the mutating call makes
	/// that state detectable and permanently non-retryable: a poisoned profile is replaced by a new
	/// dev game, never repaired in place.
	/// </para>
	/// <para>
	/// Presence means KEY presence. The verdict itself belongs to the pure
	/// <see cref="KingdomScenarioStateShape"/> classifier; this shard only reads the two keys and
	/// re-proves their exact table shape after each write.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioTransactionMarker
	{
		/// <summary>Written 1 before the mutating call, advanced to 2 on its successful return.</summary>
		internal const string TransactionState = "r_TAF_ScenarioTransaction_v1";

		/// <summary>
		/// The committed key. Absent while merely attempted; exactly 1 once committed. Two keys
		/// rather than one so a half-written commit is a disagreement instead of a value.
		/// </summary>
		internal const string RealizedState = "r_TAF_ScenarioRealized_v1";

		/// <summary>
		/// Stable refusal codes, carried beside the prose exactly as
		/// <see cref="KingdomScenarioAdvance"/> carries its own. A host asserting on this verb's
		/// journal row binds to the code; the wording beside it is free to improve without
		/// silently retargeting somebody's expectation.
		/// </summary>
		internal const string CodeAttempted = "taf-scenario-transaction-attempted";

		internal const string CodeCommitted = "taf-scenario-transaction-committed";

		internal const string CodeTorn = "taf-scenario-transaction-torn";

		/// <summary>The marker did not read back in the exact shape a marker has.</summary>
		internal const string CodeUnwritable = "taf-scenario-transaction-marker-unwritable";

		/// <summary>Reads the durable transaction state, failing closed on every ambiguity.</summary>
		internal static KingdomScenarioTransactionShape Observe(out string Detail)
		{
			return KingdomScenarioStateShape.Transaction(
				KingdomScenarioDurableState.Observe(TransactionState),
				KingdomScenarioDurableState.Observe(RealizedState), out Detail);
		}

		/// <summary>
		/// Whether a run may proceed. Every state except None refuses, and refuses permanently:
		/// Attempted with no owner and Attempted over partly-altered ground are indistinguishable
		/// from outside, so both are treated as the worse case.
		/// </summary>
		internal static bool TryBegin(out string Failure)
		{
			string detail;
			switch (Observe(out detail))
			{
				case KingdomScenarioTransactionShape.None:
					break;
				case KingdomScenarioTransactionShape.Attempted:
					return Refuse(CodeAttempted,
						"this profile already attempted its production transaction. The "
						+ "ground may have been altered and is not journalled, so it can never be "
						+ "retried here; prepare a new dev game", out Failure);
				case KingdomScenarioTransactionShape.Committed:
					return Refuse(CodeCommitted,
						"this profile already committed its production transaction; "
						+ "re-running a scenario is a new dev game, never a second pass", out Failure);
				default:
					return Refuse(CodeTorn, "the scenario transaction marker is torn ("
						+ (detail ?? "unknown fault") + "); this profile is poisoned and cannot run",
						out Failure);
			}
			The.Game.SetIntGameState(TransactionState, KingdomScenarioStateShape.AttemptedValue);
			// Reprove the exact table shape, not the value alone: a write landing beside a key of
			// another durable type is torn, and torn must be caught here rather than at next boot.
			if (!KingdomScenarioDurableState.ProvesExactInt(TransactionState,
					KingdomScenarioStateShape.AttemptedValue)
				|| Observe(out detail) != KingdomScenarioTransactionShape.Attempted)
				return Refuse(CodeUnwritable,
					"the attempt marker did not read back as exactly one int key; refusing "
					+ "to mutate without a durable record that the attempt happened", out Failure);
			Failure = null;
			return true;
		}

		/// <summary>
		/// Advances a successful mutating return to Committed. Called before any capture or
		/// reporting, so a later failure can never look like an uncommitted transaction.
		/// </summary>
		internal static bool TryCommit(out string Failure)
		{
			string detail;
			The.Game.SetIntGameState(TransactionState, KingdomScenarioStateShape.CommittedValue);
			The.Game.SetIntGameState(RealizedState, KingdomScenarioStateShape.MarkerValue);
			if (!KingdomScenarioDurableState.ProvesExactInt(TransactionState,
					KingdomScenarioStateShape.CommittedValue)
				|| !KingdomScenarioDurableState.ProvesExactInt(RealizedState,
					KingdomScenarioStateShape.MarkerValue)
				|| Observe(out detail) != KingdomScenarioTransactionShape.Committed)
				return Refuse(CodeUnwritable,
					"the commit marker did not read back as exactly the two int keys",
					out Failure);
			Failure = null;
			return true;
		}

		/// <summary>
		/// One refusal, code first. The code is the assertable half and the prose is the readable
		/// half; both callers of this shard hand the whole string to the journal unchanged.
		/// </summary>
		private static bool Refuse(string Code, string Message, out string Failure)
		{
			Failure = "[" + Code + "] " + Message;
			return false;
		}
	}
}
