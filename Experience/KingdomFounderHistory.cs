using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// Best-effort reconstruction of one exact founder rite as a TAF-owned read-only projection.
	/// Schema 2 never publishes to Qud's global HistoryKit or journal collections.
	/// </summary>
	public static partial class KingdomFounderHistory
	{
		public static bool PublishBestEffort(KingdomSystem System, string DeathToken,
			long DeathTick, string FounderName, string CityName, string RegionName,
			string Cause)
		{
			if (System == null) return false;
			try
			{
				KingdomFounderHistoryReceipt receipt = System.FounderHistory;
				if (receipt == null || receipt.Phase == KingdomFounderHistoryPhase.None)
				{
					long now = The.Game == null || The.Game.TimeTicks < DeathTick
						? DeathTick : The.Game.TimeTicks;
					long historicYear = DeathTick / XRL.World.Calendar.TurnsPerYear + 1001L;
					KingdomFounderHistoryReceipt prepared;
					string preparationFailure = "";
					if (!KingdomFounderHistoryRules.TryPrepare(
						System.RealmId, DeathToken, DeathTick, now, historicYear,
						FounderName, CityName, RegionName, Cause, Enabled: true,
						out prepared, out preparationFailure))
					{
						KingdomLog.Log("founder memory: preparation refused ("
							+ (preparationFailure ?? "invalid evidence") + ")");
						return false;
					}
					System.FounderHistory = prepared;
					receipt = prepared;
				}
				if (receipt.Phase == KingdomFounderHistoryPhase.Suppressed) return true;
				if (receipt.Phase == KingdomFounderHistoryPhase.Quarantined) return false;
				// A later succession maintains the first local projection; it never mints another.
				return TryReconcile(System, out _);
			}
			catch (Exception ex)
			{
				Quarantine(System?.FounderHistory,
					"publication threw " + ex.GetType().Name);
				MetricsManager.LogError(
					"ThousandAndFirst: founder-memory projection failed", ex);
				return false;
			}
		}

		public static void ReconcileBestEffort(KingdomSystem System)
		{
			KingdomFounderHistoryReceipt receipt = System?.FounderHistory;
			if (receipt == null || receipt.Phase == KingdomFounderHistoryPhase.None
				|| receipt.Phase == KingdomFounderHistoryPhase.Suppressed
				|| receipt.Phase == KingdomFounderHistoryPhase.Quarantined) return;
			try
			{
				string failure;
				if (!TryReconcile(System, out failure) && !string.IsNullOrEmpty(failure))
					KingdomLog.Log("founder memory: recovery waiting (" + failure + ")");
			}
			catch (Exception ex)
			{
				Quarantine(receipt, "recovery threw " + ex.GetType().Name);
				MetricsManager.LogError(
					"ThousandAndFirst: founder-memory recovery failed", ex);
			}
		}

		private static bool TryReconcile(KingdomSystem System, out string Failure)
		{
			Failure = "";
			KingdomFounderHistoryReceipt receipt = System?.FounderHistory;
			string receiptFailure;
			if (!KingdomFounderHistoryRules.Validate(receipt, out receiptFailure))
				return Quarantine(receipt, receiptFailure, out Failure);
			if (!TryEnsureLegacyIsolation(receipt, out Failure)) return false;
			KingdomFounderHistoryProjection projection;
			if (!TryBuildProjection(receipt, out projection, out Failure))
				return Quarantine(receipt, Failure, out Failure);
			long now = The.Game == null || The.Game.TimeTicks < receipt.PreparedTick
				? receipt.PreparedTick : The.Game.TimeTicks;
			receipt.Phase = KingdomFounderHistoryPhase.Committed;
			receipt.CommittedTick = now;
			receipt.Fault = "";
			return KingdomFounderHistoryRules.Validate(receipt, out Failure)
				|| Quarantine(receipt, Failure, out Failure);
		}

		private static bool Quarantine(KingdomFounderHistoryReceipt Receipt, string Reason)
		{
			string ignored;
			return Quarantine(Receipt, Reason, out ignored);
		}

		private static bool Quarantine(KingdomFounderHistoryReceipt Receipt, string Reason,
			out string Failure)
		{
			Failure = KingdomFounderHistoryRules.QuarantineReason(Reason);
			if (Receipt != null)
			{
				Receipt.Phase = KingdomFounderHistoryPhase.Quarantined;
				Receipt.PublicationEnabled = true;
				Receipt.CommittedTick = 0L;
				Receipt.Fault = Failure;
			}
			KingdomLog.Log("founder memory: quarantined (" + Failure + ")");
			return false;
		}
	}
}
