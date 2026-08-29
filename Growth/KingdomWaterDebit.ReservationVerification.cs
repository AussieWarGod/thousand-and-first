using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomWaterDebit
	{
		private void SynchronizeCachedRows()
		{
			for (int i = 0; i < Entries.Count; i++)
				Survey.SynchronizeReceiptObject(Entries[i].Owner);
		}

		private void ReconcilePhysicalRows()
		{
			for (int i = 0; i < Entries.Count; i++)
				Survey.ObserveChanged(Entries[i].Owner);
		}

		private bool AllStillReserved()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				if (!KingdomWaterDebitRules.EntryStillReserved(
					entry.OriginalVolume,
					entry.Vessel == null ? -1 : entry.Vessel.Volume,
					entry.Allocation,
					KingdomLiquids.HasFreshWater(entry.Vessel),
					IsDedicated(entry),
					OwnsVessel(entry.Owner, entry.Vessel),
					entry.Vessel != null && entry.Vessel.MaxVolume == entry.OriginalMaxVolume,
					LocationMatches(entry),
					entry.Vessel != null && ReferenceEquals(entry.Vessel.ComponentLiquids,
						entry.ComponentIdentity) && ComponentsMatch(entry.Vessel.ComponentLiquids,
						entry.OriginalComponents)))
				{
					return false;
				}
			}
			return true;
		}

		private bool CurrentLeaseAuthorityAllowsDebit()
		{
			if (Entries.Count == 0 || !Entries[0].Dedicated) return true;
			KingdomConstructionInputLeaseSnapshot leases;
			string failure;
			int available;
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure)
				|| !KingdomConstructionInputLeaseAuthority.TryWaterAllowance(
					leases, Survey, true, out available, out failure)
				|| available < Amount) return false;
			for (int i = 0; i < Entries.Count; i++)
				if (KingdomConstructionInputLeaseAuthority.IsLeased(
					leases, Entries[i].Owner)) return false;
			return true;
		}

		private bool AllStillCommitted()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				int volume = (entry.Vessel == null) ? -1 : entry.Vessel.Volume;
				bool emptyOrPure = entry.Vessel != null &&
					(volume == 0 ? entry.Vessel.ComponentLiquids.Count == 0 : KingdomLiquids.HasFreshWater(entry.Vessel));
				if (!KingdomWaterDebitRules.EntryStillCommitted(
					entry.OriginalVolume,
					volume,
					entry.Allocation,
					emptyOrPure,
					IsDedicated(entry),
					OwnsVessel(entry.Owner, entry.Vessel),
					entry.Vessel != null && entry.Vessel.MaxVolume == entry.OriginalMaxVolume,
					LocationMatches(entry),
					entry.Vessel != null && ReferenceEquals(entry.Vessel.ComponentLiquids,
						entry.ComponentIdentity) && CompositionMatches(entry, volume)))
				{
					return false;
				}
			}
			return true;
		}

		private bool RestoreAll(out bool NotificationClean)
		{
			NotificationClean = true;
			bool exact = true;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				if (SnapshotMatches(entry))
				{
					continue;
				}
				if (!BindingMatches(entry) || !entry.DrainAttempted ||
					!StateMatches(entry, entry.OriginalVolume - entry.Allocation) ||
					(entry.Dedicated && !KingdomConstructionInputLeaseAuthority
						.TryObjectAvailableForLocalDebit(entry.Owner, out _))
					|| !TryAssignSnapshot(entry))
				{
					entry.ObservationUncertain = true;
					NotificationClean = false;
					exact = false;
					continue;
				}
				try
				{
					entry.Vessel.Update();
				}
				catch
				{
					NotificationClean = false;
				}
				if (!SnapshotMatches(entry))
				{
					entry.ObservationUncertain = true;
					exact = false;
				}
				if (!RestoreProgressMatches(i)) exact = false;
			}
			return exact && AllSnapshotsMatch();
		}

		private bool AllSnapshotsMatch()
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				if (!SnapshotMatches(Entries[i])) return false;
			}
			return true;
		}

		private bool DrainProgressMatches(int LastDrained)
		{
			bool exact = true;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				int expected = i <= LastDrained
					? entry.OriginalVolume - entry.Allocation
					: entry.OriginalVolume;
				if (!StateMatches(entry, expected))
				{
					entry.ObservationUncertain = true;
					exact = false;
				}
			}
			return exact;
		}

		private bool RestoreProgressMatches(int RestoredThrough)
		{
			bool exact = true;
			for (int i = 0; i < Entries.Count; i++)
			{
				Entry entry = Entries[i];
				bool matches = SnapshotMatches(entry);
				if (!matches && i > RestoredThrough && entry.DrainAttempted)
				{
					matches = StateMatches(entry, entry.OriginalVolume - entry.Allocation);
				}
				if (!matches)
				{
					entry.ObservationUncertain = true;
					exact = false;
				}
			}
			return exact;
		}

	}
}
