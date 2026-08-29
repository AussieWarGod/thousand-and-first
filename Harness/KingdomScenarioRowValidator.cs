using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The one row validator. Registry load, direct preflight, and canonicalization all judge a row
	/// through this class, so they cannot disagree about what a well-formed scenario is: anything
	/// <see cref="Findings"/> reports is refused by planning and yields no canonical text.
	/// </summary>
	internal static class KingdomScenarioRowValidator
	{
		internal const int MaxParameters = 8;
		internal const int MaxDomainValues = 64;
		internal const int MaxTextChars = 300;

		/// <summary>Steps are bounded by the provenance verb cap: the sequence must be recordable.</summary>
		internal const int MaxSteps = KingdomScenarioProvenanceRules.MaxVerbs;

		/// <summary>Every structural fault in one row. Empty means the row is well formed.</summary>
		internal static IList<string> Findings(KingdomScenarioDefinition Row)
		{
			List<string> findings = new List<string>();
			if (Row == null) { findings.Add("the row is empty"); return findings; }
			if (!KingdomScenarioRules.SafeToken(Row.Key))
			{
				findings.Add("the row has a malformed key");
				return findings;
			}
			if (!KingdomScenarioRules.SafeToken(Row.Family)) findings.Add("malformed family");
			if (!KingdomScenarioRules.SafeToken(Row.AuthorityClass))
				findings.Add("no authority class");
			if (Row.AnchorId != null && !KingdomScenarioRules.SafeToken(Row.AnchorId))
				findings.Add("malformed anchor id");
			bool synthetic;
			string syntheticFailure;
			if (!KingdomScenarioVerbSchema.TryParseSynthetic(Row.SyntheticRaw, out synthetic,
				out syntheticFailure)) findings.Add(syntheticFailure);
			if (Overlong(Row.DisplayName) || Overlong(Row.Description))
				findings.Add("oversize authored text");
			Parameters(Row, findings);
			Steps(Row, findings);
			return findings;
		}

		internal static bool Valid(KingdomScenarioDefinition Row)
		{
			return Findings(Row).Count == 0;
		}

		private static void Parameters(KingdomScenarioDefinition Row, IList<string> Findings)
		{
			IList<KingdomScenarioParameter> parameters = Row.Parameters;
			if (parameters == null) { Findings.Add("no parameter list"); return; }
			// Refuse AT the cap, before walking: recording an over-cap finding and then traversing
			// the whole hostile list is a bounded verdict over an unbounded scan.
			if (parameters.Count > MaxParameters)
			{
				Findings.Add("more than " + MaxParameters + " parameters");
				return;
			}
			HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
			for (int p = 0; p < parameters.Count; p++)
			{
				KingdomScenarioParameter parameter = parameters[p];
				if (parameter == null || !KingdomScenarioRules.SafeToken(parameter.Name))
				{
					Findings.Add("malformed parameter name");
					continue;
				}
				if (!names.Add(parameter.Name))
					Findings.Add("parameter " + parameter.Name + " is declared twice");
				// The launcher freezes the seed into the request; an authored parameter of that
				// name would shadow it and make the request grammar ambiguous.
				if (string.Equals(parameter.Name, KingdomScenarioRequest.SeedName,
					StringComparison.Ordinal))
					Findings.Add("parameter " + parameter.Name
						+ " uses the reserved request name");
				if (parameter.Domain == null || parameter.Domain.Count == 0)
				{
					Findings.Add(parameter.Name + " has an empty domain");
					continue;
				}
				if (parameter.Domain.Count > MaxDomainValues)
				{
					Findings.Add(parameter.Name + " has an oversize domain");
					continue;
				}
				HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
				for (int v = 0; v < parameter.Domain.Count; v++)
				{
					string value = parameter.Domain[v];
					if (!KingdomScenarioRules.SafeToken(value))
						Findings.Add(parameter.Name + " has a malformed domain value");
					else if (!values.Add(value))
						Findings.Add(parameter.Name + " repeats domain value " + value);
				}
			}
		}

		private static void Steps(KingdomScenarioDefinition Row, IList<string> Findings)
		{
			IList<KingdomScenarioStep> steps = Row.Steps;
			if (steps == null || steps.Count == 0) { Findings.Add("no steps"); return; }
			if (steps.Count > MaxSteps)
			{
				Findings.Add("more than " + MaxSteps
					+ " steps; the verb sequence must stay recordable");
				return;
			}
			int mutations = 0;
			for (int s = 0; s < steps.Count; s++)
			{
				KingdomScenarioStep step = steps[s];
				string where = "step " + (s + 1);
				if (step == null) { Findings.Add(where + " is empty"); continue; }
				if (KingdomScenarioRules.VerbToken(step.Verb) == null)
				{
					Findings.Add(where + " has no admitted verb");
					continue;
				}
				if (step.Arguments == null) { Findings.Add(where + " has no argument map"); continue; }
				if (KingdomScenarioVerbSchema.Mutates(step.Verb))
				{
					mutations++;
					// Atomicity is structural: read-only observations must all prove out before
					// the single production transaction, so a cut cannot tear a partial sequence.
					if (s != steps.Count - 1)
						Findings.Add(where + " mutates production state but is not the last step");
				}
				IDictionary<string, string> ignored;
				string failure;
				if (!KingdomScenarioRules.TryResolveArguments(step, Row, null, true, out ignored,
					out failure)) Findings.Add(where + ": " + failure);
			}
			if (mutations > 1)
				Findings.Add("declares " + mutations
					+ " mutating verbs; a scenario may run at most one production transaction");
		}

		private static bool Overlong(string Value)
		{
			return Value != null && Value.Length > MaxTextChars;
		}
	}
}
