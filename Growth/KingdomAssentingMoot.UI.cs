using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		public static void Open(GameObject Building, GameObject Founder)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string failure = null;
			if (Founder == null || !Founder.IsPlayer()
				|| !TryContext(system, Building, out KingdomAssentingMootContext context,
					out failure) || !context.Owned)
			{
				Popup.Show(failure ?? "The moot does not answer.");
				return;
			}
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("{{K|The realm master option pauses new moot work.}}\n\n"
					+ Status(context, context.Book.AssentingMoot));
				return;
			}
			if (!EnsureAuthority(system, Building, out context, out failure))
			{
				Popup.Show(failure ?? "The moot does not answer.");
				return;
			}
			while (true)
			{
				KingdomAssentingMootReceipt receipt = context.Book.AssentingMoot;
				if (receipt.Phase == KingdomAssentingMootPhase.Quarantined)
				{
					Popup.Show("The assenting-moot receipt is quarantined. Nothing was overwritten.\n\n"
						+ KingdomPresentation.Rich(receipt.Fault));
					return;
				}
				string[] options =
				{
					"ask a named resident for assent",
					"withdraw a named resident's assent",
					"grant a named resident an exemption",
					"revoke a named resident's exemption",
					"hear one recorded civic exchange",
					"leave the circuit"
				};
				int pick = Popup.PickOption(Title: "Assenting moot of "
					+ KingdomPresentation.Rich(context.SettlementName),
					Intro: Status(context, receipt), Options: options, AllowEscape: true);
				if (pick < 0 || pick == 5) return;
				if (pick == 4)
				{
					if (KingdomExperienceRuntime.TryRecallCivicVoice(system, Now(),
						out string callback)) Popup.Show(callback);
					else Popup.Show("No recorded witness is both present and still owed a callback.");
					continue;
				}
				KingdomAssentingMootRole role = pick < 2
					? KingdomAssentingMootRole.Assent : KingdomAssentingMootRole.Exemption;
				bool add = pick == 0 || pick == 2;
				if (!TryChoose(context.Book, receipt, role, add, out int residentId,
					out string residentName)) continue;
				string verb = Verb(role, add);
				string facts = KingdomAssentingMootRules.MembershipPreview(receipt,
					role, add, residentName);
				long tick = Now();
				string source = KingdomLifecycleRules.ChildId(receipt.AuthorityId,
					"civic-moot-" + receipt.Generation + "-" + residentId + "-"
						+ (add ? "add" : "remove") + "-" + (int)role, 0);
				KingdomExperienceRuntime.TryPrepareCivicVoice(system,
					KingdomCivicVoiceFixture.AssentingMoot,
					KingdomAssentingMootRules.CurrentReceiptVersion, source,
					context.SettlementId, facts, tick, out KingdomCivicVoiceReceipt voice,
					out string rendering);
				if (Popup.ShowYesNo(rendering) != DialogResult.Yes)
					continue;
				if (!TryChangeMember(context, role, add, residentId, out failure))
				{
					Popup.Show(failure ?? "The membership did not change.");
					continue;
				}
				KingdomGovernanceScope.Commit(verb.ToLowerInvariant());
				KingdomExperienceRuntime.TryPublishCivicVoice(system, voice);
				Popup.Show(verb + ".");
			}
		}

		private static string Status(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt)
		{
			StringBuilder text = new StringBuilder();
			text.Append("Six seats face one circuit. Named residents consent explicitly; exemptions "
				+ "are explicit and weaken the field.\n\nAssents: ");
			if (Receipt == null || Receipt.Phase == KingdomAssentingMootPhase.None)
			{
				text.Append("{{K|none}}\nExemptions: {{K|none}}"
					+ "\n\n{{K|No moot authority has been recorded.}}");
				return text.ToString();
			}
			AppendNames(text, Receipt.AssentResidentNames);
			text.Append("\nExemptions: ");
			AppendNames(text, Receipt.ExemptResidentNames);
			if (Receipt.Phase == KingdomAssentingMootPhase.Applied)
				text.Append("\n\n{{Y|Current native ward strength: ")
					.Append(Receipt.Strength).Append(".}}");
			else text.Append("\n\n{{K|Ward suspended: ")
				.Append(KingdomPresentation.Rich(Receipt.SuspendedReason)).Append("}}");
			return text.ToString();
		}

		private static void AppendNames(StringBuilder Text, List<string> Names)
		{
			if (Names == null || Names.Count == 0)
			{
				Text.Append("{{K|none}}");
				return;
			}
			for (int i = 0; i < Names.Count; i++)
			{
				if (i > 0) Text.Append(", ");
				Text.Append(KingdomPresentation.Rich(Names[i]));
			}
		}

		private static string Verb(KingdomAssentingMootRole Role, bool Add)
		{
			if (Role == KingdomAssentingMootRole.Assent)
				return Add ? "Record this assent" : "Withdraw this assent";
			return Add ? "Grant this exemption" : "Revoke this exemption";
		}
	}
}
