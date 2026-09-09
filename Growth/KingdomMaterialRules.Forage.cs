using System;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		public const int BrushPerForagerPerDay = 1;
		public const int MaxForagingHands = 3;
		public const int ForageRadius = 12;
		public const int ForageCeilingUnits = 12;

		[Flags]
		public enum ForageFacts
		{
			None = 0, Plant = 1, Wall = 2, Tree = 4, Creature = 8,
			Food = 16, Owned = 32, UntakenSeed = 64, Protected = 128,
			Plot = 256, Portable = 512
		}

		/// <summary>Only a positively identified wild plant with no exclusion is admitted.</summary>
		public static bool ForageCandidate(ForageFacts Facts) => Facts == ForageFacts.Plant;

		public static int ForageHands(int Free) => Math.Max(0, Math.Min(MaxForagingHands, Free));

		/// <summary>Hard per-pass stock ceiling, including uncapped elapsed absences.</summary>
		public static int ForageUnits(int Hands, int Days, int Held)
		{
			if (Days <= 0 || Held < 0 || Held >= ForageCeilingUnits) return 0;
			long work = (long)ForageHands(Hands) * Days * BrushPerForagerPerDay;
			return (int)Math.Min(work, ForageCeilingUnits - Held);
		}

		/// <summary>Charges whole days before any competing job can return from the pass.</summary>
		public static int ForageDays(ref long Last, long Now)
		{
			int days = Last > 0 && Now > Last ? KingdomRules.ElapsedDays(Now - Last) : 0;
			Last = KingdomRules.AdvanceCheckpoint(Last, Now);
			return days;
		}

		public static long ForageDistance(int X, int Y, int RiteX, int RiteY)
			=> Math.Max(Math.Abs((long)X - RiteX), Math.Abs((long)Y - RiteY));

		/// <summary>Distance first, then the stored cell's y and x. No mutable ranking.</summary>
		public static int ForageOrder(int AX, int AY, int BX, int BY, int RiteX, int RiteY)
		{
			int order = ForageDistance(AX, AY, RiteX, RiteY)
				.CompareTo(ForageDistance(BX, BY, RiteX, RiteY));
			if (order != 0) return order;
			order = AY.CompareTo(BY);
			return order != 0 ? order : AX.CompareTo(BX);
		}

		/// <summary>Returns true only on entry to a block; observing recovery rearms it.</summary>
		public static bool ForageAnnounce(ref bool Announced, bool Blocked)
		{
			bool first = Blocked && !Announced;
			Announced = Blocked;
			return first;
		}
	}
}
