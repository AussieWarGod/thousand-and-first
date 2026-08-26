using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// A bounded ring of the most recent receipts, plus the session worst per lane — which is what
	/// LIVING-CITY-ARCHITECTURE &sect;6.5 appends to <c>kingdom:dump</c>.
	/// <para>
	/// A ring rather than a list, for the same reason the told-log is one: a season of receipts and
	/// a day of them must differ in what is remembered and never in what is held.
	/// </para>
	/// </summary>
	internal sealed class KingdomComputeJournalRing : IKingdomComputeJournal
	{
		internal const int Capacity = 32;

		private readonly KingdomPerfReceipt[] entries;

		private readonly KingdomPerfReceipt[] worst;

		private readonly bool[] worstSeen;

		private int count;

		private int cursor;

		internal KingdomComputeJournalRing()
		{
			entries = new KingdomPerfReceipt[Capacity];
			worst = new KingdomPerfReceipt[KingdomBudgetRules.LaneCount];
			worstSeen = new bool[KingdomBudgetRules.LaneCount];
			count = 0;
			cursor = 0;
		}

		internal int Count
		{
			get { return count; }
		}

		public void Record(KingdomPerfReceipt receipt)
		{
			entries[cursor] = receipt;
			cursor = (cursor + 1) % Capacity;
			if (count < Capacity)
			{
				count++;
			}
			int lane = (int)receipt.Lane;
			if (lane < 0 || lane >= worst.Length)
			{
				return;
			}
			if (!worstSeen[lane] || receipt.Microseconds > worst[lane].Microseconds)
			{
				worst[lane] = receipt;
				worstSeen[lane] = true;
			}
		}

		/// <summary>The ring, oldest first.</summary>
		internal bool TryGet(int ordinalFromOldest, out KingdomPerfReceipt receipt)
		{
			receipt = default(KingdomPerfReceipt);
			if (ordinalFromOldest < 0 || ordinalFromOldest >= count)
			{
				return false;
			}
			int oldest = (count < Capacity) ? 0 : cursor;
			receipt = entries[(oldest + ordinalFromOldest) % Capacity];
			return true;
		}

		internal bool TryWorst(KingdomBudgetLane lane, out KingdomPerfReceipt receipt)
		{
			receipt = default(KingdomPerfReceipt);
			int index = (int)lane;
			if (index < 0 || index >= worst.Length || !worstSeen[index])
			{
				return false;
			}
			receipt = worst[index];
			return true;
		}
	}
}
