#if !TAF_TESTS
using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomVocationServiceRuntime
	{
		/// <summary>Reproves the loaded source, then records one exact C18 receipt.</summary>
		internal static bool TryExecuteCurrent(KingdomSystem system, Zone zone,
			KingdomVocationServiceOffer openedOffer,
			out KingdomVocationServiceCommitResult result, out string failure)
		{
			result = null; failure = null;
			if (!KingdomGovernanceScope.TryReserve("record vocation service",
				out KingdomGovernanceReservation reservation))
			{
				failure = "No active uncommitted Charter action can record this vocation service.";
				return false;
			}
			try
			{
				if (!KingdomMaster.NewWorkAllowed(system))
				{
					failure = "Settlement simulation is paused; no vocation service was recorded.";
					return false;
				}
				if (!TryOpenCurrent(system, zone, out KingdomVocationServiceOffer fresh,
					out failure) || !KingdomVocationServiceRules.TryMatchAvailableOffers(
						openedOffer, fresh, out failure)) return false;
				if (!system.TryGetCurrentIdentity(out string exactRealmId,
					out string exactSettlementId) || exactSettlementId != fresh.SettlementId)
				{
					failure = "The current realm or loaded city changed before service.";
					return false;
				}
				KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
				if (memory == null)
				{
					failure = "Civic memory is unavailable in this save.";
					return false;
				}
				if (!KingdomMaster.NewWorkAllowed(system))
				{
					failure = "Settlement simulation paused before the vocation receipt could commit.";
					return false;
				}
				long now = Math.Max(0L, The.Game.TimeTicks);
				if (!KingdomVocationServiceTransactions.TryRecordGoverned(new ServicePort(memory),
					exactRealmId, fresh, now, reservation,
					out KingdomVocationServiceCommitResult recorded,
					out failure)) return false;
				if (recorded == null)
				{
					failure = "Civic memory accepted no vocation result."; return false;
				}
				if (recorded.Changed && !KingdomGovernanceScope.HasCommitted)
				{
					KingdomLog.Log("governance: governed vocation publication lost its commit boundary");
					failure = "The durable vocation receipt lost its Charter action boundary.";
					return false;
				}
				result = recorded; return true;
			}
			finally { reservation.Dispose(); }
		}

		/// <summary>Reads durable C18 history without opening work or spending governance.</summary>
		public static bool TryReadCurrentHistory(KingdomSystem system, Zone zone,
			out string history, out string failure)
		{
			history = null; failure = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(system, zone, null, false,
				out KingdomCurrentCityEvidenceRuntime.Context context, out failure)) return false;
			if (!system.TryGetCurrentIdentity(out string exactRealmId,
				out string exactSettlementId) || exactSettlementId != context.SettlementId)
			{
				failure = "The current realm or loaded city changed before history was read.";
				return false;
			}
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
			{
				failure = "Civic memory is unavailable in this save.";
				return false;
			}
			return KingdomVocationServiceTransactions.TryReadHistory(new ServicePort(memory),
				exactRealmId, exactSettlementId, context.Vocation, out history, out failure);
		}

		internal static bool TryReadCurrentView(KingdomSystem system, Zone zone,
			KingdomVocationServiceOffer offer, out string history,
			out KingdomVocationServiceStatus status, out string failure)
		{
			history = null; status = null; failure = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(system, zone, null, false,
				out KingdomCurrentCityEvidenceRuntime.Context context, out failure)) return false;
			if (!system.TryGetCurrentIdentity(out string exactRealmId,
				out string exactSettlementId) || exactSettlementId != context.SettlementId)
			{
				failure = "The current realm or loaded city changed before the service view.";
				return false;
			}
			if (offer == null || offer.SettlementId != exactSettlementId ||
				offer.Vocation != context.Vocation)
			{
				failure = "The vocation report does not belong to this exact current city.";
				return false;
			}
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
			{
				failure = "Civic memory is unavailable in this save."; return false;
			}
			return KingdomVocationServiceTransactions.TryReadView(new ServicePort(memory),
				exactRealmId, exactSettlementId, context.Vocation, offer,
				out history, out status, out failure);
		}

		public static bool TryReadRealmResults(KingdomSystem system, Zone zone,
			out List<string> pages, out string failure)
		{
			pages = null; failure = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(system, zone, null, false,
				out KingdomCurrentCityEvidenceRuntime.Context context, out failure)) return false;
			if (!system.TryGetCurrentIdentity(out string exactRealmId,
				out string exactSettlementId) || exactSettlementId != context.SettlementId)
			{
				failure = "The current realm identity changed before its results were read.";
				return false;
			}
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
			{
				failure = "Civic memory is unavailable in this save."; return false;
			}
			return KingdomVocationServiceTransactions.TryReadRealmResults(
				new ServicePort(memory), exactRealmId, out pages, out failure);
		}

		private sealed class ServicePort : IKingdomCivicPracticeSectionPort
		{
			private readonly KingdomCivicMemorySystem Memory;
			internal ServicePort(KingdomCivicMemorySystem memory) { Memory = memory; }

			public bool TryReadSection(int sectionId,
				out KingdomCivicMemorySectionLease lease, out string failure)
			{
				return Memory.TryReadSection(sectionId, out lease, out failure);
			}

			public bool TryCommitSection(KingdomCivicMemorySectionLease lease,
				byte[] payload, out string failure)
			{
				return Memory.TryCommitSection(lease, payload, out failure);
			}
		}
	}
}
#endif
