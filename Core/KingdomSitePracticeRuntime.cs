using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Read-only D1 adapter for the exact loaded current city.</summary>
	public static class KingdomSitePracticeRuntime
	{
		public static bool TryPreviewCurrent(KingdomSystem system, Zone zone,
			KingdomSurvey survey, out KingdomSitePracticePreview preview,
			out string failure)
		{
			preview = null;
			failure = null;
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(system, zone, survey,
				requireSurvey: true, out KingdomCurrentCityEvidenceRuntime.Context context,
				out failure)) return false;
			if (!TryFounding(context, out KingdomSiteFoundingEvidence founding, out failure) ||
				!KingdomCurrentCityEvidenceRuntime.TryBuiltWorks(context,
					out List<KingdomCurrentCityEvidenceRuntime.Work> currentWorks,
					out failure)) return false;
			List<KingdomSiteBuiltWorkEvidence> evidence =
				new List<KingdomSiteBuiltWorkEvidence>(currentWorks.Count);
			for (int i = 0; i < currentWorks.Count; i++)
				evidence.Add(currentWorks[i].Evidence);
			return KingdomSitePracticeRules.TryBuildPreview(founding, evidence,
				out preview, out failure);
		}

		private static bool TryFounding(KingdomCurrentCityEvidenceRuntime.Context context,
			out KingdomSiteFoundingEvidence evidence, out string failure)
		{
			evidence = null;
			failure = null;
			KingdomSystem system = context.System;
			if (system.SettlementIdentityOrigin != KingdomIdentityOrigin.FoundingTransaction ||
				!KingdomIdentityRules.IsFoundingTransaction(context.FoundingTransactionId) ||
				string.IsNullOrWhiteSpace(context.Vocation) ||
				!KingdomSettlement.IsKnownVocation(context.Vocation) ||
				string.IsNullOrWhiteSpace(context.Style) ||
				string.IsNullOrWhiteSpace(context.Terrain) ||
				string.IsNullOrWhiteSpace(context.Region) ||
				string.IsNullOrEmpty(context.FoundingZoneId) || context.FoundedTick < 0L)
			{
				failure = "This city lacks exact founding, vocation, terrain, or region evidence.";
				return false;
			}
			KingdomFoundingKind kind = context.FoundingTransactionId ==
				system.RealmIdentityTransactionId
					? KingdomFoundingKind.FirstCity : KingdomFoundingKind.SecondCity;
			string deed = KingdomFoundingTransaction.FoundingEventID(kind,
				context.FoundingTransactionId, "chronicle");
			if (string.IsNullOrEmpty(deed) || !ExactTerminalDeed(deed))
			{
				failure = "The city's exact terminal founding receipt cannot be proved.";
				return false;
			}
			evidence = new KingdomSiteFoundingEvidence
			{
				SettlementId = context.SettlementId,
				Vocation = context.Vocation,
				Style = context.Style,
				Terrain = context.Terrain,
				Region = context.Region,
				Creed = null,
				DeedReceiptId = deed,
				DeedText = "the founding transaction at " + context.FoundingZoneId,
				FoundedTick = context.FoundedTick
			};
			return true;
		}

		private static bool ExactTerminalDeed(string deed)
		{
			if (!KingdomChronicle.TryCaptureRealmRegistry(out string registry,
				out string _, out string _) ||
				!KingdomChronicleReceiptRules.TryParseRegistry(registry,
					out List<KingdomChronicleReceipt> rows, out bool migrated,
					out KingdomChronicleRegistryFault _) || migrated) return false;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].EventId == deed) return !rows[i].LegacyBlocked &&
					KingdomChronicleReceiptRules.IsTerminal(rows[i]);
			return false;
		}
	}
}
