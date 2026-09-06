using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDeathRuntime
	{
		private static XRLGame NoticeGame;
		private static readonly HashSet<string> Notices = new HashSet<string>(StringComparer.Ordinal);
		private static bool Tell(Frame f, int index, KingdomResidentDeathReceipt receipt, Zone zone)
		{
			var r = receipt.Copy();
			if (r.Phase != KingdomResidentDeathPhase.Accounted || !DeadExact(f, r)) return false;
			if (r.Telling == KingdomResidentDeathTelling.Attempting)
			{
				// No return value survives this cut. Do not re-enter a possibly delivered callback.
				r.Telling = KingdomResidentDeathTelling.Uncertain;
			}
			else if (r.Telling == KingdomResidentDeathTelling.Pending)
			{
				r.Telling = KingdomResidentDeathTelling.Attempting;
				if (!Save(f, index, r) || !DeadExact(f, r)) return false;
				bool owned = false;
				try
				{
					var cause = (KingdomOfficeRules.DeathCause)((int)r.Cause - (int)KingdomStandingCause.Unwitnessed);
					if (ReferenceEquals(f.City, f.System.City))
						owned = KingdomHappenings.OwnDeathTelling(f.System, r.Before.Name, r.Before.Origin, cause, zone, r.Tick);
					if (!DeadExact(f, r)) return false;
					if (!owned)
					{
						string text = KingdomOfficeRules.MourningChronicle(KingdomPresentation.Rich(r.Before.Name),
							KingdomPresentation.Rich(r.Before.Origin), KingdomPresentation.Rich(r.SettlementName), cause);
						string id = "taf:witnessed-death:v1:" + r.Realm + ":" + r.Settlement + ":" + N(r.Before.ResidentId);
						owned = KingdomChronicle.RecordOnceAt(f.System, id, text, r.Tick, () => DeadExact(f, r));
						if (!DeadExact(f, r)) return false;
						MessageQueue.AddPlayerMessage(KingdomVoices.Say(f.System, VoiceOccasion.CitizenLost,
							"{{r|" + KingdomOfficeRules.MourningMessage(KingdomPresentation.Rich(r.Before.Name), cause) + "}}"));
						if (!DeadExact(f, r)) return false;
					}
				}
				catch { owned = false; }
				if (!DeadExact(f, r)) return false;
				r.Telling = owned ? KingdomResidentDeathTelling.Owned : KingdomResidentDeathTelling.Uncertain;
			}
			r.Phase = KingdomResidentDeathPhase.Settled;
			// The terminal stage and framed before/after digest retain replay evidence without quadratic history copies.
			r.BeforeAccounts = r.AfterAccounts = new string[0];
			if (!Save(f, index, r)) return false;
			WarnOutcome(f.System, r); return Exact(f);
		}
		private static void WarnOutcome(KingdomSystem system, KingdomResidentDeathReceipt r)
		{
			if (r.RemembranceUnavailable)
				Notice(system, r, "The death is recorded. Remembrance authority was unavailable at the witness; no later opportunity will be invented.");
			if (r.Telling == KingdomResidentDeathTelling.Uncertain)
				Notice(system, r, "The death is recorded, but its telling is unconfirmed and will not be replayed automatically.");
			if (r.RoofBlocked)
				Notice(system, r, "The death is recorded. A frozen roof obligation still names this resident; the missing body is not roof proof.");
		}
		private static void Notice(KingdomSystem system, KingdomResidentDeathReceipt r, string text)
		{
			try
			{
				if (!ReferenceEquals(NoticeGame, The.Game)) { NoticeGame = The.Game; Notices.Clear(); }
				string key = (r?.Body ?? "unbound") + ":" + text;
				if (Notices.Count >= KingdomResidentDeathRules.MaxEntries || !Notices.Add(key)) return;
				KingdomLog.Log("witnessed death: " + text);
				MessageQueue.AddPlayerMessage("{{R|" + text + "}}");
			}
			catch { }
		}
	}
}
