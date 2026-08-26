using System.Collections.Generic;

using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the way down: remembering which ground has a shaft in it, and
	/// refusing the work that cannot be done without one.
	/// <para>
	/// The arithmetic is all in <see cref="KingdomDelveRules"/>, which never touches
	/// <c>XRL</c>. What is here is the one thing that needs the game: <b>where a finished shaft
	/// is written down.</b>
	/// </para>
	/// <para>
	/// It is written down against the ZONE, in game state, in the same idiom
	/// <c>KingdomUpgrade.GroundHeldState</c> already uses for "leave this ground as it is". That
	/// choice is load-bearing and worth stating: a shaft has to be readable from the OTHER END,
	/// by a founder standing in the dark asking why a wall will not go up, and the zone that
	/// holds the winding gear is a parasang away and very probably not in memory. Game state is
	/// always in memory. A zone string property would not be, and a field on the settlement would
	/// be a fifth list to keep in step with <c>ClaimedZones</c> for a fact that belongs to the
	/// ground rather than to the realm &mdash; two cities cannot share a parasang, so the ground
	/// is the honest owner.
	/// </para>
	/// </summary>
	public static class KingdomDelve
	{
		/// <summary>Prefixed with the head zone's id: 1 where a finished shaft goes down.
		/// The zone that HOLDS the winding gear, never the one it opens.</summary>
		public const string ShaftState = "r_TAF_Delved:";

		/// <summary>Whether a finished shaft goes down from this ground.</summary>
		/// <param name="ZoneId">The head zone's id.</param>
		public static bool ShaftStands(string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId) || The.Game == null) return false;
			// Presence of any new-format key is authoritative. Corrupt/partial/tombstoned physical
			// evidence fails closed and never falls through to a stale legacy integer.
			if (KingdomDelveLink.HasPhysicalState(ZoneId))
			{
				return KingdomDelveLink.PhysicalLinkStands(ZoneId);
			}
			// Old saves remain readable until that exact shaft is struck or restaked.
			return The.Game.GetIntGameState(ShaftState + ZoneId) == 1;
		}

		/// <summary>
		/// Writes down that the shaft raised on this ground is cut, so the rock below it can be
		/// worked from now on. Called once, from the works-completion path.
		/// </summary>
		/// <param name="ZoneId">The zone the delve stands in.</param>
		public static void RecordShaft(string ZoneId)
		{
			if (!string.IsNullOrEmpty(ZoneId) && The.Game != null)
			{
				The.Game.SetIntGameState(ShaftState + ZoneId, 1);
			}
		}

		/// <summary>
		/// Forgets the shaft when the work that was the shaft comes down. A struck delve is a
		/// filled hole: the deep goes back to being owned and unworkable.
		/// <para>
		/// Damage is deliberately not this. A holed winding house still has a hole in the floor
		/// under it, so wear costs the work its effectiveness the way it costs every other work
		/// its effectiveness (<c>KingdomWearRules</c>) and never closes the way down.
		/// </para>
		/// </summary>
		/// <param name="Key">The struck design's catalogue key. Anything that is not the delve
		/// returns without touching a thing.</param>
		/// <param name="ZoneId">The zone it stood in.</param>
		public static void OnStruck(string Key, string ZoneId)
		{
			if (KingdomDelveRules.IsDelve(Key) && !string.IsNullOrEmpty(ZoneId) && The.Game != null)
			{
				// New links are cleared only by KingdomDelveLink after both endpoints and both native
				// connections are proved absent. This legacy callback must never turn active physical
				// authority into a boolean-only success. Tombstones may mirror their cleared int.
				if (!KingdomDelveLink.HasPhysicalState(ZoneId)
					|| The.Game.GetStringGameState(KingdomDelveLink.LinkState + ZoneId, null)
						== KingdomDelveLink.Tombstone)
				{
					The.Game.SetIntGameState(ShaftState + ZoneId, 0);
				}
			}
		}

		/// <summary>
		/// Which of the settlement's own zones have a shaft going down from them, in the order the
		/// claims were made.
		/// <para>
		/// Read by probing the claims rather than by keeping a list, because game state cannot be
		/// enumerated by prefix and because a list would be a second copy of a fact the ground
		/// already holds. A realm holds at most a parasang of ground
		/// (<c>KingdomZoningRules.ZonesForStage</c>), so this is at most nine dictionary reads.
		/// </para>
		/// </summary>
		/// <param name="ClaimedZones">The settlement's own ground. Null answers empty.</param>
		public static List<string> DelvedZones(IEnumerable<string> ClaimedZones)
		{
			List<string> delved = new List<string>();
			if (ClaimedZones == null)
			{
				return delved;
			}
			foreach (string zone in ClaimedZones)
			{
				if (!delved.Contains(zone) && ShaftStands(zone))
				{
					delved.Add(zone);
				}
			}
			return delved;
		}

		/// <summary>Whether the settlement's people can get a load to and from this ground.
		/// Surface ground always answers true; rock answers true once a shaft reaches it.</summary>
		/// <param name="System">The settlement. Null or unfounded answers false.</param>
		/// <param name="ZoneId">The ground in question.</param>
		public static bool Reaches(KingdomSystem System, string ZoneId)
		{
			if (System == null || !System.Founded)
			{
				return false;
			}
			return KingdomDelveRules.Reaches(ZoneId, System.ClaimedZones, DelvedZones(System.ClaimedZones));
		}

		/// <summary>
		/// Why this ground will not take this design, when the reason is the way down. Asked
		/// before the ordinary plot gates, because "you cannot get a cart here" is a fact about
		/// the ground rather than about the building, and the founder should hear it whichever
		/// building they asked for.
		/// </summary>
		/// <param name="System">The settlement.</param>
		/// <param name="ZoneId">The ground the plan is staked on.</param>
		/// <param name="Key">The design's catalogue key. The delve itself is judged by its own
		/// rules, since a shaft is exactly the thing that may be raised over unopened rock.</param>
		/// <param name="Name">The design's display name, for the sentence.</param>
		/// <returns>Null when nothing about the way down is in the way, which is every design on
		/// the surface of every city that never went below.</returns>
		public static string Refusal(KingdomSystem System, string ZoneId, string Key, string Name)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(ZoneId))
			{
				return null;
			}
			// A refusal has to name a fix the founder can actually perform (STANDARDS 7b), and a
			// catalogue with no way down in it has no shaft to send anybody to sink. So the gate
			// is only ever as live as the data: no delve record, and the rock is worked exactly as
			// it was before this file existed. It is also how a third party turns the gate on
			// after retheming it - ship the key, and the ground starts asking for it.
			if (!KingdomData.TryGetBuilding(KingdomDelveRules.DelveKey, out _))
			{
				return null;
			}
			List<string> delved = DelvedZones(System.ClaimedZones);
			if (KingdomDelveRules.IsDelve(Key))
			{
				KingdomDelveRules.DelveVerdict verdict = KingdomDelveRules.JudgeDelve(System.Founded, ZoneId,
					System.ClaimedZones, delved);
				return (verdict == KingdomDelveRules.DelveVerdict.Allowed)
					? null
					: KingdomDelveRules.DelveRefusal(verdict, KingdomPresentation.Rich(System.SeatName));
			}
			if (KingdomDelveRules.Reaches(ZoneId, System.ClaimedZones, delved))
			{
				return null;
			}
			// Ground the seated city does not hold is not this gate's question. The seat has
			// already moved to whichever of the realm's cities the founder is standing in by the
			// time anything is commissioned (KingdomSystem.TrySeat, called ahead of the claim
			// guard), so a zone that is still not in the list is a stranger's, and whether
			// anything may be raised on a stranger's ground is a claim the claim gates answer.
			if (System.ClaimedZones == null || !System.ClaimedZones.Contains(ZoneId))
			{
				return null;
			}
			return KingdomDelveRules.RefuseUnreached(KingdomPresentation.Rich(System.SeatName), Name);
		}

		/// <summary>
		/// What the settlement's unopened claims are waiting for, or null when every parasang it
		/// holds is ground its people can work. One line for the ledger and the status report
		/// (STANDARDS 7b), because the answer is the same building however much rock is waiting.
		/// </summary>
		/// <param name="System">The settlement. Null or unfounded answers null.</param>
		public static string UnreachedNote(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return null;
			}
			return KingdomDelveRules.UnreachedNote(KingdomPresentation.Rich(System.SeatName),
				KingdomDelveRules.UnreachedZones(System.ClaimedZones, DelvedZones(System.ClaimedZones)).Count);
		}
	}
}
