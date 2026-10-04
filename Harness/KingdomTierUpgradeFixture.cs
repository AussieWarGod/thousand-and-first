using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Setup-only helpers for <see cref="KingdomTierUpgradeChecks"/>: a real founding, the real
	/// rung-one completion of its founding heart, a real water dedication, real resident
	/// enrollment, minted raw units in the founding heart's own authored stockpile, and a REAL
	/// production commission of one tent. No assertion about what the improvement pass later did
	/// lives here.
	/// <para>
	/// THE HEART IS COMPLETED BEFORE ANYTHING BINDS IT. Straight after the founding transaction
	/// the founding heart is only a Staked works without <c>KingdomBuilt</c>, so the survey
	/// lists it as no built work (<c>Growth/KingdomSurvey.01.Capture.cs</c>), and its authored
	/// <c>fixture:storage</c> stockpile exists only on the final layout
	/// (<c>Growth/KingdomPlot2.27.FinalBuilding.cs</c>). Setup therefore completes rung one
	/// through the shared <see cref="KingdomScenarioCompletedHeart"/> helper, as the
	/// natively-run camp-heart fixture does. DISCLOSED synthetic parts of that helper: the
	/// founder's westward walk off the heart's footprint by ordinary movement, and the
	/// explicit future calendar frontier it hands the production labour driver for the
	/// heart's own works. No rung state is stamped by hand, and the helper never touches the tent.
	/// </para>
	/// <para>
	/// The tent is commissioned rather than fabricated on purpose: the authored upgrade lane
	/// needs the frozen lot receipts (<c>r_TAF_ArchitectureSchema</c>,
	/// <c>r_TAF_LayoutNextLayer == 3</c>, the stamped plot rect and footprint -
	/// <c>Growth/KingdomUpgrade.15.Prepare.cs:55-58,72-75</c>,
	/// <c>Growth/KingdomArchitectureStamper.UpgradePreflight.cs:82</c>), and a fixture that
	/// stamped those by hand would be proving its own arithmetic rather than production's.
	/// </para>
	/// <para>
	/// THE SHORTAGE IS HELD FROM SETUP. The store is minted with the tent's own canvas bill plus
	/// the upgrade's canvas bill less <see cref="UpgradeShortBy"/>. The commission pays the tent
	/// out of that store, so from the first settlement pass the upgrade is exactly one unit short
	/// and no real pass can begin it, however early the tent completes. A pass may begin the
	/// improvement in the same pass the tent completes (the commission job is terminal by then),
	/// so an idle standing tent between two passes is only guaranteed this way. Nothing is
	/// withheld across turns; the missing unit is minted only by the shortfall leg.
	/// </para>
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		/// <summary>Stateful anchor identity of the founding heart's authored stockpile.</summary>
		internal const string StorageRole = "fixture:storage";

		/// <summary>Drams dedicated up front: enough for the tent commission, the two-dram
		/// improvement, and the three days of drinking the reserve holds back.</summary>
		internal const int DedicatedDrams = 400;

		/// <summary>Residents enrolled, the count the natively-run camp-heart fixture uses:
		/// fixture bodies can leave the zone or die during the first ordinary day (camp-heart
		/// native run ea2bf92d kept two of six in the zone), and the raising gang wants two.
		/// </summary>
		internal const int ResidentCount = 6;

		/// <summary>Canvas units the upgrade bill is left short by once the tent is paid.</summary>
		internal const int UpgradeShortBy = 1;

		internal const int MintedTimberUnits = 4;
		internal const string FixtureOrigin = "tier upgrade fixture";

		private sealed partial class Frame
		{
			internal GameObject Heart, Store;

			/// <summary>Canvas units minted at setup, derived from the production bills.</summary>
			internal int MintedBrushUnits;

			internal void Start()
			{
				System = KingdomNativeCampFounding.Found(Game, Zone, Require);
				Require(System.ClaimedZones.Contains(Zone.ZoneID),
					"the real founding did not claim this ground");
				// Real rung-one completion through the production plot works; see the class
				// summary for why it comes first and for its two disclosed synthetic parts.
				KingdomScenarioCompletedHeart.Complete(Game, System, Zone);
				Require(KingdomPlots.HeartRung(Zone) == 1,
					"the completed rite ground does not stand at rung one");
				KingdomNativeCampFounding.Dedicate(Game, Zone, System, DedicatedDrams,
					Ignore, Require);
				BindHeartAndStore();
				EnrollResidents();
				MintStoreContents();
				SuppressFirstNotice();
				CommissionTent();
				RequireExactShortfall();
				Armed = true;
				Phase = 1;
				Cell founder = The.Player?.CurrentCell;
				Evidence.Append("\nfounded tick=").Append(Game.TimeTicks)
					.Append("; zone=").Append(Zone.ZoneID)
					.Append("; heart=").Append(Heart.IDIfAssigned)
					.Append("; heart-rung=").Append(KingdomPlots.HeartRung(Zone))
					.Append("; founder-cell=").Append(founder == null ? "absent"
						: founder.X + "," + founder.Y)
					.Append("; store=").Append(Store.IDIfAssigned)
					.Append("; population=").Append(System.Population)
					.Append("; stored-water=").Append(KingdomGrowth.CountStoredWater(Zone))
					.Append("; minted-brush=").Append(MintedBrushUnits)
					.Append("; minted-timber=").Append(MintedTimberUnits)
					.Append("; upgrade-short-by=").Append(UpgradeShortBy);
			}

			private static void Ignore(GameObject Vessel) { }

			/// <summary>The standing (completed) founding heart through the production survey,
			/// confirmed by its production design key, and its authored stockpile through the
			/// production anchored-component resolver.</summary>
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
				Require(KingdomUpgrade.DesignKeyOf(Heart) == FoundingHeartKey,
					"the standing heart is not the authored first rung");
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

			/// <summary>The tent's canvas bill plus the upgrade's, less the exact shortage, read
			/// from the production registry so a catalogue edit cannot desynchronise the boundary.
			/// </summary>
			private void MintStoreContents()
			{
				int tentBrush = KingdomMaterials.CostFor(FromKey).Get(KingdomMaterial.Brush);
				int upgradeBrush = KingdomMaterials.UpgradeCostFor(FromKey)
					.Get(KingdomMaterial.Brush);
				Require(tentBrush > 0 && upgradeBrush > UpgradeShortBy,
					"the catalogue bills leave no exact canvas shortage to hold");
				MintedBrushUnits = tentBrush + upgradeBrush - UpgradeShortBy;
				Mint(KingdomMaterial.Brush, MintedBrushUnits);
				Mint(KingdomMaterial.Timber, MintedTimberUnits);
			}

			private void Mint(KingdomMaterial Material, int Units)
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
				}
			}

			/// <summary>Pre-marks the once-per-game first notice as given. DISCLOSED synthetic
			/// input. Under the sealed script the runner's <c>Popup.Suppress</c> auto-acknowledges
			/// the modal, but <c>GiveFirstNotice</c> still returns true and
			/// <c>Growth/KingdomUpgrade.13.Resolve.cs:90</c> returns before <c>Begin</c>, so the
			/// first ready pass would only tell. Pre-marking keeps the begin on that pass; it
			/// asserts nothing about production behaviour.</summary>
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
				TentJobId = found.Id;
				Evidence.Append("\ncommissioned tent job=").Append(found.Id)
					.Append("; route=").Append(found.Route)
					.Append("; due=").Append(found.DueTick);
			}

			/// <summary>The commission paid its bill out of this store, and what is left is the
			/// upgrade bill less exactly <see cref="UpgradeShortBy"/> canvas units.</summary>
			private void RequireExactShortfall()
			{
				string missing;
				Require(!KingdomMaterials.CanPayUpgrade(Zone, FromKey, out missing),
					"the upgrade bill is already covered after the tent commission");
				int left = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Require(left == KingdomMaterials.UpgradeCostFor(FromKey).Get(KingdomMaterial.Brush)
					- UpgradeShortBy, "the commission did not leave the upgrade exactly "
						+ UpgradeShortBy + " canvas short: " + left + " left");
				Evidence.Append("\nstore-brush-after-commission=").Append(left)
					.Append("; missing=").Append(KingdomScenarioRules.Bounded(missing));
			}

			private GameObject Create(string Blueprint)
			{
				GameObject created = GameObject.Create(Blueprint);
				Require(GameObject.Validate(created) && created.Blueprint == Blueprint
					&& created.Count == 1 && created.CurrentCell == null
					&& created.InInventory == null, Blueprint + " produced no fresh custody");
				created.SetIntProperty("NoLoot", 1);
				return created;
			}
		}
	}
}
