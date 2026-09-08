using System;
using System.Collections.Generic;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomWaterDebit
	{
		/// <summary>
		/// Every receipt that is still holding vessels it has neither drained nor released.
		/// <para>
		/// This is the per-VESSEL question <see cref="TransactionOpen"/> cannot answer.
		/// <c>OpenTransactions</c> counts call-stack depth, so it is zero in exactly the window
		/// that matters: after <c>Reserve</c> has handed back a reserved receipt and before
		/// <c>Commit</c> or <c>Rollback</c> runs. Real callers hold a reservation across that
		/// window &mdash; construction funding reserves, publishes a WaterPending transition and
		/// only then commits &mdash; and every verifier on the way through asserts
		/// <c>entry.Vessel.MaxVolume == entry.OriginalMaxVolume</c>. A subsystem that would RESIZE
		/// a vessel has to be able to see that hold, or it widens a vessel underneath a receipt
		/// the founder is in the middle of spending and the failed commit is escalated as a
		/// quarantine.
		/// </para>
		/// <para>
		/// Weak references, and the state is re-read on every query: a receipt that is simply
		/// abandoned is collected and its hold disappears with it, so this registry cannot leak a
		/// permanent refusal the way a strong per-vessel set keyed on an abandoned reservation
		/// would. While an abandoned receipt IS still referenced somewhere its reservation is
		/// genuinely open, and answering "held" for it is the honest answer.
		/// </para>
		/// </summary>
		private static readonly List<WeakReference<KingdomWaterDebit>> OpenReservations =
			new List<WeakReference<KingdomWaterDebit>>();

		/// <summary>Whether any live receipt has this exact vessel reserved and not yet committed,
		/// rolled back or failed. Reference identity only; no vessel is read or touched.</summary>
		internal static bool VesselReserved(LiquidVolume Vessel)
		{
			if (Vessel == null) return false;
			bool held = false;
			for (int i = OpenReservations.Count - 1; i >= 0; i--)
			{
				KingdomWaterDebit debit;
				if (!OpenReservations[i].TryGetTarget(out debit) || debit == null
					|| debit.State != KingdomWaterDebitState.Reserved)
				{
					OpenReservations.RemoveAt(i);
					continue;
				}
				if (debit.BindsVessel(Vessel)) held = true;
			}
			return held;
		}

		/// <summary>Records a receipt that came back still reserved, so a resize can see its hold.
		/// Returns the same receipt so reservation returns read as one expression.</summary>
		private KingdomWaterDebit RegisterReservation()
		{
			if (State != KingdomWaterDebitState.Reserved || Entries.Count <= 0) return this;
			for (int i = 0; i < OpenReservations.Count; i++)
			{
				KingdomWaterDebit existing;
				if (OpenReservations[i].TryGetTarget(out existing)
					&& ReferenceEquals(existing, this)) return this;
			}
			OpenReservations.Add(new WeakReference<KingdomWaterDebit>(this));
			return this;
		}

		/// <summary>Drops this receipt's hold the moment it is settled one way or the other.
		/// Idempotent, and never throws: it runs from terminal finally blocks.</summary>
		private void ReleaseReservation()
		{
			for (int i = OpenReservations.Count - 1; i >= 0; i--)
			{
				KingdomWaterDebit existing;
				if (!OpenReservations[i].TryGetTarget(out existing) || existing == null
					|| ReferenceEquals(existing, this)) OpenReservations.RemoveAt(i);
			}
		}

		private bool BindsVessel(LiquidVolume Vessel)
		{
			for (int i = 0; i < Entries.Count; i++)
				if (Entries[i] != null && ReferenceEquals(Entries[i].Vessel, Vessel)) return true;
			return false;
		}
	}
}
