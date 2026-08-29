using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The differential-anchor law. A scenario-built state never signs native acceptance on its
	/// own: each production authority class must first have an ordinary-play-reached anchor whose
	/// declared semantic key set matches. This class owns what "matches" means, and is engine-free
	/// so the verdict is testable without a game.
	/// </summary>
	internal static partial class KingdomScenarioAnchorRules
	{
		/// <summary>How a state was reached. Only OrdinaryPlay may found an anchor.</summary>
		internal enum Provenance : byte
		{
			Unknown = 0,
			OrdinaryPlay = 1,
			ScenarioBuilt = 2
		}

		internal enum Verdict : byte
		{
			/// <summary>No anchor exists for this authority class yet.</summary>
			NoAnchor = 0,

			/// <summary>An anchor exists but the compared key sets differ.</summary>
			Divergent = 1,

			/// <summary>Anchor and scenario agree across the whole declared key set.</summary>
			Matched = 2,

			/// <summary>The anchor was founded under different authored or build authority.</summary>
			Stale = 3
		}

		/// <summary>
		/// Declared semantic key set per authority class. Comparing a fixed, named set is what
		/// makes a divergence attributable; comparing whole-save bytes would not be reproducible.
		/// </summary>
		internal static IList<string> KeySet(string AuthorityClass)
		{
			List<string> keys = new List<string>();
			if (string.Equals(AuthorityClass, "architecture-stamper", StringComparison.Ordinal))
			{
				// Every key below is read from the production architecture intent, which both
				// an ordinary commission and a gallery staging produce. Gallery-only receipt
				// properties are deliberately absent: a differential whose keys exist on only one
				// path could never be satisfied by an ordinary-play anchor.
				// The realized digest is the key that makes this differential answer the question a
				// visual harness asks: a matching receipt over different ground, objects, or
				// rendering fails here even when every identity key below agrees.
				keys.Add("architecture.realized.digest");
				keys.Add("architecture.binding.key");
				keys.Add("architecture.build.key");
				keys.Add("architecture.extent");
				keys.Add("architecture.facing");
				keys.Add("architecture.lot.size");
				keys.Add("architecture.lot.type");
				keys.Add("architecture.main.offset");
				keys.Add("architecture.palette.key");
				keys.Add("architecture.plan.key");
				keys.Add("architecture.receipt.schema");
				keys.Add("architecture.snapshot.hash");
				keys.Add("architecture.tier.key");
				keys.Add("architecture.variant.key");
			}
			return keys;
		}

		internal static bool IsKnownAuthorityClass(string AuthorityClass)
		{
			return KeySet(AuthorityClass).Count > 0;
		}

		/// <summary>
		/// Canonical digest over one capture. Every declared key must be present exactly once;
		/// a missing or extra key is a refusal rather than a silently shorter comparison.
		/// </summary>
		internal static bool TryDigest(string AuthorityClass,
			IDictionary<string, string> Captured, out string Digest, out string Failure)
		{
			Digest = null;
			Failure = null;
			IList<string> keys = KeySet(AuthorityClass);
			if (keys.Count == 0)
				return Refuse("Authority class '" + KingdomScenarioRules.Bounded(AuthorityClass)
					+ "' declares no semantic key set.", out Failure);
			if (Captured == null)
				return Refuse("No capture was supplied for the declared key set.", out Failure);
			// Exact arity BEFORE enumeration: a different shape is refused as a shape, not
			// discovered by walking a capture whose size nobody bounded.
			if (Captured.Count != keys.Count)
				return Refuse("The capture declares " + Captured.Count + " keys where the authority "
					+ "class declares " + keys.Count + "; a different shape is not a comparison.",
					out Failure);
			StringBuilder sb = new StringBuilder(Grammar);
			// The class that SELECTED these keys is part of what was measured. Two classes
			// declaring identical key lists would otherwise digest identically.
			string authority = Field(AuthorityClass);
			if (authority == null)
				return Refuse("The authority class cannot be encoded injectively.", out Failure);
			sb.Append(authority);
			for (int i = 0; i < keys.Count; i++)
			{
				string value;
				if (!Captured.TryGetValue(keys[i], out value) || value == null)
					return Refuse("The capture is missing declared key '" + keys[i]
						+ "'; a shorter comparison is not a comparison.", out Failure);
				string key = Field(keys[i]);
				string measured = Field(value);
				if (key == null || measured == null)
					return Refuse("Declared key '" + keys[i] + "' measured a value this grammar "
						+ "cannot encode injectively.", out Failure);
				sb.Append(key).Append(measured);
			}
			Digest = Sha256(sb.ToString());
			if (Digest == null)
				return Refuse("The declared key set could not be encoded as strict UTF-8.",
					out Failure);
			return true;
		}

		/// <summary>
		/// An anchor may be founded only from a state ordinary play actually reached. Founding one
		/// from a scenario-built state would make the harness its own oracle.
		/// </summary>
		internal static bool TryFoundAnchor(Provenance Reached, string AuthorityClass,
			string CaptureDigest, out string Failure)
		{
			Failure = null;
			if (Reached != Provenance.OrdinaryPlay)
				return Refuse("An anchor may be founded only from an ordinary-play-reached state; "
					+ "a scenario-built state cannot anchor itself.", out Failure);
			if (!IsKnownAuthorityClass(AuthorityClass))
				return Refuse("Authority class '" + KingdomScenarioRules.Bounded(AuthorityClass)
					+ "' declares no semantic key set.", out Failure);
			if (!KingdomScenarioRules.ValidDigest(CaptureDigest))
				return Refuse("The anchor capture digest is malformed.", out Failure);
			return true;
		}

		/// <summary>
		/// Judge a scenario-built capture against a recorded anchor. Absence of an anchor is
		/// NoAnchor, never a pass; divergence names the authority class rather than the case.
		/// </summary>
		internal static Verdict Judge(string AnchorDigest, string AnchorDefinitionDigest,
			string ScenarioDigest, string CurrentDefinitionDigest, out string Detail)
		{
			Detail = null;
			if (string.IsNullOrEmpty(AnchorDigest))
			{
				Detail = "No ordinary-play differential anchor exists for this authority class; "
					+ "scenario verdicts under it are ineligible, not green.";
				return Verdict.NoAnchor;
			}
			if (!KingdomScenarioRules.ValidDigest(AnchorDigest)
				|| !KingdomScenarioRules.ValidDigest(ScenarioDigest))
			{
				Detail = "An anchor or scenario capture digest is malformed.";
				return Verdict.Divergent;
			}
			if (!string.Equals(AnchorDefinitionDigest, CurrentDefinitionDigest,
				StringComparison.Ordinal))
			{
				Detail = "The anchor was founded under different authored scenario text; "
					+ "re-found it before trusting a sibling verdict.";
				return Verdict.Stale;
			}
			if (!string.Equals(AnchorDigest, ScenarioDigest, StringComparison.Ordinal))
			{
				Detail = "The scenario-built state diverges from the ordinary-play anchor across "
					+ "the declared key set; the scenario is not reproducing ordinary play.";
				return Verdict.Divergent;
			}
			Detail = "The scenario-built state matches the ordinary-play anchor across every "
				+ "declared key.";
			return Verdict.Matched;
		}

		/// <summary>Only a matched anchor lets a sibling verdict count.</summary>
		internal static bool Signs(Verdict Verdict)
		{
			return Verdict == KingdomScenarioAnchorRules.Verdict.Matched;
		}

		/// <summary>
		/// The one place a scenario-built state may be called green.
		/// <para>
		/// The stamp names an anchor; it never proves one. This requires the independently held
		/// frozen anchor-evidence record and re-proves the whole tuple against it: exact anchor id,
		/// authority class, production verb sequence, compared key-set digest, and current
		/// definition/mod/core authority, with the anchor itself reached by ordinary play. Every
		/// mismatch is a refusal naming the field, so an invented, stale, wrong-class, wrong-verb,
		/// wrong-key-set, or scenario-founded anchor cannot pass.
		/// </para>
		/// <para>
		/// The caller must obtain <paramref name="Evidence"/> from the curated checked-in source.
		/// Passing a record derived from the save, the scenario registry, or a scenario-built
		/// capture defeats the ruling and is never lawful.
		/// </para>
		/// </summary>
		internal static bool TrySignAcceptance(KingdomScenarioProvenance Stamp,
			KingdomScenarioAnchorEvidence Evidence, string CurrentDefinitionDigest,
			string CurrentModVersion, string CurrentQudCoreVersion, out string Failure)
		{
			Failure = null;
			if (Stamp == null) return Refuse("No scenario stamp to judge.", out Failure);
			if (Stamp.Synthetic)
				return Refuse("Synthetic scenario states are recovery diagnostics only; "
					+ "they never sign native acceptance.", out Failure);
			if (!KingdomScenarioProvenanceRules.TryValidateStampShape(Stamp,
				CurrentDefinitionDigest, CurrentModVersion, CurrentQudCoreVersion, out Failure))
				return false;
			if (Stamp.AnchorId == null || Stamp.KeySetDigest == null)
				return Refuse("This state names no ordinary-play differential anchor; "
					+ "the verdict is ineligible, not green.", out Failure);
			if (Evidence == null)
				return Refuse("No independently held anchor-evidence record was supplied for '"
					+ Stamp.AnchorId + "'; a stamp names its anchor but cannot prove it.",
					out Failure);
			if (!KingdomScenarioRules.SafeToken(Evidence.AnchorId)
				|| !KingdomScenarioRules.SafeToken(Evidence.AuthorityClass)
				|| !KingdomScenarioRules.ValidDigest(Evidence.KeySetDigest)
				|| !KingdomScenarioRules.ValidDigest(Evidence.DefinitionDigest)
				|| !KingdomScenarioRules.ValidDigest(Evidence.PlanDigest)
				|| !KingdomScenarioRules.SafeToken(Evidence.ModVersion)
				|| !KingdomScenarioRules.SafeToken(Evidence.QudCoreVersion)
				|| KingdomScenarioProvenanceRules.VerbSequence(Evidence.Verbs).Count == 0)
				return Refuse("The anchor-evidence record is malformed.", out Failure);
			if (Evidence.Reached != Provenance.OrdinaryPlay)
				return Refuse("The anchor was not reached by ordinary play; "
					+ "a scenario-built state cannot anchor itself.", out Failure);
			if (!IsKnownAuthorityClass(Evidence.AuthorityClass))
				return Refuse("The anchor-evidence record names authority class '"
					+ KingdomScenarioRules.Bounded(Evidence.AuthorityClass)
					+ "', which declares no semantic key set.", out Failure);
			if (!string.Equals(Evidence.AnchorId, Stamp.AnchorId, StringComparison.Ordinal))
				return Refuse("The anchor-evidence record is for a different anchor id.",
					out Failure);
			if (!string.Equals(Evidence.AuthorityClass, Stamp.AuthorityClass,
				StringComparison.Ordinal))
				return Refuse("The anchor was measured for authority class '"
					+ KingdomScenarioRules.Bounded(Evidence.AuthorityClass) + "', not '"
					+ KingdomScenarioRules.Bounded(Stamp.AuthorityClass) + "'.", out Failure);
			if (!string.Equals(Evidence.Verbs, Stamp.Verbs, StringComparison.Ordinal))
				return Refuse("The anchor was reached by a different production verb sequence.",
					out Failure);
			if (!string.Equals(Evidence.KeySetDigest, Stamp.KeySetDigest, StringComparison.Ordinal))
				return Refuse("The scenario-built state diverges from the anchor across the "
					+ "declared key set.", out Failure);
			if (!KingdomScenarioRules.ValidDigest(Stamp.PlanDigest)
				|| !string.Equals(Evidence.PlanDigest, Stamp.PlanDigest, StringComparison.Ordinal))
				return Refuse("The anchor was measured against a different resolved plan; "
					+ "the bindings or resolved arguments differ.", out Failure);
			if (!string.Equals(Evidence.DefinitionDigest, CurrentDefinitionDigest,
				StringComparison.Ordinal))
				return Refuse("The anchor was founded under different authored scenario text; "
					+ "it is stale.", out Failure);
			if (!string.Equals(Evidence.ModVersion, CurrentModVersion, StringComparison.Ordinal)
				|| !string.Equals(Evidence.QudCoreVersion, CurrentQudCoreVersion,
					StringComparison.Ordinal))
				return Refuse("The anchor was founded under a different mod or core build; "
					+ "it is stale.", out Failure);
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
