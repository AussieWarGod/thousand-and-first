using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// New-game gate for the scenario harness.
	/// <para>
	/// Ordering constraint: this mutator runs at the embark player-mutator step, before the player
	/// is placed in a zone, so it performs no ground mutation. It proves the request, proves the
	/// engine actually generated the world under the declared seed, and writes the stamp. The
	/// ground-touching transaction runs later from the attended runner, which refuses without a
	/// stamp whose resolved-plan digest matches what it is about to do.
	/// </para>
	/// </summary>
	[PlayerMutator]
	public sealed class KingdomScenarioNewGameGate : IPlayerMutator
	{
		/// <summary>Set declaratively by the harness embark mode before world build.</summary>
		internal const string RequestState = "r_TAF_ScenarioRequest_v1";

		/// <summary>
		/// Journal verb column for a gate refusal.
		/// <para>
		/// The gate runs at the embark player-mutator step, long before the auto-runner's first
		/// action opportunity, so a refusal here means the runner never arms and no
		/// <c>SCRIPT-BEGIN</c>, <c>SCRIPT-COMPLETE</c>, or <c>SCRIPT-STOPPED</c> row will ever
		/// land. Without a row of its own, an unattended run that the gate refused is
		/// indistinguishable from one that hung, and a matrix host can only time out on it. This
		/// row is that terminal, and it is written BEFORE the throw for exactly that reason.
		/// </para>
		/// </summary>
		internal const string RefusedRow = "GATE-REFUSED";

		/// <summary>The request key was present in a shape no gate ever wrote.</summary>
		internal const string CodeUnreadableRequest = "taf-scenario-gate-unreadable-request";

		/// <summary>The request was readable and the gate declined to open it.</summary>
		internal const string CodeRefused = "taf-scenario-gate-refused";

		public void mutate(GameObject player)
		{
			string request;
			bool present;
			string detail;
			// An ordinary game has no request KEY at all. A key holding an empty string, or living
			// under the wrong type table, is corruption and may never be read as "no scenario".
			if (!KingdomScenarioRealizer.TryReadRequest(out request, out present, out detail))
			{
				// Fail-open, exactly like every other journal write: a lost row must not also lose
				// the refusal, so the return value is deliberately ignored and the throw stands.
				KingdomScenarioJournal.Append(RefusedRow, false, "[" + CodeUnreadableRequest
					+ "] the scenario request state is unreadable ("
					+ (detail ?? "unknown fault") + ")");
				throw new InvalidOperationException(
					"ThousandAndFirst scenario harness found an unreadable request state ("
					+ (detail ?? "unknown fault") + ").");
			}
			if (!present) return;
			string failure;
			if (!KingdomScenarioRealizer.TryOpen(request, out failure))
			{
				KingdomScenarioJournal.Append(RefusedRow, false, "[" + CodeRefused
					+ "] the gate refused '" + KingdomScenarioRules.Bounded(request) + "': "
					+ failure);
				throw new InvalidOperationException(
					"ThousandAndFirst scenario harness refused to open '"
					+ KingdomScenarioRules.Bounded(request) + "' (" + failure + ").");
			}
		}
	}

	/// <summary>Preflight, seed proof, and stamping. Nothing here touches ground.</summary>
	internal static partial class KingdomScenarioRealizer
	{
		/// <summary>Engine-owned literal seed string, written by the boot module before world build.</summary>
		internal const string EngineSeedState = "OriginalWorldSeed";

		/// <summary>
		/// Write-last presence marker. Paired with the stamp so neither half alone can be believed:
		/// stamp-without-marker and marker-without-stamp are both unreadable, and only total absence
		/// of both keys under every durable type table is ordinary play.
		/// </summary>
		internal const string StampedState = "r_TAF_ScenarioStamped_v1";

		/// <summary>The harness-owned request key, read by exact presence rather than by default.</summary>
		internal static bool TryReadRequest(out string Request, out bool Present, out string Detail)
		{
			return KingdomScenarioStateShape.TryAuthorityText(
				KingdomScenarioDurableState.Observe(KingdomScenarioNewGameGate.RequestState),
				out Request, out Present, out Detail);
		}

		/// <summary>
		/// Proves the request and the engine's own seed evidence on a new game, then stamps. No
		/// ground is touched. An asserted seed is never stamped: the value recorded is the one the
		/// engine reports having generated under.
		/// </summary>
		internal static bool TryOpen(string Request, out string Failure)
		{
			Failure = null;
			string presenceDetail;
			KingdomScenarioStampShape shape = Shape(out presenceDetail);
			if (shape == KingdomScenarioStampShape.Readable)
				return Refuse("this game already carries a scenario stamp; refusing to overwrite it",
					out Failure);
			if (shape == KingdomScenarioStampShape.PresentUnreadable)
				return Refuse("this game already carries scenario provenance in a shape no gate "
					+ "ever wrote (" + (presenceDetail ?? "unknown fault")
					+ "); refusing to overwrite it", out Failure);
			string transactionDetail;
			if (KingdomScenarioTransactionMarker.Observe(out transactionDetail)
				!= KingdomScenarioTransactionShape.None)
				return Refuse("this game already carries a scenario transaction marker ("
					+ (transactionDetail ?? "already attempted or committed") + ")", out Failure);
			KingdomScenarioPlan plan;
			if (!KingdomScenarioRequest.TryPlan(Request, out plan, out Failure)) return false;
			string actualSeed;
			if (!TryProveSeed(plan.Seed, out actualSeed, out Failure)) return false;
			KingdomScenarioProvenance record = new KingdomScenarioProvenance
			{
				ScenarioKey = plan.Key,
				AuthorityClass = plan.AuthorityClass,
				Verbs = plan.Verbs,
				AnchorId = plan.AnchorId,
				KeySetDigest = null,
				Seed = actualSeed,
				ModVersion = KingdomReleaseInfo.Version,
				QudCoreVersion = XRLGame.CoreVersion.ToString(),
				DefinitionDigest = plan.DefinitionDigest,
				PlanDigest = plan.PlanDigest,
				Synthetic = plan.Synthetic
			};
			return TryStamp(record, out Failure);
		}

		/// <summary>
		/// Writes the stamp, then the presence marker, re-proving the exact table shape after each
		/// write so a key landing beside another type is caught here and not at the next boot.
		/// </summary>
		private static bool TryStamp(KingdomScenarioProvenance Record, out string Failure)
		{
			Failure = null;
			// The ONE write path, shared with the measured republication. Two encode/write/readback
			// sequences that agree today are not one authority; they are a divergence waiting.
			if (!TryWriteProvenance(Record, out Failure)) return false;
			// Written last, so presence can never be inferred from a half-written stamp.
			The.Game.SetIntGameState(StampedState, KingdomScenarioStateShape.MarkerValue);
			string detail;
			if (!KingdomScenarioDurableState.ProvesExactInt(StampedState,
					KingdomScenarioStateShape.MarkerValue)
				|| Shape(out detail) != KingdomScenarioStampShape.Readable)
				return Refuse("the scenario presence marker did not read back as exactly one int key",
					out Failure);
			KingdomLog.Log("[TAF scenario] opened key=" + Record.ScenarioKey
				+ " authority=" + Record.AuthorityClass + " verbs=" + Record.Verbs
				+ " seed=" + Record.Seed + " plan=" + Record.PlanDigest
				+ " synthetic=" + (Record.Synthetic ? "1" : "0"));
			return true;
		}

		/// <summary>
		/// Reads the engine's own seed evidence and proves the world was generated under the
		/// declared seed. <c>KingdomScenarioFastEmbarkModule</c> sets the seed in-chargen from the
		/// same sealed request, but that is an ASSERTION and this is the proof: the value recorded is
		/// the one the engine reports having generated under, and a mismatch is a refusal rather than
		/// a stamped claim. Nothing here trusts the setter.
		/// </summary>
		internal static bool TryProveSeed(string Declared, out string Actual, out string Failure)
		{
			Actual = null;
			Failure = null;
			if (The.Game == null) return Refuse("there is no game to read seed evidence from", out Failure);
			// Engine-owned, so it follows the engine's contract rather than the harness presence law.
			string engine = The.Game.GetStringGameState(EngineSeedState);
			if (string.IsNullOrEmpty(engine))
				return Refuse("the engine reported no world seed; refusing to stamp an asserted seed",
					out Failure);
			if (!KingdomScenarioRules.ValidSeed(engine))
				return Refuse("the engine world seed is not a recordable token", out Failure);
			if (Declared != null && !string.Equals(engine, Declared, StringComparison.Ordinal))
				return Refuse("this world was generated under seed '"
					+ KingdomScenarioRules.Bounded(engine) + "' but the request froze '"
					+ KingdomScenarioRules.Bounded(Declared)
					+ "'; the scenario mode sets the frozen seed before world generation, so this "
					+ "world was not built by it", out Failure);
			if (engine[0] == '#')
			{
				int declaredInt;
				if (!int.TryParse(engine.Substring(1), NumberStyles.None,
					CultureInfo.InvariantCulture, out declaredInt))
					return Refuse("the exact-seed form is not an integer", out Failure);
				// An exact '#<int>' seed must have taken the engine's exact path, not its hash
				// fallback; equality here is what proves the world is reproducible.
				if (The.Game.GetWorldSeed() != declaredInt)
					return Refuse("the engine did not generate this world from the exact seed",
						out Failure);
			}
			Actual = engine;
			return true;
		}

		/// <summary>
		/// Rebuilds the plan from the current request and proves it is the plan that was stamped.
		/// The request state is mutable, so equality of the whole tuple, resolved-plan digest
		/// included, is what licenses an attended action.
		/// </summary>
		internal static bool TryBindStampedPlan(out KingdomScenarioPlan Plan,
			out KingdomScenarioProvenance Record, out string Failure)
		{
			Plan = null;
			KingdomScenarioStampShape presence = Presence(out Record, out Failure);
			if (presence == KingdomScenarioStampShape.Absent)
				return Refuse("this game carries no scenario stamp", out Failure);
			if (presence == KingdomScenarioStampShape.PresentUnreadable)
				return Refuse("this game carries an unreadable scenario stamp (" + Failure
					+ "); refusing to act on it", out Failure);
			// The stamp must still be well formed and describe this build, judged by the
			// production rule rather than a local re-check.
			if (!KingdomScenarioProvenanceRules.TryValidateStampShape(Record,
				KingdomScenarioRegistry.Digest, KingdomReleaseInfo.Version,
				XRLGame.CoreVersion.ToString(), out Failure)) return false;
			string request;
			bool present;
			string detail;
			if (!TryReadRequest(out request, out present, out detail) || !present)
				return Refuse("the scenario request state is missing or unreadable ("
					+ (detail ?? "no request key") + ")", out Failure);
			KingdomScenarioPlan plan;
			if (!KingdomScenarioRequest.TryPlan(request, out plan, out Failure)) return false;
			// The request state is mutable, so every stamped field is compared, not a subset.
			if (!string.Equals(plan.PlanDigest, Record.PlanDigest, StringComparison.Ordinal))
				return Refuse("the current request resolves to a different plan than the stamp",
					out Failure);
			if (!string.Equals(plan.Key, Record.ScenarioKey, StringComparison.Ordinal)
				|| !string.Equals(plan.AuthorityClass, Record.AuthorityClass, StringComparison.Ordinal)
				|| !string.Equals(plan.Verbs, Record.Verbs, StringComparison.Ordinal)
				|| !string.Equals(plan.DefinitionDigest, Record.DefinitionDigest,
					StringComparison.Ordinal)
				|| !string.Equals(plan.AnchorId ?? "", Record.AnchorId ?? "",
					StringComparison.Ordinal)
				|| plan.Synthetic != Record.Synthetic)
				return Refuse("the current scenario request no longer matches the stamped plan; "
					+ "refusing to act on a state this stamp does not describe", out Failure);
			// Re-prove the engine seed at attended execution: the stamp recorded what the engine
			// reported at boot, and it must still report the same world.
			string actualSeed;
			if (!TryProveSeed(Record.Seed, out actualSeed, out Failure)) return false;
			if (!string.Equals(actualSeed, Record.Seed, StringComparison.Ordinal))
				return Refuse("the engine world seed no longer matches the stamped seed", out Failure);
			Plan = plan;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
