#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;
using Codec = ThousandAndFirst.Harness.KingdomCampHeartChainSnapshotCodec;
using Anchor = ThousandAndFirst.Harness.KingdomCampHeartChainAnchor;

namespace ThousandAndFirst.Tests
{
	public class KingdomCampHeartChainSnapshotTests
	{
		private static KingdomCampHeartChainSnapshot Sample(int rung = 4, string resident = "resident-50",
			Anchor heart = null, Anchor basin = null, int population = 50, int water = 4848,
			int food = 1728, long turns = 31204, long ticks = 445200, string game = null,
			string digest = null, string realm = "realm", string job = "paid-court")
			=> new KingdomCampHeartChainSnapshot(game ?? "01234567-89ab-cdef-0123-456789abcdef",
				realm, "city", "JoppaWorld.8.22.1.1.10", rung, heart ?? new Anchor("heart", 41, 12),
				basin ?? new Anchor("basin", 40, 12), new Anchor("store", 43, 13), new Anchor("track", 36, 9),
				resident, job, digest ?? new string('a', 64), new string('b', 64), new string('c', 64),
				new string('d', 64), population, water, food, turns, ticks);

		private static string Encode(KingdomCampHeartChainSnapshot value)
		{
			Assert.That(Codec.TryEncode(value, out string wire), Is.True);
			return wire;
		}

		[TestCase(3)]
		[TestCase(4)]
		public void CanonicalRoundTripRetainsEveryObservedField(int rung)
		{
			var wanted = Sample(rung);
			string wire = Encode(wanted);
			Assert.That(Codec.TryDecode(wire, out var got), Is.True);
			Assert.That(new[] { got.GameId, got.RealmId, got.CityId, got.ZoneId, got.ResidentId,
				got.JobId, got.JobsDigest, got.ResidentsDigest, got.SupportDigest, got.CustodyDigest },
				Is.EqualTo(new[] { wanted.GameId, wanted.RealmId, wanted.CityId, wanted.ZoneId,
					wanted.ResidentId, wanted.JobId, wanted.JobsDigest, wanted.ResidentsDigest,
					wanted.SupportDigest, wanted.CustodyDigest }));
			Assert.That(new long[] { got.Rung, got.Population, got.Water, got.Food, got.Turns, got.TimeTicks },
				Is.EqualTo(new long[] { wanted.Rung, wanted.Population, wanted.Water, wanted.Food,
					wanted.Turns, wanted.TimeTicks }));
			var observed = new[] { got.Heart, got.Basin, got.Store, got.Track };
			var anchors = new[] { wanted.Heart, wanted.Basin, wanted.Store, wanted.Track };
			for (int i = 0; i < anchors.Length; i++)
			{
				Assert.That(observed[i].Id, Is.EqualTo(anchors[i].Id));
				Assert.That(new[] { observed[i].X, observed[i].Y }, Is.EqualTo(new[] { anchors[i].X, anchors[i].Y }));
			}
			Assert.That(Encode(got), Is.EqualTo(wire));
		}

		[Test]
		public void EveryTruncationAndTrailingPayloadRefusesWithoutPartialWitness()
		{
			byte[] bytes = Convert.FromBase64String(Encode(Sample()).Substring(Codec.Prefix.Length));
			for (int length = 0; length < bytes.Length; length++)
			{
				Assert.That(Codec.TryDecode(Codec.Prefix + Convert.ToBase64String(bytes, 0, length), out var value), Is.False);
				Assert.That(value, Is.Null);
			}
			Assert.That(Codec.TryDecode(Codec.Prefix + Convert.ToBase64String(bytes.Concat(new byte[] { 0 }).ToArray()),
				out var trailing), Is.False);
			Assert.That(trailing, Is.Null);
		}

		[TestCase(0, 0)]
		[TestCase(4, 2)]
		[TestCase(8, -1)]
		[TestCase(8, 0)]
		[TestCase(8, 4097)]
		[TestCase(8, int.MaxValue)]
		public void BadMagicVersionAndFieldLengthsRefuse(int offset, int word)
		{
			byte[] bytes = Convert.FromBase64String(Encode(Sample()).Substring(Codec.Prefix.Length));
			using (var stream = new MemoryStream(bytes, true))
			using (var writer = new BinaryWriter(stream)) { stream.Position = offset; writer.Write(word); }
			Assert.That(Codec.TryDecode(Codec.Prefix + Convert.ToBase64String(bytes), out var value), Is.False);
			Assert.That(value, Is.Null);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("taf-camp-heart-save-v2:AAAA")]
		[TestCase("taf-camp-heart-chain-save-v2:AAAA")]
		[TestCase("taf-camp-heart-chain-save-v1:!")]
		public void OtherContractsAndMalformedWiresNeverFallBack(string wire)
		{
			Assert.That(Codec.TryDecode(wire, out var value), Is.False);
			Assert.That(value, Is.Null);
		}

		[Test]
		public void MalformedUtf8IsNotSilentlyReplacedInAnIdentity()
		{
			byte[] bytes = Convert.FromBase64String(Encode(Sample()).Substring(Codec.Prefix.Length));
			using (var stream = new MemoryStream(bytes, true))
			using (var reader = new BinaryReader(stream))
			{
				stream.Position = 8;
				int gameLength = reader.ReadInt32();
				stream.Position += gameLength;
				Assert.That(reader.ReadInt32(), Is.EqualTo(5));
				bytes[(int)stream.Position] = 0xff;
			}
			Assert.That(Codec.TryDecode(Codec.Prefix + Convert.ToBase64String(bytes), out var value), Is.False);
			Assert.That(value, Is.Null);
		}

		[Test]
		public void NoncanonicalBase64AndOversizedWiresRefuse()
		{
			string wire = Encode(Sample());
			Assert.That(Codec.TryDecode(wire.Insert(Codec.Prefix.Length + 4, "\n"), out _), Is.False);
			Assert.That(Codec.TryDecode(new string('a', Codec.MaxWireChars + 1), out _), Is.False);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(5)]
		public void UntestedRungsAreNotMislabelledAsHigherHeartEvidence(int rung)
			=> Assert.That(Codec.TryEncode(Sample(rung), out _), Is.False);

		[TestCase("heart")]
		[TestCase("basin")]
		[TestCase("store")]
		[TestCase("track")]
		public void ResidentCannotAliasAnObservedBuildingOrTrack(string id)
			=> Assert.That(Codec.TryEncode(Sample(resident: id), out _), Is.False);

		[Test]
		public void DistinctObjectsCanShareCellsButCannotShareIdentity()
		{
			Assert.That(Codec.TryEncode(Sample(basin: new Anchor("heart", 40, 12)), out _), Is.False);
			Assert.That(Codec.TryEncode(Sample(basin: new Anchor("basin", 41, 12)), out _), Is.True);
		}

		[TestCase(-1, 12)]
		[TestCase(4096, 12)]
		[TestCase(41, -1)]
		[TestCase(41, 4096)]
		public void ImpossibleAnchorsRefuse(int x, int y)
			=> Assert.That(Codec.TryEncode(Sample(heart: new Anchor("heart", x, y)), out _), Is.False);

		[Test]
		public void NegativeCountsAndClockRefuseButSchemaAddsNoPopulationCap()
		{
			foreach (var value in new[] { Sample(population: 0), Sample(population: -1), Sample(water: -1),
				Sample(food: -1), Sample(turns: -1), Sample(ticks: -1) })
				Assert.That(Codec.TryEncode(value, out _), Is.False);
			Assert.That(Codec.TryEncode(Sample(population: int.MaxValue, water: 0, food: 0,
				turns: long.MaxValue, ticks: long.MaxValue), out _), Is.True);
		}

		[Test]
		public void InvalidTextAndDigestRefuse()
		{
			foreach (var value in new[] { Sample(realm: ""), Sample(realm: "bad\nrealm"),
				Sample(realm: new string('x', 1025)), Sample(realm: new string((char)0xd800, 1)),
				Sample(digest: new string('A', 64)), Sample(digest: new string('g', 64)),
				Sample(digest: new string('a', 63)), Sample(game: "01234567-89AB-CDEF-0123-456789ABCDEF") })
				Assert.That(Codec.TryEncode(value, out _), Is.False);
			Assert.That(Codec.TryEncode(null, out var wire), Is.False);
			Assert.That(wire, Is.Null);
		}
	}
}
#endif
