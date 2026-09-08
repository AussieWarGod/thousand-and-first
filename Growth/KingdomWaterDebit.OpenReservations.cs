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

		/// <summary>Set while the caller of a COMMITTED receipt has declared that it may still
		/// compensate it. Opt-in, and never inferred: a caller that commits and then runs its own
		/// completion work &mdash; a construction whose finished rung widens the very basin it just
		/// drained &mdash; must NOT hold the vessel, or the rung it paid for could never widen
		/// anything. Only a caller that will call <c>Rollback</c> after its own callbacks asks for
		/// this.</summary>
		private bool CompensationWindowOpen;

		/// <summary>Whether any live receipt still holds this exact vessel: reserved and unsettled,
		/// or committed inside a caller's declared compensation window. Reference identity only; no
		/// vessel is read or touched.</summary>
		internal static bool VesselReserved(LiquidVolume Vessel)
		{
			if (Vessel == null) return false;
			bool held = false;
			for (int i = OpenReservations.Count - 1; i >= 0; i--)
			{
				KingdomWaterDebit debit;
				if (!OpenReservations[i].TryGetTarget(out debit) || debit == null
					|| !debit.HoldsVessels)
				{
					OpenReservations.RemoveAt(i);
					continue;
				}
				if (debit.BindsVessel(Vessel)) held = true;
			}
			return held;
		}

		/// <summary>
		/// Whether this receipt is still holding the vessels it bound. Reserved is the ordinary
		/// answer. Committed counts too while the caller's compensation window is open, because
		/// <c>Rollback</c> re-proves <c>entry.Vessel.MaxVolume == entry.OriginalMaxVolume</c>
		/// before it restores a single dram: a resize landing between the commit and the caller's
		/// compensation turns a recoverable interruption into a refused restoration.
		/// </summary>
		private bool HoldsVessels
		{
			get
			{
				return State == KingdomWaterDebitState.Reserved
					|| (State == KingdomWaterDebitState.Committed && CompensationWindowOpen);
			}
		}

		/// <summary>
		/// Declares that this receipt's caller may still compensate the debit AFTER it commits, so
		/// the vessel hold survives the commit instead of being dropped by it.
		/// <para>
		/// Opened before <c>Commit</c> and closed by <see cref="EndCompensationWindow"/> at the
		/// caller's last compensation point, or by the terminal release of whichever
		/// <c>Rollback</c> settles the receipt. A receipt that is neither reserved nor committed is
		/// past compensating and opens nothing.
		/// </para>
		/// </summary>
		public void BeginCompensationWindow()
		{
			if (State == KingdomWaterDebitState.Reserved
				|| State == KingdomWaterDebitState.Committed) CompensationWindowOpen = true;
		}

		/// <summary>Closes the caller's compensation window and drops the hold of a settled
		/// receipt. Idempotent, and it never drops a hold that is still an open RESERVATION: only
		/// the reservation's own terminal transition may do that.</summary>
		public void EndCompensationWindow()
		{
			CompensationWindowOpen = false;
			if (State != KingdomWaterDebitState.Reserved) ReleaseReservation();
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
			CompensationWindowOpen = false;
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
