using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Attended operator surface. Reachable only when the excluded Harness tree is loaded, because
	/// wish discovery is a reflection scan over compiled types.
	/// </summary>
	[HasWishCommand]
	public static class KingdomScenarioWishes
	{
		[WishCommand("kingdom:scenario", null)]
		public static void Scenario(string Parameter)
		{
			KingdomSystem.Guard("scenario harness", delegate
			{
				string raw = (Parameter ?? "").Trim();
				if (raw.StartsWith("capture ", StringComparison.OrdinalIgnoreCase))
				{
					Popup.Show(KingdomScenarioCaptureReport.Emit(raw.Substring(8)));
					return;
				}
				string control = raw.ToLowerInvariant();
				switch (control)
				{
					case "": Popup.Show(Help()); return;
					case "list": Popup.Show(List()); return;
					case "status": Popup.Show(Status()); return;
					case "realize": Popup.Show(Realize()); return;
					case "anchor": Popup.Show(Anchor()); return;
					default:
						Popup.Show("Use {{W|kingdom:scenario}} with list, status, realize, anchor, "
							+ "or capture <anchor-id> <scenario-key>[;param=value] "
							+ "[at=<x>,<y>|id=<object-id>].");
						return;
				}
			});
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
				+ "Harness scope: a scenario skips the walk between production verbs. No verdict "
				+ "taken in a scenario-built state signs native acceptance until independently "
				+ "curated anchor evidence proves a green ordinary-play anchor for its authority "
				+ "class.";
		}

		private static string List()
		{
			IList<KingdomScenarioDefinition> rows = KingdomScenarioRegistry.Scenarios;
			StringBuilder sb = new StringBuilder("{{C|Authored scenarios}}\nRoster digest ")
				.Append(KingdomScenarioRegistry.Digest ?? "(none)");
			for (int i = 0; i < rows.Count; i++)
				sb.Append("\n\n{{W|").Append(rows[i].Key ?? "(unkeyed)").Append("}}  family=")
					.Append(rows[i].Family).Append("  authority=").Append(rows[i].AuthorityClass)
					.Append(string.Equals(rows[i].SyntheticRaw, "true", StringComparison.Ordinal)
						? "  {{R|SYNTHETIC}}" : "")
					.Append("\n  anchor ").Append(rows[i].AnchorId ?? "none (verdicts ineligible)")
					.Append("\n  ").Append(rows[i].Description ?? "");
			Faults(sb, "roster", KingdomScenarioRegistry.Findings);
			Faults(sb, "anchors", KingdomScenarioAnchorStore.Findings);
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

		private static string Realize()
		{
			string report;
			string failure;
			return KingdomScenarioRun.TryRun(out report, out failure)
				? report
				: "{{R|Scenario refused}}: " + failure;
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
				sb.Append("\n\n{{W|").Append(anchors[i].AnchorId ?? "(unkeyed)").Append("}}  authority=")
					.Append(anchors[i].AuthorityClass).Append("\n  verbs ").Append(anchors[i].Verbs)
					.Append("\n  keyset ").Append(anchors[i].KeySetDigest)
					.Append("\n  reached ").Append(anchors[i].Reached);
			Faults(sb, "anchors", KingdomScenarioAnchorStore.Findings);
			return sb.ToString();
		}
	}
}
