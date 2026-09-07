using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPresentationRulesTests
	{
		[Test]
		public void NameIsTrimmedAndNormalizedToNfc()
		{
			string name;
			string error;
			ClassicAssert.IsTrue(KingdomPresentationRules.TryNormalizeName("  Cafe\u0301  ",
				out name, out error), error);
			ClassicAssert.AreEqual("Caf\u00e9", name);
		}

		[Test]
		public void LimitCountsTextElementsRatherThanUtf16Units()
		{
			string thirty = "";
			for (int i = 0; i < 30; i++) thirty += "\U0001f40c";
			string name;
			string error;
			ClassicAssert.IsTrue(KingdomPresentationRules.TryNormalizeName(thirty,
				out name, out error), error);
			ClassicAssert.AreEqual(thirty, name);
			ClassicAssert.IsFalse(KingdomPresentationRules.TryNormalizeName(thirty + "x",
				out name, out error));
			StringAssert.Contains("30", error);
		}

		[TestCase("\n")]
		[TestCase("\0")]
		[TestCase("\u001f")]
		[TestCase("\u007f")]
		[TestCase("\u009f")]
		[TestCase("\u061c")]
		[TestCase("\u200e")]
		[TestCase("\u200f")]
		[TestCase("\u202a")]
		[TestCase("\u202e")]
		[TestCase("\u2066")]
		[TestCase("\u2069")]
		public void ForbiddenControlsRefuseBeforeTrimming(string forbidden)
		{
			string name;
			string error;
			ClassicAssert.IsFalse(KingdomPresentationRules.TryNormalizeName(
				forbidden + "Joppa" + forbidden, out name, out error));
			StringAssert.Contains("control", error);
		}

		[Test]
		public void PlainBracesAreAcceptedForEscapingAtPresentationBoundary()
		{
			string name;
			string error;
			ClassicAssert.IsTrue(KingdomPresentationRules.TryNormalizeName("{{R|Not markup}}",
				out name, out error), error);
			ClassicAssert.AreEqual("{{R|Not markup}}", name);
		}

		[TestCase("Kavvat", "Kavvat")]
		[TestCase("A&B^C", "A&&B^^C")]
		[TestCase("{{R|Not markup}}", "{\\{R|Not markup}\\}")]
		[TestCase("{{R|one {{G|two}} three}}", "{\\{R|one {\\{G|two}\\} three}\\}")]
		public void RuntimeBoundaryUsesQudFormattingEscape(string plain, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomPresentation.Rich(plain));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void BlankNamesRefuse(string raw)
		{
			string name;
			string error;
			ClassicAssert.IsFalse(KingdomPresentationRules.TryNormalizeName(raw,
				out name, out error));
			ClassicAssert.IsNull(name);
		}

		[Test]
		public void IllFormedSurrogateRefuses()
		{
			string name;
			string error;
			ClassicAssert.IsFalse(KingdomPresentationRules.TryNormalizeName("bad\ud800text",
				out name, out error));
			StringAssert.Contains("Unicode", error);
		}
	}
}
