using System;
using XRL.UI;
using XRL.World;
using XRL.World.ZoneParts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Attaches, restamps and revokes the claimed-ground light
	/// (<see cref="XRL.World.ZoneParts.KingdomClaimedGroundLight"/>) on the zone the founder just
	/// walked into. It is reached from the one semantic activation guard, so ground that stopped
	/// being the realm's &mdash; secession, exile, a claim let go &mdash; loses its light on the
	/// next visit rather than on a sweep. The current zone only: no claim is ever thawed to be lit.
	/// </summary>
	public static class KingdomClaimedGround
	{
		public const string OptionId = "r_TAF_OptionClaimedGroundLight";

		/// <summary>A convenience the author asked for, so it ships on. Read per frame as well as
		/// at attachment: switching it off darkens the zone before the part is next removed.</summary>
		public static bool Enabled => Options.GetOption(OptionId, "Yes") != "No";

		/// <summary>One activation of one zone the seat claims. Attaches the light when the option
		/// stands and the realm may still take work, and revokes it in every other case: a claim
		/// the seat no longer holds, ground two settlements both answer for, the option switched
		/// off, and a realm the master gate has stopped. A standing light is a standing effect,
		/// not queued work, so a stopped realm loses it here rather than keeping it until the
		/// option or the claim happens to change.</summary>
		internal static void ReconcileZone(KingdomSystem System, Zone Zone)
		{
			if (System == null || Zone == null) return;
			if (!Enabled || !KingdomMaster.NewWorkAllowed(System)
				|| System.ClaimedZones == null
				|| !System.ClaimedZones.Contains(Zone.ZoneID))
			{
				RemoveZone(Zone);
				return;
			}
			// Ground two settlements both answer for is not exactly anybody's, and the light is a
			// statement of whose ground this is. Ambiguity revokes rather than guesses.
			string settlementId = System.SettlementIdForOwnedZone(Zone.ZoneID);
			if (string.IsNullOrEmpty(settlementId))
			{
				RemoveZone(Zone);
				return;
			}
			KingdomClaimedGroundLight light = Zone.GetPart<KingdomClaimedGroundLight>();
			if (light == null)
			{
				light = new KingdomClaimedGroundLight();
				Zone.AddPart(light);
			}
			light.SettlementId = settlementId;
			// The floor the city holds is remembered as walked, so the minimap reads as a settlement
			// instead of a corridor. Walls and what stands on them still wait on the founder's own
			// line of sight, and this is one-way: a claim let go stops new reveals, it does not
			// unremember ground.
			Zone.ExploreAll();
		}

		/// <summary>Ground that is not the realm's, or is no longer lit by choice: take the part
		/// off. Nothing else on the zone is touched, and a zone without one is already correct.</summary>
		internal static void RemoveZone(Zone Zone)
		{
			KingdomClaimedGroundLight light = Zone?.GetPart<KingdomClaimedGroundLight>();
			if (light != null) Zone.RemovePart(light);
		}
	}
}
