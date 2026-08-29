using System;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static bool BuildingReady(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, out string Failure)
		{
			Failure = null;
			GameObject building = Context?.Building;
			if (Context == null || !Context.Owned || !Context.Seated)
				return Fail("This city does not presently hold the moot.", out Failure);
			if (!TryExactBuilding(Receipt, out GameObject exact)
				|| !ReferenceEquals(exact, building))
				return Fail("The exact moot building is missing, replaced, or moved.", out Failure);
			if (building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0)
				return Fail("The moot is under an active strike order.", out Failure);
			r_KingdomWear wear = building.GetPart<r_KingdomWear>();
			if (wear != null && wear.Wear > 0)
				return Fail("The moot is worn and must be mended before the ward can answer.", out Failure);
			if (!building.HasStat("Hitpoints") || building.baseHitpoints != Receipt.BaselineHitpoints
				|| building.hitpoints < building.baseHitpoints)
				return Fail("The moot has been damaged; repair it before asking the voices again.",
					out Failure);
			if (!KingdomReopenedExoticActivation.AssentingMootEligible(
				Context.System, KingdomZoning.Roster(Context.System)))
				return Fail("The assent node, Chavvah rite, or horizontal Moon Stair adjacency is absent.",
					out Failure);
			return true;
		}

		internal static bool TryMemberBody(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, KingdomAssentingMootRole Role, int Index,
			bool LoadZone, out GameObject Body)
		{
			Body = null;
			if (Context?.Book == null || Receipt == null) return false;
			System.Collections.Generic.List<int> ids = Role == KingdomAssentingMootRole.Assent
				? Receipt.AssentResidentIds : Receipt.ExemptResidentIds;
			System.Collections.Generic.List<string> bodies =
				Role == KingdomAssentingMootRole.Assent
					? Receipt.AssentBodyObjectIds : Receipt.ExemptBodyObjectIds;
			if (Index < 0 || Index >= ids.Count || Index >= bodies.Count) return false;
			KingdomResidentRow resident;
			if (!KingdomResidents.TryResident(Context.Book, ids[Index], out resident)
				|| resident.Standing != KingdomResidentStanding.Resident) return false;
			string zoneId;
			GameObject exact;
			if (!KingdomResidents.TryResolveBoundBody(Context.System, ids[Index], LoadZone,
				out exact, out zoneId)
				|| !string.Equals(exact.IDIfAssigned, bodies[Index], StringComparison.Ordinal)
				|| !BookOwnsZone(Context, zoneId)) return false;
			Body = exact;
			return true;
		}

		internal static int ValidAssentCount(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, bool LoadZones)
		{
			int count = 0;
			for (int i = 0; i < Receipt.AssentResidentIds.Count; i++)
				if (TryMemberBody(Context, Receipt, KingdomAssentingMootRole.Assent,
					i, LoadZones, out GameObject _)) count++;
			return count;
		}

		private static bool BookOwnsZone(KingdomAssentingMootContext Context, string ZoneId)
		{
			if (Context == null || string.IsNullOrEmpty(ZoneId)) return false;
			if (Context.Seated) return Context.System.ClaimedZones != null
				&& Context.System.ClaimedZones.Contains(ZoneId);
			return Context.Settlement?.ClaimedZones != null
				&& Context.Settlement.ClaimedZones.Contains(ZoneId);
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
