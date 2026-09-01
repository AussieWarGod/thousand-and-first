#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture] public sealed class KingdomPactVesselSourceTests
	{
		[Test] public void VesselOwnsOnlyConfirmedBreakAndNonMoralWitnesses()
		{
			string s=TestMain.ReadRepositoryText("Treaty/KingdomPactVesselRuntime.cs");
			StringAssert.Contains("liquid.ManualSeal=false",s);StringAssert.Contains("liquid.Sealed=true",s);
			StringAssert.Contains("RequirePart<Unreplicable>",s);StringAssert.Contains("Popup.ShowYesNo",s);
			StringAssert.Contains("PactWitnessEventKind.DeliberateBreach",s);StringAssert.Contains("BeforeDestroyObjectEvent",s);
			StringAssert.Contains("PactWitnessEventKind.WitnessLost",s);StringAssert.Contains("ObserveTypedTheft",s);
			StringAssert.Contains("string actor=e.Actor.ID",s);StringAssert.DoesNotContain("e.Actor.id",s);
			StringAssert.DoesNotContain("e.Actor.IDIfAssigned",s);
			StringAssert.DoesNotContain("SetFactionFeeling",s);StringAssert.DoesNotContain("Stat.Random",s);
			StringAssert.DoesNotContain("ManualSeal=true",s);StringAssert.DoesNotContain("E.Actor",s.Replace("e.Actor", ""));
		}
	}
}
#endif
