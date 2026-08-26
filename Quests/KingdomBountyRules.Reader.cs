using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		// ==================================================================================
		// Who reads it, and what they think of it
		// ==================================================================================

		/// <summary>
		/// A settler's own ordinal for the ceremony's taste and trait draws, folded from their
		/// name so the same settler always carries the same tastes for as long as they are called
		/// that.
		/// <para>
		/// The top bit is always set, and that is the whole safety argument: every other ceremony
		/// draw keys its ordinal on <c>The.Game.TimeTicks</c>, which is a signed tick count that
		/// would need something on the order of 7.6 quadrillion in-game days to reach 2^63. A
		/// person-keyed draw therefore cannot land on a tick-keyed one, so a settler's tastes can
		/// never be an accidental copy of a notable's.
		/// </para>
		/// </summary>
		/// <param name="Name">The settler's roster name. Null or empty folds to a stable ordinal
		/// of its own rather than throwing.</param>
		public static ulong PersonOrdinal(string Name)
		{
			// FNV-1a, 64-bit: written out rather than taken from string.GetHashCode, which is
			// randomised per process in .NET and would give one settler different tastes on every
			// launch.
			ulong hash = 14695981039346656037uL;
			if (!string.IsNullOrEmpty(Name))
			{
				for (int i = 0; i < Name.Length; i++)
				{
					char c = Name[i];
					hash ^= (byte)(c & 0xFF);
					hash *= 1099511628211uL;
					hash ^= (byte)((c >> 8) & 0xFF);
					hash *= 1099511628211uL;
				}
			}
			return hash | 0x8000000000000000uL;
		}

		/// <summary>How eager for paid work the settler's drawn pair of traits leaves them.</summary>
		public const int AppetiteEager = 1;

		/// <summary>How reluctant the other third of pairs leaves them.</summary>
		public const int AppetiteReluctant = -1;

		/// <summary>
		/// Reads a settler's appetite for posted work off <i>which</i> virtue and flaw they drew
		/// rather than off what those lines say.
		/// <para>
		/// Deliberate: the ceremony owns the vocabulary and may grow it, and a table here keyed on
		/// what each line means would go quietly wrong the day a ninth virtue is written. A
		/// function of the pair stays total however long the arrays get, and the prose the founder
		/// reads still comes from the ceremony's own text.
		/// </para>
		/// </summary>
		/// <param name="VirtueIndex">Index the ceremony drew. Negative reads as zero.</param>
		/// <param name="FlawIndex">Index the ceremony drew. Negative reads as zero.</param>
		/// <returns><see cref="AppetiteEager"/>, 0, or <see cref="AppetiteReluctant"/>.</returns>
		public static int TraitAppetite(int VirtueIndex, int FlawIndex)
		{
			int virtueIndex = (VirtueIndex > 0) ? VirtueIndex : 0;
			int flawIndex = (FlawIndex > 0) ? FlawIndex : 0;
			switch ((virtueIndex + flawIndex) % 3)
			{
			case 1:
				return AppetiteEager;
			case 2:
				return AppetiteReluctant;
			default:
				return 0;
			}
		}

		/// <summary>Chance in 100 that anybody reads a standing notice on a given attended pass,
		/// before anyone decides whether to take it.</summary>
		public const int ReadBaseChance = 20;

		/// <summary>Added to the read chance per dram promised.</summary>
		public const int ReadChancePerDram = 2;

		/// <summary>Ceiling on the read chance: a notice is never certain to be looked at, however
		/// rich, because the settlement has its own day to get through.</summary>
		public const int ReadChanceCeiling = 90;

		/// <summary>
		/// Whether the price is loud enough to pull somebody over to the notice board at all.
		/// Depends on the price and nothing else &mdash; who reads it is drawn separately, and what
		/// they think of it is judged in <see cref="TakeChancePercent"/>.
		/// </summary>
		/// <param name="Price">Drams promised. Clamped before use.</param>
		public static int ReadChancePercent(int Price)
		{
			int chance = ReadBaseChance + (ClampPrice(Price) * ReadChancePerDram);
			return (chance > ReadChanceCeiling) ? ReadChanceCeiling : chance;
		}

		/// <summary>Base chance in 100 that a reader takes each task, before anything about them
		/// is considered. In enum order: clearing is honest work, carrying is easy, a whole season
		/// on one work is a commitment, and walking out past the claim is the one that asks
		/// something.</summary>
		public static readonly int[] TakeBaseChance = new int[TaskCount] { 45, 60, 30, 25 };

		/// <summary>Added when the task's family is one the reader stated a taste for.</summary>
		public const int TakeTasteBonus = 20;

		/// <summary>Added when the reader is the settlement's notable &mdash; the longest-served
		/// settler, who holds its one office.</summary>
		public const int TakeNotableBonus = 10;

		/// <summary>Added, or taken away, per point of <see cref="TraitAppetite"/>.</summary>
		public const int TakeAppetiteWeight = 12;

		/// <summary>Added per dram promised.</summary>
		public const int TakeChancePerDram = 1;

		/// <summary>Floor on the take chance: no notice is impossible to take, because a refusal
		/// that can never be anything else is a stall wearing a settler's face.</summary>
		public const int TakeChanceFloor = 5;

		/// <summary>Ceiling on the take chance: refusal is always on the table.</summary>
		public const int TakeChanceCeiling = 95;

		/// <summary>
		/// Chance in 100 that the settler who read the notice takes it. Everything that shades it
		/// is something the founder can see and act on: what they are offering, who is on the
		/// roster, and what those people have said they care about.
		/// </summary>
		/// <param name="Task">The task posted.</param>
		/// <param name="Price">Drams promised. Clamped before use.</param>
		/// <param name="Notable">True when the reader is the settlement's office holder.</param>
		/// <param name="TasteMatched">True when the task's family is one of the reader's tastes.</param>
		/// <param name="Appetite">The reader's <see cref="TraitAppetite"/>.</param>
		public static int TakeChancePercent(BountyTask Task, int Price, bool Notable, bool TasteMatched, int Appetite)
		{
			int index = (int)Task;
			int chance = ((index >= 0 && index < TakeBaseChance.Length) ? TakeBaseChance[index] : TakeBaseChance[0])
				+ (ClampPrice(Price) * TakeChancePerDram)
				+ (TasteMatched ? TakeTasteBonus : 0)
				+ (Notable ? TakeNotableBonus : 0)
				+ (Appetite * TakeAppetiteWeight);
			if (chance < TakeChanceFloor)
			{
				return TakeChanceFloor;
			}
			return (chance > TakeChanceCeiling) ? TakeChanceCeiling : chance;
		}

	}
}
