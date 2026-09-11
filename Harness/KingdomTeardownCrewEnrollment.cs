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
	/// checks. PostOf==0 stays a strict assertion only at setup (before any raising exists); the
	/// per-Check re-ask instead accepts a body posted to one of THIS fixture's own raisings
	/// (fire's or larder's live WorksId, resolved through the same KingdomCityRules.StableId the
	/// allocator itself posts with) as Construction work, journaling the raw post per body as
	/// "posted-to=" rather than asserting a value that legitimately changes turn to turn.
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

		/// <summary>Read-only, re-askable every check: each body must be grounded, unstaged and
		/// Resident (the exact production KingdomCrews.AvailableSettlers membership). At setup
		/// (AcceptablePostIds null) PostOf==0 is a strict assertion, since no raising exists yet.
		/// On a per-Check re-ask, a non-zero post is accepted ONLY when it names one of this
		/// fixture's own raisings (AcceptablePostIds, Construction-kind) -- never any other post
		/// -- and the raw post is journaled ("posted-to=") rather than asserted, since it
		/// legitimately changes turn to turn while the crew is working. Never mints or forces
		/// standing or a post; refuses by name, per body, on any real mismatch.</summary>
		internal static void RequireAvailable(KingdomSystem System, Zone Zone,
			List<GameObject> Bodies, Action<bool, string> Require, ISet<int> AcceptablePostIds,
			Action<string> Journal)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
			List<GameObject> available = KingdomCrews.AvailableSettlers(System, survey);
			for (int i = 0; i < Bodies.Count; i++)
			{
				GameObject body = Bodies[i];
				bool isAvailable = false;
				for (int j = 0; j < available.Count; j++)
					if (ReferenceEquals(available[j], body)) { isAvailable = true; break; }
				Require(isAvailable, "crew body " + (i + 1) + " of " + Bodies.Count
					+ " is not present in the production AvailableSettlers projection "
					+ "(not Resident standing, staged, or ungrounded)");
				int post = KingdomStations.PostOf(body);
				bool free = post == 0;
				bool postedToThisFixture = !free && AcceptablePostIds != null
					&& AcceptablePostIds.Contains(post)
					&& body.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction;
				Require(free || postedToThisFixture, "crew body " + (i + 1) + " of "
					+ Bodies.Count + " carries post " + post
					+ ", neither free nor posted to this fixture's own raising");
				Journal?.Invoke("; crew=" + (i + 1) + " posted-to=" + post);
			}
		}
	}
}
