#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>#244/#257 source pins for the engine-bound scheduler and KingdomSystem shard, which
	/// TafTests does not compile. The log lines themselves are built engine-free by
	/// KingdomPolityDispatchRules.DispatchNotes and executed in KingdomPolityDispatchWindowDriftTests;
	/// these pins cover only the wiring that hands those lines to the log.</summary>
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
		public void SchedulerReportsTheWindowBeforeAnyDueWork()
		{
			// A later TryOrderFair, TryPlan or ambient-reservation failure in the same pass must not
			// swallow a withdrawal that TryOpen has already committed.
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			int open = Index(scheduler,
				"out List<KingdomPolityDueWork> work, out bool factsDrifted, out List<string> withdrawn,");
			int note = Index(scheduler, "\t\t\tNoteDispatch(System, window, factsDrifted, withdrawn);\n");
			int order = Index(scheduler, "if (!TryOrderFair(System, work, out Failure)) return false;");
			ClassicAssert.Less(open, note, "the window must reconcile before it is reported");
			ClassicAssert.Less(note, order, "the report must precede every due-work step");
			ClassicAssert.AreEqual(1, Count(scheduler, "NoteDispatch("));
		}

		[Test]
		public void EveryDispatchNoteReachesTheLogAndDriftOnlyWhenTheWindowDrifted()
		{
			string notes = Read("Polity/KingdomPolitySchedulerRuntime.DispatchNotes.cs");
			StringAssert.Contains("List<string> lines = KingdomPolityDispatchRules.DispatchNotes("
				+ "Window, Withdrawn,\n\t\t\t\tFactsDrifted && System.TryNotePolityDrift(Window));", notes);
			StringAssert.Contains("for (int i = 0; i < lines.Count; i++) KingdomLog.Log(lines[i]);",
				notes);
			ClassicAssert.AreEqual(1, Count(notes, "KingdomLog.Log("));
			ClassicAssert.AreEqual(1, Count(notes, "TryNotePolityDrift("));
			StringAssert.DoesNotContain("refused", notes);
		}

		[Test]
		public void DriftNoteDedupeIsPrivateInstanceStateNeverSerialized()
		{
			string state = Read("Core/KingdomSystem.z02d.State.PolityDispatch.cs");
			StringAssert.Contains("[System.NonSerialized]\n\t\tprivate ulong PolityDriftNotedWindow;",
				state);
			StringAssert.Contains("if (PolityDriftNotedWindow == Window + 1UL) return false;", state);
			StringAssert.Contains("PolityDriftNotedWindow = Window + 1UL; return true;", state);
			StringAssert.DoesNotContain("static", state);
		}

		private static int Index(string Source, string Value)
		{
			int at = Source.IndexOf(Value, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(at, 0, "missing: " + Value);
			ClassicAssert.AreEqual(at, Source.LastIndexOf(Value, StringComparison.Ordinal),
				"repeated: " + Value);
			return at;
		}

		private static int Count(string Source, string Value)
		{
			int count = 0;
			for (int at = Source.IndexOf(Value, StringComparison.Ordinal); at >= 0;
				at = Source.IndexOf(Value, at + Value.Length, StringComparison.Ordinal)) count++;
			return count;
		}

		private static string Read(string Relative)
		{
			return File.ReadAllText(Path.Combine(TestMain.RepositoryRoot, Relative));
		}
	}
}
#endif
