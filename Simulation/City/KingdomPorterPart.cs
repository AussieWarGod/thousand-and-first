using System;

using ThousandAndFirst.Simulation.City;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X"
// (GamePartBlueprint.cs:178, :240), so a part MUST live in this namespace or the object is built
// without it, silently. This one is never named in XML — it is added at the moment a carrier is
// minted — but it lives here anyway, because a part that is only findable by one path is a part
// the next wave will name in XML and quietly lose.
namespace XRL.World.Parts
{
	/// <summary>
	/// What one carrier is out doing, in the shape a save can hold it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7 gives the porter four steps &mdash; mint at the edge, let
	/// vanilla walk them, deposit the real goods, leave &mdash; and vanilla's <c>Bed</c> does the
	/// identical construction with a <c>MoveTo</c> plus a <c>DelegateGoal</c>
	/// (<c>D/XRL/World/Parts/Bed.cs:213-222</c>). <b>The goal chain is right and the delegate is
	/// not:</b> every one of <c>DelegateGoal</c>'s three delegates is <c>[NonSerialized]</c>
	/// (<c>D/XRL/World/AI/GoalHandlers/DelegateGoal.cs:8-19</c>), so a save taken mid-walk comes
	/// back with a carrier who has forgotten why they were walking. The walk stays vanilla's; what
	/// happens when it ends is a handful of fields on a part.
	/// </para>
	/// <para>
	/// <b>Few fields, deliberately.</b> Parts serialize by positional reflection, so
	/// appending to one is a save-compatibility hazard for every object that already carries it.
	/// The job id is the key to everything else; the rest of what a carrier is doing lives on the
	/// job row, which is where a row belongs.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomPorter : IPart
	{
		/// <summary>The job this body renders. Also the binding key the registry answers for, and
		/// the key the stale-transient sweep is keyed on &mdash; one identity, one body.</summary>
		public int JobId;

		/// <summary>The store cell this trip's load lands in.</summary>
		public int DestX;

		public int DestY;

		/// <summary>The edge cell they came in by and go back out by. Not drawn twice: it is the
		/// same cell, because the road home is the road they walked.</summary>
		public int ExitX;

		public int ExitY;

		/// <summary>The leg this carrier's itinerary has already been re-projected on, plus one, or
		/// zero for none. LIVING-CITY-ARCHITECTURE &sect;3.7 allows <b>at most one re-projection per
		/// leg</b>, and a body-blocked porter that could buy itself a new deadline every check-in
		/// would never fail and never be a story.</summary>
		public int ReprojectedLeg;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (!ThousandAndFirst.KingdomMaster.AutomaticWorkAllowed(
				XRL.The.Game?.GetSystem<ThousandAndFirst.KingdomSystem>())) return;
			if (JobId == 0)
			{
				return;
			}
			ThousandAndFirst.KingdomSystem.Guard("porter tick", delegate
			{
				KingdomPorters.Step(ParentObject, this, TimeTick);
			});
		}
	}
}
