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
				{
					// The material vocabulary claims a thing first and exclusively. Vanilla's own
					// Scrap Metal is how this settlement STORES scrap, and it is also a tinkering
					// bit; counted as both, a wall's worth of it could be spent twice - once on the
					// wall and once on a machine - which is minting. So the shelf's scrap answers
					// for the walls, and the settlement's bits come from the other things a founder
					// donates: fried processing cores, cracked robotics housings, and whatever else
					// came home from a ruin.
					if (TryMaterialOf(held, out var material))
					{
						stock.Tally.Add(material, held.Count);
						continue;
					}
					if (TryExoticOf(held, out var exotic))
					{
						stock.Exotics.Add(exotic, held.Count);
						continue;
					}
					TryBitsOf(held, stock.Bits);
				}
			}
			return stock;
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

		// --- Paying for a building in material ------------------------------------------------

		/// <summary>
		/// Reserves an arbitrary composite claim against the exact physical contents of this
		/// ground's dedicated stockpiles. Reservation is read-only. The returned receipt is non-null
		/// even on refusal; inspect <c>Reservation.Outcome</c> before creating or funding a job.
		/// </summary>
		public static KingdomMaterialDebit ReserveComposite(Zone Z, KingdomMaterialDebitCost Cost)
		{
			return KingdomMaterialDebit.Reserve(Stock(Z), Cost);
		}

		/// <summary>Reserves an arbitrary outstanding claim while requiring the same exact object
		/// named by its durable operation receipt. Used only where the route already froze identity;
		/// it never chooses a same-kind replacement.</summary>
		public static KingdomMaterialDebit ReserveCompositeWithRequiredItem(Zone Z,
			KingdomMaterialDebitCost Cost, GameObject RequiredItem)
		{
			return KingdomMaterialDebit.Reserve(Stock(Z), Cost, RequiredItem);
		}

		/// <summary>Read-only exact reservation of a catalogue design's full composite price.</summary>
		public static KingdomMaterialDebit ReservePayment(Zone Z, string Key)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(
				CostFor(Key), BitCostFor(Key), ExoticCostFor(Key)));
		}

		/// <summary>
		/// Read-only exact reservation that requires one particular delivered consignment object to
		/// answer the design's ordinary material price. The item is not an extra token cost: it is
		/// one physically produced unit already present in that price.
		/// </summary>
		public static KingdomMaterialDebit ReservePaymentWithRequiredItem(Zone Z, string Key,
			GameObject RequiredItem)
		{
			return KingdomMaterialDebit.Reserve(Stock(Z), new KingdomMaterialDebitCost(
				CostFor(Key), BitCostFor(Key), ExoticCostFor(Key)), RequiredItem);
		}

		/// <summary>
		/// Read-only exact reservation of an improvement's registered price. The present catalogue
		/// authors upgrade material separately and declares no upgrade-only bit or exotic attributes,
		/// so those two lanes are empty until that data contract is extended explicitly.
		/// </summary>
		public static KingdomMaterialDebit ReserveUpgradePayment(Zone Z, string PredecessorKey)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(
				UpgradeCostFor(PredecessorKey), null, null));
		}

		/// <summary>Read-only exact reservation of one authored same-set transition price.</summary>
		public static KingdomMaterialDebit ReserveTransitionPayment(Zone Z,
			KingdomMaterialTally Materials)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(Materials, null, null));
		}

		/// <summary>Whether dedicated stockpiles cover one authored same-set transition.</summary>
		public static bool CanPayTransition(Zone Z, KingdomMaterialTally Cost,
			out string Failure)
		{
			Failure = null;
			KingdomMaterialTally cost = Cost ?? new KingdomMaterialTally();
			if (cost.IsEmpty()) return true;
			MaterialStock stock = Stock(Z);
			if (KingdomMaterialRules.Covers(stock.Tally, cost)) return true;
			string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
			Failure = "The change wants {{C|" + cost.Describe()
				+ "}}, and the stockpiles are short "
				+ (missing == null ? "of it" : "{{C|" + missing + "}}") + ".";
			return false;
		}

		/// <summary>Read-only exact reservation of an arbitrary bit price, including a lab record.</summary>
		public static KingdomMaterialDebit ReserveBits(Zone Z, KingdomBitTally Bits)
		{
			return ReserveComposite(Z, new KingdomMaterialDebitCost(null, Bits, null));
		}

		/// <summary>
		/// Whether the dedicated stockpiles on this ground cover a design's material cost. A
		/// design with no material cost is always affordable, which is every design the catalogue
		/// carried before materials existed.
		/// </summary>
		/// <param name="Z">Ground the commission would be issued on.</param>
		/// <param name="Key">The design's registry key.</param>
		/// <param name="Failure">A founder-facing reason when this returns false, naming the
		/// shortfall rather than restating the whole price.</param>
		public static bool CanPay(Zone Z, string Key, out string Failure)
		{
			Failure = null;
			// The yards first, and before a single unit is counted. A founder standing at a design
			// the settlement has no mason for should be told THAT, not handed a shopping list they
			// could fill and still be refused (STANDARDS 7b).
			if (!AllowsInfrastructure(Z, Key, out Failure))
			{
				return false;
			}
			KingdomMaterialTally cost = CostFor(Key);
			MaterialStock stock = null;
			if (!cost.IsEmpty())
			{
				stock = Stock(Z);
				if (!KingdomMaterialRules.Covers(stock.Tally, cost))
				{
					string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
					Failure = "The work wants {{C|" + cost.Describe() + "}}, and the stockpiles are short "
						+ ((missing == null) ? "of it" : ("{{C|" + missing + "}}"))
						+ ". Clear ground for it, trade for it, or strike something that was built of it."
						+ (stock.None ? " Nothing here is dedicated as a stockpile yet." : "");
					return false;
				}
			}
			KingdomBitTally bits = BitCostFor(Key);
			KingdomExoticTally exotics = ExoticCostFor(Key);
			if (bits.IsEmpty() && exotics.IsEmpty())
			{
				return true;
			}
			if (stock == null)
			{
				stock = Stock(Z);
			}
			if (!KingdomMaterialRules.CoversBits(stock.Bits, bits))
			{
				string missing = KingdomMaterialRules.MissingBits(stock.Bits, bits).Describe();
				Failure = "This is high-craft work. It wants {{C|" + bits.Describe()
					+ "}} out of the stockpiles, and the keepers are short " + ((missing == null) ? "of it" : ("{{C|" + missing + "}}"))
					+ ". Bring scrap home and put it in a stockpile; whatever comes apart into the right stock will do.";
				return false;
			}
			if (!KingdomMaterialRules.CoversExotics(stock.Exotics, exotics))
			{
				string missing = KingdomMaterialRules.MissingExotics(stock.Exotics, exotics).Describe();
				Failure = "A work like this is finished in something rarer than stone. It wants {{C|" + exotics.Describe()
					+ "}}, and the stockpiles hold no " + ((missing == null) ? "such thing" : ("{{C|" + missing + "}}"))
					+ ". Nobody here can make one. Somebody has to find one and carry it home.";
				return false;
			}
			return true;
		}

	}
}
