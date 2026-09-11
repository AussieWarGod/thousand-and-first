using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>
		/// Stakes one rung of the heart on the surveyed ground. Used once at the founding, for the
		/// basin; every rung above it climbs through the ordinary improvement machinery instead,
		/// which is what makes the heart's gates the same gates every other design answers to.
		/// </summary>
		/// <returns>The works object, or null when the rung's design is missing, its ground will
		/// not take it, or the engine refuses the object.</returns>
		public static GameObject StakeHeartRung(KingdomSystem System, Zone Z, int Rung, KingdomPlotRules.PlotRect Survey, int RiteX, int RiteY)
		{
			if (Rung != 1 || Z == null
				|| !KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Z.Width, Z.Height,
					out KingdomPlotRules.PlotRect expected)
				|| !SameRect(expected, Survey)
				|| !EnsureFoundingHeartProjection(System, Z, RiteX, RiteY)) return null;
			GameObject exact = null;
			int count = 0;
			foreach (GameObject item in Z.GetObjects())
				if (GameObject.Validate(item) && item.GetIntProperty(HeartPlotProperty) == 1
					&& item.GetIntProperty(PlotPartProperty) != 1)
				{
					exact = item;
					count++;
				}
			return count == 1 ? exact : null;
		}

		private static GameObject StakeFirstHeartPrepared(KingdomSystem System, Zone Z,
			FoundingHeartContext Context, FoundingHeartPlacement Placement)
		{
			if (System == null || Z == null || Context == null || Placement?.Zone != Z
				|| !ReferenceEquals(Context, Placement.Context)
				|| Placement.Slot != KingdomFoundingHeartRules.WorksSlot)
				return HeartRefusedNull("stake: placement is not this zone's works slot");
			if (!FoundingHeartGroundAllows(Z, Context.Rect))
				return HeartRefusedNull("stake: liquid stands inside the rite ground");
			GroundGrid grid = new GroundGrid(Z);
			// The rite ground is not chosen by the plan; it is chosen by where the water was
			// poured, and the heart is laid on it whatever else is standing there. That is safe
			// because clearing a plot never takes down what the settlement may not take down --
			// ClearGround leaves every Held cell exactly as it found it -- and because the first
			// rung raises no wall at all. Anything inviolate simply stands inside the ring, and
			// the founder is told about it by name at the rung where it actually blocks something
			// (HeartGrowRefused).
			// Open water is the one exception, and it is fatal rather than awkward: a plot is
			// never laid over liquid and liquid is never filled in.
			KingdomConstructionJob founding = null;
			return Stake(System, Z, Context.Rect, Context.Entry, Context.Spec, grid, null,
				KingdomPlotRules.IsUnderground(Z.Z), Context.Architecture, false,
				ref founding, Placement);
		}

		// --- Siting -----------------------------------------------------------------------

		/// <summary>
		/// Finds the ground for one plot: every rect of the right tier that fits the zone's
		/// interior, keeps its lane from every plot already laid, and holds nothing the settlement
		/// may not take &mdash; scored by the settlement's own layout grammar
		/// (<see cref="KingdomPlotRules.ChooseRect"/>).
		/// <para>
		/// Never silent. When no rect survives, <paramref name="Refusal"/> is the sentence the
		/// founder reads, and it names the ground that came closest and the exact thing standing
		/// in it (STANDARDS 7b).
		/// </para>
		/// </summary>
		/// <param name="Z">The zone to build in.</param>
		/// <param name="System">The realm, for its claim and its stage.</param>
		/// <param name="Entry">The design being raised.</param>
		/// <param name="Spec">Its plot spec.</param>
		/// <param name="Grid">The zone's ground, read once.</param>
		/// <param name="Prefer">A cell the founder has already chosen &mdash; a staked plan's own
		/// stake &mdash; which is taken as the plot's centre and scored as the founder's ground.
		/// Null falls back to wherever the founder is standing.</param>
		/// <param name="Rect">The chosen rect, meaningful only when this returns true.</param>
		/// <param name="Outcome">What the plan did, for the message the founder reads.</param>
		/// <param name="Refusal">Null on success; a founder-facing sentence otherwise.</param>
		public static bool TryFindRect(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, GroundGrid Grid, Cell Prefer, out KingdomPlotRules.PlotRect Rect, out KingdomLayoutRules.LayoutOutcome Outcome, out string Refusal)
		{
			return TryFindRect(Z, System, Entry, Spec, (Spec == null) ? KingdomPlotRules.PlotSize.None : Spec.Size, Grid, Prefer, out Rect, out Outcome, out Refusal);
		}

		/// <summary>
		/// Finds the ground for one plot at a tier the founder chose, which is never smaller than
		/// the design's own but may be larger: staking wide is how a founder buys a building room
		/// to grow into and a yard to work in meanwhile. Otherwise identical to the overload above,
		/// which stakes exactly the ground the design asks for.
		/// </summary>
		/// <param name="Stake">The tier of plot to lay. <see cref="KingdomPlotRules.PlotSize.None"/>
		/// falls back to the design's own.</param>
		public static bool TryFindRect(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, KingdomPlotRules.PlotSize Stake, GroundGrid Grid, Cell Prefer, out KingdomPlotRules.PlotRect Rect, out KingdomLayoutRules.LayoutOutcome Outcome, out string Refusal)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Outcome = KingdomLayoutRules.LayoutOutcome.None;
			Refusal = null;
			if (Z == null || Entry == null || Spec == null || Grid == null)
			{
				Refusal = KingdomPlotRules.RefuseRoom((Spec == null) ? KingdomPlotRules.PlotSize.Small : Spec.Size);
				return false;
			}
			KingdomPlotRules.PlotSize staked = StakedSize(Spec, Stake);
			if (!KingdomPlotRules.TryInterior(Z.Width, Z.Height, out var interior)
				|| !KingdomPlotRules.TryDimensions(staked, out var plotWidth, out var plotHeight))
			{
				Refusal = KingdomPlotRules.RefuseRoom(staked);
				return false;
			}
			List<KingdomPlotRules.PlotRect> laid = ReadPlots(Z);
			List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
			bool hasRite = TryRiteGround(Z, out var riteX, out var riteY);
			Cell founderCell = Prefer ?? The.Player?.CurrentCell;
			bool hasFounder = founderCell != null && founderCell.ParentZone == Z;
			int founderX = hasFounder ? founderCell.X : 0;
			int founderY = hasFounder ? founderCell.Y : 0;
			List<KingdomPlotRules.PlotRect> groundCandidates =
				new List<KingdomPlotRules.PlotRect>();
			bool sawBlocked = false;
			KingdomPlotRules.PlotRect nearestBlocked = default(KingdomPlotRules.PlotRect);
			int nearestBlockedReach = 0;
			List<KingdomPlotPoseCandidate> posed = KingdomPlotPoseSitingRules.Enumerate(
				interior, plotWidth, plotHeight);
			for (int i = 0; i < posed.Count; i++)
			{
				KingdomPlotRules.PlotRect rect = posed[i].Rect;
				if (KingdomPlotRules.CrowdsExisting(rect, laid)) continue;
				if (Grid.AnyRefusal(rect))
				{
					int reach = hasFounder ? KingdomPlotRules.Reach(rect, founderX, founderY) : 0;
					if (!sawBlocked || KingdomPlotRules.Beats(0, reach, rect,
						0, nearestBlockedReach, nearestBlocked))
					{
						sawBlocked = true;
						nearestBlocked = rect;
						nearestBlockedReach = reach;
					}
					continue;
				}
				groundCandidates.Add(rect);
			}
			if (groundCandidates.Count == 0)
			{
				// The ground that came closest is the one the founder is told about: naming a
				// refusal on the far side of the zone would be true and useless.
				if (sawBlocked && Grid.TryFirstRefusal(nearestBlocked, out var blockX, out var blockY, out var blockKind, out var blocker))
				{
					Refusal = (blockKind == KingdomPlotRules.GroundKind.Liquid)
						? KingdomPlotRules.RefuseLiquid(blockX, blockY)
						: KingdomPlotRules.RefuseObstruction(blocker ?? "something", blockX, blockY);
				}
				else
				{
					Refusal = KingdomPlotRules.RefuseRoom(staked);
				}
				return false;
			}

			// ReadGround's own ground grid deliberately skips living occupants (a settler walks
			// off ground, KingdomPlot2.04.Ground.cs:19-24), so nothing above ever saw one. A
			// fixed founder or fauna body is gathered here, once -- not against the whole staked
			// rect (that over-rejects: 11.4% of authored lot cells are unclaimed margin, up to
			// 72% on some designs), but held for the architecture stage below, which filters
			// against exactly the cells the stamper itself manages
			// (Growth/KingdomArchitectureStamper.Preflight.cs:94-96 "a living occupant stands on
			// authored ground").
			HashSet<int> occupiedCells = new HashSet<int>();
			for (int y = interior.Y1; y <= interior.Y2; y++)
				for (int x = interior.X1; x <= interior.X2; x++)
				{
					Cell occupantCell = Z.GetCell(x, y);
					if (occupantCell == null) continue;
					List<GameObject> occupants = occupantCell.GetObjects();
					for (int k = 0; k < occupants.Count; k++)
					{
						GameObject occupant = occupants[k];
						if (GameObject.Validate(occupant) && (occupant.IsCreature || occupant.IsPlayer()))
						{ occupiedCells.Add(y * Z.Width + x); break; }
					}
				}

			// A rectangle is not buildable merely because its cells are clear. Resolve the exact
			// typed architecture and authored frontage against every pose before the layout grammar
			// scores anything. This keeps a valid transposed entrance from losing to a canonical
			// rectangle which the road cannot actually reach.
			KingdomArchitectureRuntime.SitingProbe probe;
			if (!KingdomArchitectureRuntime.TryCreateSitingProbe(System, Z,
				groundCandidates[0], Entry.Key, Entry.Category, out probe, out Refusal))
				return false;
			List<KingdomPlotRules.PlotRect> candidates;
			if (!KingdomPlotSelectionRules.TrySelect(groundCandidates,
				candidate => ResolveArchitecture(probe, candidate, Z, occupiedCells),
				hasFounder, founderX, founderY, out candidates, out Refusal))
			{
				return false;
			}
			KingdomLayoutRules.LayoutPurpose purpose = KingdomLayout.PurposeOfEntry(Entry);
			KingdomRules.Frontier edges = (System != null)
				? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
				: KingdomRules.Frontier.None;
			bool hasSurvey = TrySurveyedHeart(Z, out var survey) && KingdomPlotRules.HeartRungOf(Entry.Key) == 0;
			Outcome = KingdomPlotRules.ChooseRect(purpose, staked, Z.Width, Z.Height, edges, marks, candidates,
				hasFounder, founderX, founderY, hasRite, riteX, riteY, out var index, hasSurvey, survey, RiteWeight(Z));
			if (index < 0)
			{
				// The plan has nothing to say - empty ground, or a purpose it does not file. The
				// founder's own ground wins outright, exactly as it does everywhere else.
				index = NearestIndex(candidates, hasFounder, founderX, founderY);
				Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
			}
			Rect = candidates[index];
			return true;
		}

		/// <summary>The candidate nearest the founder, or the lowest-positioned one when the
		/// founder is elsewhere. Deterministic either way.</summary>
		public static int NearestIndex(IList<KingdomPlotRules.PlotRect> Candidates, bool HasFounder, int FounderX, int FounderY)
		{
			int best = -1;
			int bestReach = 0;
			for (int i = 0; i < Candidates.Count; i++)
			{
				int reach = HasFounder ? KingdomPlotRules.Reach(Candidates[i], FounderX, FounderY) : 0;
				if (best < 0 || KingdomPlotRules.Beats(0, reach, Candidates[i], 0, bestReach, Candidates[best]))
				{
					best = i;
					bestReach = reach;
				}
			}
			return best;
		}

		/// <summary>Architecture acceptance plus occupancy, filtered against exactly the cells
		/// the resolved snapshot manages (claimed cells + placements) -- the same set
		/// Growth/KingdomArchitectureStamper.Preflight.cs:90,94 inspects, via the same
		/// TryWorldCell/TryWorldPlacement coordinate mapping, never the whole staked rect. A
		/// mapping failure on either call REFUSES this candidate by that same failure text
		/// (never silently drops the cell/placement from the sweep): TryManagedCells
		/// (Growth/KingdomArchitectureStamper.OwnerReceipts.cs:140-168) treats the identical
		/// failure as fatal for Preflight, so quoting must refuse for the same reason or a
		/// candidate the stamper would refuse could still be selected here.</summary>
		private static KingdomPlotSelectionRules.Resolution ResolveArchitecture(
			KingdomArchitectureRuntime.SitingProbe Probe, KingdomPlotRules.PlotRect Candidate,
			Zone Z, HashSet<int> OccupiedCells)
		{
			if (!Probe.TryAccept(Candidate, out ArchitectureLayoutSnapshot accepted, out string failure))
				return new KingdomPlotSelectionRules.Resolution(false, failure);
			if (accepted != null && OccupiedCells.Count > 0)
			{
				for (int c = 0; c < accepted.Cells.Count; c++)
				{
					ArchitectureCellState cell = accepted.Cells[c];
					if (!KingdomArchitectureRules.IsClaimed(cell.Claim)) continue;
					if (!KingdomArchitectureRuntime.TryWorldCell(accepted, Candidate, cell,
						out int cx, out int cy, out string mappingFailure))
						return new KingdomPlotSelectionRules.Resolution(false, mappingFailure);
					if (OccupiedCells.Contains(cy * Z.Width + cx))
						return new KingdomPlotSelectionRules.Resolution(false,
							KingdomPlotRules.RefuseObstruction("a living occupant", cx, cy));
				}
				for (int p = 0; p < accepted.Placements.Count; p++)
				{
					if (!KingdomArchitectureRuntime.TryWorldPlacement(accepted, Candidate,
						accepted.Placements[p], out int px, out int py, out string mappingFailure))
						return new KingdomPlotSelectionRules.Resolution(false, mappingFailure);
					if (OccupiedCells.Contains(py * Z.Width + px))
						return new KingdomPlotSelectionRules.Resolution(false,
							KingdomPlotRules.RefuseObstruction("a living occupant", px, py));
				}
			}
			return new KingdomPlotSelectionRules.Resolution(true, null);
		}

	}
}
