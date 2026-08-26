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
	}
}
