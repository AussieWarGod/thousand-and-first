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
			/// two parallel cases: the known zero-salvage boundary ("fire", 1 timber cost) and
			/// the positive-salvage case ("larder", 3 timber cost, no extra prerequisite beyond
			/// "fire"'s own).</summary>
			internal void Start()
			{
				StartTicks = Game.TimeTicks;
				KingdomSystem system = KingdomNativeCampFounding.Found(Game, Zone, Require);
				System = system;
				KingdomNativeCampFounding.Dedicate(Game, Zone, system,
					16 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				Require(KingdomTeardownCrewEnrollment.Enroll(Game, Zone, system, Owned.Add,
					Require) == 2, "the disclosed synthetic crew did not reach its exact size");
				bool foundFire = KingdomData.TryGetBuilding("fire", out KingdomRules.BuildEntry fireEntry);
				bool foundLarder = KingdomData.TryGetBuilding("larder", out KingdomRules.BuildEntry larderEntry);
				Require(foundFire && foundLarder,
					"the fixture designs are missing from the live catalogue");
				Require(KingdomGrowth.CountStoredWater(Zone) >= fireEntry.CostDrams + larderEntry.CostDrams,
					"the dedicated store does not cover both fixture buildings' cost");
				Cell seat = KingdomNativeCampFounding.Clear(Zone);
				Case fire = new Case("fire", "fire", system, Zone, Game, Owned);
				Case larder = new Case("larder", "larder", system, Zone, Game, Owned);
				fire.Start(Require, (chest, name) => PlaceChest(seat, chest, name));
				larder.Start(Require, (chest, name) => PlaceChest(seat, chest, name));
				Cases.Add(fire);
				Cases.Add(larder);
			}

			private GameObject PlaceChest(Cell Seat, GameObject Chest, string Name)
			{
				Require(ReferenceEquals(Seat.AddObject(Chest, NoStack: true), Chest),
					Name + ": native placement substituted the synthetic store");
				return Chest;
			}

			/// <summary>Driven only by the sealed script's four teardown-check verbs (Provider.cs,
			/// cumulative ticks 2000/4800/7600/10800), never every tick. Done only once every
			/// case's negative path has been observed; forces no transition.</summary>
			/// <summary>Required per review-3f010e3-teardown-findings.md finding 3: a genuine
			/// crew departure (the roofless brink) previously stalled both cases at Phase 1 with
			/// Ok=true and no named diagnostic -- production's own labour requirement (Core/
			/// KingdomRules.Population.cs:72-87, CrewEffectiveness) silently starves without ever
			/// surfacing that the crew itself is gone. This re-Requires the crew is STILL on the
			/// roll before touching either case, reading the same production
			/// KingdomResidents.OnRollCount(System) the enrollment fixture itself proves against
			/// (Harness/KingdomTeardownCrewEnrollment.cs:55) -- never a cached or harness-local
			/// count. No departure freeze (this never tries to stop a real departure) and no
			/// re-enrolment (this never tries to replace a departed body) -- purely detection, by
			/// name, naming whichever case is still open when it fires.</summary>
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
				foreach (Case c in Cases)
					if (!c.Done) c.Check(Require, Evidence, elapsed);
				Done = true;
				foreach (Case c in Cases) if (!c.Done) Done = false;
			}
		}
	}
}
