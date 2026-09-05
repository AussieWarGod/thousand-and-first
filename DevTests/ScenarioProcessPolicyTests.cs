#if TAF_TESTS
using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using ThousandAndFirst.Tools;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class ScenarioProcessPolicyTests
	{
		private const string Root = @"C:\taf-scenario.A1b2";
		private const string Executable = @"D:\Steam Library\Caves of Qud\CoQ.exe";

		private static ScenarioProcessIdentity Identity()
		{
			return new ScenarioProcessIdentity
			{
				Pid = 1234, StartTicks = 638900000000000000L, Executable = Executable,
				Arguments = ScenarioProcessPolicy.ExpectedArguments(Root, Executable)
			};
		}

		[Test]
		public void ExpectedArgumentsAreTheExactTwelveParsedTokensIncludingArgvZero()
		{
			Assert.AreEqual("taf-scenario-process-v1", ScenarioProcessPolicy.Schema);
			CollectionAssert.AreEqual(new[] { Executable, "-savepath", Root + @"\Save",
				"-sharedpath", Root + @"\Local", "-syncedpath", Root + @"\Synced",
				"-logFile", Root + @"\Player.log", "NOMETRICS", "STEAM:NO", "GALAXY:NO" },
				ScenarioProcessPolicy.ExpectedArguments(Root, Executable));
		}

		[Test]
		public void ExactIdentityStopsAndOnlyAValidRecordedIdentityMayAlreadyBeExited()
		{
			Assert.IsTrue(ScenarioProcessPolicy.ValidIdentity(Root, Executable, Identity()));
			Assert.AreEqual("STOP_EXACT", ScenarioProcessPolicy.Decide(Root, Executable, Identity(), Identity()));
			Assert.AreEqual("ALREADY_EXITED", ScenarioProcessPolicy.Decide(Root, Executable, Identity(), null));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, null, null));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, null, Identity()));
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void InvalidPidCannotAuthorizeAnAbsentOrPresentProcess(int Pid)
		{
			ScenarioProcessIdentity bad = Identity();
			bad.Pid = Pid;
			AssertRejectedOnEitherSide(bad);
		}

		[TestCase(0L)]
		[TestCase(-1L)]
		[TestCase(long.MaxValue)]
		public void InvalidStartTicksCannotAuthorizeAnAbsentOrPresentProcess(long Ticks)
		{
			ScenarioProcessIdentity bad = Identity();
			bad.StartTicks = Ticks;
			AssertRejectedOnEitherSide(bad);
		}

		[Test]
		public void DateTimeTickBoundsAndLargestPositivePidAreExplicit()
		{
			ScenarioProcessIdentity identity = Identity();
			identity.Pid = int.MaxValue;
			identity.StartTicks = 1;
			Assert.IsTrue(ScenarioProcessPolicy.ValidIdentity(Root, Executable, identity));
			identity.StartTicks = DateTime.MaxValue.Ticks;
			Assert.IsTrue(ScenarioProcessPolicy.ValidIdentity(Root, Executable, identity));
			identity.StartTicks++;
			AssertRejectedOnEitherSide(identity);
		}

		[Test]
		public void ReusedPidOrChangedPidRefusesDespiteOtherwiseExactFacts()
		{
			ScenarioProcessIdentity live = Identity();
			live.StartTicks++;
			Assert.IsTrue(ScenarioProcessPolicy.ValidIdentity(Root, Executable, live));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, Identity(), live));
			live = Identity();
			live.Pid++;
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, Identity(), live));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(6)]
		[TestCase(7)]
		[TestCase(8)]
		[TestCase(9)]
		[TestCase(10)]
		[TestCase(11)]
		public void EveryArgumentSlotIsRequiredAndNeverMatchedBySubstring(int Index)
		{
			ScenarioProcessIdentity bad = Identity();
			bad.Arguments[Index] += "_foreign";
			AssertRejectedOnEitherSide(bad);
			bad.Arguments[Index] = null;
			AssertRejectedOnEitherSide(bad);
		}

		[TestCase(1)]
		[TestCase(3)]
		[TestCase(5)]
		[TestCase(7)]
		[TestCase(9)]
		[TestCase(10)]
		[TestCase(11)]
		public void FlagsAreOrdinalCaseSensitive(int Index)
		{
			ScenarioProcessIdentity bad = Identity();
			bad.Arguments[Index] = Index < 9 ? bad.Arguments[Index].ToUpperInvariant()
				: bad.Arguments[Index].ToLowerInvariant();
			AssertRejectedOnEitherSide(bad);
		}

		[Test]
		public void MissingExtraDuplicateReorderedAndUnparsedArgumentsRefuse()
		{
			ScenarioProcessIdentity bad = Identity();
			bad.Arguments = null;
			AssertRejectedOnEitherSide(bad);
			bad.Arguments = new string[0];
			AssertRejectedOnEitherSide(bad);
			bad.Arguments = new string[11];
			Array.Copy(Identity().Arguments, bad.Arguments, 11);
			AssertRejectedOnEitherSide(bad);
			bad.Arguments = new string[13];
			Array.Copy(Identity().Arguments, bad.Arguments, 12);
			bad.Arguments[12] = "NOMETRICS";
			AssertRejectedOnEitherSide(bad);
			bad = Identity();
			bad.Arguments[3] = bad.Arguments[1];
			bad.Arguments[4] = bad.Arguments[2];
			AssertRejectedOnEitherSide(bad);
			bad = Identity();
			string firstFlag = bad.Arguments[1], firstPath = bad.Arguments[2];
			bad.Arguments[1] = bad.Arguments[3];
			bad.Arguments[2] = bad.Arguments[4];
			bad.Arguments[3] = firstFlag;
			bad.Arguments[4] = firstPath;
			AssertRejectedOnEitherSide(bad);
			bad.Arguments = new[] { string.Join(" ", Identity().Arguments) };
			AssertRejectedOnEitherSide(bad);
		}

		[Test]
		public void ProcessNameAndQuotedOrSpoofedPathArgumentsNeverEstablishOwnership()
		{
			ScenarioProcessIdentity bad = Identity();
			bad.Executable = @"E:\Another Game\CoQ.exe";
			AssertRejectedOnEitherSide(bad);
			bad = Identity();
			bad.Arguments[0] = @"E:\Another Game\CoQ.exe";
			AssertRejectedOnEitherSide(bad);
			bad = Identity();
			bad.Arguments[2] = "\"" + bad.Arguments[2] + "\"";
			AssertRejectedOnEitherSide(bad);
			bad.Arguments[2] = Root + @"-foreign\Save";
			AssertRejectedOnEitherSide(bad);
			bad.Arguments[2] = Root + @"\Save -sharedpath C:\foreign";
			AssertRejectedOnEitherSide(bad);
		}

		[TestCase(@"C:\taf-scenario.a")]
		[TestCase(@"z:\taf-scenario.Z9a012")]
		[TestCase(@"c:\TAF-SCENARIO.X")]
		public void ExactDriveRootWithAsciiAlphanumericSuffixIsAccepted(string Value)
		{
			Assert.IsTrue(ScenarioProcessPolicy.ValidRoot(Value));
			Assert.IsNotNull(ScenarioProcessPolicy.ExpectedArguments(Value, Executable));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(@"C:\")]
		[TestCase(@"C:\taf-scenario.")]
		[TestCase(@"C:\taf-scenario.x\")]
		[TestCase(@"C:\taf-scenario.x\Save")]
		[TestCase(@"C:\other\taf-scenario.x")]
		[TestCase(@"C:\taf-scenario.x-y")]
		[TestCase(@"C:\taf-scenario.x_y")]
		[TestCase(@"C:\taf-scenario.x.")]
		[TestCase(@"C:\taf-scenario.x ")]
		[TestCase(@"C:\taf-scenario.x:stream")]
		[TestCase(@"C:\taf-scenario.é")]
		[TestCase(@"1:\taf-scenario.x")]
		[TestCase(@"é:\taf-scenario.x")]
		[TestCase(@"C:taf-scenario.x")]
		[TestCase(@"C:/taf-scenario.x")]
		[TestCase(@"\\server\taf-scenario.x")]
		[TestCase(@"\\?\C:\taf-scenario.x")]
		[TestCase("C:\\taf-scenario.x\n")]
		public void NoncanonicalOrBroaderRootsRefuseEvenWhenProcessIsAbsent(string Value)
		{
			Assert.IsFalse(ScenarioProcessPolicy.ValidRoot(Value));
			Assert.IsNull(ScenarioProcessPolicy.ExpectedArguments(Value, Executable));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Value, Executable, Identity(), null));
		}

		[TestCase(@"C:\CoQ.exe")]
		[TestCase(@"d:\Steam Library\Caves of Qud\CoQ.exe")]
		[TestCase(@"C:\Jeux été\CoQ.exe")]
		[TestCase(@"\\server\share\CoQ.exe")]
		[TestCase(@"\\server\share$\Games\CoQ.exe")]
		public void CanonicalAbsoluteDriveAndUncExecutablesAreAccepted(string Value)
		{
			ScenarioProcessIdentity identity = Identity();
			identity.Executable = Value;
			identity.Arguments = ScenarioProcessPolicy.ExpectedArguments(Root, Value);
			Assert.IsTrue(ScenarioProcessPolicy.ValidIdentity(Root, Value, identity));
			Assert.AreEqual("STOP_EXACT", ScenarioProcessPolicy.Decide(Root, Value, identity, identity));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(@"CoQ.exe")]
		[TestCase(@"C:CoQ.exe")]
		[TestCase(@"\CoQ.exe")]
		[TestCase(@"C:/Games/CoQ.exe")]
		[TestCase(@"C:\")]
		[TestCase(@"C:\Games\")]
		[TestCase(@"C:\Games\\CoQ.exe")]
		[TestCase(@"C:\.\CoQ.exe")]
		[TestCase(@"C:\Games\..\CoQ.exe")]
		[TestCase(@"C:\Games.\CoQ.exe")]
		[TestCase(@"C:\Games \CoQ.exe")]
		[TestCase(@"C:\Games\CoQ.exe.")]
		[TestCase(@"C:\Games\CoQ.exe ")]
		[TestCase(@"C:\Games\CoQ.exe:stream")]
		[TestCase("C:\\Games\\Co\"Q.exe")]
		[TestCase("C:\\Games\\Co\nQ.exe")]
		[TestCase("C:\\Games\\Co\u0085Q.exe")]
		[TestCase(@"C:\Games\Co*Q.exe")]
		[TestCase(@"C:\Games\Co?Q.exe")]
		[TestCase(@"C:\Games\Co|Q.exe")]
		[TestCase(@"C:\Games\Co<Q.exe")]
		[TestCase(@"C:\Games\Co>Q.exe")]
		[TestCase(@"\\server")]
		[TestCase(@"\\server\share")]
		[TestCase(@"\\server\share\\CoQ.exe")]
		[TestCase(@"\\server\share\..\CoQ.exe")]
		[TestCase(@"\\?\C:\Games\CoQ.exe")]
		[TestCase(@"\\?\UNC\server\share\CoQ.exe")]
		[TestCase(@"\\.\C:\Games\CoQ.exe")]
		[TestCase(@"\??\C:\Games\CoQ.exe")]
		[TestCase(@"C:\Games\CON.exe")]
		[TestCase(@"C:\Games\nul.txt")]
		[TestCase(@"C:\COM1\CoQ.exe")]
		[TestCase(@"C:\Games\LPT9.exe")]
		[TestCase(@"C:\Games\CON .exe")]
		[TestCase(@"C:\Games\CONOUT$")]
		[TestCase("C:\\Games\\COM\u00b9.exe")]
		public void ExecutableAliasesAndMalformedPathsNeverAuthorizeProcessOwnership(string Value)
		{
			Assert.IsNull(ScenarioProcessPolicy.ExpectedArguments(Root, Value));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Value, Identity(), null));
			ScenarioProcessIdentity bad = Identity();
			bad.Executable = Value;
			AssertRejectedOnEitherSide(bad);
		}

		[Test]
		public void CallerRootAndExecutableBindEvenAnAbsentProcessToItsOwnReceipt()
		{
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(@"C:\taf-scenario.Other",
				Executable, Identity(), null));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root,
				@"D:\Other\CoQ.exe", Identity(), null));
		}

		[TestCase("en-US")]
		[TestCase("tr-TR")]
		public void WindowsPathsUseOrdinalIgnoreCaseRegardlessOfCurrentCulture(string Culture)
		{
			CultureInfo previous = Thread.CurrentThread.CurrentCulture;
			try
			{
				Thread.CurrentThread.CurrentCulture = new CultureInfo(Culture);
				ScenarioProcessIdentity live = Identity();
				live.Executable = live.Executable.ToUpperInvariant();
				foreach (int index in new[] { 0, 2, 4, 6, 8 })
					live.Arguments[index] = live.Arguments[index].ToUpperInvariant();
				Assert.AreEqual("STOP_EXACT", ScenarioProcessPolicy.Decide(Root.ToUpperInvariant(),
					Executable, Identity(), live));
				live.Arguments[9] = "nometrics";
				Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, Identity(), live));
			}
			finally { Thread.CurrentThread.CurrentCulture = previous; }
		}

		[Test]
		public void DecisionsDoNotMutateInputsAndArgumentArraysAreNotShared()
		{
			ScenarioProcessIdentity recorded = Identity(), live = Identity();
			string[] recordedArguments = recorded.Arguments, liveArguments = live.Arguments;
			string[] snapshot = (string[])recordedArguments.Clone();
			Assert.AreEqual("STOP_EXACT", ScenarioProcessPolicy.Decide(Root, Executable, recorded, live));
			live.StartTicks++;
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, recorded, live));
			Assert.AreEqual(1234, recorded.Pid);
			Assert.AreEqual(638900000000000000L, recorded.StartTicks);
			Assert.AreEqual(Executable, recorded.Executable);
			Assert.AreSame(recordedArguments, recorded.Arguments);
			Assert.AreSame(liveArguments, live.Arguments);
			CollectionAssert.AreEqual(snapshot, recorded.Arguments);
			CollectionAssert.AreEqual(snapshot, live.Arguments);
			live.Arguments[0] = "changed";
			CollectionAssert.AreEqual(snapshot, recorded.Arguments);
			CollectionAssert.AreEqual(snapshot, ScenarioProcessPolicy.ExpectedArguments(Root, Executable));
		}

		private static void AssertRejectedOnEitherSide(ScenarioProcessIdentity Bad)
		{
			Assert.IsFalse(ScenarioProcessPolicy.ValidIdentity(Root, Executable, Bad));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, Bad, null));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, Bad, Identity()));
			Assert.AreEqual("REFUSE", ScenarioProcessPolicy.Decide(Root, Executable, Identity(), Bad));
		}
	}
}
#endif
