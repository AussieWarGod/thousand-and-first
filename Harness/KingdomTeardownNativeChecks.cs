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
	/// a harness-assigned raw timber count, real <c>KingdomCommission.Commission</c> and real
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
			private List<GameObject> Crew;
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
			}

			private GameObject PlaceChest(Cell Seat, GameObject Chest, string Name)
			{
				Require(ReferenceEquals(Seat.AddObject(Chest, NoStack: true), Chest),
					Name + ": native placement substituted the synthetic store");
				return Chest;
			}

			/// <summary>Driven only by the sealed script's four teardown-check verbs (Provider.cs,
			/// cumulative ticks 2000/4800/7600/10800), never every tick. Done only once every
			/// started case's negative path has been observed AND larder has started; forces no
			/// transition.
			/// <para>
			/// Required per review-3f010e3-teardown-findings.md finding 3: a genuine crew
			/// departure (the roofless brink) previously stalled both cases at Phase 1 with
			/// Ok=true and no named diagnostic. This re-Requires the crew is STILL on the roll
			/// (production KingdomResidents.OnRollCount) before touching either case, and ALSO
			/// re-asserts (review-teardown-run15-neverbuilt.md finding 4c) that every enrolled
			/// body is still present in KingdomCrews.AvailableSettlers and carries no post --
			/// OnRollCount alone is blind to a standing or posting change. No departure freeze,
			/// no re-enrolment -- purely detection, by name.
			/// </para>
			/// </summary>
			internal void Check()
			{
				long elapsed = Game.TimeTicks - StartTicks;
				long tick = Game.TimeTicks;
				int onRoll = KingdomResidents.OnRollCount(System);
				if (KingdomTeardownCrewDepartureClaims.HasDeparted(onRoll,
					KingdomTeardownCrewEnrollment.CrewSize))
					foreach (Case c in Cases)
						if (!c.Done)
							Require(false, KingdomTeardownCrewDepartureClaims.Diagnostic(c.Name,
								onRoll, KingdomTeardownCrewEnrollment.CrewSize, tick));
				KingdomTeardownCrewEnrollment.RequireAvailable(System, Zone, Crew, Require);
				foreach (Case c in Cases)
					if (!c.Done) c.Check(Require, Evidence, elapsed);
				if (!LarderStarted && Fire.Phase >= 2)
				{
					Larder.Start(Require, (chest, name) => PlaceChest(Seat, chest, name),
						line => Evidence.Append(line));
					Cases.Add(Larder);
					LarderStarted = true;
				}
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
