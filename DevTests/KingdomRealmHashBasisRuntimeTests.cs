#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Codec = ThousandAndFirst.KingdomArchivedSettlementCodec;
using Rules = ThousandAndFirst.KingdomRealmHashBasisRules;
using Runtime = ThousandAndFirst.KingdomRealmHashBasisRuntime;

namespace ThousandAndFirst.Tests
{
	/// <summary>The runtime basis rules are driven engine-free: every case supplies its own
	/// recording hashers and owner proof, so the real KingdomRealmHashBasisRuntime, the real
	/// KingdomRealmHashBasisRules selector and the real KingdomRealmArchive.ValidBasisShape run
	/// with no archive, no KingdomSystem and no SDK. The last group is SOURCE-pinned: the two
	/// callback files are read as text because their call sites need KingdomSystem, which is not
	/// compiled under TAF_TESTS. These are not native callback or save-load proofs.</summary>
	[TestFixture]
	public class KingdomRealmHashBasisRuntimeTests
	{
		private const string Hex = "0123456789abcdef";
		private const string Callback = "Core/KingdomSystem.z12.Return.Callback.cs";
		private const string Chronicle = "Core/KingdomSystem.z09b.Exile.ChronicleDispatch.cs";
		private const string RuntimeFile = "Core/KingdomRealmHashBasisRuntime.cs";

		/// <summary>A canonical 64-digit lowercase hex hash, distinct per seed.</summary>
		private static string Digest(int Seed)
		{
			return new string('0', 61) + Hex[(Seed >> 8) & 0xF] + Hex[(Seed >> 4) & 0xF] +
				Hex[Seed & 0xF];
		}

		/// <summary>Answers that reproduce <paramref name="Hash"/> at exactly one version.</summary>
		private static Dictionary<int, string> Only(int Version, string Hash, int Seed)
		{
			Dictionary<int, string> answers = new Dictionary<int, string>();
			for (int version = Rules.MinVersion; version <= Rules.MaxVersion; version++)
				answers.Add(version, version == Version ? Hash : Digest(Seed + version));
			return answers;
		}

		/// <summary>Records the versions it is asked for and can interfere once, at a chosen ask,
		/// so a graph that moves under the hash can be reproduced without an engine.</summary>
		private sealed class Hasher
		{
			internal readonly Dictionary<int, string> Answers;
			private readonly List<int> Asked = new List<int>();
			internal Action Interfere;
			internal int InterfereAtAsk = -1;

			internal Hasher(Dictionary<int, string> Answers) { this.Answers = Answers; }

			internal int[] Order() { return Asked.ToArray(); }

			internal bool Compute(int Version, out string Hash, out string Failure)
			{
				Asked.Add(Version);
				if (Interfere != null && Asked.Count == InterfereAtAsk) Interfere();
				Failure = null;
				if (!Answers.TryGetValue(Version, out Hash))
				{
					Failure = "schema " + Version + " cannot represent this archive";
					return false;
				}
				return true;
			}
		}

		private sealed class Owners
		{
			internal bool Intact = true;
			internal bool Proof() { return Intact; }
		}

		private static KingdomRealmCallbackReceipt Blank()
		{
			return new KingdomRealmCallbackReceipt();
		}

		private static KingdomRealmCallbackReceipt Begun(KingdomRealmCallbackPhase Phase,
			KingdomRealmCallbackScope Scope, int IntentBasis, string Archive, string Graph)
		{
			KingdomRealmCallbackReceipt receipt = new KingdomRealmCallbackReceipt();
			receipt.Phase = Phase;
			receipt.Scope = Scope;
			receipt.BeforeGraph = Graph;
			receipt.BeforeArchiveGraph = Archive;
			receipt.BeforeEffect = "frozen-before";
			receipt.AfterEffect = "frozen-after";
			receipt.IntentSettlementSchema = IntentBasis;
			return receipt;
		}

		// ---- Group 1: mixed 18 -> 19 cuts -------------------------------------------------

		[Test]
		public void UnresolvedIntentIsFoundByUniqueSearchAndPinnedOnlyOnFullSuccess()
		{
			string archiveHash = Digest(801);
			string graphHash = Digest(802);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Intent,
				KingdomRealmCallbackScope.Chronicle, Rules.Unresolved, archiveHash, graphHash);
			Hasher authority = new Hasher(Only(18, archiveHash, 100));
			Hasher graph = new Hasher(Only(18, graphHash, 300));
			Owners owners = new Owners();
			Assert.IsTrue(Runtime.TryProveAttempting(receipt, owners.Proof, graph.Compute,
				authority.Compute, out string failure), failure);
			Assert.IsNull(failure);
			Assert.AreEqual(18, receipt.IntentSettlementSchema, "the unique match is pinned here");
			Assert.AreEqual(Rules.Unresolved, receipt.SettledSettlementSchema);
			Assert.AreEqual(archiveHash, receipt.BeforeArchiveGraph);
			Assert.AreEqual(graphHash, receipt.BeforeGraph);
			Assert.AreEqual(Rules.MaxVersion, authority.Order()[0], "search runs newest first");
			Assert.AreEqual(Rules.MaxVersion - Rules.MinVersion + 2, authority.Order().Length,
				"a full search plus exactly one explicit re-proof");
			CollectionAssert.AreEqual(new[] { 18 }, graph.Order(),
				"the live graph is only ever cut at the resolved basis here");
		}

		[Test]
		public void SettleReprovesTheOldBasisAndCutsTheNewGraphAtCurrentVersion()
		{
			string archiveHash = Digest(811);
			string graphHash = Digest(812);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Attempting,
				KingdomRealmCallbackScope.Reputation, 18, archiveHash, graphHash);
			Hasher authority = new Hasher(Only(18, archiveHash, 100));
			Dictionary<int, string> live = Only(18, graphHash, 300);
			Hasher graph = new Hasher(live);
			Assert.IsTrue(Runtime.TrySettle(receipt, new Owners().Proof,
				KingdomRealmCallbackDisposition.Delivered, "observed", graph.Compute,
				authority.Compute, out string failure), failure);
			Assert.AreEqual(live[Codec.CurrentVersion], receipt.AfterGraph,
				"AfterGraph is an independent cut at today's CurrentVersion");
			Assert.AreEqual(archiveHash, receipt.AfterArchiveGraph,
				"AfterArchiveGraph copies the exactly re-proved Before value");
			Assert.AreEqual(receipt.BeforeArchiveGraph, receipt.AfterArchiveGraph);
			Assert.AreEqual(18, receipt.IntentSettlementSchema, "the intent basis is not rewritten");
			Assert.AreEqual(Codec.CurrentVersion, receipt.SettledSettlementSchema);
			Assert.AreEqual(KingdomRealmCallbackPhase.Settled, receipt.Phase);
			Assert.AreEqual(KingdomRealmCallbackDisposition.Delivered, receipt.Disposition);
			Assert.AreEqual("observed", receipt.ObservedEffect);
			CollectionAssert.AreEqual(new[] { 18, Codec.CurrentVersion }, graph.Order(),
				"the live comparison is taken at the intent basis, never at 19");
			CollectionAssert.AreEqual(new[] { 18, 18 }, authority.Order());
		}

		[Test]
		public void SettleSkipsTheLiveComparisonForScopesThatMayLegitimatelyMove()
		{
			string archiveHash = Digest(821);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Attempting,
				KingdomRealmCallbackScope.Seat, 18, archiveHash, Digest(822));
			Hasher graph = new Hasher(Only(-1, null, 300));
			Assert.IsTrue(Runtime.TrySettle(receipt, new Owners().Proof,
				KingdomRealmCallbackDisposition.Delivered, "seated", graph.Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string _));
			CollectionAssert.AreEqual(new[] { Codec.CurrentVersion }, graph.Order());
			Assert.AreNotEqual(receipt.BeforeGraph, receipt.AfterGraph);
		}

		// ---- Group 2: pinned mismatch, legacy zero, failed proofs -------------------------

		[Test]
		public void PinnedBasisThatDisagreesRefusesWithoutFallingBackToSearch()
		{
			string archiveHash = Digest(831);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Intent,
				KingdomRealmCallbackScope.Chronicle, Codec.CurrentVersion, archiveHash,
				Digest(832));
			Hasher authority = new Hasher(Only(18, archiveHash, 100));
			Hasher graph = new Hasher(Only(18, receipt.BeforeGraph, 300));
			Assert.IsFalse(Runtime.TryProveAttempting(receipt, new Owners().Proof, graph.Compute,
				authority.Compute, out string failure));
			Assert.IsNotNull(failure);
			CollectionAssert.AreEqual(new[] { Codec.CurrentVersion }, authority.Order(),
				"a pinned slot is asked once and never widens into a search");
			CollectionAssert.IsEmpty(graph.Order());
			Assert.AreEqual(Codec.CurrentVersion, receipt.IntentSettlementSchema,
				"the stored basis is left exactly as it was");
			Assert.AreEqual(archiveHash, receipt.BeforeArchiveGraph);
		}

		[Test]
		public void LegacyZeroSurvivesWhenNoAcceptedVersionReproducesTheHash()
		{
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Intent,
				KingdomRealmCallbackScope.Chronicle, Rules.Unresolved, Digest(841), Digest(842));
			Hasher authority = new Hasher(Only(-1, null, 100));
			Assert.IsFalse(Runtime.TryProveAttempting(receipt, new Owners().Proof,
				new Hasher(Only(-1, null, 300)).Compute, authority.Compute, out string failure));
			Assert.IsNotNull(failure);
			Assert.AreEqual(Rules.Unresolved, receipt.IntentSettlementSchema,
				"an unresolvable slot is never promoted to a guess");
			Assert.AreEqual(Rules.MaxVersion - Rules.MinVersion + 1, authority.Order().Length);
		}

		[Test]
		public void AmbiguousSearchRefusesAndWritesNoBasis()
		{
			string archiveHash = Digest(851);
			Dictionary<int, string> answers = Only(18, archiveHash, 100);
			answers[12] = archiveHash;
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Intent,
				KingdomRealmCallbackScope.Chronicle, Rules.Unresolved, archiveHash, Digest(852));
			Assert.IsFalse(Runtime.TryProveAttempting(receipt, new Owners().Proof,
				new Hasher(Only(18, receipt.BeforeGraph, 300)).Compute,
				new Hasher(answers).Compute, out string failure));
			Assert.IsNotNull(failure);
			Assert.AreEqual(Rules.Unresolved, receipt.IntentSettlementSchema);
		}

		[Test]
		public void FailedLiveProofLeavesEveryBasisAndEveryStoredHashUntouched()
		{
			string archiveHash = Digest(861);
			string graphHash = Digest(862);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Intent,
				KingdomRealmCallbackScope.Chronicle, Rules.Unresolved, archiveHash, graphHash);
			Assert.IsFalse(Runtime.TryProveAttempting(receipt, new Owners().Proof,
				new Hasher(Only(18, Digest(863), 300)).Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string failure));
			Assert.AreEqual(Runtime.GraphReproofFailure, failure);
			Assert.AreEqual(Rules.Unresolved, receipt.IntentSettlementSchema);
			Assert.AreEqual(Rules.Unresolved, receipt.SettledSettlementSchema);
			Assert.AreEqual(archiveHash, receipt.BeforeArchiveGraph);
			Assert.AreEqual(graphHash, receipt.BeforeGraph);
			Assert.IsNull(receipt.AfterGraph);
			Assert.IsNull(receipt.AfterArchiveGraph);
		}

		[Test]
		public void SettleThatCannotCutTheNewGraphPublishesNothing()
		{
			string archiveHash = Digest(871);
			string graphHash = Digest(872);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Attempting,
				KingdomRealmCallbackScope.Reputation, 18, archiveHash, graphHash);
			Dictionary<int, string> live = Only(18, graphHash, 300);
			live.Remove(Codec.CurrentVersion);
			Assert.IsFalse(Runtime.TrySettle(receipt, new Owners().Proof,
				KingdomRealmCallbackDisposition.Delivered, "observed", new Hasher(live).Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string failure));
			Assert.IsNotNull(failure);
			Assert.AreEqual(KingdomRealmCallbackPhase.Attempting, receipt.Phase);
			Assert.AreEqual(Rules.Unresolved, receipt.SettledSettlementSchema);
			Assert.IsNull(receipt.AfterGraph);
			Assert.IsNull(receipt.AfterArchiveGraph);
			Assert.IsNull(receipt.ObservedEffect);
		}

		// ---- Group 3: mutation during hashing --------------------------------------------

		[Test]
		public void AReceiptEditedWhileItIsHashedRefusesAndWritesNoBasis()
		{
			string archiveHash = Digest(881);
			string graphHash = Digest(882);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Intent,
				KingdomRealmCallbackScope.Chronicle, 18, archiveHash, graphHash);
			Hasher graph = new Hasher(Only(18, graphHash, 300));
			graph.InterfereAtAsk = 1;
			graph.Interfere = delegate { receipt.AfterEffect = "moved under the hash"; };
			Assert.IsFalse(Runtime.TryProveAttempting(receipt, new Owners().Proof, graph.Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string failure));
			Assert.AreEqual(Runtime.MutatedFailure, failure);
			Assert.AreEqual(18, receipt.IntentSettlementSchema);
			Assert.AreEqual(archiveHash, receipt.BeforeArchiveGraph);
		}

		[Test]
		public void AnOwnerReferenceSwappedWhileHashingRefusesTheWholeCut()
		{
			string archiveHash = Digest(891);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Attempting,
				KingdomRealmCallbackScope.Reputation, 18, archiveHash, Digest(892));
			Owners owners = new Owners();
			Hasher graph = new Hasher(Only(18, receipt.BeforeGraph, 300));
			graph.InterfereAtAsk = 1;
			graph.Interfere = delegate { owners.Intact = false; };
			Assert.IsFalse(Runtime.TrySettle(receipt, owners.Proof,
				KingdomRealmCallbackDisposition.Delivered, "observed", graph.Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string failure));
			Assert.AreEqual(Runtime.MutatedFailure, failure);
			Assert.AreEqual(Rules.Unresolved, receipt.SettledSettlementSchema);
			Assert.IsNull(receipt.AfterGraph);
		}

		// ---- Group 4: the None -> Intent capture ------------------------------------------

		[Test]
		public void CaptureIntentPublishesNineteenAndZeroOnlyAfterBothHashesSucceed()
		{
			KingdomRealmCallbackReceipt receipt = Blank();
			Dictionary<int, string> archive = Only(-1, null, 100);
			Dictionary<int, string> live = Only(-1, null, 300);
			Hasher graph = new Hasher(live);
			Hasher authority = new Hasher(archive);
			Assert.IsTrue(Runtime.TryCaptureIntent(receipt, new Owners().Proof,
				KingdomRealmCallbackScope.Feelings, "before", "after", 1, 2, graph.Compute,
				authority.Compute, out string failure), failure);
			Assert.AreEqual(Codec.CurrentVersion, receipt.IntentSettlementSchema);
			Assert.AreEqual(Rules.Unresolved, receipt.SettledSettlementSchema);
			Assert.AreEqual(KingdomRealmCallbackPhase.Intent, receipt.Phase);
			Assert.AreEqual(KingdomRealmCallbackScope.Feelings, receipt.Scope);
			Assert.AreEqual(live[Codec.CurrentVersion], receipt.BeforeGraph);
			Assert.AreEqual(archive[Codec.CurrentVersion], receipt.BeforeArchiveGraph);
			Assert.AreEqual("before", receipt.BeforeEffect);
			Assert.AreEqual("after", receipt.AfterEffect);
			Assert.AreEqual(1, receipt.BeforeStamp);
			Assert.AreEqual(2, receipt.AfterStamp);
			CollectionAssert.AreEqual(new[] { Codec.CurrentVersion }, graph.Order());
			CollectionAssert.AreEqual(new[] { Codec.CurrentVersion }, authority.Order());
		}

		[Test]
		public void CaptureIntentThatCannotHashPublishesNothingAtAll()
		{
			KingdomRealmCallbackReceipt receipt = Blank();
			Dictionary<int, string> archive = new Dictionary<int, string>();
			Assert.IsFalse(Runtime.TryCaptureIntent(receipt, new Owners().Proof,
				KingdomRealmCallbackScope.Chronicle, "before", "after", int.MinValue,
				int.MinValue, new Hasher(Only(-1, null, 300)).Compute,
				new Hasher(archive).Compute, out string failure));
			Assert.IsNotNull(failure);
			Assert.AreEqual(KingdomRealmCallbackPhase.None, receipt.Phase);
			Assert.AreEqual(Rules.Unresolved, receipt.IntentSettlementSchema);
			Assert.AreEqual(Rules.Unresolved, receipt.SettledSettlementSchema);
			Assert.IsNull(receipt.BeforeGraph);
			Assert.IsNull(receipt.BeforeArchiveGraph);
			Assert.IsTrue(KingdomRealmArchive.ValidBasisShape(receipt, out string _));
		}

		// ---- Group 5: the settled verifier -----------------------------------------------

		private static KingdomRealmCallbackReceipt Settled(int IntentBasis, int SettledBasis,
			string Archive, string After)
		{
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Settled,
				KingdomRealmCallbackScope.Chronicle, IntentBasis, Archive, Digest(902));
			receipt.AfterArchiveGraph = Archive;
			receipt.AfterGraph = After;
			receipt.ObservedEffect = "observed";
			receipt.Disposition = KingdomRealmCallbackDisposition.Delivered;
			receipt.SettledSettlementSchema = SettledBasis;
			return receipt;
		}

		[TestCase(19)]
		[TestCase(0)]
		public void VerifySettledResolvesTheSettleBasisIndependentlyOfTheIntentBasis(int stored)
		{
			string archiveHash = Digest(911);
			string afterHash = Digest(912);
			KingdomRealmCallbackReceipt receipt = Settled(18, stored, archiveHash, afterHash);
			Hasher authority = new Hasher(Only(18, archiveHash, 100));
			Hasher graph = new Hasher(Only(Codec.CurrentVersion, afterHash, 300));
			Assert.IsTrue(Runtime.TryVerifySettled(receipt, new Owners().Proof, graph.Compute,
				authority.Compute, out string failure), failure);
			CollectionAssert.AreEqual(new[] { 18, 18 }, authority.Order(),
				"the archive half stays on the intent basis");
			Assert.AreEqual(Codec.CurrentVersion, graph.Order()[graph.Order().Length - 1]);
			Assert.AreEqual(stored, receipt.SettledSettlementSchema, "the verifier writes nothing");
			Assert.AreEqual(18, receipt.IntentSettlementSchema);
			Assert.AreEqual(afterHash, receipt.AfterGraph);
		}

		[Test]
		public void VerifySettledRefusesWhenTheTwoArchiveHashesAreNotOneProvedValue()
		{
			KingdomRealmCallbackReceipt receipt = Settled(18, 19, Digest(921), Digest(922));
			receipt.AfterArchiveGraph = Digest(923);
			Hasher authority = new Hasher(Only(18, receipt.BeforeArchiveGraph, 100));
			Assert.IsFalse(Runtime.TryVerifySettled(receipt, new Owners().Proof,
				new Hasher(Only(19, receipt.AfterGraph, 300)).Compute, authority.Compute,
				out string failure));
			Assert.AreEqual(Runtime.SettledCopyFailure, failure);
			CollectionAssert.IsEmpty(authority.Order(), "nothing is hashed once the copy is broken");
		}

		[Test]
		public void VerifySettledRefusesAPinnedSettleBasisThatDoesNotReproduceAfterGraph()
		{
			string archiveHash = Digest(931);
			KingdomRealmCallbackReceipt receipt = Settled(18, Codec.CurrentVersion, archiveHash,
				Digest(932));
			Hasher graph = new Hasher(Only(17, receipt.AfterGraph, 300));
			Assert.IsFalse(Runtime.TryVerifySettled(receipt, new Owners().Proof, graph.Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string failure));
			Assert.IsNotNull(failure);
			CollectionAssert.AreEqual(new[] { Codec.CurrentVersion }, graph.Order(),
				"a pinned settle basis is asked once and never searches");
		}

		// ---- Group 6: the absent-Chronicle prestate proof ---------------------------------

		[Test]
		public void ChroniclePrestateHashesAtTheResolvedIntentBasisAndNeverAtTheDefault()
		{
			string archiveHash = Digest(941);
			string graphHash = Digest(942);
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Attempting,
				KingdomRealmCallbackScope.Chronicle, 18, archiveHash, graphHash);
			Hasher graph = new Hasher(Only(18, graphHash, 300));
			Assert.IsTrue(Runtime.TryProveIntentGraph(receipt, new Owners().Proof, graph.Compute,
				new Hasher(Only(18, archiveHash, 100)).Compute, out string failure), failure);
			CollectionAssert.AreEqual(new[] { 18 }, graph.Order(),
				"the prestate cut takes the persisted basis, not CurrentVersion");
			Assert.AreEqual(18, receipt.IntentSettlementSchema, "the prestate proof writes nothing");
		}

		[Test]
		public void ChroniclePrestateRefusesAnUnresolvableStoredZeroRatherThanGuessing()
		{
			KingdomRealmCallbackReceipt receipt = Begun(KingdomRealmCallbackPhase.Attempting,
				KingdomRealmCallbackScope.Chronicle, Rules.Unresolved, Digest(951), Digest(952));
			Hasher graph = new Hasher(Only(-1, null, 300));
			Assert.IsFalse(Runtime.TryProveIntentGraph(receipt, new Owners().Proof, graph.Compute,
				new Hasher(Only(-1, null, 100)).Compute, out string failure));
			Assert.IsNotNull(failure);
			CollectionAssert.IsEmpty(graph.Order(), "no live cut is taken without a basis");
			Assert.AreEqual(Rules.Unresolved, receipt.IntentSettlementSchema);
		}

		// ---- Group 7: SOURCE pins (the call sites need KingdomSystem) ----------------------

		[Test]
		public void NeitherCallbackFileTakesAHashWithoutAnExplicitBasis()
		{
			foreach (string path in new[] { Callback, Chronicle })
			{
				string source = Read(path);
				StringAssert.DoesNotContain("TryAuthorityHash(", source, path);
				StringAssert.DoesNotContain("TryCurrentGraphHash(", source, path);
			}
		}

		[Test]
		public void EveryCallbackSiteGoesThroughTheRuntimeBindingWithItsGuardsIntact()
		{
			string source = Compact(Read(Callback));
			foreach (string pin in new[]
			{
				"!Archive.CurrentGraphMatches(this,outstringfailure)||!ExactExileMirrors(Archive)||"
					+ "!TradeTransitionProofMatches(Archive,RequireBound:"
					+ "ReturnCallbackTradeBound(Archive),outfailure)||"
					+ "!newKingdomRealmHashBasisRuntime.Binding(Archive,this,Receipt,Scope)"
					+ ".CaptureIntent(BeforeEffect,AfterEffect,BeforeStamp,AfterStamp,outfailure))",
				"!newKingdomRealmHashBasisRuntime.Binding(Archive,this,Receipt,Scope)"
					+ ".ProveAttempting(outfailure))",
				"!newKingdomRealmHashBasisRuntime.Binding(Archive,this,Receipt,Receipt.Scope)"
					+ ".Settle(Disposition,ObservedEffect,outfailure))",
				"!newKingdomRealmHashBasisRuntime.Binding(Archive,this,Receipt,Receipt.Scope)"
					+ ".VerifySettled(outfailure))"
			})
				StringAssert.Contains(pin, source);
			StringAssert.Contains("!newKingdomRealmHashBasisRuntime.Binding(Archive,this,Receipt,"
				+ "Receipt.Scope).ProveIntentGraph(outgraphFailure))", Compact(Read(Chronicle)));
		}

		[Test]
		public void TheCallbackFilesStillPublishNoHashOrBasisOfTheirOwn()
		{
			foreach (string path in new[] { Callback, Chronicle })
			{
				string source = Compact(Read(path));
				foreach (string forbidden in new[]
				{
					"Receipt.BeforeGraph=", "Receipt.AfterGraph=", "Receipt.BeforeArchiveGraph=",
					"Receipt.AfterArchiveGraph=", "Receipt.IntentSettlementSchema=",
					"Receipt.SettledSettlementSchema="
				})
					StringAssert.DoesNotContain(forbidden, source, path);
			}
			string callback = Compact(Read(Callback));
			foreach (string kept in new[]
			{
				"\"callbackintentisunbounded\"", "\"callbackreceiptconflictswithfrozenintent\"",
				"\"callbackgraphchangedbeforeattempt\"", "\"callbackcouldnotsettleexactgraph\"",
				"\"settledcallbackproofnolongermatchesexactpoststate\"",
				"Archive.CurrentGraphMatchesAfterSeat(this,true,outfailure)"
			})
				StringAssert.Contains(kept, callback, "existing refusals and guards are unchanged");
		}

		[Test]
		public void TheBindingDelegatesToTheRealExplicitSchemaMethods()
		{
			string source = Compact(Read(RuntimeFile));
			StringAssert.Contains("returnKingdomRealmArchive.TryCurrentGraphHash(Realm,Version,"
				+ "outHash,outFailure);", source);
			StringAssert.Contains("returnArchive.TryAuthorityHash(Receipt,Scope,Version,outHash,"
				+ "outFailure);", source);
			StringAssert.Contains("KingdomArchivedSettlementCodec.CurrentVersion", source,
				"the new cut tracks the codec rather than a literal 19");
			StringAssert.DoesNotContain("Cache", source);
			StringAssert.DoesNotContain("Reflection", source);
		}

		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static string Compact(string source)
		{
			return System.Text.RegularExpressions.Regex.Replace(source, @"\s+", "");
		}
	}
}
#endif
