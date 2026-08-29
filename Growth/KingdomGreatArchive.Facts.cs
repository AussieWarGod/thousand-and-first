using System;
using System.Collections.Generic;
using Qud.API;

namespace ThousandAndFirst
{
	public static partial class KingdomGreatArchive
	{
		private static bool TryFacts(KingdomSystem System,
			out List<KingdomGreatArchiveCityFacts> Cities,
			out List<KingdomGreatArchiveNodeFacts> Nodes, out string Failure)
		{
			Cities = new List<KingdomGreatArchiveCityFacts>();
			Nodes = new List<KingdomGreatArchiveNodeFacts>(); Failure = null;
			if (System?.City == null || string.IsNullOrEmpty(System.City.SettlementId))
				return Fail("The capital's keeper-roll identity is incomplete.", out Failure);
			AddCity(Cities, System.City.SettlementId, System.SeatName,
				KingdomZoning.Roster(System), KingdomResearch.Nodes);
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; i < others.Count; i++)
			{
				KingdomSettlement city = others[i];
				if (city?.City == null) return Fail(
					"A non-seat keeper roll is incomplete.", out Failure);
				AddCity(Cities, city.City.SettlementId,
					string.IsNullOrEmpty(city.SettlementName) ? city.City.SettlementId
						: city.SettlementName,
					KingdomZoning.RosterOf(city), KingdomResearch.Nodes);
			}
			Dictionary<string, List<ResearchNode>> grants = GrantSources(KingdomResearch.Nodes);
			for (int i = 0; i < KingdomResearch.Nodes.Count; i++)
			{
				ResearchNode node = KingdomResearch.Nodes[i];
				KingdomGreatArchiveNodeFacts facts = new KingdomGreatArchiveNodeFacts {
					Key = node.Key, DisplayName = node.Named, Branch = node.Branch,
					Tier = node.Tier,
					Discovered = KingdomResearch.Enabled
						&& JournalAPI.HasNote(KingdomResearch.NoteId(node.Key))
						&& KingdomResearch.Admissible(System, node)
				};
				if (!TryRequirements(node.Requires, grants, facts.Requirements, out Failure))
					return false;
				Nodes.Add(facts);
			}
			return true;
		}

		private static void AddCity(List<KingdomGreatArchiveCityFacts> Cities,
			string Id, string Name, List<string> Roster, List<ResearchNode> Nodes)
		{
			KingdomGreatArchiveCityFacts city = new KingdomGreatArchiveCityFacts {
				SettlementId = Id, DisplayName = Name
			};
			for (int i = 0; i < Nodes.Count; i++)
				if (Holds(Roster, Nodes[i])) city.HeldNodeKeys.Add(Nodes[i].Key);
			Cities.Add(city);
		}

		private static bool Holds(List<string> Roster, ResearchNode Node)
		{
			List<string> grants = KingdomZoningRules.Tokens(Node?.Grants);
			if (Node == null || grants.Count == 0) return false;
			for (int i = 0; i < grants.Count; i++)
				if (!KingdomZoningRules.Knows(Roster, grants[i])) return false;
			return true;
		}

		private static Dictionary<string, List<ResearchNode>> GrantSources(
			List<ResearchNode> Nodes)
		{
			Dictionary<string, List<ResearchNode>> result =
				new Dictionary<string, List<ResearchNode>>(StringComparer.Ordinal);
			for (int i = 0; i < Nodes.Count; i++)
				foreach (string grant in KingdomZoningRules.Tokens(Nodes[i].Grants))
				{
					if (KingdomZoningRules.KindOf(grant) != KingdomZoningRules.KindNode)
						continue;
					if (!result.TryGetValue(grant, out List<ResearchNode> sources))
					{
						sources = new List<ResearchNode>(); result.Add(grant, sources);
					}
					if (!sources.Contains(Nodes[i])) sources.Add(Nodes[i]);
				}
			return result;
		}
	}
}
