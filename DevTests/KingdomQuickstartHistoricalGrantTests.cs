#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Grant = ThousandAndFirst.Harness.KingdomQuickstartHistoricalGrant;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomQuickstartHistoricalGrantTests
	{
		private const string Game = "01234567-89ab-cdef-0123-456789abcdef";
		private static readonly string Hash = new string('a', 64);
		private static string Wire => Grant.Header + "\n" + Grant.ProductionPin + "\n" + Game + "\n" + Hash + "\n24\n";

		[Test]
		public void ExactPublicSourceAndSnapshotSelectHistoricalQuantity()
		{
			ClassicAssert.IsTrue(Grant.TryRead(Wire, Game, Hash, out int drams));
			ClassicAssert.AreEqual(24, drams);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
		public void ChangedFieldNeverFallsBackToCurrentGrant(int field)
		{
			string[] fields = Wire.Split('\n'); fields[field] += "x";
			Refuses(string.Join("\n", fields), Game, Hash);
		}

		[Test]
		public void AnotherSaveOrSnapshotCannotBorrowHistoricalQuantity()
		{
			Refuses(Wire, "11234567-89ab-cdef-0123-456789abcdef", Hash);
			Refuses(Wire, Game, new string('b', 64));
		}

		[Test]
		public void WaterChangeWhitespaceAndTruncationRefuse()
		{
			foreach (string text in new[] { null, "", Wire.Replace("\n24\n", "\n64\n"),
				Wire.Replace("\n", "\r\n"), Wire.TrimEnd(), " " + Wire, Wire + "\n", new string('a', 257) })
				Refuses(text, Game, Hash);
		}

		[Test]
		public void EvenMatchingNoncanonicalBindingsRefuse()
		{
			foreach (string game in new[] { "", "invalid", Game.ToUpperInvariant() })
				Refuses(Wire.Replace(Game, game), game, Hash);
			foreach (string hash in new[] { "", new string('a', 63), new string('A', 64), new string('g', 64) })
				Refuses(Wire.Replace(Hash, hash), Game, hash);
		}

		private static void Refuses(string text, string game, string hash)
		{
			ClassicAssert.IsFalse(Grant.TryRead(text, game, hash, out int drams));
			ClassicAssert.AreEqual(0, drams);
		}
	}
}
#endif
