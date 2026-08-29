using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{

		/// <summary>Walks a zone once and reads every dedicated stockpile in it.</summary>
		/// <param name="Z">Zone to read. Null yields an empty stock.</param>
		public static MaterialStock Stock(Zone Z)
		{
			MaterialStock stock = new MaterialStock();
			if (Z == null)
			{
				return stock;
			}
			stock.Zone = Z;
			stock.InputLeaseAuthorityExact =
				KingdomConstructionInputLeaseAuthority.TryCapture(
					out stock.InputLeases, out stock.InputLeaseFailure);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			List<GameObject> candidates = survey.MaterialStockpiles;
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				if (!IsStockpile(item) || item.Inventory == null)
				{
					continue;
				}
				stock.Stockpiles.Add(item);
				foreach (GameObject held in item.Inventory.Objects)
					TallyAvailableHeld(stock, held);
			}
			return stock;
		}

		/// <summary>Canonical debit-only stock view restricted to one exact dedicated
		/// container. It shares the same routed-input authority snapshot as <see cref="Stock"/>
		/// and never broadens a purpose-local debit to another stockpile.</summary>
		internal static MaterialStock StockForExactContainer(Zone Z, GameObject Container)
		{
			MaterialStock all = Stock(Z);
			MaterialStock exact = new MaterialStock
			{
				Zone = all.Zone,
				InputLeases = all.InputLeases,
				InputLeaseAuthorityExact = all.InputLeaseAuthorityExact,
				InputLeaseFailure = all.InputLeaseFailure
			};
			if (!GameObject.Validate(Container) || Container.Inventory == null
				|| Container.CurrentZone != Z) return exact;
			bool canonical = false;
			for (int i = 0; i < all.Stockpiles.Count; i++)
				if (ReferenceEquals(all.Stockpiles[i], Container))
				{
					canonical = true;
					break;
				}
			if (!canonical) return exact;
			exact.Stockpiles.Add(Container);
			foreach (GameObject held in Container.Inventory.Objects)
				TallyAvailableHeld(exact, held);
			return exact;
		}

		private static void TallyAvailableHeld(MaterialStock stock, GameObject held)
		{
			if (stock == null || !stock.InputLeaseAuthorityExact
				|| !KingdomConstructionInputLeaseAuthority.CanUseMaterial(
					stock.InputLeases, held)) return;
			// The material vocabulary claims a thing first and exclusively. Vanilla's own
			// Scrap Metal is how this settlement STORES scrap, and it is also a tinkering bit;
			// counted as both, a wall's worth could be spent twice. The shelf's scrap answers
			// for walls; the settlement's bits come from other donated ruin-stock.
			if (TryOrdinaryMaterialOf(held, out var material))
			{
				stock.Tally.Add(material, held.Count);
				return;
			}
			if (TryExoticOf(held, out var exotic))
			{
				stock.Exotics.Add(exotic, held.Count);
				return;
			}
			TryBitsOf(held, stock.Bits);
		}

		/// <summary>The item blueprint a material is stored as, or null for a value outside the
		/// enum.</summary>
		public static string BlueprintFor(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= MaterialBlueprints.Length)
			{
				return null;
			}
			return MaterialBlueprints[index];
		}

		/// <summary>
		/// Dedicates a container to the settlement's stockpiles, or releases one already
		/// dedicated. Dedication is a mark and never a transfer &mdash; what is inside stays
		/// where it is and stays the founder's, and releasing it un-counts it without moving a
		/// thing.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the container stands in; must be the kingdom's own ground.</param>
		/// <param name="Container">The container to mark. Must hold things rather than liquid.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// marked when it does.</param>
		/// <returns>True once the container's standing has actually changed.</returns>
		public static bool DedicateStockpile(KingdomSystem System, Zone Z, GameObject Container, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A stockpile is kept on the kingdom's own ground, not in other people's yards.";
				return false;
			}
			if (Container == null || Container.Inventory == null)
			{
				Failure = "A stockpile has to be something that holds things.";
				return false;
			}
			if (IsStockpile(Container))
			{
				if (!KingdomDesignationReleaseAuthority.TryCanRelease(
					System, Z, Container, out Failure)) return false;
				Container.SetIntProperty(StockpileProperty, 0);
				MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName + " is yours alone again. Nothing in it will be counted.");
				return true;
			}
			if (CountStockpiles(Z) >= MaxStockpiles)
			{
				Failure = "The settlement keeps as many stockpiles as anyone can keep an honest account of.";
				return false;
			}
			Container.SetIntProperty(StockpileProperty, 1);
			MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName + " is a stockpile of " + KingdomPresentation.Rich(System.SeatName) + " now. What is in it is counted, and still yours.");
			KingdomLog.Log("materials: stockpile dedicated at " + System.SeatName);
			return true;
		}

		/// <summary>One line for the status report: what the stockpiles hold, or why they hold
		/// nothing. Never null.</summary>
		public static string StockLine(Zone Z)
		{
			MaterialStock stock = Stock(Z);
			if (stock.None)
			{
				return "Nothing here is dedicated as a stockpile. Materials cleared from the ground will lie where they fall.";
			}
			string held = stock.Tally.Describe();
			string bits = stock.Bits.Describe();
			string exotics = stock.Exotics.Describe();
			if (held == null && bits == null && exotics == null)
			{
				return "The stockpiles stand empty.";
			}
			// The rare finds and the tinkering stock are counted separately because they are spent
			// separately: neither one is ever drawn on for a wall, and a founder reading one line
			// for all three would not know which of them the next work is short of.
			return ((held == null) ? "The stockpiles hold nothing the walls are made of" : ("The stockpiles hold {{C|" + held + "}}"))
				+ ((bits == null) ? "" : (", and stock enough for bits: {{C|" + bits + "}}"))
				+ ((exotics == null) ? "" : (", and {{C|" + exotics + "}} laid aside"))
				+ ".";
		}

	}
}
