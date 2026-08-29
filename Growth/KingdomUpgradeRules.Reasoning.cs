using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// The one sentence a blocked improvement owes the founder, or null for a verdict that
		/// correctly says nothing. Every reason names the thing that would lift it, because a
		/// stall the player cannot act on is the failure this whole file exists to prevent.
		/// </summary>
		/// <param name="Verdict">What <see cref="Assess"/> found.</param>
		/// <param name="PredecessorName">What is standing there now.</param>
		/// <param name="SuccessorName">What it would become. May be null when the successor
		/// could not be resolved at all.</param>
		/// <param name="StageNeeded">Stage the improvement waits for.</param>
		/// <param name="CrewNeeded">Hands the improvement needs free.</param>
		/// <param name="Shortfall">Drams the stores are short, from
		/// <see cref="Shortfall(int, int, int)"/>.</param>
		/// <returns>A player-facing line, or null when the verdict is not the blocked kind.
		/// </returns>
		public static string ReasonLine(UpgradeVerdict Verdict, string PredecessorName,
			string SuccessorName, GrowthStage StageNeeded, int CrewNeeded, int Shortfall,
			string CraftDetail = null, bool KnowledgeMissing = false)
		{
			string predecessor = string.IsNullOrEmpty(PredecessorName) ? "work" : PredecessorName;
			string successor = string.IsNullOrEmpty(SuccessorName) ? "something better" : SuccessorName;
			switch (Verdict)
			{
			case UpgradeVerdict.SuccessorUnknown:
				return "The " + predecessor + " was meant to grow into something this settlement has no design for.";
			case UpgradeVerdict.HeldOnThisGround:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but this ground is to be left as it is.";
			case UpgradeVerdict.HeldByFounder:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but it is to be left as it is.";
			case UpgradeVerdict.StageTooLow:
				return "The " + predecessor + " could be raised into " + Article(successor) + " once this is a " + StageWord(StageNeeded) + ".";
			case UpgradeVerdict.NotEnoughHands:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but no "
					+ ((CrewNeeded == 1) ? "one is" : (CrewNeeded + " settlers are")) + " free for the work.";
			case UpgradeVerdict.WouldSpill:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but what it holds would have nowhere to go.";
			case UpgradeVerdict.NotEnoughWater:
				return "The " + predecessor + " could be raised into " + Article(successor) + " for "
					+ Shortfall + " more " + ((Shortfall == 1) ? "dram" : "drams") + " than the stores can spare.";
			case UpgradeVerdict.WorksElsewhere:
				return "The " + predecessor + " could be raised into " + Article(successor) + " once the settlement is done with the work it has in hand.";
			case UpgradeVerdict.CraftNotMet:
				if (string.IsNullOrEmpty(CraftDetail))
					return "The " + predecessor + " could be raised into " + Article(successor)
						+ " once this settlement's own craft reaches it.";
				return "The " + predecessor + " could be raised into " + Article(successor)
					+ (KnowledgeMissing ? " once its keepers know {{C|" : " once its craft reaches {{C|")
					+ CraftDetail + "}}.";
			case UpgradeVerdict.NotEnoughMaterial:
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but the stockpiles are short of what it is built of.";
			case UpgradeVerdict.NoGroundToGrow:
				// The general line. It is true of both causes -- a tier that wants more of the plot
				// than was staked, and a yard trade standing where the larger building must go --
				// and the engine half replaces it with the particular one whenever it has real
				// ground to read (KingdomPlots.GrowRefused). A blocked verdict never says nothing,
				// even when the only half that can see the cause is the half that cannot be tested
				// without a running game.
				return "The " + predecessor + " could be raised into " + Article(successor) + ", but the ground it was staked on has no room for what it would become.";
			default:
				return null;
			}
		}

		/// <summary>
		/// The line the founder reads when the settlement begins bettering one of its own works.
		/// </summary>
		public static string BegunLine(string PredecessorName, string SuccessorName, int Cost)
		{
			string predecessor = string.IsNullOrEmpty(PredecessorName) ? "work" : PredecessorName;
			string successor = string.IsNullOrEmpty(SuccessorName) ? "something better" : SuccessorName;
			return "The " + predecessor + " is being raised into " + Article(successor) + ". It costs "
				+ Cost + " " + ((Cost == 1) ? "dram" : "drams") + " from the stores.";
		}

		/// <summary>
		/// The notice a settlement gives the first time any of its works could be bettered, so
		/// no founder ever finds a thing changed without having been told the settlement does
		/// this and where to stop it.
		/// </summary>
		/// <param name="SeatName">What the settlement is called.</param>
		public static string FirstNoticeLine(string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			return seat + " has grown enough to better what it already built, and will do so out of "
				+ "what the stores can spare. (Charter: your works, and what they become.)";
		}

		/// <summary>
		/// English indefinite article for a display name, matching how the mod's prose reads
		/// object names. Kept here rather than borrowed from the engine so the rules file stays
		/// engine-free and this sentence is testable.
		/// </summary>
		public static string Article(string Name)
		{
			if (string.IsNullOrEmpty(Name))
			{
				return "something";
			}
			char first = Name[0];
			if (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u'
				|| first == 'A' || first == 'E' || first == 'I' || first == 'O' || first == 'U')
			{
				return "an " + Name;
			}
			return "a " + Name;
		}

		/// <summary>
		/// Which of several designs sharing one blueprint a standing work counts as, when the work
		/// carries no stamped key and the only evidence left is the blueprint it was built from.
		/// <para>
		/// A design that declares a chain wins, because it is the only candidate that can answer
		/// the question being asked; among several that do, the first registered wins, which is the
		/// same load-order rule the registry resolves every other collision by. With no chain
		/// anywhere the first candidate is returned regardless: the key is still wanted for prose
		/// and for the cost arithmetic, and none of the candidates could grow into anything.
		/// </para>
		/// </summary>
		/// <param name="Chained">For each candidate design, in registry order, whether it declares
		/// an upgrade chain.</param>
		/// <returns>An index into that array, or -1 when no design matches the blueprint at all.
		/// </returns>
		public static int ChooseDesignIndex(bool[] Chained)
		{
			if (Chained == null || Chained.Length == 0)
			{
				return -1;
			}
			for (int i = 0; i < Chained.Length; i++)
			{
				if (Chained[i])
				{
					return i;
				}
			}
			return 0;
		}

	}
}
