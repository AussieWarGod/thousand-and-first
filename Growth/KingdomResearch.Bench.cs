using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomResearch
	{
		// ==================================================================================
		// Setting the subject, and the reasons a subject cannot be set
		// ==================================================================================

		/// <summary>The one pressable research surface: a real inquiry bench on the seated city's
		/// claimed ground. The Charter and technology map remain readings. Selecting here mutates the
		/// exact city whose work the founder is touching, which is RESEARCH-SYSTEM-DESIGN §0(3), not
		/// a remote realm command disguised as a report.</summary>
		public static void OpenBench(GameObject Bench, GameObject Actor)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; the keepers cannot take up a new subject yet.");
				return;
			}
			Zone zone = Bench?.CurrentZone;
			if (!Enabled || system == null || !system.Founded || zone == null
				|| Actor == null || !Actor.IsPlayer() || Actor.CurrentZone != zone
				|| system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID)
				|| !GameObject.Validate(Bench)
				|| !Bench.HasPart<XRL.World.Parts.r_KingdomInquiry>())
			{
				Popup.Show("Research is set at a staffed inquiry bench on the seated city's own ground.");
				return;
			}
			RevealRoots(system);
			ApplySources(system);
			List<ResearchNode> subjects = Offerable(system);
			if (subjects.Count == 0)
			{
				Popup.Show("There is nothing this city's keepers have heard of and not worked out.");
				return;
			}
			List<string> options = new List<string>();
			for (int i = 0; i < subjects.Count; i++)
			{
				string refusal;
				bool can = CanTakeUp(system, subjects[i], out refusal);
				options.Add(subjects[i].Named + (can ? "" : " {{K|[" + refusal + "]}}"));
			}
			int chosen = Popup.PickOption(Title: "What shall they work out at "
				+ Bench.ShortDisplayName + "?",
				Intro: "One thing at a time. Setting a new subject aside keeps whatever work already stands on it.",
				Options: options, AllowEscape: true, RespectOptionNewlines: true);
			if (chosen < 0 || chosen >= subjects.Count) return;
			string failure;
			if (!TakeUp(system, subjects[chosen].Key, out failure,
				"set research subject at " + Bench.ShortDisplayName))
			{
				Popup.Show(failure);
				return;
			}
			Popup.Show("{{G|The keepers of " + KingdomPresentation.Rich(system.SeatName) + " take up "
				+ subjects[chosen].Named + ".}} What comes of it comes of their own work, in their own time.");
		}

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
			string seat = KingdomPresentation.Rich(System.SeatName);
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
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Refusal = "Settlement simulation is paused; the keepers cannot take up a new subject yet.";
				return false;
			}
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
					System.Ledger.Note("{{K|" + KingdomResearchRules.ForgottenLine(KingdomPresentation.Rich(System.SeatName),
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
	}
}
