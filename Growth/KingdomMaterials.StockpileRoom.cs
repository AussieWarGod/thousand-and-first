using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		// --- Room in a dedicated stockpile ----------------------------------------------------
		//
		// Ruling 5: counting stays WHOLE and intake is what refuses. Stock() and
		// StockForExactContainer() never read a capacity, so the settlement ledger and every
		// purpose-local debit view agree by construction and an over-full chest standing in an
		// old save reads exactly what it read before. What a capacity changes is only which
		// container a NEW delivery is allowed to choose.

		/// <summary>
		/// Units this dedicated stockpile can still take, never negative. A container the founder
		/// has overfilled by hand reports zero room and keeps every last thing in it: the engine
		/// has no intake veto and this mod invents none, so the count keeps counting what is
		/// there while the store simply stops being chosen as a destination.
		/// </summary>
		/// <param name="Container">Any object. Null, or one that holds nothing, has no room.
		/// </param>
		public static int StockpileRoom(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int room = KingdomSurvey.StockCapacityOf(Container) - KingdomSurvey.StockHeldIn(Container);
			return (room > 0) ? room : 0;
		}

		/// <summary>
		/// Room, said out loud once when there is none (STANDARDS 7b). A delivery walking the
		/// stockpiles calls this rather than <see cref="StockpileRoom"/>, so the founder is told
		/// the store is full the first time something is actually turned away, and the saying is
		/// taken back by the next delivery that finds room in it.
		/// </summary>
		/// <param name="Container">The stockpile a delivery is considering.</param>
		/// <returns>Units it can still take, zero when it is full.</returns>
		internal static int StockpileRoomSpoken(GameObject Container)
		{
			if (Container == null)
			{
				return 0;
			}
			int room = StockpileRoom(Container);
			if (room > 0)
			{
				Container.SetIntProperty(KingdomRules.StockpileFullAnnouncedProperty, 0,
					RemoveIfZero: true);
				return room;
			}
			if (Container.GetIntProperty(KingdomRules.StockpileFullAnnouncedProperty) == 1)
			{
				return 0;
			}
			Container.SetIntProperty(KingdomRules.StockpileFullAnnouncedProperty, 1);
			MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName
				+ " will not take another bundle; it holds all the keepers can account for.");
			KingdomLog.Log("materials: stockpile full, capacity="
				+ KingdomSurvey.StockCapacityOf(Container)
				+ " held=" + KingdomSurvey.StockHeldIn(Container));
			return 0;
		}

		/// <summary>How many of a stock's dedicated stockpiles will take nothing more, for the
		/// founder's status line.</summary>
		public static int FullStockpiles(MaterialStock Stock)
		{
			int full = 0;
			if (Stock == null)
			{
				return 0;
			}
			for (int i = 0; i < Stock.Stockpiles.Count; i++)
			{
				if (StockpileRoom(Stock.Stockpiles[i]) < 1)
				{
					full++;
				}
			}
			return full;
		}
	}
}
