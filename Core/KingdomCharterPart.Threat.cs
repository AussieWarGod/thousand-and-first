using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		/// <summary>The four causal answers: exact tribute, obliged diplomacy, fight, or muster.</summary>
		public void AnswerThreat(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			if (!KingdomRaids.TryThreat(System, out KingdomRaidIncident incident))
			{
				if (!KingdomRaids.TryRecovery(System, out KingdomRaidIncident recovery))
				{
					Popup.Show("Nothing threatens the kingdom just now.");
					return;
				}
				if (recovery.RecoveryState == KingdomRaidRecoveryState.Offered)
				{
					int recoveryChoice = Popup.PickOption(
						Title: "The raid left the watch in disarray\n"
							+ "Effect: one bounded point of defensive service until recovered",
						Options: new string[2]
						{
							"Acknowledge recovery {{G|[prove the band gone; explicit seat turn-in]}}",
							"Decline recovery {{W|[one-point wound remains; never compounds or expires]}}"
						}, AllowEscape: true);
					if (recoveryChoice == 0
						&& !KingdomRaids.TryAcceptRecovery(System, out string acceptFailure))
						Popup.Show(acceptFailure);
					else if (recoveryChoice == 1
						&& !KingdomRaids.TryDeclineRecovery(System, out string declineFailure))
						Popup.Show(declineFailure);
					return;
				}
				if (recovery.RecoveryState == KingdomRaidRecoveryState.Ready)
				{
					if (Popup.ShowYesNo("The raiding band is proved gone. Set the watch in order here and turn in the recovery?")
						== DialogResult.Yes
						&& !KingdomRaids.TryResolveRecovery(System, zone, out string turnInFailure))
						Popup.Show(turnInFailure);
					return;
				}
				Popup.Show("Recovery is active. Prove the marked raiding band gone at "
					+ recovery.TargetZoneId + ", then return to the seat's charter.");
				return;
			}
			if (!KingdomRaids.CanAnswerAt(System, zone, out incident, out string channelFailure))
			{
				Popup.Show(channelFailure);
				return;
			}
			bool local = zone != null && zone.ZoneID == incident.TargetZoneId;
			int demand = incident.DisclosedStake;
			bool canTalk = System.GetRegardForRealm(incident.AttackerFactionId)
				>= KingdomRules.DiplomacyStandingRequired;
			Faction threatFaction = Factions.GetIfExists(incident.AttackerFactionId);
			string threatName = threatFaction == null ? incident.AttackerFactionId
				: threatFaction.GetFormattedName();
			int num = Popup.PickOption(Title: "Scouts of "
				+ threatName + " name a grievance\n"
				+ incident.CauseSnapshot + "\nTarget: " + incident.TargetZoneId
				+ "  " + (incident.State == KingdomRaidIncidentState.ConfrontationReady
					? "Confrontation ready; no automatic loss"
					: "Answer before: " + incident.DueTick)
				+ "  Raid stake: up to "
				+ incident.MaximumPlunder + " drams at the named stores", Options: new string[4]
			{
				local
					? "Pay exact tribute from dedicated stores {{C|[" + demand + " drams]}}"
					: "Pay exact tribute from loose, unsealed water you directly carry {{C|[" + demand + " drams]}}",
				canTalk ? "Send an envoy on our regard {{Y|[owes a doubled demand if provoked again]}}"
					: "{{K|Send an envoy [requires " + KingdomRules.DiplomacyStandingRequired + " standing]}}",
				"Refuse and meet the warband {{r|[physical fight at the named hour]}}",
				local
					? "Muster named defensive works {{G|[crews rechecked when they come]}}"
					: "Order the seat to muster {{G|[fresh exact works proved there; failure reopens every answer]}}"
			}, AllowEscape: true);
			switch (num)
			{
			case 0:
				if (!KingdomRaids.TryTribute(System, zone, out var payFail))
				{
					Popup.Show(payFail);
				}
				break;
			case 1:
				if (!KingdomRaids.TryTalkDown(System, zone, out var talkFail))
				{
					Popup.Show(talkFail);
				}
				break;
			case 2:
				if (!KingdomRaids.TryFight(System, zone, out var fightFail))
				{
					Popup.Show(fightFail);
				}
				break;
			case 3:
				if (!KingdomRaids.TryFortify(System, zone, out var fortifyFail))
				{
					Popup.Show(fortifyFail);
				}
				break;
			}
		}

	}
}
