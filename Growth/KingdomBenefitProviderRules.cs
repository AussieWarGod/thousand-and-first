using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Bounded parser for the XML provider surface.</summary>
	public static class KingdomBenefitProviderRules
	{
		public const int MaxKeyChars = 128;
		public const int MaxCarriesChars = 1024;
		public const int MaxProvidesChars = 1024;
		public const int MaxBenefitRows = 16;

		public static bool TryDescribe(string Key, string Carries, string Provides,
			string Scope, string Operation, string NetworkKey,
			out KingdomBenefitProviderDeclaration Declaration,
			out string Failure)
		{
			Declaration = null;
			Failure = null;
			string key = Fold(Key);
			if (!ValidKey(key)) return Fail("provider key is malformed", out Failure);
			if ((Carries ?? "").Length > MaxCarriesChars
				|| (Provides ?? "").Length > MaxProvidesChars)
				return Fail("provider declaration exceeds its text bound", out Failure);
			if (!TryScope(Scope, out KingdomBenefitScope scope))
				return Fail("provider scope is unknown", out Failure);
			if (!TryOperation(Operation, out KingdomBenefitOperation operation))
				return Fail("provider operation is unknown", out Failure);
			if (operation == KingdomBenefitOperation.Filled)
				return Fail("filled operation has no typed contents contract", out Failure);
			string networkKey = Fold(NetworkKey);
			if (scope == KingdomBenefitScope.Network && !ValidKey(networkKey))
				return Fail("network-scoped provider needs an exact network key", out Failure);
			if (scope != KingdomBenefitScope.Network && networkKey.Length != 0)
				return Fail("only a network-scoped provider may name a network key", out Failure);
			List<KindAmount> carries;
			if (!KingdomCatalogueRules.TryParseTally(Carries, out carries, out Failure)) return false;
			if (carries.Count > MaxBenefitRows)
				return Fail("provider declares too many carried benefit rows", out Failure);
			Dictionary<string, int> folded = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < carries.Count; i++)
			{
				string kind = Fold(carries[i].Kind);
				if (!KingdomDesignationRules.SafeToken(kind, 64) || carries[i].Amount <= 0)
					return Fail("provider amounts must be positive", out Failure);
				if (kind == KingdomCatalogueRules.SupportWater
					|| kind == KingdomCatalogueRules.SupportFood)
					return Fail("food and water use their physical inventory and flow contracts",
						out Failure);
				int prior;
				folded.TryGetValue(kind, out prior);
				long sum = (long)prior + carries[i].Amount;
				folded[kind] = sum >= int.MaxValue ? int.MaxValue : (int)sum;
			}
			carries.Clear();
			foreach (KeyValuePair<string, int> pair in folded)
				carries.Add(new KindAmount(pair.Key, pair.Value));
			carries.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind));
			if (!TryPositiveTags(Provides, out List<string> tags, out Failure)) return false;
			if (carries.Count > KingdomDesignationRules.MaxCapsPerDesignation
				|| tags.Count > KingdomDesignationRules.MaxTagsPerDesignation)
				return Fail("provider declaration exceeds its benefit row bound", out Failure);
			if (carries.Count == 0 && tags.Count == 0)
				return Fail("provider declares no benefit", out Failure);
			Declaration = new KingdomBenefitProviderDeclaration {
				Key = key, NetworkKey = networkKey, Scope = scope, Operation = operation, Carries = carries,
				Provides = tags
			};
			return true;
		}

		/// <summary>Normalizes declarations returned by code providers. Extension code cannot
		/// bypass XML bounds, food/water custody, duplicate folding, or positive-tag law.</summary>
		public static bool TryNormalize(KingdomBenefitProviderDeclaration Source,
			out KingdomBenefitProviderDeclaration Declaration, out string Failure)
		{
			Declaration = null; Failure = null;
			if (Source == null || !ValidKey(Fold(Source.Key))
				|| !Enum.IsDefined(typeof(KingdomBenefitScope), Source.Scope)
				|| !Enum.IsDefined(typeof(KingdomBenefitOperation), Source.Operation)
				|| Source.Operation == KingdomBenefitOperation.Filled
				|| Source.Carries == null || Source.Provides == null
				|| Source.Carries.Count > MaxBenefitRows || Source.Provides.Count > MaxBenefitRows)
				return Fail("code provider declaration is malformed or over its row bound", out Failure);
			string network = Fold(Source.NetworkKey);
			if (Source.Scope == KingdomBenefitScope.Network
				? !ValidKey(network) : network.Length != 0)
				return Fail("code provider network scope is malformed", out Failure);
			Dictionary<string, int> amounts = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Source.Carries.Count; i++)
			{
				string kind = Fold(Source.Carries[i].Kind);
				if (!KingdomDesignationRules.SafeToken(kind, 64) || Source.Carries[i].Amount <= 0
					|| kind == KingdomCatalogueRules.SupportWater
					|| kind == KingdomCatalogueRules.SupportFood)
					return Fail("code provider carries malformed or custody-only benefit", out Failure);
				amounts.TryGetValue(kind, out int prior);
				amounts[kind] = SaturatingAdd(prior, Source.Carries[i].Amount);
			}
			List<string> tags = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Source.Provides.Count; i++)
			{
				string tag = Fold(Source.Provides[i]);
				if (!KingdomDesignationRules.SafeToken(tag, 128)
					|| tag[0] == KingdomQolRules.RemovePrefix || tag.IndexOf('|') >= 0)
					return Fail("code provider tags must be positive bounded tokens", out Failure);
				if (seen.Add(tag)) tags.Add(tag);
			}
			Declaration = new KingdomBenefitProviderDeclaration { Key = Fold(Source.Key),
				NetworkKey = network, Scope = Source.Scope, Operation = Source.Operation };
			foreach (KeyValuePair<string, int> pair in amounts)
				Declaration.Carries.Add(new KindAmount(pair.Key, pair.Value));
			Declaration.Carries.Sort((a, b) => string.CompareOrdinal(a.Kind, b.Kind));
			tags.Sort(StringComparer.Ordinal); Declaration.Provides = tags;
			return Declaration.Carries.Count > 0 || Declaration.Provides.Count > 0
				|| Fail("code provider declares no benefit", out Failure);
		}

		public static bool ScopeAccepts(KingdomBenefitScope Scope,
			KingdomBenefitCellUse Cell, bool InContainer)
		{
			switch (Scope)
			{
			case KingdomBenefitScope.Building:
				return !InContainer && (Cell & KingdomBenefitCellUse.Building) != 0;
			case KingdomBenefitScope.Covered:
				return !InContainer && (Cell & KingdomBenefitCellUse.Covered) != 0;
			case KingdomBenefitScope.Interior:
				return !InContainer && (Cell & KingdomBenefitCellUse.Interior) != 0;
			case KingdomBenefitScope.Plot:
				return !InContainer && (Cell & KingdomBenefitCellUse.Plot) != 0;
			case KingdomBenefitScope.Yard:
				return !InContainer && (Cell & KingdomBenefitCellUse.Yard) != 0;
			case KingdomBenefitScope.Container:
				return (Cell & KingdomBenefitCellUse.Plot) != 0;
			case KingdomBenefitScope.Network:
				return (Cell & KingdomBenefitCellUse.Network) != 0;
			case KingdomBenefitScope.Habitable:
				return !InContainer && (Cell & (KingdomBenefitCellUse.Interior
					| KingdomBenefitCellUse.Covered)) == (KingdomBenefitCellUse.Interior
					| KingdomBenefitCellUse.Covered);
			default:
				return false;
			}
		}

		private static bool TryScope(string Value, out KingdomBenefitScope Scope)
		{
			return Enum.TryParse(Fold(Value), true, out Scope)
				&& Enum.IsDefined(typeof(KingdomBenefitScope), Scope);
		}

		private static bool TryOperation(string Value, out KingdomBenefitOperation Operation)
		{
			return Enum.TryParse(Fold(Value), true, out Operation)
				&& Enum.IsDefined(typeof(KingdomBenefitOperation), Operation);
		}

		private static bool ValidKey(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxKeyChars) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!(char.IsLetterOrDigit(Value[i]) || "_.:+-".IndexOf(Value[i]) >= 0)) return false;
			return true;
		}

		internal static bool TryPositiveTags(string Source, out List<string> Tags,
			out string Failure)
		{
			Tags = new List<string>(); Failure = null;
			if (string.IsNullOrWhiteSpace(Source)) return true;
			string[] rows = Source.Split(KingdomQolRules.ListSeparator);
			if (rows.Length > MaxBenefitRows)
				return Fail("provider declares too many provided tag rows", out Failure);
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < rows.Length; i++)
			{
				string tag = Fold(rows[i]);
				if (tag.Length == 0 || tag[0] == KingdomQolRules.RemovePrefix
					|| !KingdomDesignationRules.SafeToken(tag, 128)
					|| tag.IndexOf('|') >= 0)
					return Fail("provider tags must be positive bounded tokens", out Failure);
				if (seen.Add(tag)) Tags.Add(tag);
			}
			Tags.Sort(StringComparer.Ordinal);
			return true;
		}

		private static string Fold(string Value) => (Value ?? "").Trim().ToLowerInvariant();
		private static int SaturatingAdd(int A, int B)
		{
			long value = (long)A + B;
			return value >= int.MaxValue ? int.MaxValue : (int)value;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
