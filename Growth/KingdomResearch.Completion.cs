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
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return false;
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
			string seat = KingdomPresentation.Rich(System.SeatName);
			XRL.Messages.MessageQueue.AddPlayerMessage("{{G|The keepers of " + seat + " have worked out " + Node.Named + ".}}");
			KingdomChronicle.Record(System, "the keepers of " + seat + " worked out " + Node.Named);
			System.RecordDeed("set the keepers of " + seat + " to work out " + Node.Named);
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
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
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
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
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

	}
}
