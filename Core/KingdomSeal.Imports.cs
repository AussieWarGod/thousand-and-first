using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		internal bool TryReserveImport(KingdomImportPolicy Policy, out KingdomSealRecord Legacy,
			out KingdomSealReceipt Receipt, out KingdomSealReservationLease Lease, out string Failure)
		{
			Legacy = null;
			Receipt = null;
			Lease = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (!LegacyImportEnabled())
				{
					return true;
				}
				if (The.Game == null || !KingdomSealReceipt.ValidId(The.Game.GameID))
				{
					Failure = "the target game has no valid identity";
					return false;
				}
				TryReconcileProfile("import selection");
				KingdomSealStore store = GetStore();
				int refused;
				List<KingdomSealRecord> legacies = store.ReadLegacies(out refused);
				if (refused > 0)
				{
					Failure = "one or more legacy files could not be validated; latest selection is ambiguous";
					return false;
				}
				List<KingdomSealReceipt> receipts = store.ReadReceipts(out refused);
				if (refused > 0)
				{
					Failure = "one or more receipt files could not be validated; import selection is ambiguous";
					return false;
				}
				KingdomSealReceipt targetReceipt = null;
				HashSet<string> unavailable = new HashSet<string>();
				for (int i = 0; i < receipts.Count; i++)
				{
					KingdomSealReceipt item = receipts[i];
					unavailable.Add(item.LegacyId);
					if (item.TargetGameId == The.Game.GameID
						&& (item.State == KingdomSealReceiptState.Reserved
							|| item.State == KingdomSealReceiptState.Committed))
					{
						if (targetReceipt != null && targetReceipt.LegacyId != item.LegacyId)
						{
							Failure = "the target game has more than one import receipt";
							return false;
						}
						targetReceipt = item;
					}
				}
				if (targetReceipt != null)
				{
					for (int i = 0; i < legacies.Count; i++)
					{
						if (legacies[i].LegacyId == targetReceipt.LegacyId
							&& legacies[i].LineageId == targetReceipt.LineageId)
						{
							if (targetReceipt.State == KingdomSealReceiptState.Reserved
								&& !store.TryAcquireReservationLease(targetReceipt,
									out Lease, out Failure))
							{
								return false;
							}
							Legacy = legacies[i];
							Receipt = targetReceipt;
							return true;
						}
					}
					Failure = "the target receipt names no validated immutable legacy";
					return false;
				}

				KingdomSealRecord selected = KingdomSealRules.Select(legacies, unavailable, Policy);
				if (selected == null)
				{
					return true;
				}
				KingdomSealReceipt claimed;
				if (!store.TryClaimReservation(selected, The.Game.GameID,
					SafeTick(The.Game.TimeTicks), out claimed, out Lease, out Failure))
				{
					return false;
				}
				Legacy = selected;
				Receipt = claimed;
				return true;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Reads the monotone on-disk state of this target's exact persisted tuple.</summary>
		internal bool TryInspectImport(KingdomSealReceipt Expected,
			out KingdomSealReceipt Current, out string Failure)
		{
			Current = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Expected == null || The.Game == null
					|| Expected.TargetGameId != The.Game.GameID)
				{
					Failure = "only this target game's expected receipt can be inspected";
					return false;
				}
				return GetStore().TryInspectReceipt(Expected, out Current, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Reacquires the live OS claim for this target's exact persisted reservation.</summary>
		internal bool TryResumeImport(KingdomSealReceipt Reserved,
			out KingdomSealReservationLease Lease, out string Failure)
		{
			Lease = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Reserved == null || Reserved.State != KingdomSealReceiptState.Reserved
					|| The.Game == null || Reserved.TargetGameId != The.Game.GameID)
				{
					Failure = "only this target game's exact reserved receipt can resume its live claim";
					return false;
				}
				return GetStore().TryAcquireReservationLease(Reserved, out Lease, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Commits only the exact reservation whose target still holds its OS claim.</summary>
		internal bool TryCommitImport(KingdomSealReceipt Reserved,
			KingdomSealReservationLease Lease, out KingdomSealReceipt Committed,
			out string Failure)
		{
			Committed = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Reserved == null || Reserved.State != KingdomSealReceiptState.Reserved
					|| The.Game == null || Reserved.TargetGameId != The.Game.GameID)
				{
					Failure = "only this target game's exact reserved receipt can be committed";
					return false;
				}
				return GetStore().TryCommitReservation(Reserved, Lease,
					SafeTick(The.Game.TimeTicks), out Committed, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Marks an explicit player decline final and spent. Silence/crash never calls it.</summary>
		internal bool TryDeclineImport(KingdomSealReceipt Reserved, out string Failure)
		{
			Failure = "";
			if (!TryRequireAuthority(out Failure))
			{
				return false;
			}
			if (Reserved == null || Reserved.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reservation can be declined";
				return false;
			}
			KingdomSealReceipt declined = CopyReceipt(Reserved, KingdomSealReceiptState.Declined,
				SafeTick(The.Game?.TimeTicks ?? Reserved.WrittenTick));
			try
			{
				return GetStore().TryWriteReceipt(declined, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Releases a worldgen site refusal. Final receipts are never releasable.</summary>
		internal bool TryReleaseImport(KingdomSealReceipt Reserved, out string Failure)
		{
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				return GetStore().TryReleaseReservation(Reserved, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Releases an exact site refusal while the target still holds its OS live claim.</summary>
		internal bool TryReleaseImport(KingdomSealReceipt Reserved,
			KingdomSealReservationLease Lease, out string Failure)
		{
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				return GetStore().TryReleaseReservation(Reserved, Lease, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		/// <summary>Loader/import hook for bounded later-boot reconciliation.</summary>
		internal void ReconcileProfile()
		{
			KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(kingdom))
			{
				return;
			}
			string authorityFailure;
			if (!TryRequireAuthority(out authorityFailure))
			{
				ReportFailure("loader reconciliation", authorityFailure);
				return;
			}
			if (SealEnabled())
			{
				TryReconcileProfile("loader reconciliation");
				string failure;
				if (!TrySynchronizeLoadedWorld(out failure))
				{
					ReportFailure("loader stage reconciliation", failure);
				}
			}
		}

	}
}
