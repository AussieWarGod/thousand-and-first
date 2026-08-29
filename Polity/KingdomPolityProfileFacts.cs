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
		Legacy = 7
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
