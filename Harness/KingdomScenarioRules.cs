using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Pure scenario preflight and digest rules.
	/// <para>
	/// Total and fail-closed: every entry point is safe to call on an arbitrarily malformed model,
	/// including one built directly rather than parsed from the registry, and answers with a
	/// refusal or null instead of throwing. Structural judgement is delegated to
	/// <see cref="KingdomScenarioRowValidator"/> so planning and canonicalization cannot disagree
	/// with registry load about what a well-formed row is.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioRules
	{
		internal const int MaxScenarios = 256;
		internal const int MaxTextChars = KingdomScenarioRowValidator.MaxTextChars;
		internal const int MaxSteps = KingdomScenarioRowValidator.MaxSteps;

		internal static bool TryParseVerb(string Raw, out KingdomScenarioVerb Verb, out string Failure)
		{
			Verb = KingdomScenarioVerb.None;
			Failure = null;
			if (string.IsNullOrEmpty(Raw)) return Refuse("A step declares no verb.", out Failure);
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "provecatalogue": Verb = KingdomScenarioVerb.ProveCatalogue; return true;
				case "stagegallerycase": Verb = KingdomScenarioVerb.StageGalleryCase; return true;
				default:
					return Refuse("Unknown scenario verb '" + Bounded(Raw)
						+ "'; the verb set is closed.", out Failure);
			}
		}

		internal static string VerbToken(KingdomScenarioVerb Verb)
		{
			switch (Verb)
			{
				case KingdomScenarioVerb.ProveCatalogue: return "provecatalogue";
				case KingdomScenarioVerb.StageGalleryCase: return "stagegallerycase";
				default: return null;
			}
		}

		/// <summary>Structural faults across a whole authored registry.</summary>
		internal static IList<string> Validate(IList<KingdomScenarioDefinition> Definitions)
		{
			List<string> findings = new List<string>();
			if (Definitions == null || Definitions.Count == 0)
			{
				findings.Add("the scenario registry is empty");
				return findings;
			}
			// Bounded ACCESS, not a bounded answer: an over-cap registry is refused before a single
			// row is validated, so a hostile roster cannot cost a full traversal to reject.
			if (Definitions.Count > MaxScenarios)
			{
				findings.Add("the scenario registry exceeds " + MaxScenarios + " rows");
				return findings;
			}
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Definitions.Count; i++)
			{
				KingdomScenarioDefinition row = Definitions[i];
				string label = row != null && SafeToken(row.Key) ? row.Key : "row " + i;
				if (row != null && SafeToken(row.Key) && !seen.Add(row.Key))
					findings.Add("duplicate scenario key " + row.Key);
				IList<string> rowFindings = KingdomScenarioRowValidator.Findings(row);
				for (int f = 0; f < rowFindings.Count; f++)
					findings.Add(label + ": " + rowFindings[f]);
			}
			return findings;
		}

		/// <summary>
		/// Resolves one step's arguments against the closed per-verb schema, rejecting anything the
		/// verb does not admit. In authoring mode a parameter reference only has to name a declared
		/// parameter, because no selection exists yet.
		/// </summary>
		internal static bool TryResolveArguments(KingdomScenarioStep Step,
			KingdomScenarioDefinition Definition, IDictionary<string, string> Bindings,
			bool AuthoringOnly, out IDictionary<string, string> Resolved, out string Failure)
		{
			Resolved = null;
			Failure = null;
			if (Step == null) return Refuse("the step is empty", out Failure);
			if (Step.Arguments == null) return Refuse("the step has no argument map", out Failure);
			if (VerbToken(Step.Verb) == null)
				return Refuse("the step has no admitted verb", out Failure);
			IList<KingdomScenarioArgumentSpec> specs = KingdomScenarioVerbSchema.Arguments(Step.Verb);
			Dictionary<string, string> resolved = new Dictionary<string, string>(StringComparer.Ordinal);
			for (int i = 0; i < specs.Count; i++)
			{
				KingdomScenarioArgumentSpec spec = specs[i];
				string raw;
				if (!Step.Arguments.TryGetValue(spec.Name, out raw) || raw == null)
				{
					if (spec.Required)
						return Refuse("argument '" + spec.Name + "' is required", out Failure);
					continue;
				}
				string parameterName;
				if (KingdomScenarioVerbSchema.IsParameterReference(raw, out parameterName))
				{
					if (!DeclaresParameter(Definition, parameterName))
						return Refuse("argument '" + spec.Name + "' references undeclared parameter '"
							+ Bounded(parameterName) + "'", out Failure);
					if (AuthoringOnly) { resolved[spec.Name] = raw; continue; }
					if (Bindings == null || !Bindings.TryGetValue(parameterName, out raw))
						return Refuse("argument '" + spec.Name + "' has no bound value for '"
							+ Bounded(parameterName) + "'", out Failure);
				}
				if (!KingdomScenarioVerbSchema.ValidValue(spec.Kind, raw))
					return Refuse("argument '" + spec.Name + "' has a malformed value", out Failure);
				resolved[spec.Name] = raw;
			}
			foreach (KeyValuePair<string, string> supplied in Step.Arguments)
				if (!Declared(specs, supplied.Key))
					return Refuse("argument '" + Bounded(supplied.Key)
						+ "' is not admitted by this verb", out Failure);
			Resolved = resolved;
			return true;
		}

		/// <summary>
		/// Full preflight. The row must be structurally well formed by the shared validator, every
		/// parameter must come from its closed domain, and every step argument must resolve, all
		/// before a caller may mutate anything. The plan holds only exact resolved values.
		/// </summary>
		internal static bool TryPlan(KingdomScenarioDefinition Definition,
			IDictionary<string, string> Selection, string DefinitionDigest, string Seed,
			out KingdomScenarioPlan Plan, out string Failure)
		{
			Plan = null;
			Failure = null;
			if (Definition == null) return Refuse("No scenario was selected.", out Failure);
			if (!ValidDigest(DefinitionDigest))
				return Refuse("The scenario registry digest is malformed.", out Failure);
			IList<string> findings = KingdomScenarioRowValidator.Findings(Definition);
			if (findings.Count > 0)
				return Refuse("Scenario "
					+ (SafeToken(Definition.Key) ? Definition.Key : "(unkeyed)")
					+ " is malformed: " + string.Join("; ", findings), out Failure);
			bool synthetic;
			if (!KingdomScenarioVerbSchema.TryParseSynthetic(Definition.SyntheticRaw,
				out synthetic, out Failure)) return false;
			Dictionary<string, string> bound;
			if (!TryBind(Definition, Selection, out bound, out Failure)) return false;
			KingdomScenarioPlan plan = new KingdomScenarioPlan
			{
				Key = Definition.Key,
				AuthorityClass = Definition.AuthorityClass,
				Seed = Seed,
				Synthetic = synthetic,
				AnchorId = Definition.AnchorId,
				Bindings = bound,
				DefinitionDigest = DefinitionDigest
			};
			StringBuilder verbs = new StringBuilder();
			for (int i = 0; i < Definition.Steps.Count; i++)
			{
				KingdomScenarioStep step = Definition.Steps[i];
				IDictionary<string, string> resolved;
				string stepFailure;
				if (!TryResolveArguments(step, Definition, bound, false, out resolved, out stepFailure))
					return Refuse("Scenario " + Definition.Key + " step " + (i + 1) + ": "
						+ stepFailure, out Failure);
				plan.Steps.Add(new KingdomScenarioResolvedStep { Verb = step.Verb, Arguments = resolved });
				if (verbs.Length > 0) verbs.Append('+');
				verbs.Append(VerbToken(step.Verb));
			}
			plan.Verbs = verbs.ToString();
			plan.PlanDigest = KingdomScenarioDigests.Plan(plan);
			if (!ValidDigest(plan.PlanDigest))
				return Refuse("The resolved plan could not be digested.", out Failure);
			Plan = plan;
			return true;
		}

		private static bool TryBind(KingdomScenarioDefinition Definition,
			IDictionary<string, string> Selection, out Dictionary<string, string> Bound,
			out string Failure)
		{
			Bound = new Dictionary<string, string>(StringComparer.Ordinal);
			Failure = null;
			IList<KingdomScenarioParameter> parameters = Definition.Parameters;
			for (int i = 0; i < parameters.Count; i++)
			{
				KingdomScenarioParameter parameter = parameters[i];
				string value;
				if (Selection == null || !Selection.TryGetValue(parameter.Name, out value))
					return Refuse("Scenario " + Definition.Key + " needs a value for '"
						+ parameter.Name + "' (" + string.Join("|", Domain(parameter)) + ").",
						out Failure);
				if (!Contains(parameter.Domain, value))
					return Refuse("'" + Bounded(value) + "' is not a declared value for '"
						+ parameter.Name + "' (" + string.Join("|", Domain(parameter)) + ").",
						out Failure);
				Bound[parameter.Name] = value;
			}
			if (Selection != null)
				foreach (KeyValuePair<string, string> supplied in Selection)
					if (!Bound.ContainsKey(supplied.Key))
						return Refuse("Scenario " + Definition.Key + " declares no parameter '"
							+ Bounded(supplied.Key) + "'.", out Failure);
			return true;
		}

		private static bool DeclaresParameter(KingdomScenarioDefinition Definition, string Name)
		{
			if (Definition == null || Definition.Parameters == null || !SafeToken(Name)) return false;
			for (int i = 0; i < Definition.Parameters.Count; i++)
			{
				KingdomScenarioParameter parameter = Definition.Parameters[i];
				if (parameter != null
					&& string.Equals(parameter.Name, Name, StringComparison.Ordinal)) return true;
			}
			return false;
		}

		private static bool Declared(IList<KingdomScenarioArgumentSpec> Specs, string Name)
		{
			for (int i = 0; i < Specs.Count; i++)
				if (string.Equals(Specs[i].Name, Name, StringComparison.Ordinal)) return true;
			return false;
		}

		private static string[] Domain(KingdomScenarioParameter Parameter)
		{
			if (Parameter == null || Parameter.Domain == null) return new string[0];
			List<string> values = new List<string>();
			for (int i = 0; i < Parameter.Domain.Count; i++) values.Add(Parameter.Domain[i] ?? "");
			return values.ToArray();
		}

		private static bool Contains(IList<string> Domain, string Value)
		{
			if (Domain == null || Value == null) return false;
			for (int i = 0; i < Domain.Count; i++)
				if (string.Equals(Domain[i], Value, StringComparison.Ordinal)) return true;
			return false;
		}

		internal static string Bounded(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "";
			return Value.Length <= MaxTextChars ? Value : Value.Substring(0, MaxTextChars);
		}

		internal static bool ValidSeed(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return false;
			return Value[0] == '#'
				? Value.Length > 1 && SafeToken(Value.Substring(1))
				: SafeToken(Value);
		}

		internal static bool ValidDigest(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		internal static bool SafeToken(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > 96) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= 'a' && Value[i] <= 'z')
					|| (Value[i] >= '0' && Value[i] <= '9')
					|| Value[i] == '-' || Value[i] == '.')) return false;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
