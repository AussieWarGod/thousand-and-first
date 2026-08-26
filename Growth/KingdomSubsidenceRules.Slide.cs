using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 4. The slide.
		// ==================================================================================

		/// <summary>
		/// World days between one step of a slide and the next. Coarse on purpose: the settlement
		/// is not metering out a settler an hour, it is losing a household every few days, and a
		/// founder who walks in mid-slide should be able to count what has gone.
		/// </summary>
		public const int StepDays = 4;

		/// <summary>
		/// How many settlers one step takes, by what the settlement is. A city sheds faster than a
		/// steading because there are more people in it with nothing holding them: the step is the
		/// rung's ordinal plus one, so a City loses five where a Camp loses one, and the slide
		/// slows of its own accord as the place gets smaller.
		/// </summary>
		public static int SettlersPerStep(GrowthStage Stage)
		{
			int index = (int)Stage;
			if (index < 0)
			{
				index = 0;
			}
			if (index > (int)GrowthStage.City)
			{
				index = (int)GrowthStage.City;
			}
			return index + 1;
		}

		/// <summary>Hard stop on the step loop. A slide can never need more steps than there are
		/// settlers to lose plus rungs to fall, and this is comfortably past both; it exists so a
		/// nonsense elapsed can never spin.</summary>
		public const int MaxSteps = KingdomRules.MaxPopulation + 8;

		/// <summary>One place along a slide where the settlement stopped being one thing and
		/// became another. These are what the chronicle samples: the whole trajectory is a
		/// hundred small departures, and the story in it is the four rungs.</summary>
		public struct Breakpoint
		{
			/// <summary>Days into the slide, so the caller can date it against the day it is being
			/// told about. Never a tick, and never re-anchored to anything.</summary>
			public int Day;

			public GrowthStage From;

			public GrowthStage To;

			/// <summary>People left standing when the rung went.</summary>
			public int Population;

			public Breakpoint(int Day, GrowthStage From, GrowthStage To, int Population)
			{
				this.Day = Day;
				this.From = From;
				this.To = To;
				this.Population = Population;
			}
		}

		/// <summary>Where a stretch of world time left a settlement, and what happened on the way.
		/// </summary>
		public struct Trajectory
		{
			/// <summary>People at the end of it.</summary>
			public int Population;

			/// <summary>What the settlement is at the end of it.</summary>
			public GrowthStage Stage;

			/// <summary>People who left. Zero for a settlement that was not subsiding.</summary>
			public int Departed;

			/// <summary>Steps actually taken. The caller advances its checkpoint by exactly this
			/// many <see cref="StepDays"/> and keeps the remainder, so a founder cannot buy a free
			/// day by stepping in and out of the zone.</summary>
			public int Steps;

			/// <summary>Whether the slide reached the level within the elapsed time. A caller that
			/// arrives unsays its 7b line; one that did not, does not.</summary>
			public bool Arrived;

			/// <summary>Rungs lost, in order, with the day of the slide each fell on. Never null
			/// once <see cref="Slide"/> has returned.</summary>
			public List<Breakpoint> Breakpoints;
		}

		/// <summary>
		/// Runs a settlement's slide forward over a stretch of world time.
		/// <para>
		/// Pure: the same arguments always give the same trajectory, which is what makes a reload
		/// reproduce a collapse rather than reroll it. Nothing here reads a clock &mdash; the
		/// elapsed days are handed in, computed by the caller from
		/// <c>KingdomRules.ElapsedDays</c>, uncapped.
		/// </para>
		/// <para>
		/// The level is recomputed at every step rather than taken once, because the stage falls
		/// during the slide and the water bill falls with it. That is what makes this a
		/// convergence and not a countdown: a City with cisterns for a Town stops when it becomes
		/// a Town.
		/// </para>
		/// </summary>
		/// <param name="Population">People now.</param>
		/// <param name="Stage">What the settlement is now.</param>
		/// <param name="StorageCapacity">Dedicated storage capacity, for the stage ladder.</param>
		/// <param name="Supports">Everything the finished works carry, summed.</param>
		/// <param name="ElapsedDays">Whole world days since the last reckoning. Uncapped.</param>
		/// <param name="AlreadySliding">Whether this settlement was already settling back when the
		/// stretch began. This is the hysteresis, and it is why the two thresholds differ: a slide
		/// STARTS only above the band (<see cref="IsSubsiding"/>) and then CONTINUES until the
		/// level itself (<see cref="HasArrived"/>), so a settlement cannot begin and arrest inside
		/// the same handful of settlers over and over. The caller remembers the flag in the same
		/// place 7b's announcement lives, because they are the same fact.</param>
		/// <param name="Shade">What the settlement's named notable is worth to it, from
		/// <c>KingdomCeremonyRules.NotableShade</c>. Carried through every step for the same
		/// reason the stage is: the level a slide converges on must be the level the founder was
		/// told, or a settlement would be announced at one number and settled to another.</param>
		/// <returns>A trajectory that begins where it was handed and never goes below the level.
		/// A settlement inside its band comes back untouched, with zero steps.</returns>
		public static Trajectory Slide(int Population, GrowthStage Stage, int StorageCapacity,
			KingdomCatalogueRules.SupportTally Supports, int ElapsedDays, bool AlreadySliding, int Shade = 0)
		{
			Trajectory trajectory = default(Trajectory);
			trajectory.Population = Population;
			trajectory.Stage = Stage;
			trajectory.Breakpoints = new List<Breakpoint>();
			int level = SupportedLevel(Supports, Stage, Shade);
			trajectory.Arrived = HasArrived(Population, level);
			if (ElapsedDays < StepDays || (!AlreadySliding && !IsSubsiding(Population, level)))
			{
				return trajectory;
			}
			int available = ElapsedDays / StepDays;
			for (int step = 0; step < available && step < MaxSteps; step++)
			{
				level = SupportedLevel(Supports, trajectory.Stage, Shade);
				if (HasArrived(trajectory.Population, level))
				{
					break;
				}
				int take = SettlersPerStep(trajectory.Stage);
				int room = trajectory.Population - level;
				if (take > room)
				{
					take = room;
				}
				trajectory.Population -= take;
				trajectory.Departed += take;
				trajectory.Steps = step + 1;
				GrowthStage next = StageWithHysteresis(trajectory.Stage, trajectory.Population, StorageCapacity);
				if (next < trajectory.Stage)
				{
					trajectory.Breakpoints.Add(new Breakpoint(
						trajectory.Steps * StepDays, trajectory.Stage, next, trajectory.Population));
					trajectory.Stage = next;
				}
			}
			trajectory.Arrived = HasArrived(trajectory.Population, SupportedLevel(Supports, trajectory.Stage, Shade));
			return trajectory;
		}

	}
}
