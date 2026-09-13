#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;
using Codec = ThousandAndFirst.Harness.KingdomCampHeartSaveSnapshotCodec;
using Unit = ThousandAndFirst.Harness.KingdomCampHeartNativeCensus.Unit;

namespace ThousandAndFirst.Tests
{
	public class KingdomCampHeartSaveTests
	{
		private static KingdomCampHeartSaveSnapshot Sample(string game = "01234567-89ab-cdef-0123-456789abcdef",
			string heart = "heart", string store = "store", string digest = null, int x = 40, int water = 300, long turns = 6002)
			=> new KingdomCampHeartSaveSnapshot(game, "realm", "city", "JoppaWorld.8.22.1.1.10",
				heart, "upgrade", store, "fire", "timber", digest ?? new string('a', 64), new string('b', 64),
				x, 12, 43, 13, 39, 13, water, turns);
		private static string Encode(KingdomCampHeartSaveSnapshot value)
		{
			Assert.That(Codec.TryEncode(value, out string wire), Is.True);
			return wire;
		}
		private static string Wrap(byte[] bytes) => Codec.Prefix + Convert.ToBase64String(bytes);
		private static byte[] Bytes() => Convert.FromBase64String(Encode(Sample()).Substring(Codec.Prefix.Length));
		private static void Refuse(string wire)
		{
			Assert.That(Codec.TryDecode(wire, out var value), Is.False);
			Assert.That(value, Is.Null);
		}

		[Test]
		public void SnapshotRoundTripPreservesAllIdentityCustodyAndClockFields()
		{
			var want = Sample(heart: "heart|:é", turns: long.MaxValue);
			string wire = Encode(want);
			Assert.That(Codec.TryDecode(wire, out var got), Is.True);
			Assert.That(new[] { got.GameId, got.RealmId, got.CityId, got.ZoneId, got.HeartId, got.UpgradeJobId,
				got.StoreId, got.FireId, got.TimberId, got.ContentsDigest, got.BrushDigest },
				Is.EqualTo(new[] { want.GameId, want.RealmId, want.CityId, want.ZoneId, want.HeartId, want.UpgradeJobId,
					want.StoreId, want.FireId, want.TimberId, want.ContentsDigest, want.BrushDigest }));
			Assert.That(new[] { got.HeartX, got.HeartY, got.StoreX, got.StoreY, got.FireX, got.FireY, got.Water },
				Is.EqualTo(new[] { want.HeartX, want.HeartY, want.StoreX, want.StoreY, want.FireX, want.FireY, want.Water }));
			Assert.That(got.Turns, Is.EqualTo(want.Turns));
			Assert.That(Encode(got), Is.EqualTo(wire));
		}

		[Test]
		public void EveryByteTruncationAndTrailingDataRefuseWithoutPartialResult()
		{
			byte[] bytes = Bytes();
			for (int i = 0; i < bytes.Length; i++) Refuse(Wrap(bytes.Take(i).ToArray()));
			Refuse(Wrap(bytes.Concat(new byte[] { 0 }).ToArray()));
		}

		[TestCase(0, 0)]
		[TestCase(4, 2)]
		[TestCase(8, 0)]
		[TestCase(8, -1)]
		[TestCase(8, 4097)]
		[TestCase(8, 2147483647)]
		public void MagicVersionAndFramedLengthCorruptionRefuse(int offset, int value)
		{
			byte[] bytes = Bytes();
			Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
			Refuse(Wrap(bytes));
		}

		[Test]
		public void InvalidUtf8AndBase64AliasesRefuse()
		{
			byte[] bytes = Bytes();
			bytes[12] = 0xff;
			Refuse(Wrap(bytes));
			string wire = Encode(Sample());
			Refuse(wire.Insert(Codec.Prefix.Length + 4, " "));
			Refuse(wire + "\n");
			Refuse(Codec.Prefix + "!");
			Refuse("taf-camp-heart-save-v2:" + wire.Substring(Codec.Prefix.Length));
			Refuse(null);
			Refuse(new string('a', Codec.MaxWireChars + 1));
		}

		[TestCase("empty-game")]
		[TestCase("uppercase-guid")]
		[TestCase("duplicate-object")]
		[TestCase("negative-coordinate")]
		[TestCase("outside-coordinate")]
		[TestCase("negative-water")]
		[TestCase("negative-turns")]
		[TestCase("uppercase-digest")]
		[TestCase("short-digest")]
		[TestCase("control")]
		[TestCase("surrogate")]
		[TestCase("unbounded")]
		public void InvalidSnapshotsCannotPublish(string fault)
		{
			var value = fault == "empty-game" ? Sample(game: "")
				: fault == "uppercase-guid" ? Sample(game: "01234567-89AB-CDEF-0123-456789ABCDEF")
				: fault == "duplicate-object" ? Sample(store: "heart")
				: fault == "negative-coordinate" ? Sample(x: -1)
				: fault == "outside-coordinate" ? Sample(x: 4096)
				: fault == "negative-water" ? Sample(water: -1)
				: fault == "negative-turns" ? Sample(turns: -1)
				: fault == "uppercase-digest" ? Sample(digest: new string('A', 64))
				: fault == "short-digest" ? Sample(digest: "abc")
				: fault == "control" ? Sample(heart: "heart\n")
				: fault == "surrogate" ? Sample(heart: "heart\ud800") : Sample(heart: new string('a', 1025));
			Assert.That(Codec.TryEncode(value, out var wire), Is.False);
			Assert.That(wire, Is.Null);
		}

		[Test]
		public void CustodyDigestIgnoresOrderButDetectsEveryChangedFact()
		{
			var a = new Unit("one", "brush", "inv:store", 1);
			var b = new Unit("two", "timber", "inv:store", 1);
			string original = Codec.CustodyDigest(new[] { a, b });
			Assert.That(Codec.CustodyDigest(new[] { b, a }), Is.EqualTo(original));
			foreach (var replacement in new[] { new Unit("other", "brush", "inv:store", 1),
				new Unit("one", "timber", "inv:store", 1), new Unit("one", "brush", "inv:other", 1),
				new Unit("one", "brush", "inv:store", 2) })
				Assert.That(Codec.CustodyDigest(new[] { replacement, b }), Is.Not.EqualTo(original));
			Assert.That(Codec.CustodyDigest(new[] { new Unit("a|b", "c", "d", 1) }),
				Is.Not.EqualTo(Codec.CustodyDigest(new[] { new Unit("a", "b|c", "d", 1) })));
		}

		[Test]
		public void MalformedOrUnboundedCustodyCannotProduceEvidence()
		{
			var valid = new Unit("one", "brush", "inv:store", 1);
			var cases = new List<IReadOnlyList<Unit>> { null, new Unit[] { null }, new[] { valid, valid },
				Enumerable.Range(0, 49).Select(i => new Unit("id" + i, "brush", "store", 1)).ToArray() };
			foreach (var bad in new[] { new Unit("", "brush", "store", 1), new Unit("one", "", "store", 1),
				new Unit("one", "brush", "", 1), new Unit("one", "brush", "store", 0),
				new Unit("one", "brush", "store", -1), new Unit("one\n", "brush", "store", 1),
				new Unit("one", "brush\ud800", "store", 1), new Unit("one", "brush", new string('s', 1025), 1) })
				cases.Add(new[] { bad });
			foreach (var rows in cases) Assert.Throws<System.IO.InvalidDataException>(() => Codec.CustodyDigest(rows));
		}

		[Test]
		public void SavePersonaUsesExactOriginalStepsAndOneFinalSave()
		{
			string[] source = Script("camp-heart-native-checks"), saved = Script("camp-heart-save");
			Assert.That(KingdomCampHeartScript.Matches(source), Is.True);
			Assert.That(KingdomCampHeartScript.Matches(source, true), Is.False);
			Assert.That(KingdomCampHeartScript.Matches(saved, true), Is.True);
			Assert.That(saved, Is.EqualTo(source.Concat(new[] { "camp-heart-save" }).ToArray()));
			Assert.That(KingdomCampHeartScript.Matches(null), Is.False);
			for (int i = 0; i < saved.Length; i++)
			{
				var changed = (string[])saved.Clone();
				changed[i] += " ";
				Assert.That(KingdomCampHeartScript.Matches(changed, true), Is.False);
				Assert.That(KingdomCampHeartScript.Matches(saved.Where((_, at) => at != i).ToArray(), true), Is.False);
			}
			Assert.That(KingdomCampHeartScript.Matches(saved.Concat(new[] { "camp-heart-save" }).ToArray()), Is.False);
		}
		private static string[] Script(string name) => TestMain.ReadRepositoryText("Tools/personas/" + name + ".persona")
			.Split('\n').Single(line => line.StartsWith("SCRIPT=", StringComparison.Ordinal)).Substring(7).Split(';');
	}
}
#endif
