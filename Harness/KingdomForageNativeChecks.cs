using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine witness for the brush-forage duty (<c>Growth/KingdomMaterials.16.ForageWork.cs</c>,
	/// issue #44). Every assertion reads outcomes the REAL settlement-pass cadence produced on
	/// real turns the persona's <c>advance</c> spends -- this seam never calls the private
	/// <c>WorkForage</c> directly and never realizes a scenario.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. The fixture founds a real camp, enrolls four real residents,
	/// and plants one real object per exclusion category plus wild plants near the rite. This
	/// does not sign a rendered Charter, ordinary play, save/load, or overall camp acceptance.
	/// </para>
	/// </summary>
	internal static partial class KingdomForageNativeChecks
	{
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomForageNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a forage attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "forage setup is absent");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-forage cases=1 passed=1 failed=0"
				: "native-forage phase=" + Retained.Phase)
				+ "; synthetic-camp=true; synthetic-plants=true; synthetic-reservation=true"
				+ "; ordinary-acceptance=false; charter=untested; save-load=untested"
				+ Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-forage cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomForageNativeProvider.Require(Value, Failure);
		}

		/// <summary>One exclusion object's identity, position, custody and category fact,
		/// bound at setup so a recheck proves EVERY fact individually rather than trusting a
		/// single <c>Validate()</c>.</summary>
		private sealed class ExclusionSnapshot
		{
			internal readonly GameObject Item;
			internal readonly string Label;
			internal readonly string Id;
			internal readonly int X, Y;
			internal readonly string ZoneId;
			internal readonly string Owner;
			internal readonly int Count;
			internal readonly Func<GameObject, bool> Fact;

			internal ExclusionSnapshot(GameObject Item, string Label, Func<GameObject, bool> Fact)
			{
				this.Item = Item; this.Label = Label; this.Fact = Fact;
				Require(GameObject.Validate(Item),
					Label + " fixture object failed to validate at setup");
				Id = Item.IDIfAssigned;
				Require(Item.CurrentCell != null,
					Label + " fixture object has no cell at setup");
				X = Item.CurrentCell.X; Y = Item.CurrentCell.Y;
				Require(Item.CurrentZone != null,
					Label + " fixture object has no zone at setup");
				ZoneId = Item.CurrentZone.ZoneID;
				Owner = Item.GetPart<Physics>()?.Owner;
				Count = Item.Count;
				Require(Fact(Item),
					Label + " fixture object does not carry its exclusion fact at setup");
			}
		}

		/// <summary>One reserved-marker brush unit's identity and the exact marker value it was
		/// stamped with, bound at creation.</summary>
		private sealed class ReservedBrush
		{
			internal readonly GameObject Item;
			internal readonly string Id;
			internal readonly string Marker;

			internal ReservedBrush(GameObject Item, string Marker)
			{
				this.Item = Item; this.Marker = Marker;
				Require(GameObject.Validate(Item),
					"reserved brush failed to validate at creation");
				Id = Item.IDIfAssigned;
			}
		}

		private sealed partial class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private KingdomSystem System;
			private KingdomSurvey Survey;
			private GameObject Heart;
			private int RiteX, RiteY;
			private KingdomPlotRules.PlotRect HeartRect;
			private readonly List<KingdomForageNativeGeometry.Rect> PlotRects = new();
			private readonly HashSet<Cell> UsedOutdoorCells = new();
			private GameObject StockpileContainer;
			private GameObject[] Eligible; // three eligible wild plants
			private ExclusionSnapshot TreeSnap, OwnedSnap, FoodSnap, ProtectedSnap, PlotSnap, CanvasSnap;
			private GameObject ExtraPlant, FinalPlant;
			private readonly List<ReservedBrush> Reserved = new();
			private int RawBefore, TallyBefore;
			private int NotesBaseline = -1, NotesAfterFirstExhaustion = -1;
			private int RawAtCeiling, TallyAtCeiling;
			internal bool Armed, Done;
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

			private const string ExhaustionMarker =
				"is cut out. There is nothing left here to bind canvas from.";

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			/// <summary>Real founding (the #107 camp: canvas horseshoe and heart stockpile), four
			/// really-enrolled residents, and one planted object per exclusion category.</summary>
			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.Population == 0 && System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding is not an empty claimed camp");
				EnrollFour();
				Require(System.Population == 4, "enrollment did not reach population four");
				Require(KingdomPlots.TryRiteGround(Zone, out RiteX, out RiteY),
					"the founded camp has no provable rite ground");
				Survey = KingdomSurvey.Take(Zone, System);
				Require(Survey != null, "the settlement could not be surveyed after founding");
				foreach (GameObject item in Survey.ForagePlots)
					if (GameObject.Validate(item)
						&& item.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1) Heart = item;
				Require(Heart != null, "no heart plot root was found");
				Require(KingdomPlots.TryReadRect(Heart, out HeartRect),
					"the heart plot's rect could not be read");
				CollectPlotRects();
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
				Require(stock.Stockpiles.Count > 0,
					"the #107 heart stockpile is missing -- forage has nowhere to deposit brush");
				StockpileContainer = stock.Stockpiles[0];
				GameObject canvas = FindCanvas();
				Require(canvas != null,
					"no camp canvas wall stands near the rite -- the #107 horseshoe is missing");
				RawBefore = CensusBrushRaw(StockpileContainer);
				TallyBefore = stock.Tally.Get(KingdomMaterial.Brush);
				Require(RawBefore == 0, "the fresh stockpile already holds physical brush: " + RawBefore);
				Require(TallyBefore == 0, "the fresh stockpile's available tally is not zero: " + TallyBefore);
				CanvasSnap = new ExclusionSnapshot(canvas, "camp canvas",
					item => item.GetTag("BodyType") == "ClothWall");
				PlantExclusions();
				Armed = true;
				Phase = 1;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; population=").Append(System.Population)
					.Append("; rite=(").Append(RiteX).Append(',').Append(RiteY).Append(')')
					.Append("; stockpile=").Append(StockpileContainer.IDIfAssigned)
					.Append("; canvas=").Append(CanvasSnap.Id)
					.Append("; raw before=").Append(RawBefore).Append("; tally before=").Append(TallyBefore)
					.Append("; eligible=").Append(EvidenceOf(Eligible))
					.Append("; tree=").Append(EvidenceOf(TreeSnap.Item))
					.Append("; owned=").Append(EvidenceOf(OwnedSnap.Item))
					.Append("; food=").Append(EvidenceOf(FoodSnap.Item))
					.Append("; protected=").Append(EvidenceOf(ProtectedSnap.Item))
					.Append("; plot=").Append(EvidenceOf(PlotSnap.Item));
			}
		}
	}
}
