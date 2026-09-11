#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The retag peer resolved from the REAL delta, over the native geometry: a small layout whose
	/// retained core sits one cell in from the edge, expanded into a larger one where that core
	/// shifts (+1,+1) locally. That shift is what makes one slot NAME belong to two different
	/// components -- the predecessor at g:01:01 and, after the pairing, the successor partner of
	/// the predecessor at g:00:00 -- which is exactly the collision native 8 quarantined.
	/// <para>
	/// This case runs production's own delta builder and pairing
	/// (<c>KingdomArchitectureRules.TryBuildDelta</c>) rather than a hand-built tuple, then asks
	/// the census shard which placement may stand at the slot in each direction. The engine walk
	/// through the upgrade receipt itself needs a live game and remains owed to a native run.
	/// </para>
	/// </summary>
	public class KingdomArchitectureRetagPeerMappingTests
	{
		private static ArchitecturePlacement Ground(int X, int Y, string Blueprint)
		{
			return new ArchitecturePlacement
			{
				Layer = ArchitectureLayer.Ground, X = X, Y = Y, Blueprint = Blueprint,
				Slot = "g:" + X.ToString("D2") + ":" + Y.ToString("D2"),
				Material = "stone", MinTech = "hands"
			};
		}

		private static ArchitectureLayoutSnapshot Layout(int Width, int Height, int MainX,
			int MainY, ArchitectureLotSize Size, ArchitectureTransitionMode Mode,
			params ArchitecturePlacement[] Placements)
		{
			ArchitectureLayoutSnapshot snapshot = new ArchitectureLayoutSnapshot
			{
				PlanKey = "civic-heart", BindingKey = "heart", BuildKey = "heart",
				TierKey = "tier", VariantKey = "variant", PaletteKey = "civic-heart-stone",
				LotType = "civic", LotSize = Size, Facing = ArchitectureFacing.North,
				IncomingTransitionMode = Mode, Width = Width, Height = Height,
				MainX = MainX, MainY = MainY, FootprintX = 0, FootprintY = 0,
				FootprintWidth = Width, FootprintHeight = Height
			};
			for (int y = 0; y < Height; y++)
				for (int x = 0; x < Width; x++)
					snapshot.Cells.Add(new ArchitectureCellState
					{
						X = x, Y = y, Claim = ArchitectureClaim.Yard,
						Passability = ArchitecturePassability.Walkable,
						Cover = ArchitectureCover.Open
					});
			snapshot.Anchors.Add(new ArchitectureAnchor
			{
				Key = "main", X = MainX, Y = MainY, Access = ArchitectureAnchorAccess.OnCell
			});
			snapshot.Anchors.Add(new ArchitectureAnchor
			{
				Key = "entrance:public", X = 0, Y = Height - 1,
				Access = ArchitectureAnchorAccess.OnCell
			});
			foreach (ArchitecturePlacement placement in Placements)
				snapshot.Placements.Add(placement);
			return snapshot;
		}

		/// <summary>Before is the canonical Small 6x4 with a retained core at (0,0) and (1,1);
		/// After is the canonical Medium 8x6 with that core at (1,1) and (2,2) -- the local
		/// (+1,+1) shift an expanding tier gives, so a successor slot name is also a predecessor
		/// slot name of the other pair.</summary>
		private static ArchitectureLayoutDelta Delta()
		{
			ArchitectureLayoutSnapshot before = Layout(6, 4, 0, 0, ArchitectureLotSize.Small,
				ArchitectureTransitionMode.None,
				Ground(0, 0, "r_KingdomRitestone"), Ground(1, 1, "r_KingdomHearthstone"));
			ArchitectureLayoutSnapshot after = Layout(8, 6, 1, 1, ArchitectureLotSize.Medium,
				ArchitectureTransitionMode.RenovateExpand,
				Ground(1, 1, "r_KingdomRitestone"), Ground(2, 2, "r_KingdomHearthstone"),
				Ground(6, 4, "r_KingdomKerb"));
			ArchitectureLayoutDelta delta;
			string failure;
			ClassicAssert.IsTrue(
				KingdomArchitectureRules.TryBuildDelta(before, after, out delta, out failure),
				failure);
			return delta;
		}

		[Test]
		public void TheRealDeltaPairsTheShiftedCoreAndOneSlotNameNamesTwoGenerations()
		{
			ArchitectureLayoutDelta delta = Delta();
			ClassicAssert.AreEqual(2, delta.Retained.Count);
			ClassicAssert.AreEqual(delta.Retained.Count, delta.RetainedAfter.Count);
			Dictionary<string, string> pairs = new Dictionary<string, string>();
			for (int i = 0; i < delta.Retained.Count; i++)
				pairs[delta.Retained[i].Slot] = delta.RetainedAfter[i].Slot;
			// Every retained piece moves one cell in, so a successor slot name is also a
			// predecessor slot name belonging to a different pair.
			ClassicAssert.AreEqual("g:01:01", pairs["g:00:00"]);
			ClassicAssert.AreEqual("g:02:02", pairs["g:01:01"]);
			ClassicAssert.AreEqual(1, delta.Added.Count);
			ClassicAssert.AreEqual(0, delta.Removed.Count);
		}

		[Test]
		public void BothDirectionsResolveExactlyOnePeerAtTheSharedSlotName()
		{
			ArchitectureLayoutDelta delta = Delta();
			ArchitecturePlacement peer;
			ArchitecturePlacement retainKey;
			// Direction 1: settling the successor at g:01:01 (partner of predecessor g:00:00),
			// the peer is the predecessor component that still reads g:01:01.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerPlacement(
				delta, "g:01:01", true, out peer, out retainKey));
			ClassicAssert.AreEqual("g:01:01", peer.Slot);
			ClassicAssert.AreEqual(1, peer.X);
			ClassicAssert.AreEqual("g:01:01", retainKey.Slot);
			// Direction 2: settling the predecessor at g:01:01, the peer is the successor
			// component of the OTHER pair, which now reads that same name.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerPlacement(
				delta, "g:01:01", false, out peer, out retainKey));
			ClassicAssert.AreEqual("g:01:01", peer.Slot);
			ClassicAssert.AreEqual("g:00:00", retainKey.Slot);
			// A slot no retained pair carries has no peer at all, in either direction.
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.TryPeerPlacement(
				delta, "g:04:04", true, out peer, out retainKey));
			ClassicAssert.IsNull(peer);
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.TryPeerPlacement(
				null, "g:01:01", true, out peer, out retainKey));
		}

		[Test]
		public void TheSharedSlotSettlesOneOfEachAndRefusesAStranger()
		{
			ArchitectureLayoutDelta delta = Delta();
			ArchitecturePlacement peer;
			ArchitecturePlacement retainKey;
			KingdomArchitectureComponentCensusRules.TryPeerPlacement(delta, "g:01:01", true,
				out peer, out retainKey);
			// Tokens stand for the two generations' own component tokens; what matters here is
			// that the peer resolved above is proved by identity, cell and token together.
			int settled = KingdomArchitectureComponentCensusRules.Classify("after-token",
				"after-token", "mine", false, "peer-id", "before-token", true);
			int other = KingdomArchitectureComponentCensusRules.Classify("after-token",
				"before-token", "peer-id", true, "peer-id", "before-token", true);
			int stranger = KingdomArchitectureComponentCensusRules.Classify("after-token",
				"third-token", "stranger", false, "peer-id", "before-token", true);
			ClassicAssert.AreEqual(KingdomArchitectureComponentCensusRules.This, settled);
			ClassicAssert.AreEqual(KingdomArchitectureComponentCensusRules.Other, other);
			ClassicAssert.AreEqual(KingdomArchitectureComponentCensusRules.Foreign, stranger);
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.Settled(1, 1, 0));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settled(1, 1, 1));
		}
	}
}
#endif
