using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		// ==================================================================================
		// The prose
		// ==================================================================================

		/// <summary>The line cut into the notice itself, read off it by anyone who looks.</summary>
		/// <param name="Task">The task posted.</param>
		/// <param name="Price">Drams promised.</param>
		/// <param name="Detail">A short clause naming the particular ground, pile, or work, or
		/// null when the task names no particular thing.</param>
		public static string NoticeText(BountyTask Task, int Price, string Detail)
		{
			int price = ClampPrice(Price);
			string drams = price + ((price == 1) ? " dram" : " drams");
			string tail = string.IsNullOrEmpty(Detail) ? "" : (" " + Detail);
			switch (Task)
			{
			case BountyTask.Clearance:
				return "A notice on a stake, and a cord run round the ground it means: " + drams + " of fresh water to whoever clears it." + tail;
			case BountyTask.Fetch:
				return "A notice on a stake, and a mark cut into the pile it means: " + drams + " of fresh water to whoever carries it in." + tail;
			case BountyTask.Manning:
				return "A notice on a stake: " + drams + " of fresh water to whoever stands a work through the season and does not walk off it." + tail;
			case BountyTask.Scouting:
				return "A notice on a stake, facing out past the claim: " + drams + " of fresh water to whoever walks the edge and comes back able to say what is out there." + tail;
			default:
				return "A notice on a stake, promising " + drams + " of fresh water to whoever does what it asks." + tail;
			}
		}

		/// <summary>The chronicle's line for a notice going up. Lower-case clause, no trailing
		/// period &mdash; the chronicle supplies both.</summary>
		public static string PostedChronicle(string SeatName, BountyTask Task, int Price)
		{
			int price = ClampPrice(Price);
			return "a notice was staked at the heart of " + (string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName)
				+ ", promising " + price + ((price == 1) ? " dram" : " drams") + " to whoever would " + TaskName(Task);
		}

		/// <summary>The chronicle's line for a settler reading the notice and walking away. Named,
		/// and free: nothing is spent, nothing is held against them, and the reason given is their
		/// own drawn flaw rather than an accusation.</summary>
		public static string RefusedChronicle(string Name, BountyTask Task, int FlawIndex)
		{
			string who = string.IsNullOrEmpty(Name) ? "somebody" : Name;
			return who + " read the notice offering water to " + TaskName(Task) + " and left it standing -- " + KingdomCeremonyRules.FlawText(FlawIndex);
		}

		/// <summary>The chronicle's line for a settler taking the notice down off the stake.</summary>
		public static string TakenChronicle(string Name, BountyTask Task, int VirtueIndex, bool TasteMatched)
		{
			string who = string.IsNullOrEmpty(Name) ? "somebody" : Name;
			return who + " took the notice offering water to " + TaskName(Task)
				+ (TasteMatched ? ", which is the very thing they had said they wanted to see, and " : ", and ")
				+ KingdomCeremonyRules.VirtueText(VirtueIndex);
		}

		/// <summary>The chronicle's line for a claimed notice paid out in full.</summary>
		public static string PaidChronicle(string Name, string SeatName, BountyTask Task, int Paid)
		{
			string who = string.IsNullOrEmpty(Name) ? "whoever did it" : Name;
			return who + " did what the notice asked at " + (string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName)
				+ ", and was paid " + Paid + ((Paid == 1) ? " dram" : " drams") + " out of the stores in front of everyone";
		}

		/// <summary>The chronicle's line for work done that the stores could only part-cover. The
		/// debt is stated plainly rather than quietly written off.</summary>
		public static string OwedChronicle(string Name, string SeatName, int Paid, int Owed)
		{
			string who = string.IsNullOrEmpty(Name) ? "whoever did it" : Name;
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			if (Paid <= 0)
			{
				return who + " did what the notice asked at " + seat + ", and " + seat + " had not a dram to pay it with, and said so";
			}
			return who + " did what the notice asked at " + seat + ", and took " + Paid + ((Paid == 1) ? " dram" : " drams")
				+ " of the price, with " + Owed + " still owed and written down";
		}

		/// <summary>The ledger's line while a debt stands. Announced once, and again only if the
		/// amount changes.</summary>
		public static string OwedLedgerNote(string Name, int Owed)
		{
			return "{{r|" + (string.IsNullOrEmpty(Name) ? "Somebody" : Name) + " is still owed " + Owed
				+ ((Owed == 1) ? " dram" : " drams") + " for a notice they claimed. It will be paid the day the stores can cover it.}}";
		}

		/// <summary>The chronicle's line for the founder taking a notice down. Always free, and
		/// always remembered.</summary>
		public static string WithdrawnChronicle(string SeatName, BountyTask Task, bool Claimed, string Name)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			if (Claimed && !string.IsNullOrEmpty(Name))
			{
				return "the notice at " + seat + " was taken off its stake while " + Name + " was still at it, and nobody was made to give anything back";
			}
			return "the notice at " + seat + " offering water to " + TaskName(Task) + " was taken off its stake, unclaimed and unpaid for";
		}

		/// <summary>The scout's own report, named ground and all. Lower-case clause, no trailing
		/// period.</summary>
		public static string ScoutChronicle(string Name, string SeatName, string GroundName)
		{
			string who = string.IsNullOrEmpty(Name) ? "a scout" : Name;
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			if (string.IsNullOrEmpty(GroundName))
			{
				return who + " walked the edge of what " + seat + " holds and came back with the shape of the ground beyond it";
			}
			return who + " walked the edge of what " + seat + " holds and came back able to say what lies past it: " + GroundName;
		}

		/// <summary>The deed the settlement is known for after a frontier is walked &mdash; the
		/// same currency every other notable act is recorded in, so word of it draws settlers the
		/// ordinary way.</summary>
		public static string ScoutDeed(string SeatName)
		{
			return "the frontier " + (string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName) + " walked and mapped";
		}
	}
}
