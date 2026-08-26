using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Immutable scalar view of one exact building-to-tier mapping. Preview tools may enumerate
	/// these records, then ask <see cref="KingdomArchitecture.TryResolveVariant"/> for each named
	/// variant and pose. Mutable XML drafts are never exposed.
	/// </summary>
	public sealed class KingdomArchitectureMapping
	{
		private readonly string[] variantKeys;

		public string BuildKey { get; private set; }
		public string BuildingBlueprint { get; private set; }
		public string Category { get; private set; }
		public string PlanKey { get; private set; }
		public string BindingKey { get; private set; }
		public string TierKey { get; private set; }
		public int TierLevel { get; private set; }
		public string TypeKey { get; private set; }
		public ArchitectureLotSize LotSize { get; private set; }
		public ArchitectureFrontage Frontage { get; private set; }
		public string DefaultMapKey { get; private set; }
		public string DefaultPaletteKey { get; private set; }

		/// <summary>A defensive, ordinal list suitable for deterministic gallery generation.</summary>
		public IList<string> VariantKeys
		{
			get { return Array.AsReadOnly((string[])variantKeys.Clone()); }
		}

		internal KingdomArchitectureMapping(string BuildingBlueprint, string Category,
			string PlanKey,
			ArchitectureBindingDraft Binding, ArchitectureTierDraft Tier)
		{
			BuildKey = Tier.BuildKey;
			this.BuildingBlueprint = BuildingBlueprint;
			this.Category = Category;
			this.PlanKey = PlanKey;
			BindingKey = Binding.Key;
			TierKey = Tier.Key;
			TierLevel = Tier.Level;
			TypeKey = Binding.TypeKey;
			LotSize = Binding.Size;
			Frontage = Binding.Frontage;
			DefaultMapKey = Tier.MapKey;
			DefaultPaletteKey = Tier.PaletteKey;
			variantKeys = new string[Tier.Variants.Count];
			for (int i = 0; i < Tier.Variants.Count; i++) variantKeys[i] = Tier.Variants[i].Key;
			Array.Sort(variantKeys, StringComparer.Ordinal);
		}
	}
}
