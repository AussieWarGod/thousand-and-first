using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomApiRules
	{
		/// <summary>Canonicalizes one behaviour key and proves the registered owner owns its
		/// namespace. Unqualified input is filed under the owner; same-owner qualified input is
		/// accepted; foreign, blank, malformed, or overlong input is refused rather than truncated.</summary>
		/// <param name="ModName">Registered owning mod manifest ID.</param>
		/// <param name="Source">Owner-local or same-owner qualified key.</param>
		/// <returns>Owner-qualified key, or null.</returns>
		public static string ExtensionKey(string ModName, string Source)
		{
			string owner = Slug(ModName);
			if (string.IsNullOrEmpty(owner) || string.IsNullOrWhiteSpace(Source)) return null;
			string raw = Source.Trim().ToLowerInvariant();
			int colon = raw.IndexOf(':');
			string local;
			if (colon >= 0)
			{
				if (colon == 0 || colon != raw.LastIndexOf(':') || colon == raw.Length - 1
					|| !string.Equals(raw.Substring(0, colon), owner, StringComparison.Ordinal))
					return null;
				local = Slug(raw.Substring(colon + 1));
				if (!string.Equals(local, raw.Substring(colon + 1), StringComparison.Ordinal)) return null;
			}
			else
			{
				local = Slug(raw);
				if (!string.Equals(local, raw, StringComparison.Ordinal)) return null;
			}
			if (string.IsNullOrEmpty(local)) return null;
			string key = owner + ":" + local;
			return key.Length <= MaxBehaviourIdentifierLength ? key : null;
		}

		/// <summary>Bounds an engine-facing property, liquid, blueprint, unit, or node identifier
		/// without changing its case. Control characters, markup braces, and overlong values are
		/// refused; surrounding whitespace is removed.</summary>
		/// <param name="Source">Proposed identifier.</param>
		/// <param name="Required">Whether empty input is a refusal.</param>
		/// <returns>Bounded identifier, empty for an allowed absence, or null on refusal.</returns>
		public static string BehaviourIdentifier(string Source, bool Required)
		{
			if (string.IsNullOrWhiteSpace(Source)) return Required ? null : "";
			string value = Source.Trim();
			if (value.Length > MaxBehaviourIdentifierLength || value.IndexOf('{') >= 0
				|| value.IndexOf('}') >= 0 || value.IndexOf('|') >= 0) return null;
			for (int i = 0; i < value.Length; i++)
				if (char.IsControl(value[i])) return null;
			return value;
		}

	}
}
