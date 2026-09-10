using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		private sealed class ForagePlant
		{
			internal GameObject Item;
			internal Cell Cell;
			internal string Id;
		}

		private static GameObject ForageHeart(KingdomSurvey Survey)
		{
			GameObject heart = null;
			foreach (GameObject item in Survey.ForagePlots)
			{
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1) continue;
				if (heart != null) return null;
				heart = item;
			}
			return heart;
		}

		private static bool ForagePlotCell(KingdomSurvey Survey, Cell Cell)
		{
			foreach (GameObject root in Survey.ForagePlots)
			{
				if (!GameObject.Validate(root) || root.CurrentZone != Cell.ParentZone
					|| !KingdomPlots.TryReadRect(root, out var rect)) return true;
				if (Cell.X >= rect.X1 && Cell.X <= rect.X2
					&& Cell.Y >= rect.Y1 && Cell.Y <= rect.Y2) return true;
			}
			foreach (GameObject row in Survey.CropRows)
				if (GameObject.Validate(row) && row.CurrentCell == Cell) return true;
			return false;
		}

		private static bool ForagePlotAuthorityExact(KingdomSurvey Survey, Zone Z)
		{
			foreach (GameObject root in Survey.ForagePlots)
				if (!GameObject.Validate(root) || root.CurrentZone != Z
					|| !KingdomPlots.TryReadRect(root, out _)) return false;
			return true;
		}

		private static bool ForageCandidate(GameObject Item, Cell Cell, KingdomSurvey Survey)
		{
			if (!GameObject.Validate(Item) || Cell == null || Item.CurrentCell != Cell
				|| Item.Count != 1 || Item.GetPart<Physics>() == null) return false;
			var facts = KingdomMaterialRules.ForageFacts.None;
			if (Item.HasTag("Plant") || Item.HasTag("LivePlant")) facts |= KingdomMaterialRules.ForageFacts.Plant;
			if (Item.IsWall() || Item.HasTag("Wall") || Item.HasPart("Wall")
				|| Item.HasTag("PaintedWall") || Item.HasPart("PaintedWall")) facts |= KingdomMaterialRules.ForageFacts.Wall;
			if (Item.HasTag("Tree")) facts |= KingdomMaterialRules.ForageFacts.Tree;
			if (Item.IsCreature || Item.IsPlayer() || Item.HasPart("Brain")) facts |= KingdomMaterialRules.ForageFacts.Creature;
			if (Item.HasPart("Harvestable") || Item.HasPart("Food")
				|| Item.HasPart("PreparedCookingIngredient")) facts |= KingdomMaterialRules.ForageFacts.Food;
			if (!string.IsNullOrEmpty(Item.GetPart<Physics>().Owner)) facts |= KingdomMaterialRules.ForageFacts.Owned;
			if (Item.HasPart<r_KingdomWildSeed>()
				&& Item.GetIntProperty(KingdomCrops.WildSeedTakenProperty) != 1) facts |= KingdomMaterialRules.ForageFacts.UntakenSeed;
			if (IsProtected(Item, out _) || Item.GetIntProperty(KingdomPlots.PlotPartProperty) != 0
				|| Item.GetPart<r_KingdomForage>() != null) facts |= KingdomMaterialRules.ForageFacts.Protected;
			if (ForagePlotCell(Survey, Cell)) facts |= KingdomMaterialRules.ForageFacts.Plot;
			if (Item.IsTakeable()) facts |= KingdomMaterialRules.ForageFacts.Portable;
			return KingdomMaterialRules.ForageCandidate(facts);
		}

		private static void ForageBlocked(KingdomSystem System, r_KingdomForage State, string Reason)
		{
			if (KingdomMaterialRules.ForageAnnounce(ref State.BlockedAnnounced, true))
				System.Ledger.Note("{{r|" + Reason + "}}");
		}

		/// <summary>
		/// Physical brush standing in the dedicated stockpiles right now, raw and counting a
		/// unit a construction job has already routed exactly as it counts an unrouted one.
		/// <c>MaterialStock.Tally</c> is available-only &mdash; <c>TallyAvailableHeld</c> skips
		/// anything <c>KingdomConstructionInputLeaseAuthority</c> has leased to a job &mdash; and
		/// the ceiling promise is "twelve bundles stored", the same promise a stockpile's own
		/// capacity keeps by counting a reserved unit as occupying its space. Reading Tally here
		/// would let forage re-cut brush a delivery had already claimed, growing the pile past
		/// twelve the moment that delivery lands. So this walks the same dedicated containers
		/// <see cref="Stock"/> already found and re-counts the material blueprint directly with
		/// raw custody and blueprint reads, plus
		/// <see cref="RawCensusCountOf"/> and never asks an object anything that can answer back
		/// &mdash; no ordinary <c>Count</c>, no dispatch, no lease filter.
		/// </summary>
		private static int ForageCeilingHeld(MaterialStock Stock)
		{
			if (Stock == null)
			{
				return 0;
			}
			string blueprint = BlueprintFor(KingdomMaterial.Brush);
			int held = 0;
			for (int i = 0; i < Stock.Stockpiles.Count; i++)
			{
				GameObject container = Stock.Stockpiles[i];
				if (!GameObject.Validate(container) || container.Inventory == null || !IsStockpile(container)) continue;
				foreach (GameObject item in container.Inventory.Objects)
				{
					if (!GameObject.Validate(item) || item.Blueprint != blueprint || !StandsIn(item, container, null)) continue;
					held = KingdomMaterialRules.AddForageHeld(held, RawCensusCountOf(item));
					if (held == int.MaxValue) return held;
				}
			}
			return held;
		}

		/// <summary>True while an applicable block remains; false also covers healthy no-cut passes.</summary>
		private static bool WorkForage(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Heart, r_KingdomForage State, int Hands, int Days)
		{
			if (State.Held)
			{
				ForageBlocked(System, State, "The brush duty is held for inspection: an earlier removal or bundle could not be accounted for. Nothing is cut or issued again.");
				return true;
			}
			if (!GameObject.Validate(Heart) || Heart.CurrentZone != Z
				|| Heart.GetPart<r_KingdomForage>() != State
				|| ForageHeart(Survey) != Heart || !KingdomPlots.TryReadRect(Heart, out var heartRect)
				|| !KingdomPlots.TryRiteGround(Z, out int riteX, out int riteY)
				|| riteX < heartRect.X1 || riteX > heartRect.X2
				|| riteY < heartRect.Y1 || riteY > heartRect.Y2)
			{
				ForageBlocked(System, State, "The brush duty cannot prove the rite ground. Nothing is cut.");
				return true;
			}
			if (!ForagePlotAuthorityExact(Survey, Z))
			{
				ForageBlocked(System, State, "The brush duty cannot prove the plot boundaries. Nothing is cut.");
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (!stock.InputLeaseAuthorityExact)
			{
				ForageBlocked(System, State, "The brush duty cannot prove the stores' commitments. Nothing is cut.");
				return true;
			}
			int held = ForageCeilingHeld(stock);
			bool enough = held >= KingdomMaterialRules.ForageCeilingUnits;
			if (KingdomMaterialRules.ForageAnnounce(ref State.EnoughAnnounced, enough))
				System.Ledger.Note("The stockpiles hold brush enough; the scrub is left standing.");
			if (KingdomMaterialRules.ForageAnnounce(ref State.NoHandsAnnounced, Hands <= 0))
				System.Ledger.Note("The camp has nobody free to cut scrub.");
			if (enough || Hands <= 0 || Days <= 0) return false;
			var plants = new List<ForagePlant>();
			foreach (GameObject item in Survey.ForagePlants)
			{
				Cell cell = item?.CurrentCell;
				if (cell == null || cell.ParentZone != Z
					|| KingdomMaterialRules.ForageDistance(cell.X, cell.Y, riteX, riteY)
						> KingdomMaterialRules.ForageRadius || !ForageCandidate(item, cell, Survey)) continue;
				plants.Add(new ForagePlant { Item = item, Cell = cell, Id = item.ID });
			}
			if (!ForagePlotAuthorityExact(Survey, Z))
			{
				ForageBlocked(System, State, "The brush duty lost the plot boundaries while surveying. Nothing is cut.");
				return true;
			}
			if (KingdomMaterialRules.ForageAnnounce(ref State.NoBrushAnnounced, plants.Count == 0))
				System.Ledger.Note("The scrub within " + KingdomMaterialRules.ForageRadius + " paces of "
					+ KingdomPresentation.Rich(System.SeatName) + " is cut out. There is nothing left here to bind canvas from.");
			plants.Sort((a, b) =>
			{
				int order = KingdomMaterialRules.ForageOrder(a.Cell.X, a.Cell.Y, b.Cell.X, b.Cell.Y, riteX, riteY);
				return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
			});
			int attempts = Math.Min(plants.Count, KingdomMaterialRules.ForageUnits(Hands, Days, held));
			for (int i = 0; i < attempts; i++)
			{
				ForagePlant plant = plants[i];
				GameObject item = plant.Item;
				stock = Stock(Z);
				if (!stock.InputLeaseAuthorityExact || !GameObject.Validate(Heart)
					|| Heart.CurrentZone != Z || ForageHeart(Survey) != Heart
					|| Heart.GetPart<r_KingdomForage>() != State
					|| !System.Founded || !System.ClaimedZones.Contains(Z.ZoneID))
				{
					ForageBlocked(System, State, "The brush duty lost its heart or stock authority. Nothing more is cut.");
					return true;
				}
				if (ForageCeilingHeld(stock) >= KingdomMaterialRules.ForageCeilingUnits) break;
				bool candidate = ForageCandidate(item, plant.Cell, Survey);
				if (!ForagePlotAuthorityExact(Survey, Z))
				{
					ForageBlocked(System, State, "The brush duty lost the plot boundaries before cutting. Nothing more is cut.");
					return true;
				}
				if (!candidate || item.ID != plant.Id) continue;
				if (!KingdomOrdinaryCustody.TryProveEmpty(item, out _))
				{
					ForageBlocked(System, State, "A scrub plant now holds another object; it is left untouched.");
					return true;
				}
				State.Held = true;
				bool gone = false;
				try { gone = item.Obliterate(null, Silent: true); }
				catch { }
				if (GameObject.Validate(item))
				{
					State.Held = gone || item.CurrentCell != plant.Cell;
					ForageBlocked(System, State, "A scrub plant refused removal or changed custody. No brush was credited.");
					return true;
				}
				KingdomSurvey.ObserveRemovedFromActive(Z, item);
				// A destroy callback may replace the heart or revoke the settlement's ground.
				// Keep the pre-call hold; never issue against authority that ceased to exist.
				stock = Stock(Z);
				if (!stock.InputLeaseAuthorityExact || !GameObject.Validate(Heart)
					|| Heart.CurrentZone != Z || Heart.GetPart<r_KingdomForage>() != State
					|| ForageHeart(Survey) != Heart || !System.Founded
					|| !System.ClaimedZones.Contains(Z.ZoneID))
				{
					ForageBlocked(System, State, "The cut brush lost its heart or stock authority. The duty is held for inspection; no bundle was issued.");
					return true;
				}
				int spilled;
				KingdomDepositCustody custody;
				try { spilled = stock.Put(KingdomMaterial.Brush, 1, plant.Cell, out custody); }
				catch
				{
					ForageBlocked(System, State, "The cut brush could not be accounted for. The duty is held for inspection rather than issuing it twice.");
					return true;
				}
				if (custody != KingdomDepositCustody.Settled)
				{
					ForageBlocked(System, State, "The cut brush's custody is unproved. The duty is held for inspection rather than issuing it twice.");
					return true;
				}
				State.Held = false;
				State.BlockedAnnounced = false;
				if (KingdomMaterialRules.ForageAnnounce(ref State.NoRoomAnnounced, spilled > 0))
					System.Ledger.Note("The stockpiles will not take another bundle; it is stacked where it was cut.");
				if (spilled > 0) return false;
			}
			return false;
		}

		/// <summary>Live physical store count; does not attach state or advance the duty.</summary>
		public static string ForageStatus(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null) return "";
			var survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z, System);
			GameObject heart = ForageHeart(survey);
			if (heart == null) return "";
			bool held = heart.GetPart<r_KingdomForage>()?.Held == true;
			return "\nbrush: " + KingdomMaterialRules.ForageHands(KingdomMaterialRules.FreeHands(
				System.Population, System.AssignedCrew)) + " hands within " + KingdomMaterialRules.ForageRadius
				+ " paces; " + ForageCeilingHeld(Stock(Z)) + " bundles stored"
				+ (held ? "; held for inspection" : "; cutting waits on higher-priority orders");
		}
	}
}
