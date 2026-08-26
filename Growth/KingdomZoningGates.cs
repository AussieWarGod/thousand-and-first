using System.Collections.Generic;

namespace ThousandAndFirst
{
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

		/// <summary>
		/// Where this design lives and where else it may stand: a comma list in the tag idiom whose
		/// FIRST welcomed token is the design's home stratum and whose remaining tokens are the
		/// strata it shares into (Addendum 15). Null when the design was written before strata
		/// existed, which stands everywhere &mdash; the whole of the back-compatibility promise, and
		/// the reason the weep-tap goes on being raised under the rock.
		/// <para>
		/// Distinct from the plot spec's <c>Sky</c> flag on purpose, and the two are not
		/// interchangeable: <c>Sky</c> says the work wants WEATHER, which is a fact about the design;
		/// this says which SET the design belongs to, which is a fact about the catalogue. A carved
		/// cell wants no weather and is still refused on open ground.
		/// </para>
		/// </summary>
		public readonly string Strata;

		/// <summary>
		/// Whether this design is one of the great works a city may keep exactly one of
		/// (Addendum 22 A1, Design B). False for every design in the catalogue but one, which is
		/// why the gate that reads it is inert almost everywhere.
		/// <para>
		/// Not a size and not a plot: an XL design is ordinary unless it says this. The theatre
		/// contends with the arcology because both are what a city is ABOUT, and nothing about
		/// twenty cells by fourteen says that on its own.
		/// </para>
		/// </summary>
		public readonly bool Megastructure;

		/// <summary>
		/// Whether only the capital may raise this design (Addendum 22 A4 and the capital ruling
		/// extending Addendum 19). False for every design in the catalogue but the arcology set.
		/// <para>
		/// <b>The second cardinality lane, and not a degree of the first.</b>
		/// <see cref="Megastructure"/> asks the city to spend its one purpose;
		/// this asks the realm to have set its crown down here, and a design declaring it never
		/// touches the purpose slot &mdash; which is what "a couple of extra capital-specific
		/// megastructures BEYOND its one" means when it is written down as a rule
		/// (<c>KingdomLabRules.JudgePurpose</c> holds the precedence).
		/// </para>
		/// </summary>
		public readonly bool Capital;

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech)
			: this(Districts, MinZones, Knowledge, MinTech, null, null, ShareUnsaid)
		{
		}

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech,
			string Builders, string Creed, int CreedShare)
			: this(Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, null)
		{
		}

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech,
			string Builders, string Creed, int CreedShare, string Strata)
			: this(Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, Strata, Megastructure: false)
		{
		}

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech,
			string Builders, string Creed, int CreedShare, string Strata, bool Megastructure)
			: this(Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, Strata, Megastructure, Capital: false)
		{
		}

		public ZoneGate(string Districts, int MinZones, string Knowledge, TechLevel MinTech,
			string Builders, string Creed, int CreedShare, string Strata, bool Megastructure, bool Capital)
		{
			this.Districts = Districts;
			this.MinZones = MinZones;
			this.Knowledge = Knowledge;
			this.MinTech = MinTech;
			this.Builders = Builders;
			this.Creed = Creed;
			this.CreedShare = CreedShare;
			this.Strata = Strata;
			this.Megastructure = Megastructure;
			this.Capital = Capital;
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
			&& MinTech <= TechLevel.Hands && string.IsNullOrEmpty(Builders) && string.IsNullOrEmpty(Creed)
			&& string.IsNullOrEmpty(Strata) && !Megastructure;
	}
}
