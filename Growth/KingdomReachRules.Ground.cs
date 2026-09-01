using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomReachRules
	{
		// --- The quarter, measured ------------------------------------------------------------

		/// <summary>
		/// How near two pieces of built ground must be to read as one quarter. Six cells: wider
		/// than the lane <c>KingdomLayoutRules.CrowdPenalty</c> keeps between neighbours, narrower
		/// than the ring <c>KingdomLayoutRules.FieldRingCells</c> puts the fields out at, so a
		/// housing cluster holds together and the fields past it are their own ground.
		/// </summary>
		public const int QuarterLinkCells = 6;

		/// <summary>How far a first-tier work shades past the built ground of its own
		/// quarter.</summary>
		public const int QuarterBaseRadius = 4;

		/// <summary>How much further each tier above the first shades. This is the whole of
		/// "tier shifts within the band": the garth carries past the stone without either of them
		/// changing what band they are in.</summary>
		public const int QuarterRadiusPerTier = 2;

		/// <summary>The furthest any quarter-band work shades, however long its chain. A quarter
		/// that swallowed the zone would make the zone band mean nothing.</summary>
		public const int QuarterRadiusCap = 10;

		/// <summary>The shading radius a tier carries. Clamped at both ends.</summary>
		public static int QuarterRadius(int TierIndex)
		{
			int index = (TierIndex < 0) ? 0 : TierIndex;
			long radius = QuarterBaseRadius + ((long)QuarterRadiusPerTier * index);
			return (radius > QuarterRadiusCap) ? QuarterRadiusCap : (int)radius;
		}

		/// <summary>
		/// The cluster of built ground one work stands in: every mark reachable from the work by
		/// steps of at most <paramref name="LinkCells"/>, transitively. Single-linkage on the
		/// marks the layout grammar already reads, which is why a quarter needs no type, no
		/// field, and no save slot &mdash; it is whatever the city has built, measured now.
		/// </summary>
		/// <param name="Marks">Everything standing, from <c>KingdomLayout.ReadMarks</c>. Null
		/// reads as none.</param>
		/// <param name="X">The work's cell.</param>
		/// <param name="Y">The work's cell.</param>
		/// <param name="LinkCells">Chebyshev step that joins two marks. Below one, nothing links
		/// and every work is its own quarter.</param>
		/// <returns>Indices into <paramref name="Marks"/>, ascending. Empty when nothing is built
		/// within a step of the work, which is a work standing alone and is not a fault.</returns>
		public static List<int> QuarterMarks(IList<KingdomLayoutRules.LayoutMark> Marks, int X, int Y, int LinkCells)
		{
			List<int> cluster = new List<int>();
			if (Marks == null || Marks.Count == 0 || LinkCells < 1)
			{
				return cluster;
			}
			bool[] taken = new bool[Marks.Count];
			List<int> frontier = new List<int>();
			for (int i = 0; i < Marks.Count; i++)
			{
				if (KingdomLayoutRules.Chebyshev(Marks[i].X, Marks[i].Y, X, Y) <= LinkCells)
				{
					taken[i] = true;
					cluster.Add(i);
					frontier.Add(i);
				}
			}
			while (frontier.Count > 0)
			{
				int at = frontier[frontier.Count - 1];
				frontier.RemoveAt(frontier.Count - 1);
				for (int i = 0; i < Marks.Count; i++)
				{
					if (taken[i])
					{
						continue;
					}
					if (KingdomLayoutRules.Chebyshev(Marks[i].X, Marks[i].Y, Marks[at].X, Marks[at].Y) <= LinkCells)
					{
						taken[i] = true;
						cluster.Add(i);
						frontier.Add(i);
					}
				}
			}
			cluster.Sort();
			return cluster;
		}

		/// <summary>
		/// Whether a place stands in a work's quarter: within <paramref name="Radius"/> of the
		/// work itself, or of any built ground in the work's own cluster.
		/// </summary>
		/// <param name="Marks">Everything standing. Null reads as none, which leaves only the
		/// work's own radius.</param>
		/// <param name="WorkX">The work's cell.</param>
		/// <param name="WorkY">The work's cell.</param>
		/// <param name="AtX">The place being asked about.</param>
		/// <param name="AtY">The place being asked about.</param>
		/// <param name="LinkCells">See <see cref="QuarterMarks"/>.</param>
		/// <param name="Radius">How far past built ground the shading carries,
		/// <see cref="QuarterRadius"/>. Negative reads as zero.</param>
		public static bool InQuarter(IList<KingdomLayoutRules.LayoutMark> Marks, int WorkX, int WorkY, int AtX, int AtY, int LinkCells, int Radius)
		{
			int radius = (Radius < 0) ? 0 : Radius;
			if (KingdomLayoutRules.Chebyshev(WorkX, WorkY, AtX, AtY) <= radius)
			{
				return true;
			}
			List<int> cluster = QuarterMarks(Marks, WorkX, WorkY, LinkCells);
			for (int i = 0; i < cluster.Count; i++)
			{
				KingdomLayoutRules.LayoutMark mark = Marks[cluster[i]];
				if (KingdomLayoutRules.Chebyshev(mark.X, mark.Y, AtX, AtY) <= radius)
				{
					return true;
				}
			}
			return false;
		}

		// --- What scopes ----------------------------------------------------------------------

		/// <summary>
		/// Whether a support is one that reach scopes. The three binding goods are drawn and
		/// carried, so they stay citywide pools whatever building supplies them; everything else
		/// &mdash; including a good a third party invents, which
		/// <c>KingdomCatalogueRules.IsKnownSupport</c> already treats as a lift &mdash; shades
		/// only what its work reaches.
		/// </summary>
		public static bool ScopedByReach(string Kind)
		{
			return !KingdomCatalogueRules.IsBindingSupport(Kind);
		}

		/// <summary>
		/// How much of a work's declared amount actually lands, given how well the work is
		/// running. Reach decides where a lift goes; this decides how much of it there is, and it
		/// is the one place staffing and (later) damage compose &mdash; a work at half
		/// effectiveness shades half as hard, and is still a work.
		/// </summary>
		/// <param name="Amount">The declared amount. Negative reads as zero.</param>
		/// <param name="Percent">How well it runs, <c>KingdomRules.CrewEffectiveness</c>'s own
		/// scale. Negative reads as zero; above a hundred is kept, because a future bonus is not
		/// this function's to refuse.</param>
		/// <returns>Floored, never negative. A work running at all keeps at least one point of a
		/// lift it declares, so a shrine with one hand at it is never silently nothing.</returns>
		public static int Scaled(int Amount, int Percent)
		{
			if (Amount <= 0 || Percent <= 0)
			{
				return 0;
			}
			long scaled = (long)Amount * Percent / 100L;
			if (scaled >= int.MaxValue)
			{
				return int.MaxValue;
			}
			return (scaled < 1L) ? 1 : (int)scaled;
		}

		/// <summary>
		/// How much of one work's lift reaches the settlement's own level: the share of the homes
		/// it covers. This is the whole of Addendum 6's second clause on the level side &mdash;
		/// binding needs stay citywide pools, and a lift only lifts the people it actually
		/// reaches, so a shrine among the houses is worth its full amount and the same shrine out
		/// past the fields is worth what it touches.
		/// <para>
		/// Floored the way <see cref="Scaled"/> floors, and for the same reason: a work that
		/// reaches anybody at all keeps one point of what it declares. A work that reaches nobody
		/// keeps nothing here &mdash; it still shades the ground it stands on
		/// (<see cref="Character"/>), which is what an S plot was always for.
		/// </para>
		/// </summary>
		/// <param name="Amount">The work's declared lift, already scaled by how well it runs.
		/// Negative or zero lands nothing.</param>
		/// <param name="Reached">Homes this work covers. Clamped to <paramref name="Homes"/>, so a
		/// caller that double-counts a home cannot mint a lift.</param>
		/// <param name="Homes">Homes the settlement has. Zero or fewer lands nothing: a
		/// settlement with nowhere to live has nobody to lift, and its roof pool binds the level
		/// to the floor anyway.</param>
		public static int Landed(int Amount, int Reached, int Homes)
		{
			if (Amount <= 0 || Reached <= 0 || Homes <= 0)
			{
				return 0;
			}
			int reached = (Reached > Homes) ? Homes : Reached;
			int percent = (int)((long)reached * 100L / Homes);
			return Scaled(Amount, percent);
		}

		/// <summary>The order kinds are listed and ties are broken in: the catalogue's own
		/// lifting supports, in the order it declares them. A kind neither file names keeps its
		/// first-seen place after all of these.</summary>
		public static readonly string[] LiftOrder = KingdomCatalogueRules.LiftingSupports;

		/// <summary>
		/// Folds every lift reaching one piece of ground into what that ground is like: amounts
		/// summed per kind, listed in <see cref="LiftOrder"/> and then first-seen order, with the
		/// loudest kind named.
		/// </summary>
		/// <param name="Lifts">Every in-reach contribution, in any order, repeats allowed. Null
		/// reads as none. Binding supports among them are ignored rather than counted: they are
		/// citywide pools and have no business shading one quarter.</param>
		/// <returns>Never null. <see cref="GroundCharacter.Dominant"/> is null for ground nothing
		/// shades.</returns>
		public static GroundCharacter Character(IEnumerable<KindAmount> Lifts)
		{
			GroundCharacter character = new GroundCharacter();
			if (Lifts == null)
			{
				return character;
			}
			List<string> kinds = new List<string>();
			List<int> amounts = new List<int>();
			foreach (KindAmount lift in Lifts)
			{
				string kind = Fold(lift.Kind);
				if (kind == null || lift.Amount <= 0 || !ScopedByReach(kind))
				{
					continue;
				}
				int at = kinds.IndexOf(kind);
				if (at < 0)
				{
					kinds.Add(kind);
					amounts.Add(lift.Amount);
				}
				else
				{
					amounts[at] = KingdomCatalogueRules.SaturatingCounterAdd(
						amounts[at], lift.Amount);
				}
			}
			int[] order = new int[kinds.Count];
			for (int i = 0; i < kinds.Count; i++)
			{
				int rank = IndexIn(LiftOrder, kinds[i]);
				order[i] = (rank < 0)
					? KingdomCatalogueRules.SaturatingCounterAdd(LiftOrder.Length, i) : rank;
			}
			for (int i = 0; i < kinds.Count; i++)
			{
				int best = -1;
				for (int j = 0; j < kinds.Count; j++)
				{
					if (amounts[j] >= 0 && (best < 0 || order[j] < order[best]))
					{
						best = j;
					}
				}
				if (best < 0)
				{
					break;
				}
				character.Lifts.Add(new KindAmount(kinds[best], amounts[best]));
				character.Total = KingdomCatalogueRules.SaturatingCounterAdd(
					character.Total, amounts[best]);
				if (amounts[best] > character.DominantAmount)
				{
					character.Dominant = kinds[best];
					character.DominantAmount = amounts[best];
				}
				amounts[best] = -1;
			}
			return character;
		}

	}
}
