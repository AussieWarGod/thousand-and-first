using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Hubris subsides (VISION.md: <i>"a city stands on what actually feeds it"</i>).
	/// <para>
	/// Three things live here and nothing else does. <b>The level</b>: what a settlement's finished
	/// works carry between them, denominated against the stage the settlement has become, which is
	/// the first thing anywhere to consume <c>KingdomCatalogueRules.Equilibrium</c>. <b>The
	/// ladder</b>: the stage rule, hysteretic both ways, replacing the ratchet that only ever
	/// climbed. <b>The slide</b>: how a settlement standing above its own level converges back
	/// down to it as world time passes, in coarse per-stage steps, and where along the way that
	/// story has breakpoints worth writing down.
	/// </para>
	/// <para>
	/// <b>What subsidence is a punishment for.</b> Building past your works, and only that. It is
	/// not a punishment for absence &mdash; Addendum 8 clause 1 says the settlement lives whether
	/// the founder is there or not, so the slide runs on world time and would run identically
	/// under the founder's nose. It is not a punishment for building: a settlement whose works
	/// carry its people never subsides however much it has raised. What it costs is the gap
	/// between the two, and the gap is closed by raising works or by losing the people the works
	/// were never feeding, whichever the founder chooses first.
	/// </para>
	/// <para>
	/// <b>The floor is Camp's own equilibrium</b> (<c>KingdomCatalogueRules.FloorLevel</c>), not a
	/// special case bolted underneath. Nobody subsides out of existence, and a settlement that
	/// arrives at the floor is a camp, whatever its cisterns still measure.
	/// </para>
	/// <para>
	/// <b>Nothing here destroys anything.</b> The protection law (STANDARDS 7) forbids kingdom
	/// systems from consuming, moving, or deleting what the player placed. Subsidence ruins
	/// through wear &mdash; <see cref="RuinIncrement"/> against
	/// <c>KingdomMaterialRules.MaxWearPercent</c>, which a work never passes and which every
	/// mending undoes &mdash; and through people leaving, which goes through the one departure
	/// path the settlement already has. A subsided city is a damaged city standing in place, and
	/// it is put right by mending it.
	/// </para>
	/// </summary>
	public static class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 1. The level: what the works carry, at the rate this settlement drinks.
		// ==================================================================================

		/// <summary>
		/// Settlers the summed <c>water</c> support sustains at a given stage.
		/// <para>
		/// The catalogue denominates water in drams a day, which is one settler's thirst
		/// <i>at camp rates</i> and says so at the attribute. <c>KingdomRules.UpkeepDrams</c> then
		/// scales the real bill by <c>StageUpkeepPercent</c>, so the same cisterns carry fewer
		/// people the grander the place becomes: a camp lives thin and a city drinks like a city.
		/// This is the conversion between the two, and it is the cross-check nothing performed
		/// before &mdash; the catalogue and the upkeep table had never been read against each
		/// other.
		/// </para>
		/// <para>
		/// Note which way it runs when a settlement falls: a City that becomes a Town needs less
		/// water per head, so its level RISES as it subsides. That is what makes the slide
		/// converge on something rather than run to the floor every time.
		/// </para>
		/// </summary>
		/// <param name="Water">Summed <c>water</c> contribution of every finished work.</param>
		/// <param name="Stage">What the settlement is now.</param>
		public static int LevelFromWater(int Water, GrowthStage Stage)
		{
			if (Water <= 0)
			{
				return 0;
			}
			int percent = UpkeepPercent(Stage);
			return (percent <= 100) ? Water : (Water * 100 / percent);
		}

		/// <summary>What a settler costs a day at this stage, per hundred. Fails closed onto the
		/// camp rate for a stage this build does not define, which charges the least and so can
		/// never invent a shortfall out of a bad cast.</summary>
		private static int UpkeepPercent(GrowthStage Stage)
		{
			int index = (int)Stage;
			if (index < 0 || index >= KingdomRules.StageUpkeepPercent.Length)
			{
				return 100;
			}
			return KingdomRules.StageUpkeepPercent[index];
		}

		/// <summary>
		/// The population this settlement's works honestly carry: the frozen
		/// <c>KingdomCatalogueRules.Equilibrium</c>, handed a water figure converted out of drams
		/// into settlers at this stage's own rate.
		/// </summary>
		/// <param name="Supports">Every finished work's <c>Carries</c>, summed.</param>
		/// <param name="Stage">What the settlement is now.</param>
		/// <returns>Never below <c>KingdomCatalogueRules.FloorLevel</c>.</returns>
		public static int SupportedLevel(KingdomCatalogueRules.SupportTally Supports, GrowthStage Stage)
		{
			return KingdomCatalogueRules.Equilibrium(
				LevelFromWater(Supports.Water, Stage), Supports.Food, Supports.Roof, Supports.Lift);
		}

		/// <summary>
		/// Which of the three binding goods is holding the settlement where it is, asked with the
		/// water already converted &mdash; so a city whose cisterns would be ample at camp rates
		/// is correctly told that it is the water, which is the whole point of the conversion.
		/// </summary>
		/// <returns>One of <c>KingdomCatalogueRules.BindingSupports</c>. Never null.</returns>
		public static string BindingSupportFor(KingdomCatalogueRules.SupportTally Supports, GrowthStage Stage)
		{
			return KingdomCatalogueRules.BindingSupport(
				LevelFromWater(Supports.Water, Stage), Supports.Food, Supports.Roof);
		}

		/// <summary>
		/// A stored binding-support name, read back safely. Anything this build does not recognise
		/// as one of <c>KingdomCatalogueRules.BindingSupports</c> comes back null.
		/// <para>
		/// Read-side rather than a repair in <c>Normalize</c>, deliberately. The seat swap's own
		/// contract is that a field survives a round trip byte for byte
		/// (<c>SettlementSeatTests.CaptureAndRestoreCarryEveryFieldACityHolds</c>), and a
		/// <c>Normalize</c> that rewrote this string would break it for no gain: the thing worth
		/// preventing is a sentence that blames the water for a name this build cannot read, and
		/// that is prevented here, where the sentences are written.
		/// </para>
		/// </summary>
		public static string NormalizedBinding(string Stored)
		{
			if (string.IsNullOrEmpty(Stored) || !KingdomCatalogueRules.IsBindingSupport(Stored))
			{
				return null;
			}
			// Handed back as the canonical constant rather than as stored, so a name that only
			// differed in case cannot reach a switch that compares against the constants.
			for (int i = 0; i < KingdomCatalogueRules.BindingSupports.Length; i++)
			{
				string canonical = KingdomCatalogueRules.BindingSupports[i];
				if (string.Equals(Stored.Trim(), canonical, System.StringComparison.OrdinalIgnoreCase))
				{
					return canonical;
				}
			}
			return null;
		}

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
		/// <returns>A trajectory that begins where it was handed and never goes below the level.
		/// A settlement inside its band comes back untouched, with zero steps.</returns>
		public static Trajectory Slide(int Population, GrowthStage Stage, int StorageCapacity,
			KingdomCatalogueRules.SupportTally Supports, int ElapsedDays, bool AlreadySliding)
		{
			Trajectory trajectory = default(Trajectory);
			trajectory.Population = Population;
			trajectory.Stage = Stage;
			trajectory.Breakpoints = new List<Breakpoint>();
			int level = SupportedLevel(Supports, Stage);
			trajectory.Arrived = HasArrived(Population, level);
			if (ElapsedDays < StepDays || (!AlreadySliding && !IsSubsiding(Population, level)))
			{
				return trajectory;
			}
			int available = ElapsedDays / StepDays;
			for (int step = 0; step < available && step < MaxSteps; step++)
			{
				level = SupportedLevel(Supports, trajectory.Stage);
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
			trajectory.Arrived = HasArrived(trajectory.Population, SupportedLevel(Supports, trajectory.Stage));
			return trajectory;
		}

		// ==================================================================================
		// 5. Ruin. Damage, never deletion (STANDARDS 7).
		// ==================================================================================

		/// <summary>
		/// Chance one standing work is among those the WIDEST rung there is ruins &mdash; a City
		/// ceasing to be a city. Every shallower rung reaches proportionally less of the
		/// settlement (<see cref="RuinChanceFor"/>).
		/// <para>
		/// This replaced a flat allowance of two works a rung, which was the whole of Addendum
		/// 10(c)'s first complaint: a City falling all the way to Camp left eight works scuffed
		/// out of however many dozen were standing, and the rest pristine. "A place that has gone
		/// from city back to a few tents should have ruins on the plots that were previously
		/// buildings" is a statement about MOST of the plots, so the allowance had to stop being
		/// a count at all.
		/// </para>
		/// </summary>
		public const int RuinChancePercent = 50;

		/// <summary>
		/// Chance one standing work is among those THIS lost rung ruins: the rung's own scale,
		/// out of the scales there are. A City rung reaches half the settlement, a Steading rung
		/// a fifth of it, and the ladder in between is the same "the rung's ordinal plus one"
		/// shape <see cref="SettlersPerStep"/> already sheds people by.
		/// <para>
		/// <b>This is the reach rule</b> (Addendum 10(c)): each lost rung reaches the works that
		/// rung's scale supported, so a one-rung slide scuffs a corner of the settlement and a
		/// City falling all the way asks every standing work four separate times, at four
		/// narrowing chances. Nothing here bounds how MANY works a rung takes &mdash; the reach
		/// is a chance asked of every work independently, not a quota filled in survey order, so
		/// the field of ruins is a pure function of the draws and does not depend on which work
		/// happened to be raised first.
		/// </para>
		/// </summary>
		/// <param name="From">The rung being lost &mdash; what the settlement WAS. Out-of-range
		/// values clamp to the ladder rather than faulting.</param>
		public static int RuinChanceFor(GrowthStage From)
		{
			int index = (int)From;
			if (index < 0)
			{
				index = 0;
			}
			if (index > (int)GrowthStage.City)
			{
				index = (int)GrowthStage.City;
			}
			return RuinChancePercent * (index + 1) / ((int)GrowthStage.City + 1);
		}

		/// <summary>
		/// Wear one lost rung adds to a work it takes: the complement of what
		/// <c>KingdomRules.StandingPercent</c> says survives a ruined interregnum, halved and
		/// scaled to the wear ceiling.
		/// <para>
		/// Halved because a subsidence is not an interregnum. <c>StandingPercent</c> answers "how
		/// much of a settlement nobody has lived in for a generation is still up"; this is one
		/// rung of a place that is still lived in, still mends itself, and still has people in it
		/// arguing about which cistern to dig. Two or three rungs bring a work to
		/// <c>KingdomMaterialRules.MaxWearPercent</c>, which it never passes, so a city that falls
		/// all the way to Camp is derelict and legible rather than gone.
		/// </para>
		/// </summary>
		/// <param name="Roll">Adversity, 0 to 99. A high draw is a hard fall, exactly as
		/// <c>StandingPercent</c> reads it.</param>
		/// <returns>At least one, so a ruin is never a no-op that reads like one.</returns>
		public static int RuinIncrement(int Roll)
		{
			int standing = KingdomRules.StandingPercent(KingdomRules.InheritedState.Ruins, Roll);
			int increment = KingdomMaterialRules.MaxWearPercent * (100 - standing) / 200;
			return (increment < 1) ? 1 : increment;
		}

		// ----------------------------------------------------------------------------------
		// The draws. Counter-based on a key naming the settlement, the work, the channel and the
		// breakpoint's own ordinal, exactly as KingdomWearRules' three causes are: an ordinary
		// pseudorandom cursor depends on every unrelated roll since the game started, and a reload
		// must not re-roll a collapse the chronicle has already described.
		//
		// The stream grammar is folded here rather than borrowed from KingdomWearRules, whose own
		// folder is private to the "taf:wear:" prefix. Two files, two prefixes, so a log can tell
		// a work that wore out from a work that was let go.
		// ----------------------------------------------------------------------------------

		private const int SubsidenceRulesVersion = 1;

		private const uint SubsidenceDrawIndex = 0u;

		/// <summary>Fixed, all-zero seed, for the reason <c>KingdomChronicle</c> gives at length:
		/// domain separation comes entirely from the settlement id, stream, kind and ordinal baked
		/// into the key, and which shed sags is not a question that needs to be unguessable.
		/// </summary>
		private static readonly KernelSeed128 SubsidenceSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:subsidence:";

		private const string StreamSuffix = ":v1";

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// read from the kernel because that constant is the kernel's own and this file must fold
		/// to fit it, not reach into it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>Which question a draw answers. Frozen: never zero, never renumbered.</summary>
		public enum SubsidenceChannel
		{
			/// <summary>Whether one standing work is among those a lost rung ruins.</summary>
			Ruin = 1,

			/// <summary>How hard that fall was for it &mdash; the adversity
			/// <see cref="RuinIncrement"/> reads.</summary>
			Severity = 2,
		}

		/// <summary>Folds one work's own id into the frozen <c>taf:</c> semantic-id grammar. The
		/// work belongs in the stream rather than the ordinal because two works asked about at the
		/// same breakpoint must not be forced to share one answer.</summary>
		/// <param name="WorkId">The work's persistent <c>GameObject.id</c>. Null and blank yield
		/// the lane an unidentified work would draw on.</param>
		internal static string WorkStream(string WorkId)
		{
			StringBuilder builder = new StringBuilder(StreamPrefix);
			int room = KernelSemanticIdBudget - StreamPrefix.Length - StreamSuffix.Length;
			if (!string.IsNullOrEmpty(WorkId))
			{
				foreach (char c in WorkId)
				{
					if (builder.Length - StreamPrefix.Length >= room)
					{
						break;
					}
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length == StreamPrefix.Length)
			{
				builder.Append("unidentified");
			}
			builder.Append(StreamSuffix);
			return builder.ToString();
		}

		private static bool TryDraw(string SettlementId, string WorkId, SubsidenceChannel Channel, ulong Ordinal, out int Value)
		{
			Value = 0;
			if (!SemanticEventKey.TryCreate(SubsidenceRulesVersion, SettlementId, WorkStream(WorkId), (uint)Channel, Ordinal, out var key, out var _))
			{
				return false;
			}
			if (!CounterRandom.TryDrawBelow(SubsidenceSeed, key, SubsidenceDrawIndex, 100uL, out var value, out var _))
			{
				return false;
			}
			Value = (int)value;
			return true;
		}

		/// <summary>
		/// Whether one standing work is among those this lost rung ruins. False (never faulting)
		/// for a malformed settlement id, which ruins nothing and is the safe answer.
		/// <para>
		/// <b>The draw did not move when the reach did.</b> The key is the same settlement, work,
		/// channel and breakpoint ordinal it always was &mdash; what changed in Addendum 10(c) is
		/// only how much of the ladder answers yes (<see cref="RuinChanceFor"/>). So a wider rung
		/// ruins a strict SUPERSET of what a narrower rung would have ruined out of the same
		/// works, and a save whose collapse has already been chronicled draws exactly the numbers
		/// it drew before.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="WorkId">The work's persistent object id.</param>
		/// <param name="Ordinal">The breakpoint's own ordinal, so every rung asks fresh.</param>
		/// <param name="From">The rung being lost, which sets how far it reaches.</param>
		public static bool RollRuin(string SettlementId, string WorkId, ulong Ordinal, GrowthStage From)
		{
			int value;
			return TryDraw(SettlementId, WorkId, SubsidenceChannel.Ruin, Ordinal, out value) && value < RuinChanceFor(From);
		}

		/// <summary>How hard this rung's fall was for one work, as the wear it adds. Zero when the
		/// draw could not be made, which adds nothing rather than guessing.</summary>
		public static int RolledRuinIncrement(string SettlementId, string WorkId, ulong Ordinal)
		{
			int value;
			return TryDraw(SettlementId, WorkId, SubsidenceChannel.Severity, Ordinal, out value) ? RuinIncrement(value) : 0;
		}

		// ==================================================================================
		// 6. What it says (STANDARDS 7b: once, by name, and unsaid when it stops).
		// ==================================================================================

		/// <summary>The clause both registers name a subsidence departure by, handed to
		/// <c>KingdomGrowth.Emigrate</c> so the chronicle and the ledger cannot disagree about why
		/// somebody left.</summary>
		/// <param name="Binding">Which good is holding the level, from
		/// <see cref="BindingSupportFor"/>.</param>
		public static string DepartureCause(string Binding)
		{
			switch (NormalizedBinding(Binding))
			{
			case KingdomCatalogueRules.SupportWater:
				return "because the water here was never enough for this many";
			case KingdomCatalogueRules.SupportFood:
				return "because the fields here never fed this many";
			case KingdomCatalogueRules.SupportRoof:
				return "because there was never a roof here for this many";
			default:
				// No binding good named, so none is blamed. Reached only by a settlement no pass
				// has measured yet, or by a saved name from a build with a different vocabulary -
				// and in both cases inventing a cause would be worse than naming none.
				return "because the works here never carried this many";
			}
		}

		/// <summary>The once-only line that says a settlement has begun to settle back, and what
		/// is holding it where it is going.</summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="Binding">The good holding the level.</param>
		/// <param name="Level">Where it is going.</param>
		/// <param name="Population">Where it is standing now.</param>
		public static string BeganNote(string Name, string Binding, int Level, int Population)
		{
			return Name + " is settling back. There are " + Population + " here, and "
				+ KingdomCatalogueRules.LimitLine(Binding, Level)
				+ " Raise what it lacks and the slide stops where it stands.";
		}

		/// <summary>The chronicle's own telling of the same moment.</summary>
		public static string BeganChronicle(string Name, string Binding, int Level)
		{
			return "the works of " + Name + " no longer carried the people in it, and it began to settle back toward "
				+ Level + ", " + HeldBy(Binding);
		}

		/// <summary>The unsaying: the block has lifted, so 7b's flag clears and says why.</summary>
		public static string ArrestedNote(string Name, int Level, int Population)
		{
			return (Population < Level)
				? (Name + " has stopped settling. The works carry " + Level + ", and there are " + Population + " here.")
				: (Name + " has settled. The works carry " + Level + ", and that is what stands here now.");
		}

		/// <summary>The chronicle's own telling of an arrest.</summary>
		public static string ArrestedChronicle(string Name, int Level)
		{
			return Name + " stopped settling back, and stood at the " + Level + " its works honestly carry";
		}

		// --- The chronicle budget ----------------------------------------------------------
		//
		// The register keeps two hundred entries and trims the oldest. A City standing at fifty
		// people sliding to Camp's own floor of four is forty-six departures, four rungs, and up
		// to two ruined works a rung: fifty-eight lines, better than a quarter of the whole
		// record, for one event. A founder who came home to that would find the settlement's
		// entire memory replaced by the story of losing it.
		//
		// So a long slide is TOLD IN RUNGS, which is what BreakpointChronicle's own doc comment
		// already claimed it was: the rungs are always all told, first and last included, because
		// there are at most four of them and each is the place becoming a different place. What
		// is sampled is the departures underneath them - the first few by name, the last by name
		// because the last one out is the one the founder remembers, and everybody in between
		// counted in one line. Nobody vanishes from the count; what they lose is a chronicle
		// entry each, and what they were owed was the truth about the slide, which the rungs and
		// the summary tell between them.

		/// <summary>
		/// Departures of one slide that get a chronicle entry to themselves. Three: enough that a
		/// short slide reads exactly as it always did (a handful of people leaving IS the story
		/// at that size), few enough that a collapse spends three lines and not fifty.
		/// </summary>
		public const int NamedDeparturesPerSlide = 3;

		/// <summary>
		/// Whether the <paramref name="Index"/>-th departure of a slide is chronicled by name.
		/// Keeps the FIRST and the LAST always: the first is when it started going, the last is
		/// who turned the lights off, and a sample that dropped either would be a worse record
		/// than a shorter one.
		/// </summary>
		/// <param name="Index">Which departure, from zero.</param>
		/// <param name="Departed">How many are going in this slide.</param>
		public static bool TellsDeparture(int Index, int Departed)
		{
			if (Index < 0 || Departed <= 0 || Index >= Departed)
			{
				return false;
			}
			if (Departed <= NamedDeparturesPerSlide)
			{
				return true;
			}
			return Index < NamedDeparturesPerSlide - 1 || Index == Departed - 1;
		}

		/// <summary>How many of a slide's departures are chronicled by name. Never more than
		/// <see cref="NamedDeparturesPerSlide"/>, and never more than went.</summary>
		public static int NamedDepartures(int Departed)
		{
			if (Departed <= 0)
			{
				return 0;
			}
			return (Departed < NamedDeparturesPerSlide) ? Departed : NamedDeparturesPerSlide;
		}

		/// <summary>
		/// The one line that carries everybody the sample did not name. Null when the sample
		/// named them all, which is the caller's signal to say nothing.
		/// <para>
		/// Takes the named count rather than deriving it, because a slide the settlement cut
		/// short &mdash; people standing in another claimed zone, the loyal core refusing to go
		/// &mdash; loses fewer than the trajectory called for and may name fewer than the sample
		/// planned. The summary counts what actually happened, so the two numbers always add up
		/// to the departures the ledger recorded.
		/// </para>
		/// </summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="Departed">How many actually went.</param>
		/// <param name="Named">How many of those were chronicled by name.</param>
		/// <param name="Cause">The departure cause, from <see cref="DepartureCause"/>, so the
		/// summary blames exactly what the named departures blamed.</param>
		public static string SlideDepartureSummary(string Name, int Departed, int Named, string Cause)
		{
			int unnamed = Departed - ((Named > 0) ? Named : 0);
			if (unnamed <= 0)
			{
				return null;
			}
			string who = (unnamed == 1) ? "one more" : (unnamed + " more");
			return who + " went from " + Name + " over the same days, " + Cause;
		}

		// --- The ruins of one rung, told the same way --------------------------------------
		//
		// Addendum 10(c) broadened the DAMAGE and must not broaden the TELLING with it: a rung
		// that leaves eleven works the worse for it is eleven real ruins and two chronicle
		// entries. So the ruins of a rung are sampled exactly as its departures are - a couple
		// named, everybody else carried by one line that counts them and names how bad the worst
		// of them got. Nobody vanishes from the count; what they lose is an entry each.

		/// <summary>Ruined works of one rung that get a chronicle entry to themselves. One: a
		/// rung already spends an entry on itself, and the sentence that matters after it is how
		/// many went and how far, not which shed was third.</summary>
		public const int NamedRuinsPerBreakpoint = 1;

		/// <summary>Whether the <paramref name="Index"/>-th work this rung ruined is chronicled
		/// by name. The first ones, in the order the pass found them.</summary>
		/// <param name="Index">Which ruined work, from zero.</param>
		public static bool TellsRuin(int Index)
		{
			return Index >= 0 && Index < NamedRuinsPerBreakpoint;
		}

		/// <summary>The line one named ruined work gets, in the ledger and the register alike.
		/// </summary>
		/// <param name="WorkName">The work, as the founder refers to it.</param>
		/// <param name="Name">The settlement's display name.</param>
		public static string RuinedWorkLine(string WorkName, string Name)
		{
			string work = string.IsNullOrEmpty(WorkName) ? "a work" : WorkName;
			return work + " fell into disrepair as " + Name + " settled back, with nobody left who kept it";
		}

		/// <summary>
		/// The one line carrying every work of this rung the sample did not name. Null when the
		/// sample named them all, which is the caller's signal to say nothing.
		/// </summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="Ruined">Works this rung actually left the worse for it.</param>
		/// <param name="Named">How many of those were chronicled by name.</param>
		/// <param name="DeepestWear">The worst wear any of them was left standing at, for
		/// <c>KingdomMaterialRules.ConditionWord</c> to put a stage on.</param>
		public static string RuinSummary(string Name, int Ruined, int Named, int DeepestWear)
		{
			int unnamed = Ruined - ((Named > 0) ? Named : 0);
			if (unnamed <= 0)
			{
				return null;
			}
			string how = (unnamed == 1) ? "one more work" : (unnamed + " more works");
			return how + " at " + Name + " went the same way over those days, the worst of them "
				+ KingdomMaterialRules.ConditionWord(DeepestWear);
		}

		/// <summary>
		/// How many chronicle entries one slide writes, so a test can hold the budget rather than
		/// trusting the arithmetic to stay small. Named departures, the summary line if there is
		/// one, one entry per rung, and per rung its
		/// <see cref="NamedRuinsPerBreakpoint"/> named ruins plus the one line that carries the
		/// rest of them.
		/// <para>
		/// Note what is NOT in here: how many works a rung actually ruined. That is the point of
		/// the coarsening &mdash; Addendum 10(c) let a rung reach the whole settlement, and the
		/// register's share of a collapse did not move a line for it.
		/// </para>
		/// </summary>
		/// <param name="Departed">People the slide took.</param>
		/// <param name="Rungs">Rungs it fell, which is what <c>Trajectory.Breakpoints</c>
		/// holds.</param>
		public static int ChronicleEntriesFor(int Departed, int Rungs)
		{
			int named = NamedDepartures(Departed);
			int summary = (Departed > named) ? 1 : 0;
			int rungs = (Rungs > 0) ? Rungs : 0;
			return named + summary + rungs + rungs * (NamedRuinsPerBreakpoint + 1);
		}

		/// <summary>
		/// The ceiling <see cref="ChronicleEntriesFor"/> promises for any slide this build can
		/// produce, including a City falling all the way to Camp. Stated as a number rather than
		/// read off <c>KingdomChronicle.MaxEntries</c> because that constant is the chronicle's
		/// own and this file must fold to fit it, not reach into it: the register keeps two
		/// hundred, and one collapse may not spend more than a tenth of them.
		/// </summary>
		public const int ChronicleBudgetPerSlide = 20;

		/// <summary>One rung, dated against the day the founder is being told about it. This is
		/// the sample the chronicle keeps: the slide itself is a hundred small departures, and
		/// what is worth writing down is the four times the place stopped being one thing.
		/// </summary>
		/// <param name="Name">The settlement's display name.</param>
		/// <param name="From">What it was.</param>
		/// <param name="To">What it became.</param>
		/// <param name="DaysAgo">Days before now that this happened. Zero and below read as
		/// today, which is what a slide that finished this morning is.</param>
		public static string BreakpointChronicle(string Name, GrowthStage From, GrowthStage To, int DaysAgo)
		{
			string when = (DaysAgo <= 0)
				? "today"
				: ((DaysAgo == 1) ? "a day before you saw it" : (DaysAgo + " days before you saw it"));
			return Name + " was a " + From.ToString().ToLowerInvariant() + " and became a "
				+ To.ToString().ToLowerInvariant() + ", " + when;
		}

		private static string HeldBy(string Binding)
		{
			switch (NormalizedBinding(Binding))
			{
			case KingdomCatalogueRules.SupportWater:
				return "and it is the water that holds it there";
			case KingdomCatalogueRules.SupportFood:
				return "and it is the harvest that holds it there";
			case KingdomCatalogueRules.SupportRoof:
				return "and there are only so many roofs";
			default:
				return "and that is what its works carry";
			}
		}
	}
}
