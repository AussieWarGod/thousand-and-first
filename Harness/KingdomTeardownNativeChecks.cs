using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;
using XRL.World.Parts;
namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage for building teardown (issue: coverage-matrix). See
	/// <see cref="KingdomTeardownNativeProvider"/> for the sealed script and scope.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED, like <see cref="KingdomDepositOverflowNativeChecks"/>: real
	/// founding/dedication, a 2-body labour crew (<see cref="KingdomTeardownCrewEnrollment"/>),
	/// each design's own authored bill minted as real single-unit objects bounded by the
	/// dedicated store's declared capacity, real <c>KingdomCommission.Commission</c> and real
	/// <c>KingdomMaterials.OrderStrike</c> -- never forcing <c>KingdomBuilt</c> or the job phase.
	/// EXACT SALVAGE: <c>OrderStrike</c> = <c>Cost.Scaled(StrikeSalvagePercent=50)</c>,
	/// integer-floor per material (<c>Growth/KingdomMaterialTally.cs:101-111</c>). <c>"fire"</c>
	/// (1 timber) floors to 0, the ZERO-SALVAGE BOUNDARY; <c>"larder"</c> (3 timber) gives 1,
	/// POSITIVE-SALVAGE. Both computed live from <c>CostFor</c>, never hardcoded.
	/// </para>
	/// </summary>
	internal static partial class KingdomTeardownNativeChecks
	{
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomTeardownNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a teardown attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "teardown setup is absent");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-teardown cases=2 passed=2 failed=0"
				: "native-teardown phase=" + Retained.PhaseSummary())
				+ "; synthetic-camp=true; synthetic-crew=true; synthetic-materials=true"
				+ "; ordinary-acceptance=false"
				+ "; save-load=untested" + Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			return "native-teardown cases=2 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomTeardownNativeProvider.Require(Value, Failure);
		}

		/// <summary>Commission, await built, strike, resolve the new registry row by reference
		/// (<see cref="KingdomTeardownStrikeRowClaims"/>), await removed, assert the exact
		/// salvage delta by STRIKE-RECEIPT ATTRIBUTION -- never "my own chest", never the
		/// pre-strike receipt. Then the negative second-strike path. Forces no transition.</summary>

		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly List<GameObject> Owned = new List<GameObject>();
			private readonly List<Case> Cases = new List<Case>();
			private long StartTicks;
			private KingdomSystem System;
			private Case Fire, Larder;
			private Cell Seat;
			private bool LarderStarted;
			internal List<GameObject> Crew;
			internal bool Done;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			internal string PhaseSummary()
			{
				var parts = new List<string>();
				foreach (Case c in Cases) parts.Add(c.Name + "=" + c.Phase);
				return string.Join(",", parts);
			}

			/// <summary>Real founding, real dedication, a disclosed synthetic labour crew, then
			/// ONE case commissioned now ("fire", 1 timber cost) -- review-teardown-run15-
			/// neverbuilt.md: production's one-gang allocator gives its whole crew to the oldest
			/// open raising only, so two concurrent commissions structurally pin the second at
			/// zero hands. "larder" (3 timber cost) is deferred: Check() commissions it only
			/// once fire reaches Phase 2 (built and struck), so the two never compete for the
			/// same gang.</summary>
			internal void Start()
			{
				StartTicks = Game.TimeTicks;
				KingdomSystem system = KingdomNativeCampFounding.Found(Game, Zone, Require);
				System = system;
				KingdomNativeCampFounding.Dedicate(Game, Zone, system,
					16 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				Require(KingdomTeardownCrewEnrollment.Enroll(Game, Zone, system, Owned.Add,
					Require, out Crew) == 2,
					"the disclosed synthetic crew did not reach its exact size");
				bool foundFire = KingdomData.TryGetBuilding("fire", out KingdomRules.BuildEntry fireEntry);
				bool foundLarder = KingdomData.TryGetBuilding("larder", out KingdomRules.BuildEntry larderEntry);
				Require(foundFire && foundLarder,
					"the fixture designs are missing from the live catalogue");
				Require(KingdomGrowth.CountStoredWater(Zone) >= fireEntry.CostDrams + larderEntry.CostDrams,
					"the dedicated store does not cover both fixture buildings' cost");
				Seat = KingdomNativeCampFounding.Clear(Zone);
				Fire = new Case("fire", "fire", system, Zone, Game, Owned);
				Larder = new Case("larder", "larder", system, Zone, Game, Owned);
				Fire.Start(Require, (chest, name) => PlaceChest(Seat, chest, name),
					line => Evidence.Append(line));
				Cases.Add(Fire);
				KeepCrewOutsideRaisings();
			}

			/// <summary>Filed as issue #163: review-teardown-run20-stuckworking.md -- fire's
			/// labour clock finished on the FIRST settlement pass but its Cleared stage was
			/// refused forever because a living occupant stood on one authored layout slot --
			/// almost certainly one of this fixture's own crew bodies (they never wander
			/// otherwise; production enrolls no other settlers). Once a raising's rect is known
			/// (Case.Rect, re-resolved every Check until known), relocate any of THIS fixture's
			/// own crew bodies found standing inside it to a free cell outside every known rect
			/// -- never a production resident or the player, and never any object this fixture
			/// did not itself enroll.
			/// <para>
			/// review-teardown-run25-staked.md: a one-shot, check-boundary-only relocation
			/// cannot hold -- crew walk back onto the footprint before the next settlement pass,
			/// so #163 kept firing between checks. DISCLOSED AS SYNTHETIC: a relocated body is now
			/// pinned with the production idiom for anchoring an NPC
			/// (Simulation/City/KingdomStations.Claims.cs:137-139) -- Wanders/WandersRandomly =
			/// false, Stay(destination) -- so it no longer WANDERS back on its own. FIXTURE-ONLY:
			/// a real settlement's wandering residents are never anchored this way and can still
			/// trigger #163 -- the gap fix/034-apply-occupant-announce (displacement at apply)
			/// closes. Every call journals each crew body's cell and its walkability.
			/// review-5093b00-teardown-findings.md item 2: Stay only stops WANDERING, not LABOUR
			/// (posting is property-driven off KingdomConstructionPresence, never reading Brain)
			/// and not PRODUCTION -- KingdomStations.Claim (Claims.cs:137-139,150,182-197)
			/// re-issues Stay(target) plus a MoveTo(target) goal for a posted body, target being
			/// the works cell or an adjacent one: production's own posting walks a posted body
			/// back onto the footprint. This fixture cannot keep a site clear by construction;
			/// run 25's 42 firings may recur, and a GREEN run is owed to the #163 production fix,
			/// not this mitigation. This sweep still avoids only the bounding Rect, never
			/// Case.PlacementCells directly -- a superset once a rect is known, so it still parks
			/// outside every placement; Telemetry's occupants= is the one place that reads
			/// PlacementCells (review-5093b00-teardown-findings.md REQUIRED 1). No production
			/// change: a harness precondition mitigation only.</para>
			/// </summary>
			private void KeepCrewOutsideRaisings()
			{
				List<KingdomPlotRules.PlotRect> rects = new List<KingdomPlotRules.PlotRect>();
				if (Fire.HasRect) rects.Add(Fire.Rect);
				if (Larder.HasRect) rects.Add(Larder.Rect);
				if (rects.Count == 0 || Crew == null) return;
				foreach (GameObject body in Crew)
				{
					if (!GameObject.Validate(body)) continue;
					Cell cell = body.CurrentCell;
					if (cell == null) continue;
					KingdomPlotRules.PlotRect hit = default;
					bool inside = false;
					foreach (KingdomPlotRules.PlotRect rect in rects)
						if (cell.X >= rect.X1 && cell.X <= rect.X2
							&& cell.Y >= rect.Y1 && cell.Y <= rect.Y2) { hit = rect; inside = true; break; }
					Cell parked = cell;
					if (inside)
					{
						Cell destination = FindCellOutside(rects);
						Require(destination != null, "no free cell exists outside every known "
							+ "raising rect to relocate a fixture crew body");
						cell.RemoveObject(body);
						Require(ReferenceEquals(destination.AddObject(body, NoStack: true), body),
							"native relocation substituted the synthetic crew body");
						if (body.Brain != null)
						{
							body.Brain.Wanders = false;
							body.Brain.WandersRandomly = false;
							body.Brain.Stay(destination);
						}
						parked = destination;
						Evidence.Append("; crew-relocated id=").Append(body.IDIfAssigned ?? "unassigned")
							.Append(" out-of-rect=(").Append(hit.X1).Append(',').Append(hit.Y1)
							.Append(")-(").Append(hit.X2).Append(',').Append(hit.Y2)
							.Append(") to=(").Append(destination.X).Append(',').Append(destination.Y)
							.Append(") stationary=").Append(body.Brain != null);
					}
					Evidence.Append("; crew=").Append(body.IDIfAssigned ?? "unassigned")
						.Append(" parked-at=(").Append(parked.X).Append(',').Append(parked.Y)
						.Append(") parked-empty=").Append(parked.IsEmptyIgnoring(item => ReferenceEquals(item, body)))
						.Append(" parked-passable=").Append(parked.IsPassable());
				}
			}

			private Cell FindCellOutside(List<KingdomPlotRules.PlotRect> Avoid)
			{
				for (int y = 0; y < Zone.Height; y++)
					for (int x = 0; x < Zone.Width; x++)
					{
						bool inside = false;
						foreach (KingdomPlotRules.PlotRect rect in Avoid)
							if (x >= rect.X1 && x <= rect.X2 && y >= rect.Y1 && y <= rect.Y2)
							{ inside = true; break; }
						if (inside) continue;
						Cell cell = Zone.GetCell(x, y);
						if (cell == null || !cell.IsEmpty() || !cell.IsPassable()) continue;
						return cell;
					}
				return null;
			}

			private GameObject PlaceChest(Cell Seat, GameObject Chest, string Name)
			{
				Require(ReferenceEquals(Seat.AddObject(Chest, NoStack: true), Chest),
					Name + ": native placement substituted the synthetic store");
				return Chest;
			}

			/// <summary>Driven only by the sealed script's five teardown-check verbs (Provider.cs,
			/// cumulative ticks 2400/6000/9600/13200/16800), never every tick. Done only once every
			/// started case's negative path has been observed AND larder has started; forces no
			/// transition.
			/// <para>
			/// Required per review-3f010e3-teardown-findings.md finding 3: a genuine crew
			/// departure (the roofless brink) previously stalled both cases at Phase 1 with
			/// Ok=true and no named diagnostic. This re-Requires the crew is STILL on the roll
			/// (production KingdomResidents.OnRollCount) before touching either case, and ALSO
			/// re-asserts (review-teardown-run15-neverbuilt.md finding 4c) that every enrolled
			/// body is still present in KingdomCrews.AvailableSettlers -- OnRollCount alone is
			/// blind to a standing change. review-bba51c4-teardown-findings.md REQUIRED 1: a post
			/// is no longer asserted to be zero here -- production posts the selected hands while
			/// a raising is open and only un-posts at the next Assign pass, so a strict PostOf==0
			/// re-ask refused a healthy, working crew. A post is now accepted when it names one
			/// of THIS fixture's own live raisings (fire's/larder's WorksId, resolved through
			/// KingdomCityRules.StableId, the same id the allocator posts with); the raw post is
			/// journaled per body ("posted-to="), never asserted. No departure freeze, no
			/// re-enrolment -- purely detection, by name.
			/// </para>
			/// </summary>
			internal void Check()
			{
				foreach (Case c in Cases) Evidence.Append(c.StrikeReceiptDiagnostic());
				long elapsed = Game.TimeTicks - StartTicks;
				long tick = Game.TimeTicks;
				int onRoll = KingdomResidents.OnRollCount(System);
				if (KingdomTeardownCrewDepartureClaims.HasDeparted(onRoll,
					KingdomTeardownCrewEnrollment.CrewSize))
					foreach (Case c in Cases)
						if (!c.Done)
							Require(false, KingdomTeardownCrewDepartureClaims.Diagnostic(c.Name,
								onRoll, KingdomTeardownCrewEnrollment.CrewSize, tick));
				HashSet<int> acceptablePosts = new HashSet<int>();
				if (!string.IsNullOrEmpty(Fire.WorksId))
					acceptablePosts.Add(KingdomCityRules.StableId(Fire.WorksId));
				if (!string.IsNullOrEmpty(Larder.WorksId))
					acceptablePosts.Add(KingdomCityRules.StableId(Larder.WorksId));
				KingdomTeardownCrewEnrollment.RequireAvailable(System, Zone, Crew, Require,
					acceptablePosts, line => Evidence.Append(line));
				foreach (Case c in Cases)
					if (!c.Done) c.Check(Require, Evidence, elapsed);
				if (!LarderStarted && Fire.Phase >= 2)
				{
					Larder.Start(Require, (chest, name) => PlaceChest(Seat, chest, name),
						line => Evidence.Append(line));
					Cases.Add(Larder);
					LarderStarted = true;
				}
				KeepCrewOutsideRaisings();
				KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
				List<GameObject> available = KingdomCrews.AvailableSettlers(System, survey);
				int free = 0;
				foreach (GameObject settler in available)
					if (KingdomStations.PostOf(settler) == 0) free++;
				int labours = 0;
				List<KingdomResidentRow> labourRows = KingdomResidents.RollRows(System, true);
				foreach (KingdomResidentRow row in labourRows)
					if (KingdomResidentRules.Labours(row)) labours++;
				int assignedCrew = 0;
				foreach (Case c in Cases) assignedCrew += c.LastHands;
				Evidence.Append("; available=").Append(available.Count)
					.Append(" free=").Append(free)
					.Append(" assigned-crew=").Append(assignedCrew)
					.Append(" on-roll=").Append(onRoll)
					.Append(" labours=").Append(labours);
				Done = LarderStarted;
				foreach (Case c in Cases) if (!c.Done) Done = false;
			}
		}
	}
}
