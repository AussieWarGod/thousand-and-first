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

		/// <summary>Walking legs. The rite ground is a camp open only to the south (see
		/// Architecture/KingdomArchitectures-CivicFaith.xml, civic-heartbasin-s0), so a founder on
		/// the surveyed heart ground first leaves it by real southward steps, walks the parasang on
		/// one row, and re-enters northward. Vertical steps are lawful only in Egress and Ingress.</summary>
		internal enum Leg { Egress, Travel, Ingress, Done }

		internal static bool Vertical(Leg Current) => Current == Leg.Egress || Current == Leg.Ingress;

		/// <summary>Southward rows owed from (X,Y) inside the rect to the first row below it; zero
		/// outside the rect. A rect touching the zone's last row leaves no egress row and refuses.</summary>
		internal static bool TryEgress(int X, int Y, int X1, int Y1, int X2, int Y2, out int Steps)
		{
			Steps = 0;
			if (X < 0 || X >= 80 || Y < 0 || Y >= 25 || X1 < 0 || Y1 < 0 || X2 >= 80 || Y2 >= 25
				|| X1 > X2 || Y1 > Y2) return false;
			if (X < X1 || X > X2 || Y < Y1 || Y > Y2) return true;
			if (Y2 + 1 >= 25) return false;
			Steps = Y2 - Y + 1;
			return true;
		}

		/// <summary>The phase table: outbound is egress then travel; returning is travel then ingress.
		/// Horizontal steps begin only once the egress is complete; ingress begins only once the
		/// return route equals the outbound route, and ends when every egress row is walked back.</summary>
		internal static bool TryLeg(bool Outbound, int Egress, int EgressDone, int OutSteps, int BackSteps,
			int IngressDone, out Leg Current)
		{
			Current = Leg.Done;
			if (Egress < 0 || Egress >= 25 || EgressDone < 0 || EgressDone > Egress || IngressDone < 0
				|| IngressDone > Egress || OutSteps < 0 || BackSteps < 0 || BackSteps > OutSteps) return false;
			if (Outbound)
			{
				if (BackSteps != 0 || IngressDone != 0 || (OutSteps > 0 && EgressDone < Egress)) return false;
				Current = EgressDone < Egress ? Leg.Egress : Leg.Travel;
				return true;
			}
			if (EgressDone != Egress) return false;
			if (BackSteps < OutSteps) { if (IngressDone != 0) return false; Current = Leg.Travel; return true; }
			Current = IngressDone < Egress ? Leg.Ingress : Leg.Done;
			return true;
		}

		/// <summary>One real step under a leg: the parasang walk keeps the same-row rule; an egress or
		/// ingress step is exactly one cell south or north inside the same zone.</summary>
		internal static bool Step(string Before, int BX, int BY, string After, int AX, int AY, bool West, Leg Current)
		{
			if (Current == Leg.Travel) return Step(Before, BX, BY, After, AX, AY, West);
			if (!Vertical(Current) || Before != After || AX != BX || BX < 0 || BX >= 80
				|| BY < 0 || BY >= 25 || AY < 0 || AY >= 25
				|| !Zone(Before, out _, out _, out _, out _, out _, out _)) return false;
			return AY == BY + (Current == Leg.Egress ? 1 : -1);
		}

		internal static bool Clock(long Before, long After, long Now)
			=> Before >= 0 && After >= Before && After <= Now;

		// The engine completes an advance on the next player action opportunity, so a wait can
		// observe one extra completed turn beyond the request (docs/DEVELOPMENT.md). Return is
		// ready at the requested wait or exactly one turn past it; never more.
		internal static bool ReturnReady(long Elapsed, long Required)
			=> Required >= 0 && Elapsed >= Required && Elapsed <= Required + 1;

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
			bool Blocked, out int Thirds)
		{
			Thirds = -1;
			if (!Measured || Blocked || ContainerThirds < 0 || MisplacedBodies < 0) return false;
			long total = ContainerThirds + (long)MisplacedBodies *
				ThousandAndFirst.Simulation.City.KingdomCatchUpRules.WeightThirds(
					ThousandAndFirst.Simulation.City.KingdomUnitWeight.Heavy);
			if (total > 936) return false;
			Thirds = (int)total;
			return true;
		}

		internal static bool ContainerOnlyAdmission(int Population, int ResidentRows, int CitizenBodies)
			=> Population == 0 && ResidentRows == 0 && CitizenBodies == 0;

		internal static bool TryObserveZero(long FirstHome, long PreviousZero, long Now, int PhysicalThirds,
			bool BookSettled, out long Zero)
		{
			Zero = -1;
			if (FirstHome < 0 || Now < FirstHome || PreviousZero < -1 || PreviousZero > Now
				|| (PreviousZero >= 0 && PreviousZero < FirstHome)
				|| PhysicalThirds < 0 || PhysicalThirds > 936) return false;
			if (!BookSettled || PhysicalThirds != 0) return true;
			Zero = PreviousZero < 0 ? Now : PreviousZero;
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
