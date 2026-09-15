#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public partial class KingdomResidenceTests
	{
		private static KingdomResidence Home(string zone = "home", string plot = "plot", string bed = "bed",
			string creed = null, string[] needs = null)
			=> new KingdomResidence(zone, plot, bed, creed, needs ?? new[] { "warm", "dry" },
				new[] { "fungal" }, new[] { "human" });

		private static string Wire(KingdomResidence home)
		{
			ClassicAssert.IsTrue(KingdomResidenceRules.TryEncode(home, out string wire));
			return wire;
		}

		[TestCase(null)] [TestCase("")] [TestCase("Mechanimists")]
		public void CanonicalRoundTripKeepsHomeAndObservedHouseholdFacts(string creed)
		{
			string wire = Wire(Home(creed: creed));
			ClassicAssert.IsTrue(KingdomResidenceRules.TryDecode(wire, out KingdomResidence decoded));
			ClassicAssert.AreEqual(creed, decoded.Creed);
			CollectionAssert.AreEqual(new[] { "dry", "warm" }, decoded.Needs);
			CollectionAssert.AreEqual(new[] { "fungal" }, decoded.Refuses);
			CollectionAssert.AreEqual(new[] { "human" }, decoded.SelfTags);
			ClassicAssert.AreEqual(wire, Wire(decoded));
			ClassicAssert.IsTrue(KingdomResidenceRules.SameBed(Home(), decoded));
			ClassicAssert.IsFalse(KingdomResidenceRules.SameBed(Home(zone: "away"), decoded));
			ClassicAssert.IsFalse(KingdomResidenceRules.SameBed(Home(bed: ""), decoded));
		}

		[Test]
		public void UnknownResidenceAndObservedHomelessnessRemainDifferent()
		{
			ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode("", out _));
			ClassicAssert.IsTrue(KingdomResidenceRules.TryDecode(Wire(Home("", "", "")), out var home));
			ClassicAssert.IsFalse(home.HasHome);
			ClassicAssert.IsFalse(KingdomResidenceRules.SameHome(home, "", ""));
			ClassicAssert.IsTrue(KingdomResidenceRules.TryEncode(Home(bed: ""), out _),
				"legacy household reservation is not an invented unique bed");
		}

		[Test]
		public void EncodingOwnsItsFactsAndRejectsInvalidOrOversizedAuthority()
		{
			var tags = new[] { "warm", "dry" };
			string wire = Wire(Home(needs: tags));
			tags[0] = "changed";
			ClassicAssert.AreEqual(Wire(Home()), wire);
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home("", "plot", ""), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home("home", "", ""), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home("", "", "bed"), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home(zone: new string('x', 257)), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home(plot: "bad\nplot"), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home(needs: new[] { "dry", "dry" }), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home(needs: new[] { "" }), out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(Home(needs: new string[33]), out _));
			var many = new List<string>();
			for (int i = 0; i < 32; i++) many.Add(i.ToString("D2") + new string('x', 126));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryEncode(new KingdomResidence("home", "plot", "bed",
				"creed", many, many, many), out _));
		}

		[TestCase(null)] [TestCase("")] [TestCase("taf-residence-v2")]
		public void UnknownOrMissingWireIsNotDecodedAsAnEmptyHome(string wire)
			=> ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode(wire, out _));

		[Test]
		public void NoncanonicalOrMalformedWireCannotChangeTheMeaningOfAnObservedHome()
		{
			string wire = Wire(Home());
			ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode(wire + "\n", out _));
			ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode(new string('a', 8193), out _));
			string[] fields = wire.Split('\n');
			fields[1] = "!!!!";
			ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode(string.Join("\n", fields), out _));
			fields[1] = "/w=="; // invalid UTF-8
			ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode(string.Join("\n", fields), out _));
			fields = wire.Split('\n'); fields[5] = "d2FybQ==,ZHJ5"; // valid tags, wrong order
			ClassicAssert.IsFalse(KingdomResidenceRules.TryDecode(string.Join("\n", fields), out _));
		}

		private static KingdomCityBook Book()
		{
			var resident = new KingdomResidentRow(7, "Tes", 2, 3, 40, 101, 0, 0,
				KingdomDayShape.Hearth, KingdomResidentStanding.Resident, KingdomStandingCause.None,
				"away", KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion, "city", 900, default, null, null,
				new[] { resident.WithResidence(Wire(Home())), resident.WithResidence("").WithBoundZone("home") },
				null, out var state, out _));
			var book = new KingdomCityBook();
			ClassicAssert.IsTrue(book.TryPublish(state, out _));
			book.ResidentIds[1] = 8;
			return book;
		}

		private static void ReadFields(KingdomCityBook source, KingdomCityBook target, bool legacy)
		{
			foreach (FieldInfo field in typeof(KingdomCityBook).GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				if (legacy && field.Name == "ResidentResidences") continue;
				object value = field.GetValue(source);
				if (value is IList list)
				{
					var copy = (IList)Activator.CreateInstance(value.GetType());
					foreach (object item in list) copy.Add(item);
					value = copy;
				}
				field.SetValue(target, value);
			}
			if (legacy) target.SchemaVersion = 4;
		}

		[TestCase(false)] [TestCase(true)]
		public void NamedReadKeepsAllResidentsAndMigratesOnlyUnknownFacts(bool legacy)
		{
			var source = Book(); var loaded = new KingdomCityBook();
			loaded.ReadNamedState(() => ReadFields(source, loaded, legacy));
			ClassicAssert.IsTrue(loaded.TryReadExact(out var state, out _));
			ClassicAssert.AreEqual(2, state.ResidentCount);
			ClassicAssert.AreEqual(KingdomCityRules.SchemaVersion, state.SchemaVersion);
			ClassicAssert.AreEqual(900, state.ProcessedThroughTick);
			ClassicAssert.IsTrue(state.TryResident(0, out var resident));
			ClassicAssert.AreEqual("away", resident.BoundZoneId);
			ClassicAssert.AreEqual(101, resident.HomeWorkId);
			ClassicAssert.AreEqual(legacy ? "" : Wire(Home()), resident.Residence);
			ClassicAssert.AreEqual(source.SubsidenceModel, loaded.SubsidenceModel);
		}

		[TestCase("missing")] [TestCase("short")] [TestCase("null")] [TestCase("invalid")] [TestCase("duplicate")]
		public void MalformedCurrentResidenceRefusesWithoutTruncatingAnyResident(string corruption)
		{
			var source = Book();
			if (corruption == "missing") source.ResidentResidences = null;
			if (corruption == "short") source.ResidentResidences.RemoveAt(1);
			if (corruption == "null") source.ResidentResidences[0] = null;
			if (corruption == "invalid") source.ResidentResidences[0] = "broken";
			if (corruption == "duplicate") source.ResidentResidences[1] = source.ResidentResidences[0];
			var loaded = new KingdomCityBook();
			Assert.Throws<InvalidDataException>(() => loaded.ReadNamedState(() => ReadFields(source, loaded, false)));
			ClassicAssert.IsFalse(loaded.TryRead(out _, out _));
			ClassicAssert.IsFalse(source.TryRead(out _, out _));
			CollectionAssert.AreEqual(new[] { 7, 8 }, source.ResidentIds);
			CollectionAssert.AreEqual(source.ResidentIds, loaded.ResidentIds);
			CollectionAssert.AreEqual(source.ResidentResidences, loaded.ResidentResidences);
		}

		[TestCase(false)] [TestCase(true)]
		public void DuplicatePhysicalBedsRefuseOnlyWithinTheSameHomeMap(bool sameMap)
		{
			var book = Book(); book.TryRead(out var state, out _);
			state.TryResident(0, out var first); state.TryResident(1, out var second);
			var rows = new[] { first, second.WithResidence(Wire(Home(zone: sameMap ? "home" : "other"))) };
			ClassicAssert.AreEqual(!sameMap, state.TryWithResidents(rows, out _, out _));
			ClassicAssert.AreEqual(!sameMap, state.TryWithResident(1, rows[1], out _, out _));
			ClassicAssert.AreEqual(!sameMap, KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion, "city", 0, default, null, null, rows, null, out _, out _));
			rows[1] = second.WithResidence("malformed");
			ClassicAssert.IsFalse(state.TryWithResidents(rows, out _, out _));
			ClassicAssert.IsFalse(state.TryWithResident(1, rows[1], out _, out _));
			ClassicAssert.AreEqual(Wire(Home()), book.ResidentResidences[0]);
		}

		[Test]
		public void MovementStandingReadingAndBrinksCannotDropHomeAuthority()
		{
			var book = Book(); ClassicAssert.IsTrue(book.TryRead(out var state, out _));
			state.TryResident(0, out var row);
			var moved = row.WithBoundZone("third").WithStanding(KingdomResidentStanding.Abroad,
				KingdomStandingCause.Followed).WithReading("Tes", 2, 3, 101, 202, 0, KingdomDayShape.Hearth)
				.WithKeptCreeds("old").WithBrink(BrinkKind.Roof, KingdomBrinkWindow.None, null, 0)
				.WithBrink(BrinkKind.Creed, KingdomBrinkWindow.None, null, 0);
			ClassicAssert.AreEqual(row.Residence, moved.Residence);
			ClassicAssert.AreEqual("third", moved.BoundZoneId);
			ClassicAssert.AreEqual(202, moved.JobWorkId);
			state.TryResident(1, out var second);
			ClassicAssert.IsTrue(state.TryWithResidents(new[] { row.WithResidence(Wire(Home(zone: "other"))), second },
				out var changed, out _));
			ClassicAssert.IsFalse(KingdomResidentRules.SameCity(state, changed));
		}
	}
}
#endif
