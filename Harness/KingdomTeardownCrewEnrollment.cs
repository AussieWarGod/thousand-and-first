using System;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// A disclosed synthetic labour crew for the teardown scenario, enrolled through the exact
	/// same production call sequence Harness/KingdomBountyFetchNativeFixture.cs:109-136 already
	/// uses for its own fixture bodies (KingdomCitizenship.TryEnroll then
	/// KingdomResidents.TryEnsureRow, with KingdomBorn=1 stamped first -- Enrollable requires it
	/// and TryEnroll does not set it, exactly as that fixture's own comment there states). That
	/// method is a private instance member of a sealed, unrelated fixture class closing over its
	/// own Owned/Clear/Create helpers, so a call-through was not possible; this shard replicates
	/// its production call shape instead of copy-pasting its fixture-specific plumbing, and nothing
	/// here is a NEW production API.
	/// <para>
	/// CREW SIZE = 2 = <c>RaisingHandsWanted</c> (Core/KingdomRules.Clock.cs:54), the exact hands
	/// count <c>CrewEffectiveness</c> saturates at 100% for (Core/KingdomRules.Population.cs:
	/// 72-87) -- a third body would not raise a design any faster. A freshly founded camp has
	/// Population==0 and raises nothing at all (review-teardown-reachability-findings.md
	/// section 2-3), so this crew is the minimal harness-usability fix for that gap.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownCrewEnrollment
	{
		internal const int CrewSize = 2;

		internal static int Enroll(XRLGame Game, Zone Zone, KingdomSystem System,
			Action<GameObject> Own, Action<bool, string> Require)
		{
			long tick = Game.TimeTicks;
			int enrolled = 0;
			for (int i = 0; i < CrewSize; i++)
			{
				GameObject body = GameObject.Create("NPC");
				Require(GameObject.Validate(body) && body.Brain != null && body.Body != null
					&& body.IsAlive && !body.IsPlayer(), "crew body is not a live NPC shape");
				Own(body);
				body.SetStringProperty("Species", "human");
				Require(KingdomCitizenship.TryEnroll(System, body,
					KingdomCitizenshipEnrollmentReason.Arrival, tick, out string failure),
					failure ?? "crew enrollment refused");
				body.SetIntProperty("KingdomBorn", 1);
				Cell seat = KingdomNativeCampFounding.Clear(Zone);
				Require(ReferenceEquals(seat.AddObject(body, NoStack: true), body),
					"native placement substituted the synthetic crew body");
				Require(KingdomResidents.TryEnsureRow(System, body,
					"native teardown crew fixture", null, tick, out var city, out int id)
					&& ReferenceEquals(city, System.City) && id > 0,
					"real crew enrollment did not publish a row");
				enrolled++;
			}
			Require(KingdomResidents.OnRollCount(System) >= CrewSize,
				"the synthetic crew roster cannot read a notice");
			return enrolled;
		}
	}
}
