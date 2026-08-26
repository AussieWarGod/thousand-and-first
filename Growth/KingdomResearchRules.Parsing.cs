using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomResearchRules
	{
		// --- Parsing (STANDARDS 6) -------------------------------------------------------------

		/// <summary>
		/// Reads one <c>&lt;node&gt;</c> element into a record.
		/// <para>
		/// A node is REFUSED whole on a fault, unlike a building gate, and the asymmetry is
		/// deliberate: a gate is a restriction on a design that exists either way, and a node
		/// whose tier or effort cannot be read is a thing the founder could work on forever. The
		/// one fault that is a rule rather than a typo is a <c>rite:</c> token in
		/// <see cref="ResearchNode.TaughtBy"/> &mdash; Addendum 18's seed-not-ceiling clause,
		/// refused by file and key rather than left as a convention somebody can forget (RR6).
		/// </para>
		/// </summary>
		/// <param name="Error">Null on success; one sentence naming the key and the fault otherwise.</param>
		/// <returns>True when <paramref name="Node"/> is a node this build can use.</returns>
		public static bool TryParseNodeAttributes(string Key, string DisplayName, string Branch, string Tier,
			string Requires, string MinTech, string Grants, string Effort, string Reveals,
			string TaughtBy, string SeededBy, string Forbidden, string Quest, string Effect,
			out ResearchNode Node, out string Error)
		{
			Node = null;
			string key = Fold(Key);
			if (key == null)
			{
				Error = "a <node> element carries no Key.";
				return false;
			}
			if (key.IndexOf(KingdomZoningRules.KindSeparator) >= 0 || key.IndexOf(KingdomZoningRules.RosterSeparator) >= 0)
			{
				Error = "node " + key + ": a Key may not carry '" + KingdomZoningRules.KindSeparator + "' or '" + KingdomZoningRules.RosterSeparator + "'.";
				return false;
			}
			int tier = 1;
			if (!string.IsNullOrEmpty(Tier) && (!int.TryParse(Tier.Trim(), out tier) || tier < 1 || tier > TierCount))
			{
				Error = "node " + key + ": Tier \"" + Tier + "\" is not one of 1 to " + TierCount + ".";
				return false;
			}
			int effort = 1;
			if (!string.IsNullOrEmpty(Effort) && (!int.TryParse(Effort.Trim(), out effort) || effort < 1))
			{
				Error = "node " + key + ": Effort \"" + Effort + "\" is not a count of staff-days.";
				return false;
			}
			TechLevel minTech = TechLevel.Hands;
			if (!string.IsNullOrEmpty(MinTech) && MinTech.Trim().Length > 0
				&& (!System.Enum.TryParse<TechLevel>(MinTech.Trim(), ignoreCase: true, out minTech) || !KingdomZoningRules.IsKnownTechLevel(minTech)))
			{
				Error = "node " + key + ": MinTech \"" + MinTech + "\" names no craft this build knows.";
				return false;
			}
			string taught = Trimmed(TaughtBy);
			foreach (string token in KingdomZoningRules.Tokens(taught))
			{
				if (KingdomZoningRules.KindOf(token) == KindRite)
				{
					Error = "node " + key + ": TaughtBy names \"" + token
						+ "\". A rite SEEDS a branch and never finishes a node (Addendum 18); move it to SeededBy.";
					return false;
				}
			}
			string grants = Trimmed(Grants);
			if (grants == null)
			{
				grants = KingdomZoningRules.ComposeKey(KindNode, key);
			}
			List<ResearchEffect> effects;
			string effectError;
			if (!TryParseEffects(Effect, out effects, out effectError))
			{
				Error = "node " + key + ": Effect " + effectError;
				return false;
			}
			Node = new ResearchNode
			{
				Key = key,
				DisplayName = Trimmed(DisplayName),
				Branch = Fold(Branch),
				Tier = tier,
				Requires = Trimmed(Requires),
				MinTech = minTech,
				Grants = grants,
				Effort = effort,
				Reveals = Trimmed(Reveals),
				TaughtBy = taught,
				SeededBy = Trimmed(SeededBy),
				Forbidden = Trimmed(Forbidden),
				Quest = Trimmed(Quest),
				Effect = Trimmed(Effect),
				Effects = effects
			};
			Error = null;
			return true;
		}

		/// <summary>
		/// Reads an <c>Effect</c> attribute: <c>efficiency:5</c>, <c>statcap:Intelligence:1</c>,
		/// <c>recruitreveal:1</c>, comma separated. A kind this build does not know is carried with
		/// its amount rather than refused, so a later wave or another mod's lane can read it
		/// (STANDARDS 9); what IS refused is an amount that is not a number, because that is a typo
		/// wearing a rule's clothes.
		/// </summary>
		public static bool TryParseEffects(string Source, out List<ResearchEffect> Effects, out string Error)
		{
			Effects = new List<ResearchEffect>();
			Error = null;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			foreach (string token in KingdomZoningRules.Tokens(Source))
			{
				string[] parts = token.Split(KingdomZoningRules.KindSeparator);
				if (parts.Length < 2 || parts.Length > 3)
				{
					Error = "\"" + token + "\" is not kind:amount or kind:stat:amount.";
					Effects = new List<ResearchEffect>();
					return false;
				}
				string kind = Fold(parts[0]);
				string stat = (parts.Length == 3) ? Fold(parts[1]) : null;
				int amount;
				if (kind == null || !int.TryParse(parts[parts.Length - 1].Trim(), out amount))
				{
					Error = "\"" + token + "\" carries no readable amount.";
					Effects = new List<ResearchEffect>();
					return false;
				}
				if (kind == EffectStatCap && stat == null)
				{
					Error = "\"" + token + "\" is a stat cap that names no stat.";
					Effects = new List<ResearchEffect>();
					return false;
				}
				Effects.Add(new ResearchEffect(kind, stat, amount));
			}
			return true;
		}

		/// <summary>
		/// What is wrong with a merged registry, said once at load. Nothing is unregistered: a node
		/// that is wrong about itself stays in the tree and becomes visible, which is the only shape
		/// a check on third-party content can honestly take. The checks are the ones no single node
		/// can see &mdash; a reveal into a key nothing declares, a requirement on a node that does
		/// not exist, a tier below the tier it hangs from.
		/// </summary>
		/// <returns>One sentence per finding, in registry order; never null.</returns>
		public static List<string> Validate(IList<ResearchNode> Nodes)
		{
			List<string> findings = new List<string>();
			if (Nodes == null)
			{
				return findings;
			}
			List<string> keys = new List<string>();
			List<string> granted = new List<string>();
			for (int i = 0; i < Nodes.Count; i++)
			{
				if (Nodes[i] == null || Nodes[i].Key == null)
				{
					continue;
				}
				keys.Add(Nodes[i].Key);
				foreach (string token in KingdomZoningRules.Tokens(Nodes[i].Grants))
				{
					string name = KingdomZoningRules.NameOf(token);
					if (name != null && !granted.Contains(name))
					{
						granted.Add(name);
					}
				}
			}
			for (int i = 0; i < Nodes.Count; i++)
			{
				ResearchNode node = Nodes[i];
				if (node == null || node.Key == null)
				{
					continue;
				}
				foreach (string token in KingdomZoningRules.Tokens(node.Reveals))
				{
					string name = KingdomZoningRules.NameOf(token);
					if (name != null && !keys.Contains(name))
					{
						findings.Add("node " + node.Key + " reveals \"" + token + "\", which no node declares.");
					}
				}
				foreach (string token in KingdomZoningRules.Tokens(node.Requires))
				{
					if (KingdomZoningRules.KindOf(token) != KindNode)
					{
						continue;
					}
					foreach (string arm in token.Split(KingdomZoningRules.RosterSeparator))
					{
						string name = KingdomZoningRules.NameOf(arm);
						if (name != null && !granted.Contains(name))
						{
							findings.Add("node " + node.Key + " requires \"" + arm + "\", which no node grants.");
						}
					}
				}
			}
			return findings;
		}

	}
}
