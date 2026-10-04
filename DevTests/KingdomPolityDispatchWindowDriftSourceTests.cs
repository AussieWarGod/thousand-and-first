#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>#244/#257 source pins for the engine-bound scheduler, which TafTests does not
	/// compile. Behaviour is pinned engine-free in KingdomPolityDispatchWindowDriftTests.</summary>
	[TestFixture]
	public sealed class KingdomPolityDispatchWindowDriftSourceTests
	{
		[Test]
		public void WholeCityDigestRefusalIsGone()
		{
			StringAssert.DoesNotContain("open polity topology differs from its frozen facts",
				Read("Polity/KingdomPolityDispatchRules.cs")
				+ Read("Polity/KingdomPolityDispatchRules.Drift.cs"));
		}

		[Test]
		public void SchedulerReportsDriftAndWithdrawalsWithoutRefusalWording()
		{
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			string notes = Read("Polity/KingdomPolitySchedulerRuntime.DispatchNotes.cs");
			StringAssert.Contains("out bool factsDrifted, out List<string> withdrawn", scheduler);
			StringAssert.Contains("NoteDispatch(System, window, factsDrifted, withdrawn)", scheduler);
			StringAssert.Contains("polity: dispatch window ", notes);
			StringAssert.Contains("polity: ", notes);
			StringAssert.DoesNotContain("refused", notes);
			StringAssert.Contains("[System.NonSerialized]",
				Read("Core/KingdomSystem.z02d.State.PolityDispatch.cs"));
			StringAssert.Contains("private ulong PolityDriftNotedWindow",
				Read("Core/KingdomSystem.z02d.State.PolityDispatch.cs"));
		}

		private static string Read(string Relative)
		{
			return File.ReadAllText(Path.Combine(TestMain.RepositoryRoot, Relative));
		}
	}
}
#endif
