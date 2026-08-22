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
	/// advanced, then the realm is too small, then this stratum will never take it, then finally
	/// &mdash; and only then &mdash; this is the wrong ground. District comes last deliberately,
	/// so the founder hears "the forgeworks would take it" at the moment that sentence is the
	/// only thing standing between them and the building.
	/// <para>
	/// Stratum sits directly above district because the two are the same kind of refusal told at
	/// different scales, and only one of them can be answered: ground can be named a district
	/// tomorrow, and no naming puts weather under a mountain.
	/// </para>
	/// <para>
	/// <b>The three creed gates are checked FIRST and are numbered LAST</b>, which is the one
	/// place where the ordinals and the reading order disagree. They belong at the head of the
	/// order because who your people are is more fundamental than what your keepers were taught:
	/// a city with nobody who has ever held a creed is not one disk away from its shrine, it is a
	/// different city. They are numbered at the end because these ordinals are published
	/// (STANDARDS &sect;9), and renumbering a published enum to make it read prettily would move
	/// every value a third party already switched on. Appending is additive; renumbering is a
	/// break. <see cref="KingdomZoningRules.Judge"/> is the authority on the order.
	/// </para>
	/// </summary>
	public enum ZoningVerdict
	{
		Permitted = 0,
		RefusedUnlearned = 1,
		RefusedTechLevel = 2,
		RefusedTerritory = 3,
		RefusedStratum = 4,
		RefusedDistrict = 5,

		/// <summary>Nobody living here holds &mdash; or has ever held &mdash; the creed the design
		/// belongs to. Checked first of all.</summary>
		RefusedUnaligned = 6,

		/// <summary>The creed is held here, but by too few of the city for a work of it to stand.
		/// </summary>
		RefusedCreedShare = 7,

		/// <summary>The hands the design asks for are not among this city's people.</summary>
		RefusedBuilders = 8
	}

	/// <summary>
	/// Who a city's people are, as a gate has to see them: how many there are, where they walked
	/// in from, what they hold with, and what they have <em>held and left</em>. The last of those
	/// is the whole reason this type exists &mdash; a creed somebody once held is a fact about
	/// them from the moment they leave it (Addendum 16), and no tally of present belief can say
	/// it.
	/// <para>
	/// Three tallies and a head count, passed in already read. Lookups are case-insensitive and
	/// linear because these tallies hold a handful of entries each and a linear scan allocates
	/// nothing; nothing here ever enumerates one, so no answer depends on a dictionary's order.
	/// </para>
	/// <para>
	/// <see cref="Unknown"/> is the roll a caller could not supply, and every creed gate
	/// <b>permits</b> against it. That asymmetry is deliberate and is the same one
	/// <c>KingdomZoning.Permits</c> already makes: a gate that cannot see the city must never be
	/// the reason a founder cannot build in it.
	/// </para>
	/// </summary>
	public readonly struct BuilderRoll
	{
		/// <summary>False for <see cref="Unknown"/>. Every creed gate permits when this is false.
		/// </summary>
		public readonly bool Known;

		/// <summary>Everyone living in the city, believers and ordinary alike &mdash; the
		/// denominator the creed share is taken against, exactly as
		/// <c>KingdomCreedRules.DominantCreed</c> takes it.</summary>
		public readonly int People;

		private readonly IDictionary<string, int> origins;

		private readonly IDictionary<string, int> holding;

		private readonly IDictionary<string, int> kept;

		/// <param name="People">The city's population.</param>
		/// <param name="Origins">Country to people from it living here. Null reads as none.</param>
		/// <param name="Holding">Creed to people holding it now. Null reads as none.</param>
		/// <param name="Kept">Creed to people who have held it and left it. Null reads as none.
		/// </param>
		public BuilderRoll(int People, IDictionary<string, int> Origins, IDictionary<string, int> Holding, IDictionary<string, int> Kept)
		{
			Known = true;
			this.People = (People > 0) ? People : 0;
			origins = Origins;
			holding = Holding;
			kept = Kept;
		}

		/// <summary>A roll nobody supplied. Permits every creed gate; see the type's own summary.
		/// </summary>
		public static BuilderRoll Unknown => default(BuilderRoll);

		/// <summary>People here who walked in from this country.</summary>
		public int FromCountry(string Origin)
		{
			return Count(origins, Origin);
		}

		/// <summary>People here who hold with this creed today.</summary>
		public int HoldingNow(string Creed)
		{
			return Count(holding, Creed);
		}

		/// <summary>People here who have held this creed and left it.</summary>
		public int HeldOnce(string Creed)
		{
			return Count(kept, Creed);
		}

		/// <summary>People here who ALIGN with this creed: they hold it, or they once did. The
		/// alignment gate's whole arithmetic, and the visibility law's
		/// (<see cref="KingdomZoningRules.NoPathToCreed"/>).</summary>
		public int Aligned(string Creed)
		{
			return HoldingNow(Creed) + HeldOnce(Creed);
		}

		// Linear and case-insensitive: a creed is a faction name and an origin is a country, both
		// of them authored in one file and written again by hand in another, so "Barathrumites"
		// and "barathrumites" must be one creed. Summed rather than short-circuited so that two
		// keys differing only in case cannot hide half a tally.
		private static int Count(IDictionary<string, int> Tally, string Key)
		{
			if (Tally == null || string.IsNullOrEmpty(Key))
			{
				return 0;
			}
			int total = 0;
			foreach (KeyValuePair<string, int> entry in Tally)
			{
				if (entry.Value > 0 && string.Equals(entry.Key, Key, System.StringComparison.OrdinalIgnoreCase))
				{
					total += entry.Value;
				}
			}
			return total;
		}
	}

	/// <summary>
	/// The optional gates a <c>&lt;building&gt;</c> entry may declare, parsed. Every field
	/// has an "ungated" value and that value is what an absent attribute produces, so an entry
	/// written before these gates existed &mdash; ours or a third party's &mdash; is
	/// <see cref="IsOpen"/> and behaves exactly as it always did.
	/// <para>
	/// Four gates shipped first (district, ground, knowledge, craft). Addendum 16 added three
	/// more, and they are the creed stack: who must be here to raise it, which creed it belongs
	/// to, and how much of the city must hold that creed. The two constructors are both kept for
	/// the same reason the verdict enum was appended to rather than renumbered &mdash; a
	/// published shape is not re-cut under a third party who is already calling it.
	/// </para>
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

		/// <summary>
		/// Comma list of facts that must be true of the city's own people, ALL of them:
		/// <c>origin:the rust wells</c>, <c>creed:Barathrumites</c>, <c>kept:Mechanimists</c>, any
		/// of them optionally with a count (<c>origin:the rust wells:2</c>). Null when the design
		/// asks for nobody in particular. See <see cref="MissingBuilders"/> for the match rule.
		/// <para>
		/// Distinct from <see cref="Knowledge"/> on purpose: knowledge is what the keepers were
		/// TAUGHT and it never leaves, and this is who is STANDING here and it leaves when they
		/// do.
		/// </para>
		/// </summary>
		public readonly string Builders;

		/// <summary>
		/// The creed this design belongs to, by faction name, or null. Kept in the case the
		/// author wrote it in rather than folded, because it is handed to the engine's own
		/// faction table and read back to the founder as prose.
		/// </summary>
		public readonly string Creed;

		/// <summary>
		/// Percent of the city that must hold <see cref="Creed"/>. <see cref="ShareUnsaid"/> when
		/// the design named a creed and no share, which reads as
		/// <c>KingdomCreedRules.DominantSharePercent</c> &mdash; the same third a city's own creed
		/// is read at, so "a creed-work wants a creed city" is one rule and not two. Zero means one
		/// aligned builder is enough.
		/// </summary>
		public readonly int CreedShare;

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech)
			: this(Districts, MinZones, Knowledge, MinTech, null, null, ShareUnsaid)
		{
		}

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech,
			string Builders, string Creed, int CreedShare)
		{
			this.Districts = Districts;
			this.MinZones = MinZones;
			this.Knowledge = Knowledge;
			this.MinTech = MinTech;
			this.Builders = Builders;
			this.Creed = Creed;
			this.CreedShare = CreedShare;
		}

		/// <summary>What <see cref="CreedShare"/> holds when the attribute was not written. Not
		/// zero, because zero is a thing an author can mean.</summary>
		public const int ShareUnsaid = -1;

		/// <summary>A design that declares none of the gates. What an entry with no new
		/// attributes parses to, and the value used for any key the registry never registered.</summary>
		public static ZoneGate Open => new ZoneGate(null, 0, null, TechLevel.Hands);

		/// <summary>The share this gate actually asks for: what the author wrote, or the third a
		/// city's own creed is read at when they wrote nothing. Meaningless without
		/// <see cref="Creed"/>, and never consulted without it.</summary>
		public int EffectiveCreedShare => (CreedShare == ShareUnsaid) ? KingdomCreedRules.DominantSharePercent : CreedShare;

		/// <summary>True when nothing here can refuse anything.</summary>
		public bool IsOpen => string.IsNullOrEmpty(Districts) && MinZones <= 0 && string.IsNullOrEmpty(Knowledge)
			&& MinTech <= TechLevel.Hands && string.IsNullOrEmpty(Builders) && string.IsNullOrEmpty(Creed);
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

		/// <summary>
		/// Prefix that turns one token of a tag list into a refusal: <c>Styles="all,!eater"</c> is
		/// every style but the ancients'. Vanilla's own operator &mdash; <c>Chavvah</c>'s water
		/// ritual ships <c>RecipeGenotype="!True Kin"</c> in <c>Factions.xml</c> &mdash; so an
		/// author already knows how to read it.
		/// <para>
		/// It exists because the tag sets here are OPEN. A design that belongs everywhere except
		/// one place cannot say so by enumeration: the moment a third party declares a sixth
		/// style, every list that spelled "everywhere" as four names is quietly wrong about
		/// itself. A refusal stays right.
		/// </para>
		/// </summary>
		public const char NegationPrefix = '!';

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

		// ==================================================================================
		// Tags: the one list idiom every open-ended set in the catalogue is matched by.
		// ==================================================================================

		/// <summary>
		/// Whether a tag list accepts one value. The whole of what <c>Styles</c> means, and the
		/// shape Addendum 16 rules every open-ended catalogue set into: a comma list of tags,
		/// <see cref="AnyToken"/> for "all of them", and <see cref="NegationPrefix"/> for "all of
		/// them except this".
		/// <para>
		/// Three rules, in this order, and the order is the contract:
		/// </para>
		/// <list type="number">
		/// <item>An empty or absent list accepts everything. That is what keeps every entry
		/// written before a tag existed working untouched (STANDARDS &sect;6).</item>
		/// <item>A negation that matches refuses, whatever else the list says. An author who
		/// writes both a welcome and a refusal for the same tag meant the refusal &mdash; nobody
		/// writes <c>!x</c> by accident.</item>
		/// <item>Otherwise the list accepts when it names <see cref="AnyToken"/>, when it names
		/// the value, or when it names nothing but refusals &mdash; because a list of pure
		/// refusals is "everywhere except", and reading it as "nowhere" would gate the design out
		/// of the game.</item>
		/// </list>
		/// <para>
		/// Case-folded on both sides, unlike the exact comparison <c>Styles</c> shipped with. A
		/// tag is data an author types twice in two files, and <c>Verdant</c> silently matching
		/// nothing was a trap rather than a rule.
		/// </para>
		/// </summary>
		/// <param name="Tags">The authored list. Null and empty accept everything.</param>
		/// <param name="Value">The one tag being tested. Null is accepted only by a list that
		/// gates nothing, so a caller with nothing to test is never told it may not.</param>
		public static bool TagAccepts(string Tags, string Value)
		{
			List<string> tokens = Tokens(Tags);
			if (tokens.Count == 0)
			{
				return true;
			}
			string value = Fold(Value);
			bool welcomed = false;
			bool anyWelcome = false;
			for (int i = 0; i < tokens.Count; i++)
			{
				string token = tokens[i];
				if (token[0] == NegationPrefix)
				{
					string refused = token.Substring(1).Trim();
					if (refused.Length == 0)
					{
						continue;
					}
					if (refused == value || refused == AnyToken)
					{
						return false;
					}
					continue;
				}
				anyWelcome = true;
				if (token == AnyToken || token == value)
				{
					welcomed = true;
				}
			}
			return welcomed || !anyWelcome;
		}

		/// <summary>
		/// A tag list read back as prose: "the fungal or the eater city", "every style but the
		/// eater's". Names come back exactly as the author folded them, because a tag set is open
		/// and there is no table here to look a third party's tag up in.
		/// </summary>
		/// <returns>Null when the list gates nothing, so a caller can drop the whole clause.</returns>
		public static string DescribeTags(string Tags)
		{
			List<string> tokens = Tokens(Tags);
			if (tokens.Count == 0)
			{
				return null;
			}
			List<string> welcomed = new List<string>();
			List<string> refused = new List<string>();
			bool takesAll = false;
			for (int i = 0; i < tokens.Count; i++)
			{
				string token = tokens[i];
				if (token[0] == NegationPrefix)
				{
					string name = token.Substring(1).Trim();
					if (name.Length > 0)
					{
						refused.Add(name);
					}
					continue;
				}
				if (token == AnyToken)
				{
					takesAll = true;
					continue;
				}
				welcomed.Add(token);
			}
			if (welcomed.Count == 0 && refused.Count == 0)
			{
				return null;
			}
			if (welcomed.Count == 0)
			{
				return "anything but " + JoinOr(refused);
			}
			string said = takesAll ? ("anything, or " + JoinOr(welcomed)) : JoinOr(welcomed);
			return (refused.Count == 0) ? said : (said + ", but never " + JoinOr(refused));
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

		// ==================================================================================
		// The creed stack (Addendum 16): who is here, what they hold, and what they once held.
		// ==================================================================================

		/// <summary>A <c>Builders</c> kind: somebody living here holds that creed today. Read off
		/// the city's own creed tally, so it comes and goes with the believers.</summary>
		public const string KindCreed = "creed";

		/// <summary>A <c>Builders</c> kind: somebody here has held that creed and LEFT it. The one
		/// fact no tally of present belief can answer, and the reason a settler's creed history is
		/// recorded at all (Addendum 16).</summary>
		public const string KindKept = "kept";

		/// <summary>
		/// Whether the roll satisfies one <c>Builders</c> requirement.
		/// <para>
		/// A requirement is <c>kind:name</c>, or <c>kind:name:count</c> when one of them is not
		/// enough. The kinds are <see cref="KindOrigin"/> (people from a country),
		/// <see cref="KindCreed"/> (people holding a creed today) and <see cref="KindKept"/>
		/// (people who hold it or once did &mdash; the aligned). A requirement written as a bare
		/// name, with no kind, is satisfied by any of the three, exactly as
		/// <see cref="Knows"/> lets a bare <c>Knowledge</c> name be satisfied by any kind.
		/// </para>
		/// <para>
		/// A kind this file does not know never matches, and the refusal names the requirement as
		/// the author wrote it. That is the same bargain <see cref="Knows"/> strikes for an
		/// invented knowledge kind, told the other way round: a knowledge kind can be supplied by
		/// a third party's own <c>Learn</c> call, and a people-kind cannot, so an unknown one here
		/// is a gate that will never open and the log has to be able to say which.
		/// </para>
		/// </summary>
		public static bool HasBuilders(BuilderRoll Roll, string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null)
			{
				return true;
			}
			if (!Roll.Known)
			{
				return true;
			}
			string kind;
			string name;
			int wanted;
			if (!SplitBuilder(required, out kind, out name, out wanted))
			{
				return false;
			}
			if (kind == null)
			{
				return Roll.FromCountry(name) >= wanted || Roll.HoldingNow(name) >= wanted || Roll.Aligned(name) >= wanted;
			}
			if (kind == KindOrigin)
			{
				return Roll.FromCountry(name) >= wanted;
			}
			if (kind == KindCreed)
			{
				return Roll.HoldingNow(name) >= wanted;
			}
			if (kind == KindKept)
			{
				return Roll.Aligned(name) >= wanted;
			}
			return false;
		}

		/// <summary>Every requirement in a <c>Builders</c> list the roll does not satisfy, in the
		/// order the author wrote them. Empty when the city has all the hands it asks for.</summary>
		public static List<string> MissingBuilders(BuilderRoll Roll, string Required)
		{
			List<string> missing = new List<string>();
			if (!Gated(Required) || !Roll.Known)
			{
				return missing;
			}
			foreach (string token in Tokens(Required))
			{
				if (!HasBuilders(Roll, token) && !missing.Contains(token))
				{
					missing.Add(token);
				}
			}
			return missing;
		}

		/// <summary>One <c>Builders</c> requirement as prose: "somebody from the rust wells",
		/// "three who hold with the Barathrumites", "somebody who has ever held with the
		/// Mechanimists".</summary>
		public static string DescribeBuilder(string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null)
			{
				return "";
			}
			string kind;
			string name;
			int wanted;
			if (!SplitBuilder(required, out kind, out name, out wanted))
			{
				return required;
			}
			bool one = wanted <= 1;
			string many = one ? "somebody" : (wanted + " people");
			string holds = one ? "holds" : "hold";
			if (kind == KindOrigin)
			{
				return many + " from " + name;
			}
			if (kind == KindCreed)
			{
				return many + " who " + holds + " with " + name;
			}
			if (kind == KindKept)
			{
				return many + " who " + holds + ", or " + (one ? "has" : "have") + " ever held, with " + name;
			}
			return many + " answering to " + required;
		}

		/// <summary>Every requirement of a list, read back as prose.</summary>
		public static List<string> DescribeBuilders(IEnumerable<string> Requirements)
		{
			List<string> said = new List<string>();
			if (Requirements == null)
			{
				return said;
			}
			foreach (string requirement in Requirements)
			{
				string one = DescribeBuilder(requirement);
				if (!string.IsNullOrEmpty(one) && !said.Contains(one))
				{
					said.Add(one);
				}
			}
			return said;
		}

		/// <summary>
		/// Whether anybody here ALIGNS with a creed: holds it, or has held it and left it. The
		/// alignment gate of Addendum 16 clause (4), and &mdash; through
		/// <see cref="NoPathToCreed"/> &mdash; the visibility law of Addendum 14 as it applies to
		/// creed-works.
		/// </summary>
		/// <returns>True for a design that names no creed, and true against
		/// <see cref="BuilderRoll.Unknown"/>.</returns>
		public static bool Aligned(BuilderRoll Roll, string Creed)
		{
			if (string.IsNullOrEmpty(Creed) || !Roll.Known)
			{
				return true;
			}
			return Roll.Aligned(Creed) > 0;
		}

		/// <summary>
		/// A city with no way to this design at all: it names a creed, nobody here holds that
		/// creed, and nobody here ever has. Addendum 14's visibility law &mdash; <i>you especially
		/// cannot see what you CAN'T unlock</i> &mdash; and the exact complement of
		/// <see cref="Aligned"/>, deliberately, so that "shown" and "buildable" can never drift
		/// apart into two rules.
		/// <para>
		/// A creed somebody once held is still a path: they can be turned back, and their
		/// household can be turned with them. Only a creed no one here has ever carried is a door
		/// with no key, and only that one is hidden.
		/// </para>
		/// </summary>
		public static bool NoPathToCreed(BuilderRoll Roll, string Creed)
		{
			return !Aligned(Roll, Creed);
		}

		/// <summary>
		/// Whether enough of the city holds a creed for a work of it to stand &mdash; Addendum 16
		/// clause (2), the AMOUNT.
		/// <para>
		/// The threshold is not chosen here. It is <c>KingdomCreedRules.DominantCreed</c>'s own
		/// arithmetic, minus one clause: at least <c>KingdomCreedRules.MinBelievers</c> people, and
		/// at least the asked share of everyone living there. What is dropped is the
		/// no-larger-rival test, and dropping it is the point &mdash; that test answers "what creed
		/// is this CITY", and a congregation large enough to raise its own shrine does not have to
		/// be the largest congregation in town.
		/// </para>
		/// </summary>
		/// <param name="Holding">People holding the creed now.</param>
		/// <param name="People">Everyone living in the city.</param>
		/// <param name="Percent">The share asked for. Zero and below ask for no share at all, and
		/// the believers floor goes with it: an author who writes <c>CreedShare="0"</c> has said
		/// one aligned builder is enough.</param>
		public static bool CreedShareMet(int Holding, int People, int Percent)
		{
			if (Percent <= 0)
			{
				return true;
			}
			if (Holding < KingdomCreedRules.MinBelievers || People <= 0)
			{
				return false;
			}
			return Holding * 100 >= People * Percent;
		}

		/// <summary>The share a city actually holds, in whole percent, for the sentence that names
		/// it. Zero for a city with nobody in it, which is the only honest answer.</summary>
		public static int ShareHeld(int Holding, int People)
		{
			if (Holding <= 0 || People <= 0)
			{
				return 0;
			}
			return Holding * 100 / People;
		}

		// A Builders token, split. `kind:name`, `kind:name:count`, or a bare `name` (kind null).
		// A count that is not a positive number is not a count -- it is part of the name, because
		// nothing stops a country or a faction from ending in a colon and a word.
		private static bool SplitBuilder(string Requirement, out string Kind, out string Name, out int Wanted)
		{
			Kind = null;
			Name = Requirement;
			Wanted = 1;
			if (string.IsNullOrEmpty(Requirement))
			{
				return false;
			}
			int last = Requirement.LastIndexOf(KindSeparator);
			if (last > 0 && last < Requirement.Length - 1)
			{
				int count;
				if (int.TryParse(Requirement.Substring(last + 1), out count) && count > 0)
				{
					Wanted = count;
					Name = Requirement.Substring(0, last);
				}
			}
			int first = Name.IndexOf(KindSeparator);
			if (first > 0 && first < Name.Length - 1)
			{
				Kind = Name.Substring(0, first);
				Name = Name.Substring(first + 1);
			}
			return !string.IsNullOrEmpty(Name);
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
			return ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech, null, null, null, out Error);
		}

		/// <summary>
		/// The same parse with Addendum 16's three creed attributes folded in. Kept as a second
		/// overload rather than a widened signature because the first one is published and a third
		/// party may already be calling it (STANDARDS &sect;9); the four-gate overload is exactly
		/// this one with three nulls.
		/// </summary>
		/// <param name="Builders">The <c>Builders</c> attribute, or null.</param>
		/// <param name="Creed">The <c>Creed</c> attribute, or null.</param>
		/// <param name="CreedShare">The <c>CreedShare</c> attribute (a whole percent), or null.</param>
		public static ZoneGate ParseGateAttributes(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, out string Error)
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
			string builders = null;
			if (!string.IsNullOrEmpty(Builders) && Builders.Trim().Length > 0)
			{
				builders = NormalizeList(Builders);
				if (builders == null || ListContains(builders, AnyToken))
				{
					// "all" is how every list in this file spells "no restriction", and a design
					// that wants anybody at all wants nobody in particular.
					builders = null;
				}
			}
			// Trimmed and NOT folded: this is handed to the engine's faction table and read back to
			// the founder as prose, and a faction name is the game's, not ours, to re-case.
			string creed = (string.IsNullOrEmpty(Creed) || Creed.Trim().Length == 0) ? null : Creed.Trim();
			int creedShare = ZoneGate.ShareUnsaid;
			if (!string.IsNullOrEmpty(CreedShare) && CreedShare.Trim().Length > 0)
			{
				// A share outside 0..100 is not a stricter gate, it is a design nobody can ever
				// raise; dropped to the default like every other malformed attribute here.
				if (!int.TryParse(CreedShare.Trim(), out creedShare) || creedShare < 0 || creedShare > 100)
				{
					creedShare = ZoneGate.ShareUnsaid;
					faults.Add("CreedShare");
				}
			}
			if (creed == null && (creedShare != ZoneGate.ShareUnsaid || builders != null))
			{
				// Not a fault: Builders stands perfectly well on its own. A share without a creed
				// does not, and saying so is cheaper than a design that silently ignores half of
				// what its author wrote.
				if (creedShare != ZoneGate.ShareUnsaid)
				{
					creedShare = ZoneGate.ShareUnsaid;
					faults.Add("CreedShare (no Creed to take a share of)");
				}
			}
			Error = (faults.Count == 0) ? null : ("building " + Key + " has a bad " + JoinOr(faults) + "; the attribute was ignored");
			return new ZoneGate(districts, minZones, knowledge, minTech, builders, creed, creedShare);
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
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground: false, RequiresSky: false);
		}

		/// <summary>
		/// The same verdict, with the stratum the ground sits in folded in &mdash; which is what
		/// narrows the catalogue by depth at the moment the founder is choosing, rather than
		/// after they have chosen (the brief's "different catalogue subset by stratum", as far as
		/// the catalogue's own attributes can express it today).
		/// <para>
		/// The subset is derived, never authored: a design that declares <c>Sky</c> wants sun,
		/// wind, or rain, and there is none of any of them under the rock. That refusal already
		/// existed at <c>KingdomPlotRules.RefuseSky</c>, one step further down, where it fires
		/// only once the founder has picked the design and the commission is already running.
		/// Reading it here as a gate means the menu itself carries the tag, and the whole
		/// catalogue still shows &mdash; a list that silently shortens teaches nothing.
		/// </para>
		/// <para>
		/// Only the surface-only half of the stratum subset is expressible from what the
		/// catalogue declares today: nothing can yet say "this design belongs to the deep". That
		/// half wants an authored attribute and is deliberately not faked here.
		/// </para>
		/// </summary>
		/// <param name="Underground">Whether the ground is below <c>KingdomRules.SurfaceZLevel</c>
		/// (<c>KingdomPlotRules.IsUnderground</c>).</param>
		/// <param name="RequiresSky">The design's <c>Sky</c> flag.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky)
		{
			return Judge(Gate, TileDistrict, Category, ClaimedZones, Roster, Underground, RequiresSky, BuilderRoll.Unknown);
		}

		/// <summary>
		/// The same verdict with the city's own people folded in, which is what Addendum 16's
		/// creed stack is judged against.
		/// <para>
		/// The three creed gates are checked BEFORE all five of the older ones, in the order the
		/// addendum states them: alignment, then amount, then the hands. Alignment leads because it
		/// is the only one of the eight that a founder cannot answer by doing anything to the
		/// ground &mdash; a city where nobody has ever held the creed is not short of a disk or a
		/// parasang, and telling them about the disk first would send them the wrong way for a
		/// season.
		/// </para>
		/// <para>
		/// Against <see cref="BuilderRoll.Unknown"/> every creed gate permits, so this overload
		/// answers exactly as the one above it for a caller who has no roll to give.
		/// </para>
		/// </summary>
		/// <param name="Roll">Who lives here. <see cref="BuilderRoll.Unknown"/> permits.</param>
		public static ZoningJudgement Judge(ZoneGate Gate, string TileDistrict, string Category, int ClaimedZones,
			IEnumerable<string> Roster, bool Underground, bool RequiresSky, BuilderRoll Roll)
		{
			if (!Aligned(Roll, Gate.Creed))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedUnaligned, Gate.Creed, "no one holds with it");
			}
			if (!string.IsNullOrEmpty(Gate.Creed) && Roll.Known
				&& !CreedShareMet(Roll.HoldingNow(Gate.Creed), Roll.People, Gate.EffectiveCreedShare))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedCreedShare, Gate.Creed,
					"wants " + Gate.EffectiveCreedShare + "% of the city");
			}
			List<string> hands = MissingBuilders(Roll, Gate.Builders);
			if (hands.Count > 0)
			{
				return new ZoningJudgement(ZoningVerdict.RefusedBuilders, JoinAnd(DescribeBuilders(hands)), "nobody here can");
			}
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
			if (!StratumAccepts(Underground, RequiresSky))
			{
				return new ZoningJudgement(ZoningVerdict.RefusedStratum, StratumName(Underground: false), "wants open sky");
			}
			if (!DistrictAccepts(TileDistrict, Gate.Districts, Category))
			{
				string where = DescribeDistricts(Gate.Districts);
				return new ZoningJudgement(ZoningVerdict.RefusedDistrict, where, where);
			}
			return ZoningJudgement.Allowed;
		}

		/// <summary>Whether this stratum will take this design at all. The one depth rule the
		/// catalogue can state today: weather does not reach under the rock.</summary>
		public static bool StratumAccepts(bool Underground, bool RequiresSky)
		{
			return !(Underground && RequiresSky);
		}

		/// <summary>What the founder calls the stratum, for the sentence that names it.</summary>
		public static string StratumName(bool Underground)
		{
			return Underground ? "under the rock" : "open sky";
		}

		// ==================================================================================
		// The claim: what widens the ground every gate above is measured against.
		// ==================================================================================

		/// <summary>
		/// Why the founder's claim may not proceed, or <see cref="ClaimVerdict.Allowed"/>. Pure,
		/// so the rule is tabled rather than discovered in the field &mdash; the same bargain
		/// <c>KingdomSettlement.JudgeSecondFounding</c> makes for the founding rite.
		/// <para>
		/// Ordered from the fact nothing can change to the one the founder can answer today:
		/// there is no realm, then this ground is already somebody's, then it is out of reach,
		/// then finally the city is not yet large enough to hold more.
		/// </para>
		/// </summary>
		public enum ClaimVerdict
		{
			Allowed = 0,
			NothingFoundedYet = 1,
			GroundIsAlreadyOurs = 2,
			GroundIsAnotherCitys = 3,
			GroundIsAnotherRealms = 4,
			GroundIsForeign = 5,
			GroundIsNotAdjacent = 6,
			CityHoldsAllItCan = 7
		}

		/// <summary>
		/// How many zones a city of this stage holds at most.
		/// <para>
		/// Not a number chosen here: it is read off the catalogue the brief already wrote. The
		/// eight <c>MinZones</c> designs line up exactly with stage &mdash; the two-zone designs
		/// are <c>MinStage="Village"</c>, the three-zone designs are <c>Town</c>, and the
		/// four-zone designs are <c>City</c> &mdash; so a settlement reaches the ground a design
		/// wants at the same moment it reaches the stage that design wants. A camp claiming its
		/// fourth parasang would have made every one of those gates meaningless.
		/// </para>
		/// <para>
		/// A stage this build does not define reads as one zone: the founding claim, and no
		/// expansion out of a bad cast.
		/// </para>
		/// </summary>
		public static int ZonesForStage(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Village:
				return 2;
			case GrowthStage.Town:
				return 3;
			case GrowthStage.City:
				return 4;
			case GrowthStage.Camp:
			case GrowthStage.Steading:
				return 1;
			default:
				return 1;
			}
		}

		/// <summary>
		/// Judges whether the founder standing on this ground may take it into the seated city.
		/// </summary>
		/// <param name="Founded">Whether the realm exists at all.</param>
		/// <param name="Stage">What the seated city has become.</param>
		/// <param name="ZonesHeld">Zones the seated city already holds.</param>
		/// <param name="GroundIsOurs">Whether the seated city already holds this zone.</param>
		/// <param name="GroundIsAnotherCitys">Whether the realm's other city holds it. Two cities
		/// claiming one zone would break the seat quietly rather than loudly.</param>
		/// <param name="GroundIsAnotherRealms">Whether a realm that put this founder out still
		/// holds it. That city goes on without them.</param>
		/// <param name="GroundIsForeign">Whether some other faction already answers for it.</param>
		/// <param name="GroundIsAdjacent">Whether it borders ground the seated city holds &mdash;
		/// including the stratum directly above or below, which is what a cellar is.</param>
		public static ClaimVerdict JudgeClaim(bool Founded, GrowthStage Stage, int ZonesHeld, bool GroundIsOurs,
			bool GroundIsAnotherCitys, bool GroundIsAnotherRealms, bool GroundIsForeign, bool GroundIsAdjacent)
		{
			if (!Founded)
			{
				return ClaimVerdict.NothingFoundedYet;
			}
			if (GroundIsOurs)
			{
				return ClaimVerdict.GroundIsAlreadyOurs;
			}
			if (GroundIsAnotherCitys)
			{
				return ClaimVerdict.GroundIsAnotherCitys;
			}
			if (GroundIsAnotherRealms)
			{
				return ClaimVerdict.GroundIsAnotherRealms;
			}
			if (GroundIsForeign)
			{
				return ClaimVerdict.GroundIsForeign;
			}
			if (!GroundIsAdjacent)
			{
				return ClaimVerdict.GroundIsNotAdjacent;
			}
			if (ZonesHeld >= ZonesForStage(Stage))
			{
				return ClaimVerdict.CityHoldsAllItCan;
			}
			return ClaimVerdict.Allowed;
		}

		/// <summary>
		/// What the founder is told when the claim will not proceed. Every branch names the lack
		/// AND what lifts it (STANDARDS 7b): a refusal that only says no teaches nothing.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="ClaimVerdict.Allowed"/> returns empty.</param>
		/// <param name="SeatName">The seated city's name.</param>
		/// <param name="Stage">What the seated city has become, for the sentence that names the
		/// rung it would have to reach.</param>
		public static string ClaimRefusal(ClaimVerdict Verdict, string SeatName, GrowthStage Stage)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : ("{{C|" + SeatName + "}}");
			switch (Verdict)
			{
			case ClaimVerdict.NothingFoundedYet:
				return "There is no city yet to take ground for. Pour the first water somewhere first.";
			case ClaimVerdict.GroundIsAlreadyOurs:
				return "This ground is already " + seat + "'s. Walk out to the parasang next door and claim there.";
			case ClaimVerdict.GroundIsAnotherCitys:
				return "Your other city holds this ground, and one parasang answers to one city. Nothing has changed.";
			case ClaimVerdict.GroundIsAnotherRealms:
				return "This ground is not yours to take any more. Ask that realm to have you back, or walk until the ground answers to nobody.";
			case ClaimVerdict.GroundIsForeign:
				return "This ground already answers to someone else. Nothing is annexed by pouring water on it: a living village is asked into a covenant through the charter rite, and anything else foreign simply is not this rite's to take.";
			case ClaimVerdict.GroundIsNotAdjacent:
				return "A city grows outward from what it already holds. Stand on ground that borders " + seat
					+ " — beside it, or the stratum directly above or below it — and claim there.";
			case ClaimVerdict.CityHoldsAllItCan:
				if (Stage >= GrowthStage.City)
				{
					return seat + " holds " + ZoneCount(ZonesForStage(Stage))
						+ ", which is all the ground one city answers for. Pour again out past the horizon of what you hold if you want more.";
				}
				return seat + " is " + StageWord(Stage) + ", and " + StageWord(Stage) + " holds "
					+ ZoneCount(ZonesForStage(Stage)) + ". Grow into " + StageWord(NextStage(Stage))
					+ " and this ground is yours to take.";
			default:
				return "";
			}
		}

		/// <summary>
		/// What a claim did to the wall line. The brief's "walls move outward as the city spans
		/// zones" is real but quiet: nothing standing is moved (the protection law forbids it)
		/// &mdash; the edge simply stops facing the world, so every wall raised from here goes to
		/// the new outer line and the old line becomes an inner wall. A founder who is not told
		/// that has no way to see it happened.
		/// <para>
		/// A claim that frees no edge says so too, and that is not a bug. <c>FrontierEdges</c>
		/// clears an edge only for an orthogonal neighbour in the same stratum, so ground taken
		/// diagonally across a corner, or straight down into the rock, is legal ground that
		/// leaves the wall line exactly where it was. Saying otherwise would be the lie.
		/// </para>
		/// </summary>
		/// <param name="Before">Edges of the ground the city ALREADY held that faced the world
		/// before the claim, summed across those zones.</param>
		/// <param name="After">The same zones' edges facing the world after it.</param>
		/// <param name="SeatName">The seated city's name.</param>
		public static string ClaimedWallClause(int Before, int After, string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			int freed = Before - After;
			if (freed <= 0)
			{
				return "The wall line does not move: this ground touches no side " + seat + " was already walling, so every edge it held still faces the world.";
			}
			return "The wall line moves outward. What " + seat + " raises from here stands on the new edge, and "
				+ ((freed == 1) ? "the side you claimed past becomes an inner wall" : ("the " + freed + " sides you claimed past become inner walls"))
				+ " — nothing already built is moved.";
		}

		/// <summary>How many of the four edges are set.</summary>
		public static int EdgeCount(KingdomRules.Frontier Edges)
		{
			int count = 0;
			if ((Edges & KingdomRules.Frontier.North) != 0) { count++; }
			if ((Edges & KingdomRules.Frontier.South) != 0) { count++; }
			if ((Edges & KingdomRules.Frontier.West) != 0) { count++; }
			if ((Edges & KingdomRules.Frontier.East) != 0) { count++; }
			return count;
		}

		/// <summary>
		/// The line that tells the founder how much ground the city holds and how much a city of
		/// its rung may hold. Said after a claim so the next one is never a surprise.
		/// </summary>
		/// <param name="Held">Parasangs the city holds now.</param>
		/// <param name="Ceiling">What <see cref="ZonesForStage"/> allows it.</param>
		public static string ClaimHoldingLine(int Held, int Ceiling)
		{
			int held = (Held < 0) ? 0 : Held;
			int ceiling = (Ceiling < held) ? held : Ceiling;
			if (held >= ceiling)
			{
				return "{{K|" + ZoneCount(held) + " held, which is all this rung answers for.}}";
			}
			int room = ceiling - held;
			return "{{K|" + ZoneCount(held) + " held; room for " + ((room == 1) ? "one more" : (room + " more")) + " at this rung.}}";
		}

		private static string ZoneCount(int Zones)
		{
			return (Zones == 1) ? "one parasang" : (Zones + " parasangs");
		}

		private static string StageWord(GrowthStage Stage)
		{
			return "a " + Stage.ToString().ToLowerInvariant();
		}

		private static GrowthStage NextStage(GrowthStage Stage)
		{
			// Floors at City rather than running off the end of the ladder: a City that somehow
			// holds its four parasangs is told to grow into a City, which is odd but honest, and
			// is unreachable while ZonesForStage tops out with the ladder.
			return (Stage >= GrowthStage.City) ? GrowthStage.City : (GrowthStage)((int)Stage + 1);
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
