using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 2. When a slide begins, and when it has arrived.
		// ==================================================================================

		/// <summary>
		/// How far above its level a settlement may stand before it begins to settle back, as a
		/// percentage of the level.
		/// <para>
		/// The band exists so the settlement breathes. Arrivals push the population up and the
		/// slide pulls it down; without a band the two would trade a settler back and forth
		/// forever and 7b would have to announce it every time. A fifth is wide enough that
		/// ordinary growth never trips it and narrow enough that a city held up by hauling still
		/// settles: at a level of forty-two the band ends at fifty, and fifty-one people is a
		/// slide.
		/// </para>
		/// </summary>
		public const int StartMarginPercent = 20;

		/// <summary>The highest population a settlement may hold at this level without beginning
		/// to settle back. Always at least one above the level, so the band never vanishes at the
		/// small end where a fifth rounds to nothing.</summary>
		public static int SlideBeginsAbove(int Level)
		{
			int level = (Level < 0) ? 0 : Level;
			int margin = level * StartMarginPercent / 100;
			return level + ((margin < 1) ? 1 : margin);
		}

		/// <summary>Whether this settlement is standing far enough above its level to be
		/// subsiding. Strictly above the band: at the band's own edge it holds.</summary>
		public static bool IsSubsiding(int Population, int Level)
		{
			return Population > SlideBeginsAbove(Level);
		}

		/// <summary>Whether the slide has arrived. Arrival is at the level itself, not at the
		/// band's edge: a settlement that began settling settles all the way, which is what makes
		/// "a hundred days and a thousand days end at the same honest level" true.</summary>
		public static bool HasArrived(int Population, int Level)
		{
			return Population <= Level;
		}

		// ==================================================================================
		// 3. The stage ladder, both ways.
		// ==================================================================================

		/// <summary>
		/// How far under a rung's own thresholds a settlement may fall before it loses the rung,
		/// as a percentage. The same fifth the slide's band uses, and for the same reason: the
		/// stage may not flap at a boundary a single arrival or a single departure crosses.
		/// </summary>
		public const int StageFallMarginPercent = 20;

		/// <summary>A reading with the benefit of the doubt applied &mdash; what a settlement one
		/// settler or one cask under its rung is treated as having.</summary>
		public static int Forgiven(int Reading)
		{
			if (Reading <= 0)
			{
				return 0;
			}
			return (int)((long)Reading * 100L / (100L - StageFallMarginPercent));
		}

		/// <summary>
		/// The stage a settlement is at, rising and falling.
		/// <para>
		/// Rising is exactly what shipped: <c>KingdomRules.StageFor</c>'s own population and
		/// storage thresholds, straight up. Hauling can still carry a settlement to City &mdash;
		/// the pillar promises that a city held up by your own hauling <i>settles back</i>, not
		/// that it could never be raised.
		/// </para>
		/// <para>
		/// Falling is one rung per reckoning, and only on a clear shortfall: both readings get
		/// <see cref="Forgiven"/> before they are asked, so a Town holds its rung down to twenty
		/// settlers and loses it at nineteen. One rung at a time because a City that empties has a
		/// story with four chapters in it, and telling all four at once is telling none.
		/// </para>
		/// </summary>
		/// <param name="Current">The stage the settlement holds now.</param>
		/// <param name="Population">Its people.</param>
		/// <param name="StorageCapacity">Its dedicated stores' capacity, as
		/// <c>KingdomRules.StageFor</c> reads it.</param>
		public static GrowthStage StageWithHysteresis(GrowthStage Current, int Population, int StorageCapacity)
		{
			GrowthStage rising = KingdomRules.StageFor(Population, StorageCapacity);
			if (rising > Current)
			{
				return rising;
			}
			if (Current <= GrowthStage.Camp)
			{
				return GrowthStage.Camp;
			}
			// The floor is Camp's OWN equilibrium, so a settlement standing at the floor is a
			// camp whatever its cisterns still measure. Without this clause the fall margin holds
			// the smallest rung one settler under its own threshold, and a collapsed city ends its
			// slide as a four-person steading - which is the one outcome the pillar names in so
			// many words ("to Camp if that is all that stands").
			if (Population <= KingdomCatalogueRules.FloorLevel && rising == GrowthStage.Camp)
			{
				return Current - 1;
			}
			if (KingdomRules.StageFor(Forgiven(Population), Forgiven(StorageCapacity)) >= Current)
			{
				return Current;
			}
			return Current - 1;
		}

		/// <summary>
		/// Where the ladder leaves a settlement whose population has already finished moving:
		/// every rung the new figures give away, not merely the first.
		/// <para>
		/// <see cref="StageWithHysteresis"/> falls one rung per reckoning because that is the
		/// pace of a slide being lived through. This is the settling-up afterwards, for a caller
		/// that has just executed a whole trajectory in one pass and needs the stage the people
		/// actually left behind. Bounded by the number of rungs there are, so it cannot spin.
		/// </para>
		/// </summary>
		public static GrowthStage SettledStage(GrowthStage From, int Population, int StorageCapacity)
		{
			GrowthStage stage = From;
			for (int i = 0; i <= (int)GrowthStage.City; i++)
			{
				GrowthStage next = StageWithHysteresis(stage, Population, StorageCapacity);
				if (next >= stage)
				{
					return next;
				}
				stage = next;
			}
			return stage;
		}

	}
}
