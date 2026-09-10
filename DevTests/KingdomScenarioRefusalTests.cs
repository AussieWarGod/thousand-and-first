#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The helper itself, plus source-contract pins on the three verb-provider catch sites it
	/// serves: each site must call it with the site's own prefix rather than concatenate a
	/// literal "prefix: " + error.Message, or a fix here can silently regress in one call site
	/// while this suite still passes on the other two.
	/// </summary>
	public class KingdomScenarioRefusalTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		[TestCase("plain refusal reason")]
		[TestCase("")]
		public void UnprefixedMessageGetsExactlyOneSitePrefix(string reason)
		{
			string result = KingdomScenarioRefusal.Message("taf-example-refused", reason);
			ClassicAssert.AreEqual("taf-example-refused: " + reason, result);
		}

		[TestCase("taf-travel-refused: normal walking was blocked")]
		[TestCase("taf-pause-oracle-refused: pause observation owner changed")]
		[TestCase("taf-container-stress-refused: existing containers exceed fixture bounds")]
		[TestCase("taf-scenario-gate-refused: no fresh plan")]
		[TestCase("taf-x9-refused: minimal lowercase-digit code")]
		public void AlreadyPrefixedMessagePassesThroughUnchanged(string reason)
		{
			string result = KingdomScenarioRefusal.Message("taf-example-refused", reason);
			ClassicAssert.AreEqual(reason, result);
		}

		[Test]
		public void NestedDoublePrefixIsNeverProduced()
		{
			string once = KingdomScenarioRefusal.Message("taf-travel-refused", "normal walking was blocked");
			string twice = KingdomScenarioRefusal.Message("taf-travel-refused", once);
			ClassicAssert.AreEqual(once, twice);
			ClassicAssert.AreEqual(1, CountOccurrences(twice, "taf-travel-refused:"));
		}

		[Test]
		public void ForeignPrefixIsNotReplacedByTheCatchingSite()
		{
			string result = KingdomScenarioRefusal.Message("taf-pause-refused", "taf-travel-refused: blocked route");
			ClassicAssert.AreEqual("taf-travel-refused: blocked route", result);
			ClassicAssert.IsFalse(result.Contains("taf-pause-refused"));
		}

		private static int CountOccurrences(string text, string token)
		{
			int count = 0, index = 0;
			while ((index = text.IndexOf(token, index)) >= 0) { count++; index += token.Length; }
			return count;
		}

		[Test]
		public void ContainerStressCatchUsesTheHelperWithItsOwnPrefix()
		{
			string source = Read("Harness/KingdomScenarioContainerStress.cs");
			StringAssert.Contains(
				"catch (Exception error) { return KingdomScenarioRefusal.Message(\"taf-container-stress-refused\", error.Message); }",
				source);
		}

		[Test]
		public void PauseControllerCatchUsesTheHelperWithItsOwnPrefix()
		{
			string source = Read("Harness/KingdomScenarioPauseController.cs");
			StringAssert.Contains(
				"catch (Exception error) { Stop(); return KingdomScenarioRefusal.Message(\"taf-pause-refused\", error.Message); }",
				source);
		}

		[Test]
		public void TravelProviderCatchUsesTheHelperWithItsOwnPrefix()
		{
			string source = Read("Harness/KingdomScenarioTravelProvider.cs");
			StringAssert.Contains(
				"return KingdomScenarioRefusal.Message(\"taf-travel-refused\", error.Message);",
				source);
		}
	}
}
#endif
