using System;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceReportRules
	{
		/// <summary>Publishes only a proved capacity observation, with exact reproof around the
		/// caller's durable parent write. Failure never authorizes a Chronicle attempt or retry.</summary>
		internal static bool TryPublishCapacity(KingdomSubsidenceReportPlan plan, int index,
			KingdomChronicleCapacityWitness witness, Func<bool> reprove,
			Func<KingdomSubsidenceReportPlan, bool> save, out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (reprove == null || save == null || !TryRefuseCapacity(plan, index, witness, out var value)) return false;
			try
			{
				if (!reprove() || !save(value) || !reprove()) return false;
				next = value; return true;
			}
			catch { return false; }
		}

		/// <summary>Capacity refusal is a terminal non-publication, not an observed Lost sink.
		/// The runtime must reprove the exact full-registry observation before saving this plan.</summary>
		internal static bool TryRefuseCapacity(KingdomSubsidenceReportPlan plan, int index,
			KingdomChronicleCapacityWitness witness, out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (!AtFrontier(plan, index)) return false;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			if (!LedgerSettled(entry.LedgerPhase) || entry.ChronicleProved || entry.ChronicleLost
				|| !CapacityFingerprint(plan, index, entry, out string fingerprint)
				|| !KingdomChronicleCapacityRules.Valid(witness, EventId(plan, index), fingerprint)) return false;
			if (entry.CapacityRefused)
			{
				if (entry.CapacityCount != witness.RegistryCount || entry.CapacityHash != witness.RegistryHash
					|| entry.CapacityFingerprint != witness.Fingerprint) return false;
				next = plan; return true;
			}
			KingdomSubsidenceReportPlan value = plan.With(index, entry.WithCapacityRefusal(witness));
			if (!Valid(value)) return false;
			next = value; return true;
		}

		private static bool ValidCapacity(KingdomSubsidenceReportPlan plan, int index, KingdomSubsidenceReportEntry entry)
		{
			if (entry.ChronicleRefusal == ReportChronicleRefusal.None)
				return entry.CapacityCount == 0 && entry.CapacityHash == "" && entry.CapacityFingerprint == "";
			return entry.CapacityRefused && !entry.ChronicleLost && !entry.ChronicleProved
				&& LedgerSettled(entry.LedgerPhase) && entry.CapacityCount == KingdomChronicleReceiptRules.MaxReceipts
				&& KingdomChronicleReceiptRules.IsSha256(entry.CapacityHash)
				&& CapacityFingerprint(plan, index, entry, out string fingerprint) && fingerprint == entry.CapacityFingerprint;
		}

		private static bool CapacityFingerprint(KingdomSubsidenceReportPlan plan, int index,
			KingdomSubsidenceReportEntry entry, out string fingerprint)
		{
			return KingdomChronicleCapacityRules.TryFingerprint(plan.RealmId, plan.SettlementId,
				plan.OwnerId + EventInfix + index.ToString(CultureInfo.InvariantCulture), entry.Text, entry.AtTick, out fingerprint);
		}
	}
}
