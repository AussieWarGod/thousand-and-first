using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
		public string WriterVersion = "";

		public string EngineVersion = "";

		public KingdomSealStatus Status = KingdomSealStatus.Living;

		/// <summary>The lineage this realm belongs to. Minted once, at founding, and carried by
		/// every generation after it.</summary>
		public string LineageId = "";

		/// <summary>The unique identity of this generation's immutable result. Unlike
		/// <see cref="LineageId"/>, this changes for every successor.</summary>
		public string LegacyId = "";

		/// <summary>The game this record was written from. The eligibility matrix is keyed on it.</summary>
		public string OriginGameId = "";

		/// <summary>How many lives deep this lineage is. Zero for a founder who founded from
		/// nothing.</summary>
		public int Generation;

		/// <summary>Monotonic within one origin. The journal keeps the highest complete one.</summary>
		public int Revision;

		/// <summary>The world tick the record was written at. Diagnostics and ordering only: no
		/// rule anywhere reads it as a clock (<c>DECISIONS.md:151-165</c>).</summary>
		public long WrittenTick;

		public string FounderName = "";

		/// <summary>How the founder died, in one bounded clause. Empty until a terminal attempt.</summary>
		public string CauseText = "";

		/// <summary>A short token for what kind of death it was, for the cairn's grammar.</summary>
		public string CauseKind = "";

		public long CauseTurn;

		public string RealmName = "";

		public string SettlementName = "";

		/// <summary>The settlement's stable semantic id, for telling two seals apart.</summary>
		public string SettlementId = "";

		public string RealmId = "";
		public List<string> RealmSettlementIds = new List<string>();
		public List<string> RealmSettlementProvenance = new List<string>();
		public int RealmIdentityVersion;
		public KingdomIdentityOrigin RealmIdentityOrigin;
		public string RealmIdentityTransactionId = "";
		public string RealmIdentityLegacyFaction = "";
		public long RealmIdentityFoundedTick;
		public ulong RealmIdentitySeedHigh;
		public ulong RealmIdentitySeedLow;
		public string RealmIdentityFirstClaimedZone = "";
		public int SettlementIdentityVersion;
		public KingdomIdentityOrigin SettlementIdentityOrigin;
		public string SettlementIdentityTransactionId = "";
		public long SettlementIdentityFoundedTick;
		public string SettlementIdentityFirstClaimedZone = "";
		public string SettlementIdentityLegacyId = "";

		public string Vocation = "";

		public string Style = "";

		public long FoundedTick;

		/// <summary>The seat's ground, as a zone id. Qud's overworld map is fixed
		/// (<c>SUCCESSION-RESEARCH.md</c> &sect;1.7), so this names real ground in the next world
		/// too &mdash; which is what lets a later life find the same place rather than a copy of
		/// it somewhere else.</summary>
		public string GroundZoneId = "";

		public string RegionName = "";

		public string TerrainBlueprint = "";

		public int Depth;

		/// <summary>The growth stage at sealing, as <c>GrowthStage</c>.</summary>
		public int Stage;

		public int Population;

		public int Defence;

		/// <summary>Drams in the dedicated stores at sealing. A <b>term of the seal's arithmetic
		/// only</b>; see this type's own remarks. No water crosses.</summary>
		public int StoredWater;

		public bool Withered;

		/// <summary>The one bounded number, from <c>KingdomRules.SealedVigour</c>. Written by the
		/// capture and re-derivable from the terms above, which is how a reader checks the two
		/// against each other.</summary>
		public int Vigour;

		/// <summary>The interregnum draw, 0&ndash;99, or -1 before promotion.</summary>
		public int InterregnumRoll = -1;

		/// <summary>The resolved state, or -1 before promotion.</summary>
		public int InheritedState = -1;

		public List<string> WorkKeys = new List<string>();

		public List<int> WorkX = new List<int>();

		public List<int> WorkY = new List<int>();

		public List<int> WorkConditions = new List<int>();

		/// <summary>Zero is a schema-4 compatible anchor proxy. One freezes exact authored
		/// architecture and a zone-relative street graph.</summary>
		public int SpatialVersion;

		public int SpatialWidth;

		public int SpatialHeight;

		public int SpatialEntrySide = KingdomInheritanceSpatialRules.NoEntry;

		public int SpatialEntryX;

		public int SpatialEntryY;

		public List<string> WorkSnapshots = new List<string>();

		public List<string> WorkSnapshotHashes = new List<string>();

		public List<int> StreetX = new List<int>();

		public List<int> StreetY = new List<int>();

		public List<string> RollNames = new List<string>();

		public List<string> RollOrigins = new List<string>();

		public List<string> RollArrived = new List<string>();

		public List<string> OriginKeys = new List<string>();

		public List<int> OriginCounts = new List<int>();

		public List<string> CreedKeys = new List<string>();

		public List<int> CreedCounts = new List<int>();

		public List<string> Chronicle = new List<string>();

		public List<string> Outsider = new List<string>();

		public List<string> DeadNames = new List<string>();

		public List<string> DeadCauses = new List<string>();

		/// <summary>Zero means a schema-4/5 or otherwise unresolved institutional legacy.
		/// Current records carry the exact bounded phenotype committed by the live polity profile.</summary>
		public int ProfileSchema;

		/// <summary>Exact craft-derived equipment band. Never inferred from growth stage.</summary>
		public int TechnologyBand;

		/// <summary>Canonical population body keys only; never actor or object identities.</summary>
		public List<string> CanonicalBodyKeys = new List<string>();

		/// <summary>Seal-safe commitment of projected technology and body phenotype only.</summary>
		public string SourceProfileDigest = "";

		/// <summary>Self-commitment over this bounded profile projection.</summary>
		public string ProfileProvenanceDigest = "";

		/// <summary>True once the interregnum has been drawn and the state fixed.</summary>
		public bool IsResolved => InterregnumRoll >= 0 && KingdomRules.IsKnownState((KingdomRules.InheritedState)InheritedState);

	}
}
