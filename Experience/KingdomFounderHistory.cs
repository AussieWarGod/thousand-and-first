using System;
using HistoryKit;
using XRL;
using XRL.UI;

namespace ThousandAndFirst
{
	/// <summary>Best-effort projection of one exact founder rite into public Qud history.</summary>
	public static partial class KingdomFounderHistory
	{
		public const string OptionId = "r_TAF_OptionFounderHistory";

		public static bool ConfiguredEnabled
		{
			get { return Options.GetOption(OptionId, "Yes") != "No"; }
		}

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
					History history = The.Game?.sultanHistory;
					long now = The.Game == null || The.Game.TimeTicks < DeathTick
						? DeathTick : The.Game.TimeTicks;
					KingdomFounderHistoryReceipt prepared;
					string preparationFailure = "";
					if (history == null || !KingdomFounderHistoryRules.TryPrepare(
						System.RealmId, DeathToken, DeathTick, now, history.currentYear,
						FounderName, CityName, RegionName, Cause, ConfiguredEnabled,
						out prepared, out preparationFailure))
					{
						KingdomLog.Log("founder history: preparation refused ("
							+ (preparationFailure ?? "history unavailable") + ")");
						return false;
					}
					System.FounderHistory = prepared;
					receipt = prepared;
				}
				if (receipt.Phase == KingdomFounderHistoryPhase.Suppressed) return true;
				if (receipt.Phase == KingdomFounderHistoryPhase.Quarantined) return false;
				// A later succession maintains the first public memory; it never mints another.
				return TryReconcile(System, out _);
			}
			catch (Exception ex)
			{
				Quarantine(System?.FounderHistory,
					"publication threw " + ex.GetType().Name);
				MetricsManager.LogError(
					"ThousandAndFirst: public founder history publication failed", ex);
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
					KingdomLog.Log("founder history: recovery waiting (" + failure + ")");
			}
			catch (Exception ex)
			{
				Quarantine(receipt, "recovery threw " + ex.GetType().Name);
				MetricsManager.LogError(
					"ThousandAndFirst: public founder history recovery failed", ex);
			}
		}

		private static bool TryReconcile(KingdomSystem System, out string Failure)
		{
			Failure = "";
			KingdomFounderHistoryReceipt receipt = System?.FounderHistory;
			string receiptFailure;
			if (!KingdomFounderHistoryRules.Validate(receipt, out receiptFailure))
				return Quarantine(receipt, receiptFailure, out Failure);
			History history = The.Game?.sultanHistory;
			if (history == null)
			{
				Failure = "Qud history is not loaded";
				return false;
			}
			HistoricEntity entity;
			if (!TryEnsureEntity(history, receipt, out entity, out Failure)) return false;
			if (!TryEnsureEvent(history, entity, receipt, out Failure)) return false;
			if (!TryEnsureNote(receipt, out Failure)) return false;
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
			KingdomLog.Log("founder history: quarantined (" + Failure + ")");
			return false;
		}
	}
}
