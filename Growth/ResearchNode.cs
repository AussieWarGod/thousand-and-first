using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One authored research node. Prerequisites in the roster vocabulary the catalogue's
	/// <c>Knowledge</c> gates already speak, a tier expressed as an Intelligence threshold, and
	/// exactly one effect &mdash; mint these roster keys &mdash; so that every design gated on
	/// <c>Knowledge=</c>, ours and any third party's, is satisfied by research without one line of
	/// the gate machinery changing.
	/// </summary>
	public sealed class ResearchNode
	{
		/// <summary>Registry identity. Merge-by-key, like every other data lane.</summary>
		public string Key;

		/// <summary>What the founder calls it. Falls back to <see cref="Key"/>.</summary>
		public string DisplayName;

		/// <summary>Which spine it hangs on. A free string, so a mod adds a fourth.</summary>
		public string Branch;

		/// <summary>1 to 4. Each tier names an Intelligence threshold on the city's best
		/// researcher (<see cref="KingdomResearchRules.IntelligenceForTier"/>).</summary>
		public int Tier = 1;

		/// <summary>Roster tokens, ALL required, in <c>KingdomZoningRules.Knows</c>'s own grammar.</summary>
		public string Requires;

		/// <summary>Craft rung the city must have reached to work on it at all.</summary>
		public TechLevel MinTech = TechLevel.Hands;

		/// <summary>Roster keys minted on completion. Defaults to <c>node:&lt;Key&gt;</c>.</summary>
		public string Grants;

		/// <summary>Staff-days of thinking a fully-crewed lab takes.</summary>
		public int Effort = 1;

		/// <summary>Nodes made VISIBLE on completion.</summary>
		public string Reveals;

		/// <summary>Tokens that grant this node outright, with no lab time: a disk taught, a
		/// treatise read. Never a <c>rite:</c> token &mdash; that is a load-time schema error
		/// (Addendum 18).</summary>
		public string TaughtBy;

		/// <summary>Tokens that REVEAL this node and BEGIN it at a head start, never finish it.
		/// Every <c>rite:</c> token lives here and nowhere else.</summary>
		public string SeededBy;

		/// <summary>Creed / culture / species / genotype tokens that make this node invisible and
		/// unreachable, told to nobody.</summary>
		public string Forbidden;

		/// <summary>A vanilla quest, or <c>Quest~Step</c>, that must be finished before this node
		/// exists at all.</summary>
		public string Quest;

		/// <summary>Named non-key effects, as authored.</summary>
		public string Effect;

		/// <summary>The parsed <see cref="Effect"/>. Never null; empty for a node that names none.</summary>
		public List<ResearchEffect> Effects = new List<ResearchEffect>();

		/// <summary>What the founder calls it, with the key as the fallback so a half-authored node
		/// still reads as something.</summary>
		public string Named => string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;
	}
}
