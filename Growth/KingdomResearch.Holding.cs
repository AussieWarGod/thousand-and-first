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

	}
}
