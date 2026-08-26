using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomNetworkRules
	{
		/// <summary>
		/// Whether a network's graph must be rebuilt from the ground before it can be believed.
		/// <para>
		/// The only three things that may bump a ground stamp are the three
		/// <c>KingdomDistanceMatrix.MarkDirty</c> permits by the same argument: a conduit or node
		/// placed, one removed, one destroyed. <b>Never a clock and never a stock level.</b> A null
		/// graph always needs building, which is what makes "no graph yet" and "stale graph" the
		/// same caller-side branch instead of two.
		/// </para>
		/// </summary>
		internal static bool NeedsRebuild(KingdomNetworkGraph graph, long groundStamp)
		{
			return graph == null || graph.TopologyStamp != groundStamp;
		}

		/// <summary>
		/// The LIQUID LAW, as one total function.
		/// <para>
		/// BUILDING-CATALOGUE-BRIEF, 2026-08-22: <i>"connection is DECLARED, never inferred. (1)
		/// Typed lines — one liquid per network; a cross-liquid join REFUSES by name, never merges.
		/// (2) Explicit topology — segments join by declaration, not tile adjacency, so lines cross
		/// in one tile via crossover pieces without merging."</i>
		/// </para>
		/// <para>
		/// Note the shape of the first branch: an <b>undeclared</b> pair is
		/// <see cref="KingdomJoinVerdict.Crossed"/> and not a refusal, because two lines sharing a
		/// tile is an ordinary, legal, intended thing — it is what a crossover piece is for. A
		/// refusal is reserved for a join somebody actually asked for and cannot have, so that the
		/// telling means something when it fires.
		/// </para>
		/// </summary>
		/// <param name="declared">Whether both segments declared a join toward each other. Anything
		/// less than both is not a declaration.</param>
		internal static KingdomJoinVerdict JudgeJoin(bool declared, KingdomNetworkKind kindA, string liquidA, KingdomNetworkKind kindB, string liquidB)
		{
			if (!declared)
			{
				return KingdomJoinVerdict.Crossed;
			}
			if (kindA != kindB)
			{
				return KingdomJoinVerdict.RefusedKind;
			}
			if (kindA != KingdomNetworkKind.Liquid)
			{
				// Electrical, hydraulic, mechanical: mixing is not a concept in these families, so
				// a declared same-family join is simply a join. Vanilla's own adjacency stands
				// where it stands and we add nothing to it.
				return KingdomJoinVerdict.Joined;
			}
			if (string.IsNullOrEmpty(liquidA) || string.IsNullOrEmpty(liquidB))
			{
				return KingdomJoinVerdict.RefusedUntyped;
			}
			return LiquidsMatch(liquidA, liquidB) ? KingdomJoinVerdict.Joined : KingdomJoinVerdict.RefusedLiquid;
		}

		/// <summary>
		/// Whether two declared liquid names are the same liquid. Case-insensitive ordinal, because
		/// vanilla's liquid ids are plain lowercase strings — <c>"water"</c>, <c>"salt"</c>,
		/// <c>"blood"</c>, declared as <c>public new const string ID</c> on each
		/// <c>BaseLiquid</c> subclass (<c>D/XRL/Liquids/LiquidWater.cs:17</c> and siblings) — and a
		/// founder writing <c>Water</c> in XML meant water.
		/// <para>
		/// An empty name matches nothing, including another empty name. <i>Declared, never
		/// inferred</i>: two lines that both failed to say what they carry have not agreed on
		/// anything.
		/// </para>
		/// </summary>
		internal static bool LiquidsMatch(string liquidA, string liquidB)
		{
			if (string.IsNullOrEmpty(liquidA) || string.IsNullOrEmpty(liquidB))
			{
				return false;
			}
			return string.Equals(liquidA.Trim(), liquidB.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// The sentence a refused join is told with. <b>Named by liquid</b>, per the law: the
		/// founder is told which two lines would not meet, never that "a connection failed".
		/// <para>
		/// Empty for <see cref="KingdomJoinVerdict.Joined"/> and
		/// <see cref="KingdomJoinVerdict.Crossed"/>: nothing has gone wrong and 7b forbids telling
		/// somebody about the absence of a problem.
		/// </para>
		/// </summary>
		internal static string RefusalLine(KingdomJoinVerdict verdict, string liquidA, string liquidB)
		{
			switch (verdict)
			{
			case KingdomJoinVerdict.RefusedLiquid:
				return "The " + Named(liquidA) + " line will not join the " + Named(liquidB)
					+ " line. A line carries one liquid and only one. Lay a crossover if they are meant to pass.";
			case KingdomJoinVerdict.RefusedUntyped:
				return "That line has never been told what it carries, so it joins nothing. Say what runs in it first.";
			case KingdomJoinVerdict.RefusedKind:
				return "Those two do not belong to one another. A shaft carries no charge and a wire carries no water.";
			default:
				return "";
			}
		}

		/// <summary>A liquid's name as a founder would say it, or an honest word when the line
		/// never declared one.</summary>
		internal static string Named(string liquid)
		{
			return string.IsNullOrEmpty(liquid) ? "unnamed" : liquid.Trim().ToLowerInvariant();
		}

	}
}
