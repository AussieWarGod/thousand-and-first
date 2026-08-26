using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCeremonyRules
	{
		// ==================================================================================
		// Leader traits
		// ==================================================================================

		private const string LeaderEventStreamId = "taf:ceremony:leader:v1";
		private const uint LeaderEventKind = 1u;
		private const uint VirtueDrawIndex = 0u;
		private const uint FlawDrawIndex = 1u;

		private static readonly string[] Virtues = new string[8]
		{
			"keeps their word like water in a cask, sealed and spent only on purpose",
			"has never once let the ledger lie, even when the truth cost standing",
			"remembers every name on the roster without needing the roll read aloud",
			"works before dawn and says nothing about it",
			"trusts strangers exactly as far as the road has proven them, and no further, and no less",
			"would rather go without than watch the settlement go without",
			"has buried enough of their own to know which griefs are real",
			"says the hard thing to the founder's face, once, and then lets it go"
		};

		private static readonly string[] Flaws = new string[8]
		{
			"cannot let a debt go unmentioned, even a settled one",
			"trusts their own judgment past the point anyone asked for it",
			"keeps a grudge the way the settlement keeps water: carefully, and too long",
			"would rather be right in front of the founder than quietly correct",
			"spends more breath on how a thing should be done than on doing it",
			"cannot stand an empty larder, and has been known to hoard against one",
			"trusts the stranger with the better story over the one with the better sense",
			"has never forgiven the settlement for the year it nearly starved"
		};

		/// <summary>Equilibrium points a notable's virtue is worth.</summary>
		public const int VirtueShadeAmount = 2;

		/// <summary>Equilibrium points a notable's flaw costs. Smaller than the virtue on
		/// purpose: net texture, not a trap.</summary>
		public const int FlawShadeAmount = 1;

		/// <summary>
		/// Draws the one virtue and one flaw a newly named or newly passed office holder carries.
		/// Deterministic in <paramref name="SettlementId"/> and <paramref name="Ordinal"/>
		/// together, which is the whole of "no reroll": the same office transition always draws
		/// the same pair, on any reload, without anything needing to be stored for it.
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="Ordinal">The tick the transition happened at.</param>
		/// <param name="VirtueIndex">Index into <see cref="Virtues"/>.</param>
		/// <param name="FlawIndex">Index into <see cref="Flaws"/>.</param>
		public static void ChooseLeaderTraits(string SettlementId, ulong Ordinal, out int VirtueIndex, out int FlawIndex)
		{
			VirtueIndex = 0;
			FlawIndex = 0;
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(CeremonyRulesVersion, SettlementId, LeaderEventStreamId, LeaderEventKind, Ordinal, out key, out fault))
			{
				return;
			}
			ulong value;
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, VirtueDrawIndex, (ulong)Virtues.Length, out value, out fault))
			{
				VirtueIndex = (int)value;
			}
			if (CounterRandom.TryDrawBelow(CeremonySeed, key, FlawDrawIndex, (ulong)Flaws.Length, out value, out fault))
			{
				FlawIndex = (int)value;
			}
		}

		public static string VirtueText(int Index)
		{
			return (Index >= 0 && Index < Virtues.Length) ? Virtues[Index] : Virtues[0];
		}

		public static string FlawText(int Index)
		{
			return (Index >= 0 && Index < Flaws.Length) ? Flaws[Index] : Flaws[0];
		}

		/// <summary>The chronicle's line naming an office holder's virtue and flaw together, so
		/// no notable is ever chronicled flawless. Lower-case clause, no trailing period.</summary>
		public static string LeaderTraitChronicle(string Title, string HolderName, string SeatName, int VirtueIndex, int FlawIndex)
		{
			string title = string.IsNullOrEmpty(Title) ? "the office" : Title;
			string holder = string.IsNullOrEmpty(HolderName) ? "the new holder" : HolderName;
			return holder + ", " + title + " of " + SeatName + ", " + VirtueText(VirtueIndex) + " -- but " + FlawText(FlawIndex);
		}

		/// <summary>Net equilibrium points one notable's virtue and flaw carry together.</summary>
		public static int LeaderShade()
		{
			return VirtueShadeAmount - FlawShadeAmount;
		}

		// ==================================================================================
		// The shade a named notable carries, and how the settlement reads it
		// ==================================================================================

		/// <summary>
		/// The whole shade one named notable carries into
		/// <c>KingdomCatalogueRules.Equilibrium</c>: their met tastes, the net of their virtue and
		/// their flaw, and whatever their own <c>Prefers</c> found in the quarters they were given
		/// (<c>KingdomQolRules.PreferShade</c>, Addendum 4). One number, because the three halves
		/// were always meant to be one balance rather than three roads to the level.
		/// <para>
		/// Never a penalty: a notable whose tastes are unmet and whose Prefers found nothing still
		/// brings their virtue, and the floor at zero means a hypothetical flaw heavier than a
		/// virtue can never make a settlement carry fewer people than its works honestly do.
		/// </para>
		/// </summary>
		/// <param name="Met">Met flags for the tastes stated, from <see cref="TastesMet"/>. Null
		/// is a notable who stated none.</param>
		/// <param name="PreferShade">This notable's met <c>Prefers</c>, already counted and capped
		/// by <c>KingdomQolRules.PreferShade</c>. Negative reads as none.</param>
		/// <returns>Between zero and <see cref="MaxNotableShade"/>.</returns>
		public static int NotableShade(IList<bool> Met, int PreferShade)
		{
			int shade = TasteShade(Met) + LeaderShade() + ((PreferShade < 0) ? 0 : PreferShade);
			if (shade < 0)
			{
				shade = 0;
			}
			return (shade > MaxNotableShade) ? MaxNotableShade : shade;
		}

		/// <summary>The most any one named notable can ever shade a settlement's equilibrium by:
		/// two tastes met, a virtue net of a flaw, and two <c>Prefers</c> met. Texture, and small
		/// enough to stay texture &mdash; the lift cap in
		/// <c>KingdomCatalogueRules.Equilibrium</c> binds it again on top of this.</summary>
		public static int MaxNotableShade
		{
			get
			{
				return (MaxTastesStated * TasteShadeAmount) + LeaderShade() + KingdomQolRules.MaxPreferShade;
			}
		}

		/// <summary>
		/// The status report's clause naming what the settlement's named notable is worth to the
		/// level, so a shade is a thing the founder reads rather than an invisible modifier
		/// (STANDARDS 7b's own posture, applied to a number that helps rather than blocks).
		/// </summary>
		/// <param name="Shade">From <see cref="NotableShade"/>.</param>
		/// <returns>Empty for a settlement whose notable is worth nothing to it, which is a
		/// sentence not worth writing.</returns>
		public static string ShadeClause(int Shade)
		{
			return (Shade <= 0) ? "" : ("  {{K|+" + Shade + " for what its notable finds here}}");
		}

	}
}
