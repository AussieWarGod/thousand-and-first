using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Closed vocabulary for concrete facts allowed to revise polity expression.</summary>
	public enum KingdomPolityProfileFactKind : byte
	{
		None = 0,
		Decision = 1,
		Creed = 2,
		Style = 3,
		Technology = 4,
		Alliance = 5,
		Relationship = 6,
		Legacy = 7,
		// Append-only causal sources. These are deliberately distinct from style/origin prose:
		// body and transformation surfaces may only read a fact that actually proves them.
		Population = 8,
		Practice = 9,
		Transformation = 10,
		Covenant = 11,
		Work = 12,
		Cargo = 13
	}

	/// <summary>Closed, portable expression surfaces. Values are append-only wire ordinals.</summary>
	public enum KingdomPolityExpressionKind : byte
	{
		None = 0,
		Body = 1,
		Role = 2,
		Skill = 3,
		Mutation = 4,
		Cybernetic = 5,
		Gear = 6,
		Signature = 7,
		Cargo = 8,
		Dialogue = 9
	}

	/// <summary>One legal weighted expression and the exact fact that justified it.</summary>
	[Serializable]
	public sealed class KingdomPolityExpressionCue
	{
		public KingdomPolityExpressionKind Kind;
		public string ExpressionKey;
		public int Weight;
		public KingdomPolityProfileFactKind SourceKind;
		public string SourceValueKey;
		public string SourceRef;
		public string ReasonFactId;
	}

	/// <summary>
	/// One exact source value. The value is descriptive evidence, never an ancestry-to-behaviour
	/// rule; phenotype remains pinned from the preceding immutable profile.
	/// </summary>
	[Serializable]
	public sealed class KingdomPolityProfileFact
	{
		public string FactId;
		public KingdomPolityProfileFactKind Kind;
		public string ValueKey;
		public string SourceRef;
	}

	/// <summary>Canonical fact offer for one append-only profile revision.</summary>
	[Serializable]
	public sealed class KingdomPolityProfileFactSet
	{
		public string PolityId;
		public string ProfileId;
		public int PreviousRevision;
		public long EffectiveTick;
		public int TechnologyBand;
		public List<KingdomPolityProfileFact> Facts =
			new List<KingdomPolityProfileFact>();
	}
}
