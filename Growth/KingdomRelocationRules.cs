using System;

namespace ThousandAndFirst
{
	/// <summary>Pure limits, labour, geometry, and transitions for whole-lot relocation.</summary>
	public static partial class KingdomRelocationRules
	{
		public const int Schema = 1;
		public const int MaxMoves = 32;
		public const int MaxRowsPerMove = 4096;
		public const int MaxClearRowsPerMove = 4096;
		public const int MaxStakeIds = 4;
		public const int MaxIdChars = 128;
		public const int MaxKeyChars = 256;
		public const int MaxNameChars = 512;
		public const int MaxFailureChars = 512;
		public const int MaxSnapshotChars = 262144;
		public const int MaxReceiptChars = 4194304;
		public const int MaxCoordinate = 65535;
		public const int MinimumDays = 2;
		public const int BaseCrewTicksPerCell = 18;
		public const int CrewTicksPerPart = 24;

		/// <summary>Labour only: footprint + exact carried fabric, scaled by hardest material.</summary>
		public static long LabourTicks(int Area, int PartCount, int HardnessPercent,
			long TicksPerDay)
		{
			if (Area < 1 || PartCount < 1 || TicksPerDay < 1L) return 0L;
			int hardness = HardnessPercent < 100 ? 100
				: (HardnessPercent > 300 ? 300 : HardnessPercent);
			long units = (long)Area * BaseCrewTicksPerCell
				+ (long)PartCount * CrewTicksPerPart;
			long scaled = units > long.MaxValue / hardness
				? long.MaxValue : units * hardness / 100L;
			long floor = TicksPerDay > long.MaxValue / MinimumDays
				? long.MaxValue : TicksPerDay * MinimumDays;
			return scaled < floor ? floor : scaled;
		}

		public static KingdomRelocationRect Shift(KingdomRelocationRect Rect,
			int DeltaX, int DeltaY)
		{
			return new KingdomRelocationRect(Rect.X1 + DeltaX, Rect.Y1 + DeltaY,
				Rect.X2 + DeltaX, Rect.Y2 + DeltaY);
		}

		public static bool SameRect(KingdomRelocationRect A, KingdomRelocationRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}

		public static bool Overlaps(KingdomRelocationRect A, KingdomRelocationRect B)
		{
			return A.X1 <= B.X2 && A.X2 >= B.X1 && A.Y1 <= B.Y2 && A.Y2 >= B.Y1;
		}

		public static int Days(long Ticks, long TicksPerDay)
		{
			if (Ticks <= 0L || TicksPerDay <= 0L) return 0;
			long days = Ticks / TicksPerDay + (Ticks % TicksPerDay == 0L ? 0L : 1L);
			return days > int.MaxValue ? int.MaxValue : (int)days;
		}

		public static long TotalTicks(KingdomRelocationReceipt Receipt)
		{
			if (Receipt == null || Receipt.Moves == null) return 0L;
			long total = 0L;
			for (int i = 0; i < Receipt.Moves.Count; i++)
			{
				long add = Receipt.Moves[i] == null ? 0L : Receipt.Moves[i].RequiredTicks;
				if (add < 0L || total > long.MaxValue - add) return long.MaxValue;
				total += add;
			}
			return total;
		}
	}
}
