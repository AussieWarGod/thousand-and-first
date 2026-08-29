#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The D6 contracts that live in engine-coupled source the pure projects cannot compile: the
	/// Charter route, the one-lease transaction, the single governance commit, and the sweep that
	/// proves nothing in this family ever touches the original.
	/// </summary>
	[TestFixture]
	public sealed class KingdomArtifactRecognitionSourceTests
	{
		private static readonly string[] Engine =
		{
			"Experience/KingdomArtifactRecognitionCharterRuntime.cs",
			"Experience/KingdomArtifactRecognitionCharterRuntime.Choose.cs",
			"Experience/KingdomArtifactRecognitionCharterRuntime.Commit.cs",
			"Experience/KingdomArtifactRecognitionSelectionRuntime.cs",
			"Core/KingdomArtifactRecognitionRuntime.cs"
		};

		private static readonly string[] Pure =
		{
			"Experience/KingdomArtifactRecognitionSelectionRuntime.Prepare.cs",
			"Experience/KingdomArtifactRecognitionPlan.cs",
			"Experience/KingdomArtifactRecognitionRegister.cs",
			"Experience/KingdomArtifactRecognitionLease.cs",
			"Experience/KingdomArtifactRecognitionCommit.cs",
			"Core/KingdomArtifactRecognitionModels.cs",
			"Core/KingdomArtifactRecognitionRules.cs",
			"Core/KingdomArtifactRecognitionCodec.cs"
		};

		private static string Read(string Relative)
		{
			return TestMain.ReadRepositoryText(Relative);
		}

		private static string Family()
		{
			string text = "";
			for (int i = 0; i < Engine.Length; i++) text += Read(Engine[i]);
			for (int i = 0; i < Pure.Length; i++) text += Read(Pure[i]);
			return text;
		}

		/// <summary>
		/// The file with its comments removed. A sweep that forbids naming a dangerous API must
		/// still let the source explain, in prose, why that API is dangerous.
		/// </summary>
		private static string CodeOnly(string Source)
		{
			string withoutBlocks = Regex.Replace(Source, @"/\*.*?\*/", " ",
				RegexOptions.Singleline);
			return Regex.Replace(withoutBlocks, @"//[^\n]*", " ");
		}

		private static int Occurrences(string Text, string Needle)
		{
			int count = 0;
			int at = 0;
			while ((at = Text.IndexOf(Needle, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += Needle.Length;
			}
			return count;
		}

		private static string Method(string Source, string Signature)
		{
			int start = Source.IndexOf(Signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing method: " + Signature);
			int open = Source.IndexOf('{', start + Signature.Length);
			Assert.GreaterOrEqual(open, 0, "missing method body: " + Signature);
			int depth = 0;
			for (int i = open; i < Source.Length; i++)
			{
				if (Source[i] == '{') depth++;
				else if (Source[i] == '}' && --depth == 0)
					return Source.Substring(open, i - open + 1);
			}
			throw new InvalidOperationException("unclosed method: " + Signature);
		}

		/// <summary>
		/// The whole point of the lane: one player-invoked Charter verb whose handler reaches the
		/// civic-artifacts section of civic memory, with nothing between them that could be a stub.
		/// </summary>
		[Test]
		public void OneCharterVerbReachesTheCivicArtifactsSectionEndToEnd()
		{
			string menu = Read("Core/KingdomCharterMenuRules.cs");
			StringAssert.Contains("CivicCommitments = 46,", menu);
			StringAssert.Contains("RecognizeArtifact = 47", menu);
			StringAssert.Contains("ordinals", menu.Substring(
				menu.IndexOf("CivicCommitments = 46,", StringComparison.Ordinal)));
			StringAssert.Contains("KingdomCharterMenuRoute.ForAction(\"What this city remembers of "
				+ "your travels\", 'a', KingdomCharterAction.RecognizeArtifact)", menu);

			string part = Read("Core/KingdomCharterPart.cs");
			StringAssert.Contains("case KingdomCharterAction.RecognizeArtifact: "
				+ "KingdomArtifactRecognitionCharterRuntime.Open(System, ParentObject); break;",
				part);
			Assert.AreEqual(1, Occurrences(part,
				"KingdomArtifactRecognitionCharterRuntime.Open("),
				"D6 has exactly one Charter entry point");

			string open = Read(Engine[0]);
			StringAssert.Contains("KingdomArtifactRecognitionLease.TryReadAuthority(ground.Memory, "
				+ "ground.RealmId,", open);
			StringAssert.Contains("The.Game?.GetSystem<KingdomCivicMemorySystem>()", open);
			StringAssert.Contains("Recognize(ground, lease, held);", open);

			string commit = Read(Engine[2]);
			StringAssert.Contains("KingdomArtifactRecognitionCommit.TryCommitPlanned(Ground.Memory, "
				+ "Lease,", commit);

			string seam = Read("Experience/KingdomArtifactRecognitionCommit.cs");
			StringAssert.Contains("Authority.TryCommitSection(Lease, bytes, out Failure)", seam);
			string lease = Read("Experience/KingdomArtifactRecognitionLease.cs");
			StringAssert.Contains("SectionId = KingdomCivicMemoryLimits.SectionCivicArtifacts",
				lease);
			StringAssert.Contains("Authority.TryReadSection(SectionId, out Lease, out Failure)",
				lease);
		}

		/// <summary>
		/// One lease means one lease across the whole conversation. The section is opened once, in
		/// <c>Open</c>; the commit shard opens nothing and offers the carried lease; only the
		/// post-commit readback is allowed to look at the save afresh.
		/// </summary>
		[Test]
		public void TheDisclosureAndTheCommitShareExactlyOneSectionLease()
		{
			string open = Read(Engine[0]);
			string choose = Read(Engine[1]);
			string commit = Read(Engine[2]);
			string charter = open + choose + commit;
			Assert.AreEqual(1, Occurrences(charter, "TryReadAuthority("),
				"the Charter conversation may open section one exactly once");
			Assert.AreEqual(0, Occurrences(commit, "TryReadAuthority("),
				"the commit shard must not take a fresh lease");
			Assert.AreEqual(0, Occurrences(choose, "TryReadAuthority("));
			Assert.AreEqual(0, Occurrences(charter, "TryReadSection("),
				"only the lease seam may name the section API");

			string report = Method(commit, "private static void Report(Ground Ground,");
			Assert.AreEqual(1, Occurrences(commit, "TryReadBackRow("),
				"the only re-read is the post-commit readback");
			StringAssert.Contains("TryReadBackRow(", report);

			string body = Method(commit, "private static void Commit(Ground Ground, "
				+ "KingdomCivicMemorySectionLease Lease,");
			StringAssert.Contains("TrySnapshotNearby(Ground.Founder", body);
			StringAssert.Contains("Plan.Source.SnapshotDigest", body);
			StringAssert.Contains("ProveResident(Ground, Plan.AttributedResidentId", body);
			Assert.Less(body.IndexOf("SnapshotDigest", StringComparison.Ordinal),
				body.IndexOf("TryCommitPlanned", StringComparison.Ordinal),
				"the object is re-proved before anything is offered");
		}

		/// <summary>
		/// Reading, browsing, and cancelling are free. The family marks the governance scope once,
		/// after civic memory has actually taken a row, and never charges energy itself.
		/// </summary>
		[Test]
		public void OnlyAnAcceptedRowMarksTheGovernanceScope()
		{
			string family = Family();
			Assert.AreEqual(1, Occurrences(family, "KingdomGovernanceScope.Commit("),
				"exactly one governance commit in the whole D6 family");
			Assert.AreEqual(0, Occurrences(family, "UseEnergy"),
				"D6 must never charge energy directly");
			string commit = Read(Engine[2]);
			StringAssert.Contains("if (outcome == KingdomArtifactRecognitionOutcome.Recorded)\n"
				+ "\t\t\t\tKingdomGovernanceScope.Commit(\"recognize artifact\");", commit);
			Assert.Less(commit.IndexOf("TryCommitPlanned", StringComparison.Ordinal),
				commit.IndexOf("KingdomGovernanceScope.Commit(", StringComparison.Ordinal),
				"the scope is marked only after the durable commit returned");
		}

		/// <summary>
		/// The paused realm and the external-owner gate both still apply, because the new verb is
		/// absent from the two whitelists that would exempt it. Adding it to either would let a
		/// paused or externally owned city record a recognition.
		/// </summary>
		[Test]
		public void TheNewVerbIsOnNeitherPausedNorOwnershipReportWhitelist()
		{
			string paused = Method(Read("Core/KingdomCharterMenuRules.cs"),
				"public static bool AvailableWhileSimulationPaused(KingdomCharterAction Action)");
			StringAssert.DoesNotContain("RecognizeArtifact", paused);
			string reports = Method(Read("Core/KingdomCharterPart.ExternalOwnership.cs"),
				"private static bool IsOwnershipReport(KingdomCharterAction Action)");
			StringAssert.DoesNotContain("RecognizeArtifact", reports);
		}

		/// <summary>
		/// The non-custodial sweep. None of these tokens may appear anywhere in the family, because
		/// each of them is a way of touching the original rather than writing about it.
		/// </summary>
		[Test]
		public void NothingInTheFamilyCanTouchTheOriginal()
		{
			string[] forbidden =
			{
				"Inventory", "GetInventory", ".AddObject(", ".RemoveObject(", "TakeObject",
				"Obliterate", "Physics.Owner = ", "Journal", "KingdomProperty", "IsTakeable",
				"OwnedByPlayer", "ValueEach", "GetIntrinsicValue", "GetExtrinsicValue",
				"SetStringProperty", "SetIntProperty", "CurrentCell = ", "DroppedByPlayer",
				"GameObjectFactory", "ForceUnequip", "SetHolder"
			};
			List<string> owned = new List<string>(Engine);
			owned.AddRange(Pure);
			for (int f = 0; f < owned.Count; f++)
			{
				string source = CodeOnly(Read(owned[f]));
				for (int i = 0; i < forbidden.Length; i++)
					Assert.AreEqual(0, Occurrences(source, forbidden[i]),
						owned[f] + " must never name " + forbidden[i]);
			}
		}

		/// <summary>
		/// Selection reads the founder's own cell and its immediate neighbours, and nothing else.
		/// The two cell reads are pinned by count so a third cannot be added quietly.
		/// </summary>
		[Test]
		public void SelectionReadsOnlyTheFoundersOwnGroundAndItsNeighbours()
		{
			string selection = Read("Experience/KingdomArtifactRecognitionSelectionRuntime.cs");
			StringAssert.Contains("origin.GetLocalAdjacentCells()", selection);
			StringAssert.Contains("cells[c].GetObjects()", selection);
			StringAssert.Contains("MaxNearbyChoices = 64", selection);
			StringAssert.Contains("Another nearby object claims the selected object's exact "
				+ "identity.", selection);
			Assert.AreEqual(2, Occurrences(selection, ".GetObjects()"),
				"exactly two bounded ground reads: the picker and the identity uniqueness proof");
			Assert.AreEqual(2, Occurrences(selection, "GetLocalAdjacentCells()"));
			StringAssert.Contains("Cell.cs", selection);
			StringAssert.Contains("GameObject.cs", selection);
		}

		/// <summary>
		/// One subject, one recognition. The rules scan the held rows for the same exact object
		/// before deriving a new one, so a later move, sale, or change of form cannot duplicate a
		/// representation or rewrite the row that already stands.
		/// </summary>
		[Test]
		public void RecognitionRulesRefuseASecondRowForTheSameObject()
		{
			string rules = Read("Core/KingdomArtifactRecognitionRules.cs");
			StringAssert.Contains("Book.Rows[i].Source.ObjectId != Snapshot.ObjectId", rules);
			StringAssert.Contains("already recognized and its record cannot be", rules);
			int scan = rules.IndexOf("Book.Rows[i].Source.ObjectId != Snapshot.ObjectId",
				StringComparison.Ordinal);
			int add = rules.IndexOf("candidate.Rows.Add(Receipt)", StringComparison.Ordinal);
			Assert.Less(scan, add, "the subject scan must run before a row is derived or added");
			StringAssert.Contains("{ Receipt = null; return false; }", rules);
		}

		/// <summary>
		/// The Reliquary unlock comes from an exact current-realm readback of the committed rows,
		/// never from what the founder is carrying, standing near, or ranked as.
		/// </summary>
		[Test]
		public void ReliquaryUnlocksOnlyFromExactCurrentRealmReadback()
		{
			string service = Read("Core/KingdomVocationServiceRuntime.cs");
			StringAssert.Contains("KingdomCivicMemoryLimits.SectionCivicArtifacts", service);
			StringAssert.Contains("KingdomCivicArtifactsStore.ReadForRealm(lease.Payload()",
				service);
			StringAssert.Contains("artifacts.RealmId, exactRealmId", service);
			StringAssert.Contains("artifacts.Recognitions.Rows", service);
			string reliquary = Method(service, "private static bool TryReliquary(");
			Assert.AreEqual(0, Occurrences(reliquary, "Inventory"));
			Assert.AreEqual(0, Occurrences(reliquary, "Standing"));
			Assert.AreEqual(0, Occurrences(reliquary, "GetObjects"));
			StringAssert.Contains("SettlementIdForOwnedZone(zoneId) != context.SettlementId",
				reliquary);
		}

		/// <summary>
		/// Reading an identity must never create one.
		/// <para>
		/// <c>GameObject.ID</c>'s getter writes the <c>id</c> property (GameObject.cs 436-448) and
		/// falls back on <c>BaseID</c>, which advances the save's <c>GameObjectIDSequence</c>
		/// (GameObject.cs 400-417). Naming it anywhere in this family would mean that merely opening
		/// the Charter page and cancelling had already changed both the object and the save. Only
		/// <c>IDIfAssigned</c> (GameObject.cs 424-434) is a pure read, so it is the only identity
		/// this family may use, and this pin is what keeps it that way.
		/// </para>
		/// </summary>
		[Test]
		public void NothingInTheFamilyReadsTheMintingIdentityProperty()
		{
			Regex minting = new Regex(@"\.ID(?![A-Za-z0-9_])");
			List<string> owned = new List<string>(Engine);
			owned.AddRange(Pure);
			for (int f = 0; f < owned.Count; f++)
			{
				string source = CodeOnly(Read(owned[f]));
				Assert.AreEqual(0, minting.Matches(source).Count,
					owned[f] + " reads GameObject.ID, which mints an identity and writes the save");
				Assert.AreEqual(0, Occurrences(source, "BaseID"),
					owned[f] + " must not touch the native id sequence");
				Assert.AreEqual(0, Occurrences(source, "GameObjectIDSequence"));
			}
			string selection = Read("Experience/KingdomArtifactRecognitionSelectionRuntime.cs");
			StringAssert.Contains("string engineId = Selected.IDIfAssigned;", selection);
			StringAssert.Contains("string.IsNullOrEmpty(item.IDIfAssigned)", selection);
			StringAssert.Contains("Unidentified++", selection);
			string adapter = Read("Core/KingdomArtifactRecognitionRuntime.cs");
			StringAssert.Contains("string assigned = Selected.IDIfAssigned;", adapter);
			StringAssert.Contains("GameObject.cs 436-448", adapter);
			StringAssert.Contains("GameObject.cs 400-417", adapter);
			StringAssert.Contains("GameObject.cs 424-434", adapter);
			StringAssert.Contains("never given that thing an exact identity", adapter);
		}

		/// <summary>
		/// Capacity is decided by the transition, not by a door. An early full-register return in
		/// the Charter flow would make the free retry unreachable for a realm holding eight rows.
		/// </summary>
		[Test]
		public void TheCharterDoesNotRefuseOnCapacityBeforeTheSubjectRetryIsKnown()
		{
			string choose = Read(Engine[1]);
			string recognize = Method(choose, "private static void Recognize(Ground Ground, "
				+ "KingdomCivicMemorySectionLease Lease,");
			int capacity = recognize.IndexOf("MaxRows", StringComparison.Ordinal);
			int selection = recognize.IndexOf("TryCollectNearby", StringComparison.Ordinal);
			Assert.GreaterOrEqual(capacity, 0, "a full register is still disclosed");
			Assert.Greater(capacity, selection,
				"capacity may only be reported, never returned on, before selection");
			Assert.AreEqual(0, Occurrences(recognize, "Popup.Show(\"This realm has already kept"),
				"the early capacity refusal must be gone");
			StringAssert.Contains("only something the city has already recorded can be confirmed",
				recognize);
			string rules = Read("Core/KingdomArtifactRecognitionRules.cs");
			int subject = rules.IndexOf("SameSubject(Book.Rows[i]", StringComparison.Ordinal);
			int cap = rules.IndexOf("Book.Rows.Count >= MaxRows", StringComparison.Ordinal);
			Assert.Less(subject, cap,
				"the subject retry is answered before capacity is consulted");
		}

		/// <summary>
		/// The retry is judged on meaning, not on a digest that carries the moment it was taken.
		/// Only a later reading of identical facts is tolerated.
		/// </summary>
		[Test]
		public void TheRetryComparisonIgnoresOnlyTheObservationTick()
		{
			string rules = Read("Core/KingdomArtifactRecognitionRules.cs");
			string same = Method(rules, "private static bool SameSubject(");
			foreach (string field in new string[] { "Kind", "ObjectId", "Blueprint", "DisplayName",
				"OwnerId", "LocationId", "DeedId", "DeedText", "AttributedResidentId",
				"AttributionName" })
				StringAssert.Contains(field, same, "SameSubject must compare " + field);
			Assert.AreEqual(0, Occurrences(same, "SnapshotDigest"),
				"the digest carries the tick and must not decide a retry");
			Assert.AreEqual(0, Occurrences(same, "ObservedTick"));
			StringAssert.Contains("Snapshot.ObservedTick >= Book.Rows[i].Source.ObservedTick",
				rules, "only a later reading of the same facts is tolerated");
		}

		/// <summary>
		/// The city named on every D6 page is the one that owns the ground, resolved through the
		/// realm's settlement topology.
		/// <para>
		/// <c>SeatName</c> is banned outright in this family. It follows the seat cursor rather than
		/// the ground, and it falls back to the realm's display name when a settlement has none of
		/// its own &mdash; so either way it can name something that is not this city.
		/// </para>
		/// </summary>
		[Test]
		public void EveryPageNamesTheSettlementThatOwnsTheGroundNotTheSeat()
		{
			List<string> owned = new List<string>(Engine);
			owned.AddRange(Pure);
			for (int f = 0; f < owned.Count; f++)
				Assert.AreEqual(0, Occurrences(CodeOnly(Read(owned[f])), "SeatName"),
					owned[f] + " names the seat instead of the ground's own settlement");

			string open = Read(Engine[0]);
			string resolver = Method(open, "private static bool TrySettlementName(");
			StringAssert.Contains("System.TryFindSettlement(SettlementId, out bool seated,",
				resolver);
			StringAssert.Contains("seated ? System.SettlementName : settlement.SettlementName",
				resolver);
			StringAssert.Contains("string.IsNullOrWhiteSpace(resolved)", resolver);
			StringAssert.Contains("no name of its own", resolver);
			Assert.AreEqual(0, Occurrences(resolver, "DisplayName"),
				"a settlement name is never inferred from zone or realm display text");
			Assert.AreEqual(0, Occurrences(resolver, "ZoneID"));
			StringAssert.Contains("ground.SettlementName", open);

			string ground = Method(open, "private static bool TryGround(");
			Assert.Less(ground.IndexOf("TrySettlementName", StringComparison.Ordinal),
				ground.IndexOf("Result = new Ground", StringComparison.Ordinal),
				"an unnameable city must refuse before a Ground is ever built");

			StringAssert.Contains("Ground.SettlementName", Read(Engine[1]));
			StringAssert.Contains("KingdomPresentation.Rich(Ground.SettlementName)",
				Read(Engine[2]));
			string plan = Read("Experience/KingdomArtifactRecognitionPlan.cs");
			StringAssert.Contains("public readonly string SettlementName;", plan);
			StringAssert.Contains("Kept by: ", plan);
			StringAssert.Contains("string.IsNullOrWhiteSpace(SettlementName)",
				Read("Experience/KingdomArtifactRecognitionCommit.cs"));
		}

		/// <summary>Structure law: every D6 production shard stays strictly under 300 lines.</summary>
		[Test]
		public void EveryRecognitionProductionShardStaysBelowThreeHundredLines()
		{
			List<string> owned = new List<string>(Engine);
			owned.AddRange(Pure);
			for (int i = 0; i < owned.Count; i++)
			{
				int lines = Read(owned[i]).Split('\n').Length;
				Assert.Less(lines, 300, owned[i] + " is " + lines + " physical lines");
			}
		}

		/// <summary>
		/// No source in this family may carry a raw control or format character. A C1 control in a
		/// literal is eaten before anyone reads the intent, and a format character is invisible to
		/// the next person who opens the file; both are spelled as escapes instead.
		/// </summary>
		[Test]
		public void NoRecognitionSourceCarriesARawControlOrFormatCharacter()
		{
			List<string> owned = new List<string>(Engine);
			owned.AddRange(Pure);
			owned.Add("DevTests/KingdomArtifactRecognitionServiceTests.cs");
			owned.Add("DevTests/KingdomArtifactRecognitionAdversarialTests.cs");
			owned.Add("DevTests/KingdomArtifactRecognitionTransactionTests.cs");
			owned.Add("DevTests/KingdomArtifactRecognitionSourceTests.cs");
			for (int f = 0; f < owned.Count; f++)
			{
				string text = Read(owned[f]);
				for (int i = 0; i < text.Length; i++)
				{
					char c = text[i];
					if (c == '\t' || c == '\n' || c == '\r') continue;
					bool control = c < ' ' || (c >= '\u0080' && c <= '\u009f') || c == '\u007f';
					bool format = char.GetUnicodeCategory(c)
						== System.Globalization.UnicodeCategory.Format;
					Assert.IsFalse(control || format, owned[f] + " carries U+"
						+ ((int)c).ToString("X4") + " raw at offset " + i);
				}
			}
		}

		/// <summary>
		/// Hashes are checked for integrity, and semantic owners are validated. Neither is
		/// authenticated, and this family must not say so.
		/// </summary>
		[Test]
		public void TheFamilyNeverCallsAnIntegrityCheckAnAuthentication()
		{
			string family = Family();
			Assert.AreEqual(0, Regex.Matches(family, "authenticat", RegexOptions.IgnoreCase).Count,
				"digests verify integrity; owners are validated");
		}
	}
}
#endif
