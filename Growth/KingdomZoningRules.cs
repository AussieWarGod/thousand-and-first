using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for what a founder may commission and where. Four gates sat on top of
	/// the city style and growth stage <c>KingdomRules</c> already applies: the district the
	/// ground carries, how much ground the realm holds, which designs the keepers have learned,
	/// and how far the settlement's own craft has come. Addendum 16 added the three creed gates
	/// and Addendum 15 added <c>Strata</c> &mdash; which set of the catalogue a design lives in,
	/// and which strata it may stand in besides. The catalogue brief's covenant axis is the live
	/// kingdom-standing gate parsed at the head of this class and judged before this ground stack.
	/// <para>
	/// Every one of them is an OPTIONAL attribute on a <c>&lt;building&gt;</c> entry and an absent
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
	public static partial class KingdomZoningRules
	{
		/// <summary>Hard bounds for one city's permanent keeper-knowledge heap. The roster is a
		/// save string rather than a row reference in reality, so both its decoded shape and both
		/// common encodings are bounded. These caps are also priced by KingdomCityMemoryRules.</summary>
		public const int MaxRosterRows = 512;
		public const int MaxRosterKeyChars = 512;
		public const int MaxRosterKeyUtf8Bytes = 1024;
		public const int MaxRosterEncodedChars = 8192;
		public const int MaxRosterEncodedUtf8Bytes = 16384;
		/// <summary>Accepted authored range on Qud's ordinary reputation scale. Runtime standing
		/// may move outside it; catalogue thresholds may not, because an unreachable typo must fail
		/// during load rather than masquerade as a permanent gate.</summary>
		public const int CovenantStandingFloor = -1000;

		public const int CovenantStandingCeiling = 1000;

		public const int CovenantFactionMaxLength = 128;

		/// <summary>
		/// Parses the paired <c>Covenant</c>/<c>MinStanding</c> building attributes. Both blank is
		/// the open legacy behaviour; exactly one, a control character, an oversized faction key,
		/// or an out-of-scale threshold is malformed and fails loudly.
		/// </summary>
		public static bool TryParseCovenantAttributes(string Key, string Covenant, string MinStanding,
			out CovenantGate Gate, out string Error)
		{
			Gate = CovenantGate.Open;
			Error = null;
			string faction = string.IsNullOrWhiteSpace(Covenant) ? null : Covenant.Trim();
			string standing = string.IsNullOrWhiteSpace(MinStanding) ? null : MinStanding.Trim();
			string named = string.IsNullOrWhiteSpace(Key) ? "building" : ("building " + Key);
			if (faction == null && standing == null)
			{
				return true;
			}
			if (faction == null || standing == null)
			{
				Error = named + " must name Covenant and MinStanding together";
				return false;
			}
			if (faction.Length > CovenantFactionMaxLength)
			{
				Error = named + " has an overlong Covenant faction key";
				return false;
			}
			for (int i = 0; i < faction.Length; i++)
			{
				if (char.IsControl(faction[i]))
				{
					Error = named + " has a control character in Covenant";
					return false;
				}
			}
			if (!int.TryParse(standing, out int threshold)
				|| threshold < CovenantStandingFloor || threshold > CovenantStandingCeiling)
			{
				Error = named + " has a bad MinStanding (expected " + CovenantStandingFloor
					+ " to " + CovenantStandingCeiling + ")";
				return false;
			}
			Gate = new CovenantGate(faction, threshold);
			return true;
		}

		/// <summary>Judges one already-validated covenant gate against the realm's current
		/// standing. Open gates always permit; a refusal names both the faction and threshold in its
		/// short menu note.</summary>
		public static ZoningJudgement JudgeCovenant(CovenantGate Gate, int Standing)
		{
			if (Gate.IsOpen || Standing >= Gate.MinStanding)
			{
				return ZoningJudgement.Allowed;
			}
			return new ZoningJudgement(ZoningVerdict.RefusedCovenantStanding, Gate.Faction,
				"wants " + Gate.MinStanding + " standing with " + Gate.Faction);
		}
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

		/// <summary>A thing this city's keepers worked out for themselves at their own bench.
		/// Minted by <see cref="KingdomResearchRules"/> through the same <c>Learn</c> the disk and
		/// the certification use, and matched by the same <see cref="Knows"/>: research is a new
		/// SOURCE of keys, never a parallel gate system.</summary>
		public const string KindNode = "node";

		/// <summary>Water shared with a faction, and what they taught over it. A seed and never a
		/// ceiling (Addendum 18): it reveals a branch and begins its head, and no rite anywhere
		/// finishes a node.</summary>
		public const string KindRite = "rite";

		/// <summary>A treatise carried home and read to the keepers.</summary>
		public const string KindBook = "book";

		/// <summary>A lodged notable, who teaches while they stay. Live, like
		/// <see cref="KindOrigin"/>: the holding lapses when they leave and returns with them.</summary>
		public const string KindSavant = "savant";

		/// <summary>What a people KNOWS (Addendum 17). Projected live from resident bodies'
		/// vanilla culture, so nodes and third-party designs share the ordinary roster gate.</summary>
		public const string KindCulture = "culture";

		/// <summary>What a body IS (Addendum 17). Projected live from vanilla species and kept
		/// separate from culture because anatomy and practice are not synonyms.</summary>
		public const string KindSpecies = "species";

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

		/// <summary>
		/// Points a research node is worth: none, and for a harder version of the reason an origin
		/// is worth none. A research system that raised the craft rung would make that rung a
		/// readout of the research system rather than of what was found in the world, and the two
		/// ladders are orthogonal on purpose &mdash; craft is disks and certifications, exactly as
		/// it was the day before nodes existed.
		/// </summary>
		public const int TechPointsPerNode = 0;

		/// <summary>Points needed for each <see cref="TechLevel"/>, by its numeric value.</summary>
		public static readonly int[] TechThresholds = new int[5] { 0, 2, 5, 9, 14 };

		/// <summary>What the settlement calls each level of its own craft.</summary>
		public static readonly string[] TechLevelNames = new string[5] { "hands", "salvage", "workshop", "foundry", "arclight" };

	}
}
