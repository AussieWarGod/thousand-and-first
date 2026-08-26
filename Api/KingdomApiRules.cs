using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// Why a marked extension was or was not admitted. LIVING-CITY-ARCHITECTURE &sect;6.6:
	/// <i>"refused by mod name, on screen and in the log &hellip; never silently skipped and never
	/// half-loaded."</i>
	/// <para>
	/// Values are appended and never reordered: a refusal is quoted in a log line a player pastes
	/// into a bug report, and the ordinal is what a test pins.
	/// </para>
	/// </summary>
	public enum KingdomExtensionVerdict : byte
	{
		/// <summary>Admitted. The extension runs under the invariants in MODDING.md.</summary>
		Accepted = 0,

		/// <summary>The type carries the marker but declares no API version at all.</summary>
		RefusedNoVersion = 1,

		/// <summary>Built against a later API than this copy of the mod publishes.</summary>
		RefusedAhead = 2,

		/// <summary>Built against an earlier API than this copy of the mod publishes.</summary>
		RefusedBehind = 3,

		/// <summary>Marked, but implements none of the published contracts.</summary>
		RefusedNoContract = 4,

		/// <summary>Nothing to name in the refusal, which is itself a refusal: a contract that
		/// cannot say whose fault a fault is has no owner.</summary>
		RefusedUnnamed = 5,

		/// <summary>The extension's own constructor or version property threw. Distinct from
		/// <see cref="RefusedNoVersion"/> on purpose: telling a modder their class "declares no API
		/// version" when what actually happened is that it threw sends them to the wrong line.</summary>
		RefusedThrew = 6,

		/// <summary>Another installed manifest ID maps to the same bounded durable namespace. Both
		/// owners are refused so load order cannot decide which mod receives shared state.</summary>
		RefusedNamespaceCollision = 7
	}

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
	public static class KingdomApiRules
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

		/// <summary>
		/// Whether a marked type may register, and if not, why.
		/// <para>
		/// The order is frozen so that combined-invalid input cannot vary by implementation:
		/// no name, then no contract, then the version. A nameless extension is judged first
		/// because every other refusal is reported <i>by mod name</i>, and a refusal that cannot
		/// name its owner is the one failure this contract exists to prevent.
		/// </para>
		/// </summary>
		/// <param name="ModName">The owning mod's immutable manifest ID.</param>
		/// <param name="DeclaredVersion">What the extension says it was built against.</param>
		/// <param name="ImplementsContract">Whether it implements at least one published
		/// contract interface.</param>
		/// <returns>The verdict. Only <see cref="KingdomExtensionVerdict.Accepted"/> admits.</returns>
		public static KingdomExtensionVerdict Judge(string ModName, int DeclaredVersion, bool ImplementsContract)
		{
			return Judge(ModName, DeclaredVersion, ImplementsContract, MinSupportedVersion);
		}

		/// <summary>Version judgment for a specific contract family. Additive v3 behaviour cannot be
		/// claimed by a source declaring v1, while genuine v1 ask/happening sources remain admitted.</summary>
		/// <param name="ModName">Owning mod immutable manifest ID.</param>
		/// <param name="DeclaredVersion">Declared API version.</param>
		/// <param name="ImplementsContract">Whether any published contract is implemented.</param>
		/// <param name="MinimumContractVersion">First API version containing every implemented
		/// contract on this type.</param>
		/// <returns>Registration verdict.</returns>
		public static KingdomExtensionVerdict Judge(string ModName, int DeclaredVersion,
			bool ImplementsContract, int MinimumContractVersion)
		{
			if (string.IsNullOrEmpty(Slug(ModName)))
			{
				return KingdomExtensionVerdict.RefusedUnnamed;
			}
			if (!ImplementsContract)
			{
				return KingdomExtensionVerdict.RefusedNoContract;
			}
			if (DeclaredVersion <= 0)
			{
				return KingdomExtensionVerdict.RefusedNoVersion;
			}
			if (DeclaredVersion > Version)
			{
				return KingdomExtensionVerdict.RefusedAhead;
			}
			int required = MinimumContractVersion < MinSupportedVersion
				? MinSupportedVersion : MinimumContractVersion;
			if (DeclaredVersion < required)
			{
				return KingdomExtensionVerdict.RefusedBehind;
			}
			return KingdomExtensionVerdict.Accepted;
		}

		/// <summary>
		/// What the log and the message line say about a refusal. Names the mod, the version it
		/// wanted, and the version we are &mdash; the three facts a player pasting a line into a
		/// bug report needs, and the three &sect;6.6 requires.
		/// </summary>
		/// <returns>The line, or empty for <see cref="KingdomExtensionVerdict.Accepted"/>.</returns>
		public static string RefusalLine(KingdomExtensionVerdict Verdict, string ModName, int DeclaredVersion)
		{
			return RefusalLine(Verdict, ModName, DeclaredVersion, MinSupportedVersion);
		}

		/// <summary>Refusal prose using a specific contract family's minimum version.</summary>
		/// <param name="Verdict">Registration verdict.</param>
		/// <param name="ModName">Owning mod immutable manifest ID.</param>
		/// <param name="DeclaredVersion">Declared API version.</param>
		/// <param name="MinimumContractVersion">First version containing every implemented contract.</param>
		/// <returns>Founder-facing refusal line, or empty for acceptance.</returns>
		public static string RefusalLine(KingdomExtensionVerdict Verdict, string ModName,
			int DeclaredVersion, int MinimumContractVersion)
		{
			string who = string.IsNullOrEmpty(ModName) ? "an unnamed mod" : ModName;
			switch (Verdict)
			{
			case KingdomExtensionVerdict.Accepted:
				return "";
			case KingdomExtensionVerdict.RefusedUnnamed:
				return "A kingdom extension was refused: it belongs to no mod this game can name, so a fault in it could never be attributed. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedNoContract:
				return who + " marks a type as a kingdom extension that implements none of the published contracts. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedNoVersion:
				return who + " marks a kingdom extension that declares no API version. The kingdom API is version " + Version + ". Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedAhead:
				return who + " was built against kingdom API version " + DeclaredVersion + "; this copy of The Thousand and First publishes version " + Version + ". Update the mod, or update this one. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedBehind:
				int required = MinimumContractVersion < MinSupportedVersion
					? MinSupportedVersion : MinimumContractVersion;
				return who + " was built against kingdom API version " + DeclaredVersion
					+ "; this copy of The Thousand and First publishes version " + Version
					+ " and its chosen contracts require version " + required
					+ ". The mod needs an update. Nothing of it is loaded.";
			case KingdomExtensionVerdict.RefusedThrew:
				return who + " threw while the kingdom API was building its extension. The fault is in that mod and is in the log; nothing of it is loaded, and every other extension still runs.";
			case KingdomExtensionVerdict.RefusedNamespaceCollision:
				return who + " has the same bounded kingdom namespace as another installed manifest ID. Both owners are refused so load order cannot transfer durable state; change one manifest ID before publishing it.";
			default:
				return who + " was refused by the kingdom API. Nothing of it is loaded.";
			}
		}

		/// <summary>
		/// The draw stream one extension lane runs on, in the kernel's own grammar.
		/// <para>
		/// Owning a stream per (mod, lane) is what makes an extension's draws unable to shift
		/// ours or another extension's: ordinal ownership is one <c>(EventStreamId,
		/// EventKindCode)</c> lane, so two mods at ordinal zero cannot collide (&sect;2.4).
		/// </para>
		/// </summary>
		/// <param name="ModName">The owning mod's immutable manifest ID. Slugged.</param>
		/// <param name="Lane">The extension's own name for this stream. Slugged.</param>
		/// <param name="Stream">The stream id, or empty on refusal.</param>
		/// <returns>False when either part slugs away to nothing or the pair will not fit the
		/// kernel's 128-byte identifier. The caller refuses the draw; it never invents a
		/// substitute stream, because a substituted stream is a silently different random
		/// sequence.</returns>
		public static bool TryStream(string ModName, string Lane, out string Stream)
		{
			Stream = "";
			string mod = Slug(ModName);
			string lane = Slug(Lane);
			if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(lane))
			{
				return false;
			}
			string candidate = StreamPrefix + mod + ":" + lane;
			if (candidate.Length > MaxStreamLength)
			{
				return false;
			}
			Stream = candidate;
			return true;
		}

		/// <summary>
		/// A name reduced to the kernel identifier alphabet: lowercase, digits, and the three
		/// separators <c>. _ -</c>. Everything else becomes a hyphen, runs of hyphens collapse,
		/// and leading and trailing hyphens are dropped.
		/// </summary>
		/// <returns>The slug, or empty when nothing survives.</returns>
		public static string Slug(string Source)
		{
			if (string.IsNullOrEmpty(Source))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder(Source.Length);
			bool pendingSeparator = false;
			for (int i = 0; i < Source.Length; i++)
			{
				char c = Source[i];
				if (c >= 'A' && c <= 'Z')
				{
					c = (char)(c + 32);
				}
				bool plain = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_';
				if (plain)
				{
					if (pendingSeparator && builder.Length > 0)
					{
						builder.Append('-');
					}
					pendingSeparator = false;
					builder.Append(c);
					continue;
				}
				if (builder.Length > 0)
				{
					pendingSeparator = true;
				}
			}
			return builder.ToString();
		}

		/// <summary>Characters a colour code may run to before the opener is treated as ordinary
		/// text. The engine's own codes are one or two (<c>{{K|</c>, <c>{{rr|</c>).</summary>
		private const int MaxColourCode = 3;

		/// <summary>
		/// An extension-supplied line as the surfaces will carry it: no control characters, no runs
		/// of whitespace, no markup that could open a colour span the rest of the report never
		/// closes, and never longer than <see cref="MaxTextLength"/>.
		/// <para>
		/// <b>Colour is taken away entirely, opener and all.</b> Stripping the braces and leaving
		/// the code behind would put a literal <c>R|</c> in the founder's report, which is worse
		/// than either extreme &mdash; and letting the span through would recolour every line after
		/// it, which is how one mod's ask makes the whole board look like ours is broken.
		/// </para>
		/// <para>
		/// Cut at a word boundary with an ellipsis when it is too long, because a line cut mid-word
		/// reads as corruption and a line refused outright reads as a silent stall.
		/// </para>
		/// </summary>
		public static string Trim(string Text, int Limit)
		{
			if (string.IsNullOrEmpty(Text) || Limit <= 0)
			{
				return "";
			}
			StringBuilder builder = new StringBuilder(Text.Length);
			bool pendingSpace = false;
			for (int i = 0; i < Text.Length; i++)
			{
				char c = Text[i];
				if (c == '}')
				{
					continue;
				}
				if (c == '{')
				{
					i = AfterOpener(Text, i);
					continue;
				}
				if (c <= ' ')
				{
					if (builder.Length > 0)
					{
						pendingSpace = true;
					}
					continue;
				}
				if (pendingSpace)
				{
					builder.Append(' ');
					pendingSpace = false;
				}
				builder.Append(c);
			}
			string clean = builder.ToString();
			if (clean.Length <= Limit)
			{
				return clean;
			}
			// The ellipsis counts. Cutting to Limit and then appending one would return Limit+1
			// characters from a method whose whole contract is a ceiling.
			int room = Limit - 1;
			int cut = clean.LastIndexOf(' ', Math.Min(room, clean.Length - 1));
			if (cut < room / 2)
			{
				cut = room;
			}
			return clean.Substring(0, cut).TrimEnd() + "…";
		}

		/// <summary>
		/// The index of the last character of a colour opener beginning at <paramref name="at"/>,
		/// so the caller's loop skips the whole of it. A run of braces with no pipe close behind it
		/// is just braces, and only the braces are dropped.
		/// </summary>
		private static int AfterOpener(string text, int at)
		{
			int i = at;
			while (i < text.Length && text[i] == '{')
			{
				i++;
			}
			for (int j = i; j < text.Length && j - i <= MaxColourCode; j++)
			{
				if (text[j] == '|')
				{
					return j;
				}
			}
			return i - 1;
		}

		/// <summary>The ordinary clamp: a sentence for a founder to read.</summary>
		public static string Trim(string Text)
		{
			return Trim(Text, MaxTextLength);
		}

		/// <summary>A filing key, slugged and clamped. Empty is a refused ask or notice, never a
		/// blank one.</summary>
		public static string Kind(string Source)
		{
			string slug = Slug(Source);
			return (slug.Length <= MaxKindLength) ? slug : slug.Substring(0, MaxKindLength);
		}

		/// <summary>Canonicalizes one behaviour key and proves the registered owner owns its
		/// namespace. Unqualified input is filed under the owner; same-owner qualified input is
		/// accepted; foreign, blank, malformed, or overlong input is refused rather than truncated.</summary>
		/// <param name="ModName">Registered owning mod manifest ID.</param>
		/// <param name="Source">Owner-local or same-owner qualified key.</param>
		/// <returns>Owner-qualified key, or null.</returns>
		public static string ExtensionKey(string ModName, string Source)
		{
			string owner = Slug(ModName);
			if (string.IsNullOrEmpty(owner) || string.IsNullOrWhiteSpace(Source)) return null;
			string raw = Source.Trim().ToLowerInvariant();
			int colon = raw.IndexOf(':');
			string local;
			if (colon >= 0)
			{
				if (colon == 0 || colon != raw.LastIndexOf(':') || colon == raw.Length - 1
					|| !string.Equals(raw.Substring(0, colon), owner, StringComparison.Ordinal))
					return null;
				local = Slug(raw.Substring(colon + 1));
				if (!string.Equals(local, raw.Substring(colon + 1), StringComparison.Ordinal)) return null;
			}
			else
			{
				local = Slug(raw);
				if (!string.Equals(local, raw, StringComparison.Ordinal)) return null;
			}
			if (string.IsNullOrEmpty(local)) return null;
			string key = owner + ":" + local;
			return key.Length <= MaxBehaviourIdentifierLength ? key : null;
		}

		/// <summary>Bounds an engine-facing property, liquid, blueprint, unit, or node identifier
		/// without changing its case. Control characters, markup braces, and overlong values are
		/// refused; surrounding whitespace is removed.</summary>
		/// <param name="Source">Proposed identifier.</param>
		/// <param name="Required">Whether empty input is a refusal.</param>
		/// <returns>Bounded identifier, empty for an allowed absence, or null on refusal.</returns>
		public static string BehaviourIdentifier(string Source, bool Required)
		{
			if (string.IsNullOrWhiteSpace(Source)) return Required ? null : "";
			string value = Source.Trim();
			if (value.Length > MaxBehaviourIdentifierLength || value.IndexOf('{') >= 0
				|| value.IndexOf('}') >= 0 || value.IndexOf('|') >= 0) return null;
			for (int i = 0; i < value.Length; i++)
				if (char.IsControl(value[i])) return null;
			return value;
		}

		/// <summary>A frozen identity name: trimmed, control-free, and bounded. Case is preserved
		/// because culture and species are vanilla display vocabularies rather than filing keys.</summary>
		/// <param name="Source">The engine or extension supplied identity name. Null is empty.</param>
		/// <returns>A bounded display value. Never null.</returns>
		public static string IdentityName(string Source)
		{
			if (string.IsNullOrWhiteSpace(Source))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			bool spacing = false;
			string source = Source.Trim();
			for (int i = 0; i < source.Length && builder.Length < MaxIdentityNameLength; i++)
			{
				char c = source[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c))
				{
					spacing = builder.Length > 0;
					continue;
				}
				if (spacing && builder.Length < MaxIdentityNameLength)
				{
					builder.Append(' ');
				}
				spacing = false;
				if (builder.Length < MaxIdentityNameLength)
				{
					builder.Append(c);
				}
			}
			return builder.ToString();
		}

		/// <summary>One work-kind label handed to an identity source. Empty stays empty; every other
		/// value uses the same bounded filing grammar as asks and notices.</summary>
		/// <param name="Source">The existing work lane's label.</param>
		/// <returns>The bounded canonical label, or empty.</returns>
		public static string IdentityWorkKind(string Source)
		{
			return Kind(Source);
		}

		/// <summary>
		/// Canonicalizes one extension-supplied roster key and proves the source owns its namespace.
		/// Unqualified input is filed under the owning mod. Qualified input is accepted only when its
		/// kind is that same mod slug. Blank, malformed, foreign, or over-long input is dropped.
		/// </summary>
		/// <param name="ModName">The registered owning mod's immutable manifest ID.</param>
		/// <param name="Source">The source's proposed key.</param>
		/// <returns>The owned canonical key, or null when it is unsafe or foreign.</returns>
		public static string IdentityKey(string ModName, string Source)
		{
			string owner = Kind(ModName);
			if (owner.Length == 0 || string.IsNullOrWhiteSpace(Source))
			{
				return null;
			}
			string raw = Source.Trim();
			if (raw.Length > MaxIdentityKeyLength)
			{
				return null;
			}
			string source = IdentityName(raw).ToLowerInvariant();
			if (source.IndexOf('|') >= 0 || source.Length == 0)
			{
				return null;
			}
			int colon = source.IndexOf(':');
			string name;
			if (colon < 0)
			{
				name = source;
			}
			else
			{
				if (colon == 0 || colon == source.Length - 1
					|| !string.Equals(source.Substring(0, colon), owner, StringComparison.Ordinal))
				{
					return null;
				}
				name = source.Substring(colon + 1);
			}
			if (name.Length == 0 || name.IndexOf('|') >= 0)
			{
				return null;
			}
			string key = owner + ":" + name;
			return (key.Length <= MaxIdentityKeyLength) ? key : null;
		}

		/// <summary>Clamps one affinity answer into the Addendum 17 band.</summary>
		/// <param name="Percent">The source's answer, where 100 is neutral.</param>
		/// <returns>The answer clamped to 70&ndash;130.</returns>
		public static int IdentityAffinity(int Percent)
		{
			if (Percent < MinIdentityAffinity) return MinIdentityAffinity;
			if (Percent > MaxIdentityAffinity) return MaxIdentityAffinity;
			return Percent;
		}

		/// <summary>Finalizes an order-independent sum of source deltas around neutral 100. The sum
		/// stays unclamped until every source has spoken; otherwise early saturation would let source
		/// order change a mixed positive/negative answer.</summary>
		internal static int IdentityAffinityFromDelta(long Delta)
		{
			if (Delta <= MinIdentityAffinity - 100L) return MinIdentityAffinity;
			if (Delta >= MaxIdentityAffinity - 100L) return MaxIdentityAffinity;
			return 100 + (int)Delta;
		}
	}
}
