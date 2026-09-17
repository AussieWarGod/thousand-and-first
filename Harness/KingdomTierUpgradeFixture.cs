using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Setup-only helpers for <see cref="KingdomTierUpgradeChecks"/>: a real founding, a real
	/// water dedication, real resident enrollment, minted raw units in the founding heart's own
	/// authored stockpile, and a REAL production commission of one tent. No assertion about what
	/// the improvement pass later did lives here.
	/// <para>
	/// The tent is commissioned rather than fabricated on purpose: the authored upgrade lane
	/// needs the frozen lot receipts (<c>r_TAF_ArchitectureSchema</c>,
	/// <c>r_TAF_LayoutNextLayer == 3</c>, the stamped plot rect and footprint -
	/// <c>Growth/KingdomUpgrade.15.Prepare.cs:55-58,72-75</c>,
	/// <c>Growth/KingdomArchitectureStamper.UpgradePreflight.cs:82</c>), and a fixture that
	/// stamped those by hand would be proving its own arithmetic rather than production's.
	/// </para>
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		/// <summary>Stateful anchor identity of the founding heart's authored stockpile.</summary>
		internal const string StorageRole = "fixture:storage";

		/// <summary>Drams dedicated up front: enough for the tent commission, the two-dram
		/// improvement, and the three days of drinking the reserve holds back.</summary>
		internal const int DedicatedDrams = 400;

		/// <summary>Residents enrolled so the improvement's one free hand is really free.</summary>
		internal const int ResidentCount = 4;

		internal const int MintedBrushUnits = 24;
		internal const int MintedTimberUnits = 4;
		internal const string FixtureOrigin = "tier upgrade fixture";

		private sealed partial class Frame
		{
			private readonly List<GameObject> Owned = new List<GameObject>();
			private readonly List<string> MintedBrushIds = new List<string>();
			internal GameObject Heart, Store;

			/// <summary>Fixture-owned brush units withdrawn from the store to make the material
			/// shortage exact. Disclosed; every one of these is a minted fixture body.</summary>
			private readonly List<GameObject> Withheld = new List<GameObject>();

			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding did not claim this ground");
				KingdomNativeCampFounding.Dedicate(Game, Zone, System, DedicatedDrams,
					Owned.Add, Require);
				BindHeartAndStore();
				EnrollResidents();
				MintStoreContents();
				SuppressFirstNotice();
				CommissionTent();
				Armed = true;
				Phase = 1;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; zone=").Append(Zone.ZoneID)
					.Append("; store=").Append(Store.IDIfAssigned)
					.Append("; population=").Append(System.Population)
					.Append("; stored-water=").Append(KingdomGrowth.CountStoredWater(Zone))
					.Append("; minted-brush=").Append(MintedBrushUnits)
					.Append("; minted-timber=").Append(MintedTimberUnits);
			}

			/// <summary>The standing founding heart through the production survey, and its
			/// authored stockpile through the production anchored-component resolver.</summary>
			private void BindHeartAndStore()
			{
				KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
				Require(survey != null, "the settlement could not be surveyed after founding");
				for (int i = 0; i < survey.Built.Count; i++)
				{
					GameObject item = survey.Built[i];
					if (!GameObject.Validate(item)
						|| item.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1) continue;
					Require(Heart == null, "more than one heart plot stands on this ground");
					Heart = item;
				}
				Require(Heart != null, "no standing heart plot was surveyed");
				GameObject store;
				string failure;
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(Heart, Zone,
					StorageRole, out store, out failure), failure ?? "no anchored camp store");
				Store = store;
				Require(GameObject.Validate(Store) && Store.Inventory != null
					&& Store.GetIntProperty("KingdomStockpile") == 1,
					"the anchored camp store is not a dedicated stockpile");
				Require(Store.Inventory.Objects.Count == 0,
					"the founding camp store is not empty before the fixture fills it");
			}

			/// <summary>Real enrollment through the production citizenship and roster APIs. The
			/// born stamp is a DISCLOSED synthetic input: <c>TryEnroll</c> does not supply the
			/// provenance <c>KingdomResidents.Enrollable</c> requires, and the body is PLACED on
			/// claimed ground before its row is published.</summary>
			private void EnrollResidents()
			{
				long tick = Game.TimeTicks;
				for (int i = 0; i < ResidentCount; i++)
				{
					GameObject body = Create("NPC");
					Require(body.Brain != null && body.Body != null && body.IsAlive
						&& !body.IsPlayer(), "a fresh NPC lacks eligible physical shape");
					body.SetStringProperty("Species", "human");
					body.SetStringProperty("KingdomOrigin", FixtureOrigin);
					string failure;
					Require(KingdomCitizenship.TryEnroll(System, body,
						KingdomCitizenshipEnrollmentReason.Arrival, tick, out failure), failure);
					body.SetIntProperty("KingdomBorn", 1);
					string name = "tier upgrade fixture resident " + (i + 1);
					body.GiveProperName(name, Force: true);
					body.SetStringProperty("KingdomName", name);
					Cell cell = KingdomNativeCampFounding.Clear(Zone);
					Require(cell != null, "no clear cell for a fixture resident");
					Require(ReferenceEquals(cell.AddObject(body, NoStack: true), body),
						"native placement substituted a resident");
					Require(!body.IsPlayer() && !body.IsPlayerLed(),
						"a player-led body is refused by the roster gate");
					KingdomCityBook book;
					int id;
					Require(KingdomResidents.TryEnsureRow(System, body, FixtureOrigin, null, tick,
						out book, out id) && ReferenceEquals(book, System.City) && id > 0,
						"native enrollment did not publish an exact row and binding");
				}
				Require(Game.TimeTicks == tick,
					"fixture enrollment advanced the world clock");
				Require(System.Population >= ResidentCount,
					"the enrolled residents are not counted by the settlement");
			}

			private void MintStoreContents()
			{
				Mint(KingdomMaterial.Brush, MintedBrushUnits, MintedBrushIds);
				Mint(KingdomMaterial.Timber, MintedTimberUnits, null);
			}

			private void Mint(KingdomMaterial Material, int Units, List<string> Ids)
			{
				string blueprint = KingdomMaterials.BlueprintFor(Material);
				Require(!string.IsNullOrEmpty(blueprint),
					"no production blueprint for material " + Material);
				for (int i = 0; i < Units; i++)
				{
					GameObject unit = Create(blueprint);
					string id = unit.ID;
					Require(!string.IsNullOrEmpty(id) && unit.IDIfAssigned == id,
						"a fresh fixture unit did not acquire its engine identity");
					GameObject accepted = Store.Inventory.AddObject(unit, null,
						Silent: true, NoStack: true);
					Require(ReferenceEquals(accepted, unit),
						"a minted unit did not land as its own physical unit in the camp store");
					if (Ids != null) Ids.Add(id);
				}
			}

			/// <summary>Pre-marks the once-per-game modal first notice as given. DISCLOSED
			/// synthetic input: <c>Growth/KingdomUpgrade.13.Resolve.cs:90</c> returns without
			/// beginning anything while <c>GiveFirstNotice</c> can still fire, and that helper
			/// shows a <c>Popup</c> a sealed script cannot answer. This buys the first ready pass
			/// the right to begin; it asserts nothing about production behaviour.</summary>
			private void SuppressFirstNotice()
			{
				Game.SetIntGameState(KingdomUpgrade.NoticedState, 1);
				Require(Game.GetIntGameState(KingdomUpgrade.NoticedState) == 1,
					"the first-notice suppression did not persist");
			}

			/// <summary>One REAL commission through the production plot-commission API - the same
			/// call <c>Harness/KingdomCampHeartNativeClaim.cs:48</c> makes.</summary>
			private void CommissionTent()
			{
				KingdomRules.BuildEntry entry;
				Require(KingdomData.TryGetBuilding(FromKey, out entry) && entry != null,
					"the catalogue has no '" + FromKey + "' design");
				string failure;
				Require(KingdomPlots.Commission(System, Zone, entry, null, out failure),
					"the production tent commission refused: "
						+ KingdomScenarioRules.Bounded(failure));
				List<KingdomConstructionJob> jobs;
				Require(KingdomConstruction.TryRead(out jobs, out failure), failure);
				KingdomConstructionJob found = null;
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (job == null || job.TargetKey != FromKey
						|| job.ZoneId != Zone.ZoneID) continue;
					Require(found == null, "the camp has more than one tent job");
					found = job;
				}
				Require(found != null && found.Route == KingdomConstructionRoute.PlotCommission
					&& KingdomConstruction.Owns(System, Zone, found),
					"the tent commission published no owned plot-commission job");
				Evidence.Append("\ncommissioned tent job=").Append(found.Id)
					.Append("; route=").Append(found.Route)
					.Append("; due=").Append(found.DueTick);
			}

			private GameObject Create(string Blueprint)
			{
				GameObject created = GameObject.Create(Blueprint);
				Require(GameObject.Validate(created) && created.Blueprint == Blueprint
					&& created.Count == 1 && created.CurrentCell == null
					&& created.InInventory == null, Blueprint + " produced no fresh custody");
				created.SetIntProperty("NoLoot", 1);
				Owned.Add(created);
				return created;
			}
		}
	}
}
