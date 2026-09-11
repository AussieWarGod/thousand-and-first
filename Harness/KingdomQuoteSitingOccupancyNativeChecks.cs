using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Three behaviours for the #034 quote-siting occupancy fix, all synchronous (no engine
	/// turns needed: quoting and committing are instant real production calls). SYNTHETIC
	/// SETUP, DISCLOSED: real founding/dedication, one synthetic NPC body per phase, harness-
	/// assigned raw timber -- nothing forces a rect or a snapshot directly.
	/// </summary>
	internal static class KingdomQuoteSitingOccupancyNativeChecks
	{
		private const string BuildKey = "fire";
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomQuoteSitingOccupancyNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a quote-occupancy attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			Complete = Retained.Done;
			return "native-quote-occupancy cases=3 passed="
				+ (Complete ? "3 failed=0" : "0 failed=0") + Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			return "native-quote-occupancy cases=3 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomQuoteSitingOccupancyNativeProvider.Require(Value, Failure);
		}

		/// <summary>All three behaviours run synchronously inside Start() -- quoting and
		/// committing never wait on engine turns, unlike construction/strike.</summary>
		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly List<GameObject> Owned = new List<GameObject>();
			internal bool Done;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			internal void Start()
			{
				KingdomSystem system = KingdomNativeCampFounding.Found(Game, Zone, Require);
				KingdomNativeCampFounding.Dedicate(Game, Zone, system,
					16 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				GameObject chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null,
					"the container blueprint produced nothing that holds things");
				Owned.Add(chest);
				Cell seat = KingdomNativeCampFounding.Clear(Zone);
				Require(ReferenceEquals(seat.AddObject(chest, NoStack: true), chest),
					"native placement substituted the synthetic store");
				string dedicateFailure;
				Require(KingdomMaterials.DedicateStockpile(system, Zone, chest, out dedicateFailure),
					dedicateFailure ?? "the production check-in refused the synthetic store");
				Require(KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry),
					"the fixture design is missing from the live catalogue");
				OccupiedFirstClearAlternate(system, entry);
				AllOccupiedNoMutation(system, entry);
				DriftAfterQuotePreflightRefusal(system, entry);
				Done = true;
			}

			/// <summary>(1) A quote's first candidate is occupied by a synthetic body; the
			/// re-quote must choose a different rect that excludes the occupied cell; removing
			/// the body and quoting a third time must return the original rect.</summary>
			private void OccupiedFirstClearAlternate(KingdomSystem system, KingdomRules.BuildEntry entry)
			{
				string failure;
				Require(KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote firstQuote, out failure),
					failure ?? "the first quote refused with no occupant on the ground");
				KingdomPlotRules.PlotRect firstRect = firstQuote.Rect;
				GameObject occupant = GameObject.Create("NPC");
				Require(GameObject.Validate(occupant), "the probe body blueprint produced nothing");
				Owned.Add(occupant);
				Cell occupiedCell = Zone.GetCell(firstRect.X1, firstRect.Y1);
				Require(occupiedCell != null, "the first quote's own rect has no cell to occupy");
				Require(ReferenceEquals(occupiedCell.AddObject(occupant, NoStack: true), occupant),
					"native placement substituted the probe body");
				bool secondFound = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote secondQuote, out failure);
				bool alternateChosen = secondFound && !secondQuote.Rect.Contains(firstRect.X1, firstRect.Y1);
				occupiedCell.RemoveObject(occupant);
				occupant.Obliterate(null, Silent: true);
				Require(alternateChosen || !secondFound,
					"occupying the first candidate's own corner did not change the chosen rect or "
					+ "produce a refusal");
				bool thirdFound = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote thirdQuote, out failure);
				Require(thirdFound && thirdQuote.Rect.X1 == firstRect.X1 && thirdQuote.Rect.Y1 == firstRect.Y1,
					"the original rect did not return once the probe body was removed");
				Evidence.Append("; case=occupied-first-clear-alternate alternate-chosen=")
					.Append(alternateChosen).Append(" original-rect-restored=true");
			}

			/// <summary>(2) When every lawful pose is occupied, the quote must refuse by the
			/// exact "living occupant" sentence and spend nothing -- census before/after the
			/// refused attempt must be identical.</summary>
			private void AllOccupiedNoMutation(KingdomSystem system, KingdomRules.BuildEntry entry)
			{
				// Every candidate pose the ground loop could ever propose lies inside the zone's
				// interior (Growth/KingdomPlotBoundsRules.cs:35 TryInterior) -- occupying every
				// interior cell is the only way to guarantee no lawful pose survives, short of
				// laying real plots over the whole zone. Expensive, disclosed, and bounded by one
				// zone's own size, never repeated.
				Require(KingdomPlotRules.TryInterior(Zone.Width, Zone.Height, out var interior),
					"the zone has no interior to occupy for the all-occupied case");
				List<GameObject> occupants = new List<GameObject>();
				for (int y = interior.Y1; y <= interior.Y2; y++)
					for (int x = interior.X1; x <= interior.X2; x++)
					{
						Cell cell = Zone.GetCell(x, y);
						if (cell == null || !cell.IsEmpty()) continue;
						GameObject occupant = GameObject.Create("NPC");
						Require(GameObject.Validate(occupant), "an all-occupied probe body produced nothing");
						Owned.Add(occupant);
						occupants.Add(occupant);
						Require(ReferenceEquals(cell.AddObject(occupant, NoStack: true), occupant),
							"native placement substituted an all-occupied probe body");
					}
				string failure;
				int timberBefore = RawTimber(system);
				int waterBefore = KingdomGrowth.CountStoredWater(Zone);
				bool stillFound = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote refusedQuote, out failure);
				int timberAfter = RawTimber(system);
				int waterAfter = KingdomGrowth.CountStoredWater(Zone);
				foreach (GameObject occupant in occupants)
				{
					GameObject.Validate(occupant);
					occupant.Obliterate(null, Silent: true);
				}
				Require(!stillFound && !string.IsNullOrEmpty(failure)
					&& failure.StartsWith("{{C|a living occupant}} stands at ", StringComparison.Ordinal),
					"filling every lawful pose with an occupant did not produce the named refusal: "
					+ KingdomScenarioRules.Bounded(failure));
				Require(timberAfter == timberBefore && waterAfter == waterBefore,
					"a refused all-occupied quote spent timber or water");
				Evidence.Append("; case=all-occupied-no-mutation refused=true timber-unchanged=true"
					+ " water-unchanged=true");
			}

			/// <summary>(3) A creature drifting onto a claimed cell of the SAME rect a quote
			/// already resolved must still refuse at Commission's own preflight, unconditionally
			/// -- this fix never touches that refusal.</summary>
			private void DriftAfterQuotePreflightRefusal(KingdomSystem system, KingdomRules.BuildEntry entry)
			{
				string failure;
				Require(KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote quote, out failure),
					failure ?? "no quote available to prove the drift-after-quote refusal");
				Require(KingdomArchitectureRuntime.TryDecode(quote.Architecture, out var snapshot, out failure),
					failure ?? "the quoted architecture could not be decoded");
				GameObject drifter = null;
				Cell drifterCell = null;
				foreach (ArchitectureCellState state in snapshot.Cells)
				{
					if (!KingdomArchitectureRules.IsClaimed(state.Claim)) continue;
					if (!KingdomArchitectureRuntime.TryWorldCell(snapshot, quote.Architecture.Rect,
						state, out int x, out int y, out string ignored)) continue;
					Cell candidate = Zone.GetCell(x, y);
					if (candidate == null || !candidate.IsPassable()) continue;
					drifter = GameObject.Create("NPC");
					Require(GameObject.Validate(drifter), "the drift probe body produced nothing");
					Owned.Add(drifter);
					Require(ReferenceEquals(candidate.AddObject(drifter, NoStack: true), drifter),
						"native placement substituted the drift probe body");
					drifterCell = candidate;
					break;
				}
				Require(drifter != null, "no claimed walkable cell was found for the drift probe");
				bool committed = KingdomCommission.Commission(system, BuildKey, null,
					KingdomPlotRules.PlotSize.None, quote, out string commitFailure);
				drifterCell.RemoveObject(drifter);
				drifter.Obliterate(null, Silent: true);
				Require(!committed && !string.IsNullOrEmpty(commitFailure)
					&& commitFailure.StartsWith("a living occupant stands on authored ground at ",
						StringComparison.Ordinal),
					"a creature drifting onto the quoted rect between quote and stamp did not "
					+ "refuse at preflight: " + KingdomScenarioRules.Bounded(commitFailure));
				Evidence.Append("; case=drift-after-quote-preflight-refused=true");
			}

			private int RawTimber(KingdomSystem System)
			{
				int total = 0;
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
				foreach (GameObject stockpile in stock.Stockpiles)
				{
					if (!GameObject.Validate(stockpile) || stockpile.Inventory == null) continue;
					foreach (GameObject item in stockpile.Inventory.Objects)
						if (KingdomMaterials.TryOrdinaryMaterialOf(item, out KingdomMaterial kind)
							&& kind == KingdomMaterial.Timber)
							total += KingdomMaterials.RawCensusCountOf(item);
				}
				return total;
			}
		}
	}
}
