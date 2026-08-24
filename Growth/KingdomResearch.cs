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
	public static class KingdomResearch
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
						xml.HandleNodes(handlers);
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

		// ==================================================================================
		// Discovery — the founder's ledger, in vanilla's own book
		// ==================================================================================

		/// <summary>The journal id one node's discovery bit lives under.</summary>
		public static string NoteId(string Key)
		{
			return string.IsNullOrEmpty(Key) ? null : (NotePrefix + Key.Trim().ToLowerInvariant());
		}

		/// <summary>
		/// Files one unrevealed journal note per node, once per game. Vanilla refuses an id it
		/// already holds, so this is idempotent whatever calls it; the flag only keeps it from
		/// walking the registry on every read.
		/// </summary>
		public static void FileNotes()
		{
			if (NotesFiled || !Enabled)
			{
				return;
			}
			EnsureLoaded();
			NotesFiled = true;
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				string id = NoteId(node.Key);
				if (id == null || JournalAPI.GetObservation(id) != null)
				{
					continue;
				}
				JournalAPI.AddObservation(KingdomResearchRules.LeadText(node.Named, node.Branch), id, NoteCategory, id, null,
					revealed: false, -1L);
			}
		}

		/// <summary>Whether the founder has heard of this node at all. An O(1) lookup in vanilla's
		/// own note map, deliberately not the scan beside it.</summary>
		public static bool Discovered(string Key)
		{
			if (!Enabled)
			{
				return false;
			}
			FileNotes();
			string id = NoteId(Key);
			return id != null && JournalAPI.HasNote(id);
		}

		/// <summary>
		/// Tells the founder a node exists, and where they heard it. Vanilla stamps the provenance
		/// on the entry itself, so the chronicle line writes itself and the note is sellable at a
		/// water ritual like every other thing they have learned about the world.
		/// </summary>
		/// <param name="Key">The node.</param>
		/// <param name="LearnedFrom">Who said so, in the founder's words. May be null.</param>
		/// <returns>True when this call is what revealed it.</returns>
		public static bool Reveal(string Key, string LearnedFrom)
		{
			if (!Enabled || Discovered(Key))
			{
				return false;
			}
			string id = NoteId(Key);
			if (id == null || !JournalAPI.TryRevealNote(id, LearnedFrom))
			{
				return false;
			}
			KingdomLog.Log("research: revealed " + Key + ((LearnedFrom == null) ? "" : (" (" + LearnedFrom + ")")));
			return true;
		}

		// ==================================================================================
		// Holdings, admissibility, and the closed door
		// ==================================================================================

		/// <summary>Whether the SEATED city's keepers hold this node.</summary>
		public static bool Held(KingdomSystem System, string Key)
		{
			ResearchNode node;
			if (!TryGetNode(Key, out node))
			{
				return false;
			}
			return Holds(KingdomZoning.Roster(System), node);
		}

		/// <summary>Whether a city the founder is not standing in holds this node. The teaching act
		/// has to be able to ask.</summary>
		public static bool HeldIn(KingdomSettlement City, string Key)
		{
			ResearchNode node;
			if (City == null || !TryGetNode(Key, out node))
			{
				return false;
			}
			return Holds(KingdomZoning.RosterOf(City), node);
		}

		private static bool Holds(List<string> Roster, ResearchNode Node)
		{
			foreach (string token in KingdomZoningRules.Tokens(Node.Grants))
			{
				if (!KingdomZoningRules.Knows(Roster, token))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Whether a node EXISTS for this realm at all. A node whose quest is unfinished is absent
		/// from the registry's answer to every question the map asks &mdash; not hidden-but-counted,
		/// not revealed-and-locked. A node whose <c>Forbidden</c> list names something this city's
		/// people are is the same absence, and deliberately indistinguishable from it: that is the
		/// visibility law's hard clause, and it holds by construction rather than by care.
		/// </summary>
		public static bool Admissible(KingdomSystem System, ResearchNode Node)
		{
			if (Node == null)
			{
				return false;
			}
			if (!QuestFinished(Node.Quest))
			{
				return false;
			}
			if (Node.Forbidden == null)
			{
				return true;
			}
			List<string> roster = KingdomZoning.Roster(System);
			BuilderRoll roll = KingdomZoning.BuilderRollOf(System);
			foreach (string token in KingdomZoningRules.Tokens(Node.Forbidden))
			{
				if (KingdomZoningRules.Knows(roster, token))
				{
					return false;
				}
				// A creed nobody here holds and nobody here has ever held is not a forbidding: the
				// road is simply not this city's. What forbids is the creed being HERE.
				if (KingdomZoningRules.KindOf(token) == KingdomZoningRules.KindCreed
					&& roll.Aligned(KingdomZoningRules.NameOf(token)) > 0)
				{
					return false;
				}
			}
			return true;
		}

		private static bool QuestFinished(string Lock)
		{
			if (string.IsNullOrEmpty(Lock))
			{
				return true;
			}
			bool finished;
			if (QuestCache.TryGetValue(Lock, out finished))
			{
				return finished;
			}
			finished = false;
			int at = Lock.IndexOf('~');
			string id = (at > 0) ? Lock.Substring(0, at) : Lock;
			string step = (at > 0 && at < Lock.Length - 1) ? Lock.Substring(at + 1) : null;
			if (step == null)
			{
				finished = The.Game != null && The.Game.HasFinishedQuest(id);
			}
			else
			{
				// Deliberately not HasFinishedQuestStep, which answers true for ANY step id once
				// the whole quest is finished, including ids that do not exist.
				XRL.World.Quest quest;
				finished = The.Game != null && The.Game.TryGetQuest(id, out quest) && quest != null && quest.IsStepFinished(step);
			}
			QuestCache[Lock] = finished;
			return finished;
		}

		// ==================================================================================
		// The tier gate and the crew
		// ==================================================================================

		/// <summary>
		/// The best Intelligence among the seated city's people, measured now if their zone is
		/// loaded and remembered on the city if it is not. The tier is checked against the CITY's
		/// researchers and never against the founder, which is the clause that keeps a well-travelled
		/// founder from being the research system (Addendum 18).
		/// </summary>
		public static int BestMind(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return 0;
			}
			Zone zone = The.ZoneManager?.ActiveZone;
			if (zone != null && System.ClaimedZones != null && System.ClaimedZones.Contains(zone.ZoneID))
			{
				int best = 0;
				List<GameObject> settlers = KingdomSurvey.Take(zone, System).Settlers;
				for (int i = 0; i < settlers.Count; i++)
				{
					int mind = KingdomCrews.CapabilityOf(settlers[i]).ValueOf(KingdomCrewRules.KindIntelligence);
					if (mind > best)
					{
						best = mind;
					}
				}
				System.ResearchBestMind = best;
			}
			return System.ResearchBestMind;
		}

		// ==================================================================================
		// Setting the subject, and the reasons a subject cannot be set
		// ==================================================================================

		/// <summary>
		/// Whether the seated city may take up this subject, with the whole sentence when it may
		/// not. Every refusal names the lack AND what would lift it, because a gate that only says
		/// no teaches nothing (STANDARDS 7b).
		/// </summary>
		public static bool CanTakeUp(KingdomSystem System, ResearchNode Node, out string Refusal)
		{
			Refusal = null;
			if (System == null || !System.Founded || Node == null)
			{
				Refusal = "You rule nothing yet.";
				return false;
			}
			string seat = System.SeatName;
			if (Held(System, Node.Key))
			{
				Refusal = "The keepers of " + seat + " already have " + Node.Named + " written down.";
				return false;
			}
			List<string> roster = KingdomZoning.Roster(System);
			List<string> missing = KingdomZoningRules.MissingKnowledge(roster, Node.Requires);
			if (missing.Count > 0)
			{
				Refusal = "Nobody at " + seat + " can begin " + Node.Named + " yet. It wants {{C|"
					+ KingdomZoningRules.JoinAnd(KingdomZoningRules.DescribeKeys(missing))
					+ "}}. Learn that first, here, and the bench can take this up.";
				return false;
			}
			TechLevel tech = KingdomZoning.Tech(System);
			if (Node.MinTech > tech)
			{
				Refusal = seat + " builds at the level of {{C|" + KingdomZoningRules.TechName(tech) + "}}, and "
					+ Node.Named + " wants {{C|" + KingdomZoningRules.TechName(Node.MinTech)
					+ "}}. Teach the keepers more designs and certify more machines hauled home.";
				return false;
			}
			int mind = BestMind(System);
			if (!KingdomResearchRules.TierReached(mind, Node.Tier))
			{
				Refusal = KingdomResearchRules.TierRefusal(seat, Node.Named, mind, KingdomResearchRules.IntelligenceForTier(Node.Tier));
				return false;
			}
			return true;
		}

		/// <summary>
		/// Sets the one subject the seated city's bench is working out, shelving whatever it was
		/// working out before with its labour intact.
		/// <para>
		/// Preconditions: a founded realm and a node <see cref="CanTakeUp"/> permits. Side effects:
		/// the city's subject, accrual and shelf are rewritten and the stall flag is cleared.
		/// Failure mode: returns false with <paramref name="Refusal"/> set, having changed nothing.
		/// </para>
		/// </summary>
		public static bool TakeUp(KingdomSystem System, string Key, out string Refusal,
			string GovernanceVerb = null)
		{
			ResearchNode node;
			Refusal = null;
			if (!TryGetNode(Key, out node) || !Admissible(System, node))
			{
				Refusal = "Nobody here has heard of that.";
				return false;
			}
			if (!CanTakeUp(System, node, out Refusal))
			{
				return false;
			}
			Shelve(System, GovernanceVerb);
			System.ResearchSubject = node.Key;
			MarkGovernance(GovernanceVerb);
			System.ResearchAccrued = Recall(System, node.Key);
			System.ResearchTakenUpTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
			System.ResearchStalledAnnounced = false;
			KingdomLog.Log("research: " + System.SeatName + " takes up " + node.Key + " at " + System.ResearchAccrued + " ticks");
			return true;
		}

		// The shelf is memory, never a queue: the abandoned subject keeps its labour and nothing
		// there progresses. The ninth shelving drops the least advanced row and says so once.
		private static void Shelve(KingdomSystem System, string GovernanceVerb = null)
		{
			if (string.IsNullOrEmpty(System.ResearchSubject) || System.ResearchAccrued <= 0)
			{
				return;
			}
			if (System.ResearchShelf == null)
			{
				System.ResearchShelf = new Dictionary<string, int>();
			}
			if (!System.ResearchShelf.ContainsKey(System.ResearchSubject))
			{
				string crowded = KingdomResearchRules.Crowded(System.ResearchShelf);
				if (crowded != null)
				{
					System.ResearchShelf.Remove(crowded);
					MarkGovernance(GovernanceVerb);
					ResearchNode dropped;
					System.Ledger.Note("{{K|" + KingdomResearchRules.ForgottenLine(System.SeatName,
						TryGetNode(crowded, out dropped) ? dropped.Named : crowded) + "}}");
				}
			}
			System.ResearchShelf[System.ResearchSubject] = System.ResearchAccrued;
			MarkGovernance(GovernanceVerb);
		}

		private static int Recall(KingdomSystem System, string Key)
		{
			int accrued;
			if (System.ResearchShelf != null && System.ResearchShelf.TryGetValue(Key, out accrued))
			{
				System.ResearchShelf.Remove(Key);
				return (accrued > 0) ? accrued : 0;
			}
			return 0;
		}

		// ==================================================================================
		// What the keepers' screen offers
		// ==================================================================================

		/// <summary>
		/// Reveals the roots of the tree: every admissible node that asks for nothing at all.
		/// <para>
		/// The visibility law needs a place to START. A node with no requirements, no seed, no
		/// teacher and no quest is a thing anybody standing in the city can see somebody could
		/// begin &mdash; the founder wonders whether anyone here is writing this down, and that
		/// wondering is the discovery. Everything past the roots is found the way everything else in
		/// this mod is found: by doing something in the world.
		/// </para>
		/// </summary>
		public static void RevealRoots(KingdomSystem System)
		{
			if (!Enabled || System == null || !System.Founded)
			{
				return;
			}
			EnsureLoaded();
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				if (node.Requires == null && node.SeededBy == null && node.TaughtBy == null && node.Quest == null
					&& Admissible(System, node))
				{
					Reveal(node.Key, System.SeatName);
				}
			}
		}

		/// <summary>
		/// Reads the city's own rolls for nodes somebody has already answered.
		/// <para>
		/// Two arms, and the difference between them is Addendum 18's whole ruling. A
		/// <c>TaughtBy</c> token &mdash; a disk read to the keepers, a treatise &mdash; HOLDS the
		/// node outright: somebody wrote the answer down and the keepers copied it. A
		/// <c>SeededBy</c> token &mdash; every <c>rite:</c> key, and a machine whose insides were
		/// most of the answer &mdash; only reveals the node and begins it. The founder opens the
		/// door; the city walks through.
		/// </para>
		/// <para>
		/// Called wherever a roll can have changed: after a disk is taught, after a machine is
		/// certified, and whenever the founder looks at what the keepers know. Idempotent.
		/// </para>
		/// </summary>
		public static void ApplySources(KingdomSystem System)
		{
			KingdomSystem.Guard("research sources", delegate
			{
				if (!Enabled || System == null || !System.Founded)
				{
					return;
				}
				EnsureLoaded();
				List<string> roster = KingdomZoning.Roster(System);
				for (int i = 0; i < _nodes.Count; i++)
				{
					ResearchNode node = _nodes[i];
					if (Holds(roster, node) || !Admissible(System, node))
					{
						continue;
					}
					string taught = AnySatisfied(roster, node.TaughtBy);
					if (taught != null)
					{
						Complete(System, node, KingdomZoningRules.NameOf(taught));
						roster = KingdomZoning.Roster(System);
						continue;
					}
					List<string> seeded = KingdomZoningRules.SatisfyingKeys(roster, node.SeededBy);
					int sourceCount = SeedSourceCount(System, node.Key);
					string learnedFrom = null;
					for (int j = 0; j < seeded.Count; j++)
					{
						int nextCount = ApplySeedSourceReceipt(System, node.Key, seeded[j]);
						if (nextCount < 0)
						{
							break;
						}
						sourceCount = KingdomResearchRules.DurableSeedSourceCount(sourceCount, nextCount);
						if (SeedSourceRecorded(System, node.Key, seeded[j]))
						{
							learnedFrom = KingdomZoningRules.NameOf(seeded[j]);
						}
					}
					if (sourceCount > 0)
					{
						SeedBySources(System, node.Key, learnedFrom ?? System.SeatName, sourceCount);
					}
				}
			});
		}

		/// <summary>
		/// Records what the founder was told on the first sharing of water with a faction, then
		/// applies that source to the seated city's research when one exists. A rite belongs to the
		/// founder's permanent ledger (Addendum 22 B1/B3), not to one city's rolls: a rite performed
		/// before founding, between realms, or before city two still opens the same door wherever the
		/// founder later takes it. The existing <see cref="ApplySources"/> path reveals and seeds only
		/// matching heads; it never completes one.
		/// <para>
		/// The ledger write deliberately does not depend on <see cref="Enabled"/> or on a founded
		/// realm. Turning the
		/// research option off must pause the research surface, not make a water ritual that happened
		/// while it was off cease to have happened; a later keepers' read applies the retained source.
		/// </para>
		/// </summary>
		/// <returns>True when this call added the rite key to the founder's ledger.</returns>
		internal static bool RememberRite(KingdomSystem System, bool Initial, string Faction)
		{
			if (!KingdomResearchRules.MayRememberRite(Initial, Faction) || The.Game == null)
			{
				return false;
			}
			string key = KingdomZoningRules.ComposeKey(KingdomZoningRules.KindRite, Faction);
			List<string> rites = FounderRites();
			bool learned = !rites.Contains(key);
			if (learned)
			{
				if (rites.Count >= KingdomResearchRules.MaxFounderRites)
				{
					return false;
				}
				rites.Add(key);
				string encoded = KingdomZoningRules.EncodeRoster(rites);
				The.Game.SetStringGameState(FounderRiteState, encoded);
				if (!string.Equals(The.Game.GetStringGameState(FounderRiteState, ""), encoded,
					StringComparison.Ordinal))
				{
					return false;
				}
			}
			// Apply even when the founder already remembers the faction. Vanilla's Initial bit is
			// per ritualist, not an entitlement to re-run the rite, and this keeps a retained source
			// useful after a registry reload or an option change without making a second key.
			if (System != null && System.Founded)
			{
				KingdomResearch.ApplySources(System);
			}
			return learned;
		}

		internal const string FounderRiteState = "r_TAF_FounderRites";

		/// <summary>The founder-held permanent rite keys, separate from every city's rolls.</summary>
		internal static List<string> FounderRites()
		{
			List<string> result = new List<string>();
			if (The.Game == null)
			{
				return result;
			}
			return KingdomResearchRules.CanonicalFounderRites(
				The.Game.GetStringGameState(FounderRiteState, ""));
		}

		// The first token of a source list the city's rolls actually satisfy, or null. Any ONE of
		// them is enough: a node with two teachers is taught by either.
		private static string AnySatisfied(List<string> Roster, string Tokens)
		{
			foreach (string token in KingdomZoningRules.Tokens(Tokens))
			{
				string concrete = KingdomZoningRules.SatisfyingKey(Roster, token);
				if (concrete != null)
				{
					return concrete;
				}
			}
			return null;
		}

		private const string SeedReceiptStatePrefix = "r_TAF_ResearchSeedSources:";

		// A SeededBy arm is one source, not a button the keepers' screen may press every time it
		// opens. Receipts live in game state under the city's immutable id: they follow the city
		// through a seat swap, secession and reload without widening the serialized settlement wire
		// format. The source name is folded through the roster's own grammar, so an XML override
		// that changes only case must not buy another quarter of the same node.
		private static int ApplySeedSourceReceipt(KingdomSystem System, string NodeKey,
			string ConcreteSource)
		{
			string state = SeedReceiptState(System);
			if (state == null || The.Game == null)
			{
				return -1;
			}
			string before = The.Game.GetStringGameState(state, "");
			string updated;
			int count;
			bool changed;
			if (!KingdomResearchRules.TryApplySeedReceipt(before, NodeKey, ConcreteSource,
				out updated, out count, out changed))
			{
				return -1;
			}
			if (changed)
			{
				The.Game.SetStringGameState(state, updated);
				if (!string.Equals(The.Game.GetStringGameState(state, ""), updated,
					StringComparison.Ordinal))
				{
					return -1;
				}
			}
			return count;
		}

		private static int SeedSourceCount(KingdomSystem System, string NodeKey)
		{
			string state = SeedReceiptState(System);
			return state == null || The.Game == null ? 0 : KingdomResearchRules.SeedReceiptCount(
				The.Game.GetStringGameState(state, ""), NodeKey);
		}

		private static bool SeedSourceRecorded(KingdomSystem System, string NodeKey,
			string ConcreteSource)
		{
			string state = SeedReceiptState(System);
			return state != null && The.Game != null && KingdomResearchRules.SeedReceiptStored(
				The.Game.GetStringGameState(state, ""), NodeKey, ConcreteSource);
		}

		private static string SeedReceiptState(KingdomSystem System)
		{
			string settlement = (System == null) ? null : System.CurrentSettlementId;
			return string.IsNullOrEmpty(settlement) ? null : SeedReceiptStatePrefix + settlement;
		}

		/// <summary>
		/// Reveals every node a thing the founder is CARRYING could teach or seed.
		/// <para>
		/// Vanilla's own precedent: a data disk you cannot yet learn from still tells you what it
		/// is, provided you could read it. Ours is the same shape one step out &mdash; you hold the
		/// fragment and there are keepers who could use it, so you now know the thing exists. Only
		/// what the founder actually carries; reaching into containers they merely own would be the
		/// protection law broken for a convenience.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Carried">Roster keys the founder is carrying, e.g. <c>disk:arc winder</c>.</param>
		public static void RevealFromCarried(KingdomSystem System, IEnumerable<string> Carried)
		{
			if (!Enabled || System == null || !System.Founded || Carried == null)
			{
				return;
			}
			EnsureLoaded();
			List<string> held = new List<string>(Carried);
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				if (Discovered(node.Key) || !Admissible(System, node))
				{
					continue;
				}
				if (AnySatisfied(held, node.TaughtBy) != null || AnySatisfied(held, node.SeededBy) != null
					|| AnySatisfied(held, node.Requires) != null)
				{
					Reveal(node.Key, "what you are carrying");
				}
			}
		}

		/// <summary>
		/// The subjects this city's bench could be set, nearest first: heard of, admissible, and not
		/// already held here. A node the founder has never heard of is absent, and so is one this
		/// city can never reach &mdash; identically, which is the law's hard clause.
		/// </summary>
		public static List<ResearchNode> Offerable(KingdomSystem System)
		{
			List<ResearchNode> offered = new List<ResearchNode>();
			if (!Enabled || System == null || !System.Founded)
			{
				return offered;
			}
			List<ResearchRow> rows = HeardOf(System);
			for (int i = 0; i < rows.Count; i++)
			{
				ResearchNode node;
				if (TryGetNode(rows[i].Key, out node))
				{
					offered.Add(node);
				}
			}
			return offered;
		}

		/// <summary>
		/// What the realm's OTHER city's keepers have worked out and this city's have not: the set
		/// the teaching act sets down. Nothing here is a holding when it arrives &mdash; it is a
		/// seed, and the receiving city still walks the rest (Addendum 22 B4).
		/// </summary>
		public static List<ResearchNode> CarriedFromAway(KingdomSystem System)
		{
			List<ResearchNode> carried = new List<ResearchNode>();
			if (!Enabled || System == null || !System.Founded || System.Away == null)
			{
				return carried;
			}
			EnsureLoaded();
			List<string> theirs = KingdomZoning.RosterOf(System.Away);
			List<string> ours = KingdomZoning.Roster(System);
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				if (Holds(theirs, node) && !Holds(ours, node) && Admissible(System, node))
				{
					carried.Add(node);
				}
			}
			return carried;
		}

		// ==================================================================================
		// The benches
		// ==================================================================================

		/// <summary>
		/// Gives every standing work in this city that is a place people think at the part that
		/// charges thinking.
		/// <para>
		/// <b>The lab BUILDING is its own wave, and this is the seam it lands on.</b> When the
		/// laboratory and the arclight annexe are raised they will carry
		/// <c>r_KingdomInquiry</c> on their blueprints and this sweep will find nothing left to do;
		/// until then the shipped scriptorium is the bench, because it already is one &mdash; a
		/// staffed knowledge work with two people at it &mdash; and a research system with nowhere
		/// to think would be content nobody could reach.
		/// </para>
		/// <para>
		/// It touches only objects this mod raised and marked (<c>KingdomBuilt</c>) and only ones
		/// whose design is a staffed knowledge work: the protection law is not bent for a
		/// convenience, and a shelf is not a bench because nobody stands at a shelf.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone to sweep. Null or unclaimed ground does nothing.</param>
		public static void EnsureBenches(KingdomSystem System, Zone Z)
		{
			KingdomSystem.Guard("research benches", delegate
			{
				if (!Enabled || System == null || !System.Founded || Z == null
					|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
				{
					return;
				}
				List<GameObject> built = KingdomSurvey.Take(Z, System).Built;
				for (int i = 0; i < built.Count; i++)
				{
					GameObject work = built[i];
					if (!GameObject.Validate(work) || work.HasPart<XRL.World.Parts.r_KingdomInquiry>()
						|| work.GetIntProperty(KingdomAdopt.StaffNeededProperty) <= 0)
					{
						continue;
					}
					KingdomRules.BuildEntry entry;
					if (!KingdomData.TryGetBuilding(work.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out entry)
						|| entry.Category != BenchCategory)
					{
						continue;
					}
					work.AddPart(new XRL.World.Parts.r_KingdomInquiry());
					KingdomLog.Log("research: " + work.ShortDisplayName + " at " + System.SeatName + " is a bench");
				}
			});
		}

		/// <summary>The catalogue category a design has to be in for its finished work to be a place
		/// people think at.</summary>
		public const string BenchCategory = "knowledge";

		// ==================================================================================
		// The loop: one stretch of thinking, charged
		// ==================================================================================

		/// <summary>
		/// Charges one stretch of elapsed world time against the seated city's subject, at the pace
		/// this one lab's crew, condition, best mind and bench actually manage, and completes the
		/// node when the work runs out.
		/// <para>
		/// <b>Each lab charges its own stretch, from whichever is later of its own last-worked stamp
		/// and the tick the city took the subject up.</b> That is what makes a second lab THROUGHPUT
		/// rather than a second lane (RR2) while keeping idle time SPENT and never banked: a bench
		/// nobody has looked at since last winter cannot cash the winter against a subject set this
		/// morning.
		/// </para>
		/// <para>
		/// An unstaffed, unsupplied, or over-its-head lab produces nothing and says so once, and
		/// unsays it the moment the block lifts (Addendum 8 clause 2, STANDARDS 7b).
		/// </para>
		/// <para>
		/// Preconditions: none; every degenerate case answers by charging nothing. Side effects: the
		/// city's accrual, subject and stall flag may change, and a completed node mints roster
		/// keys. Failure mode: guarded, so a fault logs and charges nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm; the seated city is the one that thinks.</param>
		/// <param name="TimeTick">Now.</param>
		/// <param name="LabLastWorkedTick">This lab's own previous stamp, 0 before its first look.</param>
		/// <param name="CrewEffectiveness">Headcount and capability combined, 0 to 100.</param>
		/// <param name="WearEffectiveness">What the lab's condition leaves of it, 0 to 100.</param>
		/// <param name="LabPercent">The bench's own rung
		/// (<see cref="KingdomResearchRules.ScriptoriumPercent"/> and its kin).</param>
		/// <param name="LabName">What the founder calls the building, for the 7b sentence.</param>
		/// <returns>The lab's new stamp, which the caller stores. Always
		/// <paramref name="TimeTick"/> once a look has happened.</returns>
		public static long Advance(KingdomSystem System, long TimeTick, long LabLastWorkedTick, int CrewEffectiveness,
			int WearEffectiveness, int LabPercent, string LabName)
		{
			KingdomSystem.Guard("research", delegate
			{
				if (!Enabled || System == null || !System.Founded)
				{
					return;
				}
				ResearchNode node;
				if (string.IsNullOrEmpty(System.ResearchSubject) || !TryGetNode(System.ResearchSubject, out node))
				{
					Stall(System, KingdomResearchRules.NoSubjectLine(LabName, System.SeatName));
					return;
				}
				long from = (LabLastWorkedTick > System.ResearchTakenUpTick) ? LabLastWorkedTick : System.ResearchTakenUpTick;
				long elapsed = TimeTick - from;
				if (from <= 0L || elapsed <= 0L)
				{
					return;
				}
				int mind = BestMind(System);
				int bonus = KingdomResearchRules.TierBonus(mind, node.Tier);
				int rate = KingdomResearchRules.InquiryRate(CrewEffectiveness, WearEffectiveness, bonus, LabPercent);
				if (rate <= 0)
				{
					Stall(System, KingdomResearchRules.StallLine(LabName, node.Named, CrewEffectiveness, WearEffectiveness,
						mind, KingdomResearchRules.IntelligenceForTier(node.Tier)));
					return;
				}
				Unstall(System);
				int worked = KingdomResearchRules.Worked(elapsed, rate);
				if (worked <= 0)
				{
					return;
				}
				int effort = KingdomResearchRules.EffortTicks(node.Effort);
				System.ResearchAccrued += worked;
				if (System.ResearchAccrued < effort)
				{
					return;
				}
				System.ResearchAccrued = 0;
				System.ResearchSubject = null;
				Complete(System, node, System.SeatName + "'s own bench");
			});
			return TimeTick;
		}

		private static void Stall(KingdomSystem System, string Line)
		{
			if (Line == null || System.ResearchStalledAnnounced)
			{
				return;
			}
			System.ResearchStalledAnnounced = true;
			System.Ledger.Note("{{r|" + Line + "}}");
		}

		private static void Unstall(KingdomSystem System)
		{
			System.ResearchStalledAnnounced = false;
		}

		// ==================================================================================
		// Completion, and the seed that is never completion
		// ==================================================================================

		/// <summary>
		/// Holds a node in the seated city: mints its <c>Grants</c> through the same
		/// <see cref="KingdomZoning.Learn"/> a data disk uses, reveals what it opens onto (filtered
		/// through admissibility first, so a road this city can never walk is never offered), and
		/// records it.
		/// </summary>
		/// <returns>True when this call is what held it.</returns>
		public static bool Complete(KingdomSystem System, ResearchNode Node, string LearnedFrom)
		{
			if (System == null || !System.Founded || Node == null || Held(System, Node.Key))
			{
				return false;
			}
			foreach (string token in KingdomZoningRules.Tokens(Node.Grants))
			{
				KingdomZoning.Learn(System, KingdomZoningRules.KindOf(token) ?? KingdomZoningRules.KindNode,
					KingdomZoningRules.NameOf(token));
			}
			Reveal(Node.Key, LearnedFrom);
			foreach (string token in KingdomZoningRules.Tokens(Node.Reveals))
			{
				ResearchNode opened;
				// The closed door: Reveals is filtered through admissibility BEFORE it is applied,
				// so a city that finishes butchery is offered physic and is never offered a road its
				// own people close -- and is never told that anything was filtered.
				if (TryGetNode(KingdomZoningRules.NameOf(token), out opened) && Admissible(System, opened))
				{
					Reveal(opened.Key, System.SeatName);
				}
			}
			XRL.Messages.MessageQueue.AddPlayerMessage("{{G|The keepers of " + System.SeatName + " have worked out " + Node.Named + ".}}");
			KingdomChronicle.Record(System, "the keepers of " + System.SeatName + " worked out " + Node.Named);
			System.RecordDeed("set the keepers of " + System.SeatName + " to work out " + Node.Named);
			KingdomLog.Log("research: " + System.SeatName + " completed " + Node.Key);
			return true;
		}

		/// <summary>
		/// Seeds a node in the seated city: reveals it, and credits its bench with a head start it
		/// could not have earned. Never completes and never skips a tier &mdash; the founder opens
		/// the door and the city walks through (Addendum 18, generalised to exile and to teaching by
		/// Addendum 22 B3/B4).
		/// </summary>
		/// <returns>True when anything changed.</returns>
		public static bool Seed(KingdomSystem System, string Key, string LearnedFrom,
			string GovernanceVerb = null)
		{
			return SeedCore(System, Key, LearnedFrom, 0, false, GovernanceVerb);
		}

		/// <summary>
		/// Seeds from one durable, concrete source. Repeating the same transfer is a no-op; a
		/// genuinely different source raises the recoverable floor, up to the shared half-way cap.
		/// The receipt is written first, so a save or exception between the write and the bench
		/// update is repaired by the next attempt rather than charged twice.
		/// </summary>
		internal static bool SeedFromSource(KingdomSystem System, string Key, string ConcreteSource,
			string LearnedFrom, string GovernanceVerb = null)
		{
			ResearchNode node;
			if (System == null || !System.Founded || !Enabled ||
				!TryGetNode(Key, out node) || !Admissible(System, node) || Held(System, node.Key))
			{
				return false;
			}
			int sourceCount = ApplySeedSourceReceipt(System, node.Key, ConcreteSource);
			return sourceCount > 0 && SeedBySources(System, node.Key, LearnedFrom,
				sourceCount, GovernanceVerb);
		}

		private static bool SeedBySources(KingdomSystem System, string Key, string LearnedFrom,
			int SourceCount, string GovernanceVerb = null)
		{
			return SeedCore(System, Key, LearnedFrom, SourceCount, true, GovernanceVerb);
		}

		private static bool SeedCore(KingdomSystem System, string Key, string LearnedFrom,
			int SourceCount, bool UseSourceFloor, string GovernanceVerb)
		{
			ResearchNode node;
			if (System == null || !System.Founded || !Enabled || !TryGetNode(Key, out node) || !Admissible(System, node))
			{
				return false;
			}
			if (Held(System, node.Key))
			{
				return false;
			}
			bool revealed = Reveal(node.Key, LearnedFrom);
			if (revealed)
			{
				MarkGovernance(GovernanceVerb);
			}
			int standing = (System.ResearchSubject == node.Key) ? System.ResearchAccrued : Peek(System, node.Key);
			int seeded = UseSourceFloor
				? KingdomResearchRules.SeededBySources(node.Effort, standing, SourceCount)
				: KingdomResearchRules.Seeded(node.Effort, standing);
			if (seeded <= standing)
			{
				return revealed;
			}
			if (System.ResearchSubject == node.Key)
			{
				System.ResearchAccrued = seeded;
				MarkGovernance(GovernanceVerb);
			}
			else
			{
				if (System.ResearchShelf == null)
				{
					System.ResearchShelf = new Dictionary<string, int>();
				}
				if (!System.ResearchShelf.ContainsKey(node.Key))
				{
					string crowded = KingdomResearchRules.Crowded(System.ResearchShelf);
					if (crowded != null)
					{
						System.ResearchShelf.Remove(crowded);
					}
				}
				System.ResearchShelf[node.Key] = seeded;
				MarkGovernance(GovernanceVerb);
			}
			KingdomLog.Log("research: seeded " + node.Key + " at " + System.SeatName + " to " + seeded + " ticks");
			return true;
		}

		private static void MarkGovernance(string Verb)
		{
			if (!string.IsNullOrEmpty(Verb) && !KingdomGovernanceScope.HasCommitted)
			{
				KingdomGovernanceScope.Commit(Verb);
			}
		}

		private static int Peek(KingdomSystem System, string Key)
		{
			int accrued;
			return (System.ResearchShelf != null && System.ResearchShelf.TryGetValue(Key, out accrued) && accrued > 0) ? accrued : 0;
		}

		// ==================================================================================
		// The reach into the catalogue — the visibility law's single filter
		// ==================================================================================

		/// <summary>
		/// Whether the founder has heard of everything a design's <c>Knowledge</c> gate names.
		/// <para>
		/// The one place the visibility law touches the catalogue, and it is deliberately the place
		/// every menu, every map row and every refusal already funnels through
		/// (<c>KingdomZoning.Visible</c>), so a third party's building gated on a hidden node is
		/// filtered by the same code as ours. A requirement token whose every arm is a
		/// <c>node:</c> key the founder has never heard of hides the design outright: vanilla's own
		/// precedent for an unknown recipe is total omission, never a greyed-out row.
		/// </para>
		/// </summary>
		public static bool KnowledgeGateHeardOf(KingdomSystem System, string Knowledge)
		{
			if (!Enabled || string.IsNullOrEmpty(Knowledge))
			{
				return true;
			}
			List<string> roster = KingdomZoning.Roster(System);
			List<string> discovered = null;
			foreach (string token in KingdomZoningRules.Tokens(Knowledge))
			{
				if (KingdomZoningRules.Knows(roster, token))
				{
					continue;
				}
				if (discovered == null)
				{
					discovered = DiscoveredKeys();
				}
				if (!KingdomResearchRules.AnyRoadVisible(token, discovered))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Every node key the founder has heard of. Gathered once per question rather than
		/// per token, and never for a gate that is already satisfied.</summary>
		public static List<string> DiscoveredKeys()
		{
			List<string> keys = new List<string>();
			if (!Enabled)
			{
				return keys;
			}
			EnsureLoaded();
			for (int i = 0; i < _nodes.Count; i++)
			{
				if (Discovered(_nodes[i].Key))
				{
					keys.Add(_nodes[i].Key);
				}
			}
			return keys;
		}

		// ==================================================================================
		// The two lanes a held node feeds
		// ==================================================================================

		/// <summary>Every effect of every node the seated city holds. The one read the method lane
		/// and the citizen ceiling share.</summary>
		public static List<ResearchEffect> HeldEffects(KingdomSystem System)
		{
			List<ResearchEffect> effects = new List<ResearchEffect>();
			if (!Enabled || System == null || !System.Founded)
			{
				return effects;
			}
			List<string> roster = KingdomZoning.Roster(System);
			EnsureLoaded();
			for (int i = 0; i < _nodes.Count; i++)
			{
				if (Holds(roster, _nodes[i]))
				{
					effects.AddRange(_nodes[i].Effects);
				}
			}
			return effects;
		}

		/// <summary>
		/// What the keepers' method is worth to every work this city runs, as a percent to multiply
		/// output by. A third factor beside crew and condition, never folded into either: idle still
		/// produces nothing, because zero times anything is zero, and method never papers over a
		/// broken building.
		/// </summary>
		public static int MethodPercent(KingdomSystem System)
		{
			return KingdomResearchRules.MethodPercent(KingdomResearchRules.Efficiency(HeldEffects(System)));
		}

		/// <summary>How far above what they walked in with this city may teach one citizen in one
		/// stat. See <see cref="KingdomResearchRules.Headroom"/> for the clamps, and RR8 for why
		/// this is ours and never <c>Statistic.Max</c>.</summary>
		public static int Headroom(KingdomSystem System, string Stat)
		{
			return KingdomResearchRules.Headroom(HeldEffects(System), Stat);
		}

		/// <summary>Property one citizen's stat as they WALKED IN is remembered under, stamped the
		/// first time this city looks at them. What the city may teach is measured from there, so a
		/// citizen never exceeds what they arrived with plus what the city knows how to teach.</summary>
		public const string BaseStatPrefix = "KingdomBaseStat_";

		/// <summary>
		/// Teaches one citizen one point of one stat, if this city knows how to teach that far.
		/// <para>
		/// <b>Vanilla's <c>Statistic.Max</c> is never written, and this is the reason the method
		/// exists at all:</b> <c>_Max</c> is a static dictionary of boxed ints keyed by stat NAME, so
		/// one write would raise the ceiling for every creature in Qud, the player included. The
		/// ceiling here is OURS &mdash; what they walked in with, plus this city's headroom &mdash;
		/// and vanilla is touched only through <c>BaseValue</c>, which fires its own notification and
		/// lets the engine keep hit points and skill points consistent for itself.
		/// </para>
		/// <para>
		/// Preconditions: a founded realm and a real citizen. Side effects: one <c>BaseValue</c>
		/// write and, on the first look, one remembered base. Failure mode: returns false having
		/// changed nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Citizen">The settler. Null and non-citizens are refused.</param>
		/// <param name="Stat">A vanilla stat name, e.g. <c>Intelligence</c>.</param>
		/// <returns>True when the citizen is one point better than they were.</returns>
		public static bool Train(KingdomSystem System, GameObject Citizen, string Stat)
		{
			bool taught = false;
			KingdomSystem.Guard("research training", delegate
			{
				if (!Enabled || System == null || !System.Founded || !GameObject.Validate(Citizen) || string.IsNullOrEmpty(Stat))
				{
					return;
				}
				Statistic statistic = Citizen.GetStat(Stat);
				if (statistic == null)
				{
					return;
				}
				string remembered = BaseStatPrefix + Stat;
				int walkedInWith = Citizen.GetIntProperty(remembered);
				if (walkedInWith <= 0)
				{
					walkedInWith = statistic.BaseValue;
					Citizen.SetIntProperty(remembered, walkedInWith);
				}
				int headroom = Headroom(System, Stat);
				if (!KingdomResearchRules.CanTrain(statistic.BaseValue, walkedInWith, headroom))
				{
					return;
				}
				statistic.BaseValue = KingdomResearchRules.TrainedValue(statistic.BaseValue, walkedInWith, headroom);
				taught = true;
			});
			return taught;
		}

		/// <summary>Whether the city has heard of enough people to be sent word of them
		/// (<c>recruitreveal:</c>, the census's own effect). The guestbook's gate for the wave that
		/// adds the lead hook.</summary>
		public static bool HearsOfPeople(KingdomSystem System)
		{
			foreach (ResearchEffect effect in HeldEffects(System))
			{
				if (effect.Kind == KingdomResearchRules.EffectRecruitReveal && effect.Amount > 0)
				{
					return true;
				}
			}
			return false;
		}

		// ==================================================================================
		// What the map reads
		// ==================================================================================

		/// <summary>
		/// The nodes the founder has heard of and this city does not hold, as rows the map can
		/// draw: the name, how far off in things rather than in numbers, and what is in the way.
		/// Hidden and forbidden nodes are absent, and absent identically.
		/// </summary>
		public static List<ResearchRow> HeardOf(KingdomSystem System)
		{
			List<ResearchRow> rows = new List<ResearchRow>();
			if (!Enabled || System == null || !System.Founded)
			{
				return rows;
			}
			EnsureLoaded();
			List<string> roster = KingdomZoning.Roster(System);
			TechLevel tech = KingdomZoning.Tech(System);
			int mind = BestMind(System);
			for (int i = 0; i < _nodes.Count; i++)
			{
				ResearchNode node = _nodes[i];
				if (node.Key == System.ResearchSubject || Holds(roster, node) || !Discovered(node.Key) || !Admissible(System, node))
				{
					continue;
				}
				List<string> missing = KingdomZoningRules.DescribeKeys(KingdomZoningRules.MissingKnowledge(roster, node.Requires));
				bool tierShort = !KingdomResearchRules.TierReached(mind, node.Tier);
				bool techShort = node.MinTech > tech;
				rows.Add(new ResearchRow(node.Key, node.Named,
					KingdomResearchRules.Distance(tierShort, techShort, missing.Count),
					Peek(System, node.Key) > 0,
					KingdomTechMapRules.MissingForNode(missing,
						tierShort ? KingdomResearchRules.IntelligenceForTier(node.Tier) : 0, mind,
						techShort ? KingdomZoningRules.TechName(node.MinTech) : null,
						KingdomZoningRules.TechName(tech))));
			}
			KingdomTechMapRules.SortResearch(rows);
			return rows;
		}

		/// <summary>The subject the seated city is working out, as the map's second chapter reads
		/// it, or null when the bench has taken nothing up.</summary>
		public static string Working(KingdomSystem System)
		{
			ResearchNode node;
			if (!Enabled || System == null || !System.Founded || !TryGetNode(System.ResearchSubject, out node))
			{
				return null;
			}
			List<string> shelved = new List<string>();
			if (System.ResearchShelf != null)
			{
				foreach (KeyValuePair<string, int> row in System.ResearchShelf)
				{
					ResearchNode other;
					shelved.Add(TryGetNode(row.Key, out other) ? other.Named : row.Key);
				}
				shelved.Sort(StringComparer.Ordinal);
			}
			return KingdomTechMapRules.WorkingChapter(node.Named,
				KingdomResearchRules.Reach(0, System.ResearchAccrued > 0), shelved);
		}

		/// <summary>Whether this city's keepers write anything down at all &mdash; the one node the
		/// keepers' map itself waits on (&sect;4.1 T1). With research switched off, they always do,
		/// so the map is exactly what it was before nodes existed.</summary>
		public static bool KeepsNotes(KingdomSystem System)
		{
			if (!Enabled)
			{
				return true;
			}
			EnsureLoaded();
			return _nodes.Count == 0 || KingdomZoningRules.Knows(KingdomZoning.Roster(System), KingdomResearchRules.NotesKey);
		}
	}
}
