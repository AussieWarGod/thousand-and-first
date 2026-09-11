using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Behavioural coverage for building teardown (issue: coverage-matrix). See
	/// <see cref="KingdomTeardownNativeProvider"/> for the sealed script and scope.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. Real founding
	/// (<c>KingdomNativeCampFounding.Found</c>), real water dedication
	/// (<c>KingdomNativeCampFounding.Dedicate</c>) and real stockpile dedication
	/// (<c>KingdomMaterials.DedicateStockpile</c>), exactly like
	/// <see cref="KingdomDepositOverflowNativeChecks"/>. Starter timber stacks are placed
	/// directly with harness-assigned raw counts, never minted through Quickstart. Buildings are
	/// raised through the real, unmodified <c>KingdomCommission.Commission</c> and torn down
	/// through the real, unmodified <c>KingdomMaterials.OrderStrike</c> — this harness asserts on
	/// their outputs, it does not force <c>KingdomBuilt</c>, the construction job phase, or the
	/// strike receipt directly.
	/// </para>
	/// <para>
	/// EXACT SALVAGE, NOT "SOME". <c>OrderStrike</c> reads the paid receipt's own material tally
	/// and calls <c>KingdomMaterialRules.StrikeSalvage</c>
	/// (<c>Growth/KingdomMaterialRules.Clearance.cs:211-219</c>), which is exactly
	/// <c>Cost.Scaled(StrikeSalvagePercent)</c> — <c>StrikeSalvagePercent = 50</c>
	/// (<c>:193</c>) and <c>Scaled</c> is integer-floor per material,
	/// <c>(long)Amounts[i] * Percent / 100L</c> (<c>Growth/KingdomMaterialTally.cs:101-111</c>).
	/// Two cases run in parallel to prove both ends of that floor:
	/// <list type="bullet">
	/// <item><description><c>"fire"</c> costs exactly 1 timber
	/// (<c>RuntimeData/KingdomBuildings.xml:544-546</c>, <c>Materials="timber:1"</c>), so
	/// <c>(1*50)/100 = 0</c> — the explicit ZERO-SALVAGE BOUNDARY row: a struck "fire" plot
	/// returns no timber, by design ("nothing about striking is a refund",
	/// <c>KingdomMaterialRules.Clearance.cs:188-190</c>).</description></item>
	/// <item><description><c>"larder"</c> costs exactly 3 timber
	/// (<c>RuntimeData/KingdomBuildings.xml:399-400</c>, <c>Materials="timber:3"</c>), has no
	/// <c>MinStage</c>/<c>MinTech</c>/<c>Staff</c> attribute (so it is reachable at
	/// <c>GrowthStage.Camp</c> with no crew, exactly like "fire";
	/// <c>Core/KingdomRules.cs:3-9</c>, <c>Growth/KingdomCommission.cs:20</c>) and shares "fire"'s
	/// single-cell <c>Plot="S"</c>, so it commissions through the identical call shape with no
	/// extra prerequisite. <c>(3*50)/100 = 1</c> — a struck larder returns exactly 1 timber, the
	/// POSITIVE-SALVAGE row this fixture was missing before.</description></item>
	/// </list>
	/// Both deltas are computed from <c>KingdomMaterials.CostFor</c> and
	/// <c>KingdomMaterialRules.StrikeSalvagePercent</c> directly, never hardcoded, so a future
	/// catalogue or rule change is caught rather than silently re-passing a stale expectation.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownNativeChecks
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
				+ "; synthetic-camp=true; synthetic-materials=true; ordinary-acceptance=false"
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

		/// <summary>One design's teardown lifecycle: commission, await built, strike, await
		/// removed, assert the exact computed salvage delta, then the negative second-strike
		/// path. Never forces any transition.</summary>
		private sealed class Case
		{
			internal readonly string Name;
			private readonly string BuildKey;
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly XRLGame Game;
			private readonly List<GameObject> Owned;
			private GameObject Chest, Works;
			private string WorksId;
			private int TimberBeforeStrike, ExpectedSalvageDelta, TimberCost;
			internal int Phase;
			internal bool Done;

			internal Case(string Name, string BuildKey, KingdomSystem System, Zone Zone,
				XRLGame Game, List<GameObject> Owned)
			{
				this.Name = Name; this.BuildKey = BuildKey; this.System = System;
				this.Zone = Zone; this.Game = Game; this.Owned = Owned;
			}

			/// <summary>One direct raw material stack sized to this design's own cost, and one
			/// real, synchronous plot commission.</summary>
			internal void Start(Action<bool, string> Require, Func<GameObject, string, GameObject> Place)
			{
				GameObject chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null,
					Name + ": the container blueprint produced nothing that holds things");
				Owned.Add(chest);
				Chest = (GameObject)Place(chest, Name);
				string failure;
				Require(KingdomMaterials.DedicateStockpile(System, Zone, Chest, out failure),
					failure ?? Name + ": the production check-in refused the synthetic store");
				TimberCost = KingdomMaterials.CostFor(BuildKey).Get(KingdomMaterial.Timber);
				ExpectedSalvageDelta = (int)((long)TimberCost
					* KingdomMaterialRules.StrikeSalvagePercent / 100L);
				GameObject timber = GameObject.Create(
					KingdomMaterials.BlueprintFor(KingdomMaterial.Timber));
				Require(GameObject.Validate(timber), Name + ": the timber blueprint produced nothing");
				Owned.Add(timber);
				timber.SetIntProperty("NeverStack", 1);
				timber.Count = TimberCost;
				Chest.Inventory.AddObject(timber, null, true, NoStack: true);
				Require(ReferenceEquals(timber.Physics?.InInventory, Chest),
					Name + ": the fixture timber stack is not standing in the store's own custody");
				Require(KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry),
					Name + ": the design is missing from the live catalogue");
				bool commissioned = KingdomCommission.Commission(System, BuildKey, null,
					KingdomPlotRules.PlotSize.None, null, out string commissionFailure);
				Require(commissioned, commissionFailure ?? Name + ": the fixture commission refused");
				Require(KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _),
					Name + ": the construction registry could not be read after commissioning");
				KingdomConstructionJob job = null;
				foreach (KingdomConstructionJob candidate in jobs)
					if (candidate != null && candidate.TargetKey == BuildKey) job = candidate;
				Require(job != null && !string.IsNullOrEmpty(job.OutputId),
					Name + ": the fixture commission produced no linked plot-works output");
				WorksId = job.OutputId;
				Phase = 1;
			}

			internal void Check(Action<bool, string> Require, StringBuilder Evidence, long ElapsedTicks)
			{
				Require(GameObject.Validate(Chest) && ReferenceEquals(Chest.CurrentZone, Zone),
					Name + ": the synthetic store did not survive across turns");
				if (Phase == 1)
				{
					GameObject works = Zone.FindObjectByID(WorksId);
					if (works == null || !KingdomUpgrade.IsFunctionallyBuilt(works))
					{
						Evidence.Append("; case=").Append(Name).Append(" awaiting-built=true; elapsed-ticks=")
							.Append(ElapsedTicks);
						return;
					}
					Works = works;
					TimberBeforeStrike = RawTimber();
					Require(KingdomMaterials.OrderStrike(System, Zone, Works, out string failure),
						failure ?? Name + ": the real strike order was refused");
					Phase = 2;
					return;
				}
				GameObject stillThere = Zone.FindObjectByID(WorksId);
				bool gone = stillThere == null || !GameObject.Validate(stillThere)
					|| !ReferenceEquals(stillThere, Works);
				if (!gone)
				{
					Evidence.Append("; case=").Append(Name).Append(" awaiting-struck=true");
					return;
				}
				int timberAfter = RawTimber();
				// Exact delta, not "some change": the production formula is asserted, never assumed.
				Require(timberAfter - TimberBeforeStrike == ExpectedSalvageDelta,
					Name + ": struck building's material return did not match the exact computed "
					+ "salvage rule: expected-delta=" + ExpectedSalvageDelta + " observed-delta="
					+ (timberAfter - TimberBeforeStrike));
				bool secondOrder = KingdomMaterials.OrderStrike(System, Zone, Works,
					out string secondFailure);
				Require(!secondOrder && !string.IsNullOrEmpty(secondFailure),
					Name + ": a second strike order against the absent building was not refused");
				Evidence.Append("; case=").Append(Name).Append(" timber-cost=").Append(TimberCost)
					.Append(" timber-before=").Append(TimberBeforeStrike)
					.Append(" timber-after=").Append(timberAfter)
					.Append(" expected-salvage-delta=").Append(ExpectedSalvageDelta)
					.Append(" elapsed-ticks=").Append(ElapsedTicks)
					.Append(" negative-path-refusal=").Append(secondFailure);
				Done = true;
			}

			private int RawTimber()
			{
				int total = 0;
				foreach (GameObject item in Chest.Inventory.Objects)
					if (KingdomMaterials.TryOrdinaryMaterialOf(item, out KingdomMaterial kind)
						&& kind == KingdomMaterial.Timber)
						total += KingdomMaterials.RawCensusCountOf(item);
				return total;
			}
		}

		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly List<GameObject> Owned = new List<GameObject>();
			private readonly List<Case> Cases = new List<Case>();
			private long StartTicks;
			internal bool Done;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			internal string PhaseSummary()
			{
				var parts = new List<string>();
				foreach (Case c in Cases) parts.Add(c.Name + "=" + c.Phase);
				return string.Join(",", parts);
			}

			/// <summary>Real founding, real dedication, then two parallel cases: the known
			/// zero-salvage boundary ("fire", 1 timber cost) and the positive-salvage case
			/// ("larder", 3 timber cost, no extra prerequisite beyond "fire"'s own).</summary>
			internal void Start()
			{
				StartTicks = Game.TimeTicks;
				KingdomSystem system = KingdomNativeCampFounding.Found(Game, Zone, Require);
				KingdomNativeCampFounding.Dedicate(Game, Zone, system,
					16 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				Require(KingdomData.TryGetBuilding("fire", out KingdomRules.BuildEntry fireEntry)
					&& KingdomData.TryGetBuilding("larder", out KingdomRules.BuildEntry larderEntry),
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

			/// <summary>Polls both cases every tick; done only once every case's negative path
			/// has been observed. Never forces either case's transitions.</summary>
			internal void Check()
			{
				long elapsed = Game.TimeTicks - StartTicks;
				foreach (Case c in Cases)
					if (!c.Done) c.Check(Require, Evidence, elapsed);
				Done = true;
				foreach (Case c in Cases) if (!c.Done) Done = false;
			}
		}
	}
}
