using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomPolityFoundationRelationState : byte
	{
		Ordinary = 0,
		LegacyUnresolved = 1,
		Causal = 2
	}

	[Serializable]
	public sealed class KingdomPolityRecord
	{
		public string PolityId;
		public string DisplayName;
		public int NameRevision;
		public KingdomPolitySource Source;
		public KingdomPolityLifecycle Lifecycle;
		public string ProfileId;
		public int ProfileRevision;
		public string ProjectedFactionId;
		public string ExternalCounterpartyKey;
		public long EndedTick;
	}

	[Serializable]
	public sealed class KingdomPolityRelation
	{
		public string RelationId;
		public string FromPolityId;
		public string ToPolityId;
		public KingdomPolityRelationBand Band;
		public List<string> SourceRefs = new List<string>();
		public long ChangedTick;
		public KingdomPolityFoundationRelationState FoundationState;
		public KingdomPolityRelationBand InitialBand;
		public string FoundationOriginalCauseRef;
		public string FoundationCorrectionReceiptId;
	}

	[Serializable]
	public sealed class KingdomPolityLoadoutPolicy
	{
		public KingdomPolityLoadoutPolicyKind Kind;
		public int ExpectedValueBudget;
		public List<string> ExcludedKeys = new List<string>();
		public List<string> SelectedKeys = new List<string>();
	}

	[Serializable]
	public sealed class KingdomPolityProfileRevision
	{
		public string ProfileId;
		public int Revision;
		public string PolityId;
		public long EffectiveTick;
		public int RulesVersion;
		public List<string> DerivedFromFactIds = new List<string>();
		public string FactsDigest;
		public int TechnologyBand;
		public List<string> PracticeTags = new List<string>();
		public List<string> BodyKeys = new List<string>();
		public List<string> RoleKeys = new List<string>();
		public List<string> GearKeys = new List<string>();
		public KingdomPolityLoadoutPolicy Loadout = new KingdomPolityLoadoutPolicy();
		public List<KingdomPolityExpressionCue> ExpressionCues =
			new List<KingdomPolityExpressionCue>();
	}

	[Serializable]
	public sealed class KingdomPolityProfileRef
	{
		public string ProfileId;
		public int Revision;
	}
}
