#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The request parser as an untrusted boundary.
	/// <para>
	/// The request is a durable game-state string, so every malformed shape has to produce a named
	/// refusal rather than a lucky success. The old parser split with RemoveEmptyEntries and then
	/// indexed <c>parts[0]</c>, so <c>";"</c> reached an empty array; duplicate parameters silently
	/// overwrote each other; and a second seed replaced the first, letting one request name two
	/// worlds while the gate proved only the last.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioRequestRulesTests
	{
		private static bool Parse(string request, out string key,
			out IDictionary<string, string> selection, out string seed, out string failure)
		{
			return KingdomScenarioRequest.TryParse(request, out key, out selection, out seed,
				out failure);
		}

		private static string Refusal(string request)
		{
			string key;
			IDictionary<string, string> selection;
			string seed;
			string failure;
			Assert.IsFalse(Parse(request, out key, out selection, out seed, out failure),
				"expected a refusal for " + (request ?? "<null>"));
			Assert.IsNotNull(failure);
			Assert.IsNotEmpty(failure);
			Assert.IsNull(key);
			Assert.IsNull(selection);
			Assert.IsNull(seed);
			return failure;
		}

		[Test]
		public void AWellFormedRequestParses()
		{
			string key;
			IDictionary<string, string> selection;
			string seed;
			string failure;
			Assert.IsTrue(Parse("arch-gallery-slice;facing=north;seed=#4242", out key,
				out selection, out seed, out failure), failure);
			Assert.AreEqual("arch-gallery-slice", key);
			Assert.AreEqual("north", selection["facing"]);
			Assert.AreEqual(1, selection.Count);
			Assert.AreEqual("#4242", seed);
		}

		[Test]
		public void AKeyOnlyRequestParsesWithNoSelectionAndNoSeed()
		{
			string key;
			IDictionary<string, string> selection;
			string seed;
			string failure;
			Assert.IsTrue(Parse("arch-gallery-slice", out key, out selection, out seed,
				out failure), failure);
			Assert.AreEqual(0, selection.Count);
			Assert.IsNull(seed);
		}

		// ----- totality: the shapes that used to crash or launder ---------------------------------

		/// <summary>The exact input that indexed an empty array.</summary>
		[Test]
		public void ASeparatorOnlyRequestRefusesRatherThanThrowing()
		{
			StringAssert.Contains("names no key", Refusal(";"));
			StringAssert.Contains("names no key", Refusal(";;;"));
		}

		[TestCase(null)]
		[TestCase("")]
		public void AnAbsentRequestRefuses(string request)
		{
			Refusal(request);
		}

		[Test]
		public void AnEmptySegmentIsVisibleRatherThanDiscarded()
		{
			StringAssert.Contains("empty segment", Refusal("arch-gallery-slice;;facing=north"));
			StringAssert.Contains("empty segment", Refusal("arch-gallery-slice;facing=north;"));
		}

		[Test]
		public void AMalformedKeyRefuses()
		{
			StringAssert.Contains("malformed", Refusal("Arch Gallery;facing=north"));
			StringAssert.Contains("malformed", Refusal("arch/gallery"));
		}

		[TestCase("arch;facing")]
		[TestCase("arch;=north")]
		[TestCase("arch; =north")]
		public void AMalformedParameterRefuses(string request)
		{
			Refusal(request);
		}

		/// <summary>
		/// Whitespace is REFUSED, not trimmed away. Trim-before-SafeToken silently repaired
		/// malformed durable text, which is the same laundering the XML adapter stopped doing.
		/// </summary>
		[TestCase("  arch-gallery-slice;facing=north")]
		[TestCase("arch-gallery-slice ;facing=north")]
		[TestCase("arch-gallery-slice; facing=north")]
		[TestCase("arch-gallery-slice;facing =north")]
		[TestCase("arch-gallery-slice;facing= north")]
		[TestCase("arch-gallery-slice;facing=north ")]
		public void PaddedInputIsRefusedRatherThanNormalized(string request)
		{
			Refusal(request);
		}

		[Test]
		public void AnEmptyParameterValueRefuses()
		{
			StringAssert.Contains("empty value", Refusal("arch;facing="));
		}

		// ----- exactly once: no overwrite, no ambiguity -------------------------------------------

		/// <summary>
		/// The defect: the second value silently replaced the first, so an ambiguous request
		/// resolved instead of refusing.
		/// </summary>
		[Test]
		public void ADuplicateParameterRefusesRatherThanOverwriting()
		{
			StringAssert.Contains("more than once",
				Refusal("arch;facing=north;facing=south"));
		}

		[Test]
		public void ADuplicateSeedRefusesRatherThanOverwriting()
		{
			StringAssert.Contains("more than one seed",
				Refusal("arch;seed=#1;seed=#2"));
		}

		[Test]
		public void AMalformedSeedRefuses()
		{
			Refusal("arch;seed=");
			Refusal("arch;seed=#");
			Refusal("arch;seed=north south");
		}

		// ----- bounded: the cap bounds the scan, not only the verdict ------------------------------

		[Test]
		public void AnOverlongRequestRefusesBeforeSplitting()
		{
			string request = "arch" + new string('x', KingdomScenarioRequest.MaxRequestChars);
			StringAssert.Contains("exceeds", Refusal(request));
		}

		[Test]
		public void AnOverSegmentedRequestRefusesBeforeSplitting()
		{
			string request = "arch";
			for (int i = 0; i <= KingdomScenarioRequest.MaxSegments; i++) request += ";a" + i + "=v";
			StringAssert.Contains("segments", Refusal(request));
		}

		/// <summary>
		/// A very large counted list must be refused by the cap, not walked. If the parser only
		/// judged the result this would still allocate and traverse a hostile request first.
		/// </summary>
		[Test]
		public void AVeryLargeRequestIsRefusedByTheCap()
		{
			string request = "arch" + new string(';', 200000);
			StringAssert.Contains("exceeds", Refusal(request));
		}
	}
}
#endif
