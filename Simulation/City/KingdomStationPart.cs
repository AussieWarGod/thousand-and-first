using System;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// XRL.World.Parts, for the reason r_KingdomPlot states: GamePartBlueprint resolves a part named in
// XML as exactly "XRL.World.Parts.<Name>" and tries no other name (GamePartBlueprint.cs:178, :240).
namespace XRL.World.Parts
{
	/// <summary>
	/// The part that makes a workplace somewhere a settler goes.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.2(b): <i>"one small part, <c>r_KingdomStation</c>, on each
	/// work, handling <c>IdleQueryEvent</c>: if the actor's <c>KingdomResidentId</c> is rostered to
	/// this work, and the current <c>Calendar</c> band matches the role's, push <c>MoveTo(this)</c>
	/// &hellip; and return <c>false</c>."</i> This is that part and nothing more; the band is
	/// <see cref="KingdomPlacementRules"/>'s and the anchor move is
	/// <see cref="KingdomStations"/>'s.
	/// </para>
	/// <para>
	/// <b>Attended-only, and that is the division of labour this architecture wants.</b>
	/// <c>Bored</c> does nothing when the actor is not in the player's zone
	/// (<c>D/XRL/World/AI/GoalHandlers/Bored.cs:267-269</c>), so a station in a suspended zone
	/// costs exactly nothing per turn &mdash; there is no idle actor to offer it.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomStation : IPart
	{
		/// <summary>The work row this object is. Stamped at render from the object's own persistent
		/// id, so it matches the id a settler's post was stamped with.</summary>
		public int WorkId;

		/// <summary>The work's kind, as a <c>KingdomWorkKind</c>. Cached so a claim does not walk
		/// the object's parts on every idle offer.</summary>
		public int Kind;

		/// <summary>When this station last spent somebody's turn. Vanilla's <c>Bed</c> keeps the
		/// identical field for the identical reason (<c>D/XRL/World/Parts/Bed.cs:209-212</c>).</summary>
		public long LastClaimTick;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == IdleQueryEvent.ID;
		}

		public override bool HandleEvent(IdleQueryEvent E)
		{
			if (WorkId == 0 || E.Actor == null || The.Game == null)
			{
				return base.HandleEvent(E);
			}
			bool claimed = false;
			KingdomSystem.Guard("station", delegate
			{
				claimed = KingdomStations.Claim(ParentObject, this, E.Actor, The.Game.TimeTicks);
			});
			// Returning false claims the actor's turn; Bored then spends its 1000 energy and stops
			// offering (Bored.cs:311-316). Returning base keeps them available to every other idle
			// object in the zone, which is what a station that has nothing for them owes.
			return claimed ? false : base.HandleEvent(E);
		}
	}
}
