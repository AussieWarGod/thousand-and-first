using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>What shape one step argument may take once resolved.</summary>
	internal enum KingdomScenarioArgumentKind : byte
	{
		/// <summary>Lowercase token: a-z, 0-9, '-', '.'.</summary>
		Token = 0,

		/// <summary>Bounded display text with no control or field-separator characters.</summary>
		Name = 1
	}

	internal sealed class KingdomScenarioArgumentSpec
	{
		internal string Name;
		internal KingdomScenarioArgumentKind Kind;
		internal bool Required;
	}

	/// <summary>
	/// The closed argument schema for each closed verb, and which verbs mutate.
	/// <para>
	/// Atomicity is structural here rather than promised by the runner: a scenario may declare at
	/// most one mutating verb, and it must be the last step. Read-only observations therefore all
	/// prove out before anything changes, and there is no interleaving for a save cut to tear.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioVerbSchema
	{
		internal const int MaxNameChars = 64;

		internal static IList<KingdomScenarioArgumentSpec> Arguments(KingdomScenarioVerb Verb)
		{
			List<KingdomScenarioArgumentSpec> specs = new List<KingdomScenarioArgumentSpec>();
			switch (Verb)
			{
				case KingdomScenarioVerb.ProveCatalogue:
					specs.Add(Spec("Catalogue", KingdomScenarioArgumentKind.Token, true));
					break;
				case KingdomScenarioVerb.StageGalleryCase:
					specs.Add(Spec("Suite", KingdomScenarioArgumentKind.Token, true));
					// The exact expected case is frozen in authored data and compared field by
					// field after staging, so the pose selects a known case rather than whatever
					// a positional index happens to land on.
					specs.Add(Spec("Build", KingdomScenarioArgumentKind.Token, true));
					specs.Add(Spec("Variant", KingdomScenarioArgumentKind.Token, true));
					specs.Add(Spec("Facing", KingdomScenarioArgumentKind.Token, true));
					break;
				case KingdomScenarioVerb.FoundFirstCity:
					// The one caller-supplied fact production founding needs: the city's display
					// name. Frozen in the authored roster rather than requested per run, so the
					// scenario carries no open parameter surface it does not need.
					specs.Add(Spec("CityName", KingdomScenarioArgumentKind.Name, true));
					break;
			}
			return specs;
		}

		/// <summary>Verbs that change production state. At most one per scenario, declared last.</summary>
		internal static bool Mutates(KingdomScenarioVerb Verb)
		{
			return Verb == KingdomScenarioVerb.StageGalleryCase
				|| Verb == KingdomScenarioVerb.FoundFirstCity;
		}

		private static KingdomScenarioArgumentSpec Spec(string Name,
			KingdomScenarioArgumentKind Kind, bool Required)
		{
			return new KingdomScenarioArgumentSpec { Name = Name, Kind = Kind, Required = Required };
		}

		/// <summary>
		/// Exactly "true" or "false", lowercase. Any other text is a fault rather than a silent
		/// downgrade: reading a typo as "not synthetic" would promote a recovery diagnostic into an
		/// acceptance-eligible state.
		/// </summary>
		internal static bool TryParseSynthetic(string Raw, out bool Synthetic, out string Failure)
		{
			Synthetic = false;
			Failure = null;
			if (Raw == null)
				return Refuse("Synthetic is missing; declare exactly \"true\" or \"false\".",
					out Failure);
			if (string.Equals(Raw, "true", StringComparison.Ordinal)) { Synthetic = true; return true; }
			if (string.Equals(Raw, "false", StringComparison.Ordinal)) return true;
			return Refuse("Synthetic must be exactly \"true\" or \"false\" in lowercase, not \""
				+ KingdomScenarioRules.Bounded(Raw) + "\".", out Failure);
		}

		/// <summary>A value may reference one declared parameter as <c>{name}</c>.</summary>
		internal static bool IsParameterReference(string Value, out string ParameterName)
		{
			ParameterName = null;
			if (Value == null || Value.Length < 3 || Value[0] != '{'
				|| Value[Value.Length - 1] != '}') return false;
			ParameterName = Value.Substring(1, Value.Length - 2);
			return true;
		}

		internal static bool ValidValue(KingdomScenarioArgumentKind Kind, string Value)
		{
			switch (Kind)
			{
				case KingdomScenarioArgumentKind.Token: return KingdomScenarioRules.SafeToken(Value);
				case KingdomScenarioArgumentKind.Name: return ValidName(Value);
				default: return false;
			}
		}

		private static bool ValidName(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxNameChars) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (c < ' ' || c > '~' || c == '|' || c == '{' || c == '}') return false;
			}
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
