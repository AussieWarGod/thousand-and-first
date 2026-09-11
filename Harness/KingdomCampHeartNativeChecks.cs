using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine witness for PR #107 native case 2: the camp heart's dedicated store, its contents
	/// and the camp fire across a REAL PAID rung-1 to rung-2 improvement. Every assertion after
	/// setup reads what the REAL settlement pass did on the turns the persona's <c>advance</c>
	/// spends; this seam never begins, funds, advances or applies the upgrade itself.
	/// <para>
	/// SYNTHETIC SETUP, DISCLOSED. Real founding, real rung-1 completion, six really enrolled
	/// residents, a dedicated reservoir with minted drams, and forty-eight minted physical units
	/// inside the authored camp stockpile. Not ordinary play, not a rendered Charter, not
	/// save/load, and not overall camp acceptance.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal const string StoreBlueprint = "r_KingdomHeartStockpile";
		internal const string FireBlueprint = "r_KingdomCivicCampfireCamp";
		internal const string StorageRole = "fixture:storage";
		internal const string FirstRungKey = "heartbasin";
		internal const string SecondRungKey = "heartwaterstone";
		internal const string ThirdRungKey = "heartmoot";
		internal const string FixtureOrigin = "native camp heart fixture";
		internal const int ResidentCount = 6;
		internal const int DedicatedDrams = 400;
		internal const int MintedStoneUnits = 24;
		internal const int MintedTimberUnits = 1;
		internal const int MintedBrushUnits = 23;
		// The authored rung 2 -> 3 bill, RuntimeData/KingdomBuildings.xml:646
		// (heartwaterstone UpgradeMaterials="timber:17,stone:2,shapedtimber:6"). Minted at the
		// phase-2 boundary, after the rung-2 bill has left the store, so the 48-unit declared
		// capacity (r_KingdomStockpileCapacity) is met exactly and never exceeded.
		internal const int Rung3TimberUnits = 17;
		internal const int Rung3StoneUnits = 2;
		internal const int Rung3ShapedTimberUnits = 6;
		// The moot yard asks for a Town (KingdomBuildings.xml:652 MinStage="Town"), which is
		// twenty-five people over 256 drams of dedicated capacity
		// (Core/KingdomRules.TradeAndGrowth.cs:153), and for the workshop craft level, which is
		// five tech points (Growth/KingdomZoningRules.cs:237 thresholds, one point per disk).
		internal const int TownResidentCount = 25;
		internal const int TownDedicatedDrams = 1920;
		internal const int WorkshopDisks = 5;
		// The rung-3 run leaves ROOM: production's own keepers deposit what the settlement's yard
		// work makes into this same dedicated stockpile while a rung is built, and native run 5
		// found 46 of 48 units standing at the phase-2 boundary. Two unasked units still witness
		// "the bill took only what it asked for" without filling the store the next bill needs.
		internal const int Rung3UnaskedBrushUnits = 2;
		/// <summary>The at-most-once 0/1/2 rung-effect marker production sets on the successor of
		/// each climb, declared private at Growth/KingdomPlot2.03.RegistryAndDeclarations.cs and
		/// read here by its exact name. The DevTests pin holds the two spellings together.
		/// </summary>
		internal const string HeartEffectProperty = "r_TAF_ConstructionHeartEffect";
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, int TargetRung,
			out bool Complete)
		{
			Complete = false;
			Require(TargetRung == 2 || TargetRung == 3,
				"the sealed camp heart script names no known target rung");
			if (Verb == KingdomCampHeartNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a camp heart attempt is already retained");
				Retained = new Frame(Game, Zone, TargetRung);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "camp heart setup is absent");
				Require(Retained.TargetRung == TargetRung,
					"the sealed script changed its target rung mid-run");
				Retained.Check();
			}
			Complete = Retained.Done;
			return (Complete ? "native-camp-heart cases=1 passed=1 failed=0"
				: "native-camp-heart phase=" + Retained.Phase)
				+ "; synthetic-camp=true; synthetic-residents=true; synthetic-store-contents=true"
				+ "; synthetic-drams=true; synthetic-born-provenance=true"
				+ "; synthetic-material-identities=true"
				+ "; target-rung=" + Retained.TargetRung
				+ "; synthetic-rung3-bill=" + (Retained.TargetRung >= 3)
				+ "; synthetic-craft-disks=" + (Retained.TargetRung >= 3)
				+ "; synthetic-town-works=" + (Retained.TargetRung >= 3)
				+ "; synthetic-stage-derived=" + (Retained.TargetRung >= 3)
				+ "; synthetic-water-identity=true"
				+ "; improvement-notice-premarked=true"
				+ "; stockpile-refusal-reason-claimed=false"
				+ "; ordinary-acceptance=false; charter=untested; save-load=untested"
				+ Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-camp-heart cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomCampHeartNativeProvider.Require(Value, Failure);
		}

		private sealed partial class Frame
		{
			internal readonly XRLGame Game;
			internal readonly Zone Zone;
			/// <summary>The rung this sealed run climbs to: 2 or 3.</summary>
			internal readonly int TargetRung;
			internal readonly List<GameObject> Owned = new List<GameObject>();
			internal KingdomSystem System;
			internal GameObject Heart;
			internal string HeartId;
			internal GameObject Store;
			internal string StoreId;
			internal Cell StoreCell;
			internal GameObject Fire;
			internal string FireId;
			internal Cell FireCell;
			internal readonly List<string> MintedStone = new List<string>();
			internal readonly List<string> MintedTimber = new List<string>();
			internal readonly List<string> MintedBrush = new List<string>();
			internal readonly List<string> MintedRung3 = new List<string>();
			/// <summary>The body rung two settled on, kept so the second consecutive climb can be
			/// told from the first by its own at-most-once marker.</summary>
			internal GameObject SecondStanding;
			internal string SecondHeartId;
			internal int BeforeWater;
			internal List<KingdomCampHeartNativeCensus.Unit> RetainedBrush;
			internal List<GameObject> RetainedBrushBodies;
			internal bool Begun;
			internal bool Armed, Done;
			internal int Phase;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone, int TargetRung)
			{
				this.Game = Game;
				this.Zone = Zone;
				this.TargetRung = TargetRung;
			}

			/// <summary>People this run enrolls: the moot yard asks for a Town, the waterstone
			/// only for a Steading.</summary>
			internal int Residents
			{
				get { return TargetRung >= 3 ? TownResidentCount : ResidentCount; }
			}

			/// <summary>Drams dedicated at setup: a Town of twenty-five drinks 45 a day
			/// (Core/KingdomRules.Economy.cs:76,83), and the run spends twelve thousand turns.
			/// </summary>
			internal int Drams
			{
				get { return TargetRung >= 3 ? TownDedicatedDrams : DedicatedDrams; }
			}

			/// <summary>Units the rung-2 bill never asks for, minted so the store can be proved
			/// untouched where the bill did not reach. Fewer on the rung-3 run, which needs the
			/// room for the next authored bill.</summary>
			internal int Unasked
			{
				get { return TargetRung >= 3 ? Rung3UnaskedBrushUnits : MintedBrushUnits; }
			}

			/// <summary>Real founding, real rung-1 rite-ground completion, six really enrolled
			/// residents, one really dedicated reservoir, and forty-eight minted physical units in
			/// the authored camp stockpile - enough that the settlement can genuinely afford the
			/// authored rung-2 bill out of its own store when the real pass assesses the heart.
			/// </summary>
			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, RequirePair);
				Require(System != null && System.Founded && System.Population == 0
					&& System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding is not an empty claimed camp");
				// Real rung-1 completion through the production plot works. The only synthetic part
				// is the explicit future calendar frontier the shared helper hands the labour driver;
				// no rung state is stamped by hand, and nothing below rung one is skipped.
				KingdomScenarioCompletedHeart.Complete(Game, System, Zone);
				Require(KingdomPlots.HeartRung(Zone) == 1,
					"the completed rite ground does not stand at rung one");
				EnrollResidents();
				Require(System.Population == Residents,
					"enrollment did not reach the fixture population");
				TeachCraftIfOwed();
				GameObject ownedVessel = null;
				string waterId = null;
				var water = KingdomNativeCampFounding.Dedicate(Game, Zone, System, Drams,
					delegate(GameObject item)
					{
						Owned.Add(item);
						Require(GameObject.Validate(item) && item.CurrentCell == null
							&& item.Physics != null && item.Physics.InInventory == null,
							"the synthetic water vessel is not a fresh unplaced body");
						ownedVessel = item;
						waterId = item.ID;
						Require(!string.IsNullOrEmpty(waterId) && item.IDIfAssigned == waterId,
							"the synthetic water vessel has no stable assigned identity");
					}, RequirePair);
				Require(GameObject.Validate(ownedVessel) && water != null
					&& ReferenceEquals(water.ParentObject, ownedVessel)
					&& ownedVessel.IDIfAssigned == waterId && ownedVessel.CurrentZone == Zone,
					"water dedication changed its exact body, assigned identity or ground");
				BindHeart();
				ProveStructuralOccupancy();
				BindStoreAndFire();
				MintStoreContents();
				SeedTownIfOwed();
				// The founder's one-time modal notice would stall a headless run. Marking it read is
				// production game state a founder who has already seen it once carries anyway.
				Game.SetIntGameState(KingdomUpgrade.NoticedState, 1);
				Require(Game.GetIntGameState(KingdomUpgrade.NoticedState) == 1,
					"the improvement notice mark failed its readback");
				RecordBefore();
				ProveFounderAndWalkClear();
				Armed = true;
				Phase = 1;
			}
		}
	}
}
