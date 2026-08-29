using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRelocationRulesTests
	{
		[Test] public void LabourIsTimeAndHardnessBounded()
		{
			Assert.AreEqual(2000L, KingdomRelocationRules.LabourTicks(20, 5, 100, 1000));
			Assert.Greater(KingdomRelocationRules.LabourTicks(200, 50, 180, 1000), 2000L);
			Assert.AreEqual(0L, KingdomRelocationRules.LabourTicks(0, 5, 100, 1000));
		}

		[Test] public void ShiftPreservesWholeLotGeometry()
		{
			KingdomRelocationRect source = new KingdomRelocationRect(2, 3, 9, 8);
			KingdomRelocationRect moved = KingdomRelocationRules.Shift(source, 20, 4);
			Assert.AreEqual(source.Width, moved.Width); Assert.AreEqual(source.Height, moved.Height);
			Assert.AreEqual(22, moved.X1); Assert.AreEqual(12, moved.Y2);
			Assert.AreEqual(25, moved.CenterX); Assert.AreEqual(9, moved.CenterY);
		}

		[Test] public void DayCeilingCannotOverflow()
		{
			Assert.AreEqual(int.MaxValue, KingdomRelocationRules.Days(long.MaxValue, 2L));
			Assert.AreEqual(2, KingdomRelocationRules.Days(1001L, 1000L));
		}

		[Test] public void HappyReceiptRoundTripsCanonically()
		{
			KingdomRelocationReceipt receipt = Receipt();
			Assert.IsTrue(KingdomRelocationRules.Valid(receipt, out string failure), failure);
			Assert.IsTrue(KingdomRelocationCodec.TryEncode(receipt, out string first, out failure), failure);
			Assert.IsTrue(KingdomRelocationCodec.TryDecode(first, out var read, out failure), failure);
			Assert.IsTrue(KingdomRelocationCodec.TryEncode(read, out string second, out failure), failure);
			Assert.AreEqual(first, second); Assert.AreEqual("lot-1", read.Moves[0].PlotId);
		}

		[TestCase(KingdomRelocationMovePhase.Waiting)]
		[TestCase(KingdomRelocationMovePhase.Working)]
		[TestCase(KingdomRelocationMovePhase.Handover)]
		[TestCase(KingdomRelocationMovePhase.RollingBack)]
		[TestCase(KingdomRelocationMovePhase.RolledBack)]
		public void EveryInterruptionPhaseHasAValidDurableShape(KingdomRelocationMovePhase phase)
		{
			KingdomRelocationReceipt receipt = Receipt(); KingdomRelocationMove move = receipt.Moves[0];
			move.Phase = phase;
			if (phase == KingdomRelocationMovePhase.Working) move.RemainingTicks = 1500;
			if (phase == KingdomRelocationMovePhase.Handover
				|| phase == KingdomRelocationMovePhase.RollingBack)
			{ move.RemainingTicks = 0; move.CompletionTick = 3000; move.Rows[0].State = KingdomRelocationRowState.Rooted; }
			if (phase == KingdomRelocationMovePhase.RolledBack)
			{ move.RemainingTicks = 0; move.CompletionTick = 3000; }
			Assert.IsTrue(KingdomRelocationRules.Valid(receipt, out string failure), failure);
		}

		[Test] public void CompleteRequiresDestinationParity()
		{
			KingdomRelocationReceipt receipt = Receipt(); KingdomRelocationMove move = receipt.Moves[0];
			move.Phase = KingdomRelocationMovePhase.Complete; move.RemainingTicks = 0;
			move.CompletionTick = 3000;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out _));
			move.Rows[0].State = move.Rows[1].State = KingdomRelocationRowState.Destination;
			move.Clearance[0].State = KingdomRelocationClearState.Removed;
			receipt.CurrentMove = 1; receipt.Phase = KingdomRelocationPhase.Complete;
			Assert.IsTrue(KingdomRelocationRules.Valid(receipt, out string failure), failure);
		}

		[Test] public void PartialHandoverIsDurableButNotComplete()
		{
			KingdomRelocationReceipt receipt = Receipt(); KingdomRelocationMove move = receipt.Moves[0];
			move.Phase = KingdomRelocationMovePhase.Handover; move.RemainingTicks = 0;
			move.CompletionTick = 3000; move.Rows[0].State = KingdomRelocationRowState.Destination;
			move.Rows[1].State = KingdomRelocationRowState.Rooted;
			move.Clearance[0].State = KingdomRelocationClearState.RemovalPending;
			Assert.IsTrue(KingdomRelocationRules.Valid(receipt, out string failure), failure);
			move.Phase = KingdomRelocationMovePhase.Complete;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out _));
		}

		[Test] public void DuplicateObjectIdsFailClosed()
		{
			KingdomRelocationReceipt receipt = Receipt();
			receipt.Moves[0].Rows[1].ObjectId = receipt.Moves[0].Rows[0].ObjectId;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string failure));
			StringAssert.Contains("duplicated", failure);
		}

		[Test] public void HeartAndMovingFabricCannotSharePhysicalIdentity()
		{
			KingdomRelocationReceipt receipt = Receipt();
			receipt.Moves[0].Rows[0].ObjectId = receipt.HeartId;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string failure));
			StringAssert.Contains("duplicated", failure);
		}

		[Test] public void DuplicateLotIdsFailClosed()
		{
			KingdomRelocationReceipt receipt = Receipt();
			KingdomRelocationMove second = Move("root-2", "lot-1", 45, 2);
			receipt.Moves.Add(second);
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out _));
		}

		[Test] public void OverlappingDestinationsFailClosed()
		{
			KingdomRelocationReceipt receipt = Receipt();
			KingdomRelocationMove second = Move("root-2", "lot-2", 42, 2);
			receipt.Moves.Add(second);
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string failure));
			StringAssert.Contains("overlap", failure);
		}

		[Test] public void OverlappingSourcesFailClosed()
		{
			KingdomRelocationReceipt receipt = Receipt();
			KingdomRelocationMove second = Move("root-2", "lot-2", 55, 2);
			receipt.Moves.Add(second);
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string failure));
			StringAssert.Contains("sources overlap", failure);
		}

		[Test] public void HeartGroundCanNeverBeDestination()
		{
			KingdomRelocationReceipt receipt = Receipt();
			receipt.Moves[0].Destination = receipt.HeartGround;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out _));
		}

		[Test] public void SourceAndFutureSourceCollisionsFailClosed()
		{
			KingdomRelocationReceipt receipt = Receipt();
			receipt.Moves[0].Destination = new KingdomRelocationRect(14, 8, 21, 13);
			receipt.Moves[0].Clearance[0].X = 14;
			receipt.Moves[0].Clearance[0].Y = 8;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string overlap));
			StringAssert.Contains("source overlaps", overlap);
			receipt = Receipt();
			KingdomRelocationMove later = Move("root-2", "lot-2", 55, 2);
			later.Source = new KingdomRelocationRect(42, 2, 49, 7);
			later.Footprint = new KingdomRelocationRect(43, 3, 48, 6);
			receipt.Moves.Add(later);
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string future));
			StringAssert.Contains("later source", future);
		}

		[Test] public void ActiveReceiptAlwaysOwnsExactlyOneCurrentMove()
		{
			KingdomRelocationReceipt receipt = Receipt(); KingdomRelocationMove move = receipt.Moves[0];
			move.Phase = KingdomRelocationMovePhase.Complete; move.RemainingTicks = 0;
			move.CompletionTick = 3000;
			move.Rows[0].State = move.Rows[1].State = KingdomRelocationRowState.Destination;
			move.Clearance[0].State = KingdomRelocationClearState.Removed;
			receipt.CurrentMove = 1;
			Assert.IsFalse(KingdomRelocationRules.Valid(receipt, out string failure));
			StringAssert.Contains("no current move", failure);
		}

		[Test] public void ArchitectureAuthorityRoundTripsWithoutReresolution()
		{
			KingdomRelocationReceipt receipt = Receipt();
			receipt.Moves[0].Architecture = new KingdomRelocationArchitecture
			{
				Schema = 1, BuildKey = "house", PlanKey = "plan", BindingKey = "binding",
				TierKey = "tier", VariantKey = "variant", PaletteKey = "palette",
				LotType = "housing", LotSize = 2, Facing = 1, Snapshot = "snapshot",
				Hash = new string('a', 64), MainX = 15, MainY = 9
			};
			Assert.IsTrue(KingdomRelocationCodec.TryEncode(receipt, out string encoded,
				out string failure), failure);
			Assert.IsTrue(KingdomRelocationCodec.TryDecode(encoded, out var read, out failure), failure);
			Assert.AreEqual("snapshot", read.Moves[0].Architecture.Snapshot);
			Assert.AreEqual(new string('a', 64), read.Moves[0].Architecture.Hash);
		}

		[Test] public void MalformedAndFutureCodecsFailClosed()
		{
			Assert.IsFalse(KingdomRelocationCodec.TryDecode("not base64", out _, out _));
			Assert.IsTrue(KingdomRelocationCodec.TryEncode(Receipt(), out string encoded, out _));
			byte[] bytes = Convert.FromBase64String(encoded); bytes[4] = 99;
			Assert.IsFalse(KingdomRelocationCodec.TryDecode(Convert.ToBase64String(bytes), out _, out string failure));
			StringAssert.Contains("schema", failure);
		}

		[Test] public void TrailingBytesAndOversizeTextFailClosed()
		{
			Assert.IsTrue(KingdomRelocationCodec.TryEncode(Receipt(), out string encoded, out _));
			byte[] bytes = Convert.FromBase64String(encoded), extra = new byte[bytes.Length + 1];
			Array.Copy(bytes, extra, bytes.Length);
			Assert.IsFalse(KingdomRelocationCodec.TryDecode(Convert.ToBase64String(extra), out _, out _));
			KingdomRelocationReceipt receipt = Receipt();
			receipt.Moves[0].DisplayName = new string('x', KingdomRelocationRules.MaxNameChars + 1);
			Assert.IsFalse(KingdomRelocationCodec.TryEncode(receipt, out _, out _));
			receipt = Receipt(); receipt.Moves[0].Rows[0].OffsetX = -1;
			Assert.IsFalse(KingdomRelocationCodec.TryEncode(receipt, out _, out _));
			receipt = Receipt(); receipt.Moves[0].Destination = new KingdomRelocationRect(
				KingdomRelocationRules.MaxCoordinate + 1, 2,
				KingdomRelocationRules.MaxCoordinate + 8, 7);
			Assert.IsFalse(KingdomRelocationCodec.TryEncode(receipt, out _, out _));
		}

		private static KingdomRelocationReceipt Receipt()
		{
			return new KingdomRelocationReceipt { Schema = 1, PlanId = "plan-1", ZoneId = "Joppa.1.1.1.1.10",
				RealmId = "realm-1", HeartId = "heart-1", SuccessorKey = "heart-hall",
				HeartGround = new KingdomRelocationRect(10, 5, 29, 18), CreatedTick = 1000,
				Generation = 1, CurrentMove = 0, Phase = KingdomRelocationPhase.Active,
				Moves = new List<KingdomRelocationMove> { Move("root-1", "lot-1", 40, 2) } };
		}

		private static KingdomRelocationMove Move(string root, string lot, int x, int y)
		{
			return new KingdomRelocationMove { RootId = root, PlotId = lot, BuildKey = "house",
				DisplayName = "house", Source = new KingdomRelocationRect(12, 7, 19, 12),
				Destination = new KingdomRelocationRect(x, y, x + 7, y + 5),
				Footprint = new KingdomRelocationRect(13, 8, 18, 11), Roof = 2,
				StartedTick = 1000, LastTick = 1000, RequiredTicks = 2000,
				RemainingTicks = 2000, Phase = KingdomRelocationMovePhase.Waiting,
				FrameId = "frame-" + root, StakeIds = new[] { "a-" + root, "b-" + root,
					"c-" + root, "d-" + root }, Rows = new List<KingdomRelocationRow>
				{
					new KingdomRelocationRow { ObjectId = "wall-" + root, Blueprint = "Wall", OffsetX = 0, OffsetY = 0 },
					new KingdomRelocationRow { ObjectId = root, Blueprint = "House", OffsetX = 3, OffsetY = 2, Root = true }
				}, Clearance = new List<KingdomRelocationClearRow>
				{
					new KingdomRelocationClearRow { ObjectId = "tree-" + root, Blueprint = "Tree", X = x, Y = y }
				} };
		}
	}
}
