using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private static void Scout(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			if (Data.ScoutPhase == 0)
			{
				List<string> frontier = Frontier(System);
				if (frontier.Count == 0)
				{
					Announce(System, Data, BountyBlock.NoFrontier);
					return;
				}
				string settlementId = KingdomChronicle.SettlementId(System);
				if (!KingdomIdentityRules.IsSettlementId(settlementId)) return;
				int index;
				if (!KingdomBountyRules.TryPickFrontier(settlementId,
					Data.PostedTick, Data.Passes, frontier.Count, out index)) return;
				if (index < 0 || index >= frontier.Count) index = 0;
				Data.ScoutZoneId = frontier[index];
				Data.ScoutPhase = 1;
			}
			if (Data.ScoutPhase == 1 && string.IsNullOrEmpty(Data.ScoutGround))
			{
				string ground = null;
				KingdomSystem.Guard("bounty: name bound frontier", delegate
				{
					ground = The.ZoneManager.GetZoneDisplayName(Data.ScoutZoneId,
						WithIndefiniteArticle: true);
				});
				Data.ScoutGround = ground ?? "";
			}
			if (Data.ScoutPhase == 1)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "scout"),
					KingdomBountyRules.ScoutChronicle(
						KingdomPresentation.Rich(Data.WorkerName),
						KingdomPresentation.Rich(System.SeatName),
						KingdomPresentation.Rich(Data.ScoutGround)))) return;
				Data.ScoutPhase = 2;
			}
			if (Data.ScoutPhase == 2)
			{
				if (Data.ScoutDeedState == (int)BountySinkDisposition.None)
					Data.ScoutDeedState = (int)BountySinkDisposition.Pending;
				Data.ScoutPhase = 3;
				Data.ScoutDeedState = (int)BountySinkDisposition.Attempting;
				System.RecordDeed(KingdomBountyRules.ScoutDeed(
					KingdomPresentation.Rich(System.SeatName)));
				Data.ScoutDeedState = (int)BountySinkDisposition.Delivered;
				Data.ScoutPhase = 4;
			}
			else if (Data.ScoutPhase == 3)
			{
				if (Data.ScoutDeedState == (int)BountySinkDisposition.None)
					Data.ScoutDeedState = (int)BountySinkDisposition.Attempting;
				Data.ScoutDeedState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.ScoutDeedState);
				Data.ScoutPhase = 4;
			}
			if (Data.ScoutPhase != 4 && Data.ScoutPhase != 5) return;
			Data.ScoutPhase = 5;
			Announce(System, Data, BountyBlock.None);
			Finish(System, Z, Survey, Notice, Data, string.IsNullOrEmpty(Data.ScoutGround)
				? "the frontier was walked"
				: ("the frontier was walked, and " + Data.ScoutGround + " lies past it"));
		}

		private static void ManOneWork(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				if (work.GetIntProperty("KingdomEffectiveness") > 0)
				{
					continue;
				}
				work.SetIntProperty("KingdomStaffed", 1);
				work.SetIntProperty("KingdomEffectiveness", 100);
				// The hired hand is not a witnessed resident identity. Neutral is the only
				// honest factor; retaining yesterday's crew would lend their culture to a stranger.
				work.SetIntProperty(KingdomCrews.IdentityAffinityProperty,
					KingdomIdentityAffinityRules.NeutralPercent);
				if (System.IdleWorks > 0)
				{
					System.IdleWorks--;
				}
				if (System.IdleWorks == 0)
				{
					System.IdleWorksAnnounced = false;
				}
				return;
			}
		}

		/// <summary>Marks the work finished and tries to pay for it in the same breath.</summary>
		private static void Finish(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data, string Extra)
		{
			if (Data.CompletionPhase == 0)
			{
				Data.CompletionExtra = Extra;
				Data.CompletionPhase = 1;
				Data.Done = true;
			}
			ContinueFinish(System, Z, Survey, Notice, Data);
		}

		private static void ContinueFinish(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Notice, r_KingdomNotice Data)
		{
			if (Data.CompletionPhase == 1)
			{
				ClearFetchMark(Z, Notice, Data);
				if (string.IsNullOrEmpty(Data.CompletionExtra))
				{
					Data.CompletionLedgerState = (int)BountySinkDisposition.Skipped;
					Data.CompletionPhase = 3;
				}
				else
				{
					if (Data.CompletionLedgerState == (int)BountySinkDisposition.None)
						Data.CompletionLedgerState = (int)BountySinkDisposition.Pending;
					Data.CompletionPhase = 2;
					DeliverLedger(System, ref Data.CompletionLedgerState,
						"{{G|" + Data.CompletionExtra.Capitalize() + ".}}");
					Data.CompletionPhase = 3;
				}
			}
			else if (Data.CompletionPhase == 2)
			{
				if (Data.CompletionLedgerState == (int)BountySinkDisposition.None)
					Data.CompletionLedgerState = (int)BountySinkDisposition.Attempting;
				Data.CompletionLedgerState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.CompletionLedgerState);
				Data.CompletionPhase = 3;
			}
			if (Data.CompletionPhase == 3)
			{
				Data.CompletionPhase = 4;
			}
			if (Data.CompletionPhase == 4) Settle(System, Z, Survey, Notice, Data);
		}

		private static void Settle(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			if ((BountyPaymentPhase)Data.PaymentPhase == BountyPaymentPhase.Quarantined)
			{
				TellQuarantine(System, Data);
				return;
			}
			int owed = Data.Price - Data.Paid;
			if (owed > 0)
			{
				if (!ContinuePayment(System, Z, Survey, Notice, Data, owed))
				{
					if (Data.LifecycleQuarantined)
					{
						TellQuarantine(System, Data);
						return;
					}
					if (Data.AnnouncedBlock != (int)BountyBlock.StoresCannotPay)
					{
						KingdomChronicle.RecordOnce(System, EventId(Data, "owed"),
							KingdomBountyRules.OwedChronicle(
								KingdomPresentation.Rich(Data.WorkerName),
								KingdomPresentation.Rich(System.SeatName),
								Data.Paid, Data.Price - Data.Paid));
					}
					Announce(System, Data, BountyBlock.StoresCannotPay);
					Describe(System, Z, Notice, Data);
					return;
				}
				owed = Data.Price - Data.Paid;
			}
			if (owed > 0) return;
			ContinueTerminal(System, Notice, Data);
		}

	}
}
