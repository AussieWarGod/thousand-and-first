#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;
using RoofRow = ThousandAndFirst.Simulation.City.KingdomCityBook.SubsidenceRoofRow;

namespace ThousandAndFirst.Tests
{
	// Executes real carrier storage. Parent effect proofs supply model premises, not native custody evidence.
	public sealed class KingdomSubsidenceRoofCarrierTests
	{
		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void ExplicitUnadmittedStorageCanBeCapturedButCannotAuthorizeRoofPublication(string wire)
		{
			KingdomCityBook city = Carrier();
			city.SubsidenceModel = wire;
			Snapshot before = new Snapshot(city);
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow row));
			ClassicAssert.AreSame(city, row.City);
			ClassicAssert.AreEqual(city.SettlementId, row.SettlementId);
			ClassicAssert.AreEqual(11, row.ResidentId);
			ClassicAssert.AreEqual(city.ResidentHomeWorkIds[0], row.HomeWorkId);
			ClassicAssert.AreEqual(city.ResidentStandings[0], row.Standing);
			ClassicAssert.AreEqual(RungFixture.Zone, row.ZoneId);
			ClassicAssert.IsFalse(row.RoofStanding);
			ClassicAssert.AreEqual(0, row.Reached); ClassicAssert.AreEqual(0, row.Warned);
			ClassicAssert.IsFalse(city.TryPublishSubsidenceRoof(wire, row, true, RungFixture.Due, 0, out RoofRow after));
			ClassicAssert.IsNull(after);
			before.Unchanged(city);
		}

		[TestCase(false)] [TestCase(true)]
		public void ExactIntentPublishesOnlyPlannedTupleKeepingEveryOtherFieldAndRawListReference(bool alreadyStanding)
		{
			KingdomCityBook city = Bound("roof-intent", alreadyStanding);
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			Snapshot before = new Snapshot(city);
			long reached = alreadyStanding ? RungFixture.Due + 10 : RungFixture.Due;
			long warned = alreadyStanding ? RungFixture.Due + 20 : 0;
			ClassicAssert.IsTrue(city.TryPublishSubsidenceRoof(city.SubsidenceModel, prior,
				true, reached, warned, out RoofRow after));
			ClassicAssert.IsTrue(prior.SameCarriers(after));
			ClassicAssert.AreSame(prior.Ids, after.Ids); ClassicAssert.AreSame(prior.Homes, after.Homes);
			ClassicAssert.AreSame(prior.Standings, after.Standings); ClassicAssert.AreSame(prior.Zones, after.Zones);
			ClassicAssert.AreSame(prior.Roofs, after.Roofs);
			ClassicAssert.AreSame(prior.ReachedTicks, after.ReachedTicks);
			ClassicAssert.AreSame(prior.WarnedTicks, after.WarnedTicks);
			before.Unchanged(city, "ResidentRoofStanding", "ResidentRoofTicks", "ResidentRoofWarnedTicks");
			ClassicAssert.IsTrue(after.RoofStanding);
			ClassicAssert.AreEqual(reached, after.Reached); ClassicAssert.AreEqual(warned, after.Warned);
			CollectionAssert.AreEqual(new[] { 1, 0, 0 }, city.ResidentRoofStanding);
			CollectionAssert.AreEqual(new[] { reached, 0L, 0L }, city.ResidentRoofTicks);
			CollectionAssert.AreEqual(new[] { warned, 0L, 0L }, city.ResidentRoofWarnedTicks);
			ClassicAssert.AreEqual(alreadyStanding, prior.RoofStanding, "snapshot scalar must not follow the live list");
			ClassicAssert.AreEqual(alreadyStanding ? RungFixture.Due + 10 : 0, prior.Reached);
			ClassicAssert.AreEqual(alreadyStanding ? RungFixture.Due + 20 : 0, prior.Warned);
		}

		[Test]
		public void StaleBeforeSnapshotRefusesButFreshAfterSnapshotCanConfirmWithoutChangingBytes()
		{
			KingdomCityBook city = Bound("roof-intent");
			string wire = city.SubsidenceModel;
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			ClassicAssert.IsTrue(city.TryPublishSubsidenceRoof(wire, prior, true, RungFixture.Due, 0, out RoofRow after));
			Snapshot completed = new Snapshot(city);
			ClassicAssert.IsFalse(city.TryPublishSubsidenceRoof(wire, prior, true, RungFixture.Due, 0, out RoofRow refused));
			ClassicAssert.IsNull(refused);
			completed.Unchanged(city);
			ClassicAssert.IsTrue(city.TryPublishSubsidenceRoof(wire, after, true, RungFixture.Due, 0, out RoofRow confirmed));
			ClassicAssert.IsTrue(after.SameCarriers(confirmed));
			completed.Unchanged(city);
		}

		[Test]
		public void ProvedParentAcceptsOnlyMeasuredAfterTuple()
		{
			KingdomCityBook city = Bound("roof-proved");
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow before));
			Refuses(city, city.SubsidenceModel, before, true, RungFixture.Due, 0);
			city.ResidentRoofStanding[0] = 1;
			city.ResidentRoofTicks[0] = RungFixture.Due;
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow measured));
			Snapshot snapshot = new Snapshot(city);
			ClassicAssert.IsTrue(city.TryPublishSubsidenceRoof(city.SubsidenceModel, measured,
				true, RungFixture.Due, 0, out RoofRow after));
			ClassicAssert.IsTrue(measured.SameCarriers(after));
			snapshot.Unchanged(city);
		}

		[TestCase("unplanned")] [TestCase("prepared")] [TestCase("wear-intent")] [TestCase("wear-proved")]
		public void ParentWithoutCurrentRoofIntentCannotBypassCompetingWriterFence(string phase)
		{
			KingdomCityBook city = Bound(phase);
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due, 0);
		}

		[TestCase(12)] [TestCase(13)]
		public void LaterFrontierOrUnselectedResidentCannotBorrowAnotherResidentsRoofIntent(int id)
		{
			KingdomCityBook city = Bound("roof-intent");
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(id, out RoofRow prior));
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due, 0);
		}

		[TestCase(true, -1L, 0L)] [TestCase(true, 5300L, -1L)]
		[TestCase(false, 1L, 0L)] [TestCase(false, 0L, 1L)] [TestCase(false, 0L, 0L)]
		[TestCase(true, 5301L, 0L)] [TestCase(true, 5300L, 1L)]
		public void InvalidOrUnplannedTargetTupleRefusesWithoutAnyWrite(bool stands, long reached, long warned)
		{
			KingdomCityBook city = Bound("roof-intent");
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			Refuses(city, city.SubsidenceModel, prior, stands, reached, warned);
		}

		[TestCase("null-prior")] [TestCase("foreign-city")] [TestCase("wrong-wire")]
		[TestCase("replaced-parent")] [TestCase("changed-city-id")] [TestCase("foreign-parent-owner")]
		public void ParentBytesCityIdentityAndSnapshotOriginAreExact(string change)
		{
			KingdomCityBook city = Bound("roof-intent");
			string expected = city.SubsidenceModel;
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			if (change == "null-prior") prior = null;
			if (change == "foreign-city")
			{
				KingdomCityBook foreign = Bound("roof-intent");
				ClassicAssert.AreEqual(city.SubsidenceModel, foreign.SubsidenceModel);
				ClassicAssert.IsTrue(foreign.TryCaptureSubsidenceRoof(11, out prior));
			}
			if (change == "wrong-wire") expected = Wire(Parent("prepared"));
			if (change == "replaced-parent") city.SubsidenceModel = Wire(Parent("prepared"));
			if (change == "changed-city-id" || change == "foreign-parent-owner")
			{
				city.SettlementId = KingdomIdentityRules.SettlementPrefix + new string('d', 64);
				ClassicAssert.AreNotEqual(city.SettlementId, prior.SettlementId);
				ClassicAssert.IsFalse(city.TryCaptureSubsidenceRoof(11, out RoofRow renamed));
				ClassicAssert.IsNull(renamed, "foreign parent must fail before a fresh capture");
				if (change == "foreign-parent-owner") prior = new RoofRow(city, 0);
			}
			Refuses(city, expected, prior, true, RungFixture.Due, 0);
		}

		[TestCase("home")] [TestCase("zone")] [TestCase("standing")]
		public void FreshCarrierCannotSubstituteAnotherResidenceForTheFrozenRecipient(string change)
		{
			KingdomCityBook city = Bound("roof-intent");
			if (change == "home") city.ResidentHomeWorkIds[0]++;
			if (change == "zone") city.ResidentBoundZoneIds[0] = "another.zone";
			if (change == "standing") city.ResidentStandings[0] = (int)KingdomResidentStanding.Expedition;
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due, 0);
		}

		[Test]
		public void QuarantinedParentCannotPublishItsOtherwiseArmedRoof()
		{
			KingdomCityBook city = Carrier();
			KingdomSubsidenceStepBook book = Parent("roof-intent");
			book = book.With(book.Active.Copy(phase: KingdomSubsidenceStepPhase.Quarantined,
				fault: "fixture quarantine"), book.Sequence);
			city.SubsidenceModel = Wire(book);
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due, 0);
		}

		[TestCase("ids")] [TestCase("homes")] [TestCase("standings")] [TestCase("zones")]
		[TestCase("roofs")] [TestCase("reached")] [TestCase("warned")]
		public void EqualReplacementListsDoNotSatisfyRawCarrierCompareAndSwap(string column)
		{
			KingdomCityBook city = Bound("roof-intent");
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			switch (column)
			{
				case "ids": city.ResidentIds = new List<int>(city.ResidentIds); break;
				case "homes": city.ResidentHomeWorkIds = new List<int>(city.ResidentHomeWorkIds); break;
				case "standings": city.ResidentStandings = new List<int>(city.ResidentStandings); break;
				case "zones": city.ResidentBoundZoneIds = new List<string>(city.ResidentBoundZoneIds); break;
				case "roofs": city.ResidentRoofStanding = new List<int>(city.ResidentRoofStanding); break;
				case "reached": city.ResidentRoofTicks = new List<long>(city.ResidentRoofTicks); break;
				default: city.ResidentRoofWarnedTicks = new List<long>(city.ResidentRoofWarnedTicks); break;
			}
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow replaced));
			ClassicAssert.IsFalse(prior.SameCarriers(replaced));
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due, 0);
		}

		[TestCase("standing")] [TestCase("reached")] [TestCase("warned")]
		[TestCase("home")] [TestCase("resident-standing")] [TestCase("zone")] [TestCase("resident-id")]
		public void ChangedBeforeTupleOrRowIdentityRefusesStaleSnapshot(string field)
		{
			KingdomCityBook city = Bound("roof-intent", true);
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			switch (field)
			{
				case "standing":
					city.ResidentRoofStanding[0] = 0; city.ResidentRoofTicks[0] = 0; city.ResidentRoofWarnedTicks[0] = 0; break;
				case "reached": city.ResidentRoofTicks[0]++; break;
				case "warned": city.ResidentRoofWarnedTicks[0]++; break;
				case "home": city.ResidentHomeWorkIds[0]++; break;
				case "resident-standing": city.ResidentStandings[0] = (int)KingdomResidentStanding.Expedition; break;
				case "zone": city.ResidentBoundZoneIds[0] = "JoppaWorld.12.24.1.1.11"; break;
				default: city.ResidentIds[0] = 99; break;
			}
			Refuses(city, city.SubsidenceModel, prior, true, RungFixture.Due + 10, RungFixture.Due + 20);
			ClassicAssert.IsTrue(prior.RoofStanding);
			ClassicAssert.AreEqual(RungFixture.Due + 10, prior.Reached);
			ClassicAssert.AreEqual(RungFixture.Due + 20, prior.Warned);
		}

		[TestCase("null-column")] [TestCase("torn-column")] [TestCase("null-name")]
		[TestCase("duplicate-id")] [TestCase("invalid-roof-flag")] [TestCase("off-ticks")]
		[TestCase("failed-read")] [TestCase("old-schema")] [TestCase("future-schema")]
		[TestCase("missing-model")] [TestCase("empty-model")] [TestCase("torn-model")]
		public void CorruptCarriersRefuseCaptureAndPublicationWithoutNormalization(string corruption)
		{
			KingdomCityBook city = Bound("roof-intent");
			string wire = city.SubsidenceModel;
			ClassicAssert.IsTrue(city.TryCaptureSubsidenceRoof(11, out RoofRow prior));
			switch (corruption)
			{
				case "null-column": city.ResidentRoofTicks = null; break;
				case "torn-column": city.ResidentHomeWorkIds.RemoveAt(0); break;
				case "null-name": city.ResidentNames[0] = null; break;
				case "duplicate-id": city.ResidentIds[1] = city.ResidentIds[0]; break;
				case "invalid-roof-flag": city.ResidentRoofStanding[0] = 2; break;
				case "off-ticks": city.ResidentRoofTicks[0] = 1; break;
				case "failed-read": city.SubsidenceReadFailed = true; break;
				case "old-schema": city.SchemaVersion--; break;
				case "future-schema": city.SchemaVersion++; break;
				case "missing-model": city.SubsidenceModel = null; break;
				case "empty-model": city.SubsidenceModel = ""; break;
				default: city.SubsidenceModel = wire.Substring(0, 30); break;
			}
			Snapshot snapshot = new Snapshot(city);
			ClassicAssert.IsFalse(city.TryCaptureSubsidenceRoof(11, out RoofRow refused));
			ClassicAssert.IsNull(refused);
			snapshot.Unchanged(city);
			Refuses(city, wire, prior, true, RungFixture.Due, 0);
		}

		[TestCase(0)] [TestCase(-1)] [TestCase(99)]
		public void MissingResidentCannotBeEnrolledByCapture(int id)
		{
			KingdomCityBook city = Carrier();
			Snapshot before = new Snapshot(city);
			ClassicAssert.IsFalse(city.TryCaptureSubsidenceRoof(id, out RoofRow row));
			ClassicAssert.IsNull(row);
			before.Unchanged(city);
		}

		private static void Refuses(KingdomCityBook city, string wire, RoofRow prior,
			bool stands, long reached, long warned)
		{
			Snapshot before = new Snapshot(city);
			ClassicAssert.IsFalse(city.TryPublishSubsidenceRoof(wire, prior, stands, reached, warned, out RoofRow after));
			ClassicAssert.IsNull(after);
			before.Unchanged(city);
		}

		private static KingdomCityBook Bound(string phase, bool standing = false)
		{
			KingdomCityBook city = Carrier(standing);
			city.SubsidenceModel = Wire(Parent(phase, standing));
			return city;
		}
		private static KingdomSubsidenceStepBook Parent(string phase, bool standing = false)
		{
			KingdomSubsidenceStepBook book = RungFixture.Settling(false);
			if (phase == "unplanned") return book;
			KingdomSubsidenceRungWork work = RungFixture.Work(roofs: new[] {
				RungFixture.Roof(11, standing), RungFixture.Roof(12) });
			KingdomSubsidenceRungPlan plan = new KingdomSubsidenceRungPlan(book.Active.Id,
				book.RealmId, book.SettlementId, RungFixture.Zone, GrowthStage.City, GrowthStage.Town,
				book.Active.DueTick, RungFixture.Prepared, book.Active.Completed, new[] { work });
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			if (phase == "prepared") return book;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			if (phase == "wear-intent") return book;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, work.AfterWear, out book));
			if (phase == "wear-proved") return book;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRoof(book, 0, 0, out book));
			if (phase == "roof-proved") ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRoof(book,
				0, 0, true, true, standing ? RungFixture.Due + 10 : RungFixture.Due,
				standing ? RungFixture.Due + 20 : 0, out book));
			return book;
		}
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			return wire;
		}
		private static KingdomCityBook Carrier(bool standing = false)
		{
			List<KingdomResidentRow> residents = new List<KingdomResidentRow>();
			for (int id = 11; id <= 13; id++) residents.Add(new KingdomResidentRow(id, "Fixture resident " + id,
				0, 0, 1, KingdomCityRules.StableId(RungFixture.ObjectId(0)), 0, 0, KingdomDayShape.Field,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, RungFixture.Zone,
				new KingdomBrinkWindow(standing && id == 11,
					standing && id == 11 ? RungFixture.Due + 10 : 0,
					standing && id == 11 ? RungFixture.Due + 20 : 0),
				new KingdomBrinkWindow(false, 0, 0), null, 0, null, "fixture", ""));
			KingdomStocks stocks = new KingdomStocks(new KingdomStockPair(0, 0),
				new KingdomStockPair(0, 0), new KingdomStockPair(0, 0));
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				RungFixture.Settlement, 0, stocks, new KingdomZoneRow[0], new KingdomWorkRow[0],
				residents.ToArray(), new KingdomClockRow[0], out KingdomCityState state, out _));
			KingdomCityBook city = new KingdomCityBook();
			ClassicAssert.IsTrue(city.TryPublish(state, out _));
			return city;
		}

		private sealed class Snapshot
		{
			private readonly Dictionary<FieldInfo, object> fields = new Dictionary<FieldInfo, object>();
			private readonly Dictionary<FieldInfo, object[]> lists = new Dictionary<FieldInfo, object[]>();
			internal Snapshot(KingdomCityBook city)
			{
				foreach (FieldInfo field in typeof(KingdomCityBook).GetFields(
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				{
					object value = field.GetValue(city);
					fields.Add(field, value);
					if (value is IList list)
					{
						object[] values = new object[list.Count];
						list.CopyTo(values, 0); lists.Add(field, values);
					}
				}
			}
			internal void Unchanged(KingdomCityBook city, params string[] changedColumns)
			{
				HashSet<string> allowed = new HashSet<string>(changedColumns, StringComparer.Ordinal);
				foreach (KeyValuePair<FieldInfo, object> entry in fields)
				{
					object current = entry.Key.GetValue(city);
					if (lists.TryGetValue(entry.Key, out object[] values))
					{
						ClassicAssert.AreSame(entry.Value, current, entry.Key.Name + " carrier replaced");
						if (!allowed.Contains(entry.Key.Name)) CollectionAssert.AreEqual(values, (IList)current, entry.Key.Name);
					}
					else ClassicAssert.AreEqual(entry.Value, current, entry.Key.Name);
				}
			}
		}
	}
}
#endif
