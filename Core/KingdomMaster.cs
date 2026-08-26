using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	/// <summary>
	/// Runtime owner of the realm-wide master switch. It is intentionally reached before any
	/// guarded delegate is created: the steady disabled path reads four persisted scalars and the
	/// option, then returns without touching a city, zone, collection, random source, or logger.
	/// </summary>
	public static partial class KingdomMaster
	{
		public const string OptionId = "r_TAF_OptionMaster";

		public static bool ConfiguredEnabled
		{
			get { return Options.GetOption(OptionId, "Yes") != "No"; }
		}

		/// <summary>
		/// Observes one automatic wake. False also means “transition handled”: due work never runs on
		/// the same tick as disable or resume. A failed resume leaves the durable latch disabled.
		/// </summary>
		public static bool ObserveAutomaticWake(KingdomSystem system, long now)
		{
			if (system == null) return false;
			bool configured = ConfiguredEnabled;
			KingdomMasterDecision decision = KingdomMasterRules.Observe(system.MasterOption,
				system.MasterOptionTick, system.MasterResumeToken,
				system.MasterAppliedResumeToken, configured, now);
			if (!decision.Valid) return false;
			if (decision.Transition == KingdomMasterTransition.None)
				return decision.AutomaticWorkAllowed && decision.ChangedAtTick != now;

			if (decision.Transition == KingdomMasterTransition.ResumeRequired)
			{
				KingdomMasterResumePlan plan;
				if (!KingdomMasterResumePlan.TryCreate(system, decision.ChangedAtTick,
					system.MasterOptionTick, out plan)) return false;
				plan.Publish();
				decision = KingdomMasterRules.ApplyResume(decision);
				if (decision == null || !decision.AutomaticWorkAllowed) return false;
			}

			PublishLatch(system, decision);
			// Initialization and both real transitions consume this wake. This is the equal-tick
			// precedence rule, not a one-tick simulation delay: the next wake sees the published latch.
			return false;
		}

		/// <summary>Current explicit mutation gate. Reports and committed recovery use their own lanes.</summary>
		public static bool NewWorkAllowed(KingdomSystem system)
		{
			return system != null && ConfiguredEnabled
				&& (system.MasterOption == KingdomMasterLatchValue.Unobserved
					|| (system.MasterOption == KingdomMasterLatchValue.Enabled
						&& system.MasterResumeToken == system.MasterAppliedResumeToken));
		}

		/// <summary>Cheap guard for auxiliary systems which do not own transition observation.</summary>
		public static bool AutomaticWorkAllowed(KingdomSystem system)
		{
			long now = XRL.The.Game?.TimeTicks ?? -1L;
			return system != null && ConfiguredEnabled
				&& system.MasterOption == KingdomMasterLatchValue.Enabled
				&& system.MasterResumeToken == system.MasterAppliedResumeToken
				&& (now < 0L || system.MasterOptionTick != now);
		}

		private static void PublishLatch(KingdomSystem system, KingdomMasterDecision decision)
		{
			system.MasterOption = decision.State;
			system.MasterOptionTick = decision.ChangedAtTick;
			system.MasterResumeToken = decision.ResumeToken;
			system.MasterAppliedResumeToken = decision.AppliedResumeToken;
		}

		/// <summary>All allocations and list walks are confined to the one resume transition.</summary>
		private sealed class KingdomMasterResumePlan
		{
			private readonly KingdomSystem System;
			private readonly SettlementPlan Seat;
			private readonly SettlementPlan Away;
			private readonly TradePlan Trade;

			private KingdomMasterResumePlan(KingdomSystem system, SettlementPlan seat,
				SettlementPlan away, TradePlan trade)
			{
				System = system;
				Seat = seat;
				Away = away;
				Trade = trade;
			}

			internal static bool TryCreate(KingdomSystem system, long now, long disabledAt,
				out KingdomMasterResumePlan plan)
			{
				plan = null;
				if (system == null || now < disabledAt || disabledAt < 0L) return false;
				SettlementPlan seat;
				SettlementPlan away = null;
				TradePlan trade;
				if (!SettlementPlan.TryCreate(system, now, disabledAt, out seat)
					|| (system.Away != null && !SettlementPlan.TryCreate(system.Away,
						now, disabledAt, out away))
					|| !TradePlan.TryCreate(system.TradeBook, now, disabledAt, out trade))
					return false;
				plan = new KingdomMasterResumePlan(system, seat, away, trade);
				return true;
			}

			internal void Publish()
			{
				Seat.Publish(System);
				if (Away != null) Away.Publish(System.Away);
				if (Trade != null) Trade.Publish(System.TradeBook);
				// Realm-level renderer checkpoints. Existing open jobs and their semantic receipts are
				// deliberately untouched; they resume as the same committed recovery on the next wake.
				System.LastSliceTick = Seat.Now;
				System.ReifyTick = Seat.Now;
				System.ReifyQuietUntilTick = Seat.Now;
			}
		}
	}
}
