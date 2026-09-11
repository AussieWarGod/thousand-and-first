using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// A disclosed synthetic labour crew for the teardown scenario, enrolled through the exact
	/// same production call sequence Harness/KingdomBountyFetchNativeFixture.cs:109-136 already
	/// uses for its own fixture bodies: KingdomCitizenship.TryEnroll, then KingdomBorn=1, then
	/// KingdomResidents.TryEnsureRow -- the roster gate Enrollable requires the property and
	/// TryEnroll does not set it, so it must be carried before TryEnsureRow (as that fixture's
	/// own comment there states; the stamp lands between the two calls, not before both). That
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
	/// <para>
	/// review-teardown-run15-neverbuilt.md finding 4c: OnRollCount alone counts every non-Dead
	/// row, so it passes for a body that Labours()==false, is staged, or is already fully
	/// posted. RequireAvailable is the stronger, re-askable proof: each body must actually
	/// appear in the production KingdomCrews.AvailableSettlers projection (grounded, unstaged,
	/// Resident standing). It is read-only -- nothing here ever sets Standing or a post; a
	/// failing body refuses by name.
	/// </para>
	/// <para>
	/// review-bba51c4-teardown-findings.md REQUIRED 1: a strict PostOf==0 re-ask on every Check
	/// refuses the crew exactly while it is legitimately working -- production posts the
	/// selected hands (Growth/KingdomConstructionPresence.cs:128) and only un-posts at the START
	/// of the NEXT Assign pass (:60-73), so a body mid-raise carries a non-zero post between
	/// checks. PostOf==0 stays a strict assertion only at setup (before any raising exists).
	/// </para>
	/// <para>
	/// review-ead2ede-teardown-findings.md residual: Growth/KingdomGrowth.z15.WorkAssignment.cs
	/// :72-125 re-posts EVERY AvailableSettler to WHATEVER work it drew that pass, before
	/// KingdomConstructionPresence.Assign ever runs -- so a body can legitimately carry a post
	/// that names neither 0 nor one of this fixture's own raisings (some other lawful
	/// production posting this settlement happens to have). The per-Check re-ask therefore
	/// never asserts the post value once a raising can exist: it journals the raw post
	/// ("posted-to=") and whether it happens to name one of this fixture's own Construction
	/// raisings ("own-raising="), for disclosure only. What IS still asserted, always: the body
	/// is a live, valid object (not Dead/absent) and is Resident and present in the production
	/// AvailableSettlers projection -- those are what actually distinguish a healthy crew from
	/// a departed or destroyed one.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownCrewEnrollment
	{
		internal const int CrewSize = 2;

		internal static int Enroll(XRLGame Game, Zone Zone, KingdomSystem System,
			Action<GameObject> Own, Action<bool, string> Require, out List<GameObject> Bodies)
		{
			Bodies = new List<GameObject>();
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
				Bodies.Add(body);
				enrolled++;
			}
			Require(KingdomResidents.OnRollCount(System) >= CrewSize,
				"the synthetic crew roster cannot read a notice");
			RequireAvailable(System, Zone, Bodies, Require, null, null);
			return enrolled;
		}

		/// <summary>Read-only, re-askable every check: each body must be a live, valid object,
		/// grounded, unstaged and Resident (the exact production KingdomCrews.AvailableSettlers
		/// membership). At setup (AcceptablePostIds null) PostOf==0 is a strict assertion, since
		/// no raising exists yet. On a per-Check re-ask (AcceptablePostIds non-null), the post
		/// value is never asserted -- only journaled, alongside whether it happens to name one
		/// of this fixture's own Construction raisings -- since production may legitimately post
		/// an available body to any lawful work each pass. Never mints or forces standing or a
		/// post; refuses by name, per body, only when the body is dead/absent or is not present
		/// in the production AvailableSettlers projection.</summary>
		internal static void RequireAvailable(KingdomSystem System, Zone Zone,
			List<GameObject> Bodies, Action<bool, string> Require, ISet<int> AcceptablePostIds,
			Action<string> Journal)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
			List<GameObject> available = KingdomCrews.AvailableSettlers(System, survey);
			for (int i = 0; i < Bodies.Count; i++)
			{
				GameObject body = Bodies[i];
				Require(GameObject.Validate(body) && body.IsAlive, "crew body " + (i + 1)
					+ " of " + Bodies.Count + " is dead or no longer a valid object");
				bool isAvailable = false;
				for (int j = 0; j < available.Count; j++)
					if (ReferenceEquals(available[j], body)) { isAvailable = true; break; }
				Require(isAvailable, "crew body " + (i + 1) + " of " + Bodies.Count
					+ " is not present in the production AvailableSettlers projection "
					+ "(not Resident standing, staged, or ungrounded)");
				int post = KingdomStations.PostOf(body);
				if (AcceptablePostIds == null)
					Require(post == 0, "crew body " + (i + 1) + " of " + Bodies.Count
						+ " already carries a non-zero post before any raising exists");
				bool ownRaising = AcceptablePostIds != null && post != 0
					&& AcceptablePostIds.Contains(post)
					&& body.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction;
				Journal?.Invoke("; crew=" + (i + 1) + " posted-to=" + post
					+ " own-raising=" + ownRaising);
			}
		}
	}
}
