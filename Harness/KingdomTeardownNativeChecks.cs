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
		private sealed class Case
		{
			internal readonly string Name;
			private readonly string BuildKey;
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly XRLGame Game;
			private readonly List<GameObject> Owned;
			private GameObject Chest, Works;
			private Cell WorksCell;
			private string WorksId, StrikeReceiptId;
			private int ExpectedSalvageDelta, TimberCost;
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
					WorksCell = works.CurrentCell;
					Require(WorksCell != null,
						Name + ": the functionally-built works carries no standing cell");
					string preStrikeReceiptId = works.GetStringProperty(
						KingdomConstruction.ReceiptProperty);
					Require(!string.IsNullOrEmpty(preStrikeReceiptId),
						Name + ": the functionally-built works carries no construction receipt");
					Require(KingdomMaterials.OrderStrike(System, Zone, Works, out string failure),
						failure ?? Name + ": the real strike order was refused");
					// Captured AFTER the strike, never before -- OrderStrike mints a NEW strike-
					// route row and rebinds the works to it in the same call, superseding the
					// paid-construction receipt (Growth/KingdomMaterials.08.StrikeOrdering.cs:
					// 257-261, 09.StrikeStampAndCancellation.cs:35, 13.StrikeRemovalAndSalvage.cs:113).
					StrikeReceiptId = works.GetStringProperty(KingdomConstruction.ReceiptProperty);
					Require(!string.IsNullOrEmpty(StrikeReceiptId)
						&& StrikeReceiptId != preStrikeReceiptId,
						Name + ": the strike did not rebind the works to a distinct strike-job "
						+ "receipt; the old paid-construction receipt would misattribute salvage");
					// Resolve the row by reference: route/subject/owner/zone/phase re-proved live.
					KingdomConstruction.TryFind(StrikeReceiptId, out KingdomConstructionJob row);
					Require(KingdomTeardownStrikeRowClaims.IsExpectedStrikeRow(row,
						works.IDIfAssigned, KingdomConstruction.OwnerOf(System), Zone.ZoneID,
						out string rowFailure), Name + ": " + rowFailure);
					Phase = 2;
					return;
				}
				// A same-ID object that is NOT the exact struck reference is never a pass: a
				// mint-over-the-old-id replacement must refuse, not be silently read as removal.
				GameObject stillThere = Zone.FindObjectByID(WorksId);
				Require(stillThere == null || ReferenceEquals(stillThere, Works),
					Name + ": a different object now carries the struck building's own identity "
					+ WorksId + " -- a same-ID replacement is never a valid removal");
				if (stillThere != null)
				{
					Evidence.Append("; case=").Append(Name).Append(" awaiting-struck=true");
					return;
				}
				// By reference too, not merely "the old id is gone".
				foreach (GameObject onCell in WorksCell.GetObjects())
					Require(!GameObject.Validate(onCell) || onCell.GetIntProperty("KingdomBuilt") != 1
						|| onCell.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != BuildKey,
						Name + ": an object still reads as this finished building on its cell");
				int salvaged = SalvageByReceipt(Require);
				// Exact delta, attributed by THIS case's own strike receipt, never by chest.
				Require(salvaged == ExpectedSalvageDelta,
					Name + ": struck building's material return did not match the exact computed "
					+ "salvage rule: expected-delta=" + ExpectedSalvageDelta + " observed="
					+ salvaged);
				bool secondOrder = KingdomMaterials.OrderStrike(System, Zone, Works,
					out string secondFailure);
				Require(!secondOrder && !string.IsNullOrEmpty(secondFailure),
					Name + ": a second strike order against the absent building was not refused");
				Evidence.Append("; case=").Append(Name).Append(" timber-cost=").Append(TimberCost)
					.Append(" salvaged-by-receipt=").Append(salvaged)
					.Append(" expected-salvage-delta=").Append(ExpectedSalvageDelta)
					.Append(" elapsed-ticks=").Append(ElapsedTicks)
					.Append(" negative-path-refusal=").Append(secondFailure);
				Done = true;
			}

			/// <summary>Every zone stockpile (never assumed own chest) for a timber item whose
			/// salvage receipt names THIS case's exact strike, by property, not location.</summary>
			private int SalvageByReceipt(Action<bool, string> Require)
			{
				int total = 0;
				GameObject matched = null;
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
				foreach (GameObject stockpile in stock.Stockpiles)
				{
					if (!GameObject.Validate(stockpile) || stockpile.Inventory == null) continue;
					foreach (GameObject item in stockpile.Inventory.Objects)
					{
						if (!GameObject.Validate(item)
							|| item.GetStringProperty(KingdomMaterials.StrikeSalvageReceiptProperty)
								!= StrikeReceiptId) continue;
						Require(matched == null,
							Name + ": more than one salvage item carries this exact strike receipt");
						matched = item;
						Require(KingdomMaterials.TryOrdinaryMaterialOf(item, out KingdomMaterial kind)
							&& kind == KingdomMaterial.Timber,
							Name + ": the receipted salvage item is not the expected material");
						total += KingdomMaterials.RawCensusCountOf(item);
					}
				}
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

			/// <summary>Real founding, real dedication, a disclosed synthetic labour crew, then
			/// two parallel cases: the known zero-salvage boundary ("fire", 1 timber cost) and
			/// the positive-salvage case ("larder", 3 timber cost, no extra prerequisite beyond
			/// "fire"'s own).</summary>
			internal void Start()
			{
				StartTicks = Game.TimeTicks;
				KingdomSystem system = KingdomNativeCampFounding.Found(Game, Zone, Require);
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
