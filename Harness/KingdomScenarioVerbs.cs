using System;
using System.Collections.Generic;
using System.Text;

using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The scenario verb surface: the ONE entry shared by the attended wish and the unattended
	/// auto-runner, and the read-only reports it answers with.
	/// <para>
	/// Nothing here shows a popup. <c>Popup.Show</c> blocks on a keypress, so a verb that owned its
	/// own popup could only ever be driven by a human; keeping presentation in
	/// <see cref="KingdomScenarioWishes"/> is what makes the harness scriptable. Both callers run
	/// identical code and produce identical text.
	/// </para>
	/// <para>
	/// EXACTLY ONE JOURNAL ROW PER INVOCATION, written here rather than by either caller, so the
	/// attended and unattended paths can never disagree about what was recorded. The row is written
	/// AFTER the verb answers, because a refusal's own message is the thing worth recording.
	/// </para>
	/// <para>
	/// OK vs REFUSED is taken from the verb's own boolean, never guessed from its text. The reports
	/// below are OBSERVATIONS: an ineligible verdict, an unhealthy roster, and an empty anchor store
	/// are answers, so they journal OK. REFUSED means the verb declined to act, and it is the only
	/// thing that stops a script.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioVerbs
	{
		internal const string Usage =
			"Use {{W|kingdom:scenario}} with list, status, realize, anchor, ground, flatten, "
			+ "advance <turns>, or capture <anchor-id> <scenario-key>[;param=value] "
			+ "[at=<x>,<y>|id=<object-id>].";

		/// <summary>Journal verb column for the bare command, which prints help.</summary>
		internal const string HelpVerb = "help";

		private const string CapturePrefix = "capture ";

		private const string AdvancePrefix = KingdomScenarioAdvance.Verb + " ";

		/// <summary>
		/// Runs one verb and journals it. Returns the report; <paramref name="Ok"/> is false only
		/// when the verb refused. A journal failure is fail-open: it is appended to the report as a
		/// note and never changes the outcome.
		/// </summary>
		internal static string Invoke(string Parameter, out bool Ok)
		{
			string raw = (Parameter ?? "").Trim();
			string message = Dispatch(raw, out Ok);
			string note = KingdomScenarioJournal.Append(Token(raw), Ok, message);
			return note == null
				? message
				: message + "\n\n{{R|Journal row not written}}: " + note;
		}

		/// <summary>
		/// The verb column: the first word, lowercased. Arguments are deliberately dropped - the
		/// column names WHICH verb ran, and the message already carries what it was given.
		/// </summary>
		internal static string Token(string Raw)
		{
			string raw = (Raw ?? "").Trim();
			if (raw.Length == 0) return HelpVerb;
			int space = raw.IndexOf(' ');
			return (space < 0 ? raw : raw.Substring(0, space)).ToLowerInvariant();
		}

		private static string Dispatch(string Raw, out bool Ok)
		{
			Ok = true;
			if (Raw.StartsWith(CapturePrefix, StringComparison.OrdinalIgnoreCase))
				return KingdomScenarioCaptureReport.Emit(Raw.Substring(CapturePrefix.Length),
					out Ok);
			// A bare `advance` routes here too, so a missing count is refused by the verb's own
			// malformed-count code rather than falling through to a usage line that proves nothing.
			if (Raw.StartsWith(AdvancePrefix, StringComparison.OrdinalIgnoreCase))
				return KingdomScenarioAdvance.Run(Raw.Substring(AdvancePrefix.Length), out Ok);
			switch (Raw.ToLowerInvariant())
			{
				case "": return Help();
				case KingdomScenarioAdvance.Verb:
					return KingdomScenarioAdvance.Run("", out Ok);
				case "list": return List();
				case "status": return Status();
				case "anchor": return Anchor();
				case "realize": return Realize(out Ok);
				case "ground": return KingdomScenarioGround.Scout(out Ok);
				case "flatten": return KingdomScenarioFlatten.Flatten(out Ok);
				default:
				{
					// The second SOURCE behind this one dispatch path: verbs contributed by other
					// mods loaded in the same dev profile. Asked only after the closed built-in set
					// has declined, so a provider can never shadow a harness verb.
					string message;
					if (KingdomScenarioVerbRegistry.TryRun(Token(Raw), Argument(Raw), out message,
						out Ok)) return message;
					// An unrecognised verb is a REFUSAL, not help. A script that names a verb this
					// harness does not have must stop rather than walk on past a usage line.
					Ok = false;
					return Usage + KingdomScenarioVerbRegistry.Describe();
				}
			}
		}

		/// <summary>Everything after the first word, trimmed. Empty for a bare verb.</summary>
		internal static string Argument(string Raw)
		{
			string raw = (Raw ?? "").Trim();
			int space = raw.IndexOf(' ');
			return space < 0 ? "" : raw.Substring(space + 1).Trim();
		}

		private static string Realize(out bool Ok)
		{
			string report;
			string failure;
			Ok = KingdomScenarioRun.TryRun(out report, out failure);
			return Ok ? report : "{{R|Scenario refused}}: " + failure;
		}

		private static string Help()
		{
			return "{{C|Developer scenario harness}}\nMod " + KingdomReleaseInfo.Version
				+ ", Qud " + XRLGame.CoreVersion + "\n"
				+ KingdomScenarioRegistry.Scenarios.Count + " authored scenario(s); roster "
				+ (KingdomScenarioRegistry.Healthy ? "healthy" : "UNHEALTHY") + "; "
				+ KingdomScenarioAnchorStore.Anchors.Count + " curated anchor(s).\n\n"
				+ "{{W|list}} authored scenarios, {{W|status}} this game's stamp, "
				+ "{{W|realize}} the single production transaction, {{W|anchor}} to read the "
				+ "curated ordinary-play anchor evidence, and {{W|capture <anchor-id> "
				+ "<scenario-key> [at=<x>,<y>|id=<object-id>]}} to emit a curated row from an "
				+ "ordinary-play state. The selector is needed only when a zone holds more than "
				+ "one building of the scenario's exact frozen case.\n\n"
				+ "{{W|advance <turns>}} runs up to " + KingdomScenarioAdvance.MaxTurns
				+ " game turns with no player input, for behaviour that only happens on a clock. "
				+ "It spends one turn per action opportunity through the engine's own pass, so "
				+ "every system ticks; a row lands every " + KingdomScenarioAdvance.ProgressTurns
				+ " turns and a scripted run resumes at its next verb afterwards.\n\n"
				+ "Harness scope: a scenario skips the walk between production verbs. No verdict "
				+ "taken in a scenario-built state signs native acceptance until independently "
				+ "curated anchor evidence proves a green ordinary-play anchor for its authority "
				+ "class."
				+ KingdomScenarioVerbRegistry.Describe();
		}

		private static string List()
		{
			IList<KingdomScenarioDefinition> rows = KingdomScenarioRegistry.Scenarios;
			StringBuilder sb = new StringBuilder("{{C|Authored scenarios}}\nRoster digest ")
				.Append(KingdomScenarioRegistry.Digest ?? "(none)");
			for (int i = 0; i < rows.Count; i++)
				sb.Append("\n\n{{W|").Append(rows[i].Key ?? "(unkeyed)").Append("}}  from ")
					.Append(string.IsNullOrEmpty(rows[i].Owner) ? "(unowned stream)" : rows[i].Owner)
					.Append("\n  family=")
					.Append(rows[i].Family).Append("  authority=").Append(rows[i].AuthorityClass)
					.Append(string.Equals(rows[i].SyntheticRaw, "true", StringComparison.Ordinal)
						? "  {{R|SYNTHETIC}}" : "")
					.Append("\n  anchor ").Append(rows[i].AnchorId ?? "none (verdicts ineligible)")
					.Append("\n  ").Append(rows[i].Description ?? "");
			Faults(sb, "roster", KingdomScenarioRegistry.Findings);
			Faults(sb, "anchors", KingdomScenarioAnchorStore.Findings);
			// `list` is the sealable roster verb, so it is where an unattended run records which
			// mods contributed scenarios AND which verb providers were admitted or refused.
			sb.Append(KingdomScenarioVerbRegistry.Describe());
			return sb.ToString();
		}

		private static void Faults(StringBuilder Sb, string Label, IList<string> Findings)
		{
			for (int i = 0; i < Findings.Count; i++)
				Sb.Append("\n{{R|").Append(Label).Append(" fault}} ").Append(Findings[i]);
		}

		private static string Status()
		{
			KingdomScenarioProvenance record;
			string failure;
			KingdomScenarioStampShape presence =
				KingdomScenarioRealizer.Presence(out record, out failure);
			if (presence == KingdomScenarioStampShape.Absent)
				return KingdomScenarioProvenanceRules.Describe(null);
			if (presence == KingdomScenarioStampShape.PresentUnreadable)
				return KingdomScenarioProvenanceRules.DescribeUnreadable(failure);
			return KingdomScenarioProvenanceRules.Describe(record)
				+ "\n  production transaction: " + TransactionLine()
				+ "\n\n" + Verdict(record);
		}

		/// <summary>
		/// The current anchor verdict, recomputed from the DURABLE stamp.
		/// <para>
		/// Read from the save rather than from a run that may have ended sessions ago: after a
		/// reload the only truth about what was compared is the published key-set digest, and a
		/// status that reported the opening null state would keep saying nothing was compared long
		/// after a run had compared it.
		/// </para>
		/// </summary>
		private static string Verdict(KingdomScenarioProvenance Record)
		{
			if (Record == null || Record.KeySetDigest == null)
				return KingdomScenarioProvenanceRules
					.AcceptanceRequiresIndependentAnchorEvidence(Record);
			KingdomScenarioAnchorEvidence evidence = KingdomScenarioAnchorStore.Find(
				Record.AnchorId, Record.AuthorityClass);
			string failure;
			bool signs = KingdomScenarioAnchorRules.TrySignAcceptance(Record, evidence,
				KingdomScenarioRegistry.Digest, KingdomReleaseInfo.Version,
				XRLGame.CoreVersion.ToString(), out failure);
			return "Measured key set: " + Record.KeySetDigest + "\nDifferential verdict: "
				+ (signs ? "{{G|eligible}}" : "{{R|ineligible}}") + "\n  "
				+ (signs
					? "matched the ordinary-play anchor across every declared key."
					: failure);
		}

		/// <summary>Names the durable transaction state, including a poisoned profile.</summary>
		private static string TransactionLine()
		{
			string detail;
			switch (KingdomScenarioTransactionMarker.Observe(out detail))
			{
				case KingdomScenarioTransactionShape.None: return "not yet run";
				case KingdomScenarioTransactionShape.Attempted:
					return "{{R|ATTEMPTED}} - the ground may have been altered and is not "
						+ "journalled; this profile is spent";
				case KingdomScenarioTransactionShape.Committed: return "committed";
				default:
					return "{{R|TORN}} (" + (detail ?? "unknown fault") + "); profile poisoned";
			}
		}

		/// <summary>
		/// Reads the curated anchor store. Deliberately read-only: an anchor is captured by a
		/// reviewer from a state ordinary play reached, and the harness has no path that writes
		/// one. Founding an anchor from inside a scenario-built game is exactly the self-signing
		/// the ruling forbids.
		/// </summary>
		private static string Anchor()
		{
			string failure;
			StringBuilder sb = new StringBuilder("{{C|Curated ordinary-play anchors}}");
			// The SAME fail-closed proof the capture command uses. An absent stamp is not
			// eligibility, and the status must never claim more than the capture would allow.
			if (!KingdomScenarioDurableState.OrdinaryAnchorEligible(out failure))
				sb.Append("\n\n{{R|This game may not found anchor evidence}}: ").Append(failure)
					.Append(".");
			else
				sb.Append("\n\nThis game carries no scenario stamp, transaction marker, or request "
					+ "key, so a reviewer may capture an anchor from it by hand and curate it into "
					+ "the anchor store.");
			IList<KingdomScenarioAnchorEvidence> anchors = KingdomScenarioAnchorStore.Anchors;
			if (anchors.Count == 0)
				sb.Append("\n\nThe anchor store is empty. Every scenario verdict is correctly "
					+ "ineligible until a reviewer curates one.");
			for (int i = 0; i < anchors.Count; i++)
				sb.Append("\n\n{{W|").Append(anchors[i].AnchorId ?? "(unkeyed)")
					.Append("}}  authority=").Append(anchors[i].AuthorityClass)
					.Append("\n  verbs ").Append(anchors[i].Verbs)
					.Append("\n  keyset ").Append(anchors[i].KeySetDigest)
					.Append("\n  reached ").Append(anchors[i].Reached);
			Faults(sb, "anchors", KingdomScenarioAnchorStore.Findings);
			return sb.ToString();
		}
	}
}
