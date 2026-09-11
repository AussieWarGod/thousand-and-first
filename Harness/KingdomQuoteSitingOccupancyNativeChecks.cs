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
			else
			{
				Require(Retained != null, "the quote-occupancy check verb arrived before its setup verb");
				Retained.Check();
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
		/// committing never wait on engine turns, unlike construction/strike. Start() runs the
		/// cases but does NOT set Done: setup must still return the plain "intent" receipt so
		/// the check verb's own readback (Provider.cs) is not overwritten before it runs, the
		/// same reason KingdomDepositOverflowNativeChecks reaches Done only on its own last
		/// check. Since all three cases already finished by the time setup returns, the first
		/// Check() call is what flips Done -- idempotent afterward, like every later check
		/// call here.</summary>
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
			}

			/// <summary>Idempotent: the cases already ran in Start(), so every check call --
			/// the first and every one after -- only confirms completion.</summary>
			internal void Check()
			{
				Done = true;
			}

			/// <summary>(1) A quote's first candidate has a synthetic body placed on one of the
			/// resolved snapshot's own MANAGED cells (claimed, via the same TryWorldCell mapping
			/// ResolveArchitecture/TryManagedCells use) -- not an arbitrary rect corner, which may
			/// land on unclaimed margin the fix correctly ignores. The re-quote must strictly
			/// choose a genuinely different, occupant-free alternate: a refusal here is the
			/// over-rejection regression this correction removes, not an acceptable outcome.
			/// Removing the body and quoting a third time must return the original rect.</summary>
			private void OccupiedFirstClearAlternate(KingdomSystem system, KingdomRules.BuildEntry entry)
			{
				string failure;
				Require(KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote firstQuote, out failure),
					failure ?? "the first quote refused with no occupant on the ground");
				KingdomPlotRules.PlotRect firstRect = firstQuote.Rect;
				Require(TryFindManagedCell(firstQuote.Architecture, out int mx, out int my),
					"the first quote's resolved snapshot has no claimed, walkable cell to occupy");
				Cell occupiedCell = Zone.GetCell(mx, my);
				Require(occupiedCell != null, "the first quote's managed cell has no live cell");
				GameObject occupant = GameObject.Create("NPC");
				Require(GameObject.Validate(occupant), "the probe body blueprint produced nothing");
				Owned.Add(occupant);
				Require(ReferenceEquals(occupiedCell.AddObject(occupant, NoStack: true), occupant),
					"native placement substituted the probe body");
				bool secondFound;
				KingdomPlotQuote secondQuote;
				try
				{
					secondFound = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
						KingdomPlotRules.PlotSize.None, out secondQuote, out failure);
				}
				finally
				{
					occupiedCell.RemoveObject(occupant);
					occupant.Obliterate(null, Silent: true);
				}
				bool differs = secondFound
					&& (secondQuote.Rect.X1 != firstRect.X1 || secondQuote.Rect.Y1 != firstRect.Y1);
				bool excludesOccupied = secondFound && !secondQuote.Rect.Contains(mx, my);
				bool alternateChosen = differs && excludesOccupied;
				Require(alternateChosen,
					"occupying a managed cell of the first quote did not select a genuinely "
					+ "different, occupant-free alternate rect (found=" + secondFound + ")");
				bool thirdFound = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
					KingdomPlotRules.PlotSize.None, out KingdomPlotQuote thirdQuote, out failure);
				Require(thirdFound && thirdQuote.Rect.X1 == firstRect.X1 && thirdQuote.Rect.Y1 == firstRect.Y1,
					"the original rect did not return once the probe body was removed");
				Evidence.Append("; case=occupied-first-clear-alternate alternate-chosen=")
					.Append(alternateChosen).Append(" original-rect-restored=true");
			}

			/// <summary>Finds a claimed, walkable world cell from an already-resolved quote's
			/// architecture -- the same decode/IsClaimed/TryWorldCell sequence
			/// TryManagedCells/ResolveArchitecture use, so a body placed here is guaranteed to be
			/// on a cell the fix actually checks.</summary>
			private bool TryFindManagedCell(KingdomArchitectureIntent Architecture, out int X, out int Y)
			{
				X = 0;
				Y = 0;
				string ignored;
				if (!KingdomArchitectureRuntime.TryDecode(Architecture, out var snapshot, out ignored))
					return false;
				foreach (ArchitectureCellState state in snapshot.Cells)
				{
					if (!KingdomArchitectureRules.IsClaimed(state.Claim)) continue;
					if (!KingdomArchitectureRuntime.TryWorldCell(snapshot, Architecture.Rect, state,
						out int x, out int y, out ignored)) continue;
					Cell candidate = Zone.GetCell(x, y);
					if (candidate == null || !candidate.IsPassable()) continue;
					X = x;
					Y = y;
					return true;
				}
				return false;
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
				string failure = null;
				bool stillFound = false;
				int timberBefore = 0;
				int waterBefore = 0;
				int timberAfter = 0;
				int waterAfter = 0;
				try
				{
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
					timberBefore = RawTimber(system);
					waterBefore = KingdomGrowth.CountStoredWater(Zone);
					stillFound = KingdomPlots.TryQuoteCommission(system, Zone, entry, null,
						KingdomPlotRules.PlotSize.None, out KingdomPlotQuote refusedQuote, out failure);
					timberAfter = RawTimber(system);
					waterAfter = KingdomGrowth.CountStoredWater(Zone);
				}
				finally
				{
					foreach (GameObject occupant in occupants)
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
				Require(TryFindManagedCell(quote.Architecture, out int dx, out int dy),
					"the quoted architecture has no claimed, walkable cell for the drift probe");
				Cell drifterCell = Zone.GetCell(dx, dy);
				Require(drifterCell != null, "the drift probe's managed cell has no live cell");
				GameObject drifter = GameObject.Create("NPC");
				Require(GameObject.Validate(drifter), "the drift probe body produced nothing");
				Owned.Add(drifter);
				Require(ReferenceEquals(drifterCell.AddObject(drifter, NoStack: true), drifter),
					"native placement substituted the drift probe body");
				bool committed;
				string commitFailure;
				try
				{
					committed = KingdomCommission.Commission(system, BuildKey, null,
						KingdomPlotRules.PlotSize.None, quote, out commitFailure);
				}
				finally
				{
					drifterCell.RemoveObject(drifter);
					drifter.Obliterate(null, Silent: true);
				}
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
