using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		// --- The refined half: what a yard makes, and out of what ------------------------------

		/// <summary>Registry keys for the three yards, in enum order: what a <c>Refines</c>
		/// attribute may write instead of the refined material's own key.</summary>
		public static readonly string[] YardKeys = new string[YardCount] { "sawyer", "mason", "smelter" };

		/// <summary>Player-facing names for the three yards, in enum order.</summary>
		public static readonly string[] YardNames = new string[YardCount] { "sawyer's yard", "mason's yard", "smelter" };

		/// <summary>What each yard turns raw stock INTO, in yard order.</summary>
		public static readonly KingdomMaterial[] YardMakes = new KingdomMaterial[YardCount]
		{
			KingdomMaterial.ShapedTimber,
			KingdomMaterial.ShapedStone,
			KingdomMaterial.WorkedMetal
		};

		/// <summary>
		/// What each yard EATS, richest acceptable stock first. A mason's yard will dress marble
		/// as readily as shale and the settlement would rather it did not, so the plain stock is
		/// listed first everywhere and the rarer alternative last &mdash; a yard reaches for the
		/// marble only when there is no ordinary stone to work.
		/// </summary>
		public static readonly KingdomMaterial[][] YardEats = new KingdomMaterial[YardCount][]
		{
			new KingdomMaterial[1] { KingdomMaterial.Timber },
			new KingdomMaterial[2] { KingdomMaterial.Stone, KingdomMaterial.Marble },
			new KingdomMaterial[1] { KingdomMaterial.Scrap }
		};

		/// <summary>Raw loads one refined unit is made of. Two: a yard is a place where half of
		/// what comes in leaves as spoil, sawdust, and slag, and the other half leaves better than
		/// it arrived.</summary>
		public const int RawPerRefined = 2;

		/// <summary>Effort one refined unit costs a crew. Dearer per unit than clearing a cell
		/// (<see cref="StandingEffort"/>) because the work is finer, and denominated in the same
		/// effort points so one day of one pair of hands means the same thing everywhere.</summary>
		public const int RefineEffortPerUnit = 15;

		/// <summary>
		/// Refined units one yard can finish in a DAY, however many hands are standing at it: the
		/// bench's own throughput, the width of the saw-pit rather than a rule about visits.
		/// <para>
		/// This was <c>MaxRefinedPerPass</c> and was per-resolve, which meant a grand build was
		/// gated on how many times the founder walked through the gate rather than on how long
		/// the yard ran. Under Addendum 8 clause 1 the yard runs through an absence, so the
		/// ceiling has to be denominated in the same unit the work is: a rate. A crew big enough
		/// to beat it is a real answer to a big commission; the day is still the day.
		/// </para>
		/// </summary>
		public const int MaxRefinedPerDay = 8;

		/// <summary>Whether a material is one a yard makes rather than one the ground gives up.
		/// </summary>
		public static bool IsRefined(KingdomMaterial Material)
		{
			return Material == KingdomMaterial.ShapedTimber || Material == KingdomMaterial.ShapedStone || Material == KingdomMaterial.WorkedMetal;
		}

		/// <summary>The yard that makes a refined material. False for anything raw.</summary>
		public static bool TryYardFor(KingdomMaterial Refined, out KingdomYard Yard)
		{
			Yard = KingdomYard.Sawyer;
			for (int i = 0; i < YardCount; i++)
			{
				if (YardMakes[i] == Refined)
				{
					Yard = (KingdomYard)i;
					return true;
				}
			}
			return false;
		}

		/// <summary>What a yard makes. <see cref="KingdomMaterial.ShapedTimber"/> for a value
		/// outside the enum, which no caller reads, because they all check the bool first.</summary>
		public static KingdomMaterial MadeAt(KingdomYard Yard)
		{
			int index = (int)Yard;
			return (index < 0 || index >= YardCount) ? KingdomMaterial.ShapedTimber : YardMakes[index];
		}

		/// <summary>The registry key of a yard, or empty for a value outside the enum.</summary>
		public static string YardKey(KingdomYard Yard)
		{
			int index = (int)Yard;
			return (index < 0 || index >= YardCount) ? "" : YardKeys[index];
		}

		/// <summary>The player-facing name of a yard, or empty for a value outside the enum.
		/// </summary>
		public static string YardName(KingdomYard Yard)
		{
			int index = (int)Yard;
			return (index < 0 || index >= YardCount) ? "" : YardNames[index];
		}

		/// <summary>
		/// Reads a <c>Refines</c> attribute. Accepts the yard's own key (<c>mason</c>) and the
		/// refined material's key (<c>shapedstone</c>, and its spaced spelling), because an author
		/// writing "what this building makes" and an author writing "what kind of yard this is"
		/// are both saying the same thing and neither should have to look up which spelling we
		/// wanted.
		/// </summary>
		/// <param name="Key">Text to read. Null, empty, a raw material, and an unknown word all
		/// fail.</param>
		/// <param name="Yard">Set on success.</param>
		public static bool TryParseYard(string Key, out KingdomYard Yard)
		{
			Yard = KingdomYard.Sawyer;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			string trimmed = Key.Trim().ToLowerInvariant();
			for (int i = 0; i < YardCount; i++)
			{
				if (YardKeys[i] == trimmed)
				{
					Yard = (KingdomYard)i;
					return true;
				}
			}
			if (TryParseMaterial(trimmed, out var material) && IsRefined(material))
			{
				return TryYardFor(material, out Yard);
			}
			return false;
		}

		/// <summary>
		/// Which raw material a yard would reach for out of the stock it can see, and how many
		/// refined units that stock could yield. A yard with less than <see cref="RawPerRefined"/>
		/// of everything it eats has nothing to work on, which is a thing it says out loud rather
		/// than a pass that quietly does nothing (STANDARDS 7b).
		/// </summary>
		/// <param name="Yard">Which yard.</param>
		/// <param name="Stock">What the stockpiles hold. Null reads as empty.</param>
		/// <param name="Raw">Set on success to the stock it would eat.</param>
		/// <returns>Refined units that stock covers, or zero when there is nothing to work.</returns>
		public static int RefinableFrom(KingdomYard Yard, KingdomMaterialTally Stock, out KingdomMaterial Raw)
		{
			Raw = KingdomMaterial.Timber;
			int index = (int)Yard;
			if (index < 0 || index >= YardCount || Stock == null)
			{
				return 0;
			}
			KingdomMaterial[] eats = YardEats[index];
			for (int i = 0; i < eats.Length; i++)
			{
				int units = Stock.Get(eats[i]) / RawPerRefined;
				if (units > 0)
				{
					Raw = eats[i];
					return units;
				}
			}
			return 0;
		}

		/// <summary>
		/// Refined units a crew finishes in the days since it last worked: the effort those hands
		/// put in, divided by what one unit costs, held to <see cref="MaxRefinedPerDay"/> for
		/// every day of the stretch and to what the raw stock covers. Zero hands make nothing,
		/// which is the idle case and is said once by the caller rather than being a silent
		/// nothing.
		/// <para>
		/// The throughput ceiling is a RATE now, so a yard that ran for thirty days may finish
		/// thirty days of work and a yard the founder walked past thirty times in one afternoon
		/// still finishes none. That is the whole of the change: what a grand build waits on is
		/// the yard running, never the founder arriving.
		/// </para>
		/// </summary>
		/// <param name="Crew">Settlers actually standing in the yard this pass.</param>
		/// <param name="Days">Days since the yard last worked, from
		/// <c>KingdomRules.ElapsedDays</c>, uncapped.</param>
		/// <param name="Capability">Who those settlers are, as a percentage
		/// (<see cref="CrewCapability"/>). 100 is an ordinary pair of hands.</param>
		/// <param name="RefinableUnits">What the raw stock covers, from
		/// <see cref="RefinableFrom"/>.</param>
		public static int RefinedThisPass(int Crew, int Days, int Capability, int RefinableUnits)
		{
			if (Crew <= 0 || Days <= 0 || RefinableUnits <= 0)
			{
				return 0;
			}
			long capability = (Capability > 0) ? Capability : 0;
			// Widened all the way through: Days is the raw calendar now, and the effort of a big
			// crew over a long stretch leaves int behind long before the stock or the rate do.
			long effort = (long)Crew * Days * EffortPerHandPerDay * capability / 100L;
			long units = effort / RefineEffortPerUnit;
			long ceiling = (long)MaxRefinedPerDay * Days;
			if (units > ceiling)
			{
				units = ceiling;
			}
			return (units > RefinableUnits) ? RefinableUnits : (int)units;
		}

		/// <summary>Why a yard is or is not shaping anything for the days it was just handed.
		/// <see cref="YardStall.Working"/> is the only verdict that produces.</summary>
		public enum YardStall
		{
			/// <summary>A crew is standing there and there is stock to work.</summary>
			Working,

			/// <summary>Nobody is at the bench. The days are still spent -- an empty yard does not
			/// owe its labour to whoever staffs it next -- and they buy nothing.</summary>
			Unstaffed,

			/// <summary>A crew is there and the stockpiles are empty.</summary>
			NoStock
		}

		/// <summary>
		/// Which of the two ways a yard can stand idle this is, if either.
		/// <para>
		/// Split out from the caller so the ORDER of the two gates is a thing a test can hold:
		/// staffing is asked first, because "nobody is here" is the truer answer than "there is
		/// nothing to work" when both are true, and the founder can only act on one of them at a
		/// time.
		/// </para>
		/// </summary>
		/// <param name="Staffed">Whether the staffing pass drew any crew for it.</param>
		/// <param name="Crew">Hands the pass actually put there, after effectiveness.</param>
		/// <param name="RefinableUnits">What the raw stock covers
		/// (<see cref="RefinableFrom"/>).</param>
		public static YardStall AssessYard(bool Staffed, int Crew, int RefinableUnits)
		{
			if (!Staffed || Crew <= 0)
			{
				return YardStall.Unstaffed;
			}
			if (RefinableUnits <= 0)
			{
				return YardStall.NoStock;
			}
			return YardStall.Working;
		}

		/// <summary>
		/// What a stalled yard says, once, where the founder will see it (STANDARDS 7b). Null for
		/// <see cref="YardStall.Working"/>, which is the caller's signal to unsay whatever it
		/// said last.
		/// <para>
		/// Both stalls name the yard and the city, because the settlement-wide idle-works line
		/// reports a COUNT and never says which bench it was &mdash; and "three works stand idle"
		/// is not a thing a founder can act on.
		/// </para>
		/// </summary>
		public static string YardStallLine(YardStall Stall, KingdomYard Yard, string SeatName)
		{
			string yard = YardName(Yard);
			string place = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			switch (Stall)
			{
			case YardStall.Unstaffed:
				return "The " + yard + " of " + place + " stands with nobody at the bench. Nothing is being shaped there.";
			case YardStall.NoStock:
				return "The " + yard + " of " + place + " stands over an empty bench. There is nothing in the stockpiles for it to work.";
			default:
				return null;
			}
		}

		/// <summary>Raw loads a run of refining eats. Always exactly what it made, times
		/// <see cref="RawPerRefined"/>: nothing is refined out of nothing.</summary>
		public static int RawSpentFor(int RefinedUnits)
		{
			return (RefinedUnits > 0) ? (RefinedUnits * RawPerRefined) : 0;
		}

	}
}
