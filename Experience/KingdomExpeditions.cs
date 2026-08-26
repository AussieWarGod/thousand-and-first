using System;
using System.Collections.Generic;

using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Engine edge for the locked salvage commission. The durable realm job is the itinerary;
	/// the resident row is labour/standing; the resident binding names the one body; journal
	/// visitation is destination authority; dedicated stores pay physical costs; Chronicle and
	/// the existing ledger tell the one result. No proxy or second mission save object exists.
	/// </summary>
	public static partial class KingdomExpeditions
	{
		public const string ResidentJobProperty = "r_TAF_ExpeditionJob";
		public const string ProvisionJobProperty = "r_TAF_ExpeditionProvisionJob";
		public const string RewardJobProperty = "r_TAF_ExpeditionRewardJob";
		public const string DebitReceiptProperty = "r_TAF_ExpeditionDebitReceipt";
		public const string WaterJobProperty = "r_TAF_ExpeditionWaterJob";
		public const string WaterAfterProperty = "r_TAF_ExpeditionWaterAfter";

		private sealed class ResidentChoice
		{
			internal KingdomResidentRow Row;
			internal string ZoneId;
		}

		private sealed class TargetChoice
		{
			internal JournalMapNote Note;
			internal string ZoneId;
			internal string Name;
			internal KingdomExpeditionQuote Quote;
		}

		private enum BoundBodyState : byte
		{
			Unreachable = 0,
			Alive = 1,
			Led = 2,
			Dead = 3,
			Missing = 4,
			Ambiguous = 5
		}

		/// <summary>Charter route: inspect/recall open jobs or begin a new commission.</summary>
		public static void Open(KingdomSystem System, GameObject Actor)
		{
			if (System == null || !System.Founded || !GameObject.Validate(Actor)
				|| Actor.CurrentZone == null || System.Jobs == null)
			{
				Popup.Show("The Charter cannot find a founded city, its job book, and your present ground.");
				return;
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault))
			{
				Popup.Show("The realm's job book could not be read. No resident or goods were moved.");
				return;
			}
			List<KingdomJobRow> open = ExpeditionRows(table);
			if (open.Count == 0)
			{
				OpenDispatch(System, Actor, table);
				return;
			}
			string[] options = new string[open.Count + 1];
			char[] hotkeys = new char[open.Count + 1];
			options[0] = "Commission another salvage expedition";
			hotkeys[0] = 'n';
			long now = (The.Game == null) ? 0L : The.Game.TimeTicks;
			for (int i = 0; i < open.Count; i++)
			{
				KingdomJobRow row = open[i];
				bool terminal = KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode);
				options[i + 1] = (terminal
					? "Finish recording " : "Recall ") + ShownName(row.SubjectName,
					"resident " + row.SubjectId) + (terminal ? "'s result from " : " from ")
					+ ShownName(row.TargetName, row.DestZoneId) + " — "
					+ KingdomCharterMenuRules.DueWhen(row.DueTick, now, KingdomRules.TicksPerDay);
				hotkeys[i + 1] = (char)('a' + (i % 26));
			}
			int pick = Popup.PickOption(Title: "Salvage expeditions of " + KingdomPresentation.Rich(System.SeatName),
				Intro: "Each line is one named resident, one real body, and one dated realm job.",
				Options: options, Hotkeys: hotkeys, AllowEscape: true);
			if (pick < 0) return;
			if (pick == 0)
			{
				OpenDispatch(System, Actor, table);
				return;
			}
			KingdomJobRow chosen = open[pick - 1];
			if (KingdomExpeditionRules.IsResolutionPrepared(chosen.OriginCode))
			{
				if (!TryResumeTerminalResolution(System, chosen, out string terminalFailure))
				{
					Popup.Show(terminalFailure);
					return;
				}
				KingdomGovernanceScope.Commit("finish salvage expedition result");
				Popup.Show("The dated expedition result is in the Chronicle and homecoming ledger.");
				return;
			}
			if (Popup.ShowYesNo("Recall {{W|" + ShownName(chosen.SubjectName, "this resident")
				+ "}} from {{C|" + ShownName(chosen.TargetName, chosen.DestZoneId)
				+ "}}?\n\nWater and provisions were committed to the route at dispatch. Recall returns the exact "
				+ "resident body; it neither refunds nor spends those goods a second time.") != DialogResult.Yes) return;
			if (!TryResolve(System, chosen, KingdomExpeditionOutcome.Cancelled,
				(The.Game == null) ? 0L : The.Game.TimeTicks, Award: false, LoadZone: true,
				SourceSurvey: null, out string failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomGovernanceScope.Commit("recall salvage expedition");
			Popup.Show(ShownName(chosen.SubjectName, "The resident")
				+ " has returned. The dated recall is in the homecoming ledger.");
		}

	}
}
