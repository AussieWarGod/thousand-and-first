using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPower
	{
		/// <summary>
		/// Offers charge to everything the solve did not stop, and returns what was actually taken.
		/// <para>
		/// The model decides <b>how much</b> and <b>to whom</b>; this only lands it and measures
		/// the landing. A sink the brownout order stopped is skipped entirely rather than offered
		/// a smaller share: works stop whole, because a half-lit forge is not a thing a founder can
		/// see or reason about.
		/// </para>
		/// </summary>
		/// <param name="Allocated">What the solve said would reach a sink.</param>
		/// <param name="Pool">What is actually on hand &mdash; the generated charge plus whatever
		/// the stores gave back, both measured. The offer is the lesser of the two, so a store
		/// that would not release as much as the model expected cannot conjure the difference.</param>
		private static int Deliver(KingdomSystem System, List<GameObject> Sinks, KingdomFlowDemand[] Demands, int[] Order, int Stopped, int Allocated, int Pool)
		{
			int remaining = (Allocated < Pool) ? Allocated : Pool;
			if (remaining <= 0)
			{
				return 0;
			}
			int offered = remaining;
			bool[] quiet = new bool[Sinks.Count];
			for (int i = 0; i < Stopped && i < Order.Length; i++)
			{
				if (Order[i] >= 0 && Order[i] < quiet.Length)
				{
					quiet[Order[i]] = true;
				}
			}
			for (int i = 0; i < Sinks.Count && remaining > 0; i++)
			{
				GameObject sink = Sinks[i];
				if (quiet[i] || !GameObject.Validate(sink))
				{
					continue;
				}
				// Forced, because a settlement pass is a day's work arriving at once, not one
				// turn's trickle: without it a Capacitor clamps intake to its per-turn ChargeRate
				// (Capacitor.Process, IInitialChargeProductionEvent) and a day's milling would be
				// throttled to a single turn's worth. The return is the event's own accumulated
				// delta (StartingAmount - Amount), not a convenience flag, but it is clamped
				// here anyway rather than trusted on its face.
				int used = sink.ChargeAvailable(remaining, 0L, 1, Forced: true);
				if (used <= 0)
				{
					continue;
				}
				if (used > remaining)
				{
					used = remaining;
				}
				remaining -= used;
				// Latched on the object rather than the settlement, so each thing the works
				// reach announces its own first charge once and never again - and so a dormant
				// city's posts keep their own memory of it without a field on the system.
				if (sink.GetIntProperty("KingdomPowered") != 1)
				{
					sink.SetIntProperty("KingdomPowered", 1);
					string drawing = KingdomDesign.ReferenceFor(sink, sink.ShortDisplayName);
					string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
					System.RecordDeed("the " + drawing + " of " + realm + " drawing its first charge");
					KingdomChronicle.Record(System, "the works turned at " + realm + ", and " + XRL.Language.Grammar.A(drawing) + " drew its first charge from hands and weather alone", Accomplishment: true);
					MessageQueue.AddPlayerMessage("{{G|The works of " + realm + " are turning. The " + drawing + " draws from them.}}");
				}
			}
			return offered - remaining;
		}

		private static int Capacity(List<GameObject> Stores)
		{
			int total = 0;
			for (int i = 0; i < Stores.Count; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor != null && capacitor.MaxCharge > 0)
				{
					total += capacitor.MaxCharge;
				}
			}
			return total;
		}

		private static int Held(List<GameObject> Stores)
		{
			int total = 0;
			for (int i = 0; i < Stores.Count; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor != null && capacitor.Charge > 0)
				{
					total += capacitor.Charge;
				}
			}
			return total;
		}

		/// <summary>
		/// Pours charge into the stores and returns what actually went in, measured from the
		/// capacitors before and after rather than taken on the word of anything that was
		/// called. Never exceeds a store's own MaxCharge.
		/// </summary>
		private static int Deposit(List<GameObject> Stores, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor == null || capacitor.Charge >= capacitor.MaxCharge)
				{
					continue;
				}
				int before = capacitor.Charge;
				capacitor.AddCharge(remaining);
				int added = capacitor.Charge - before;
				if (added > 0)
				{
					remaining -= added;
				}
			}
			return Amount - remaining;
		}

		/// <summary>Draws charge back out of the stores, measured the same way.</summary>
		private static int Withdraw(List<GameObject> Stores, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor == null || capacitor.Charge <= 0)
				{
					continue;
				}
				int before = capacitor.Charge;
				capacitor.UseCharge(remaining);
				int taken = before - capacitor.Charge;
				if (taken > 0)
				{
					remaining -= taken;
				}
			}
			return Amount - remaining;
		}

		/// <summary>
		/// Writes the moment a bed of salt first ran full, once, and never again. Named
		/// parameters rather than a re-survey: the caller has just measured both figures.
		/// </summary>
		private static void NoteStoreFilled(KingdomSystem System, List<GameObject> Stores, int HeldCharge, int TotalCapacity)
		{
			if (TotalCapacity <= 0 || HeldCharge < TotalCapacity)
			{
				return;
			}
			for (int i = 0; i < Stores.Count; i++)
			{
				r_KingdomPowerStore store = Stores[i].GetPart<r_KingdomPowerStore>();
				if (store == null || store.EverFilled)
				{
					continue;
				}
				store.EverFilled = true;
				KingdomChronicle.Record(System, "the salt at " + KingdomPresentation.Rich(System.KingdomDisplayName) + " ran full and bright, and the settlement kept its first whole night of light");
				System.Ledger.Note("{{G|The molten-salt store is full. The settlement keeps the night now.}}");
				// One telling per pass, however many beds of salt the settlement keeps.
				return;
			}
		}	}
}
