using System;
using System.Collections.Generic;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomNetworks
	{
		/// <summary>
		/// The piece a declaration in <paramref name="direction"/> actually reaches: the immediate
		/// neighbour, or &mdash; through any run of crossovers &mdash; the first piece beyond them.
		/// <para>
		/// A crossover is transparent to the run that enters its paired face and opaque to
		/// everything else, which is exactly what <i>"lines cross in one tile without merging"</i>
		/// means. The walk is bounded by the zone's own width so a mis-declared ring of crossovers
		/// cannot spin.
		/// </para>
		/// </summary>
		private static int Through(Zone Z, Dictionary<int, int> atCell, List<GameObject> pieces, int x, int y, int direction)
		{
			int step = direction;
			int atX = x;
			int atY = y;
			for (int hops = 0; hops <= Z.Width; hops++)
			{
				switch (step)
				{
				case KingdomNetworkRules.JoinNorth:
					atY--;
					break;
				case KingdomNetworkRules.JoinSouth:
					atY++;
					break;
				case KingdomNetworkRules.JoinEast:
					atX++;
					break;
				default:
					atX--;
					break;
				}
				int index;
				if (atX < 0 || atY < 0 || atX >= Z.Width || atY >= Z.Height || !atCell.TryGetValue(atX * 1000 + atY, out index))
				{
					return -1;
				}
				r_KingdomLiquidCrossover crossing = pieces[index].GetPart<r_KingdomLiquidCrossover>();
				if (crossing == null)
				{
					return index;
				}
				// Arrived by the face opposite the way we were travelling; the piece carries us on
				// only if it pairs that face through.
				int exit = KingdomNetworkRules.CrossoverExit(crossing.PairMask, KingdomNetworkRules.OppositeJoin(step));
				if (exit == 0)
				{
					return -1;
				}
				step = KingdomNetworkRules.OppositeJoin(exit);
			}
			return -1;
		}

		/// <returns>Whether this refusal reached the founder's own register, so the caller can
		/// hold itself to one a composition.</returns>
		private static bool Refuse(GameObject piece, KingdomJoinVerdict verdict, string mine, string theirs, bool alreadySpoken)
		{
			string line = KingdomNetworkRules.RefusalLine(verdict, mine, theirs);
			if (string.IsNullOrEmpty(line))
			{
				return false;
			}
			// STANDARDS 7b, and the latch is on the PIECE rather than on the settlement: each
			// length of main remembers its own telling, so a dormant city keeps that memory with no
			// field on the system, and a founder who lays a second bad join is told about that one.
			r_KingdomLiquidConduit conduit = piece.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				if (conduit.RefusalAnnounced)
				{
					return false;
				}
				conduit.RefusalAnnounced = true;
			}
			else
			{
				r_KingdomLiquidTap tap = piece.GetPart<r_KingdomLiquidTap>();
				if (tap == null || tap.RefusalAnnounced)
				{
					return false;
				}
				tap.RefusalAnnounced = true;
			}
			// The log takes every one of them, because the log is for everything and the register
			// is for what the founder can still do something about (§3.1's own split).
			KingdomLog.Log("network: " + line);
			if (alreadySpoken)
			{
				return false;
			}
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system != null && system.Founded)
			{
				system.Ledger.Note("{{r|" + line + "}}");
			}
			return true;
		}

		private static string LiquidOf(GameObject piece)
		{
			r_KingdomLiquidConduit conduit = piece.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				return string.IsNullOrEmpty(conduit.Liquid) ? null : conduit.Liquid.Trim();
			}
			r_KingdomLiquidTap tap = piece.GetPart<r_KingdomLiquidTap>();
			if (tap != null)
			{
				return string.IsNullOrEmpty(tap.Liquid) ? null : tap.Liquid.Trim();
			}
			// A crossover types nothing, which is what makes it safe. It never begins a component
			// of its own; it is only ever walked through.
			return null;
		}

		private static int DeclarationOf(GameObject piece)
		{
			r_KingdomLiquidConduit conduit = piece.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				return conduit.JoinMask;
			}
			r_KingdomLiquidTap tap = piece.GetPart<r_KingdomLiquidTap>();
			return (tap != null) ? tap.JoinMask : 0;
		}

		/// <summary>What one segment will pass in a day, read off its own vessel: the hydraulic
		/// family's segment-volume idiom, turned into a rate. A pipe that holds eight drams passes
		/// eight drams a turn's worth of running, and a day is what the model counts in.</summary>
		private static int CapacityOf(GameObject piece)
		{
			LiquidVolume volume = piece.GetPart<LiquidVolume>();
			if (volume == null || volume.MaxVolume <= 0)
			{
				return 0;
			}
			long perDay = (long)volume.MaxVolume * KingdomRules.TicksPerDay / 100L;
			return (perDay > int.MaxValue) ? int.MaxValue : (int)perDay;
		}

		private static long Narrowest(int[] bottleneck, int count)
		{
			long narrowest = 0L;
			for (int i = 0; i < count; i++)
			{
				if (bottleneck[i] <= 0 || bottleneck[i] == KingdomNetworkRules.Unlimited)
				{
					continue;
				}
				if (narrowest == 0L || bottleneck[i] < narrowest)
				{
					narrowest = bottleneck[i];
				}
			}
			return narrowest;
		}

		private static int Find(int[] parent, int index)
		{
			while (parent[index] != index)
			{
				parent[index] = parent[parent[index]];
				index = parent[index];
			}
			return index;
		}

		private static void Union(int[] parent, int a, int b)
		{
			int rootA = Find(parent, a);
			int rootB = Find(parent, b);
			if (rootA == rootB)
			{
				return;
			}
			// Lower root wins, so the component's identity is a stored fact about the ground rather
			// than an artefact of which piece the walk happened to reach first.
			if (rootA < rootB)
			{
				parent[rootB] = rootA;
			}
			else
			{
				parent[rootA] = rootB;
			}
		}
	}
}
