using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Read-only city input for the Great Archive's unified research map.</summary>
	public sealed class KingdomGreatArchiveCityFacts
	{
		public string SettlementId;
		public string DisplayName;
		public List<string> HeldNodeKeys = new List<string>();
	}

	/// <summary>Read-only research-node input. It deliberately carries no effort or progress.</summary>
	public sealed class KingdomGreatArchiveNodeFacts
	{
		public string Key;
		public string DisplayName;
		public string Branch;
		public int Tier;
		public bool Discovered;
		public List<KingdomGreatArchiveRequirementFacts> Requirements =
			new List<KingdomGreatArchiveRequirementFacts>();
	}

	/// <summary>One comma-separated prerequisite; its alternatives are joined by “or”.</summary>
	public sealed class KingdomGreatArchiveRequirementFacts
	{
		public List<KingdomGreatArchiveAlternativeFacts> Alternatives =
			new List<KingdomGreatArchiveAlternativeFacts>();
	}

	/// <summary>A node edge, or a visible non-node roster prerequisite.</summary>
	public sealed class KingdomGreatArchiveAlternativeFacts
	{
		public string NodeKey;
		public string DisplayName;
	}

	public sealed class KingdomGreatArchiveRow
	{
		public string Key;
		public string DisplayName;
		public string Branch;
		public int Tier;
		public List<string> HoldingCityNames = new List<string>();
		public List<string> RequirementClauses = new List<string>();

		public bool Held { get { return HoldingCityNames.Count > 0; } }
	}

	/// <summary>Bounded visualization result. No field can express research work or mutation.</summary>
	public sealed class KingdomGreatArchiveMap
	{
		public List<string> CityNames = new List<string>();
		public List<KingdomGreatArchiveRow> Rows = new List<KingdomGreatArchiveRow>();
	}
}
