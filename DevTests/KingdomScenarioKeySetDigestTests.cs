#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The anchor key-set digest, which decides whether a scenario matched ordinary play.
	/// <para>
	/// It used to join key and value with two control separators without ever proving the measured
	/// value excluded them, so a property carrying a separator could imitate a different key/value
	/// sequence and two different captures could digest alike. That is the same collision the
	/// realized-capture grammar removed, one authority over.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioKeySetDigestTests
	{
		private const string Authority = "architecture-stamper";

		private static IDictionary<string, string> Capture()
		{
			IList<string> keys = KingdomScenarioAnchorRules.KeySet(Authority);
			Dictionary<string, string> captured =
				new Dictionary<string, string>(StringComparer.Ordinal);
			for (int i = 0; i < keys.Count; i++) captured[keys[i]] = "value-" + i;
			return captured;
		}

		private static string Digest(IDictionary<string, string> captured)
		{
			string digest;
			string failure;
			return KingdomScenarioAnchorRules.TryDigest(Authority, captured, out digest, out failure)
				? digest : null;
		}

		private static string FirstKey()
		{
			return KingdomScenarioAnchorRules.KeySet(Authority)[0];
		}

		private static string SecondKey()
		{
			return KingdomScenarioAnchorRules.KeySet(Authority)[1];
		}

		[Test]
		public void AWellFormedCaptureDigests()
		{
			Assert.AreEqual(64, Digest(Capture()).Length);
			Assert.AreEqual(Digest(Capture()), Digest(Capture()));
		}

		[Test]
		public void EveryDeclaredKeyChangesTheDigest()
		{
			IList<string> keys = KingdomScenarioAnchorRules.KeySet(Authority);
			for (int i = 0; i < keys.Count; i++)
			{
				IDictionary<string, string> altered = Capture();
				altered[keys[i]] = "moved";
				Assert.AreNotEqual(Digest(Capture()), Digest(altered), keys[i]);
			}
		}

		// ----- injectivity: the collisions a separator join could not avoid -----------------------

		/// <summary>
		/// Two captures whose naive key/value concatenation is identical must still differ. Under
		/// the old grammar the value on the left absorbed the next key.
		/// </summary>
		[Test]
		public void AValueSpelledLikeAFieldBoundaryDoesNotCollide()
		{
			IDictionary<string, string> a = Capture();
			IDictionary<string, string> b = Capture();
			a[FirstKey()] = "x";
			a[SecondKey()] = "y";
			b[FirstKey()] = "x" + SecondKey() + "y";
			b[SecondKey()] = "";
			Assert.AreNotEqual(Digest(a), Digest(b));
			Assert.IsNotNull(Digest(a));
		}

		[TestCase("1:x")]
		[TestCase("0:")]
		[TestCase(":")]
		[TestCase("-")]
		public void AValueSpelledLikeTheGrammarIsStillOneValue(string spelling)
		{
			IDictionary<string, string> a = Capture();
			IDictionary<string, string> b = Capture();
			a[FirstKey()] = spelling;
			b[SecondKey()] = spelling;
			Assert.AreNotEqual(Digest(a), Digest(b));
			Assert.IsNotNull(Digest(a));
		}

		/// <summary>The separators the previous grammar joined with are refused outright.</summary>
		[TestCase("\u0001")]
		[TestCase("\u0002")]
		[TestCase("a\u0000b")]
		[TestCase("a\u007Fb")]
		[TestCase("a\u0085b")]
		public void AControlValueRefusesRatherThanEncoding(string hostile)
		{
			IDictionary<string, string> captured = Capture();
			captured[FirstKey()] = hostile;
			Assert.IsNull(Digest(captured));
		}

		/// <summary>
		/// A lone surrogate is refused: the default UTF-8 encoder maps every one of them to U+FFFD,
		/// which would fold two different captures onto identical bytes.
		/// </summary>
		[Test]
		public void AnUnpairedSurrogateRefuses()
		{
			IDictionary<string, string> high = Capture();
			high[FirstKey()] = "a\uD800b";
			Assert.IsNull(Digest(high));
			IDictionary<string, string> low = Capture();
			low[FirstKey()] = "a\uDC00b";
			Assert.IsNull(Digest(low));
			IDictionary<string, string> paired = Capture();
			paired[FirstKey()] = "a\uD83D\uDE00b";
			Assert.IsNotNull(Digest(paired), "a well-formed pair is an ordinary value");
		}

		[Test]
		public void AnOverboundValueRefuses()
		{
			IDictionary<string, string> captured = Capture();
			captured[FirstKey()] = new string('v', KingdomScenarioAnchorRules.MaxFieldChars + 1);
			Assert.IsNull(Digest(captured));
		}

		// ----- exact arity ------------------------------------------------------------------------

		[Test]
		public void AMissingDeclaredKeyRefuses()
		{
			IDictionary<string, string> captured = Capture();
			captured.Remove(FirstKey());
			Assert.IsNull(Digest(captured));
		}

		[Test]
		public void AnUndeclaredKeyRefuses()
		{
			IDictionary<string, string> captured = Capture();
			captured["architecture.invented.key"] = "v";
			Assert.IsNull(Digest(captured));
		}

		[Test]
		public void AnEmptyOrNullCaptureRefuses()
		{
			Assert.IsNull(Digest(new Dictionary<string, string>(StringComparer.Ordinal)));
			Assert.IsNull(Digest(null));
		}

		[Test]
		public void AnUnknownAuthorityClassRefuses()
		{
			string digest;
			string failure;
			Assert.IsFalse(KingdomScenarioAnchorRules.TryDigest("invented", Capture(), out digest,
				out failure));
			Assert.IsNotEmpty(failure);
		}
	}
}
#endif
