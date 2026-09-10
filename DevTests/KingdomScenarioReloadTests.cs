#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public class KingdomScenarioReloadTests
	{
		[Test]
		public void LiveReloadRefusesRatherThanClaimingColdProcessEvidence()
		{
			string message = KingdomScenarioReload.Refuse(out bool ok);
			Assert.That(ok, Is.False);
			Assert.That(message, Does.Contain("taf-reload-requires-cold-process"));
			Assert.That(message, Does.Contain("reload-descendant"));
			Assert.That(message, Does.Contain("saved, loaded and advanced nothing"));
		}
	}
}
#endif
