using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far a settlement's own craft has come. Derived, never authored and never set: it is a
	/// readout of what the keepers have been taught and what they have certified fit for the grid
	/// (<see cref="KingdomZoningRules.TechPoints"/>), so it rises by playing rather than by
	/// spending anything on a research screen. This mod has no research tree and does not want
	/// one &mdash; a tree is a second job, and the founder already has one.
	/// </summary>
	public enum TechLevel
	{
		/// <summary>What hands can shape without help. Where every settlement starts.</summary>
		Hands = 0,
		/// <summary>The settlement can make use of what is dragged home from a ruin.</summary>
		Salvage = 1,
		/// <summary>There is a bench, and people who know what to do at it.</summary>
		Workshop = 2,
		/// <summary>Heat, pressure, and the confidence to run both unattended.</summary>
		Foundry = 3,
		/// <summary>The ancients' own work, understood well enough to raise more of it.</summary>
		Arclight = 4
	}

	/// <summary>
	/// Why a design may or may not be raised, beyond the city style and growth stage
	/// <c>KingdomRules.BuildEntry</c> already carries. Ordered the way
	/// <see cref="KingdomZoningRules.Judge"/> checks them, which is from the most fundamental
	/// lack to the most local one: nobody here knows how, then the settlement is not that
	/// advanced, then the realm is too small, then finally &mdash; and only then &mdash; this is
	/// the wrong ground. District comes last deliberately, so the founder hears "the forgeworks
	/// would take it" at the moment that sentence is the only thing standing between them and
	/// the building.
	/// </summary>
	public enum ZoningVerdict
	{
		Permitted = 0,
		RefusedUnlearned = 1,
		RefusedTechLevel = 2,
		RefusedTerritory = 3,
		RefusedDistrict = 4
	}

	/// <summary>
	/// The four optional gates a <c>&lt;building&gt;</c> entry may declare, parsed. Every field
	/// has an "ungated" value and that value is what an absent attribute produces, so an entry
	/// written before these gates existed &mdash; ours or a third party's &mdash; is
	/// <see cref="IsOpen"/> and behaves exactly as it always did.
	/// </summary>
	public readonly struct ZoneGate
	{
		/// <summary>
		/// Comma list of district keys whose ground will accept this design, plus the token
		/// <see cref="KingdomZoningRules.UndistrictedToken"/> for ground that has been given no
		/// district at all. Null when the design demands no particular ground.
		/// </summary>
		public readonly string Districts;

		/// <summary>Claimed zones the realm must hold. Zero when the design demands none.</summary>
		public readonly int MinZones;

		/// <summary>
		/// Comma list of knowledge keys the settlement must hold, ALL of them. Null when the
		/// design demands none. See <see cref="KingdomZoningRules.Knows"/> for the match rule.
		/// </summary>
		public readonly string Knowledge;

		/// <summary>Craft the settlement must have reached. <see cref="TechLevel.Hands"/> is
		/// every settlement's starting level and therefore gates nothing.</summary>
		public readonly TechLevel MinTech;

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech)
		{
			this.Districts = Districts;
			this.MinZones = MinZones;
			this.Knowledge = Knowledge;
			this.MinTech = MinTech;
		}

		/// <summary>A design that declares none of the four gates. What an entry with no new
		/// attributes parses to, and the value used for any key the registry never registered.</summary>
		public static ZoneGate Open => new ZoneGate(null, 0, null, TechLevel.Hands);

		/// <summary>True when nothing here can refuse anything.</summary>
		public bool IsOpen => string.IsNullOrEmpty(Districts) && MinZones <= 0 && string.IsNullOrEmpty(Knowledge) && MinTech <= TechLevel.Hands;
	}

	/// <summary>
	/// One gate's answer: whether the design may be raised, and &mdash; when it may not &mdash;
	/// the two pieces of prose a refusal owes the founder. STANDARDS 7b is the reason both
	/// strings exist: a refusal that does not name what would fix it is a locked door.
	/// </summary>
	public readonly struct ZoningJudgement
	{
		public readonly ZoningVerdict Verdict;

		/// <summary>What is missing, in the settlement's own words: "the forgeworks", "3 claimed
		/// zones", "solar condenser", "foundry". Null when nothing is missing.</summary>
		public readonly string Detail;

		/// <summary>The short tag a menu line carries so a founder can see which designs are
		/// blocked before choosing one. Null when nothing is missing.</summary>
		public readonly string Note;

		public ZoningJudgement(ZoningVerdict Verdict, string Detail, string Note)
		{
			this.Verdict = Verdict;
			this.Detail = Detail;
			this.Note = Note;
		}

		public bool Permitted => Verdict == ZoningVerdict.Permitted;

		/// <summary>The judgement a design with nothing to prove receives.</summary>
		public static ZoningJudgement Allowed => new ZoningJudgement(ZoningVerdict.Permitted, null, null);
	}

	/// <summary>
	/// Engine-free rules for what a founder may commission and where. Four gates sit on top of
	/// the city style and growth stage <c>KingdomRules</c> already applies: the district the
	/// ground carries, how much ground the realm holds, which designs the keepers have learned,
	/// and how far the settlement's own craft has come.
	/// <para>
	/// All four are OPTIONAL attributes on a <c>&lt;building&gt;</c> entry and an absent
	/// attribute gates nothing, which is what keeps every entry written before this existed
	/// &mdash; ours and every third party's &mdash; working untouched (STANDARDS 6).
	/// </para>
	/// <para>
	/// The governing ruling, which the shapes below implement: gating is <b>hard for where a
	/// structure may stand and soft for how well it works</b>. A design that names districts may
	/// only be raised on ground that carries one of them, so zoning is a real decision; nothing
	/// here ever reaches into the district BONUSES, which stay realm-wide and unconditional in
	/// <c>KingdomRules.Districts*</c>, so a design raised off its natural ground simply misses a
	/// bonus rather than being refused.
	/// </para>
	/// <para>
	/// The engine-coupled half &mdash; reading a real zone's district, the founder's data disks,
	/// the certified machines, and the settlement's own roster of peoples &mdash; is
	/// <c>ThousandAndFirst.KingdomZoning</c>, in the same folder.
	/// </para>
	/// </summary>
	public static class KingdomZoningRules
	{
		/// <summary>Token an author writes in a <c>Districts</c> list to mean "ground that has
		/// been given no district", so a design can name both a district and open ground.</summary>
		public const string UndistrictedToken = "none";

		/// <summary>Token meaning "any ground at all". Equivalent to omitting the attribute; it
		/// exists because <c>Styles="all"</c> already taught authors this spelling.</summary>
		public const string AnyToken = "all";

		/// <summary>Separator inside every comma list this file parses.</summary>
		public const char ListSeparator = ',';

		/// <summary>
		/// Separates the kind of a knowledge key from its name: <c>machine:solar condenser</c>.
		/// A requirement written without one matches any kind (see <see cref="Knows"/>).
		/// </summary>
		public const char KindSeparator = ':';

		/// <summary>
		/// Separates knowledge keys in the settlement's stored roster. Deliberately NOT the comma
		/// an author writes, because a roster key is a blueprint name the game chose and a comma
		/// is likelier to appear in one than a pipe. A key containing this character is refused
		/// at <see cref="ComposeKey"/> rather than corrupting the store.
		/// </summary>
		public const char RosterSeparator = '|';

		/// <summary>A recipe taught to the keepers from a data disk the founder carried home.</summary>
		public const string KindDisk = "disk";

		/// <summary>A machine hauled home and certified fit for the grid. Certifying teaches:
		/// taking the machine back off the grid never unlearns it.</summary>
		public const string KindMachine = "machine";

		/// <summary>A trade the settlement holds because somebody from that country lives here.
		/// Read live off the settlement's own peoples, so it comes and goes with them.</summary>
		public const string KindOrigin = "origin";

		/// <summary>
		/// The categories undistricted ground always accepts, whatever a design demands: a roof
		/// over people, a vessel for the water, and the fire they sit around. The early game must
		/// never hit a wall before the founder has learned what a district is, and these three
		/// are the whole of a camp.
		/// </summary>
		public static readonly string[] OpenCategories = new string[3] { "housing", "storage", "civic" };

		// The natural map, read straight off the names the districts already carry. This is a
		// DEFAULT for authoring and for advice - it never refuses anything by itself, because the
		// authored Districts attribute is the only thing Judge reads. Keeping it that way is what
		// lets a third party file gate their own designs however they like without arguing with a
		// table they cannot edit.
		private static readonly string[] MappedCategories = new string[10]
		{
			"food", "storage", "civic", "craft", "power", "faith", "memorial", "housing", "defense", "knowledge"
		};

		private static readonly string[] MappedDistricts = new string[10]
		{
			"agrarian", "agrarian,market", "market", "craft", "craft", "shrine", "shrine", "garrison", "garrison", "academy"
		};

		/// <summary>Points a taught recipe is worth toward the settlement's craft.</summary>
		public const int TechPointsPerDisk = 1;

		/// <summary>
		/// Points a certified machine is worth. Twice a recipe, because certification costs water
		/// and hands and happens to a real object standing on the ground, where a disk costs a
		/// walk to a merchant.
		/// </summary>
		public const int TechPointsPerCertification = 2;

		/// <summary>
		/// Points a settler's country of origin is worth: none. Origins gate particular designs
		/// (somebody from the rust wells knows rust-well work) but they arrive on their own with
		/// growth, and letting them raise the craft level would turn that readout into a
		/// population count. The level is what the settlement LEARNED and CERTIFIED, exactly.
		/// </summary>
		public const int TechPointsPerOrigin = 0;

		/// <summary>Points needed for each <see cref="TechLevel"/>, by its numeric value.</summary>
		public static readonly int[] TechThresholds = new int[5] { 0, 2, 5, 9, 14 };

		/// <summary>What the settlement calls each level of its own craft.</summary>
		public static readonly string[] TechLevelNames = new string[5] { "hands", "salvage", "workshop", "foundry", "arclight" };

		/// <summary>Whether a category is one undistricted ground always accepts.</summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>. Null, empty, and any category a
		/// third party invents are not open &mdash; only the three named ones are.</param>
		public static bool IsOpenCategory(string Category)
		{
			string category = Fold(Category);
			if (category == null)
			{
				return false;
			}
			for (int i = 0; i < OpenCategories.Length; i++)
			{
				if (OpenCategories[i] == category)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The districts whose names already imply they would take this category &mdash; the
		/// vinelands take food and storage, the forgeworks craft and power, and so on. Advice
		/// only: nothing in <see cref="Judge"/> consults this, so a design is refused ground only
		/// by what its own entry declares.
		/// </summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>.</param>
		/// <returns>A comma list of district keys, or null for a category no district claims.</returns>
		public static string NaturalDistricts(string Category)
		{
			string category = Fold(Category);
			if (category == null)
			{
				return null;
			}
			// "defence" and "defense" both ship in the wild; KingdomLayoutRules.PurposeOf already
			// accepts either and a gate that disagreed with the plan would be a trap.
			if (category == "defence")
			{
				category = "defense";
			}
			for (int i = 0; i < MappedCategories.Length; i++)
			{
				if (MappedCategories[i] == category)
				{
					return MappedDistricts[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Whether ground carrying <paramref name="TileDistrict"/> will accept a design.
		/// <para>
		/// A design that names no districts stands anywhere. A design that names districts stands
		/// on ground carrying one of them, on ground with no district at all if it named
		/// <see cref="UndistrictedToken"/>, and on ground with no district at all if its category
		/// is one of the <see cref="OpenCategories"/>. Everything else is refused.
		/// </para>
		/// <para>
		/// A district key this mod does not recognise is treated as a district all the same,
		/// never as open ground: a third party's quarter must be able to refuse a forge exactly
		/// the way ours does, and a design that wants to stand there names it and is let in.
		/// </para>
		/// </summary>
		/// <param name="TileDistrict">The district key on the zone, or null/empty for ground the
		/// founder has never designated.</param>
		/// <param name="RequiredDistricts">The entry's <c>Districts</c> attribute; null, empty,
		/// or <see cref="AnyToken"/> accepts everything.</param>
		/// <param name="Category">The entry's <c>Category</c>, for the open-ground clause.</param>
		public static bool DistrictAccepts(string TileDistrict, string RequiredDistricts, string Category)
		{
			if (!Gated(RequiredDistricts))
			{
				return true;
			}
			string tile = Fold(TileDistrict);
			if (tile != null)
			{
				return ListContains(RequiredDistricts, tile);
			}
			return ListContains(RequiredDistricts, UndistrictedToken) || IsOpenCategory(Category);
		}

		/// <summary>
		/// A <c>Districts</c> list read back as founder-facing prose: "the vinelands or the
		/// bazaar", "the forgeworks or ground with no district yet". Names come from
		/// <c>KingdomRules.DistrictName</c>, so a third party's district reads as whatever key
		/// they chose rather than as a blank.
		/// </summary>
		/// <returns>Null when the list gates nothing, so a caller can skip the whole clause.</returns>
		public static string DescribeDistricts(string RequiredDistricts)
		{
			if (!Gated(RequiredDistricts))
			{
				return null;
			}
			List<string> names = new List<string>();
			foreach (string token in Tokens(RequiredDistricts))
			{
				string name = (token == UndistrictedToken) ? "ground with no district yet" : ("the " + KingdomRules.DistrictName(token));
				if (!names.Contains(name))
				{
					names.Add(name);
				}
			}
			return JoinOr(names);
		}

		/// <summary>
		/// Builds a roster key. Returns null for anything that could not survive a round trip
		/// through the store &mdash; a blank name, or one carrying the
		/// <see cref="RosterSeparator"/> &mdash; so a hostile blueprint name disables one key
		/// rather than corrupting the whole roster (STANDARDS 9).
		/// </summary>
		/// <param name="Kind">One of <see cref="KindDisk"/>, <see cref="KindMachine"/>,
		/// <see cref="KindOrigin"/>, or any kind a third party invents. A kind this file does not
		/// weigh is worth no craft points but gates perfectly well.</param>
		/// <param name="Name">Blueprint name, origin, or trade. Case is folded away.</param>
		public static string ComposeKey(string Kind, string Name)
		{
			string kind = Fold(Kind);
			string name = Fold(Name);
			if (kind == null || name == null)
			{
				return null;
			}
			if (kind.IndexOf(RosterSeparator) >= 0 || name.IndexOf(RosterSeparator) >= 0 || kind.IndexOf(KindSeparator) >= 0)
			{
				return null;
			}
			return kind + KindSeparator + name;
		}

		/// <summary>The kind half of a roster key, or null when the key carries no kind.</summary>
		public static string KindOf(string Key)
		{
			string key = Fold(Key);
			if (key == null)
			{
				return null;
			}
			int at = key.IndexOf(KindSeparator);
			return (at <= 0) ? null : key.Substring(0, at);
		}

		/// <summary>The name half of a roster key; the whole key when it carries no kind.</summary>
		public static string NameOf(string Key)
		{
			string key = Fold(Key);
			if (key == null)
			{
				return null;
			}
			int at = key.IndexOf(KindSeparator);
			return (at < 0 || at >= key.Length - 1) ? key : key.Substring(at + 1);
		}

		/// <summary>
		/// Reads the settlement's stored roster. Order is preserved (oldest learning first, which
		/// is how the keepers' screen reads), duplicates and unusable keys are dropped, and a
		/// store that is null, empty, or complete nonsense yields an empty roster rather than
		/// throwing &mdash; an unreadable roster must never be able to cost a founder a building.
		/// </summary>
		public static List<string> DecodeRoster(string Encoded)
		{
			List<string> roster = new List<string>();
			if (string.IsNullOrEmpty(Encoded))
			{
				return roster;
			}
			string[] parts = Encoded.Split(RosterSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string key = Fold(parts[i]);
				if (key != null && !roster.Contains(key))
				{
					roster.Add(key);
				}
			}
			return roster;
		}

		/// <summary>Writes a roster back to its stored form. Round-trips
		/// <see cref="DecodeRoster"/> exactly, including the de-duplication.</summary>
		public static string EncodeRoster(IEnumerable<string> Roster)
		{
			List<string> keys = new List<string>();
			if (Roster != null)
			{
				foreach (string entry in Roster)
				{
					string key = Fold(entry);
					if (key != null && key.IndexOf(RosterSeparator) < 0 && !keys.Contains(key))
					{
						keys.Add(key);
					}
				}
			}
			return string.Join(RosterSeparator.ToString(), keys.ToArray());
		}

		/// <summary>
		/// Whether the roster satisfies one requirement. A requirement carrying a
		/// <see cref="KindSeparator"/> must match a key exactly; one without matches any key of
		/// any kind whose name half is the same, so an author can write
		/// <c>Knowledge="solar condenser"</c> and be satisfied by a disk, a certification, or a
		/// settler who already knew.
		/// </summary>
		public static bool Knows(IEnumerable<string> Roster, string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null)
			{
				return true;
			}
			if (Roster == null)
			{
				return false;
			}
			bool qualified = required.IndexOf(KindSeparator) >= 0;
			foreach (string entry in Roster)
			{
				string key = Fold(entry);
				if (key == null)
				{
					continue;
				}
				if (qualified ? (key == required) : (NameOf(key) == required))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Every requirement in a <c>Knowledge</c> list the roster does not satisfy, in
		/// the order the author wrote them. Empty when the settlement knows all of it.</summary>
		public static List<string> MissingKnowledge(IEnumerable<string> Roster, string Required)
		{
			List<string> missing = new List<string>();
			if (!Gated(Required))
			{
				return missing;
			}
			foreach (string token in Tokens(Required))
			{
				if (!Knows(Roster, token) && !missing.Contains(token))
				{
					missing.Add(token);
				}
			}
			return missing;
		}

		/// <summary>
		/// Craft points the roster is worth. Each kind is weighed by what it cost to acquire; a
		/// kind this file does not know is worth nothing, so a third party inventing a knowledge
		/// kind can gate designs on it without silently inflating the settlement's craft level.
		/// </summary>
		public static int TechPoints(IEnumerable<string> Roster)
		{
			if (Roster == null)
			{
				return 0;
			}
			int points = 0;
			List<string> counted = new List<string>();
			foreach (string entry in Roster)
			{
				string key = Fold(entry);
				if (key == null || counted.Contains(key))
				{
					continue;
				}
				counted.Add(key);
				points += PointsForKind(KindOf(key));
			}
			return points;
		}

		/// <summary>Points one roster key of the given kind is worth.</summary>
		public static int PointsForKind(string Kind)
		{
			string kind = Fold(Kind);
			if (kind == KindDisk)
			{
				return TechPointsPerDisk;
			}
			if (kind == KindMachine)
			{
				return TechPointsPerCertification;
			}
			if (kind == KindOrigin)
			{
				return TechPointsPerOrigin;
			}
			return 0;
		}

		/// <summary>
		/// The level a point total reaches. Monotonic and clamped at both ends: a negative total
		/// (which nothing here can produce, but a corrupted store could) reads as
		/// <see cref="TechLevel.Hands"/> rather than wrapping below it.
		/// </summary>
		public static TechLevel LevelForPoints(int Points)
		{
			TechLevel level = TechLevel.Hands;
			for (int i = 0; i < TechThresholds.Length; i++)
			{
				if (Points >= TechThresholds[i])
				{
					level = (TechLevel)i;
				}
			}
			return level;
		}

		/// <summary>Points the given level asks for. Out-of-range values clamp to the ends
		/// rather than throwing, because a level can arrive from third-party XML.</summary>
		public static int PointsForLevel(TechLevel Level)
		{
			int index = (int)Level;
			if (index < 0)
			{
				index = 0;
			}
			if (index >= TechThresholds.Length)
			{
				index = TechThresholds.Length - 1;
			}
			return TechThresholds[index];
		}

		/// <summary>Points still wanted for the next level up, or 0 at the top of the ladder.
		/// The number the keepers' screen shows so the level never looks like a mystery.</summary>
		public static int PointsToNext(int Points)
		{
			TechLevel level = LevelForPoints(Points);
			if ((int)level >= TechThresholds.Length - 1)
			{
				return 0;
			}
			int wanted = TechThresholds[(int)level + 1] - Points;
			return (wanted > 0) ? wanted : 0;
		}

		/// <summary>What the settlement calls a level. Out-of-range clamps rather than throws.</summary>
		public static string TechName(TechLevel Level)
		{
			int index = (int)Level;
			if (index < 0)
			{
				index = 0;
			}
			if (index >= TechLevelNames.Length)
			{
				index = TechLevelNames.Length - 1;
			}
			return TechLevelNames[index];
		}

		/// <summary>Whether a value is one of the levels this file defines. The guard that keeps
		/// <c>MinTech="99"</c> out of a gate.</summary>
		public static bool IsKnownTechLevel(TechLevel Level)
		{
			return (int)Level >= 0 && (int)Level < TechThresholds.Length;
		}

		/// <summary>
		/// Parses the four optional gate attributes off one <c>&lt;building&gt;</c> entry.
		/// <para>
		/// A malformed attribute is dropped and named in <paramref name="Error"/>; it never fails
		/// the entry. That asymmetry with <c>KingdomRules.TryParseBuildAttributes</c> is
		/// deliberate: <c>Cost</c> and <c>Ticks</c> are the design, so a bad one means there is
		/// no design, but a gate is a restriction ON a design, and a typo in one should never
		/// delete a building from the catalog. Failing open is also the safer direction &mdash;
		/// the worst case is a design that could have been harder to reach, not one that becomes
		/// permanently unreachable with no way for the founder to find out why.
		/// </para>
		/// </summary>
		/// <param name="Key">Building key, for the error text.</param>
		/// <param name="Districts">The <c>Districts</c> attribute, or null.</param>
		/// <param name="MinZones">The <c>MinZones</c> attribute, or null.</param>
		/// <param name="Knowledge">The <c>Knowledge</c> attribute, or null.</param>
		/// <param name="MinTech">The <c>MinTech</c> attribute (a level name or its number), or null.</param>
		/// <param name="Error">Null when every attribute parsed, else one sentence naming each
		/// attribute that was dropped. Callers log this; nothing else depends on its wording.</param>
		/// <returns>The gate. Never invalid; every dropped attribute reads as absent.</returns>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech, out string Error)
		{
			List<string> faults = new List<string>();
			string districts = null;
			if (!string.IsNullOrEmpty(Districts) && Districts.Trim().Length > 0)
			{
				districts = NormalizeList(Districts);
				if (districts == null)
				{
					faults.Add("Districts");
				}
				else if (ListContains(districts, AnyToken))
				{
					// "all" is how Styles spells "no restriction", so an author who writes it
					// here means the same thing rather than a district literally named all.
					districts = null;
				}
			}
			int minZones = 0;
			if (!string.IsNullOrEmpty(MinZones) && (!int.TryParse(MinZones, out minZones) || minZones < 0))
			{
				minZones = 0;
				faults.Add("MinZones");
			}
			string knowledge = null;
			if (!string.IsNullOrEmpty(Knowledge) && Knowledge.Trim().Length > 0)
			{
				knowledge = NormalizeList(Knowledge);
				if (knowledge == null || knowledge.IndexOf(RosterSeparator) >= 0)
				{
					knowledge = null;
					faults.Add("Knowledge");
				}
			}
			TechLevel minTech = TechLevel.Hands;
			if (!string.IsNullOrEmpty(MinTech) && (!System.Enum.TryParse<TechLevel>(MinTech.Trim(), ignoreCase: true, out minTech) || !IsKnownTechLevel(minTech)))
			{
				// Enum.TryParse takes any number the underlying type can hold, so "99" parses
				// happily into a level that does not exist and would gate the design forever.
				minTech = TechLevel.Hands;
				faults.Add("MinTech");
			}
			Error = (faults.Count == 0) ? null : ("building " + Key + " has a bad " + JoinOr(faults) + "; the attribute was ignored");
			return new ZoneGate(districts, minZones, knowledge, minTech);
		}

		/// <summary>
		/// The whole verdict on one design, against one piece of ground, for one settlement.
		/// Checks in <see cref="ZoningVerdict"/> order and returns at the first refusal, so the
		/// founder is told one thing to fix rather than four.
		/// </summary>
		/// <param name="Gate">The design's parsed gate. <c>ZoneGate.Open</c> always permits.</param>
		/// <param name="TileDistrict">District key on the ground being built on, or null.</param>
		/// <param name="Category">The design's <c>Category</c>, for the open-ground clause.</param>
		/// <param name="ClaimedZones">Zones the realm holds (<c>ClaimedZones.Count</c>).</param>
		/// <param name="Roster">Knowledge keys the settlement holds; null reads as none known.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones, IEnumerable<string> Roster)
		{
			List<string> missing = MissingKnowledge(Roster, Gate.Knowledge);
			if (missing.Count > 0)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedUnlearned, JoinAnd(DescribeKeys(missing)), "not known here");
			}
			TechLevel reached = LevelForPoints(TechPoints(Roster));
			if (Gate.MinTech > TechLevel.Hands && reached < Gate.MinTech)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedTechLevel, TechName(Gate.MinTech), "wants " + TechName(Gate.MinTech));
			}
			if (Gate.MinZones > 0 && ClaimedZones < Gate.MinZones)
			{
				string zones = Gate.MinZones + ((Gate.MinZones == 1) ? " claimed zone" : " claimed zones");
				return new ZoningJudgement(ZoningVerdict.RefusedTerritory, zones, "wants " + Gate.MinZones + " zones");
			}
			if (!DistrictAccepts(TileDistrict, Gate.Districts, Category))
			{
				string where = DescribeDistricts(Gate.Districts);
				return new ZoningJudgement(ZoningVerdict.RefusedDistrict, where, where);
			}
			return ZoningJudgement.Allowed;
		}

		/// <summary>Knowledge requirements read back for prose: the kind prefix is dropped,
		/// because "solar condenser" is what the founder calls it either way.</summary>
		public static List<string> DescribeKeys(IEnumerable<string> Keys)
		{
			List<string> names = new List<string>();
			if (Keys == null)
			{
				return names;
			}
			foreach (string entry in Keys)
			{
				string name = NameOf(entry);
				if (name != null && !names.Contains(name))
				{
					names.Add(name);
				}
			}
			return names;
		}

		/// <summary>Every token in a comma list, trimmed and case-folded, blanks dropped.</summary>
		public static List<string> Tokens(string Source)
		{
			List<string> tokens = new List<string>();
			if (string.IsNullOrEmpty(Source))
			{
				return tokens;
			}
			string[] parts = Source.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string token = Fold(parts[i]);
				if (token != null && !tokens.Contains(token))
				{
					tokens.Add(token);
				}
			}
			return tokens;
		}

		/// <summary>Joins prose with commas and a final "or". One item joins to itself.</summary>
		public static string JoinOr(IList<string> Items)
		{
			return Join(Items, "or");
		}

		/// <summary>Joins prose with commas and a final "and". One item joins to itself.</summary>
		public static string JoinAnd(IList<string> Items)
		{
			return Join(Items, "and");
		}

		private static string Join(IList<string> Items, string Conjunction)
		{
			if (Items == null || Items.Count == 0)
			{
				return null;
			}
			if (Items.Count == 1)
			{
				return Items[0];
			}
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i < Items.Count; i++)
			{
				if (i > 0)
				{
					text.Append((i == Items.Count - 1) ? (" " + Conjunction + " ") : ", ");
				}
				text.Append(Items[i]);
			}
			return text.ToString();
		}

		/// <summary>A list rewritten as its own trimmed, folded, de-duplicated tokens. Null when
		/// nothing usable survived, which is how a list of nothing but commas is caught.</summary>
		private static string NormalizeList(string Source)
		{
			List<string> tokens = Tokens(Source);
			if (tokens.Count == 0)
			{
				return null;
			}
			return string.Join(ListSeparator.ToString(), tokens.ToArray());
		}

		private static bool ListContains(string Source, string Token)
		{
			return Tokens(Source).Contains(Token);
		}

		/// <summary>True when a list attribute actually restricts anything.</summary>
		private static bool Gated(string Source)
		{
			if (string.IsNullOrEmpty(Source))
			{
				return false;
			}
			List<string> tokens = Tokens(Source);
			return tokens.Count > 0 && !tokens.Contains(AnyToken);
		}

		// Every key, token, and category in this file is compared case-folded and trimmed, in one
		// place, so that "Craft", " craft ", and "craft" cannot ever be three different districts.
		private static string Fold(string Text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return null;
			}
			string folded = Text.Trim().ToLowerInvariant();
			return (folded.Length == 0) ? null : folded;
		}
	}
}
