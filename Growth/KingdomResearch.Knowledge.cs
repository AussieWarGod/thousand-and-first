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
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
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

	}
}
