#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSecondCityPublicationRulesTests
	{
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix +
			new string('a', KingdomIdentityRules.HashHexChars);
		private static readonly string First = KingdomIdentityRules.SettlementPrefix +
			new string('b', KingdomIdentityRules.HashHexChars);
		private static readonly string Second = KingdomIdentityRules.SettlementPrefix +
			new string('c', KingdomIdentityRules.HashHexChars);

		[Test]
		public void Prepare_IsDetachedAndCommitPublishesBothExactBooks()
		{
			KingdomTradeBook trade = Trade();
			KingdomCarryBook carry = Carry();
			KingdomTradeBook tradeRef = trade;
			KingdomCarryBook carryRef = carry;
			byte[] tradeBefore = KingdomTradeCodec.EncodeEnvelope(trade);
			byte[] carryBefore = CarryBytes(carry);

			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, trade, carry, out var plan, out var failure), failure);
			ClassicAssert.AreSame(tradeRef, trade);
			ClassicAssert.AreSame(carryRef, carry);
			CollectionAssert.AreEqual(tradeBefore, KingdomTradeCodec.EncodeEnvelope(trade));
			CollectionAssert.AreEqual(carryBefore, CarryBytes(carry));

			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure), failure);
			ClassicAssert.AreNotSame(tradeRef, trade);
			ClassicAssert.AreNotSame(carryRef, carry);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.ExactTopology(
				new[] { First, Second }, Realm, trade, carry));
		}

		[Test]
		public void ExactRetry_PreservesReferencesAndBytes()
		{
			KingdomTradeBook trade = Trade();
			KingdomCarryBook carry = Carry();
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, trade, carry, out var first, out var failure), failure);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(first,
				ref trade, ref carry, out failure), failure);
			KingdomTradeBook tradeRef = trade;
			KingdomCarryBook carryRef = carry;
			byte[] tradeBytes = KingdomTradeCodec.EncodeEnvelope(trade);
			byte[] carryBytes = CarryBytes(carry);

			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First, Second }, Second, trade, carry, out var retry, out failure),
				failure);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(retry,
				ref trade, ref carry, out failure), failure);
			ClassicAssert.AreSame(tradeRef, trade);
			ClassicAssert.AreSame(carryRef, carry);
			CollectionAssert.AreEqual(tradeBytes, KingdomTradeCodec.EncodeEnvelope(trade));
			CollectionAssert.AreEqual(carryBytes, CarryBytes(carry));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void HalfExpandedSaveCut_RecoversForwardWithoutReplacingExactBook(
			bool TradeWonCut)
		{
			KingdomTradeBook trade = Trade();
			KingdomCarryBook carry = Carry();
			string failure;
			if (TradeWonCut)
				ClassicAssert.IsTrue(KingdomTradeRules.ExpandExactIdentity(trade, Realm,
					new[] { First, Second }, out failure), failure);
			else
				ClassicAssert.IsTrue(KingdomLifecycleRules.ExpandCarryIdentity(carry, Realm,
					new[] { First, Second }, out failure), failure);
			KingdomTradeBook tradeRef = trade;
			KingdomCarryBook carryRef = carry;

			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, trade, carry, out var plan, out failure), failure);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure), failure);
			ClassicAssert.AreEqual(TradeWonCut, ReferenceEquals(tradeRef, trade));
			ClassicAssert.AreEqual(!TradeWonCut, ReferenceEquals(carryRef, carry));
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.ExactTopology(
				new[] { First, Second }, Realm, trade, carry));
		}

		[Test]
		public void OpenTrade_RefusesBeforeChangingEitherBook()
		{
			KingdomTradeBook tradeOpen = Trade();
			KingdomCarryBook carry = Carry();
			tradeOpen.OpenOperation = new KingdomTradeOperation();
			byte[] tradeOpenBefore = KingdomTradeCodec.EncodeEnvelope(tradeOpen);
			byte[] carryBefore = CarryBytes(carry);
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, tradeOpen, carry, out var ignored, out var failure));
			CollectionAssert.AreEqual(tradeOpenBefore,
				KingdomTradeCodec.EncodeEnvelope(tradeOpen));
			CollectionAssert.AreEqual(carryBefore, CarryBytes(carry));
		}

		[Test]
		public void AuthorityChangeAfterPrepare_RefusesCommitWithoutPartialSwap()
		{
			KingdomTradeBook trade = Trade();
			KingdomCarryBook carry = Carry();
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, trade, carry, out var plan, out var failure), failure);
			KingdomTradeBook tradeRef = trade;
			KingdomCarryBook carryRef = carry;
			trade.NextOperationSequence++;
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure));
			ClassicAssert.AreSame(tradeRef, trade);
			ClassicAssert.AreSame(carryRef, carry);
			ClassicAssert.AreEqual(2L, trade.NextOperationSequence);
			CollectionAssert.AreEqual(new[] { First }, carry.SettlementIds);
		}

		[Test]
		public void CutBoundaryMatrix_AllowsAbortOnlyBeforeExpansionAndSettleOnlyAfterPublication()
		{
			KingdomTradeBook trade = Trade();
			KingdomCarryBook carry = Carry();
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.CanAbort(
				new[] { First }, Second, Realm, trade, carry));
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.CanSettle(
				new[] { First }, Second, Realm, trade, carry));

			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, trade, carry, out var plan, out var failure), failure);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure), failure);
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.CanAbort(
				new[] { First }, Second, Realm, trade, carry));
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.CanSettle(
				new[] { First }, Second, Realm, trade, carry));
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.CanSettle(
				new[] { First, Second }, Second, Realm, trade, carry));
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.CanAbort(
				new[] { First, Second }, Second, Realm, trade, carry));
		}

		[Test]
		public void SamePlanRetry_IsIdempotentOnlyWhilePublishedTopologyRemainsExact()
		{
			KingdomTradeBook trade = Trade();
			KingdomCarryBook carry = Carry();
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryPrepare(Realm,
				new[] { First }, Second, trade, carry, out var plan, out var failure), failure);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure), failure);
			ClassicAssert.IsTrue(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure), failure);
			trade.SettlementIds = new List<string> { First };
			ClassicAssert.IsFalse(KingdomSecondCityPublicationRules.TryCommit(plan,
				ref trade, ref carry, out failure));
		}

		private static KingdomTradeBook Trade()
		{
			KingdomTradeBook book = new KingdomTradeBook();
			ClassicAssert.IsTrue(KingdomTradeRules.BindExactIdentity(book, Realm,
				new[] { First }, out var failure), failure);
			return book;
		}

		private static KingdomCarryBook Carry()
		{
			KingdomCarryBook book = new KingdomCarryBook();
			ClassicAssert.IsTrue(KingdomLifecycleRules.BindCarryIdentity(book, Realm,
				new[] { First }, LegacyMigration: false, MigrationKey: null));
			return book;
		}

		private static byte[] CarryBytes(KingdomCarryBook book)
		{
			using (var stream = new System.IO.MemoryStream())
			{
				using (var writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
					KingdomLifecycleWireCodec.WriteCarry(writer, book);
				return stream.ToArray();
			}
		}
	}
}
#endif
