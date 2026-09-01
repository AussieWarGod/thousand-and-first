using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomPolityMutationSpec
	{
		public string ClassName;
		public int Level;
	}

	/// <summary>Complete deterministic additions for one newly regenerated, unplaced actor.</summary>
	[Serializable]
	public sealed class KingdomPolityNpcSpec
	{
		public string ResolverDigest;
		public string ProfileId;
		public int ProfileRevision;
		public int ProfileRulesVersion;
		public string RoleKey;
		public int Ordinal;
		public int TechnologyBand;
		public string BodyBlueprint;
		public int Level;
		public int Strength;
		public int Agility;
		public int Toughness;
		public int Intelligence;
		public int Willpower;
		public int Ego;
		public int Hitpoints;
		public List<string> Skills = new List<string>();
		public List<KingdomPolityMutationSpec> Mutations =
			new List<KingdomPolityMutationSpec>();
		public List<string> GearBlueprints = new List<string>();
		public List<string> CargoBlueprints = new List<string>();
		public List<string> SignatureCues = new List<string>();
		public List<string> DialogueCues = new List<string>();
		public List<string> ReasonFactIds = new List<string>();
	}
}
