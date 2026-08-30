using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The read-only ordinary-play capture report.
	/// <para>
	/// A reviewer reaches a building through ordinary commission, stands in its zone, and runs this
	/// against the scenario the anchor is for. It measures the shared semantic key set and prints
	/// the exact curated row. It never inserts anything into the anchor store: pasting the row in is
	/// the reviewer's deliberate act, which is what keeps the harness from signing its own evidence.
	/// </para>
	/// <para>
	/// Eligibility is one fail-closed proof, not the absence of a stamp: a scenario profile whose
	/// stamp was deleted or torn still carries the transaction marker and request key it was
	/// prepared with, and either one means this world was arranged rather than played.
	/// </para>
	/// </summary>
	internal static partial class KingdomScenarioCaptureReport
	{
		/// <summary>
		/// Emits the curated anchor row for one ordinary-play state. The building is chosen by the
		/// requested plan's own frozen case identity, never by whatever the zone enumerates first.
		/// </summary>
		internal static string Emit(string Parameter)
		{
			bool ok;
			return Emit(Parameter, out ok);
		}

		/// <summary>
		/// The same report, with its outcome as a boolean rather than a string the caller has to
		/// read. The journal and the auto-runner need OK vs REFUSED as a fact, and recovering it by
		/// matching the "{{R|Capture refused}}" prefix would break the first time it is reworded.
		/// </summary>
		internal static string Emit(string Parameter, out bool Ok)
		{
			Ok = false;
			string anchorId;
			string request;
			string selector;
			string failure;
			if (!TrySplit(Parameter, out anchorId, out request, out selector, out failure))
				return "{{R|Capture refused}}: " + failure;
			if (!KingdomScenarioDurableState.OrdinaryAnchorEligible(out failure))
				return "{{R|Capture refused}}: this game is not ordinary play (" + failure
					+ "). An anchor may be founded only from a state ordinary play reached.";
			KingdomScenarioPlan plan;
			if (!KingdomScenarioRequest.TryPlan(request, out plan, out failure))
				return "{{R|Capture refused}}: " + failure;
			// The row must be emitted under the SAME anchor id the scenario already declares, or the
			// definition and plan digests it carries describe a roster that no longer exists the
			// moment anyone wires the id in. Curation is pasting a row, never editing the roster.
			if (plan.AnchorId == null)
				return "{{R|Capture refused}}: scenario '" + plan.Key + "' declares no anchor id, "
					+ "so a curated row could not name the anchor it founds.";
			if (!string.Equals(anchorId, plan.AnchorId, StringComparison.Ordinal))
				return "{{R|Capture refused}}: this scenario's anchor is '" + plan.AnchorId
					+ "', not '" + KingdomScenarioRules.Bounded(anchorId)
					+ "'. Capture the anchor the scenario actually leans on.";
			GameObject owner;
			if (!TryChoose(plan, selector, out owner, out failure))
				return "{{R|Capture refused}}: " + failure;
			IDictionary<string, string> captured;
			if (!KingdomScenarioCapture.TryMeasure(owner, out captured, out failure))
				return "{{R|Capture refused}}: " + failure;
			string keySet;
			if (!KingdomScenarioAnchorRules.TryDigest(plan.AuthorityClass, captured, out keySet,
				out failure))
				return "{{R|Capture refused}}: " + failure;
			string check;
			if (!KingdomScenarioAnchorRules.TryFoundAnchor(
				KingdomScenarioAnchorRules.Provenance.OrdinaryPlay, plan.AuthorityClass, keySet,
				out check)) return "{{R|Capture refused}}: " + check;
			Ok = true;
			return Report(anchorId, plan, captured, keySet);
		}

		/// <summary>
		/// Resolves the plan's own mutating case, filters the zone's stamped owners to buildings
		/// that ARE that exact case, and selects one deterministically. Enumeration order never
		/// decides: with several matches the reviewer names one, and the choice is re-proved.
		/// </summary>
		private static bool TryChoose(KingdomScenarioPlan Plan, string Selector,
			out GameObject Chosen, out string Failure)
		{
			Chosen = null;
			KingdomScenarioGallerySlice.Case expected;
			if (!TryExpectedCase(Plan, out expected, out Failure)) return false;
			bool hasCoordinate;
			int x;
			int y;
			string id;
			if (!KingdomScenarioSelectorRules.TryParse(Selector, out hasCoordinate, out x, out y,
				out id, out Failure)) return false;
			Zone zone = The.Player?.CurrentZone;
			if (zone == null) return Refuse("stand in a loaded zone to capture an anchor", out Failure);
			List<GameObject> matches = new List<GameObject>();
			List<KingdomScenarioOwnerRow> rows = new List<KingdomScenarioOwnerRow>();
			IList<GameObject> owners = KingdomScenarioCapture.Owners(zone);
			for (int i = 0; i < owners.Count; i++)
			{
				GameObject candidate = owners[i];
				string ignored;
				if (!KingdomScenarioGallerySlice.TryProveExactCase(candidate, expected, out ignored))
					continue;
				// The debug gallery wish runs in ordinary games too. Without this refusal an
				// operator could stage a case with it and then curate that very object as the
				// ordinary-play anchor the scenario is judged against, which is the harness
				// anchoring itself.
				if (!TryProveOrdinaryCommission(zone, candidate, out Failure)) return false;
				if (candidate.CurrentCell == null || string.IsNullOrEmpty(candidate.IDIfAssigned))
					return Refuse("a matching building has no cell or no stable identity", out Failure);
				matches.Add(candidate);
				rows.Add(new KingdomScenarioOwnerRow
				{
					X = candidate.CurrentCell.X,
					Y = candidate.CurrentCell.Y,
					Id = candidate.IDIfAssigned
				});
			}
			return TryResolve(matches, rows, expected, hasCoordinate, x, y, id, out Chosen,
				out Failure);
		}

		private static bool TryResolve(IList<GameObject> Matches,
			List<KingdomScenarioOwnerRow> Rows, KingdomScenarioGallerySlice.Case Expected,
			bool HasCoordinate, int X, int Y, string Id, out GameObject Chosen, out string Failure)
		{
			Chosen = null;
			Order(Matches, Rows);
			int index = KingdomScenarioSelectorRules.Resolve(Rows, HasCoordinate, X, Y, Id,
				out Failure);
			if (index < 0)
				return Refuse((Failure ?? "no building matched") + " (frozen case "
					+ Expected.BuildKey + "/" + Expected.VariantKey + " pose "
					+ Expected.Facing + ")", out Failure);
			GameObject chosen = Matches[index];
			// Re-prove after the choice: the selector picked a row, and the row must still be the
			// exact frozen case standing where it was measured from.
			// Null default, because the left operand short-circuits: a chosen object that failed
			// validation never reaches the prover, and the null coalesces to "it left its identity".
			string detail = null;
			if (!GameObject.Validate(chosen)
				|| !KingdomScenarioGallerySlice.TryProveExactCase(chosen, Expected, out detail))
				return Refuse("the selected building is no longer the frozen case ("
					+ (detail ?? "it left its identity") + ")", out Failure);
			if (chosen.CurrentCell == null || chosen.CurrentCell.X != Rows[index].X
				|| chosen.CurrentCell.Y != Rows[index].Y
				|| !string.Equals(chosen.IDIfAssigned, Rows[index].Id, StringComparison.Ordinal))
				return Refuse("the selected building moved or changed identity after selection",
					out Failure);
			Chosen = chosen;
			return true;
		}

		/// <summary>Applies the deterministic ordering to both lists at once.</summary>
		private static void Order(IList<GameObject> Matches, List<KingdomScenarioOwnerRow> Rows)
		{
			List<KingdomScenarioOwnerRow> before = new List<KingdomScenarioOwnerRow>(Rows);
			KingdomScenarioSelectorRules.Sort(Rows);
			GameObject[] ordered = new GameObject[Matches.Count];
			for (int i = 0; i < Rows.Count; i++) ordered[i] = Matches[before.IndexOf(Rows[i])];
			for (int i = 0; i < ordered.Length; i++) Matches[i] = ordered[i];
		}

		/// <summary>The exact case the plan's single mutating step froze.</summary>
		private static bool TryExpectedCase(KingdomScenarioPlan Plan,
			out KingdomScenarioGallerySlice.Case Expected, out string Failure)
		{
			Expected = null;
			Failure = null;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomScenarioResolvedStep step = Plan.Steps[i];
				if (!KingdomScenarioVerbSchema.Mutates(step.Verb)) continue;
				// The ordinary capture route measures stamped architecture owners only. Refusing
				// by name beats refusing over a missing case argument: an operator pointing this
				// at the founding scenario should read "no capture route yet", not a shape fault.
				if (step.Verb != KingdomScenarioVerb.StageGalleryCase)
					return Refuse("this scenario's production transaction is not an architecture "
						+ "staging; the ordinary capture route measures stamped architecture "
						+ "owners only, and authority class '"
						+ KingdomScenarioRules.Bounded(Plan.AuthorityClass)
						+ "' has no capture route yet", out Failure);
				string facing;
				string build;
				string variant;
				if (!step.Arguments.TryGetValue("Facing", out facing)
					|| !step.Arguments.TryGetValue("Build", out build)
					|| !step.Arguments.TryGetValue("Variant", out variant))
					return Refuse("the resolved transaction is missing a frozen case argument",
						out Failure);
				return KingdomScenarioGallerySlice.TryResolveCase(build, variant, facing,
					out Expected, out Failure);
			}
			return Refuse("this scenario declares no production transaction, so it names no "
				+ "building to anchor", out Failure);
		}

		private static string Report(string AnchorId, KingdomScenarioPlan Plan,
			IDictionary<string, string> Captured, string KeySetDigest)
		{
			StringBuilder sb = new StringBuilder("{{C|Ordinary-play capture}}  anchor ")
				.Append(AnchorId).Append("\nauthority ").Append(Plan.AuthorityClass);
			foreach (KeyValuePair<string, string> row in Captured)
				sb.Append("\n  ").Append(row.Key).Append(" = ").Append(row.Value);
			sb.Append("\n  keyset digest = ").Append(KeySetDigest);
			// The ruled v1 granularity, printed where the curator decides rather than left in a
			// ledger they are not reading at this moment. Plot-plus-receipt proves a commission
			// happened; it cannot prove WHICH route commissioned it.
			sb.Append("\n\n{{R|CURATION CAVEAT}}: plot+receipt evidence cannot distinguish an "
				+ "ordinary commission from an inherited, relocated, socketed, or plot2 origin. "
				+ "Curate only a building you commissioned in this run.");
			sb.Append("\n\n{{C|Curated row}} - paste into Harness/KingdomScenarioAnchors.xml:\n\n")
				.Append(Row(AnchorId, Plan, KeySetDigest));
			sb.Append("\n\n{{K|Nothing was written to the anchor store. Curating this row is your "
				+ "deliberate act; the harness never founds its own anchor.}}");
			KingdomScenarioEvidence.Record(Plan, KeySetDigest, false,
				"ordinary-play capture for anchor " + AnchorId);
			return sb.ToString();
		}

		internal static string Row(string AnchorId, KingdomScenarioPlan Plan, string KeySetDigest)
		{
			return "<anchor AnchorId=\"" + AnchorId + "\""
				+ " AuthorityClass=\"" + Plan.AuthorityClass + "\""
				+ " Verbs=\"" + Plan.Verbs + "\""
				+ " KeySetDigest=\"" + KeySetDigest + "\""
				+ " DefinitionDigest=\"" + Plan.DefinitionDigest + "\""
				+ " PlanDigest=\"" + Plan.PlanDigest + "\""
				+ " ModVersion=\"" + KingdomReleaseInfo.Version + "\""
				+ " QudCoreVersion=\"" + XRLGame.CoreVersion + "\""
				+ " Reached=\"ordinary-play\" />";
		}

		/// <summary>Splits "&lt;anchor-id&gt; &lt;scenario-request&gt; [at=x,y|id=...]".</summary>
		private static bool TrySplit(string Parameter, out string AnchorId, out string Request,
			out string Selector, out string Failure)
		{
			AnchorId = null;
			Request = null;
			Selector = null;
			Failure = null;
			string[] words = (Parameter ?? "").Trim().Split(new char[] { ' ' },
				StringSplitOptions.RemoveEmptyEntries);
			if (words.Length < 2 || words.Length > 3)
				return Refuse("use kingdom:scenario capture <anchor-id> <scenario-key>[;param=value]"
					+ " [at=<x>,<y>|id=<object-id>]", out Failure);
			if (!KingdomScenarioRules.SafeToken(words[0]))
				return Refuse("the anchor id is malformed", out Failure);
			AnchorId = words[0];
			Request = words[1];
			Selector = words.Length == 3 ? words[2] : "";
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
