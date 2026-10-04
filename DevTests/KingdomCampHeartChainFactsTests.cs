#if TAF_TESTS
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomCampHeartChainFactsTests
	{
		private static string Capture(string domain, params string[][] rows)
		{
			Assert.That(KingdomCampHeartChainFacts.TryCapture(domain, rows, out var wire, out var digest), Is.True);
			using (var sha = SHA256.Create())
				Assert.That(digest, Is.EqualTo(BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(wire)))
					.Replace("-", "").ToLowerInvariant()));
			return wire;
		}

		[Test]
		public void SharedHostFactsRetainNullUnicodeAndFieldBoundaries()
		{
			string wire = Capture("custody", new[] { "λ", "a|b", "🌳", "-" },
				new string[] { "id", null, "", "1:a" });
			Assert.That(wire, Is.EqualTo(TestMain.ReadRepositoryText("DevTests/Fixtures/heart-chain-facts-v1.wire")));
		}

		[Test]
		public void OrderDoesNotMatterButDomainAndEveryFieldDo()
		{
			var a = new[] { "resident:1", "body", "house", "12", "7" };
			var b = new[] { "resident:2", "other", "house", "13", "7" };
			string wire = Capture("residents", a, b);
			Assert.That(Capture("residents", b, a), Is.EqualTo(wire));
			Assert.That(Capture("support", a, b), Is.Not.EqualTo(wire));
			Assert.That(Capture("residents", a), Is.Not.EqualTo(wire));
			for (int i = 0; i < a.Length; i++)
			{
				var changed = (string[])a.Clone(); changed[i] += "x";
				Assert.That(Capture("residents", changed, b), Is.Not.EqualTo(wire));
			}
		}

		[Test]
		public void FieldAndRowBoundariesCannotCollide()
		{
			Assert.That(Capture("custody", new[] { "id", "a", "bc" }),
				Is.Not.EqualTo(Capture("custody", new[] { "id", "ab", "c" })));
			var values = new string[] { null, "", "-", "1:a", "a|b", "λ🌳" };
			Assert.That(values.Select(value => Capture("custody", new[] { "id", value })).Distinct().Count(),
				Is.EqualTo(values.Length));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("bad\n")]
		public void InvalidDomainsRefuse(string domain)
		{
			Refused(domain, new[] { new[] { "id", "value" } });
		}

		[TestCase("\n")]
		[TestCase("\0")]
		public void ControlOrMalformedUnicodeRefuses(string value)
		{
			Refused("jobs", new[] { new[] { "id", value } });
		}

		[Test]
		public void UnpairedSurrogatesRefuseWithoutMetadataReplacement()
		{
			foreach (char letter in new[] { (char)0xd800, (char)0xdc00 })
			{
				string value = new string(letter, 1);
				Refused(value, new[] { new[] { "id", "valid" } });
				Refused("jobs", new[] { new[] { "id", value } });
			}
		}

		[Test]
		public void TornDuplicateAndOversizedRowsRefuseWithoutPartialOutputs()
		{
			Refused("jobs", null);
			Refused("jobs", new string[][] { null });
			Refused("jobs", new[] { Array.Empty<string>() });
			Refused("jobs", new[] { new string[] { null } });
			Refused("jobs", new[] { new[] { "" } });
			Refused("jobs", new[] { new[] { "id", "a" }, new[] { "id", "b" } });
			Refused("jobs", new[] { Enumerable.Repeat("id", 65).ToArray() });
			Refused("jobs", new[] { new[] { "id", new string('x', KingdomCampHeartChainFacts.MaxFieldChars + 1) } });
			Refused("jobs", Enumerable.Range(0, 4097).Select(i => new[] { i.ToString() }).ToArray());
			Refused("jobs", Enumerable.Range(0, 9).Select(i => new[] { i.ToString(),
				new string('x', KingdomCampHeartChainFacts.MaxFieldChars) }).ToArray());
		}

		private static void Refused(string domain, string[][] rows)
		{
			Assert.That(KingdomCampHeartChainFacts.TryCapture(domain, rows, out var wire, out var digest), Is.False);
			Assert.That(wire, Is.Null); Assert.That(digest, Is.Null);
		}
	}
}
#endif
