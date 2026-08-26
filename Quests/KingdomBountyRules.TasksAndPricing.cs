using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		// ==================================================================================
		// The tasks
		// ==================================================================================

		/// <summary>Number of values in <see cref="BountyTask"/>. Sized against the enum by
		/// <c>KingdomBountyRulesTests</c>, which is what stops a task being added here and
		/// forgotten in the tables below.</summary>
		public const int TaskCount = 4;

		/// <summary>Stable keys, in enum order &mdash; what a log line or a save-facing string
		/// writes.</summary>
		public static readonly string[] TaskKeys = new string[TaskCount] { "clearance", "fetch", "manning", "scouting" };

		/// <summary>Player-facing names, in enum order. Lowercase, in the game's register.</summary>
		public static readonly string[] TaskNames = new string[TaskCount]
		{
			"clear the staked ground",
			"carry the marked pile in",
			"man an idle work for a season",
			"walk the frontier edge"
		};

		/// <summary>The taste family each task belongs to, as
		/// <see cref="KingdomCeremonyRules.TasteCategories"/> names them. Matching a settler's
		/// stated taste is what makes them likelier to take the notice.</summary>
		public static readonly string[] TaskTasteCategories = new string[TaskCount] { "craft", "storage", "power", "defense" };

		/// <summary>The task's key, or the clearance key for a value outside the enum.</summary>
		public static string TaskKey(BountyTask Task)
		{
			int index = (int)Task;
			return (index >= 0 && index < TaskKeys.Length) ? TaskKeys[index] : TaskKeys[0];
		}

		/// <summary>The task's player-facing name, or the clearance name for a value outside the
		/// enum.</summary>
		public static string TaskName(BountyTask Task)
		{
			int index = (int)Task;
			return (index >= 0 && index < TaskNames.Length) ? TaskNames[index] : TaskNames[0];
		}

		/// <summary>
		/// Index into <see cref="KingdomCeremonyRules.TasteCategories"/> for a task's family,
		/// found rather than hardcoded, so reordering the ceremony's ten families cannot silently
		/// point a task at the wrong taste.
		/// </summary>
		/// <returns>The index, or -1 when the ceremony does not carry that family &mdash; which
		/// reads downstream as "no settler can ever match this task", never as index zero.</returns>
		public static int TasteIndexFor(BountyTask Task)
		{
			int index = (int)Task;
			if (index < 0 || index >= TaskTasteCategories.Length)
			{
				return -1;
			}
			string wanted = TaskTasteCategories[index];
			for (int i = 0; i < KingdomCeremonyRules.TasteCategories.Length; i++)
			{
				if (string.Equals(KingdomCeremonyRules.TasteCategories[i], wanted, System.StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		// ==================================================================================
		// The price
		// ==================================================================================

		/// <summary>The least a notice may promise. A notice for nothing is not a notice.</summary>
		public const int MinPrice = 1;

		/// <summary>The most a notice may promise. A founder with a full cistern can still only
		/// move one settlement's worth of opinion; past this the price stops buying enthusiasm and
		/// starts buying nothing at all.</summary>
		public const int MaxPrice = 40;

		/// <summary>Notices that may stand at one heart at once.</summary>
		public const int MaxNotices = 3;

		/// <summary>Folds any number into a payable price.</summary>
		public static int ClampPrice(int Drams)
		{
			if (Drams < MinPrice)
			{
				return MinPrice;
			}
			return (Drams > MaxPrice) ? MaxPrice : Drams;
		}

		/// <summary>
		/// What the notice is worth posting at, given how much work it names: the founder's
		/// starting point in the price picker, never a floor and never a ceiling.
		/// </summary>
		/// <param name="Task">The task.</param>
		/// <param name="Magnitude">Task-specific size &mdash; cells for a clearance, units for a
		/// fetch, works for a manning, zero for a scouting. Negative reads as zero.</param>
		public static int SuggestedPrice(BountyTask Task, int Magnitude)
		{
			int size = (Magnitude > 0) ? Magnitude : 0;
			switch (Task)
			{
			case BountyTask.Clearance:
				return ClampPrice(3 + size / 4);
			case BountyTask.Fetch:
				return ClampPrice(2 + size / 3);
			case BountyTask.Manning:
				return ClampPrice(8);
			case BountyTask.Scouting:
				return ClampPrice(6);
			default:
				return ClampPrice(3);
			}
		}

	}
}
