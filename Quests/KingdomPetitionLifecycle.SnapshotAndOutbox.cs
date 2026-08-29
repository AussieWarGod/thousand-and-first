using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomPetitionLifecycle
	{
		private static bool TryRequester(KingdomSystem system, KingdomSurvey survey,
			string exactName, out GameObject body, out string name)
		{
			body = null;
			name = null;
			if (system == null || survey?.Settlers == null) return false;
			for (int i = 0; i < survey.Settlers.Count; i++)
			{
				GameObject candidate = survey.Settlers[i];
				if (!GameObject.Validate(candidate) || candidate.CurrentZone != survey.Ground
					|| candidate.GetIntProperty("KingdomCitizen") != 1
					|| candidate.IsPlayer() || candidate.IsPlayerLed()
					|| string.IsNullOrEmpty(candidate.IDIfAssigned) || string.IsNullOrEmpty(candidate.Blueprint))
					continue;
				string semantic = candidate.GetStringProperty("KingdomName");
				if (string.IsNullOrEmpty(semantic)) semantic = candidate.BaseDisplayNameStripped;
				if (!KingdomPetitionRules.SnapshotTextValid(candidate.IDIfAssigned,
						KingdomLifecycleRules.MaxIdChars, false)
					|| !KingdomPetitionRules.SnapshotTextValid(candidate.Blueprint,
						KingdomLifecycleRules.MaxNameChars, false)
					|| !KingdomPetitionRules.SnapshotTextValid(semantic,
						KingdomLifecycleRules.MaxNameChars, false)
					|| (exactName != null && !string.Equals(semantic, exactName,
						StringComparison.Ordinal))) continue;
				if (body != null && exactName != null) return false;
				if (body == null || string.CompareOrdinal(candidate.IDIfAssigned, body.IDIfAssigned) < 0)
				{
					body = candidate;
					name = semantic;
				}
			}
			return body != null;
		}

		private static void FreezeOffer(KingdomLifecycleOperation op, GameObject body,
			string name, string settlementId, string zoneId, KingdomRules.PetitionKind kind,
			string faction, int target, string eventId, long issuedTick, long deadline)
		{
			op.ZoneId = zoneId;
			op.ObjectId = body.IDIfAssigned;
			op.Blueprint = body.Blueprint;
			op.ObjectName = name;
			op.Origin = settlementId;
			op.Faction = faction;
			op.DisplayFaction = DisplayFaction(faction);
			op.Detail = string.IsNullOrEmpty(op.DisplayFaction)
				? KingdomPetitions.Subject(kind) : op.DisplayFaction;
			op.Kind = (int)kind;
			op.Target = target;
			op.ObjectMarker = string.IsNullOrEmpty(eventId)
				? KingdomLifecycleRules.ChildId(op.Id, "petition-event", 0) : eventId;
			op.ArrivalText = issuedTick.ToString(CultureInfo.InvariantCulture);
			op.DepartTick = deadline;
			op.Creed = KingdomPetitionRules.ActiveClock;
		}

		private static void CopySnapshot(KingdomLifecycleOperation source,
			KingdomLifecycleOperation target)
		{
			target.ZoneId = source.ZoneId;
			target.ObjectId = source.ObjectId;
			target.Blueprint = source.Blueprint;
			target.ObjectName = source.ObjectName;
			target.Origin = source.Origin;
			target.Faction = source.Faction;
			target.DisplayFaction = source.DisplayFaction;
			target.Detail = source.Detail;
			target.Kind = source.Kind;
			target.Target = source.Target;
			target.ObjectMarker = source.ObjectMarker;
			target.ArrivalText = source.ArrivalText;
		}

		private static KingdomLifecycleOutbox Outbox(KingdomSystem system,
			KingdomLifecycleOperation op, string reason)
		{
			string petitioner = KingdomPresentation.Rich(op.ObjectName);
			string subject = KingdomPetitions.Subject((KingdomRules.PetitionKind)op.Kind);
			string chronicle;
			string ledger;
			string message;
			string deed = null;
			switch (reason)
			{
			case "accepted":
				chronicle = "the founder accepted " + petitioner + "'s petition about " + subject;
				ledger = "{{W|The founder accepted " + petitioner + "'s petition.}}";
				message = "{{G|Your word to " + petitioner + " stands.}}";
				break;
			case "declined":
				chronicle = petitioner + " was told the matter must wait";
				ledger = "{{K|" + petitioner + " returned to work. The matter was not pressed.}}";
				message = "{{K|The petition was declined without penalty.}}";
				break;
			case "resolved":
				deed = KingdomPetitions.Deed((KingdomRules.PetitionKind)op.Kind,
					KingdomPresentation.Rich(system.KingdomDisplayName));
				chronicle = petitioner + " asked, and " + deed;
				ledger = "{{G|" + petitioner + " has what they asked for. Word of it will travel.}}";
				message = "{{G|" + petitioner + " thanks you. "
					+ XRL.Language.Grammar.InitCap(deed) + ".}}";
				break;
			case "paused":
				chronicle = petitioner + "'s accepted petition was held while petitions were disabled";
				ledger = "{{K|The accepted petition is paused; your word is not erased.}}";
				message = "{{K|Petitions are disabled. The accepted promise is paused.}}";
				break;
			case "resumed":
				chronicle = petitioner + "'s accepted petition resumed from its saved time";
				ledger = "{{W|The accepted petition resumes with its remaining time.}}";
				message = "{{W|The accepted promise is active again.}}";
				break;
			case "option-closed":
				chronicle = petitioner + " stopped asking when petitions were disabled";
				ledger = "{{K|The unanswered petition closed without penalty.}}";
				message = "{{K|Petitions are disabled. The unanswered request is closed.}}";
				break;
			case "legacy-accepted":
				chronicle = petitioner + "'s older accepted petition was adopted by the petition book";
				ledger = "{{W|An accepted petition from an older save remains in force.}}";
				message = "{{W|An older accepted petition remains in force.}}";
				break;
			case "legacy-offered":
				chronicle = petitioner + "'s older unanswered petition was adopted by the petition book";
				ledger = "{{W|An unanswered petition from an older save still waits.}}";
				message = "{{W|An older petition still waits at the Charter.}}";
				break;
			case "expired":
				chronicle = petitioner + " stopped asking; the matter was not pressed";
				ledger = "{{K|" + petitioner + " stopped asking. The matter was not pressed.}}";
				message = "{{K|A petition expired without penalty.}}";
				break;
			default:
				chronicle = petitioner + " brought a petition about " + subject;
				ledger = "{{W|" + petitioner + " is waiting to speak with you.}}";
				message = "{{W|" + petitioner + " would have a word with you about " + subject + ".}}";
				break;
			}
			KingdomLifecycleOutbox box = KingdomLifecycleRules.PrepareOutbox(op, chronicle,
				ledger, message, deed, null);
			if (box != null && reason == "resolved") box.ChronicleAccomplishment = true;
			return box;
		}

	}
}
