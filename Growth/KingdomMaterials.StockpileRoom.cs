using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		// --- Room in a dedicated stockpile, and the intake that respects it -------------------
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
			MessageQueue.AddPlayerMessage("{{K|The " + Container.ShortDisplayName
				+ " will not take another bundle; it holds all the keepers can account for.}}");
			KingdomLog.Log("materials: stockpile full, capacity="
				+ KingdomSurvey.StockCapacityOf(Container)
				+ " held=" + KingdomSurvey.StockHeldIn(Container));
			return 0;
		}

		/// <summary>
		/// Says that a made thing never landed. A missing item blueprint has eaten real raw stock
		/// and must be loud, because it is a wiring fault in this mod's own files. Every store
		/// full with no ground to spill on is a different thing entirely &mdash; a settlement out
		/// of room, working exactly as written &mdash; and must not be reported as a fault.
		/// </summary>
		/// <param name="Stock">The settlement's stock, walked for room.</param>
		/// <param name="Ground">The cell the overflow would have gone to. Null is no ground.</param>
		/// <param name="Made">What was made, for the fault line.</param>
		/// <param name="Blueprint">The blueprint that should have existed, or null.</param>
		/// <param name="Custody">How the delivery ended. A deposit that could not prove where its
		/// bundle went is a third thing again: real stock exists somewhere, the delivery stopped
		/// rather than making it twice, and the founder has already been told once.</param>
		internal static void ReportNothingLanded(MaterialStock Stock, Cell Ground, string Made,
			string Blueprint, KingdomDepositCustody Custody)
		{
			if (Custody != KingdomDepositCustody.Settled)
			{
				KingdomLog.Log("materials: " + Made
					+ " ended in unproved custody; nothing was made again for it");
				return;
			}
			if (Ground == null && Stock != null && Stock.Stockpiles.Count > 0
				&& FullStockpiles(Stock) >= Stock.Stockpiles.Count)
			{
				KingdomLog.Log("materials: " + Made
					+ " had nowhere to land; every stockpile full and no ground to spill on");
				return;
			}
			MetricsManager.LogError("ThousandAndFirst KingdomMaterials: " + Made
				+ " and nothing could be created for it; is "
				+ (Blueprint ?? "its blueprint") + " declared?");
		}

		/// <summary>
		/// Fills one stockpile up to the room it declared, and no further.
		/// <para>
		/// The law itself is engine-free and lives in <see cref="KingdomDepositEngine"/>; this is
		/// only the seam onto real objects. Nothing is remembered across a callback, because there
		/// are three of them and every one of them belongs to somebody else: creating the bundle,
		/// STAMPING ITS COUNT (which is <c>Stacker.StackCount</c>, and sends
		/// <c>StackCountChangedEvent</c>), and inserting it.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone the delivery is running in, for the survey's observations.
		/// </param>
		/// <param name="Container">The destination, re-proved at every step rather than
		/// remembered.</param>
		/// <param name="Blueprint">What one bundle of the material is made of.</param>
		/// <param name="Room">Room the store had when this delivery chose it.</param>
		/// <param name="Remaining">Units still to deliver, lowered by what is proved placed.
		/// </param>
		/// <returns>What was proved placed, and whether the delivery may go on at all.</returns>
		internal static KingdomDepositOutcome Deposit(Zone Z, GameObject Container,
			string Blueprint, int Room, ref int Remaining)
		{
			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(
				new StockpileDepositHost(Z, Container, Blueprint), Room, Remaining);
			Remaining -= outcome.Placed;
			return outcome;
		}

		/// <summary>Room in an exact destination that is still a dedicated stockpile, proved
		/// fresh after a callback rather than remembered. Anything else has no room at all: a
		/// store another handler destroyed, took the inventory off, or released mid-delivery
		/// has stopped being a destination, and the delivery walks on rather than putting
		/// material somewhere the settlement no longer counts.</summary>
		internal static int DepositRoomNow(GameObject Container)
		{
			return (GameObject.Validate(Container) && Container.Inventory != null
				&& IsStockpile(Container)) ? StockpileRoom(Container) : 0;
		}

		/// <summary>What an exact destination physically holds right now, and nothing at all once
		/// it has stopped being a destination. Read either side of an insertion, this is how the
		/// delivery learns what a store actually gained when the bundle it made stopped being the
		/// thing the units arrived in.</summary>
		internal static int DepositHeldNow(GameObject Container)
		{
			return (GameObject.Validate(Container) && Container.Inventory != null
				&& IsStockpile(Container)) ? KingdomSurvey.StockHeldIn(Container) : 0;
		}

		/// <summary>
		/// Proof that the exact bundle this delivery made is standing in the exact store it was
		/// made for, as itself, of the material it was made of, carrying the count it was stamped
		/// with. Insertion runs other people's callbacks, so a delivery that counted the CALL
		/// would pay itself for a bundle that was refused, replaced, merged away, or carried off.
		/// </summary>
		internal static bool DepositLanded(GameObject Container, GameObject Item,
			GameObject Accepted, string Blueprint, int Batch)
		{
			return ReferenceEquals(Accepted, Item) && GameObject.Validate(Item)
				&& Item.Blueprint == Blueprint && Item.Count == Batch
				&& GameObject.Validate(Container) && Container.Inventory != null
				&& Item.Physics != null && ReferenceEquals(Item.Physics.InInventory, Container)
				&& Item.CurrentCell == null && Container.Inventory.Objects.Contains(Item);
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
				GameObject container = Stock.Stockpiles[i];
				// A thing that holds nothing is not a store standing full. The status clause skips
				// it for the same reason, so the two never disagree about what a store is.
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				if (StockpileRoom(container) < 1)
				{
					full++;
				}
			}
			return full;
		}
	}
}
