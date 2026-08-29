#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Contracts that cannot be reached from a pure test project, checked against the source that
	/// carries them. Every sweep here reads the whole tree rather than one file, because the
	/// failures these guard against are exactly the ones committed somewhere else.
	/// </summary>
	[TestFixture]
	public sealed class KingdomCuriositySourceTests
	{
		private const string CuratorRuntime = "Experience/KingdomCuriosityRuntime.cs";
		private const string LeadRuntime = "Experience/KingdomCivicLeadRuntime.cs";
		private const string LeadJournal = "Experience/KingdomCivicLeadRuntime.Journal.cs";

		[Test]
		public void CuratorIsAForeignStoreReadOnly()
		{
			string curator = TestMain.ReadRepositoryText(CuratorRuntime);
			string curatorCode = Code(curator);
			StringAssert.Contains("JournalAPI.MapNotes", curator);
			StringAssert.Contains("note.Revealed", curator);
			StringAssert.Contains("StillExact", curator);
			StringAssert.Contains("Lane = KingdomExperienceLane.Curator", curator);
			StringAssert.DoesNotContain("Lane = KingdomExperienceLane.CivicVoices", curator);
			StringAssert.Contains("KingdomCuriosityRules.TryReleaseTerminalAttention", curator);
			string[] forbidden = { ".Reveal(", ".Forget(", "AddMapNote", "AddObservation",
				"DeleteMapNote", "GetZone(", "ZoneManager", "Stat.Random", "Random(" };
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], curatorCode, forbidden[i]);

			string rules = TestMain.ReadRepositoryText("Experience/KingdomCuriosityRules.cs");
			string attention = TestMain.ReadRepositoryText(
				"Experience/KingdomCuriosityRules.Attention.cs");
			StringAssert.Contains("TryGetTerminalAttentionRelease", attention);
			StringAssert.Contains("TryReadAudienceLease", attention);
			StringAssert.Contains("TryReleaseAudience", attention);
			StringAssert.Contains("lease.SourceId != release.SourceId", attention);
			StringAssert.Contains("lease.Lane != KingdomExperienceLane.Curator", attention);
			StringAssert.Contains("SameForeignNote", rules);
		}

		/// <summary>
		/// The projection order, read off the source in the order it appears.
		/// <para>
		/// Preflight decides duplicates and capacity before anything is written. The note is added.
		/// The category cache is invalidated. Only then is the journal's own index asked whether it
		/// resolves our identity to our object, and only after that does the durable record advance
		/// to projected. Every one of those steps is load-bearing, so the test pins their order and
		/// not merely their presence.
		/// </para>
		/// </summary>
		[Test]
		public void ProjectionRunsPreflightAddInvalidateReadbackThenDurableAuthority()
		{
			string project = Method(TestMain.ReadRepositoryText(LeadRuntime),
				"internal static bool TryProject(");
			string[] order =
			{
				"KingdomCivicLeadRules.TryMatchPreparedRow",
				"KingdomCuriosityLeadCommit.TryReadDurableStanding",
				"if (!TryPreflight(",
				"TryAdd(exact",
				"TryRepairIndex(exact",
				"if (!Readback(exact, receipt",
				"KingdomCuriosityLeadCommit.TryCommitProjectedLead",
				"KingdomCivicLeadRules.TryMarkProjected"
			};
			int at = -1;
			for (int i = 0; i < order.Length; i++)
			{
				int found = project.IndexOf(order[i], StringComparison.Ordinal);
				Assert.Greater(found, at, order[i] + " is out of order in TryProject");
				at = found;
			}

			string lead = TestMain.ReadRepositoryText(LeadRuntime)
				+ TestMain.ReadRepositoryText(LeadJournal);
			StringAssert.Contains("notes.Length >= KingdomCivicLeadRules.MaxJournalMapNotes",
				lead, "capacity is judged on the snapshot the duplicates were judged on");
			StringAssert.Contains("NotesByID.TryGetValue", lead);
			StringAssert.Contains("ReferenceEquals(indexed, exact)", lead);
			StringAssert.Contains("Tradable = false", lead);
			StringAssert.Contains("Revealed = true", lead);
			StringAssert.Contains("KingdomDelveLink.TryReadLoadedCompletion", lead);
			StringAssert.Contains("link.FootZoneId", lead);
			StringAssert.Contains("KingdomCuriosityRuntime.TryReserveAttention", lead);
			StringAssert.Contains("KingdomCivicLeadRules.TryReleaseTerminalAttention", lead);
		}

		/// <summary>
		/// Nothing reaches the journal on the strength of a receipt alone. Both proofs &mdash;
		/// that this is the exact prepared row, and that the row is already durable &mdash; sit in
		/// front of every path that could add a note, so a fabricated or stale receipt causes zero
		/// <c>AddMapNote</c> calls.
		/// </summary>
		[Test]
		public void NoJournalWriteHappensBeforeTheReceiptIsProvenExactAndDurable()
		{
			string project = Method(TestMain.ReadRepositoryText(LeadRuntime),
				"internal static bool TryProject(");
			int matched = project.IndexOf("KingdomCivicLeadRules.TryMatchPreparedRow",
				StringComparison.Ordinal);
			int durable = project.IndexOf(
				"KingdomCuriosityLeadCommit.TryReadDurableStanding",
				StringComparison.Ordinal);
			int add = project.IndexOf("TryAdd(exact", StringComparison.Ordinal);
			Assert.Greater(matched, 0); Assert.Greater(durable, matched);
			Assert.Greater(add, durable, "no journal write may precede either proof");
			StringAssert.DoesNotContain("AddMapNote", Code(project),
				"TryProject must reach the journal only through the guarded helper");
			StringAssert.Contains("out failure)) return false;",
				project.Substring(matched, add - matched),
				"each proof must return on failure rather than merely record one");
		}

		/// <summary>
		/// <c>AddMapNote</c> is not atomic: it appends to <c>MapNotes</c> before it registers the
		/// note or files it by zone, so a throw part-way through can leave the note in the list
		/// with the category cache still standing. The invalidation is therefore in a
		/// <c>finally</c>, and runs on the failure path as well as the success one.
		/// </summary>
		[Test]
		public void APartialAddMapNoteStillInvalidatesTheCategoryCache()
		{
			string add = Method(TestMain.ReadRepositoryText(LeadJournal),
				"private static bool TryAdd(");
			int call = add.IndexOf("JournalAPI.AddMapNote(exact)", StringComparison.Ordinal);
			int caught = add.IndexOf("catch", StringComparison.Ordinal);
			int finallyAt = add.IndexOf("finally", StringComparison.Ordinal);
			int invalidate = add.IndexOf("InvalidateJournalCaches()", StringComparison.Ordinal);
			Assert.Greater(call, 0, "the add itself");
			Assert.Greater(caught, call, "the throw is caught");
			Assert.Greater(finallyAt, caught,
				"the cache repair must be a finally, not a line on the success path");
			Assert.Greater(invalidate, finallyAt);
			StringAssert.Contains("JournalAPI._mapNoteCategories = null",
				Method(TestMain.ReadRepositoryText(LeadJournal),
					"private static void InvalidateJournalCaches("),
				"the helper the finally calls is what actually drops the category cache");
			StringAssert.DoesNotContain("return true;",
				add.Substring(call, finallyAt - call),
				"nothing may return between the add and the cache repair");
		}

		/// <summary>
		/// Success is durable. The projection is recorded in the save before this returns true, so
		/// there is no path on which a caller is told the journal and the record agree while the
		/// record has only moved in memory.
		/// </summary>
		[Test]
		public void ProjectionReturnsTrueOnlyAfterTheDurableCommitIsTaken()
		{
			string project = Method(TestMain.ReadRepositoryText(LeadRuntime),
				"internal static bool TryProject(");
			int commit = project.IndexOf("KingdomCuriosityLeadCommit.TryCommitProjectedLead",
				StringComparison.Ordinal);
			int inMemory = project.IndexOf("KingdomCivicLeadRules.TryMarkProjected",
				StringComparison.Ordinal);
			Assert.Greater(commit, 0, "the durable commit");
			Assert.Greater(inMemory, commit,
				"the caller's book may only be advanced after the save has taken the projection");
			StringAssert.Contains("out failure)) return false;",
				project.Substring(commit, inMemory - commit),
				"a refused durable commit must stop the projection, not be noted and passed");
			StringAssert.DoesNotContain("in memory only", project);

			// And past that commit the answer is settled. Synchronising the caller's own copy of
			// the book is a courtesy: its refusal is reported, never returned, because a caller
			// told this failed would retry work the save and the journal both already agree on.
			string sync = project.Substring(inMemory);
			StringAssert.DoesNotContain("return KingdomCivicLeadRules.TryMarkProjected", project,
				"the caller-copy sync must not be this method's answer");
			StringAssert.Contains("out string sync);", sync,
				"the sync reports into its own name, not into the method's failure");
			StringAssert.Contains("return true;", sync,
				"durable save plus journal agreement is the terminal truth");
		}

		/// <summary>
		/// The journal's map-note list is public and mutable. Preflight bounds it once, copies it
		/// once, and asks every question of the copy, so capacity and duplicates are decided about
		/// the same journal rather than two readings of a moving one.
		/// </summary>
		[Test]
		public void PreflightBoundsAndSnapshotsTheJournalOnceBeforeInspectingIt()
		{
			string preflight = Method(TestMain.ReadRepositoryText(LeadJournal),
				"private static bool TryPreflight(");
			int bound = preflight.IndexOf(
				"if (live.Count > KingdomCivicLeadRules.MaxJournalMapNotes)",
				StringComparison.Ordinal);
			int snapshot = preflight.IndexOf("live.ToArray()", StringComparison.Ordinal);
			Assert.Greater(bound, 0, "the list is bounded before it is copied");
			Assert.Greater(snapshot, bound, "the copy is taken after the bound");
			int bind = preflight.IndexOf("JournalAPI.MapNotes", StringComparison.Ordinal);
			Assert.Greater(bind, 0, "the list is bound to a local");
			Assert.AreEqual(-1,
				preflight.IndexOf("JournalAPI.MapNotes", bind + 1, StringComparison.Ordinal),
				"the journal's map-note property may be named exactly once, at the binding");
		}

		/// <summary>
		/// A half-finished add is repaired through the engine's own public registration, and only
		/// where the identity is free. Nothing writes the index directly and nothing deletes.
		/// </summary>
		[Test]
		public void APartialAppendIsRepairedThroughThePublicRegistrationAndNeverOverAConflict()
		{
			string repair = Method(TestMain.ReadRepositoryText(LeadJournal),
				"private static bool TryRepairIndex(");
			int lookup = repair.IndexOf("JournalAPI.NotesByID.TryGetValue",
				StringComparison.Ordinal);
			int refuse = repair.IndexOf("holds another entry under this identity",
				StringComparison.Ordinal);
			int register = repair.IndexOf("JournalAPI.AddedNote(standing)",
				StringComparison.Ordinal);
			Assert.Greater(lookup, 0, "the identity is looked up first");
			Assert.Greater(refuse, lookup, "an occupied identity is refused");
			Assert.Greater(register, refuse, "registration happens only past that refusal");
			StringAssert.Contains("finally { InvalidateJournalCaches(); }", repair);

			string lead = Code(TestMain.ReadRepositoryText(LeadJournal));
			StringAssert.DoesNotContain("NotesByID[", lead);
			StringAssert.DoesNotContain("NotesByID.Add", lead);
			StringAssert.DoesNotContain("NotesByID.Remove", lead);
			StringAssert.DoesNotContain("MapNotes.Add", lead);
			StringAssert.DoesNotContain("MapNotes.Remove", lead);
		}

		/// <summary>
		/// Both journal map caches are dropped after an add or a repair, through the engine's own
		/// public reset, and the specific category promise is kept as its own line.
		/// </summary>
		[Test]
		public void BothJournalMapCachesAreDroppedThroughThePublicReset()
		{
			string invalidate = Method(TestMain.ReadRepositoryText(LeadJournal),
				"private static void InvalidateJournalCaches(");
			int category = invalidate.IndexOf("JournalAPI._mapNoteCategories = null",
				StringComparison.Ordinal);
			int init = invalidate.IndexOf("JournalAPI.Init()", StringComparison.Ordinal);
			Assert.Greater(category, 0,
				"the category promise stays explicit and does not lean on Init alone");
			Assert.Greater(init, category,
				"Init drops the zone index too, which the object overload never files on a throw");

			string lead = TestMain.ReadRepositoryText(LeadJournal);
			foreach (string caller in new[] { "private static bool TryAdd(",
				"private static bool TryRepairIndex(" })
				StringAssert.Contains("finally { InvalidateJournalCaches(); }",
					Method(lead, caller), caller + " must drop the caches on every path");
		}

		/// <summary>
		/// Two guards that a lawful book can never reach, pinned against the source because there
		/// is no input that can exercise them.
		/// <para>
		/// The writer's own cap is the exact arithmetic, not the wider accepted one: a book built
		/// from valid rows cannot exceed the exact total, so the comparison can only ever fire on
		/// a defect in the arithmetic itself &mdash; which is precisely when it must fire, and
		/// precisely when no test could have supplied the input. And the durable commit must be
		/// <i>returned</i>, not merely called: a refused CAS that was invoked and ignored looks
		/// identical to a successful one from every angle except this line.
		/// </para>
		/// </summary>
		[Test]
		public void TheWriterCapAndTheDurableCommitAreShapedTheOnlyWayTheyCanBe()
		{
			string curiosity = TestMain.ReadRepositoryText(
				"Experience/KingdomCuriosityLeadCodec.Curiosity.cs");
			StringAssert.Contains("if (written.Length > ExactCuriosityBookBytes)", curiosity,
				"what this build writes is bounded by the exact arithmetic, not by what it accepts");
			StringAssert.DoesNotContain("if (written.Length > MaxCuriosityBookBytes)", curiosity);
			string leads = TestMain.ReadRepositoryText(
				"Experience/KingdomCuriosityLeadCodec.Leads.cs");
			StringAssert.Contains("if (written.Length > ExactLeadBookBytes)", leads);
			StringAssert.DoesNotContain("if (written.Length > MaxLeadBookBytes)", leads);

			string projection = Method(TestMain.ReadRepositoryText(
				"Experience/KingdomCuriosityLeadCommit.Projection.cs"),
				"public static bool TryCommitProjectedLead(");
			StringAssert.Contains("return authority.TryCommitSection(lease, bytes, out failure);",
				projection, "the authority's answer is the method's answer");
			StringAssert.DoesNotContain("authority.TryCommit(", projection,
				"the lease API is used whole, never its ingredients");
		}

		/// <summary>
		/// Section five is opened exactly once across a whole projection, and the lease that
		/// opening produced is the object the commit is made under.
		/// <para>
		/// A projection reads the record, writes a note into the founder's journal, and records
		/// that the note was made. If the section were opened a second time for that last step,
		/// every decision taken before the note would have been taken about a save that may since
		/// have moved &mdash; which is the exact window a lease exists to close. So the read hands
		/// the lease back, the runtime carries it across the journal, and the commit is given that
		/// same object rather than a fresh reading of the same bytes.
		/// </para>
		/// </summary>
		[Test]
		public void SectionFiveIsOpenedOnceAndTheSameLeaseObjectIsCommittedUnder()
		{
			string projection = TestMain.ReadRepositoryText(
				"Experience/KingdomCuriosityLeadCommit.Projection.cs");
			string code = Code(projection);
			Assert.AreEqual(1, Occurrences(code, "authority.TryReadSection("),
				"the civic-lead section may be opened exactly once in this file");
			StringAssert.Contains("authority.TryReadSection(", Method(projection,
				"public static bool TryReadDurableStanding("));
			StringAssert.DoesNotContain("TryReadSection", Method(projection,
				"public static bool TryCommitProjectedLead("),
				"the commit rides the lease it was handed and opens nothing");
			StringAssert.Contains("authority.TryCommitSection(lease, bytes, out failure)",
				projection);
			StringAssert.DoesNotContain("authority.Read()", code,
				"the lease carries the bytes; nothing here reads the authority a second time");
			StringAssert.DoesNotContain("authority.Revision", code,
				"the lease carries the revision; nothing here asks for it separately");
			StringAssert.Contains("lease.ExpectedRevision != expectedRevision", projection);
			StringAssert.Contains("lease.Payload()", projection);

			// The runtime opens one lease before the journal and passes that same local in after.
			string project = Method(TestMain.ReadRepositoryText(LeadRuntime),
				"internal static bool TryProject(");
			StringAssert.Contains("out KingdomCivicMemorySectionLease lease", project,
				"the projection holds the lease it read with");
			StringAssert.Contains(
				"KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease, receipt",
				project, "the very same lease object is what the commit is made under");
			Assert.AreEqual(1, Occurrences(Code(project), "TryReadDurableStanding"),
				"one durable read per projection");
		}

		private static int Occurrences(string text, string needle)
		{
			int count = 0;
			for (int at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0;
				at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
			return count;
		}

		/// <summary>The source text of one method,		/// <summary>The source text of one method, so an ordering claim is about that method
		/// rather than about where its helpers happen to be defined in the file.</summary>
		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.Greater(start, 0, "cannot find " + signature);
			int depth = 0;
			for (int i = source.IndexOf('{', start); i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0)
					return source.Substring(start, i - start + 1);
			}
			Assert.Fail("unbalanced braces after " + signature);
			return null;
		}

		/// <summary>
		/// A conflicting note is never replaced and never deleted. The lead stays prepared, which
		/// is the only state a founder can be told about and a later run can finish from.
		/// </summary>
		[Test]
		public void AConflictingJournalNoteIsLeftAloneAndTheLeadStaysPrepared()
		{
			string lead = Code(TestMain.ReadRepositoryText(LeadRuntime)
				+ TestMain.ReadRepositoryText(LeadJournal));
			StringAssert.Contains("stays prepared", lead);
			string[] forbidden = { "DeleteMapNote", ".Reveal(", ".Forget(", "Tradable = true",
				"MapNotes.Remove", "MapNotes.Clear", "NotesByID[", "NotesByID.Remove", "Random" };
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], lead, forbidden[i]);

			string cause = Method(TestMain.ReadRepositoryText(LeadRuntime),
				"internal static bool TryCauseFromCompletedDelve(");
			StringAssert.Contains("KingdomDelveLink.TryReadLoadedCompletion", cause);
			StringAssert.DoesNotContain("GetZone(", Code(cause));
			StringAssert.DoesNotContain("PhysicalLinkStands(", Code(cause));
			string loaded = Method(TestMain.ReadRepositoryText(
				"Growth/KingdomDelveLink.02b.LoadedCompletionProof.cs"),
				"public static bool TryReadLoadedCompletion(");
			StringAssert.Contains("CachedZones", loaded);
			StringAssert.DoesNotContain("GetZone(", Code(loaded),
				"loaded-only completion proof may inspect cache but never thaw either endpoint");
		}

		[Test]
		public void RuntimePresentationAndRecoveryStayCurrentLoadedAndStaffed()
		{
			string presentation = Method(TestMain.ReadRepositoryText(
				"Experience/KingdomCivicKnowledgeRuntime.Presentation.cs"),
				"internal static bool TryProveCuratorPresentation(");
			string[] exact = { "The.Player?.CurrentZone", "SettlementIdForOwnedZone",
				"KingdomLocusRules.SelectLocusWork", "benches != 1",
				"KingdomStaffNeeded", "KingdomStaffed", "receipt.CuratorResidentId",
				"receipt.CuratorObjectId", "curator.DistanceTo(bench)", "keepers == 1" };
			for (int i = 0; i < exact.Length; i++) StringAssert.Contains(exact[i], presentation);
			StringAssert.DoesNotContain("GetZone(", Code(presentation));
			StringAssert.DoesNotContain("KingdomSurvey.Take", Code(presentation));

			string reconcile = Method(TestMain.ReadRepositoryText(
				"Experience/KingdomCivicKnowledgeRuntime.Leads.cs"),
				"internal static void ReconcileLoadedDelveBestEffort(");
			StringAssert.Contains("ReferenceEquals(zone, The.Player?.CurrentZone)", reconcile);
			StringAssert.Contains("TryHeadForLoadedZone(zone.ZoneID", reconcile);
			StringAssert.Contains("TryReadLoadedCompletion", reconcile);
			StringAssert.DoesNotContain("KingdomSurvey.Take", Code(reconcile));
			StringAssert.DoesNotContain("GetZone(", Code(reconcile));

			string ui = TestMain.ReadRepositoryText(
				"Experience/KingdomCivicKnowledgeRuntime.UI.cs")
				+ TestMain.ReadRepositoryText(
					"Experience/KingdomCivicKnowledgeRuntime.UI.Actions.cs");
			StringAssert.Contains("Settlement simulation is paused", ui);
			StringAssert.Contains("No Journal entry was removed", ui);
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit", Code(ui));
		}

		[Test]
		public void RuntimeCommitReadbackAndRollbackUseExactC18Truth()
		{
			string curiosity = Method(TestMain.ReadRepositoryText(
				"Experience/KingdomCivicKnowledgeRuntime.Curiosity.cs"),
				"internal static bool TryObserveFirstFeast(");
			int commit = curiosity.IndexOf("KingdomCuriosityLeadTransactions.TryCommit",
				StringComparison.Ordinal);
			int readback = curiosity.IndexOf(
				"KingdomCuriosityLeadTransactions.TryReadExactCuriosity", StringComparison.Ordinal);
			Assert.Greater(commit, 0); Assert.Greater(readback, commit);

			string release = Method(TestMain.ReadRepositoryText(
				"Experience/KingdomCivicKnowledgeRuntime.Store.cs"),
				"internal static void ReleaseProvisionalAttentionIfAbsent(");
			int absence = release.IndexOf("TryProveSourceAbsent", StringComparison.Ordinal);
			int releaseCall = release.IndexOf("TryReleaseAudience", StringComparison.Ordinal);
			Assert.Greater(absence, 0); Assert.Greater(releaseCall, absence);
			StringAssert.Contains("|| !absent", release.Substring(absence,
				releaseCall - absence));

			string lead = Method(TestMain.ReadRepositoryText(
				"Experience/KingdomCivicKnowledgeRuntime.Leads.cs"),
				"internal static bool TryObserveCompletedDelve(");
			commit = lead.IndexOf("KingdomCuriosityLeadTransactions.TryCommit",
				StringComparison.Ordinal);
			readback = lead.IndexOf("KingdomCuriosityLeadTransactions.TryReadExactLead",
				StringComparison.Ordinal);
			Assert.Greater(commit, 0); Assert.Greater(readback, commit);
		}

		/// <summary>
		/// The cache contract, and why it is a repo-wide sweep rather than a file check.
		/// <para>
		/// <c>JournalAPI.AddMapNote(JournalMapNote)</c> is the one overload that does not clear
		/// <c>_mapNoteCategories</c>, and a lead is built already revealed so
		/// <c>JournalMapNote.Reveal</c> &mdash; which would have cleared it &mdash; never runs.
		/// A second call site added anywhere in this mod would leave the same stale cache, and
		/// checking only the file that gets it right today would notice nothing. So the sweep
		/// finds every object-overload call in the tree and requires each one to invalidate.
		/// </para>
		/// </summary>
		[Test]
		public void EveryObjectOverloadAddMapNoteCallInTheTreeInvalidatesTheCategoryCache()
		{
			// AddMapNote(<identifier>) -- the object overload. The string overload always opens
			// with a quoted zone id or a variable followed by a comma, and clears the cache itself.
			Regex objectOverload = new Regex(@"AddMapNote\(\s*[A-Za-z_][A-Za-z0-9_.]*\s*\)");
			List<string> callSites = new List<string>();
			List<string> offenders = new List<string>();

			foreach (string path in Sources())
			{
				string text = Code(File.ReadAllText(path));
				foreach (Match match in objectOverload.Matches(text))
				{
					string shown = Shown(path);
					callSites.Add(shown);
					int after = match.Index + match.Length;
					string tail = text.Substring(after,
						Math.Min(400, text.Length - after));
					if (tail.IndexOf("_mapNoteCategories = null", StringComparison.Ordinal) < 0
						&& tail.IndexOf("InvalidateJournalCaches()", StringComparison.Ordinal) < 0)
						offenders.Add(shown);
				}
			}

			CollectionAssert.IsEmpty(offenders,
				"every AddMapNote(note) call must clear JournalAPI._mapNoteCategories, because "
					+ "that overload does not and a preset Revealed note never reaches Reveal()");
			CollectionAssert.Contains(callSites, LeadJournal.Replace('/',
				Path.DirectorySeparatorChar));
			Assert.AreEqual(1, callSites.Count,
				"this mod has exactly one object-overload call site; a new one must be reviewed "
					+ "against the same cache contract");
		}

		/// <summary>Invalidating that cache is the engine's own idiom, and this mod does it in
		/// exactly one place. A write anywhere else would be a second owner of the same repair.</summary>
		[Test]
		public void OnlyTheProjectionSiteWritesTheJournalCategoryCache()
		{
			Regex write = new Regex(@"_mapNoteCategories\s*=(?!=)");
			List<string> writers = new List<string>();
			foreach (string path in Sources())
				if (write.IsMatch(Code(File.ReadAllText(path)))) writers.Add(Shown(path));
			CollectionAssert.AreEqual(
				new[] { LeadJournal.Replace('/', Path.DirectorySeparatorChar) }, writers);
		}

		/// <summary>
		/// No file in this mod removes, hides, un-reveals or makes tradable a journal entry it did
		/// not itself create, and no O6/D7 file touches another family's knowledge at all.
		/// </summary>
		[Test]
		public void NoO6D7FileMutatesUnrelatedJournalKnowledge()
		{
			string[] owned =
			{
				"Experience/KingdomCuriosityModels.cs",
				"Experience/KingdomCuriosityRules.cs",
				"Experience/KingdomCuriosityRules.Locator.cs",
				"Experience/KingdomCuriosityRules.Validation.cs",
				"Experience/KingdomCuriosityRules.Attention.cs",
				"Experience/KingdomCuriosityRuntime.cs",
				"Experience/KingdomCivicLeadModels.cs",
				"Experience/KingdomCivicLeadRules.cs",
				"Experience/KingdomCivicLeadRules.Attention.cs",
				"Experience/KingdomCivicLeadRuntime.cs",
				"Experience/KingdomCivicLeadRuntime.Journal.cs",
				"Experience/KingdomCuriosityLeadCodec.cs",
				"Experience/KingdomCuriosityLeadCodec.Frame.cs",
				"Experience/KingdomCuriosityLeadCodec.Primitives.cs",
				"Experience/KingdomCuriosityLeadCodec.Curiosity.cs",
				"Experience/KingdomCuriosityLeadCodec.Leads.cs",
				"Experience/KingdomCuriosityLeadCommit.cs",
				"Experience/KingdomCuriosityLeadCommit.Projection.cs",
				"Experience/KingdomCuriosityLeadTransactions.cs",
				"Experience/KingdomCuriosityLeadTransactions.Lifecycle.cs",
				"Experience/KingdomCivicKnowledgeRuntime.Store.cs",
				"Experience/KingdomCivicKnowledgeRuntime.Curiosity.cs",
				"Experience/KingdomCivicKnowledgeRuntime.Leads.cs",
				"Experience/KingdomCivicKnowledgeRuntime.Presentation.cs",
				"Experience/KingdomCivicKnowledgeRuntime.UI.cs",
				"Experience/KingdomCivicKnowledgeRuntime.UI.Actions.cs"
			};
			string[] forbidden = { "DeleteMapNote", "MapNotes.Remove", "MapNotes.Clear",
				"NotesByID.Remove", "NotesByID.Clear", "TryRevealNote", "AddObservation",
				"AddAccomplishment", "AddRecipeNote", "SultanNotes", "GeneralNotes",
				"VillageNotes", "RecipeNotes", "Observations", "Accomplishments",
				"Tradable = true", "AwardXP", "GiveReward", "AwardReward" };
			for (int f = 0; f < owned.Length; f++)
			{
				string text = Code(TestMain.ReadRepositoryText(owned[f]));
				for (int i = 0; i < forbidden.Length; i++)
					StringAssert.DoesNotContain(forbidden[i], text, owned[f] + " / " + forbidden[i]);
			}
			Assert.AreEqual(owned.Length, OwnedFiles().Count,
				"a new O6/D7 production file must be added to this list and to the line law");
		}

		/// <summary>Every production file in this family stays readable. The repository's rule is
		/// strictly fewer than three hundred physical lines.</summary>
		[Test]
		public void EveryO6D7ProductionFileIsUnderThreeHundredLines()
		{
			List<string> over = new List<string>();
			foreach (string path in OwnedFiles())
			{
				int lines = File.ReadAllLines(path).Length;
				if (lines >= 300) over.Add(Shown(path) + " (" + lines + ")");
			}
			CollectionAssert.IsEmpty(over);
		}

		/// <summary>
		/// The caps this family declares are the caps civic memory mirrors. When these disagree,
		/// the mirror is stale and <c>KingdomCivicMemoryDerivation.Verify</c> will veto the save,
		/// so a drift is reported here rather than discovered at the next write.
		/// </summary>
		[Test]
		public void TheCivicMemoryMirrorStillQuotesThisFamilysOwnCaps()
		{
			Assert.AreEqual(KingdomCuriosityLeadCodec.MaxCuriosityBookBytes,
				KingdomCivicMemoryLimits.MaxCuriosityBytes,
				"civic memory's curiosity cap no longer equals this family's own");
			Assert.AreEqual(KingdomCuriosityLeadCodec.MaxLeadBookBytes,
				KingdomCivicMemoryLimits.MaxCivicLeadsBytes,
				"civic memory's civic-lead cap no longer equals this family's own");
		}

		private static List<string> OwnedFiles()
		{
			List<string> files = new List<string>();
			string experience = Path.Combine(TestMain.RepositoryRoot, "Experience");
			foreach (string path in Directory.GetFiles(experience, "*.cs"))
			{
				string name = Path.GetFileName(path);
				if (name.StartsWith("KingdomCuriosity", StringComparison.Ordinal)
					|| name.StartsWith("KingdomCivicLead", StringComparison.Ordinal)
					|| name.StartsWith("KingdomCivicKnowledge", StringComparison.Ordinal))
					files.Add(path);
			}
			files.Sort(StringComparer.Ordinal);
			return files;
		}

		/// <summary>
		/// Every production source in the tree. Tests are excluded on purpose: this file quotes
		/// the very call and the very assignment it is sweeping for, and a sweep that found its
		/// own assertions would report the law as broken by the law.
		/// </summary>
		private static IEnumerable<string> Sources()
		{
			string tests = Path.Combine(TestMain.RepositoryRoot, "DevTests")
				+ Path.DirectorySeparatorChar;
			List<string> files = new List<string>();
			foreach (string path in Directory.GetFiles(TestMain.RepositoryRoot, "*.cs",
				SearchOption.AllDirectories))
				if (!path.StartsWith(tests, StringComparison.Ordinal)) files.Add(path);
			return files;
		}

		private static string Shown(string path)
		{
			return path.Substring(TestMain.RepositoryRoot.Length)
				.TrimStart(Path.DirectorySeparatorChar);
		}

		/// <summary>
		/// Source with its commentary removed.
		/// <para>
		/// These files cite the very engine methods they are forbidden to call &mdash; naming
		/// <c>DeleteMapNote</c> and the <c>AddMapNote</c> overloads is how the reasoning is
		/// evidenced &mdash; so a sweep over raw text would convict the explanation instead of the
		/// code. It reads what compiles.
		/// </para>
		/// </summary>
		private static string Code(string text)
		{
			string[] lines = text.Split('\n');
			System.Text.StringBuilder code = new System.Text.StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				int comment = lines[i].IndexOf("//", StringComparison.Ordinal);
				code.Append(comment < 0 ? lines[i] : lines[i].Substring(0, comment));
				code.Append('\n');
			}
			return code.ToString();
		}
	}
}
#endif
