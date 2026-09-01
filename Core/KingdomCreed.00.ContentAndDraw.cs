using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomCreed
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionCreed") != "No";

		/// <summary>The string property a settler carries their creed on, alongside
		/// <c>KingdomOrigin</c>. Empty or absent means an ordinary settler, which is most of
		/// them.</summary>
		public const string CreedProperty = "KingdomCreed";

		/// <summary>
		/// The string property a settler carries the creeds they have HELD AND LEFT on, bounded to
		/// <c>KingdomCreedRules.MaxKeptCreeds</c> and joined by
		/// <c>KingdomCreedRules.KeptSeparator</c>. Empty or absent means somebody who has held one
		/// affiliation all their life, which is nearly everybody.
		/// <para>
		/// Stamped on the settler rather than kept in a map, for exactly the reason
		/// <c>KingdomConversion.CohabitTickProperty</c> gives: what a person has held with is a
		/// fact about them, and one carried on them survives a seat swap, a secession and a save
		/// without any per-city map having to remember to carry it. The city's own
		/// <c>KingdomSystem.CreedPastCounts</c> is a tally OF this, kept so that a gate can answer
		/// without the people being loaded.
		/// </para>
		/// </summary>
		public const string CreedPastProperty = "KingdomCreedPast";

		/// <summary>
		/// <c>HistoricalSignificance</c> at or above which an ancient faction still reads as a
		/// creed a living person could hold. Three: the tier the Putus Templar, the Mechanimists
		/// and the water barons sit at, which is where "a power the world turns on" begins.
		/// </summary>
		public const int CreedSignificance = 3;

		/// <summary>
		/// Whether a faction is a plausible community, people, polity, order, doctrine, or cult
		/// affiliation for a settler. The stored/public name remains Creed for compatibility.
		/// <para>
		/// A rule rather than a catalogue, so a faction another mod ships is judged on the same
		/// terms and needs no registration here. A creed is a faction the world names
		/// (<c>Visible</c>), that does not exist purely to hate the player
		/// (<c>HatesPlayer</c>), whose identity is not minted fresh each game (the procedural
		/// <c>SultanCult*</c> factions, whose display names come from game state), and that is
		/// either a people of present-day Qud (<c>Old="false"</c>), a power its history turns on
		/// (<see cref="CreedSignificance"/>), or something the language itself gives an article to
		/// (<c>FormatWithArticle</c> — "the villagers of Ezra", never "birds").
		/// </para>
		/// <para>
		/// Against shipped data this admits thirty-three affiliations. Their semantic kind is
		/// separately curated in KingdomCreeds.xml; admission never fabricates theology. It rejects
		/// fifty animal/plant buckets, internal markers, and procedural sultan cults.
		/// </para>
		/// </summary>
		/// <param name="Candidate">A faction. Null reads as unsuitable.</param>
		public static bool CanBeCreed(Faction Candidate)
		{
			return Candidate != null && KingdomCreedContentRules.CanBeCreed(
				new CreedFactionFacts(Candidate.Name, Candidate.Visible, Candidate.HatesPlayer,
					Candidate.Old, Candidate.HistoricalSignificance, Candidate.FormatWithArticle),
				CreedSignificance);
		}

		/// <summary>
		/// A creed as the founder should read it: the faction's formatted display name, article and
		/// all.
		/// </summary>
		/// <param name="CreedFactionName">A faction name, or null.</param>
		/// <returns>Empty for no creed. For a faction this build cannot resolve — a creed recorded
		/// by a save whose mods have since changed — the raw name, because naming it wrongly is
		/// better than silently dropping a city's whole character.</returns>
		public static string CreedName(string CreedFactionName)
		{
			if (string.IsNullOrEmpty(CreedFactionName))
			{
				return "";
			}
			Faction faction = Factions.GetIfExists(CreedFactionName);
			return (faction != null) ? faction.GetFormattedName() : CreedFactionName;
		}

		/// <summary>
		/// What one creed's faction thinks of another's, straight out of the engine.
		/// <para>
		/// <c>GetIfExists</c> rather than <c>Get</c>, for the reason
		/// <c>KingdomSystem.MirrorFeeling</c> gives: <c>Factions.Get</c> throws a bare exception on
		/// an unknown name, and a creed recorded in a save can outlive the faction that named it.
		/// (<c>Factions.GetFeelingFactionToFaction</c> would swallow that into a logged exception
		/// and a zero, which is the same answer with a stack trace in the player's log every pass.)
		/// </para>
		/// <para>
		/// An absent entry is <b>not</b> zero: the engine falls through to the faction's
		/// <c>"*"</c> general feeling, which for the Templar, the Girsh and the Children of Mamon
		/// is a standing -50 toward everyone they have not troubled to name. That is deliberate
		/// and is left alone — a faction that hates strangers by default should be hard to live
		/// beside.
		/// </para>
		/// </summary>
		/// <param name="From">The faction holding the opinion.</param>
		/// <param name="About">The faction it is held about.</param>
		/// <returns>The feeling, or 0 when either name is empty or unresolvable.</returns>
		public static int Feeling(string From, string About)
		{
			if (string.IsNullOrEmpty(From) || string.IsNullOrEmpty(About))
			{
				return 0;
			}
			Faction faction = Factions.GetIfExists(From);
			return (faction == null) ? 0 : faction.GetFeelingTowardsFaction(About);
		}

		/// <summary>
		/// How badly two creeds are at odds, read both ways because Qud's feelings are not
		/// symmetric — over half of all faction pairs in the shipped data disagree about each
		/// other.
		/// </summary>
		/// <returns>0 to 100; 0 when either city is mixed or both hold the same creed.</returns>
		public static int HostilityBetween(string CreedA, string CreedB)
		{
			if (string.IsNullOrEmpty(CreedA) || string.IsNullOrEmpty(CreedB))
			{
				return 0;
			}
			return KingdomCreedRules.Hostility(Feeling(CreedA, CreedB), Feeling(CreedB, CreedA), CreedA == CreedB);
		}

		/// <summary>The creed of one city, or null for a city of mixed people.</summary>
		/// <param name="City">A settlement record. Null reads as mixed.</param>
		public static string CreedOf(KingdomSettlement City)
		{
			return (City == null) ? null : KingdomCreedRules.DominantCreed(City.CreedCounts, City.Population);
		}

		/// <summary>The seated city's creed, or null.</summary>
		public static string SeatCreed(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return null;
			}
			return KingdomCreedRules.DominantCreed(System.CreedCounts, System.Population);
		}

		/// <summary>Compatibility read for the former two-city surface. Ambiguous in a three-city
		/// realm and therefore returns null there.</summary>
		[System.Obsolete("Enumerate KingdomSystem.NonSeatSettlements and call CreedOf.")]
		public static string AwayCreed(KingdomSystem System)
		{
			return System != null && System.NonSeatSettlementCount == 1 ?
				CreedOf(System.NonSeatSettlementAt(0)) : null;
		}

		/// <summary>
		/// Draws the creed of a settler arriving at the seated city.
		/// <para>
		/// Candidates are the factions the realm already has dealings with — its own standings
		/// ledger — plus whatever its cities already hold and whatever the founder declared. A
		/// young realm that has met nobody has no candidates and receives only ordinary settlers,
		/// which is the intent: affiliation walks in through what the founder did, not out of a table.
		/// </para>
		/// <para>
		/// Weighting is <see cref="KingdomCreedRules.CreedWeight"/>: the realm's standing with the
		/// faction, how many of its people already live here, and whether the founder named it.
		/// The ordinary settler always outweighs any single creed at the outset.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Unfounded returns empty.</param>
		/// <returns>A faction name, or empty for an ordinary settler.</returns>
		[System.Obsolete("Use the event-owned counter-RNG overload.")]
		public static string Draw(KingdomSystem System)
		{
			// The old signature has no event stream or ordinal. Guessing one would make a public
			// helper capable of rerolling on retry, so it now fails closed to the ordinary creed.
			return "";
		}

		/// <summary>Draws one creed on an already-admitted semantic event coordinate.</summary>
		internal static bool TryDraw(KingdomSystem System, KernelSeed128 Seed,
			SemanticEventKey Key, uint DrawIndex, out string Creed)
		{
			Creed = "";
			if (!Enabled || System == null || !System.Founded)
			{
				return true;
			}
			List<string> candidates = Candidates(System);
			if (candidates.Count == 0)
			{
				return true;
			}
			int[] weights = new int[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				System.CreedCounts.TryGetValue(candidates[i], out var here);
				weights[i] = KingdomCreedRules.CreedWeight(
					System.GetRegardForRealm(candidates[i]), here,
					candidates[i] == System.DeclaredCreed);
			}
			int total = KingdomCreedRules.TotalWeight(weights);
			ulong roll;
			KernelFaultCode fault;
			if (total <= 0 || !CounterRandom.TryDrawBelow(Seed, Key, DrawIndex,
				(ulong)total, out roll, out fault)) return false;
			int drawn = KingdomCreedRules.DrawCreed(weights, (int)roll);
			Creed = drawn < 0 ? "" : candidates[drawn];
			return true;
		}
	}
}
