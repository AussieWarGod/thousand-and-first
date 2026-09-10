using System;
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

		private sealed partial class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private KingdomSystem System;
			private GameObject Heart;
			private int RiteX, RiteY;
			private KingdomPlotRules.PlotRect HeartRect;
			private GameObject StockpileContainer;
			private GameObject Canvas;
			private GameObject[] Eligible; // three eligible wild plants
			private GameObject Tree, Owned, Food, Protected, PlotPlant, ExtraPlant, FinalPlant;
			private int BrushBefore;
			internal bool Armed, Done;
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

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
				KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
				Require(survey != null, "the settlement could not be surveyed after founding");
				foreach (GameObject item in survey.ForagePlots)
					if (GameObject.Validate(item)
						&& item.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1) Heart = item;
				Require(Heart != null, "no heart plot root was found");
				Require(KingdomPlots.TryReadRect(Heart, out HeartRect),
					"the heart plot's rect could not be read");
				KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
				Require(stock.Stockpiles.Count > 0,
					"the #107 heart stockpile is missing -- forage has nowhere to deposit brush");
				StockpileContainer = stock.Stockpiles[0];
				Canvas = FindCanvas();
				Require(Canvas != null,
					"no camp canvas wall stands near the rite -- the #107 horseshoe is missing");
				BrushBefore = stock.Tally.Get(KingdomMaterial.Brush);
				PlantExclusions();
				Armed = true;
				Phase = 1;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; population=").Append(System.Population)
					.Append("; rite=(").Append(RiteX).Append(',').Append(RiteY).Append(')')
					.Append("; stockpile=").Append(StockpileContainer.IDIfAssigned)
					.Append("; canvas=").Append(Canvas.IDIfAssigned)
					.Append("; brush before=").Append(BrushBefore)
					.Append("; eligible=").Append(EvidenceOf(Eligible))
					.Append("; tree=").Append(EvidenceOf(Tree))
					.Append("; owned=").Append(EvidenceOf(Owned))
					.Append("; food=").Append(EvidenceOf(Food))
					.Append("; protected=").Append(EvidenceOf(Protected))
					.Append("; plot=").Append(EvidenceOf(PlotPlant));
			}
		}
	}
}
