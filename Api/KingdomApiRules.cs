using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// The published extension contract's pure half: the version judgment, the refusal prose, the
	/// stream-name grammar an extension's draws must fit, and the clamps every extension-supplied
	/// string and collection passes through.
	/// <para>
	/// Engine-free and total, like every <c>*Rules</c> class in this mod, so the judgment a modder
	/// gets is the judgment the test table asserts. Nothing here reads a clock, a game, or an
	/// option.
	/// </para>
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;6.6 and BUILDING-CATALOGUE-BRIEF Addendum 12(i).
	/// </para>
	/// </summary>
	public static partial class KingdomApiRules
	{
		/// <summary>
		/// The published API version. Checked at registration against
		/// <c>IKingdomExtension.ApiVersion</c>; any drift is a refusal by mod name.
		/// <para>
		/// It moves when a published contract's shape changes, and never for an additive change
		/// that older extensions still satisfy &mdash; a new reading field, a new verdict ordinal
		/// at the end of an enum. STANDARDS &sect;9's versioning rule governs: supported API is
		/// never removed in a minor release.
		/// </para>
		/// </summary>
		public const int Version = 3;

		/// <summary>First version which publishes durable resource, job/carrier, network and work
		/// behaviour contracts. Version-one ask/happening and version-two identity sources remain in
		/// the compatibility window, but cannot claim contracts their binaries predate.</summary>
		public const int BehaviourVersion = 3;

		/// <summary>
		/// The oldest version still admitted. STANDARDS &sect;9 promises supported API is kept
		/// working for at least one minor cycle after a change, and a check that admitted only the
		/// current version would make that promise unkeepable: bumping to 2 would refuse every
		/// extension in the world on the same day.
		/// <para>
		/// It moves only when a contract changes shape in a way an older extension cannot satisfy,
		/// and moving it is a breaking change with a <c>CHANGELOG.md</c> line.
		/// </para>
		/// </summary>
		public const int MinSupportedVersion = 1;

		/// <summary>Asks one source may contribute to one reading of the board. A source that
		/// returns more is clamped, not refused: an over-eager extension is a nuisance, and a
		/// nuisance that disables the whole extension would be worse than the nuisance.</summary>
		public const int MaxAsksPerSource = 4;

		/// <summary>Happening notices one source may contribute to one settlement pass. Smaller
		/// than the ask cap because a notice can PUSH a line at the founder and an ask cannot:
		/// &sect;4.2's budget is shared, and an extension may not out-shout the city.</summary>
		public const int MaxNoticesPerSource = 2;

		/// <summary>Extra live roster keys one identity source may mint for one person. These keys
		/// are projections, not durable rows, but the cap still bounds reconciliation work.</summary>
		public const int MaxIdentityKeysPerSource = 8;

		/// <summary>Proposed key slots inspected from one source result. Invalid entries consume this
		/// budget, keeping a hostile all-invalid array from making reconciliation unbounded.</summary>
		public const int MaxIdentityKeyCandidatesPerSource = 32;

		/// <summary>Kernel draws one extension callback may spend. The thirty-third attempt refuses
		/// the whole callback as over-budget; it never reaches <c>CounterRandom</c>.</summary>
		public const int MaxDrawsPerSourceCall = 32;

		/// <summary>Longest culture, species, creed, or genotype carried over the extension seam.</summary>
		public const int MaxIdentityNameLength = 128;

		/// <summary>Longest complete extra roster key. An over-long key is dropped rather than
		/// truncated, because truncation could make two namespaces collide.</summary>
		public const int MaxIdentityKeyLength = 128;

		/// <summary>Resource kinds one owner may retain in one settlement.</summary>
		public const int MaxResourceKindsPerOwner = 4;

		/// <summary>Total extension resource rows one settlement may retain.</summary>
		public const int MaxResourceKindsPerCity = 16;

		/// <summary>Candidate slots inspected for each returned behaviour array. Invalid entries
		/// spend the inspection budget, so hostile all-invalid arrays remain bounded.</summary>
		public const int MaxBehaviourCandidatesPerCall = 32;

		/// <summary>Carrier kinds one owner may offer during one pass.</summary>
		public const int MaxCarrierKindsPerOwner = 4;

		/// <summary>Open extension jobs one owner may retain.</summary>
		public const int MaxJobsPerOwner = 4;

		/// <summary>Total open extension jobs per settlement.</summary>
		public const int MaxJobsPerCity = 16;

		/// <summary>Recent terminal idempotence receipts retained for one owner. Older terminal rows
		/// retire in insertion order. Reusing a retired key at a later tick is a new proposal, so
		/// extension job keys must identify one logical job and never be recycled.</summary>
		public const int MaxTerminalJobReceiptsPerOwner = 4;

		/// <summary>Recent terminal job receipts retained across all owners in one settlement.</summary>
		public const int MaxTerminalJobReceiptsPerCity = 16;

		/// <summary>Largest encoded job-row array: every open row plus the terminal receipt ring.</summary>
		public const int MaxStoredJobsPerCity = MaxJobsPerCity + MaxTerminalJobReceiptsPerCity;

		/// <summary>Legs one extension job may carry.</summary>
		public const int MaxLegsPerJob = 6;

		/// <summary>Atomic resource changes one completion or work advance may propose.</summary>
		public const int MaxChangesPerResult = 4;

		/// <summary>Networks one owner may retain.</summary>
		public const int MaxNetworksPerOwner = 4;

		/// <summary>Total extension network state rows per settlement.</summary>
		public const int MaxNetworksPerCity = 16;

		/// <summary>Nodes in one extension network.</summary>
		public const int MaxNodesPerNetwork = 8;

		/// <summary>Edges in one extension network.</summary>
		public const int MaxEdgesPerNetwork = 12;

		/// <summary>Work-behaviour rows one owner may retain.</summary>
		public const int MaxWorkBehavioursPerOwner = 16;

		/// <summary>Total work-behaviour rows per settlement.</summary>
		public const int MaxWorkBehavioursPerCity = 64;

		/// <summary>Physical-debt entries accepted from one work result. One keeps the durable row
		/// fixed-width and makes partial materialisation impossible.</summary>
		public const int MaxMaterialisationsPerAdvance = 1;

		/// <summary>Legacy v1 sidecars were admitted up to this exact decoded byte count.</summary>
		internal const int LegacyBehaviourModelBytes = 16384;

		/// <summary>Maximum decoded byte count of one current durable behaviour sidecar. Wire v2
		/// appends one exact generation receipt to every work row, so its bound includes the full
		/// worst-case expansion of any valid v1 carrier.</summary>
		public const int MaxBehaviourModelBytes = LegacyBehaviourModelBytes
			+ MaxWorkBehavioursPerCity * sizeof(long);

		/// <summary>Maximum identifier length for owner-qualified keys, Qud blueprint names, property
		/// names, liquid ids and node names.</summary>
		public const int MaxBehaviourIdentifierLength = 128;

		/// <summary>Lowest identity affinity an extension result may compose to.</summary>
		public const int MinIdentityAffinity = 70;

		/// <summary>Highest identity affinity an extension result may compose to.</summary>
		public const int MaxIdentityAffinity = 130;

		/// <summary>Longest extension-supplied line the surfaces will carry. Longer is cut at a
		/// word boundary rather than refused.</summary>
		public const int MaxTextLength = 200;

		/// <summary>Longest kind label. A label is a filing key, not a sentence.</summary>
		public const int MaxKindLength = 32;

		/// <summary>Every extension draw stream begins here, so an extension's ordinal lane can
		/// never collide with one of ours no matter what it calls itself
		/// (<c>SemanticEventKey.EventStreamId</c>, LIVING-CITY-ARCHITECTURE &sect;2.4).</summary>
		public const string StreamPrefix = "taf:ext:";

		/// <summary>The kernel's own ceiling on a semantic id, restated here because this is the
		/// class that has to fit inside it. <c>KernelSemanticId.MaxUtf8Bytes</c>.</summary>
		private const int MaxStreamLength = 128;

	}
}
