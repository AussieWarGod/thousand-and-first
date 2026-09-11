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
				BaseRoof = KingdomPlotRules.RoofState.Open,
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

		/// <summary>Both generations' real snapshots, so the walk hashes and tokenises exactly
		/// what the stamper would.</summary>
		private static ArchitectureLayoutSnapshot Before()
		{
			return Layout(6, 4, 0, 0, ArchitectureLotSize.Small,
				ArchitectureTransitionMode.None,
				Ground(0, 0, "r_KingdomRitestone"), Ground(1, 1, "r_KingdomHearthstone"));
		}

		private static ArchitectureLayoutSnapshot After()
		{
			return Layout(8, 6, 1, 1, ArchitectureLotSize.Medium,
				ArchitectureTransitionMode.RenovateExpand,
				Ground(1, 1, "r_KingdomRitestone"), Ground(2, 2, "r_KingdomHearthstone"),
				Ground(6, 4, "r_KingdomKerb"));
		}

		private static string Hash(ArchitectureLayoutSnapshot Snapshot)
		{
			string hash;
			string failure;
			ClassicAssert.IsTrue(
				KingdomArchitectureRules.TrySnapshotHash(Snapshot, out hash, out failure),
				failure);
			return hash;
		}

		private static ArchitecturePlacement At(ArchitectureLayoutSnapshot Snapshot, string Slot)
		{
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				if (Snapshot.Placements[i].Slot == Slot) return Snapshot.Placements[i];
			ClassicAssert.Fail("no placement at " + Slot);
			return null;
		}

		private static ArchitectureComponentCensusRow Row(string Slot, string Token, string Id,
			bool AtPeerCell)
		{
			return new ArchitectureComponentCensusRow(Lot, Slot, Token, Id, AtPeerCell);
		}

		private const string Lot = "lot-heart";

		/// <summary>
		/// The census the stamper runs at the shared slot name, over the real delta, the real
		/// snapshot hashes, the real component tokens and the real peer terms -- the same calls
		/// <c>ExactComponent</c> makes once the engine has read each candidate's lot, slot, token,
		/// identity and cell.
		/// <para>Both directions settle one of each generation, and every stranger shape refuses:
		/// a foreign token, the peer's token on the wrong cell, and the peer's token under another
		/// identity. What is NOT executed here is the engine half -- the survey iteration and
		/// property reads that build the rows, the by-reference cell compare,
		/// <c>TryWorldPlacement</c>, and the receipt state machine in <c>TryCarryUpgradeSlot</c>
		/// that chooses the direction -- because each needs a live GameObject and Zone. That
		/// remains owed to a native run.</para>
		/// </summary>
		[Test]
		public void TheSharedSlotCensusSettlesOneOfEachGenerationAndRefusesEveryStranger()
		{
			ArchitectureLayoutSnapshot before = Before();
			ArchitectureLayoutSnapshot after = After();
			string beforeHash = Hash(before);
			string afterHash = Hash(after);
			ArchitectureLayoutDelta delta = Delta();
			const string slot = "g:01:01";

			// Direction 1: settling the SUCCESSOR at g:01:01. The peer is the predecessor
			// component that has not been retagged yet and still reads that same name.
			ArchitecturePlacement peer;
			ArchitecturePlacement retainKey;
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerPlacement(
				delta, slot, true, out peer, out retainKey));
			string peerId;
			string peerToken;
			bool allowed;
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerTerms(Lot,
				beforeHash, peer, "peer-output-id", 1, true, out peerId, out peerToken,
				out allowed));
			ClassicAssert.IsTrue(allowed);
			string thisToken = KingdomArchitectureComponentCensusRules.ComponentTokenText(Lot,
				afterHash, At(after, slot));
			ClassicAssert.AreNotEqual(thisToken, peerToken);
			List<ArchitectureComponentCensusRow> rows = new List<ArchitectureComponentCensusRow>
			{
				Row(slot, thisToken, "settling-id", false),
				Row(slot, peerToken, peerId, true)
			};
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				thisToken, rows, peerId, peerToken, allowed));

			// A peer that has already settled on its successor may no longer stand here.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerTerms(Lot,
				beforeHash, peer, "peer-output-id", 2, true, out peerId, out peerToken,
				out allowed));
			ClassicAssert.IsFalse(allowed);
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				thisToken, rows, peerId, peerToken, allowed));

			// Direction 2: settling the PREDECESSOR at g:01:01 mid-pass. The peer is the
			// successor component of the other pair, already retagged onto this name.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerPlacement(
				delta, slot, false, out peer, out retainKey));
			ClassicAssert.AreEqual("g:00:00", retainKey.Slot);
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerTerms(Lot,
				afterHash, peer, "retagged-output-id", 1, false, out peerId, out peerToken,
				out allowed));
			ClassicAssert.IsTrue(allowed);
			string beforeToken = KingdomArchitectureComponentCensusRules.ComponentTokenText(Lot,
				beforeHash, At(before, slot));
			List<ArchitectureComponentCensusRow> backward =
				new List<ArchitectureComponentCensusRow>
			{
				Row(slot, beforeToken, "settling-id", false),
				Row(slot, peerToken, peerId, true)
			};
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				beforeToken, backward, peerId, peerToken, allowed));

			// An unpublished peer cannot already be wearing the successor name.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerTerms(Lot,
				afterHash, peer, "retagged-output-id", 0, false, out peerId, out peerToken,
				out allowed));
			ClassicAssert.IsFalse(allowed);
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				beforeToken, backward, peerId, peerToken, allowed));

			// Strangers at the shared name: a foreign token, the peer token on the wrong cell,
			// and the peer token under another identity. Each refuses the whole census.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.TryPeerTerms(Lot,
				beforeHash, At(before, "g:01:01"), "peer-output-id", 1, true, out peerId,
				out peerToken, out allowed));
			string foreign = KingdomArchitectureComponentCensusRules.ComponentTokenText(Lot,
				afterHash, At(after, "g:06:04"));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				thisToken, new List<ArchitectureComponentCensusRow>
				{
					Row(slot, thisToken, "settling-id", false),
					Row(slot, foreign, "stranger-id", false)
				}, peerId, peerToken, allowed));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				thisToken, new List<ArchitectureComponentCensusRow>
				{
					Row(slot, thisToken, "settling-id", false),
					Row(slot, peerToken, peerId, false)
				}, peerId, peerToken, allowed));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				thisToken, new List<ArchitectureComponentCensusRow>
				{
					Row(slot, thisToken, "settling-id", false),
					Row(slot, peerToken, "stranger-id", true)
				}, peerId, peerToken, allowed));
			// A candidate at another slot of the same lot is not this census's business.
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.Settles(Lot, slot,
				thisToken, new List<ArchitectureComponentCensusRow>
				{
					Row(slot, thisToken, "settling-id", false),
					Row("g:02:02", foreign, "elsewhere-id", false)
				}, peerId, peerToken, allowed));
		}

	}
}
#endif
