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
				string encoded;
				if (!KingdomResearchRules.TryEncodeFounderRites(rites, out encoded)) return false;
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
	}
}
