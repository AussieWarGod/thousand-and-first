using System.Collections.Generic;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		/// <summary>Compatibility field for faction-to-realm regard. Runtime code should use
		/// <see cref="RegardForRealm"/> and the directional methods; this field name remains on the
		/// named save/API surface so existing saves and integrations retain the same map.</summary>
		public Dictionary<string, int> Standings = new Dictionary<string, int>();

		/// <summary>Faction-to-realm regard: what each foreign faction thinks of this realm.</summary>
		public Dictionary<string, int> RegardForRealm { get { return Standings; } }

		/// <summary>Realm-to-faction policy: how citizens and guards regard each foreign faction.
		/// Absence is Unspecified and projects no engine edge.</summary>
		public Dictionary<string, int> RealmPolicyToward = new Dictionary<string, int>();

		/// <summary>Signed hundredths retained when personal reputation spills into regard.</summary>
		public Dictionary<string, int> RegardSpilloverRemainders =
			new Dictionary<string, int>();

		/// <summary>Last advisory personal-reputation poststate observed per faction. It is never a
		/// deduplication authority: native Qud can set reputation without emitting an event, so equality
		/// with a later event poststate cannot prove replay. Transient and master-disabled observations
		/// never change civic regard.</summary>
		public Dictionary<string, int> RegardSpilloverObservedReputation =
			new Dictionary<string, int>();

		/// <summary>Directional relationship authority schema. Zero is unfounded or an admitted
		/// version-8 migration source; one means direction/provenance separation completed.</summary>
		public int DirectionalStandingSchemaVersion;

		/// <summary>The expelled realm's own inbound regard ledger. It is held apart from the current
		/// realm so a later founding cannot inherit another realm's relationships.</summary>
		public Dictionary<string, int> ExiledStandings = new Dictionary<string, int>();

		/// <summary>Exile mirror of realm-to-faction policy; archive remains authority.</summary>
		public Dictionary<string, int> ExiledRealmPolicyToward = new Dictionary<string, int>();

		/// <summary>Exile mirror of signed regard spillover hundredths.</summary>
		public Dictionary<string, int> ExiledRegardSpilloverRemainders =
			new Dictionary<string, int>();

		/// <summary>Exile mirror of the advisory per-faction reputation observation.</summary>
		public Dictionary<string, int> ExiledRegardSpilloverObservedReputation =
			new Dictionary<string, int>();
	}
}
