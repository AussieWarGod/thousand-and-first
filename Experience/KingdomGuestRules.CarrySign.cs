using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestRules
	{
		// ==================================================================================
		// The carry-sign
		// ==================================================================================

		/// <summary>Days a haul takes even between adjacent ground &mdash; packing a pile onto
		/// porters' backs and setting out is never instant.</summary>
		public const int CarrySignBaseDays = 2;

		/// <summary>Additional days per zone-step of Chebyshev distance between the marked ground
		/// and the nearest ground the settlement holds.</summary>
		public const int CarrySignDaysPerZoneStep = 1;

		/// <summary>
		/// Chebyshev distance between two zones in the same world, three axes at once: the two
		/// grid axes <c>KingdomRules.CoordsAdjacent</c> already reads, and depth, because moving
		/// between strata is exactly as real a distance for porters carrying a load as moving
		/// across the surface.
		/// </summary>
		public static int ZoneGridDistance(int GX1, int GY1, int Z1, int GX2, int GY2, int Z2)
		{
			int dx = (GX1 > GX2) ? (GX1 - GX2) : (GX2 - GX1);
			int dy = (GY1 > GY2) ? (GY1 - GY2) : (GY2 - GY1);
			int dz = (Z1 > Z2) ? (Z1 - Z2) : (Z2 - Z1);
			int m = (dx > dy) ? dx : dy;
			return (m > dz) ? m : dz;
		}

		/// <summary>Whole days a haul over <paramref name="ZoneDistance"/> zone-steps takes.</summary>
		public static int HaulDays(int ZoneDistance)
		{
			if (ZoneDistance < 0)
			{
				ZoneDistance = 0;
			}
			return CarrySignBaseDays + ZoneDistance * CarrySignDaysPerZoneStep;
		}

		public static long HaulDueTick(long PlantedTick, int Days)
		{
			return PlantedTick + (long)Days * KingdomRules.TicksPerDay;
		}

		public static bool ShouldResolveHaul(long TimeTicks, long DueTick)
		{
			return TimeTicks >= DueTick;
		}

		/// <summary>
		/// Whether a due haul must remain in escrow. A warning nobody has answered and raiders
		/// still physically standing on the destination ground are both witnessed threats; neither
		/// is authority to invent a loss on a road the player did not see. The exact haul waits and
		/// is delivered on the first later settlement pass that proves both are absent.
		/// </summary>
		public static bool HaulWaitsForSafety(bool RaidActive, bool RaidersPresent)
		{
			return RaidActive || RaidersPresent;
		}

		public enum PlantVerdict
		{
			Planted,
			NotFounded,
			NothingToCarry,
			NoRoad,
			AlreadyInFlight
		}

		/// <summary>
		/// Judges whether planting a carry-sign right now is allowed. Checked in the order a
		/// founder would hit the refusals: you must have somewhere to carry to before anything
		/// else matters, a haul already on the road blocks a second one (mirrors
		/// <c>KingdomManifestRules.JudgeManifest</c>'s one-in-flight rule exactly), then whether
		/// there is a road there at all, then whether the ground under the sign holds anything
		/// worth the trip.
		/// </summary>
		public static PlantVerdict AssessPlant(bool Founded, bool AlreadyInFlight, bool HasRoad, int ManifestTotal)
		{
			if (!Founded)
			{
				return PlantVerdict.NotFounded;
			}
			if (AlreadyInFlight)
			{
				return PlantVerdict.AlreadyInFlight;
			}
			if (!HasRoad)
			{
				return PlantVerdict.NoRoad;
			}
			if (ManifestTotal <= 0)
			{
				return PlantVerdict.NothingToCarry;
			}
			return PlantVerdict.Planted;
		}

		public static string PlantRefusal(PlantVerdict Verdict)
		{
			switch (Verdict)
			{
				case PlantVerdict.NotFounded:
					return "There is no settlement yet for porters to carry anything home to.";
				case PlantVerdict.AlreadyInFlight:
					return "A carry-sign is already planted and porters are already on the road. Only one load travels at a time.";
				case PlantVerdict.NoRoad:
					return "This ground is too far from anything the settlement holds. Porters cannot find the road from here.";
				case PlantVerdict.NothingToCarry:
					return "There is nothing here the settlement's stockpiles would know what to do with.";
				default:
					return "";
			}
		}

		/// <summary>Builds a single-value confirmation prompt naming exactly what the sign will
		/// take. <paramref name="ManifestDescription"/> is <c>KingdomMaterialTally.Describe()</c>'s
		/// output &mdash; consent before cost: the founder sees the whole manifest before anything
		/// is swept.</summary>
		public static string PlantConfirm(string ManifestDescription, int Days)
		{
			return "Plant the carry-sign here? It will take " + ManifestDescription + " with it, and porters will need "
				+ Days + ((Days == 1) ? " day" : " days") + " to carry it home. The pile will be gone the moment the sign is planted — the sign is the designation.";
		}

		public static string PlantedChronicleLine(string SettlementName, string ManifestDescription, int Days)
		{
			return "a carry-sign was planted out on the road, marking " + ManifestDescription + " bound for " + SettlementName + ", " + Days + ((Days == 1) ? " day" : " days") + " out";
		}

		public static string PlantedMessage(int Days)
		{
			return "{{G|The carry-sign is planted.}} Porters will need " + Days + ((Days == 1) ? " day" : " days") + " to bring the load home.";
		}

		public static string DeliveredChronicleLine(string SettlementName, string ManifestDescription)
		{
			return "porters reached " + SettlementName + " carrying " + ManifestDescription + " that a carry-sign had marked out on the road";
		}

		public static string DeliveredLedgerNote(string ManifestDescription)
		{
			return "{{G|Porters arrived carrying " + ManifestDescription + " that a carry-sign had marked out on the road.}}";
		}

	}
}
