using System;
using System.Collections.Generic;
using XRL.UI;

namespace ThousandAndFirst
{
	/// <summary>
	/// Runtime owner of the realm-wide master switch. It is intentionally reached before any
	/// guarded delegate is created: the steady disabled path reads the realm fence, four persisted
	/// scalars, and the option, then returns without touching a city, zone, collection, random
	/// source, or logger.
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
			if (!RootAuthorityAvailable(system)) return false;
			bool configured = ConfiguredEnabled;
			KingdomMasterDecision decision = KingdomMasterRules.Observe(system.MasterOption,
				system.MasterOptionTick, system.MasterResumeToken,
				system.MasterAppliedResumeToken, configured, now);
			if (!decision.Valid) return false;
			if (decision.Transition == KingdomMasterTransition.None)
				return decision.AutomaticWorkAllowed && decision.ChangedAtTick != now;

			if (decision.Transition == KingdomMasterTransition.ResumeRequired)
			{
				KingdomMasterDecision applied = KingdomMasterRules.ApplyResume(decision);
				if (applied == null || !applied.AutomaticWorkAllowed) return false;
				KingdomMasterResumePlan plan;
				if (!KingdomMasterResumePlan.TryCreate(system, decision.ChangedAtTick,
					system.MasterOptionTick, out plan)) return false;
				if (!plan.Publish()) return false;
				decision = applied;
			}

			PublishLatch(system, decision);
			// Initialization and both real transitions consume this wake. This is the equal-tick
			// precedence rule, not a one-tick simulation delay: the next wake sees the published latch.
			return false;
		}

		/// <summary>Current explicit mutation gate. Reports and committed recovery use their own lanes.</summary>
		public static bool NewWorkAllowed(KingdomSystem system)
		{
			return RootAuthorityAvailable(system) && ConfiguredEnabled
				&& (system.MasterOption == KingdomMasterLatchValue.Unobserved
					|| (system.MasterOption == KingdomMasterLatchValue.Enabled
						&& system.MasterResumeToken == system.MasterAppliedResumeToken));
		}

		/// <summary>Cheap guard for auxiliary systems which do not own transition observation.</summary>
		public static bool AutomaticWorkAllowed(KingdomSystem system)
		{
			long now = XRL.The.Game?.TimeTicks ?? -1L;
			return RootAuthorityAvailable(system) && ConfiguredEnabled
				&& system.MasterOption == KingdomMasterLatchValue.Enabled
				&& system.MasterResumeToken == system.MasterAppliedResumeToken
				&& (now < 0L || system.MasterOptionTick != now);
		}

		/// <summary>A missing or failed root system cannot authorize any mutation or recovery.
		/// Presentation-only failure reporting remains on its own event path.</summary>
		private static bool RootAuthorityAvailable(KingdomSystem system)
		{
			return system != null && !system.LoadFailed && !system.RealmRetirementBlocksWork;
		}

		private static void PublishLatch(KingdomSystem system, KingdomMasterDecision decision)
		{
			system.MasterOption = decision.State;
			system.MasterOptionTick = decision.ChangedAtTick;
			system.MasterResumeToken = decision.ResumeToken;
			system.MasterAppliedResumeToken = decision.AppliedResumeToken;
		}

		/// <summary>All allocations and list walks are confined to the one resume transition.</summary>
		private sealed partial class KingdomMasterResumePlan
		{
			private readonly KingdomSystem System;
			private readonly SettlementPlan Seat;
			private readonly List<KingdomSettlement> NonSeat;
			private readonly List<SettlementPlan> NonSeatPlans;
			private readonly TradePlan Trade;
			private readonly Simulation.City.KingdomJobTable ConstructionRoutes;
			private readonly KingdomConstructionMasterPausePlan Construction;
			private readonly KingdomExperienceMasterResumePlan Experience;
			private readonly KingdomPolityMasterResumePlan Polity;
			private readonly MasterResumeSources Sources;

			private KingdomMasterResumePlan(KingdomSystem system, SettlementPlan seat,
				List<KingdomSettlement> nonSeat, List<SettlementPlan> nonSeatPlans,
				TradePlan trade,
				Simulation.City.KingdomJobTable constructionRoutes,
				KingdomConstructionMasterPausePlan construction,
				KingdomExperienceMasterResumePlan experience,
				KingdomPolityMasterResumePlan polity, MasterResumeSources sources)
			{
				System = system;
				Seat = seat;
				NonSeat = nonSeat;
				NonSeatPlans = nonSeatPlans;
				Trade = trade;
				ConstructionRoutes = constructionRoutes;
				Construction = construction;
				Experience = experience;
				Polity = polity;
				Sources = sources;
			}

			internal static bool TryCreate(KingdomSystem system, long now, long disabledAt,
				out KingdomMasterResumePlan plan)
			{
				plan = null;
				if (system == null || now < disabledAt || disabledAt < 0L) return false;
				SettlementPlan seat;
				List<KingdomSettlement> nonSeat = system.NonSeatSettlements();
				List<SettlementPlan> nonSeatPlans = new List<SettlementPlan>();
				MasterResumeSources sources;
				TradePlan trade;
				Simulation.City.KingdomJobTable constructionRoutes;
				Simulation.City.KingdomCityFault constructionRouteFault;
				KingdomConstructionMasterPausePlan construction;
				KingdomExperienceMasterResumePlan experience;
				KingdomPolityMasterResumePlan polity;
				string constructionFailure;
				if (!MasterResumeSources.TryCapture(system, nonSeat, out sources)
					|| !SettlementPlan.TryCreate(system, now, disabledAt, out seat)
					|| !TradePlan.TryCreate(system.TradeBook, now, disabledAt, out trade)
					|| !KingdomConstruction.TryPrepareMasterResume(disabledAt, now,
						out construction, out constructionFailure)
					|| !Simulation.City.KingdomCentralLogistics
						.TryPrepareConstructionInputMasterResume(system,
							construction.CopyTargets(), out constructionRoutes,
							out constructionRouteFault)
					|| !KingdomExperienceRules.TryPrepareMasterResume(sources.ExperienceOwner,
						system.RealmId, disabledAt, now,
						Options.GetOption(KingdomExperienceOptions.StoryOptionId, "Yes") != "No",
						Options.GetOption(KingdomExperienceOptions.KnowledgeOptionId, "Yes") != "No",
						Options.GetOption(KingdomExperienceOptions.AmbientOptionId, "Yes") != "No",
						out experience, out string _)
					|| system.PolityLedger == null || system.PolityDispatch == null
					|| !KingdomPolityRules.TryPrepareMasterResume(system.PolityLedger,
						system.PolityDispatch, system.PolityLedger.Revision,
						KingdomPolityPresentationRuntime.ConfiguredState, now,
						out polity, out string _))
					return false;
				for (int i = 0; i < nonSeat.Count; i++)
				{
					if (!SettlementPlan.TryCreate(nonSeat[i], now, disabledAt,
						out SettlementPlan other)) return false;
					nonSeatPlans.Add(other);
				}
				plan = new KingdomMasterResumePlan(system, seat, nonSeat, nonSeatPlans, trade,
					constructionRoutes, construction, experience, polity, sources);
				return true;
			}

			internal bool Publish()
			{
				if (!Preflight(out _)) return false;
				PublishPrevalidated(); return true;
			}
		}
	}
}
