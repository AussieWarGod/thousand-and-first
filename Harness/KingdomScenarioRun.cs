using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The attended runner.
	/// <para>
	/// Atomicity is structural, not promised: the row validator admits at most one mutating verb
	/// and requires it to be last, so this runner proves every read-only observation first, then
	/// pre-proves the transaction's own preconditions, then commits exactly one production
	/// transaction and records that fact before reporting anything. There is no interleaving for a
	/// save or crash cut to tear, and a replay is refused rather than re-run.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioRun
	{
		internal static bool TryRun(out string Report, out string Failure)
		{
			Report = null;
			Failure = null;
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			if (!KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out Failure))
				return false;
			// Any prior attempt, commit, or torn marker refuses here, before anything is planned.
			string transactionDetail;
			KingdomScenarioTransactionShape observed =
				KingdomScenarioTransactionMarker.Observe(out transactionDetail);
			if (observed != KingdomScenarioTransactionShape.None)
			{
				string begin;
				KingdomScenarioTransactionMarker.TryBegin(out begin);
				return Refuse(begin, out Failure);
			}
			Zone zone = The.Player?.CurrentZone;
			if (zone == null) return Refuse("stand in a loaded zone before realizing", out Failure);
			List<string> log = new List<string>();
			KingdomScenarioResolvedStep mutation = null;
			Stat.PushState(KingdomScenarioHarness.SeedPrefix + plan.Key);
			try
			{
				for (int i = 0; i < plan.Steps.Count; i++)
				{
					KingdomScenarioResolvedStep step = plan.Steps[i];
					if (KingdomScenarioVerbSchema.Mutates(step.Verb)) { mutation = step; continue; }
					if (!TryObserve(step, log, out Failure))
						return Refuse("observation refused before any mutation: " + Failure,
							out Failure);
				}
				if (mutation == null)
					return Refuse("this scenario declares no production transaction", out Failure);
				// One dispatch per admitted mutating verb. Founding rides the same rails as the
				// gallery staging: preconditions pre-marker, TryBegin, ONE production call,
				// TryCommit, then the shared measurement and differential judgment.
				if (mutation.Verb == KingdomScenarioVerb.FoundFirstCity)
					return TryRealizeFounding(plan, stamp, mutation, zone, log, out Report,
						out Failure);
				KingdomScenarioGallerySlice.Case expected;
				if (!TryExpectedGalleryCase(plan, out expected, out Failure)
					|| !KingdomScenarioGallerySlice.TryProvePreconditions(zone, expected, out Failure))
					return Refuse("refused before mutation: " + Failure, out Failure);
				// Everything above is a precondition and refuses BEFORE the attempt is recorded.
				// From here the ground may change, so the attempt is durable first.
				if (!KingdomScenarioTransactionMarker.TryBegin(out Failure))
					return Refuse(Failure, out Failure);
				GameObject owner;
				bool staged = KingdomScenarioGallerySlice.TryStage(zone, expected, out owner,
					out Failure);
				if (!staged)
					return Refuse("the production transaction refused: " + Failure
						+ ". The attempt marker stands, so this profile is spent: the ground is not "
						+ "journalled and cannot be proved unchanged. Prepare a new dev game.",
						out Failure);
				// Committed before any capture or reporting, so a later failure can never look
				// like an uncommitted transaction and license a second stage.
				string commitFailure;
				if (!KingdomScenarioTransactionMarker.TryCommit(out commitFailure))
					return Refuse("the production transaction committed but its marker did not: "
						+ commitFailure, out Failure);
				log.Add("  stagegallerycase: " + expected.BuildKey + "/" + expected.TypeKey
					+ "/" + expected.LotSize + "/" + expected.VariantKey
					+ " pose " + expected.Facing.ToString().ToLowerInvariant()
					+ " (case " + expected.Number + ") receipt "
					+ (KingdomScenarioGallerySlice.Receipt(owner) ?? "-"));
				Report = Conclude(plan, stamp, owner, log);
				return true;
			}
			finally
			{
				Stat.PopState();
			}
		}

		/// <summary>Resolves the exact gallery case frozen by a bound plan's sole mutation.</summary>
		internal static bool TryExpectedGalleryCase(KingdomScenarioPlan Plan,
			out KingdomScenarioGallerySlice.Case Expected, out string Failure)
		{
			Expected = null;
			Failure = null;
			for (int i = 0; Plan != null && i < Plan.Steps.Count; i++)
			{
				KingdomScenarioResolvedStep step = Plan.Steps[i];
				if (!KingdomScenarioVerbSchema.Mutates(step.Verb)) continue;
				if (step.Verb != KingdomScenarioVerb.StageGalleryCase)
					return Refuse("this scenario's production transaction is not architecture staging",
						out Failure);
				string facing;
				string build;
				string type;
				string size;
				string variant;
				if (!step.Arguments.TryGetValue("Facing", out facing)
					|| !step.Arguments.TryGetValue("Build", out build)
					|| !step.Arguments.TryGetValue("Type", out type)
					|| !step.Arguments.TryGetValue("Size", out size)
					|| !step.Arguments.TryGetValue("Variant", out variant))
					return Refuse("the resolved transaction is missing a frozen case argument",
						out Failure);
				return KingdomScenarioGallerySlice.TryResolveCase(build, type, size, variant, facing,
					out Expected, out Failure);
			}
			return Refuse("this scenario declares no production transaction", out Failure);
		}

		private static bool TryObserve(KingdomScenarioResolvedStep Step, IList<string> Log,
			out string Failure)
		{
			Failure = null;
			switch (Step.Verb)
			{
				case KingdomScenarioVerb.ProveCatalogue:
				{
					string catalogue;
					if (!Step.Arguments.TryGetValue("Catalogue", out catalogue))
						return Refuse("the resolved step carries no Catalogue argument", out Failure);
					// Trigger the production lazy load before judging health: a fresh dev game
					// may observe before any system has asked KingdomData, and an unloaded
					// catalogue must read as "load it, then judge", not as a fault.
					if (!KingdomArchitecture.Loaded && KingdomData.Buildings != null) { }
					if (!KingdomArchitecture.Healthy)
						return Refuse("the authored " + catalogue
							+ " catalogue is not healthy", out Failure);
					Log.Add("  provecatalogue: " + catalogue + " healthy");
					return true;
				}
				case KingdomScenarioVerb.ProveUnfounded:
				{
					string detail;
					if (!KingdomScenarioFoundingStep.TryProveUnfounded(out detail, out Failure))
						return false;
					Log.Add("  proveunfounded: " + detail);
					return true;
				}
				default:
					return Refuse("the resolved step carries no admitted observation verb", out Failure);
			}
		}

		/// <summary>
		/// The founding transaction on the shared rails. The marker law is inherited verbatim:
		/// attempt durable BEFORE the production call, committed before any capture or reporting,
		/// and every prior marker state already refused at the top of <see cref="TryRun"/>.
		/// </summary>
		private static bool TryRealizeFounding(KingdomScenarioPlan Plan,
			KingdomScenarioProvenance Stamp, KingdomScenarioResolvedStep Mutation, Zone Zone,
			List<string> Log, out string Report, out string Failure)
		{
			Report = null;
			Failure = null;
			string name;
			if (!Mutation.Arguments.TryGetValue("CityName", out name))
				return Refuse("the resolved transaction is missing its frozen city name",
					out Failure);
			if (!KingdomScenarioFoundingStep.TryProvePreconditions(Zone, name, out Failure))
				return Refuse("refused before mutation: " + Failure, out Failure);
			// Everything above is a precondition and refuses BEFORE the attempt is recorded.
			if (!KingdomScenarioTransactionMarker.TryBegin(out Failure))
				return Refuse(Failure, out Failure);
			string line;
			if (!KingdomScenarioFoundingStep.TryFound(Zone, name, out line, out Failure))
				return Refuse("the production transaction refused: " + Failure
					+ ". The attempt marker stands, so this profile is spent: founding "
					+ "reservations and faction registration are not journalled by the harness "
					+ "and cannot be proved unchanged. Prepare a new dev game.", out Failure);
			string commitFailure;
			if (!KingdomScenarioTransactionMarker.TryCommit(out commitFailure))
				return Refuse("the production transaction committed but its marker did not: "
					+ commitFailure, out Failure);
			Log.Add("  foundfirstcity: " + line);
			Report = Conclude(Plan, Stamp, null, Log);
			return true;
		}

		/// <summary>
		/// Measures the declared key set off the committed state and judges it against independently
		/// held anchor evidence. With no curated anchor yet the honest verdict is ineligible; the
		/// mechanism still runs end to end rather than handing the comparison to the operator.
		/// </summary>
		private static string Conclude(KingdomScenarioPlan Plan, KingdomScenarioProvenance Stamp,
			GameObject Owner, IList<string> Log)
		{
			StringBuilder sb = new StringBuilder("{{C|Scenario realized}} ").Append(Plan.Key)
				.Append("\n").Append(string.Join("\n", Log));
			IDictionary<string, string> captured = null;
			string failure = null;
			// Measurement dispatches on the authority class the plan declares: architecture
			// measures the staged owner, founding measures the persisted kingdom system. Both are
			// recomputed from durable state the production path published, never from a run cache.
			bool captureOk;
			if (string.Equals(Plan.AuthorityClass, KingdomScenarioFoundingStep.FoundingAuthority,
				StringComparison.Ordinal))
				captureOk = KingdomScenarioFoundingStep.TryMeasure(out captured, out failure);
			else
				captureOk = KingdomScenarioCapture.TryMeasure(Owner, out captured, out failure);
			if (!captureOk)
				return sb.Append("\n\n{{R|Capture refused}}: ").Append(failure)
					.Append("\nThe transaction committed; the differential comparison did not run.")
					.ToString();
			string digest;
			if (!KingdomScenarioAnchorRules.TryDigest(Plan.AuthorityClass, captured, out digest,
				out failure))
				return sb.Append("\n\n{{R|Key-set digest refused}}: ").Append(failure).ToString();
			sb.Append("\n\n{{C|Declared key set}} (").Append(Plan.AuthorityClass).Append(")");
			foreach (KeyValuePair<string, string> row in captured)
				sb.Append("\n  ").Append(row.Key).Append(" = ").Append(row.Value);
			sb.Append("\n  keyset digest = ").Append(digest);
			KingdomScenarioAnchorEvidence evidence =
				KingdomScenarioAnchorStore.Find(Plan.AnchorId, Plan.AuthorityClass);
			KingdomScenarioProvenance measured = Measured(Stamp, digest);
			// Durable BEFORE green. The measured key set has to survive the popup and a reload, or
			// status keeps describing the pre-run state; a torn publication stays non-green and
			// licenses nothing, because the transaction is already committed and non-retryable.
			string publishFailure;
			if (!KingdomScenarioRealizer.TryPublishMeasured(measured, out publishFailure))
				return sb.Append("\n\n{{R|Measured provenance not published}}: ")
					.Append(publishFailure)
					.Append("\nThe transaction committed and the key set was measured, but the "
						+ "durable stamp does not carry it. This run is NOT green, and no replay "
						+ "is licensed.").ToString();
			string signFailure;
			bool signs = KingdomScenarioAnchorRules.TrySignAcceptance(measured, evidence,
				Plan.DefinitionDigest, KingdomReleaseInfo.Version,
				XRLGame.CoreVersion.ToString(), out signFailure);
			sb.Append("\n\n{{C|Differential verdict}}: ")
				.Append(signs ? "{{G|eligible}}" : "{{R|ineligible}}")
				.Append("\n  ").Append(signs
					? "matched the ordinary-play anchor across every declared key."
					: signFailure);
			KingdomScenarioEvidence.Record(Plan, digest, signs, signFailure);
			return sb.ToString();
		}

		/// <summary>The stamp as measured: identical, plus the key set this run actually compared.</summary>
		private static KingdomScenarioProvenance Measured(KingdomScenarioProvenance Stamp,
			string KeySetDigest)
		{
			return new KingdomScenarioProvenance
			{
				ScenarioKey = Stamp.ScenarioKey,
				AuthorityClass = Stamp.AuthorityClass,
				Verbs = Stamp.Verbs,
				AnchorId = Stamp.AnchorId,
				KeySetDigest = KeySetDigest,
				Seed = Stamp.Seed,
				ModVersion = Stamp.ModVersion,
				QudCoreVersion = Stamp.QudCoreVersion,
				DefinitionDigest = Stamp.DefinitionDigest,
				PlanDigest = Stamp.PlanDigest,
				Synthetic = Stamp.Synthetic
			};
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
