using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Setup-only helpers for <see cref="KingdomCampHeartNativeChecks"/>: real resident
	/// enrollment, production resolution of the authored camp store and fire, and the minted
	/// physical units the real rung-2 bill is later drawn from. No assertion about what the
	/// upgrade did lives here; this shard only builds the ground the phases shard reads.</summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>Six real NPC residents, enrolled through the production citizenship and
			/// roster APIs - the same path a genuine arrival takes. Six is the smallest population
			/// that reaches the Steading gate the waterstone asks for while drinking little enough
			/// that a 400-dram store outlasts the whole build.
			/// <para>
			/// ORDER AND PROVENANCE ARE LOAD-BEARING, AND THE SYNTHETIC PARTS ARE DISCLOSED.
			/// Production's roster gate (<c>KingdomResidents.Enrollable</c>,
			/// <c>Simulation/City/KingdomResidents.06.Helpers.cs:156-163</c>) requires citizenship,
			/// <c>KingdomBorn == 1</c>, not-a-player and not-player-led;
			/// <c>KingdomCitizenship.TryEnroll</c> supplies only the first. The born stamp and the
			/// name are therefore explicit synthetic fixture inputs, not a real arrival, and the
			/// body is PLACED on claimed ground before its row is published. The published row is
			/// then read BACK through the book - its resident id and its bound zone - and nothing
			/// here writes book state. Proved live 2026-09-10: the forage seam's first native run
			/// refused at setup with the opposite order.
			/// </para></summary>
			private void EnrollResidents()
			{
				long tick = Game.TimeTicks;
				for (int i = 0; i < ResidentCount; i++)
				{
					GameObject body = Create("NPC");
					Require(body.Brain != null && body.Body != null && body.IsAlive
						&& !body.IsPlayer(),
						"taf-camp-resident-shape: fresh NPC lacks eligible physical shape");
					body.SetStringProperty("Species", "human");
					body.SetStringProperty("KingdomOrigin", FixtureOrigin);
					string failure;
					Require(KingdomCitizenship.TryEnroll(System, body,
						KingdomCitizenshipEnrollmentReason.Arrival, tick, out failure), failure);
					Require(KingdomCitizenship.BelongsTo(System, body),
						"taf-camp-resident-uncitizened: the fixture lost citizenship authority");
					// SYNTHETIC BORN PROVENANCE, DISCLOSED. TryEnroll does not supply the born
					// provenance the resident roster requires.
					body.SetIntProperty("KingdomBorn", 1);
					Require(body.GetIntProperty("KingdomBorn") == 1,
						"taf-camp-resident-unborn: synthetic born provenance did not persist");
					string name = "camp heart fixture resident " + (i + 1);
					body.GiveProperName(name, Force: true);
					body.SetStringProperty("KingdomName", name);
					Cell cell = KingdomNativeCampFounding.Clear(Zone);
					Require(cell != null,
						"taf-camp-resident-noground: no clear cell for a fixture resident");
					Require(ReferenceEquals(cell.AddObject(body, NoStack: true), body),
						"taf-camp-resident-substituted: native placement substituted a resident");
					Require(ReferenceEquals(body.CurrentZone, Zone)
						&& ReferenceEquals(body.CurrentCell, cell)
						&& System.ClaimedZones.Contains(Zone.ZoneID),
						"taf-camp-resident-unplaced: the resident lacks exact placed claimed "
							+ "ground before its row is published");
					// The last two clauses of the production roster gate, re-proved here so a
					// refusal names the reason rather than arriving as an empty roll.
					Require(!body.IsPlayer() && !body.IsPlayerLed(),
						"taf-camp-resident-led: a player-led body is refused by the roster gate");
					KingdomCityBook book;
					int id;
					Require(KingdomResidents.TryEnsureRow(System, body, FixtureOrigin, null, tick,
						out book, out id) && ReferenceEquals(book, System.City) && id > 0,
						"taf-camp-resident-norow: native enrollment did not publish an exact row "
							+ "and binding");
					RequireRowInBook(book, body, id, i + 1);
				}
				Require(Game.TimeTicks == tick,
					"taf-camp-enrollment-clock: fixture enrollment advanced the world clock");
			}

			/// <summary>The published row, read BACK through the settlement's own book: the book
			/// holds a row at that resident id, the roll carries exactly one such row, that row is
			/// bound to this zone, and the body carries the same id. Nothing here writes book
			/// state; a row that is not there is a refusal, never something to invent.</summary>
			private void RequireRowInBook(KingdomCityBook Book, GameObject Body, int Id,
				int Expected)
			{
				Require(KingdomResidents.IdOf(Body) == Id,
					"taf-camp-resident-idmismatch: the body does not carry the published id");
				int index;
				Require(Book.TryResidentRow(Id, out index) && index >= 0,
					"taf-camp-resident-nobookrow: the book holds no row at resident id " + Id);
				List<KingdomResidentRow> rows = KingdomResidents.RollRows(System);
				int found = 0;
				string bound = null;
				for (int i = 0; i < rows.Count; i++)
					if (rows[i].ResidentId == Id) { found++; bound = rows[i].BoundZoneId; }
				Require(found == 1, "taf-camp-resident-rollrow: resident id " + Id + " appears "
					+ found + " time(s) on the settlement roll");
				Require(string.Equals(bound, Zone.ZoneID, StringComparison.Ordinal),
					"taf-camp-resident-boundzone: the published row is bound to '" + bound
						+ "', not this ground");
				Require(KingdomResidents.OnRollCount(System) == Expected
					&& System.Population == Expected,
					"taf-camp-resident-rollcount: the roll reads "
						+ KingdomResidents.OnRollCount(System) + " and the population "
						+ System.Population + " after " + Expected + " enrollment(s)");
			}

			/// <summary>The standing rung-1 heart, found through the production survey and
			/// confirmed by the production design key rather than by blueprint guessing.</summary>
			private void BindHeart()
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
				Require(KingdomUpgrade.DesignKeyOf(Heart) == FirstRungKey,
					"the standing heart is not the authored first rung");
				HeartId = Heart.IDIfAssigned;
				Require(!string.IsNullOrEmpty(HeartId), "the heart has no assigned identity");
			}

			/// <summary>The store through the PRODUCTION anchored-component resolver: the camp
			/// stockpile is the heart layout's one <c>fixture:storage</c> stateful placement, and
			/// resolving it this way is exactly how the rung above is required to find it again.
			/// The fire carries no stateful anchor by design, so it is found physically inside the
			/// heart's own rect instead.</summary>
			private void BindStoreAndFire()
			{
				GameObject store;
				string failure;
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(Heart, Zone,
					StorageRole, out store, out failure), failure ?? "no anchored camp store");
				Store = store;
				Require(GameObject.Validate(Store) && Store.Blueprint == StoreBlueprint
					&& Store.Inventory != null && Store.GetIntProperty("KingdomStockpile") == 1,
					"the anchored camp store is not the authored dedicated stockpile");
				StoreId = Store.IDIfAssigned;
				StoreCell = Store.CurrentCell;
				Require(!string.IsNullOrEmpty(StoreId) && StoreCell != null,
					"the camp store has no assigned identity or cell");
				Require(Store.Inventory.Objects.Count == 0,
					"the founding camp store is not empty before the fixture fills it");
				KingdomPlotRules.PlotRect rect;
				Require(KingdomPlots.TryReadRect(Heart, out rect),
					"the heart plot's rect could not be read");
				for (int y = rect.Y1; y <= rect.Y2; y++)
					for (int x = rect.X1; x <= rect.X2; x++)
					{
						Cell cell = Zone.GetCell(x, y);
						if (cell == null) continue;
						foreach (GameObject item in cell.GetObjects())
							if (GameObject.Validate(item) && item.Blueprint == FireBlueprint)
							{
								Require(Fire == null, "more than one camp fire stands in the heart");
								Fire = item;
							}
					}
				Require(Fire != null, "no camp fire stands inside the heart's own rect");
				FireId = Fire.IDIfAssigned;
				FireCell = Fire.CurrentCell;
				Require(!string.IsNullOrEmpty(FireId) && FireCell != null,
					"the camp fire has no assigned identity or cell");
			}

			/// <summary>Fills the store to its declared forty-eight-unit capacity with real
			/// material objects: exactly the authored rung-2 bill (24 stone, 1 timber) plus 23
			/// brush the bill does not ask for. The bill is therefore paid out of the very store
			/// under test, and the brush is what must still be there afterwards.</summary>
			private void MintStoreContents()
			{
				Mint(KingdomMaterial.Stone, MintedStoneUnits, MintedStone);
				Mint(KingdomMaterial.Timber, MintedTimberUnits, MintedTimber);
				Mint(KingdomMaterial.Brush, MintedBrushUnits, MintedBrush);
				Require(Store.Inventory.Objects.Count
					== MintedStoneUnits + MintedTimberUnits + MintedBrushUnits,
					"taf-camp-store-fill: the minted contents do not fill the declared capacity");
			}

			private void Mint(KingdomMaterial Material, int Units, List<string> Ids)
			{
				string blueprint = KingdomMaterials.BlueprintFor(Material);
				Require(!string.IsNullOrEmpty(blueprint),
					"no production blueprint for material " + Material);
				for (int i = 0; i < Units; i++)
				{
					GameObject unit = Create(blueprint);
					GameObject accepted = Store.Inventory.AddObject(unit, null,
						Silent: true, NoStack: true);
					Require(ReferenceEquals(accepted, unit),
						"a minted unit did not land as its own physical unit in the camp store");
					string id = unit.IDIfAssigned;
					Require(!string.IsNullOrEmpty(id) && !Ids.Contains(id),
						"a minted unit has no unique assigned identity");
					Ids.Add(id);
				}
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

			private void RequirePair(bool Value, string Failure) { Require(Value, Failure); }
		}
	}
}
