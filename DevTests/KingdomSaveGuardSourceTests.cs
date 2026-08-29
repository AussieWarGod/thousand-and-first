#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source-contract coverage for the C17 save-guard repair (SH-1, SH-3 in
	/// <c>_notes/FOUNDATION-RUNTIME-FULL-AUDIT-CLAUDE.md</c>): <c>KingdomSystem.BeforeSave</c>
	/// must veto a save while <c>LoadFailed</c> or <c>RealmIdentityFenceFault</c> is latched, and
	/// the reflected-v1 <c>Read</c> branch must no longer claim to be a working migration bridge.
	/// </summary>
	[TestFixture]
	public class KingdomSaveGuardSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void BeforeSaveOverrideThrowsOnBothLatchesAndNeverClearsThem()
		{
			string guard = Source(Path.Combine("Core", "KingdomSystem.z19b.SaveGuard.cs"));
			StringAssert.Contains("public override void BeforeSave()", guard);

			int method = guard.IndexOf("public override void BeforeSave()", StringComparison.Ordinal);
			string body = guard.Substring(method);
			int loadFailedCheck = body.IndexOf("if (LoadFailed)", StringComparison.Ordinal);
			int loadFailedThrow = body.IndexOf("throw new InvalidOperationException",
				loadFailedCheck, StringComparison.Ordinal);
			int fenceCheck = body.IndexOf(
				"if (!string.IsNullOrEmpty(RealmIdentityFenceFault))", loadFailedThrow,
				StringComparison.Ordinal);
			int fenceThrow = body.IndexOf("throw new InvalidOperationException", fenceCheck,
				StringComparison.Ordinal);
			Assert.Greater(loadFailedCheck, -1, "BeforeSave must read LoadFailed.");
			Assert.Greater(loadFailedThrow, loadFailedCheck);
			Assert.Greater(fenceCheck, loadFailedThrow);
			Assert.Greater(fenceThrow, fenceCheck);

			// The veto is a read-only gate: it must never be the thing that lets a bad load
			// through by quietly repairing the state it is supposed to be refusing.
			Assert.IsFalse(guard.Contains("LoadFailed = "),
				"BeforeSave must never assign LoadFailed.");
			Assert.IsFalse(guard.Contains("RealmIdentityFenceFault = "),
				"BeforeSave must never assign RealmIdentityFenceFault.");
			StringAssert.Contains("This override only reads them", guard);
			StringAssert.Contains("FinalizeWrite", guard);
			StringAssert.Contains("RestoreBackup=false", guard);
		}

		[Test]
		public void ReportingCannotClearFailedAuthorityAndIsOnlyPresentationOnce()
		{
			string persistence = Source(Path.Combine("Core",
				"KingdomSystem.z19a.Serialization.cs"));
			int report = persistence.IndexOf("private void ReportLoadFailure()",
				StringComparison.Ordinal);
			string body = persistence.Substring(report);
			StringAssert.DoesNotContain("LoadFailed = false", body);
			StringAssert.Contains("LoadFailureReportedThisSession", body);
			StringAssert.Contains("if (LoadFailureReportedThisSession) return;", body);
			StringAssert.Contains("[NonSerialized]", persistence);
		}

		[Test]
		public void EveryMasterGateRefusesAFailedRootAuthority()
		{
			string master = Source(Path.Combine("Core", "KingdomMaster.cs"));
			StringAssert.Contains("private static bool RootAuthorityAvailable(KingdomSystem system)",
				master);
			StringAssert.Contains(
				"return system != null && !system.LoadFailed && !system.RealmRetirementBlocksWork;",
				master);
			StringAssert.Contains("if (!RootAuthorityAvailable(system)) return false;", master);
			Assert.AreEqual(2, Count(master, "return RootAuthorityAvailable(system) && ConfiguredEnabled"),
				"both explicit-new-work and automatic-work gates must share the failed-root guard");
		}

		[Test]
		public void ReflectedV1FailureBeforeReadStillLatchesTheRecoveryObject()
		{
			string system = Source(Path.Combine("Core", "KingdomSystem.z19a.Serialization.cs"));
			string callbacks = Source(Path.Combine("Core",
				"KingdomSystem.z19.PersistenceAndCallbacks.cs"));

			// The overclaim SH-1 flagged: a blanket promise that every prior layout is tolerated,
			// and that the reflected branch is how those saves get read, stated directly above a
			// branch that cannot fire for a genuine save.
			Assert.IsFalse(system.Contains("tolerating every layout this mod has ever written"),
				"the summary must not claim unconditional legacy support any more");
			Assert.IsFalse(
				system.Contains("Nothing remains in the block to read, so we return"),
				"the docstring must not claim the reflected branch is how prior saves are read");

			// ReadTypeFields and Read share an engine catch. When the former throws, the latter cannot
			// set LoadFailed. A constructor-default, nonserialized sentinel must survive that path and
			// AfterLoad must latch before it touches the blank recovery object.
			StringAssert.Contains("ICompositeFieldType", system);
			StringAssert.Contains("ReadTypeFields", system);
			StringAssert.Contains("fail before this method runs", system);
			StringAssert.Contains("private bool CustomReadCompleted;", system);
			int reset = system.IndexOf("CustomReadCompleted = false;", StringComparison.Ordinal);
			int normalize = system.IndexOf("NormalizeState(AllowLegacyIdentityMigration: false);",
				reset, StringComparison.Ordinal);
			int completed = system.IndexOf("CustomReadCompleted = true;", normalize,
				StringComparison.Ordinal);
			Assert.Greater(reset, -1);
			Assert.Greater(normalize, reset);
			Assert.Greater(completed, normalize,
				"only a fully normalized custom read may retire the sentinel");
			int afterLoad = callbacks.IndexOf("public override void AfterLoad", StringComparison.Ordinal);
			int refusal = callbacks.IndexOf("if (RefuseIncompleteLoad()) return;", afterLoad,
				StringComparison.Ordinal);
			int afterNormalize = callbacks.IndexOf(
				"NormalizeState(AllowLegacyIdentityMigration: false);", refusal,
				StringComparison.Ordinal);
			Assert.Greater(refusal, afterLoad);
			Assert.Greater(afterNormalize, refusal,
				"the blank recovery object must be refused before normalization");
			StringAssert.Contains("if (!CustomReadCompleted) LoadFailed = true;", system);

			// The branch no longer falls through to a quiet migration: it logs once, then
			// refuses like any other unreadable save, before the named-field path ever begins.
			int reflected = system.IndexOf("SerializationVersion == LegacyReflectedSerializationVersion",
				StringComparison.Ordinal);
			int log = system.IndexOf("MetricsManager.LogError(\"ThousandAndFirst: reached the reflected-v1 branch",
				reflected, StringComparison.Ordinal);
			int branchThrow = system.IndexOf("throw new InvalidOperationException(", log,
				StringComparison.Ordinal);
			int magicRead = system.IndexOf("int magic = Reader.ReadInt32();", branchThrow,
				StringComparison.Ordinal);
			Assert.Greater(reflected, -1);
			Assert.Greater(log, reflected);
			Assert.Greater(branchThrow, log);
			Assert.Greater(magicRead, branchThrow,
				"the branch must throw before falling into the named-field read path");

			// The literal old call is kept only as a historical note inside a comment -- proving
			// this repair changed real behavior, not merely renamed the same success path.
			StringAssert.Contains("called NormalizeState(AllowLegacyIdentityMigration: true)",
				system);
		}

		/// <summary>
		/// Source-level proof of the same claim the audit made and the docstring on
		/// <c>KingdomSystem.Read</c> now states: today's declared field order has drifted from
		/// the frozen v1 root, so the engine's positional <c>ReadTypeFields</c> walk cannot
		/// complete against a real v1 stream without a type mismatch. This checks declaration
		/// order in source rather than by compiled reflection, because <c>KingdomSystem</c> is
		/// not itself referenced by this test project (see audit finding F4) -- the frozen
		/// manifest itself notes a live mirror is useful corroboration but not release evidence
		/// either way, since the shipped runtime's field order is decided there, not here.
		/// </summary>
		[Test]
		public void LiveFieldOrderFailsTheFrozenV1ManifestPrefixMatch()
		{
			using JsonDocument document = JsonDocument.Parse(
				Source(Path.Combine("DevTests", "Compatibility", "KingdomSystemV1Fields.json")));
			JsonElement manifestElement = document.RootElement.GetProperty("eligibleFieldOrder");
			string[] manifest = Enumerable.Range(0, manifestElement.GetArrayLength())
				.Select(i => manifestElement[i].GetString())
				.ToArray();
			Assert.AreEqual(48, manifest.Length, "the checked-in v1 manifest itself has drifted");
			Assert.AreEqual("SerializationVersion", manifest[0]);
			Assert.AreEqual("KingdomFactionName", manifest[1]);

			// v1's manifest puts KingdomFactionName immediately after SerializationVersion.
			// Today's root declares a whole third field, KingdomMasterLatchValue MasterOption,
			// physically between them -- concrete, checkable proof of the interleaving the audit
			// (and the Read docstring) describe, without needing a compiled reflection pass.
			string root = Source(Path.Combine("Core", "KingdomSystem.cs"));
			int serializationVersion = root.IndexOf(
				"public int SerializationVersion = CurrentSerializationVersion;",
				StringComparison.Ordinal);
			int masterOption = root.IndexOf("public KingdomMasterLatchValue MasterOption;",
				serializationVersion, StringComparison.Ordinal);
			int kingdomFactionName = root.IndexOf("public string KingdomFactionName;",
				masterOption, StringComparison.Ordinal);
			Assert.Greater(serializationVersion, -1);
			Assert.Greater(masterOption, serializationVersion,
				"MasterOption must still sit between SerializationVersion and KingdomFactionName");
			Assert.Greater(kingdomFactionName, masterOption,
				"KingdomFactionName must have drifted away from v1's second field position");
		}

		private static int Count(string Text, string Needle)
		{
			int count = 0;
			for (int at = 0; (at = Text.IndexOf(Needle, at, StringComparison.Ordinal)) >= 0;
				at += Needle.Length) count++;
			return count;
		}
	}
}
#endif
