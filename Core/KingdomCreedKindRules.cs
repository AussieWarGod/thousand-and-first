using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free merge, validation, and behavior boundary for creed semantics.</summary>
	public static class KingdomCreedKindRules
	{
		public const int MaxDefinitions = 128;
		public const int MaxNameChars = 96;

		public static bool ValidName(string Name)
		{
			if (string.IsNullOrWhiteSpace(Name)) return false;
			string value = Name.Trim();
			if (value.Length > MaxNameChars) return false;
			for (int i = 0; i < value.Length; i++)
				if (char.IsControl(value[i]) || value[i] == ',') return false;
			return true;
		}

		/// <summary>Merges same-key layers. Omission inherits; blank Theology clears. A semantic
		/// kind cannot silently change with load order.</summary>
		public static bool TryMerge(KingdomCreedDraft Earlier, KingdomCreedDraft Later,
			out KingdomCreedDraft Merged, out string Error)
		{
			Merged = null;
			Error = null;
			if (Later == null) { Merged = Earlier == null ? null : Earlier.Copy(); return true; }
			if (Earlier == null) { Merged = Later.Copy(); return true; }
			if (!ValidName(Earlier.Name) || !ValidName(Later.Name)
				|| !string.Equals(Earlier.Name.Trim(), Later.Name.Trim(),
					StringComparison.OrdinalIgnoreCase))
			{
				Error = "creed layers with different or malformed Names cannot merge";
				return false;
			}
			if (Later.Kind != null && (string.IsNullOrWhiteSpace(Later.Kind)
				|| (!string.IsNullOrWhiteSpace(Earlier.Kind) && !string.Equals(
					Earlier.Kind.Trim(), Later.Kind.Trim(), StringComparison.OrdinalIgnoreCase))))
			{
				Error = "creed " + Earlier.Name + " cannot clear or change its declared Kind";
				return false;
			}
			Merged = Earlier.Copy();
			if (Later.Kind != null) Merged.Kind = Later.Kind;
			if (Later.Theology != null) Merged.Theology = Later.Theology;
			return true;
		}

		public static bool TryParse(KingdomCreedDraft Draft,
			out KingdomCreedDefinition Definition, out string Error)
		{
			Definition = null;
			Error = null;
			if (Draft == null || !ValidName(Draft.Name))
			{
				Error = "creed needs a bounded Name without commas or control characters";
				return false;
			}
			if (!TryKind(Draft.Kind, out KingdomCreedKind kind))
			{
				Error = "creed " + Draft.Name.Trim() + " has bad Kind (expected community, people, polity, order, doctrine, or cult)";
				return false;
			}
			if (!TryBoolean(Draft.Theology, out bool opted, out bool named))
			{
				Error = "creed " + Draft.Name.Trim() + " has bad Theology (expected yes or no)";
				return false;
			}
			bool intrinsic = kind == KingdomCreedKind.Doctrine || kind == KingdomCreedKind.Cult;
			if (intrinsic && named && !opted)
			{
				Error = "creed " + Draft.Name.Trim() + " cannot disable theology for doctrine or cult";
				return false;
			}
			if (opted && kind != KingdomCreedKind.Order && !intrinsic)
			{
				Error = "creed " + Draft.Name.Trim() + " may opt into theology only when Kind is order";
				return false;
			}
			Definition = new KingdomCreedDefinition
			{
				Name = Draft.Name.Trim(), Kind = kind, Theological = intrinsic || opted
			};
			return true;
		}

		public static bool TryFind(IList<KingdomCreedDefinition> Definitions, string Name,
			out KingdomCreedDefinition Definition)
		{
			Definition = null;
			if (Definitions == null || string.IsNullOrWhiteSpace(Name)) return false;
			string wanted = Name.Trim();
			for (int i = 0; i < Definitions.Count; i++)
				if (Definitions[i] != null && string.Equals(Definitions[i].Name, wanted,
					StringComparison.OrdinalIgnoreCase))
				{
					Definition = Definitions[i];
					return true;
				}
			return false;
		}

		public static bool UsesTheology(IList<KingdomCreedDefinition> Definitions, string Name)
		{
			return TryFind(Definitions, Name, out KingdomCreedDefinition found)
				&& found.Theological;
		}

		public static string AffiliationWord(KingdomCreedDefinition Definition)
		{
			if (Definition == null) return "affiliation";
			switch (Definition.Kind)
			{
			case KingdomCreedKind.Community: return "community";
			case KingdomCreedKind.People: return "people";
			case KingdomCreedKind.Polity: return "allegiance";
			case KingdomCreedKind.Order: return "order";
			case KingdomCreedKind.Doctrine: return "doctrine";
			case KingdomCreedKind.Cult: return "cult";
			default: return "affiliation";
			}
		}

		public static string AdoptionTelling(string ResidentName, string AffiliationName)
		{
			string resident = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string affiliation = string.IsNullOrEmpty(AffiliationName)
				? "the city's allegiance" : AffiliationName;
			return resident + " adopted " + affiliation + " at shared water, and the roll was amended in their own hand";
		}

		public static string AdoptionRumour(string ResidentName, string AffiliationName)
		{
			string resident = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string affiliation = string.IsNullOrEmpty(AffiliationName)
				? "the city's allegiance" : AffiliationName;
			return resident + " entered the covenant of " + affiliation
				+ " after the city poured for them, which the road remembers as a choice and a debt";
		}

		public static string AdoptionNote(string ResidentName, string AffiliationName)
		{
			string resident = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			string affiliation = string.IsNullOrEmpty(AffiliationName)
				? "the city's allegiance" : AffiliationName;
			return resident + " adopted affiliation with " + affiliation + ".";
		}

		private static bool TryKind(string Raw, out KingdomCreedKind Kind)
		{
			Kind = KingdomCreedKind.Community;
			if (string.IsNullOrWhiteSpace(Raw)) return false;
			switch (Raw.Trim().ToLowerInvariant())
			{
			case "community": Kind = KingdomCreedKind.Community; return true;
			case "people": Kind = KingdomCreedKind.People; return true;
			case "polity": Kind = KingdomCreedKind.Polity; return true;
			case "order": Kind = KingdomCreedKind.Order; return true;
			case "doctrine": Kind = KingdomCreedKind.Doctrine; return true;
			case "cult": Kind = KingdomCreedKind.Cult; return true;
			default: return false;
			}
		}

		private static bool TryBoolean(string Raw, out bool Value, out bool Named)
		{
			Value = false;
			Named = Raw != null && Raw.Length > 0;
			if (Raw == null || Raw.Length == 0) return true;
			if (string.IsNullOrWhiteSpace(Raw)) return false;
			string value = Raw.Trim();
			if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
			{ Value = true; return true; }
			if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}
	}
}
