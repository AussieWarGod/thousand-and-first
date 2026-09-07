#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomResidentCarrierAbsenceRulesTests
	{
		private const int Target = 7;
		private const string Zone = "JoppaWorld.12.24.1.1.10";

		private static KingdomCityBook Book(char identity, params int[] residents)
		{
			KingdomResidentRow[] rows = residents.Select(id => new KingdomResidentRow(id,
				"Resident " + id, 0, 0, 100L, 0, 0, 0, KingdomDayShape.Hearth,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, Zone,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0)).ToArray();
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				KingdomIdentityRules.SettlementPrefix + new string(identity, 64), 100L,
				default(KingdomStocks), null, null, rows, null, out KingdomCityState state, out KingdomCityFault fault), fault.ToString());
			KingdomCityBook book = new KingdomCityBook();
			ClassicAssert.IsTrue(book.TryPublish(state, out fault), fault.ToString());
			return book;
		}

		private static KingdomBindingRegistry Registry(int key = 0, KingdomBindingKind kind = KingdomBindingKind.Resident)
		{
			KingdomBindingTable table = KingdomBindingTable.Empty;
			if (key != 0)
				ClassicAssert.IsTrue(table.TryBind(key, kind, Zone, "exact-body-" + key, 100L,
					out table, out KingdomCityFault fault), fault.ToString());
			KingdomBindingRegistry registry = new KingdomBindingRegistry();
			ClassicAssert.IsTrue(registry.TryPublish(table, out KingdomCityFault published), published.ToString());
			return registry;
		}

		// Snapshot raw carrier fields and exact list references before invoking the production
		// predicate. Reading a malformed carrier must not repair it into apparent absence.
		private sealed class FieldSnapshot
		{
			internal object Owner, Value;
			internal FieldInfo Field;
			internal object[] Items;
			internal void Check()
			{
				object current = Field.GetValue(Owner);
				if (Value == null || Field.FieldType.IsValueType || Value is string)
					ClassicAssert.AreEqual(Value, current, Field.Name);
				else ClassicAssert.AreSame(Value, current, Field.Name);
				if (Items != null) CollectionAssert.AreEqual(Items, ((IList)current).Cast<object>().ToArray(), Field.Name);
			}
		}

		private static void Check(bool expected, KingdomCityBook intended, int residentId,
			KingdomBindingRegistry registry, IReadOnlyList<KingdomCityBook> books, int expectedCount,
			KingdomCityBook exactReadBook = null, bool exactReadExpected = false)
		{
			List<object> carriers = new List<object> { intended, registry };
			KingdomCityBook[] priorBooks = books?.ToArray();
			if (books != null) carriers.AddRange(books);
			List<FieldSnapshot> before = new List<FieldSnapshot>();
			foreach (object carrier in carriers)
				if (carrier != null)
					foreach (FieldInfo field in carrier.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					{
						object value = field.GetValue(carrier);
						before.Add(new FieldSnapshot { Owner = carrier, Field = field, Value = value,
							Items = value is IList list ? list.Cast<object>().ToArray() : null });
					}
			for (int attempt = 0; attempt < 2; attempt++)
			{
				if (exactReadBook != null)
					ClassicAssert.AreEqual(exactReadExpected, exactReadBook.TryReadExact(
						out KingdomCityState _, out KingdomCityFault _));
				ClassicAssert.AreEqual(expected, KingdomResidentCarrierAbsenceRules.ProvesAbsent(
					intended, residentId, registry, books, expectedCount));
				foreach (FieldSnapshot snapshot in before) snapshot.Check();
				if (books != null)
				{
					ClassicAssert.AreEqual(priorBooks.Length, books.Count);
					for (int i = 0; i < priorBooks.Length; i++) ClassicAssert.AreSame(priorBooks[i], books[i]);
				}
			}
		}

		private static void CheckExactCity(bool expected, KingdomCityBook book)
		{
			KingdomCityBook intended = Book('a');
			Check(expected, intended, Target, Registry(), new[] { intended, book }, 2, book, expected);
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void HealthyEmptyOwnedBooksProveAbsenceWithoutMutation(int count)
		{
			KingdomCityBook intended = Book('a');
			List<KingdomCityBook> books = new List<KingdomCityBook> { intended };
			for (int i = 1; i < count; i++) books.Add(Book((char)('a' + i)));
			Check(true, intended, Target, Registry(), books, count);
		}

		[Test]
		public void OtherResidentRowsAndBindingsRemainUntouched()
		{
			KingdomCityBook book = Book('a', 8);
			Check(true, book, Target, Registry(8), new[] { book }, 1);
		}

		[TestCase(0)]
		[TestCase(1)]
		public void TargetRowInAnyOwnedBookRefusesAbsence(int position)
		{
			KingdomCityBook[] books = { Book('a'), Book('b') };
			books[position] = Book(position == 0 ? 'a' : 'b', Target);
			Check(false, books[0], Target, Registry(), books, 2);
		}

		[TestCase(7, KingdomBindingKind.Resident, false)]
		[TestCase(7, KingdomBindingKind.Transient, true)]
		[TestCase(8, KingdomBindingKind.Resident, true)]
		public void BindingAbsenceUsesExactResidentKeyAndKind(int key, KingdomBindingKind kind, bool expected)
		{
			KingdomCityBook book = Book('a');
			Check(expected, book, Target, Registry(key, kind), new[] { book }, 1);
		}

		[TestCase("ragged-target-tail")]
		[TestCase("null-column")]
		[TestCase("unknown-standing")]
		[TestCase("negative-clock")]
		[TestCase("roof-boolean-two")]
		[TestCase("creed-boolean-two")]
		public void MalformedCityCannotBecomeAbsentThroughReaderRepair(string corruption)
		{
			KingdomCityBook intended = Book('a');
			KingdomCityBook other = Book('b', corruption == "ragged-target-tail" ? Target : 8);
			if (corruption == "ragged-target-tail") other.ResidentNames.Clear();
			if (corruption == "null-column") other.ResidentIds = null;
			if (corruption == "unknown-standing") other.ResidentStandings[0] = 259;
			if (corruption == "negative-clock") other.ProcessedThroughTick = -1;
			if (corruption == "roof-boolean-two") other.ResidentRoofStanding[0] = 2;
			if (corruption == "creed-boolean-two") other.ResidentCreedStanding[0] = 2;
			Check(false, intended, Target, Registry(), new[] { intended, other }, 2);
		}

		[TestCase((int)KingdomResidentStanding.Resident, (int)KingdomStandingCause.Founder, false)]
		[TestCase((int)KingdomResidentStanding.Expedition, (int)KingdomStandingCause.Founder, false)]
		[TestCase((int)KingdomResidentStanding.Abroad, (int)KingdomStandingCause.None, false)]
		[TestCase((int)KingdomResidentStanding.Dead, (int)KingdomStandingCause.None, false)]
		[TestCase((int)KingdomResidentStanding.Resident, (int)KingdomStandingCause.None, true)]
		[TestCase((int)KingdomResidentStanding.Expedition, (int)KingdomStandingCause.None, true)]
		[TestCase((int)KingdomResidentStanding.Abroad, (int)KingdomStandingCause.Astray, true)]
		[TestCase((int)KingdomResidentStanding.Dead, (int)KingdomStandingCause.Founder, true)]
		public void ExactReaderPreservesOnlyCoherentStandingAndCause(int standing, int cause, bool expected)
		{
			KingdomCityBook book = Book('b', 8);
			book.ResidentStandings[0] = standing;
			book.ResidentCauses[0] = cause;
			CheckExactCity(expected, book);
		}

		[TestCase(int.MinValue)]
		[TestCase(-1)]
		[TestCase(0)]
		[TestCase(8)]
		public void InvalidOrDuplicateStoredResidentIdsRefuseEvenWhenTargetIsAbsent(int id)
		{
			KingdomCityBook book = Book('b', 8, 9);
			book.ResidentIds[1] = id;
			CheckExactCity(false, book);
		}

		[TestCase(nameof(KingdomCityBook.ResidentNames))]
		[TestCase(nameof(KingdomCityBook.ResidentOrigins))]
		[TestCase(nameof(KingdomCityBook.ResidentArrived))]
		[TestCase(nameof(KingdomCityBook.ResidentBoundZoneIds))]
		[TestCase(nameof(KingdomCityBook.ResidentCreedToward))]
		[TestCase(nameof(KingdomCityBook.ResidentKeptCreeds))]
		public void NullRawResidentTextCannotBeCoercedIntoAValidProjection(string column)
		{
			KingdomCityBook book = Book('b', 8);
			((IList)typeof(KingdomCityBook).GetField(column).GetValue(book))[0] = null;
			CheckExactCity(false, book);
		}

		[TestCase(nameof(KingdomCityBook.ResidentRoofTicks))]
		[TestCase(nameof(KingdomCityBook.ResidentRoofWarnedTicks))]
		[TestCase(nameof(KingdomCityBook.ResidentCreedTicks))]
		[TestCase(nameof(KingdomCityBook.ResidentCreedWarnedTicks))]
		public void NonstandingBrinkCannotDiscardRawTicksDuringProjection(string column)
		{
			KingdomCityBook book = Book('b', 8);
			((IList)typeof(KingdomCityBook).GetField(column).GetValue(book))[0] = 123L;
			CheckExactCity(false, book);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void NonstandingCreedCannotDiscardTargetOrChannel(bool target)
		{
			KingdomCityBook book = Book('b', 8);
			if (target) book.ResidentCreedToward[0] = "Mechanimists";
			else book.ResidentCreedChannels[0] = 1;
			CheckExactCity(false, book);
		}

		[Test]
		public void EmptyRawOptionalStringsKeepLegitimateNullProjectedValues()
		{
			KingdomCityBook book = Book('b', 8);
			book.ResidentNames[0] = ""; book.ResidentOrigins[0] = "";
			book.ResidentArrived[0] = ""; book.ResidentBoundZoneIds[0] = "";
			book.ResidentCreedToward[0] = ""; book.ResidentKeptCreeds[0] = "";
			CheckExactCity(true, book);
			ClassicAssert.IsTrue(book.TryReadExact(out KingdomCityState state, out KingdomCityFault _));
			ClassicAssert.IsTrue(state.TryResident(0, out KingdomResidentRow row));
			ClassicAssert.AreEqual("", row.Name); ClassicAssert.AreEqual("", row.Origin);
			ClassicAssert.AreEqual("", row.Arrived); ClassicAssert.AreEqual("", row.BoundZoneId);
			ClassicAssert.IsNull(row.CreedToward); ClassicAssert.IsNull(row.KeptCreeds);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void StandingUnwarnedBrinkPreservesItsReachedTick(bool creed)
		{
			KingdomCityBook book = Book('b', 8);
			if (creed)
			{
				book.ResidentCreedStanding[0] = 1; book.ResidentCreedTicks[0] = 100L;
				book.ResidentCreedWarnedTicks[0] = KingdomBrinkRules.Unwarned;
				book.ResidentCreedToward[0] = "Mechanimists"; book.ResidentCreedChannels[0] = 1;
			}
			else
			{
				book.ResidentRoofStanding[0] = 1; book.ResidentRoofTicks[0] = 100L;
				book.ResidentRoofWarnedTicks[0] = KingdomBrinkRules.Unwarned;
			}
			CheckExactCity(true, book);
			ClassicAssert.IsTrue(book.TryReadExact(out KingdomCityState state, out KingdomCityFault _));
			ClassicAssert.IsTrue(state.TryResident(0, out KingdomResidentRow row));
			KingdomBrinkWindow brink = creed ? row.CreedBrink : row.RoofBrink;
			ClassicAssert.IsTrue(brink.Stands); ClassicAssert.AreEqual(100L, brink.ReachedTick);
			ClassicAssert.AreEqual(KingdomBrinkRules.Unwarned, brink.WarnedTick);
			if (creed) { ClassicAssert.AreEqual("Mechanimists", row.CreedToward); ClassicAssert.AreEqual(1, row.CreedChannel); }
		}

		[TestCase("duplicate-reference")]
		[TestCase("duplicate-identity")]
		[TestCase("foreign-intended-reference")]
		[TestCase("null-member")]
		public void ExactOwnedTopologyCannotBeSubstitutedOrDuplicated(string corruption)
		{
			KingdomCityBook intended = Book('a');
			KingdomCityBook[] books = { intended, Book('b') };
			if (corruption == "duplicate-reference") books[1] = intended;
			if (corruption == "duplicate-identity") books[1] = Book('a');
			if (corruption == "foreign-intended-reference") intended = Book('a');
			if (corruption == "null-member") books[1] = null;
			Check(false, intended, Target, Registry(), books, 2);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("taf:city:not-an-owned-identity")]
		public void EveryOwnedBookNeedsValidSettlementIdentity(string identity)
		{
			KingdomCityBook intended = Book('a'), other = Book('b');
			other.SettlementId = identity;
			Check(false, intended, Target, Registry(), new[] { intended, other }, 2);
		}

		[TestCase(-1)]
		[TestCase(0)]
		[TestCase(2)]
		[TestCase(4)]
		public void InvalidOrOmittedOwnedBookCountCannotProveAbsence(int expectedCount)
		{
			KingdomCityBook book = Book('a');
			Check(false, book, Target, Registry(), new[] { book }, expectedCount);
		}

		[Test]
		public void MatchingCountAboveRealmCapStillRefuses()
		{
			KingdomCityBook[] books = Enumerable.Range(0, KingdomIdentityRules.MaxSettlements + 1)
				.Select(i => Book((char)('a' + i))).ToArray();
			Check(false, books[0], Target, Registry(), books, books.Length);
		}

		[TestCase(int.MinValue)]
		[TestCase(-1)]
		[TestCase(0)]
		public void NonpositiveResidentIdentityNeverProvesAbsence(int residentId)
		{
			KingdomCityBook book = Book('a');
			Check(false, book, residentId, Registry(), new[] { book }, 1);
		}

		[TestCase("intended")]
		[TestCase("registry")]
		[TestCase("books")]
		public void MissingAuthorityRefusesWithoutMutation(string missing)
		{
			KingdomCityBook book = Book('a');
			Check(false, missing == "intended" ? null : book, Target,
				missing == "registry" ? null : Registry(), missing == "books" ? null : new[] { book }, 1);
		}

		[TestCase("ragged")]
		[TestCase("null-column")]
		[TestCase("duplicate-key")]
		[TestCase("zero-key")]
		[TestCase("unknown-kind")]
		[TestCase("negative-tick")]
		public void MalformedRegistryCannotBeNormalizedIntoAbsence(string corruption)
		{
			KingdomCityBook book = Book('a');
			KingdomBindingRegistry registry = Registry(8);
			if (corruption == "ragged") { registry.Keys[0] = Target; registry.ObjectIds.Clear(); }
			if (corruption == "null-column") registry.Keys = null;
			if (corruption == "zero-key") registry.Keys[0] = 0;
			if (corruption == "unknown-kind") registry.Kinds[0] = 256;
			if (corruption == "negative-tick") registry.MintedTicks[0] = -1;
			if (corruption == "duplicate-key")
			{
				registry.Keys.Add(8); registry.Kinds.Add((int)KingdomBindingKind.Resident);
				registry.ZoneIds.Add(Zone); registry.ObjectIds.Add("foreign-duplicate"); registry.MintedTicks.Add(200L);
			}
			Check(false, book, Target, registry, new[] { book }, 1);
		}
	}
}
#endif
