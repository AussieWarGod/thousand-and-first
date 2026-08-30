using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Canonical text and digests for scenario rows and resolved plans. Split from the preflight
	/// rules to keep each shard under the house line cap; the structural verdict still comes from
	/// the one shared row validator, so a row without canonical text is exactly a row planning
	/// refuses.
	/// </summary>
	internal static class KingdomScenarioDigests
	{
		private const char Unit = '\u0001';
		private const char Record = '\u0002';

		/// <summary>
		/// Canonical digest over one resolved plan. Binds the exact parameter selection and every
		/// resolved argument, so a later request that differs in any of them cannot pass as the
		/// plan that was stamped.
		/// </summary>
		internal static string Plan(KingdomScenarioPlan Resolved)
		{
			if (Resolved == null || !KingdomScenarioRules.SafeToken(Resolved.Key) || !KingdomScenarioRules.SafeToken(Resolved.AuthorityClass)
				|| string.IsNullOrEmpty(Resolved.Verbs) || Resolved.Bindings == null
				|| Resolved.Steps == null) return null;
			StringBuilder sb = new StringBuilder();
			// The seed is deliberately absent: a plan digest binds WHAT will be executed, not which
			// world it runs in. Including it would make every curated anchor profile-specific.
			sb.Append(Resolved.Key).Append(Unit).Append(Resolved.AuthorityClass)
				.Append(Unit).Append(Resolved.Verbs)
				.Append(Unit).Append(Resolved.Synthetic ? "1" : "0")
				.Append(Unit).Append(Resolved.AnchorId ?? "")
				.Append(Unit).Append(Resolved.DefinitionDigest ?? "");
			List<string> names = new List<string>(Resolved.Bindings.Keys);
			names.Sort(StringComparer.Ordinal);
			for (int i = 0; i < names.Count; i++)
				sb.Append(Record).Append(names[i]).Append('=').Append(Resolved.Bindings[names[i]] ?? "");
			for (int i = 0; i < Resolved.Steps.Count; i++)
			{
				KingdomScenarioResolvedStep step = Resolved.Steps[i];
				if (step == null || step.Arguments == null) return null;
				sb.Append(Record).Append(KingdomScenarioRules.VerbToken(step.Verb) ?? "?");
				List<string> keys = new List<string>(step.Arguments.Keys);
				keys.Sort(StringComparer.Ordinal);
				for (int a = 0; a < keys.Count; a++)
					sb.Append(Unit).Append(keys[a]).Append('=').Append(step.Arguments[keys[a]] ?? "");
			}
			return Sha256(sb.ToString());
		}

		/// <summary>Digest over the whole authored registry. Null when any row is malformed.</summary>
		internal static string Registry(IList<KingdomScenarioDefinition> Definitions)
		{
			if (Definitions == null) return null;
			List<string> rows = new List<string>();
			for (int i = 0; i < Definitions.Count; i++)
			{
				string row = Canonical(Definitions[i]);
				if (row == null) return null;
				rows.Add(row);
			}
			rows.Sort(StringComparer.Ordinal);
			return Sha256(string.Join("\n", rows));
		}

		/// <summary>
		/// Canonical text for one row, or null when the shared validator rejects it. Canonical text
		/// exists only for rows planning would also accept.
		/// </summary>
		internal static string Canonical(KingdomScenarioDefinition Definition)
		{
			if (!KingdomScenarioRowValidator.Valid(Definition)) return null;
			// Provenance is part of the row's identity, because the roster is MERGED: two mods can
			// ship rows with the same shape, and a digest blind to who authored which would let one
			// mod's roster pass as another's. Empty is lawful (an unowned stream); a separator
			// character inside an owner id is not, because it could forge a field boundary, and an
			// uncanonicalizable row makes the whole digest null rather than a laundered value.
			string owner = Definition.Owner ?? "";
			if (owner.IndexOf(Unit) >= 0 || owner.IndexOf(Record) >= 0) return null;
			StringBuilder sb = new StringBuilder();
			sb.Append(owner).Append(Unit)
				.Append(Definition.Key).Append(Unit).Append(Definition.Family)
				.Append(Unit).Append(Definition.AuthorityClass)
				.Append(Unit).Append(Definition.SyntheticRaw)
				.Append(Unit).Append(Definition.AnchorId ?? "");
			for (int i = 0; i < Definition.Parameters.Count; i++)
			{
				KingdomScenarioParameter parameter = Definition.Parameters[i];
				sb.Append(Record).Append(parameter.Name).Append('=')
					.Append(string.Join(",", Domain(parameter)));
			}
			for (int i = 0; i < Definition.Steps.Count; i++)
			{
				KingdomScenarioStep step = Definition.Steps[i];
				sb.Append(Record).Append(KingdomScenarioRules.VerbToken(step.Verb));
				List<string> keys = new List<string>(step.Arguments.Keys);
				keys.Sort(StringComparer.Ordinal);
				for (int a = 0; a < keys.Count; a++)
					sb.Append(Unit).Append(keys[a]).Append('=').Append(step.Arguments[keys[a]] ?? "");
			}
			return sb.ToString();
		}

		private static string[] Domain(KingdomScenarioParameter Parameter)
		{
			if (Parameter == null || Parameter.Domain == null) return new string[0];
			List<string> values = new List<string>();
			for (int i = 0; i < Parameter.Domain.Count; i++) values.Add(Parameter.Domain[i] ?? "");
			return values.ToArray();
		}

		internal static string Sha256(string Value)
		{
			if (Value == null) return null;
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Value));
				StringBuilder text = new StringBuilder(hash.Length * 2);
				for (int i = 0; i < hash.Length; i++)
					text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				return text.ToString();
			}
		}
	}
}
