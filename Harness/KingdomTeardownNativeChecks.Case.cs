using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomTeardownNativeChecks
	{
		private sealed partial class Case
		{
			internal readonly string Name;
			private readonly string BuildKey;
			private readonly KingdomSystem System;
			private readonly Zone Zone;
			private readonly XRLGame Game;
			private readonly List<GameObject> Owned;
			private GameObject Chest, Works;
			private Cell WorksCell;
			internal string WorksId;
			private string StrikeReceiptId, JobId;
			private int ExpectedSalvageDelta, TimberCost;
			internal int Phase;
			internal bool Done;
			/// <summary>review-teardown-run20-stuckworking.md: the raising's authored footprint,
			/// resolved once via KingdomPlots.TryReadRect the moment WorksId exists (works while
			/// still under construction -- the rect is stamped at staking, not completion).
			/// Frame uses this to keep the fixture's own crew off the footprint and to journal
			/// occupant ids on it.</summary>
			internal KingdomPlotRules.PlotRect Rect;
			internal bool HasRect;
			/// <summary>review-teardown-run25-staked.md: the authored layout's own placement
			/// cells (world coordinates), decoded once at Start from the commissioned job's own
			/// payload via the engine-touching KingdomTeardownNativeChecks.TryResolvePlacementCells
			/// -- the same decode chain Preflight/ResolveArchitecture use, never guessed from
			/// Rect. review-5093b00-teardown-findings.md REQUIRED 1: Telemetry's occupants=
			/// sweeps this exact set; Frame's KeepCrewOutsideRaisings/FindCellOutside still avoid
			/// only the bounding Rect (a true superset of these cells once both rects are known,
			/// so parking still lands outside every placement -- but NOT before Larder.Start,
			/// when only Fire's rect is known and larder has no rect to be a superset of yet).</summary>
			internal List<(int X, int Y)> PlacementCells = new List<(int X, int Y)>();
			/// <summary>Hands read off the raising root's own production presence property
			/// (KingdomConstructionPresence.HandsProperty) on the last Check() call; 0 once built
			/// or before Start(). Frame sums this across cases for its settlement-wide
			/// "assigned-crew=" telemetry line.</summary>
			internal int LastHands;

			internal Case(string Name, string BuildKey, KingdomSystem System, Zone Zone,
				XRLGame Game, List<GameObject> Owned)
			{
				this.Name = Name; this.BuildKey = BuildKey; this.System = System;
				this.Zone = Zone; this.Game = Game; this.Owned = Owned;
			}

			/// <summary>The authored bill (RuntimeData/KingdomBuildings.xml Materials="...") minted
			/// as N separate real, single-unit objects per material -- the same disclosed
			/// synthetic-stock shape KingdomCampHeartNativeFixture.Mint uses (one physical unit
			/// per Create() call, AddObject(..., NoStack: true), never a single object with its
			/// Count field set to N) -- then one real, synchronous plot commission. A pre-flight
			/// shortfall check refuses by name, per material, if the freshly minted store still
			/// cannot cover the bill; nothing is forced.</summary>
			internal void Start(Action<bool, string> Require, Func<GameObject, string, GameObject> Place,
				Action<string> Journal)
			{
				GameObject chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null,
					Name + ": the container blueprint produced nothing that holds things");
				Owned.Add(chest);
				Chest = (GameObject)Place(chest, Name);
				string failure;
				Require(KingdomMaterials.DedicateStockpile(System, Zone, Chest, out failure),
					failure ?? Name + ": the production check-in refused the synthetic store");
				KingdomMaterialTally bill = KingdomMaterials.CostFor(BuildKey);
				TimberCost = bill.Get(KingdomMaterial.Timber);
				ExpectedSalvageDelta = (int)((long)TimberCost
					* KingdomMaterialRules.StrikeSalvagePercent / 100L);
				MintBill(bill, Require, Journal);
				Require(KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry),
					Name + ": the design is missing from the live catalogue");
				string shortfall;
				Require(CanPayBill(bill, out shortfall),
					Name + ": the synthetic store cannot pay its own design bill ("
					+ (shortfall ?? "unread shortfall") + ")");
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
				JobId = job.Id;
				HasRect = KingdomPlots.TryReadRect(Zone.FindObjectByID(WorksId), out KingdomPlotRules.PlotRect rect);
				Rect = rect;
				Require(TryResolvePlacementCells(job.Payload, out PlacementCells, out string placementFailure),
					placementFailure ?? Name + ": the commissioned job's own payload would not decode");
				Phase = 1;
			}

			internal void Check(Action<bool, string> Require, StringBuilder Evidence, long ElapsedTicks)
			{
				Require(GameObject.Validate(Chest) && ReferenceEquals(Chest.CurrentZone, Zone),
					Name + ": the synthetic store did not survive across turns");
				if (Phase == 1)
				{
					GameObject works = ResolveCurrentRoot(); // KingdomTeardownNativeChecks.Root.cs
					// review-15f9de2-teardown-findings.md residual B: Rect/HasRect resolved once
					// at Start left HasRect false forever if TryReadRect could not yet read the
					// stamped rect that pass; re-try every Check until it succeeds, journaling
					// the transition once (never re-journaled once known).
					if (!HasRect && works != null)
					{
						HasRect = KingdomPlots.TryReadRect(works, out KingdomPlotRules.PlotRect rect);
						if (HasRect)
						{
							Rect = rect;
							Evidence.Append("; case=").Append(Name).Append(" rect-known=true");
						}
					}
					if (works == null || !KingdomUpgrade.IsFunctionallyBuilt(works))
					{
						LastHands = works == null ? 0
							: works.GetIntProperty(KingdomConstructionPresence.HandsProperty);
						Evidence.Append("; case=").Append(Name).Append(" awaiting-built=true; elapsed-ticks=")
							.Append(ElapsedTicks).Append(Telemetry(works));
						return;
					}
					LastHands = 0;
					// Run 45: the building's own terminal receipt may not be supersedable yet
					// (closure pending); that is a wait, not a refusal (KingdomTeardownNativeChecks.Root.cs).
					KingdomTeardownStrikeReadiness.Verdict readiness = StrikeReadiness(works, Evidence);
					if (readiness == KingdomTeardownStrikeReadiness.Verdict.WaitClosure)
					{ Evidence.Append(" awaiting-supersede=true").Append(Telemetry(works)); return; }
					Require(readiness == KingdomTeardownStrikeReadiness.Verdict.Strike,
						Name + ": a non-terminal receipt of another job holds this building");
					Works = works;
					StruckId = works.IDIfAssigned;
					WorksCell = works.CurrentCell;
					Require(WorksCell != null,
						Name + ": the functionally-built works carries no standing cell");
					string preStrikeReceiptId = works.GetStringProperty(
						KingdomConstruction.ReceiptProperty) ?? "";
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
					Evidence.Append(StrikeTelemetry(works));
					return;
				}
				// A same-ID object that is NOT the exact struck reference is never a pass: a
				// mint-over-the-old-id replacement must refuse, not be silently read as removal.
				GameObject stillThere = Zone.FindObjectByID(StruckId ?? WorksId);
				Require(stillThere == null || ReferenceEquals(stillThere, Works),
					Name + ": a different object now carries the struck building's own identity "
					+ (StruckId ?? WorksId) + " -- a same-ID replacement is never a valid removal");
				if (stillThere != null)
				{
					Evidence.Append("; case=").Append(Name).Append(" awaiting-struck=true")
						.Append(StrikeTelemetry(stillThere));
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

			/// <summary>Mints exactly the authored bill for this design (RuntimeData/
			/// KingdomBuildings.xml Materials="..."), one real single-unit object per unit --
			/// KingdomCampHeartNativeFixture.Mint's own proven shape (Create() then
			/// Inventory.AddObject(..., NoStack: true)). review-fb02900-coverage-findings.md
			/// finding 2: the reservation path honours Count end to end (Growth/
			/// KingdomConstruction.InputObservationRegistry.cs:141 -> InputPlannerScan.cs:173 ->
			/// KingdomMaterialDebitRules.Planning.cs:57), so a single stacked-Count object was
			/// never proven to be native run 12's real InsufficientMaterial cause; that claim is
			/// withdrawn and the true cause is UNPROVEN from the available evidence. Single-unit
			/// minting is kept only because it is the exact shape the proven sibling fixture
			/// uses, not because it is known to fix anything here. CAPACITY-BOUNDED (finding 2A):
			/// refuses by name, before minting anything, if the design's own bill would not fit
			/// the dedicated store's declared capacity (KingdomSurvey.StockCapacityOf -- the same
			/// read KingdomCampHeartNativeFixture uses for its own declared-capacity fill).
			/// Discloses "synthetic-bill design=&lt;key&gt; &lt;material&gt;=&lt;n&gt;..." for
			/// every nonzero material in the bill.</summary>
			private void MintBill(KingdomMaterialTally Bill, Action<bool, string> Require,
				Action<string> Journal)
			{
				int totalUnits = 0;
				foreach (KingdomMaterial material in (KingdomMaterial[])Enum.GetValues(
					typeof(KingdomMaterial)))
					totalUnits += Bill.Get(material);
				int capacity = KingdomSurvey.StockCapacityOf(Chest);
				Require(totalUnits <= capacity,
					Name + ": the dedicated store's declared capacity (" + capacity
					+ ") cannot hold this design's own bill (" + totalUnits + " units)");
				StringBuilder line = new StringBuilder("; synthetic-bill design=").Append(BuildKey);
				foreach (KingdomMaterial material in (KingdomMaterial[])Enum.GetValues(
					typeof(KingdomMaterial)))
				{
					int units = Bill.Get(material);
					if (units <= 0) continue;
					line.Append(' ').Append(material).Append('=').Append(units);
					string blueprint = KingdomMaterials.BlueprintFor(material);
					Require(!string.IsNullOrEmpty(blueprint),
						Name + ": no production blueprint for material " + material);
					for (int i = 0; i < units; i++)
					{
						GameObject unit = GameObject.Create(blueprint);
						Require(GameObject.Validate(unit) && unit.Count == 1,
							Name + ": the " + material + " blueprint produced no fresh unit");
						Owned.Add(unit);
						GameObject accepted = Chest.Inventory.AddObject(unit, null,
							Silent: true, NoStack: true);
						Require(ReferenceEquals(accepted, unit),
							Name + ": a minted " + material + " unit did not land in the "
							+ "store's own custody");
					}
				}
				Journal(line.ToString());
			}

			/// <summary>Read-only: does the store, as freshly minted, cover this design's own
			/// bill? Refuses by name with the exact missing tally rather than letting Commission
			/// fall through to realm-routed logistics no bare harness zone has set up.</summary>
			private bool CanPayBill(KingdomMaterialTally Bill, out string Shortfall)
			{
				Shortfall = null;
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
				if (KingdomMaterialRules.Covers(stock.Tally, Bill)) return true;
				KingdomMaterialTally missing = KingdomMaterialRules.Missing(stock.Tally, Bill);
				Shortfall = missing == null ? "of its own design bill" : missing.Describe();
				return false;
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
	}
}
