using System;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>Exact surface-zone geometry and clock bounds; no engine writes or guessed destinations.</summary>
	internal static class KingdomScenarioTravelRules
	{
		internal const int MaxSteps = 240;
		internal const int WaitTurns = 1200;
		internal const int DrainTurns = 39;
		internal const int CivicEnvelope = 252;

		internal static bool Zone(string Id, out string World, out int WX, out int WY,
			out int X, out int Y, out int Depth)
		{
			World = null; WX = WY = X = Y = Depth = -1;
			string[] parts = (Id ?? "").Split('.');
			if (parts.Length != 6 || parts[0].Length == 0) return false;
			World = parts[0];
			return Number(parts[1], out WX) && Number(parts[2], out WY)
				&& Number(parts[3], out X) && X <= 2 && Number(parts[4], out Y) && Y <= 2
				&& Number(parts[5], out Depth) && Depth == 10;
		}

		private static bool Number(string Text, out int Value)
			=> int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value) && Value >= 0;

		internal static bool DifferentParasang(string Home, string Other)
		{
			return Zone(Home, out string world, out int wx, out int wy, out _, out _, out int depth)
				&& Zone(Other, out string other, out int ox, out int oy, out _, out _, out int od)
				&& world == other && depth == od && (wx != ox || wy != oy);
		}

		internal static bool Step(string Before, int BX, int BY, string After, int AX, int AY, bool West)
		{
			if (BX < 0 || BX >= 80 || AX < 0 || AX >= 80 || BY < 0 || BY >= 25 || AY != BY) return false;
			if (!Zone(Before, out string world, out int wx, out int wy, out int x, out int y, out int depth)
				|| !Zone(After, out string other, out int ox, out int oy, out int nx, out int ny, out int od)
				|| world != other || wy != oy || y != ny || depth != od) return false;
			if (West ? BX > 0 : BX < 79)
				return Before == After && AX == BX + (West ? -1 : 1);
			long beforeZone = (long)wx * 3 + x, afterZone = (long)ox * 3 + nx;
			return afterZone == beforeZone + (West ? -1 : 1) && AX == (West ? 79 : 0);
		}

		internal static bool Clock(long Before, long After, long Now)
			=> Before >= 0 && After >= Before && After <= Now;

		internal static bool Budget(int Thirds, int Heavy)
			=> Thirds >= 0 && Thirds <= 24 && Heavy >= 0 && Heavy <= 4;
		internal static bool Schedule(long Before, int Ordinal, long After, int NextOrdinal)
			=> Before >= 0 && Ordinal >= 0 && After >= Before && NextOrdinal >= Ordinal;
		internal static bool Drained(long Began, long ZeroAt, int Owed)
			=> Began >= 0 && ZeroAt >= Began && ZeroAt - Began <= DrainTurns && Owed == 0;

		// Action opportunities and a real render yield can observe completion after the
		// requested wait. This is not the deadline: Drained still requires zero within 39.
		internal static bool DrainObservationReady(long Arrived, long Observed)
			=> Arrived >= 0 && Observed >= Arrived && Observed - Arrived >= DrainTurns;

		internal static bool TryPhysicalDemand(bool Measured, int ContainerThirds, int MisplacedBodies,
			out int Thirds)
		{
			Thirds = -1;
			if (!Measured || ContainerThirds < 0 || MisplacedBodies < 0) return false;
			long total = ContainerThirds + (long)MisplacedBodies *
				ThousandAndFirst.Simulation.City.KingdomCatchUpRules.WeightThirds(
					ThousandAndFirst.Simulation.City.KingdomUnitWeight.Heavy);
			if (total > 936) return false;
			Thirds = (int)total;
			return true;
		}

		internal static bool SemanticPauseReady(bool Active, long Started, string Bound, long Completed,
			long Required, long Published, string Requested)
			=> !Active || (Started > 0 && Required > 0 && Completed >= 0 && !string.IsNullOrEmpty(Bound)
				&& ThousandAndFirst.Simulation.City.KingdomSemanticClockRules.ReceiptVerdict(
					Active, Started, Bound, Completed, Required, Published, Requested)
					== ThousandAndFirst.Simulation.City.KingdomSemanticPassReceiptVerdict.Start);
	}
}
