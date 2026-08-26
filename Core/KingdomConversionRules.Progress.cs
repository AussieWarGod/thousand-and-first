using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomConversionRules
	{

		/// <summary>
		/// One settler's progress after a pull of <paramref name="Points"/> toward
		/// <paramref name="TowardCreed"/>.
		/// <para>
		/// <b>A settler pulled two ways is pulled nowhere.</b> When the pull names a different
		/// creed than the one already working on them, it does not replace it and does not start a
		/// second tally: it takes the same number of points back off the first. A citizen who
		/// sleeps in a Barathrumite house and eats at a Templar table converts to neither, and
		/// when the two cancel out entirely the slot is simply free again &mdash; the remainder is
		/// discarded rather than flipped, because winning a tug of war does not happen in the pass
		/// you win it.
		/// </para>
		/// </summary>
		/// <param name="Current">Progress now. <c>default</c> and
		/// <see cref="ConversionProgress.None"/> both read as nothing pulling.</param>
		/// <param name="TowardCreed">The creed pulling this pass. Null or empty changes
		/// nothing.</param>
		/// <param name="Points">Points the pull is worth. Non-positive changes nothing.</param>
		public static ConversionProgress Advance(ConversionProgress Current, string TowardCreed, int Points)
		{
			if (string.IsNullOrEmpty(TowardCreed) || Points <= 0)
			{
				return Current;
			}
			if (Current.Creed == null || Current.Shared <= 0)
			{
				return new ConversionProgress(TowardCreed, Points);
			}
			if (Current.Creed == TowardCreed)
			{
				return new ConversionProgress(TowardCreed, Current.Shared + Points);
			}
			int left = Current.Shared - Points;
			return (left > 0) ? new ConversionProgress(Current.Creed, left) : ConversionProgress.None;
		}

		/// <summary>
		/// One settler's progress after <paramref name="Days"/> of cohabitation at
		/// <paramref name="PerDay"/> a day, held at the road's end.
		/// <para>
		/// The brink's Rule 1, expressed where the accrual happens: nothing banks past
		/// <see cref="SharedLivingForConversion"/>, so a founder away a thousand days and a
		/// founder away two hundred come home to a settler standing in exactly the same place.
		/// Discarding the overflow rather than remembering it is the point &mdash; a banked
		/// overflow would be a debt the founder could not see and could not pay, and it would make
		/// the counter-pull that arrests this brink arithmetically impossible.
		/// </para>
		/// </summary>
		/// <param name="Current">Progress now.</param>
		/// <param name="TowardCreed">The creed the household is pulling toward.</param>
		/// <param name="PerDay">From <see cref="SharedLivingPerDay"/>.</param>
		/// <param name="Days">Cohabited days in the stretch. Non-positive changes nothing.</param>
		public static ConversionProgress AdvanceOverDays(ConversionProgress Current, string TowardCreed, int PerDay, int Days)
		{
			if (string.IsNullOrEmpty(TowardCreed) || PerDay <= 0 || Days <= 0)
			{
				return Current;
			}
			// Long before the multiply, not after: a hundred thousand days at three a day is fine
			// as a long and meaningless as an int, and the hold discards it either way.
			long points = (long)PerDay * Days;
			int capped = (points > SharedLivingForConversion) ? SharedLivingForConversion : (int)points;
			ConversionProgress next = Advance(Current, TowardCreed, capped);
			if (next.Creed != TowardCreed)
			{
				// A counter-pull. The points came OFF somebody else's road, and there is no brink
				// on that side of the arithmetic to hold anything at.
				return next;
			}
			return new ConversionProgress(TowardCreed, KingdomBrinkRules.HoldAtBrink(next.Shared, SharedLivingForConversion));
		}

		/// <summary>
		/// The shared-living figure that stands at the end of the Nth road walked, which is what
		/// the draw is keyed on.
		/// <para>
		/// A settler holds at <see cref="SharedLivingForConversion"/> and never above it, so the
		/// road they are ON can no longer be read off their progress the way it used to be. It is
		/// counted instead &mdash; the shell keeps a roads-walked tally per settler &mdash; and
		/// this restates that count in the units <see cref="Milestone"/> already speaks, so the
		/// first road still draws on ordinal one exactly as it did before the rework and no
		/// pending answer is re-rolled.
		/// </para>
		/// </summary>
		/// <param name="RoadsWalked">Roads this settler has already walked to the end, converted
		/// or refused. Negative reads as none.</param>
		public static int RoadEnd(int RoadsWalked)
		{
			int walked = (RoadsWalked < 0) ? 0 : RoadsWalked;
			return SharedLivingForConversion * (walked + 1);
		}

		/// <summary>Whether this settler has reached the end of the road and the brink is owed.
		/// False for anyone short of it.</summary>
		public static bool AtMilestone(int Shared)
		{
			return Shared >= SharedLivingForConversion;
		}

		/// <summary>
		/// Which milestone this much shared living stands at &mdash; the kernel ordinal the draw
		/// is keyed on, so every pass between one milestone and the next asks the identical
		/// question and receives the identical answer. A road that answered no is not asked again
		/// until the settler has walked a whole further <see cref="SharedLivingForConversion"/>,
		/// which since the rework is counted rather than banked: the shell hands this
		/// <see cref="RoadEnd"/> of the roads-walked tally.
		/// </summary>
		public static ulong Milestone(int Shared)
		{
			return (Shared < SharedLivingForConversion) ? 0uL : (ulong)(Shared / SharedLivingForConversion);
		}

	}
}
