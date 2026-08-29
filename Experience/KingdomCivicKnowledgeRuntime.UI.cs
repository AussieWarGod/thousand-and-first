#if !TAF_TESTS
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	internal enum KingdomCivicKnowledgeMenuKind : byte
	{
		Curiosity = 1,
		Lead = 2
	}

	internal readonly struct KingdomCivicKnowledgeMenuRow
	{
		internal readonly KingdomCivicKnowledgeMenuKind Kind;
		internal readonly int Index;
		internal KingdomCivicKnowledgeMenuRow(KingdomCivicKnowledgeMenuKind kind, int index)
		{ Kind = kind; Index = index; }
	}

	internal static partial class KingdomCivicKnowledgeRuntime
	{
		/// <summary>Charter opener: read while paused; mutate only after one explicit choice.</summary>
		public static void OpenCurrent(KingdomSystem system, GameObject actor)
		{
			if (system == null || !GameObject.Validate(actor) || !actor.IsPlayer()
				|| actor.CurrentZone == null)
			{
				Popup.Show("Civic knowledge can only be read from the founder's loaded ground.");
				return;
			}
			string settlementId = system.SettlementIdForOwnedZone(actor.CurrentZone.ZoneID);
			KingdomCivicMemorySystem memory = null;
			string failure = null;
			if (settlementId == null || !TryUniqueMemory(out memory, out failure))
			{
				Popup.Show(failure ?? "This ground has no exact current settlement authority.");
				return;
			}
			bool paused = !KingdomMaster.NewWorkAllowed(system);
			if (!paused) ReconcileCurrentBestEffort(system, actor.CurrentZone);
			if (!KingdomCuriosityLeadTransactions.TryRead(memory, out long revision,
				out KingdomCuriosityBook curiosity, out KingdomCivicLeadBook leads, out failure))
			{
				Popup.Show("Civic knowledge is unavailable. Nothing changed.\n\n"
					+ KingdomPresentation.Rich(failure)); return;
			}
			if (curiosity.State != KingdomCuriosityBookState.Compatible
				|| leads.State != KingdomCuriosityBookState.Compatible)
			{
				Popup.Show("Civic knowledge is preserved but read-only.\n\nCuriosity: "
					+ curiosity.State + "\nLeads: " + leads.State); return;
			}
			List<string> options = new List<string>();
			List<KingdomCivicKnowledgeMenuRow> rows = new List<KingdomCivicKnowledgeMenuRow>();
			for (int i = 0; i < curiosity.Rows.Count; i++)
				if (curiosity.Rows[i].SettlementId == settlementId)
				{
					options.Add("Curator: " + curiosity.Rows[i].NoteText + " ["
						+ CuriosityStatus(curiosity.Rows[i]) + "]");
					rows.Add(new KingdomCivicKnowledgeMenuRow(
						KingdomCivicKnowledgeMenuKind.Curiosity, i));
				}
			for (int i = 0; i < leads.Rows.Count; i++)
				if (leads.Rows[i].SettlementId == settlementId)
				{
					options.Add("Lead: " + leads.Rows[i].Title + " ["
						+ LeadStatus(leads.Rows[i]) + "]");
					rows.Add(new KingdomCivicKnowledgeMenuRow(
						KingdomCivicKnowledgeMenuKind.Lead, i));
				}
			if (rows.Count == 0)
			{
				Popup.Show("No finite civic curation or city-authored lead is recorded for this "
					+ "settlement.\n\nNothing was inferred or generated."); return;
			}
			options.Add("{{K|Back to people & belief}}");
			int pick = Popup.PickOption(Title: "Civic knowledge",
				Intro: "Curations only point to exact knowledge already in the Journal. Leads "
					+ "are separately authored from completed physical works."
					+ (paused ? "\n\n{{K|Settlement simulation is paused: read only.}}" : ""),
				Options: options.ToArray(), AllowEscape: true);
			if (pick < 0 || pick >= rows.Count) return;
			KingdomCivicKnowledgeMenuRow selected = rows[pick];
			if (selected.Kind == KingdomCivicKnowledgeMenuKind.Curiosity)
				OpenCuriosity(system, memory, curiosity.Rows[selected.Index].Copy(), paused);
			else OpenLead(system, memory, revision, leads,
				leads.Rows[selected.Index].Copy(), paused);
		}

		internal static string CuriosityStatus(KingdomCuriosityReceipt row)
		{
			return row == null ? "unreadable" : row.State.ToString().ToLowerInvariant();
		}

		internal static string LeadStatus(KingdomCivicLeadReceipt row)
		{
			return row == null ? "unreadable" : row.Phase.ToString().ToLowerInvariant();
		}
	}
}
#endif
