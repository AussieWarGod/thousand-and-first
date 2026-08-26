using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of the research system: the registry of authored nodes, the
	/// founder's ledger of what they have heard of, the city's rolls of what its keepers have
	/// worked out, and the one call a lab makes to charge a stretch of thinking against the one
	/// subject the founder set.
	/// <para>
	/// <b>A node's whole effect is minting a roster key.</b> Completion calls
	/// <see cref="KingdomZoning.Learn"/> &mdash; the same call a data disk makes &mdash; so every
	/// design in the catalogue that gates on <c>Knowledge=</c>, ours and any third party's, is
	/// satisfied by research with no new gate machinery anywhere.
	/// </para>
	/// <para>
	/// <b>Two ledgers, sited apart on purpose</b> (Addendum 22 B1/B2/B3). Discovery is the
	/// FOUNDER's: a vanilla <c>JournalObservation</c> per node, revealed when they first hear of
	/// it, plus permanent <c>rite:</c> keys for water they shared, all of which survive secession,
	/// exile and refounding. Holding is the CITY's: a <c>node:</c> key in that city's own rolls,
	/// which leaves with the city when it
	/// secedes and comes home whole when it rejoins. So a founder put out of their realm walks away
	/// with every lead they ever had and not one finished thing &mdash; doors, never rooms.
	/// </para>
	/// <para>
	/// <b>The visibility law is enforced by the accessor, not by discipline.</b> Every surface that
	/// could enumerate nodes asks <see cref="Discovered"/>, and every surface that could enumerate
	/// designs passes through <see cref="KnowledgeGateHeardOf"/> on its way to
	/// <c>KingdomZoning.Visible</c>. A node a city can never reach is removed from the admissible
	/// set before any reveal, count, row or refusal is computed, so "cannot unlock" and "have not
	/// discovered" are the same absence of a row rather than two renderings.
	/// </para>
	/// </summary>
	public static partial class KingdomResearch
	{
		/// <summary>Whether the research system is switched on. Off, no node is ever discovered, no
		/// subject can be set, and the keepers' map draws exactly what it drew before nodes
		/// existed.</summary>
		public static bool Enabled => Options.GetOption("r_TAF_OptionResearch") != "No";

		/// <summary>The journal id a node's discovery bit lives under. Stable, published, and a
		/// string in the save rather than an ordinal, so a mod adding nodes never renumbers
		/// ours.</summary>
		public const string NotePrefix = "taf:node:";

		/// <summary>The journal category the founder's research leads file under.</summary>
		public const string NoteCategory = "general";

		private static List<ResearchNode> _nodes;

		private static readonly Dictionary<string, ResearchNode> ByKey = new Dictionary<string, ResearchNode>();

		private static bool NotesFiled;

		private static readonly Dictionary<string, bool> QuestCache = new Dictionary<string, bool>();

		/// <summary>The whole authored tree, in the order the files declared it. Ties anywhere in
		/// this system break on key ascending, so the same city on the same save always reads the
		/// same list in the same order.</summary>
		public static List<ResearchNode> Nodes
		{
			get
			{
				EnsureLoaded();
				return _nodes;
			}
		}

		/// <summary>One node by key, or false. Keys are folded, like every roster name.</summary>
		public static bool TryGetNode(string Key, out ResearchNode Node)
		{
			EnsureLoaded();
			Node = null;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			return ByKey.TryGetValue(Key.Trim().ToLowerInvariant(), out Node);
		}

		/// <summary>Forgets the registry and every cached answer about the world. Called by the
		/// registry loader and on a game load, so a reload never leaves a node, a quest verdict, or
		/// a filed journal note behind from another game.</summary>
		public static void Reload()
		{
			_nodes = null;
			ByKey.Clear();
			QuestCache.Clear();
			NotesFiled = false;
		}

		/// <summary>Forgets what this build cached about quest state. Called on
		/// <c>QuestFinishedEvent</c>, which is the only thing that can change any of it &mdash;
		/// there is no per-turn quest polling anywhere in this system.</summary>
		public static void ForgetQuests()
		{
			QuestCache.Clear();
		}

		// ==================================================================================
		// The registry
		// ==================================================================================

		private static void EnsureLoaded()
		{
			if (_nodes != null)
			{
				return;
			}
			_nodes = new List<ResearchNode>();
			ByKey.Clear();
			Dictionary<string, Action<XmlDataHelper>> handlers = null;
			handlers = new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"kingdomresearch",
					delegate(XmlDataHelper xml)
					{
						KingdomXmlSchema.HandleRoot(xml, handlers, "KingdomResearch");
					}
				},
				{ "node", HandleNode }
			};
			foreach (XmlDataHelper item in DataManager.YieldXMLStreamsWithRoot("KingdomResearch"))
			{
				item.HandleNodes(handlers);
			}
			foreach (string finding in KingdomResearchRules.Validate(_nodes))
			{
				KingdomLog.Log("KingdomResearch: " + finding);
			}
		}

		private static void HandleNode(XmlDataHelper xml)
		{
			// Every attribute is read unconditionally, for the reason the catalogue reads its own
			// that way: the engine records which attributes a pass asked for and warns about the
			// rest, so a pass that skips one on a fault makes the loader complain about the file.
			string key = xml.GetAttribute("Key");
			string displayName = xml.GetAttribute("DisplayName");
			string branch = xml.GetAttribute("Branch");
			string tier = xml.GetAttribute("Tier");
			string requires = xml.GetAttribute("Requires");
			string minTech = xml.GetAttribute("MinTech");
			string grants = xml.GetAttribute("Grants");
			string effort = xml.GetAttribute("Effort");
			string reveals = xml.GetAttribute("Reveals");
			string taughtBy = xml.GetAttribute("TaughtBy");
			string seededBy = xml.GetAttribute("SeededBy");
			string forbidden = xml.GetAttribute("Forbidden");
			string quest = xml.GetAttribute("Quest");
			string effect = xml.GetAttribute("Effect");
			ResearchNode node;
			string error;
			if (!KingdomResearchRules.TryParseNodeAttributes(key, displayName, branch, tier, requires, minTech, grants,
				effort, reveals, taughtBy, seededBy, forbidden, quest, effect, out node, out error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomResearch: " + error);
				xml.DoneWithElement();
				return;
			}
			for (int i = 0; i < _nodes.Count; i++)
			{
				if (_nodes[i].Key == node.Key)
				{
					// In place, so the tree keeps first-declaration order: a mod that re-prices a
					// node does not move it to the bottom of the founder's map.
					_nodes[i] = node;
					ByKey[node.Key] = node;
					xml.DoneWithElement();
					return;
				}
			}
			_nodes.Add(node);
			ByKey[node.Key] = node;
			xml.DoneWithElement();
		}

	}
}
