using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		// --- Crews have capability, and it is read off who they are ---------------------------

		/// <summary>Which of a settler's numbers a yard's work is done with. Sawing and dressing
		/// stone are muscle; a furnace is a machine somebody has to understand.</summary>
		public static KingdomCapability CapabilityFor(KingdomYard Yard)
		{
			return (Yard == KingdomYard.Smelter) ? KingdomCapability.Mind : KingdomCapability.Muscle;
		}

		/// <summary>
		/// The stat an ordinary person has. Vanilla's own humanoid rolls <c>14,1d3</c> on every
		/// attribute (<c>BaseHumanoid</c> in the game's own Creatures.xml), so sixteen is the
		/// middle of what walks up the road, and a crew of ordinary people works at exactly 100.
		/// </summary>
		public const int BaselineStat = 16;

		/// <summary>Percentage points one point of the relevant stat is worth.</summary>
		public const int CapabilityPerPoint = 5;

		/// <summary>Floor on capability. Nobody is useless, and a settlement that has only weak
		/// hands still gets its beams cut, slowly.</summary>
		public const int MinCapabilityPercent = 50;

		/// <summary>Ceiling on capability. The strong settler is worth having and is never worth
		/// three ordinary ones, because the yard is the bottleneck and not the arm.</summary>
		public const int MaxCapabilityPercent = 150;

		/// <summary>What one stat value is worth, as a percentage of an ordinary pair of hands.
		/// </summary>
		public static int CapabilityPercent(int Stat)
		{
			int percent = 100 + (Stat - BaselineStat) * CapabilityPerPoint;
			if (percent < MinCapabilityPercent)
			{
				return MinCapabilityPercent;
			}
			return (percent > MaxCapabilityPercent) ? MaxCapabilityPercent : percent;
		}

		/// <summary>
		/// What a crew is worth at one yard's work, read off the people themselves. The founder
		/// assigns nobody: the settlement's own hands are what they are, and a city of scribes
		/// smelts better than it saws.
		/// </summary>
		/// <param name="Yard">The work being done.</param>
		/// <param name="Strength">The crew's Strength, averaged. Zero and negative read as
		/// <see cref="BaselineStat"/>, so a caller that could not read the people gets an ordinary
		/// crew rather than a punished one.</param>
		/// <param name="Intelligence">The crew's Intelligence, averaged. Same rule.</param>
		public static int CrewCapability(KingdomYard Yard, int Strength, int Intelligence)
		{
			int stat = (CapabilityFor(Yard) == KingdomCapability.Mind) ? Intelligence : Strength;
			return CapabilityPercent((stat > 0) ? stat : BaselineStat);
		}

		/// <summary>
		/// The average of a set of stat readings, or <see cref="BaselineStat"/> when there is
		/// nothing to read. Rounded down, because a crew is only as quick as its slowest half.
		/// </summary>
		public static int AverageStat(IList<int> Values)
		{
			if (Values == null || Values.Count == 0)
			{
				return BaselineStat;
			}
			int total = 0;
			for (int i = 0; i < Values.Count; i++)
			{
				total += Values[i];
			}
			return total / Values.Count;
		}

		/// <summary>One word for a crew's quality, for the line the founder reads. Never null.
		/// </summary>
		public static string CapabilityWord(int Percent)
		{
			if (Percent >= 120)
			{
				return "deft";
			}
			if (Percent <= 80)
			{
				return "slow";
			}
			return "steady";
		}

	}
}
