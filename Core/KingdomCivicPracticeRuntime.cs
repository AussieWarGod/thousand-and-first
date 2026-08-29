#if !TAF_TESTS
using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Player-facing D1 opener and exact-current C18 choice commit.</summary>
	public static partial class KingdomCivicPracticeRuntime
	{
		public static bool TryOpenCurrent(KingdomSystem system, Zone zone,
			out KingdomSitePracticeChoiceView view, out string failure)
		{
			view = null;
			failure = null;
			KingdomSurvey survey = KingdomSurvey.Take(zone, system);
			if (!KingdomSitePracticeRuntime.TryPreviewCurrent(system, zone, survey,
				out KingdomSitePracticePreview preview, out failure)) return false;
			if (!system.TryGetCurrentIdentity(out string exactRealmId,
				out string exactSettlementId) || !string.Equals(exactSettlementId,
				preview.Snapshot.SettlementId, StringComparison.Ordinal))
			{
				failure = "The current realm or loaded city changed while opening this choice.";
				return false;
			}
			return KingdomSitePracticeChoiceView.TryCreate(exactRealmId, preview,
				out view, out failure);
		}

		public static bool TryChooseCurrent(KingdomSystem system, Zone zone,
			KingdomSitePracticeChoiceView openedView, int reading,
			out KingdomCivicPracticeCommitResult result, out string failure)
		{
			result = null;
			failure = null;
			if (openedView == null)
			{
				failure = "No site practice choice is open.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone, system);
			if (!KingdomSitePracticeRuntime.TryPreviewCurrent(system, zone, survey,
				out KingdomSitePracticePreview fresh, out failure)) return false;
			if (!system.TryGetCurrentIdentity(out string exactRealmId,
				out string exactSettlementId) || !string.Equals(exactSettlementId,
				fresh.Snapshot.SettlementId, StringComparison.Ordinal))
			{
				failure = "The current realm or loaded city changed before this choice.";
				return false;
			}
			if (!openedView.Matches(exactRealmId, fresh, out failure)) return false;
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			if (memory == null)
			{
				failure = "Civic memory is unavailable in this save.";
				return false;
			}
			long now = Math.Max(0L, The.Game.TimeTicks);
			return KingdomCivicPracticeTransactions.TryChoose(new SystemPort(memory),
				exactRealmId, openedView, reading, now, out result, out failure);
		}

		private sealed class SystemPort : IKingdomCivicPracticeSectionPort
		{
			private readonly KingdomCivicMemorySystem Memory;

			internal SystemPort(KingdomCivicMemorySystem memory)
			{
				Memory = memory;
			}

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
