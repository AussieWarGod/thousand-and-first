#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartReservationStateTests
	{
		private const string Transaction = "0123456789abcdef0123456789abcdef";
		private const string Zone = "JoppaWorld.2.2.1.1.10";

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
		[TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
		[TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
		[TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
		[TestCase(16)] [TestCase(17)] [TestCase(18)] [TestCase(19)]
		[TestCase(20)] [TestCase(21)] [TestCase(22)] [TestCase(23)]
		[TestCase(24)] [TestCase(25)] [TestCase(26)] [TestCase(27)]
		[TestCase(28)] [TestCase(29)] [TestCase(30)] [TestCase(31)]
		public void EveryPresenceMaskDistinguishesCreationReuseAndRefusal(int mask)
		{
			KeyValuePair<string, KingdomDurableKeyObservation> row = Reservation("slot-0");
			string expected = row.Value.String;
			string foreign = Reservation("slot-1").Value.String;
			foreach (string raw in new[] { null, "", "malformed", expected, foreign })
			{
				KingdomDurableKeyObservation observed = Observation(mask, raw);
				Assert.AreEqual(mask == 0 || mask == 1 && raw == expected,
					KingdomFoundingHeartReservationState.TryExpected(row.Key, expected, observed, out bool absent));
				Assert.AreEqual(mask == 0, absent);
				Assert.AreEqual(raw, observed.String);
				Assert.AreEqual(mask, Mask(observed));
				Assert.AreEqual(73, observed.Int);
				Assert.AreEqual(mask == 1 && raw == expected,
					KingdomFoundingHeartReservationState.TryAudit(new[] { Pair(row.Key, observed) }, 1,
						out Dictionary<string, string> audited));
				if (mask == 1 && raw == expected)
				{ Assert.AreEqual(1, audited.Count); Assert.AreEqual(expected, audited[Id("slot-0")]); }
				else Assert.IsNull(audited);
				Assert.AreEqual(raw, observed.String);
				Assert.AreEqual(mask, Mask(observed));
			}
		}

		[TestCase(null)] [TestCase("")] [TestCase("malformed")]
		public void InvalidExpectedWireNeverAuthorizesEvenCompleteAbsence(string expected)
		{
			string key = Reservation("slot-0").Key;
			foreach (int mask in new[] { 0, 1 })
			{
				Assert.IsFalse(KingdomFoundingHeartReservationState.TryExpected(key, expected,
					Observation(mask, expected), out bool absent));
				Assert.IsFalse(absent);
			}
		}

		[TestCase(null)] [TestCase("")] [TestCase("unrelated")]
		public void InvalidExpectedKeyNeverAuthorizesEvenCompleteAbsence(string key)
		{
			Assert.IsFalse(KingdomFoundingHeartReservationState.TryExpected(key, Reservation("slot-0").Value.String,
				Observation(0, null), out bool absent));
			Assert.IsFalse(absent);
		}

		[Test]
		public void NullObservationAndForeignValidExpectedPairNeverBecomeAbsent()
		{
			KeyValuePair<string, KingdomDurableKeyObservation> row = Reservation("slot-0");
			Assert.IsFalse(KingdomFoundingHeartReservationState.TryExpected(row.Key, row.Value.String, null, out bool absent));
			Assert.IsFalse(absent);
			Assert.IsFalse(KingdomFoundingHeartReservationState.TryExpected(row.Key, Reservation("slot-1").Value.String,
				Observation(0, null), out absent));
			Assert.IsFalse(absent);
		}

		[Test]
		public void ValidButDifferentCommitmentCannotReuseAnOccupiedReservation()
		{
			KeyValuePair<string, KingdomDurableKeyObservation> row = Reservation("slot-0");
			string other = OtherSeal(row.Value.String);
			Assert.IsTrue(KingdomFoundingHeartReservationRules.TryRead(row.Key, other, out _, out _, out _));
			Assert.IsFalse(KingdomFoundingHeartReservationState.TryExpected(row.Key, row.Value.String,
				Observation(1, other), out bool absent));
			Assert.IsFalse(absent);
		}

		[Test]
		public void SevenReservationsAuditAsExactIndependentIdToRawDictionary()
		{
			List<KeyValuePair<string, KingdomDurableKeyObservation>> rows = Seven();
			rows.Reverse();
			Assert.IsTrue(KingdomFoundingHeartReservationState.TryAudit(rows, 7, out Dictionary<string, string> result));
			Assert.AreEqual(7, result.Count);
			foreach (KeyValuePair<string, KingdomDurableKeyObservation> row in rows)
				Assert.AreEqual(row.Value.String, result[row.Key.Substring(KingdomFoundingHeartReservationRules.Prefix.Length)]);
			string first = result[Id("final")];
			rows[0].Value.String = "mutated after observation";
			rows.Clear();
			Assert.AreEqual(first, result[Id("final")]);
			Assert.IsFalse(result.ContainsKey(Id("final").ToUpperInvariant()));
		}

		[TestCase(false)] [TestCase(true)]
		public void DuplicatePrefixKeyAndIdRefuseWithoutReturningPriorRows(bool changedWire)
		{
			List<KeyValuePair<string, KingdomDurableKeyObservation>> rows = Seven();
			KeyValuePair<string, KingdomDurableKeyObservation> repeated = rows[0];
			if (changedWire) repeated = Pair(repeated.Key, Observation(1, OtherSeal(repeated.Value.String)));
			rows.Add(repeated);
			Refuses(rows, 8);
			Assert.AreEqual(8, rows.Count);
		}

		[Test]
		public void UnrelatedKeysAreIgnoredButStillConsumeTheFiniteRowBudget()
		{
			List<KeyValuePair<string, KingdomDurableKeyObservation>> rows = Seven();
			rows.Insert(0, Pair("ordinary-int", Observation(2, null)));
			rows.Add(Pair("ordinary-empty", Observation(1, "")));
			rows.Add(Pair("r_TAF_FoundingHeartReserved", Observation(31, "not our prefix")));
			Assert.IsTrue(KingdomFoundingHeartReservationState.TryAudit(rows, 10, out Dictionary<string, string> result));
			Assert.AreEqual(7, result.Count);
			Refuses(rows, 9);
			Assert.AreEqual(10, rows.Count);
		}

		[TestCase(null)] [TestCase("")]
		public void InvalidKeysRefuseRatherThanDisappearingAsUnrelated(string key)
		{
			List<KeyValuePair<string, KingdomDurableKeyObservation>> rows = Seven();
			rows.Add(Pair(key, Observation(1, "unused")));
			Refuses(rows, 8);
		}

		[TestCase(false)] [TestCase(true)]
		public void NullObservationRefusesForReservationAndUnrelatedKeys(bool reservation)
		{
			Refuses(new[] { Pair(reservation ? Reservation("slot-0").Key : "ordinary", null) }, 1);
		}

		[TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)]
		public void NonpositiveBoundsRefuseBeforeEnumerating(int maximum)
		{
			ThrowingRows rows = new ThrowingRows("get", Reservation("slot-0"));
			Refuses(rows, maximum);
			Assert.AreEqual(0, rows.Starts);
		}

		[Test]
		public void NullSequenceRefusesWhileAnEmptyFiniteAuditSucceeds()
		{
			Refuses(null, 1);
			Assert.IsTrue(KingdomFoundingHeartReservationState.TryAudit(
				new KeyValuePair<string, KingdomDurableKeyObservation>[0], 1, out Dictionary<string, string> result));
			Assert.AreEqual(0, result.Count);
		}

		[TestCase("get")] [TestCase("null")] [TestCase("move")] [TestCase("current")] [TestCase("dispose")]
		public void EnumerationFailureCannotPublishPartialAudit(string phase)
		{
			ThrowingRows rows = new ThrowingRows(phase, Reservation("slot-0"));
			Refuses(rows, 8);
			Assert.AreEqual(1, rows.Starts);
			Assert.AreEqual(phase != "get" && phase != "null", rows.Disposed);
		}

		[Test]
		public void InfiniteUnrelatedSequenceStopsAtFirstExcessRow()
		{
			int read = 0;
			Refuses(Infinite(() => read++), 4);
			Assert.AreEqual(5, read);
		}

		private static IEnumerable<KeyValuePair<string, KingdomDurableKeyObservation>> Infinite(Action read)
		{
			while (true) { read(); yield return Pair("ordinary", Observation(2, null)); }
		}

		private static void Refuses(IEnumerable<KeyValuePair<string, KingdomDurableKeyObservation>> rows, int maximum)
		{
			Assert.IsFalse(KingdomFoundingHeartReservationState.TryAudit(rows, maximum, out Dictionary<string, string> result));
			Assert.IsNull(result);
		}

		private static List<KeyValuePair<string, KingdomDurableKeyObservation>> Seven()
		{
			List<KeyValuePair<string, KingdomDurableKeyObservation>> rows = new List<KeyValuePair<string, KingdomDurableKeyObservation>>();
			for (int i = 0; i < 6; i++) rows.Add(Reservation("slot-" + i));
			rows.Add(Reservation("final"));
			return rows;
		}

		private static KeyValuePair<string, KingdomDurableKeyObservation> Reservation(string role)
		{
			Assert.IsTrue(KingdomFoundingHeartStakeRules.TryCreate("heartbasin", "first basin",
				"r_KingdomPlotWorks", 38, 11, 42, 13, 0, true, false, null,
				"TAF_HeartBasinContents", 2, true, 3, false, 40, 11, false,
				out KingdomFoundingHeartStakeTruth truth));
			Assert.IsTrue(KingdomFoundingHeartRules.TryCreate(Transaction, Zone,
				40, 12, 30, 2, 49, 21, 38, 11, 42, 13, 900L, 600L,
				"p4,frozen-authored-payload", KingdomFoundingHeartStakeRules.Encode(truth), out KingdomFoundingHeartPlan plan));
			string wire = KingdomFoundingHeartReservationRules.Encode(plan, Id(role), role);
			Assert.IsNotNull(wire);
			return Pair(KingdomFoundingHeartReservationRules.Prefix + Id(role), Observation(1, wire));
		}

		private static string Id(string role) { return KingdomFoundingHeartRules.StableId(Transaction, Zone, role); }
		private static string OtherSeal(string wire) { return wire.Substring(0, wire.Length - 1) + (wire[wire.Length - 1] == '0' ? "1" : "0"); }
		private static KeyValuePair<string, KingdomDurableKeyObservation> Pair(string key, KingdomDurableKeyObservation observation)
		{ return new KeyValuePair<string, KingdomDurableKeyObservation>(key, observation); }
		private static KingdomDurableKeyObservation Observation(int mask, string wire)
		{
			return new KingdomDurableKeyObservation { HasString = (mask & 1) != 0, String = wire,
				HasInt = (mask & 2) != 0, Int = 73, HasInt64 = (mask & 4) != 0,
				HasObject = (mask & 8) != 0, HasBoolean = (mask & 16) != 0 };
		}
		private static int Mask(KingdomDurableKeyObservation row)
		{ return (row.HasString ? 1 : 0) | (row.HasInt ? 2 : 0) | (row.HasInt64 ? 4 : 0) | (row.HasObject ? 8 : 0) | (row.HasBoolean ? 16 : 0); }

		private sealed class ThrowingRows : IEnumerable<KeyValuePair<string, KingdomDurableKeyObservation>>,
			IEnumerator<KeyValuePair<string, KingdomDurableKeyObservation>>
		{
			private readonly string Phase;
			private readonly KeyValuePair<string, KingdomDurableKeyObservation> Row;
			private int Position;
			internal int Starts;
			internal bool Disposed;
			internal ThrowingRows(string phase, KeyValuePair<string, KingdomDurableKeyObservation> row)
			{ Phase = phase; Row = row; }
			public IEnumerator<KeyValuePair<string, KingdomDurableKeyObservation>> GetEnumerator()
			{
				Starts++;
				if (Phase == "get") throw new InvalidOperationException();
				return Phase == "null" ? null : this;
			}
			IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
			public bool MoveNext()
			{
				Position++;
				if (Phase == "move" && Position == 2) throw new InvalidOperationException();
				return Position <= (Phase == "dispose" ? 1 : 2);
			}
			public KeyValuePair<string, KingdomDurableKeyObservation> Current
			{
				get { if (Phase == "current" && Position == 2) throw new InvalidOperationException(); return Row; }
			}
			object IEnumerator.Current { get { return Current; } }
			public void Reset() { throw new NotSupportedException(); }
			public void Dispose() { Disposed = true; if (Phase == "dispose") throw new InvalidOperationException(); }
		}
	}
}
#endif
