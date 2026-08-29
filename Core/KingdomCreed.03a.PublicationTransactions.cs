using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCreed
	{
#if TAF_TESTS
		/// <summary>Test-only exception seam for every effect cut in compensated creed actions.</summary>
		internal static Action<string> PublicationFaultInjection;
#endif

		private static void PublicationCut(string cut)
		{
#if TAF_TESTS
			PublicationFaultInjection?.Invoke(cut);
#endif
		}

		private static bool TryPublishRiteEffects(KingdomSystem system,
			KingdomWaterDebit debit, CityTemper temper, out bool compensationExact)
		{
			compensationExact = true;
			long beforeTick = system.LastRiteTick;
			int beforeDissent = system.Dissent;
			bool exact = true;
			bool published = KingdomGovernanceScope.TryPublish("hold shared rite", delegate
			{
				if (!debit.Commit()) return false;
				PublicationCut("rite:water");
				system.LastRiteTick = XRL.The.Game.TimeTicks;
				PublicationCut("rite:tick");
				system.Dissent = KingdomCreedRules.ApplyDissent(system.Dissent,
					-KingdomCreedRules.RiteEase(temper));
				PublicationCut("rite:dissent");
				return debit.State == KingdomWaterDebitState.Committed &&
					system.LastRiteTick == XRL.The.Game.TimeTicks &&
					system.Dissent == KingdomCreedRules.ApplyDissent(beforeDissent,
						-KingdomCreedRules.RiteEase(temper));
			}, delegate
			{
				exact = false;
				bool water = false;
				try { water = debit.Rollback() || debit.RestorationExact; }
				catch (Exception ex)
				{
					KingdomLog.Log("shared rite water compensation threw (" + ex.Message + ")");
				}
				try
				{
					system.LastRiteTick = beforeTick;
					system.Dissent = beforeDissent;
				}
				catch (Exception ex)
				{
					KingdomLog.Log("shared rite civic compensation threw (" + ex.Message + ")");
				}
				exact = water && system.LastRiteTick == beforeTick &&
					system.Dissent == beforeDissent;
				if (!exact)
				{
					try { system.QuarantineIdentity(
						"shared rite compensation did not restore exact water and civic state"); }
					catch (Exception ex) { KingdomLog.Log(
						"shared rite quarantine threw (" + ex.Message + ")"); }
				}
				return exact;
			});
			compensationExact = exact;
			return published;
		}

		private static bool TryPublishDeclarationEffects(KingdomSystem system,
			string creedFaction, int slightedCount,
			IList<KeyValuePair<string, int>> standing, out bool compensationExact)
		{
			compensationExact = true;
			if (!system.TryCaptureRegardLedger(out KingdomRegardLedgerSnapshot before))
				return false;
			string beforeCreed = system.DeclaredCreed;
			int beforeDissent = system.Dissent;
			int expectedDissent = slightedCount > 0
				? KingdomCreedRules.ApplyDissent(beforeDissent,
					KingdomCreedRules.DeclarationShock) : beforeDissent;
			bool exact = true;
			bool published = KingdomGovernanceScope.TryPublish("declare creed", delegate
			{
				if (!system.TryAdjustRegardForRealmBatch(standing, mirror: false)) return false;
				PublicationCut("declaration:standings");
				system.DeclaredCreed = creedFaction;
				PublicationCut("declaration:creed");
				if (slightedCount > 0) system.Dissent = expectedDissent;
				PublicationCut("declaration:dissent");
				return system.DeclaredCreed == creedFaction &&
					system.Dissent == expectedDissent;
			}, delegate
			{
				exact = false;
				bool ledger = false;
				try { ledger = system.TryRestoreRegardLedger(before); }
				catch (Exception ex) { KingdomLog.Log(
					"creed standing compensation threw (" + ex.Message + ")"); }
				try
				{
					system.DeclaredCreed = beforeCreed;
					system.Dissent = beforeDissent;
				}
				catch (Exception ex) { KingdomLog.Log(
					"creed civic compensation threw (" + ex.Message + ")"); }
				exact = ledger && system.DeclaredCreed == beforeCreed &&
					system.Dissent == beforeDissent;
				if (!exact)
				{
					try { system.QuarantineIdentity(
						"creed declaration compensation did not restore exact civic state"); }
					catch (Exception ex) { KingdomLog.Log(
						"creed declaration quarantine threw (" + ex.Message + ")"); }
				}
				return exact;
			});
			compensationExact = exact;
			return published;
		}
	}
}
