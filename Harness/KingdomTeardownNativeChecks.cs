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
	/// <see cref="KingdomDepositOverflowNativeChecks"/>. The one starter timber stack is placed
	/// directly with a harness-assigned raw count, never minted through Quickstart. The building
	/// itself is raised through the real, unmodified <c>KingdomCommission.Commission</c> and torn
	/// down through the real, unmodified <c>KingdomMaterials.OrderStrike</c> — this harness
	/// asserts on their outputs, it does not force <c>KingdomBuilt</c>, the construction job
	/// phase, or the strike receipt directly.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownNativeChecks
	{
		private const string BuildKey = "fire";
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
			return (Complete ? "native-teardown cases=1 passed=1 failed=0"
				: "native-teardown phase=" + Retained.Phase)
				+ "; synthetic-camp=true; synthetic-materials=true; ordinary-acceptance=false"
				+ "; save-load=untested" + Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			return "native-teardown cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomTeardownNativeProvider.Require(Value, Failure);
		}

		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly List<GameObject> Owned = new List<GameObject>();
			private KingdomSystem System;
			private GameObject Chest, Works;
			private string WorksId;
			private int TimberBeforeStrike;
			internal bool Done;
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			/// <summary>Real founding, real dedication, one direct raw material stack, and one
			/// real, synchronous plot commission.</summary>
			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				KingdomNativeCampFounding.Dedicate(Game, Zone, System,
					8 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				GameObject chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null,
					"the container blueprint produced nothing that holds things");
				Owned.Add(chest);
				Cell seat = KingdomNativeCampFounding.Clear(Zone);
				Require(ReferenceEquals(seat.AddObject(chest, NoStack: true), chest),
					"native placement substituted the synthetic store");
				string failure;
				Require(KingdomMaterials.DedicateStockpile(System, Zone, chest, out failure),
					failure ?? "the production check-in refused the synthetic store");
				Chest = chest;
				GameObject timber = GameObject.Create(
					KingdomMaterials.BlueprintFor(KingdomMaterial.Timber));
				Require(GameObject.Validate(timber), "the timber blueprint produced nothing");
				Owned.Add(timber);
				timber.SetIntProperty("NeverStack", 1);
				Chest.Inventory.AddObject(timber, null, true, NoStack: true);
				Require(ReferenceEquals(timber.Physics?.InInventory, Chest),
					"the fixture timber stack is not standing in the store's own custody");
				Require(KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry),
					"the \"" + BuildKey + "\" design is missing from the live catalogue");
				Require(KingdomGrowth.CountStoredWater(Zone) >= entry.CostDrams,
					"the dedicated store does not cover the fixture building's cost");
				bool commissioned = KingdomCommission.Commission(System, BuildKey, null,
					KingdomPlotRules.PlotSize.None, null, out string commissionFailure);
				Require(commissioned, commissionFailure ?? "the fixture commission refused");
				Require(KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _),
					"the construction registry could not be read after commissioning");
				KingdomConstructionJob job = null;
				foreach (KingdomConstructionJob candidate in jobs)
					if (candidate != null && candidate.TargetKey == BuildKey) job = candidate;
				Require(job != null && !string.IsNullOrEmpty(job.OutputId),
					"the fixture commission produced no linked plot-works output");
				WorksId = job.OutputId;
				Phase = 1;
			}

			/// <summary>Polls for the two real-turn transitions this scenario proves: the raised
			/// building becoming functionally built, then its ordered strike completing removal
			/// with material return. Never forces either transition.</summary>
			internal void Check()
			{
				Require(GameObject.Validate(Chest) && ReferenceEquals(Chest.CurrentZone, Zone),
					"the synthetic store did not survive across turns");
				if (Phase == 1)
				{
					GameObject works = Zone.FindObjectByID(WorksId);
					if (works == null || !KingdomUpgrade.IsFunctionallyBuilt(works))
					{
						Evidence.Append("; awaiting-built=true");
						return;
					}
					Works = works;
					TimberBeforeStrike = RawTimber();
					Require(KingdomMaterials.OrderStrike(System, Zone, Works, out string failure),
						failure ?? "the real strike order was refused");
					Phase = 2;
					return;
				}
				if (Phase == 2)
				{
					GameObject stillThere = Zone.FindObjectByID(WorksId);
					bool gone = stillThere == null || !GameObject.Validate(stillThere)
						|| !ReferenceEquals(stillThere, Works);
					if (!gone)
					{
						Evidence.Append("; awaiting-struck=true");
						return;
					}
					int timberAfter = RawTimber();
					Require(timberAfter > TimberBeforeStrike,
						"struck building returned no material to the dedicated store");
					// Negative path: the same reference, now gone, must refuse by name rather
					// than silently accepting a second strike order.
					bool secondOrder = KingdomMaterials.OrderStrike(System, Zone, Works,
						out string secondFailure);
					Require(!secondOrder && !string.IsNullOrEmpty(secondFailure),
						"a second strike order against the absent building was not refused");
					Evidence.Append("; timber-before=").Append(TimberBeforeStrike)
						.Append("; timber-after=").Append(timberAfter)
						.Append("; negative-path-refusal=").Append(secondFailure);
					Done = true;
				}
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
	}
}
