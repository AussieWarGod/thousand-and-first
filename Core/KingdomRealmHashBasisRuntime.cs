using System;

namespace ThousandAndFirst
{
	/// <summary>Runtime side of the authority-hash basis contract: it resolves the wire version a
	/// persisted callback hash was cut under, re-proves that hash at its own basis, and publishes a
	/// new cut at today's CurrentVersion. Selection is KingdomRealmHashBasisRules; hashing is the
	/// archive's own TryAuthorityHash/TryCurrentGraphHash at an explicit schema. Nothing caches,
	/// reflects, replays or rewrites a hash, and every write is gated on a re-proof that the
	/// receipt and its owners are as they stood before hashing began.</summary>
	internal static class KingdomRealmHashBasisRuntime
	{
		/// <summary>One hash at an explicit settlement-wire schema. False leaves Hash unusable.</summary>
		internal delegate bool VersionedHasher(int Version, out string Hash, out string Failure);

		/// <summary>Re-proves the owning archive and its seven receipts against the frozen set.</summary>
		internal delegate bool OwnerProof();

		internal const string AbsentInputFailure = "callback hash basis inputs are absent";
		internal const string MutatedFailure = "callback receipt changed while it was hashed";
		internal const string IntentReproofFailure = "authority hash fails its intent basis";
		internal const string GraphReproofFailure = "realm hash fails its intent basis";
		internal const string SettledCopyFailure = "settled archive hashes are not one proved value";
		internal const string SettledGraphFailure = "settled realm hash fails its settle basis";

		/// <summary>H1. Basis of the immutable intent authority hash: a pinned basis is checked once
		/// and never widens into a search, a stored 0 resolves only on a unique match.</summary>
		internal static bool TryResolveIntentBasis(KingdomRealmCallbackReceipt Receipt,
			VersionedHasher Authority, out int Basis, out string Failure)
		{
			Basis = KingdomRealmHashBasisRules.Unresolved;
			if (Receipt == null || Authority == null) return Refuse(AbsentInputFailure, out Failure);
			return TrySelect(Receipt.BeforeArchiveGraph, Receipt.IntentSettlementSchema, Authority,
				out Basis, out Failure);
		}

		/// <summary>H3. None-to-Intent, the only cut that hashes today's CurrentVersion. Hash order is
		/// the pre-basis order - live TAG1, then TAA1 - so a fresh receipt's hash text is unchanged.
		/// Graphs and effects publish first, then the intent basis, then nothing.</summary>
		internal static bool TryCaptureIntent(KingdomRealmCallbackReceipt Receipt,
			OwnerProof Owners, KingdomRealmCallbackScope Scope, string BeforeEffect,
			string AfterEffect, int BeforeStamp, int AfterStamp, VersionedHasher Graph,
			VersionedHasher Authority, out string Failure)
		{
			if (Receipt == null || Owners == null || Graph == null || Authority == null)
				return Refuse(AbsentInputFailure, out Failure);
			Snapshot before = new Snapshot(Receipt);
			int current = KingdomArchivedSettlementCodec.CurrentVersion;
			if (!Graph(current, out string graph, out Failure) ||
				!Authority(current, out string archiveGraph, out Failure)) return false;
			if (!SealBasis(Receipt, Owners, before, KingdomRealmCallbackPhase.Intent, current,
				KingdomRealmHashBasisRules.Unresolved, out Failure)) return false;
			Receipt.Scope = Scope; Receipt.BeforeGraph = graph;
			Receipt.BeforeArchiveGraph = archiveGraph; Receipt.BeforeEffect = BeforeEffect;
			Receipt.AfterEffect = AfterEffect; Receipt.BeforeStamp = BeforeStamp;
			Receipt.AfterStamp = AfterStamp; Receipt.Phase = KingdomRealmCallbackPhase.Intent;
			Receipt.IntentSettlementSchema = current;
			Receipt.SettledSettlementSchema = KingdomRealmHashBasisRules.Unresolved;
			return true;
		}

		/// <summary>H4. Intent-to-Attempting: both frozen hashes must still reprove at the intent
		/// basis. Holds the one legal resolution write in the runtime - a stored 0 Select resolved
		/// uniquely is pinned here, after the full proof and a second re-proof.</summary>
		internal static bool TryProveAttempting(KingdomRealmCallbackReceipt Receipt,
			OwnerProof Owners, VersionedHasher Graph, VersionedHasher Authority, out string Failure)
		{
			if (!TryProveIntentCut(Receipt, Owners, Graph, Authority, true, true, out int basis,
				out Snapshot before, out Failure)) return false;
			if (Receipt.IntentSettlementSchema != KingdomRealmHashBasisRules.Unresolved) return true;
			if (!SealBasis(Receipt, Owners, before, Receipt.Phase, basis,
				Receipt.SettledSettlementSchema, out Failure)) return false;
			Receipt.IntentSettlementSchema = basis;
			return true;
		}

		/// <summary>H5. Settle: the frozen TAA1 reproves at the intent basis, and for Ability and
		/// Reputation the live TAG1 is compared to BeforeGraph at that same basis - never a fresh
		/// CurrentVersion cut against an older frozen hash. AfterGraph is an independent live
		/// CurrentVersion cut; AfterArchiveGraph copies the value just re-proved.</summary>
		internal static bool TrySettle(KingdomRealmCallbackReceipt Receipt, OwnerProof Owners,
			KingdomRealmCallbackDisposition Disposition, string ObservedEffect,
			VersionedHasher Graph, VersionedHasher Authority, out string Failure)
		{
			if (Receipt == null) return Refuse(AbsentInputFailure, out Failure);
			bool liveMustMatch = Receipt.Scope == KingdomRealmCallbackScope.Ability ||
				Receipt.Scope == KingdomRealmCallbackScope.Reputation;
			if (!TryProveIntentCut(Receipt, Owners, Graph, Authority, true, liveMustMatch,
				out int _, out Snapshot before, out Failure)) return false;
			int current = KingdomArchivedSettlementCodec.CurrentVersion;
			if (!Graph(current, out string afterGraph, out Failure)) return false;
			if (!SealBasis(Receipt, Owners, before, KingdomRealmCallbackPhase.Settled,
				Receipt.IntentSettlementSchema, current, out Failure)) return false;
			Receipt.AfterGraph = afterGraph; Receipt.AfterArchiveGraph = Receipt.BeforeArchiveGraph;
			Receipt.ObservedEffect = ObservedEffect; Receipt.Disposition = Disposition;
			Receipt.Phase = KingdomRealmCallbackPhase.Settled;
			Receipt.SettledSettlementSchema = current;
			return true;
		}

		/// <summary>H6. Settled verifier: the two archive hashes must be one proved value, still
		/// reproving as TAA1 under the intent basis, and AfterGraph resolves its own settle basis
		/// independently - pinned here, unique search for a legacy 0. Writes nothing.</summary>
		internal static bool TryVerifySettled(KingdomRealmCallbackReceipt Receipt,
			OwnerProof Owners, VersionedHasher Graph, VersionedHasher Authority, out string Failure)
		{
			if (Receipt == null || Owners == null || Graph == null || Authority == null)
				return Refuse(AbsentInputFailure, out Failure);
			Snapshot before = new Snapshot(Receipt);
			if (!Same(Receipt.BeforeArchiveGraph, Receipt.AfterArchiveGraph))
				return Refuse(SettledCopyFailure, out Failure);
			if (!TryResolveIntentBasis(Receipt, Authority, out int intent, out Failure) ||
				!Authority(intent, out string archiveGraph, out Failure)) return false;
			if (!Same(archiveGraph, Receipt.AfterArchiveGraph))
				return Refuse(IntentReproofFailure, out Failure);
			if (!TrySelect(Receipt.AfterGraph, Receipt.SettledSettlementSchema, Graph,
				out int settled, out Failure) ||
				!Graph(settled, out string liveGraph, out Failure)) return false;
			if (!Same(liveGraph, Receipt.AfterGraph))
				return Refuse(SettledGraphFailure, out Failure);
			if (before.Matches(Receipt) && Owners()) return true;
			return Refuse(MutatedFailure, out Failure);
		}

		/// <summary>H7. The absent-Chronicle prestate proof: the live TAG1 is cut at the receipt's
		/// already resolved intent basis, never today's default; it refuses rather than guess.</summary>
		internal static bool TryProveIntentGraph(KingdomRealmCallbackReceipt Receipt,
			OwnerProof Owners, VersionedHasher Graph, VersionedHasher Authority, out string Failure)
		{
			return TryProveIntentCut(Receipt, Owners, Graph, Authority, false, true, out int _,
				out Snapshot _, out Failure);
		}

		/// <summary>Shared proof of an existing intent cut. ReproveAuthority adds the explicit TAA1
		/// re-observation Attempting and Settle owe on top of Select's own computation; ReproveGraph
		/// compares the live TAG1 at that basis to BeforeGraph.</summary>
		private static bool TryProveIntentCut(KingdomRealmCallbackReceipt Receipt,
			OwnerProof Owners, VersionedHasher Graph, VersionedHasher Authority,
			bool ReproveAuthority, bool ReproveGraph, out int Basis, out Snapshot Before,
			out string Failure)
		{
			Basis = KingdomRealmHashBasisRules.Unresolved;
			Before = default(Snapshot);
			if (Receipt == null || Owners == null || Graph == null || Authority == null)
				return Refuse(AbsentInputFailure, out Failure);
			Before = new Snapshot(Receipt);
			if (!TryResolveIntentBasis(Receipt, Authority, out Basis, out Failure)) return false;
			if (ReproveAuthority && (!Authority(Basis, out string archiveGraph, out Failure) ||
				!Same(archiveGraph, Receipt.BeforeArchiveGraph)))
				return Unresolve(Failure ?? IntentReproofFailure, out Basis, out Failure);
			if (ReproveGraph && (!Graph(Basis, out string graph, out Failure) ||
				!Same(graph, Receipt.BeforeGraph)))
				return Unresolve(Failure ?? GraphReproofFailure, out Basis, out Failure);
			if (Before.Matches(Receipt) && Owners()) return true;
			return Unresolve(MutatedFailure, out Basis, out Failure);
		}

		/// <summary>H2. The gate every publication passes: the receipt and its owners are re-proved
		/// unchanged across the hashing driven, the pair is shape-checked against the phase the
		/// publication leaves behind, and the receipt is re-proved once more.</summary>
		private static bool SealBasis(KingdomRealmCallbackReceipt Receipt, OwnerProof Owners,
			Snapshot Before, KingdomRealmCallbackPhase Phase, int Intent, int Settled,
			out string Failure)
		{
			if (!Before.Matches(Receipt) || !Owners()) return Refuse(MutatedFailure, out Failure);
			if (!KingdomRealmArchive.ValidBasisShape(Phase, Intent, Settled, out Failure))
				return false;
			if (Before.Matches(Receipt) && Owners()) return true;
			return Refuse(MutatedFailure, out Failure);
		}

		/// <summary>Drives the pure selector and keeps its fixed reason as the refusal text.</summary>
		private static bool TrySelect(string ExpectedHash, int StoredBasis, VersionedHasher Hasher,
			out int Basis, out string Failure)
		{
			KingdomRealmHashBasisRules.AuthorityHasher probe = delegate (int Version, out string Hash)
			{ return Hasher(Version, out Hash, out string _); };
			if (KingdomRealmHashBasisRules.Select(ExpectedHash, StoredBasis, probe, out Basis,
				out string reason) == KingdomRealmHashBasisRules.BasisOutcome.Selected)
			{
				Failure = null;
				return true;
			}
			return Unresolve(reason, out Basis, out Failure);
		}
		private static bool Refuse(string Reason, out string Failure)
		{ Failure = Reason; return false; }
		private static bool Unresolve(string Reason, out int Basis, out string Failure)
		{ Basis = KingdomRealmHashBasisRules.Unresolved; Failure = Reason; return false; }
		private static bool Same(string Left, string Right)
		{ return string.Equals(Left, Right, StringComparison.Ordinal); }
		/// <summary>One receipt as it stood before hashing: phase, disposition, scope, the seven
		/// persisted strings, both stamps and both basis integers.</summary>
		private struct Snapshot
		{
			private readonly KingdomRealmCallbackPhase Phase;
			private readonly KingdomRealmCallbackDisposition Disposition;
			private readonly KingdomRealmCallbackScope Scope;
			private readonly string BeforeGraph, AfterGraph, BeforeArchiveGraph,
				AfterArchiveGraph, BeforeEffect, AfterEffect, ObservedEffect;
			private readonly int BeforeStamp, AfterStamp, Intent, Settled;

			internal Snapshot(KingdomRealmCallbackReceipt Value)
			{
				Phase = Value.Phase; Disposition = Value.Disposition; Scope = Value.Scope;
				BeforeGraph = Value.BeforeGraph; AfterGraph = Value.AfterGraph;
				BeforeArchiveGraph = Value.BeforeArchiveGraph; BeforeEffect = Value.BeforeEffect;
				AfterArchiveGraph = Value.AfterArchiveGraph; AfterEffect = Value.AfterEffect;
				ObservedEffect = Value.ObservedEffect; BeforeStamp = Value.BeforeStamp;
				AfterStamp = Value.AfterStamp; Intent = Value.IntentSettlementSchema;
				Settled = Value.SettledSettlementSchema;
			}

			internal bool Matches(KingdomRealmCallbackReceipt Value)
			{
				return Value != null && Phase == Value.Phase && Scope == Value.Scope &&
					Disposition == Value.Disposition && BeforeStamp == Value.BeforeStamp &&
					AfterStamp == Value.AfterStamp && Intent == Value.IntentSettlementSchema &&
					Settled == Value.SettledSettlementSchema &&
					Same(BeforeGraph, Value.BeforeGraph) && Same(AfterGraph, Value.AfterGraph) &&
					Same(BeforeArchiveGraph, Value.BeforeArchiveGraph) &&
					Same(AfterArchiveGraph, Value.AfterArchiveGraph) &&
					Same(BeforeEffect, Value.BeforeEffect) && Same(AfterEffect, Value.AfterEffect) &&
					Same(ObservedEffect, Value.ObservedEffect);
			}
		}

#if !TAF_TESTS
		/// <summary>Binds one call site to the archive's actual hash methods and freezes the owners
		/// they depend on. It holds no hash: a second call re-derives everything.</summary>
		internal sealed class Binding
		{
			private readonly KingdomRealmArchive Archive;
			private readonly KingdomSystem Realm;
			private readonly KingdomRealmCallbackReceipt Receipt;
			private readonly KingdomRealmCallbackScope Scope;
			private readonly KingdomRealmCallbackReceipt ExileTale, ExileCharter, ReturnTale,
				Regard, Feelings, Seat, ReturnCharter;

			internal Binding(KingdomRealmArchive Archive, KingdomSystem Realm,
				KingdomRealmCallbackReceipt Receipt, KingdomRealmCallbackScope Scope)
			{
				this.Archive = Archive; this.Realm = Realm; this.Receipt = Receipt;
				this.Scope = Scope;
				ExileTale = Archive.ExileChronicle; ExileCharter = Archive.ExileAbility;
				ReturnTale = Archive.ReturnChronicle; Regard = Archive.ReturnReputation;
				Feelings = Archive.ReturnFeelings; Seat = Archive.ReturnSeat;
				ReturnCharter = Archive.ReturnAbility;
			}

			internal bool Intact()
			{
				return Realm != null && ReferenceEquals(Realm.ExiledRealmArchive, Archive) &&
						ReferenceEquals(ExileTale, Archive.ExileChronicle) &&
					ReferenceEquals(ExileCharter, Archive.ExileAbility) &&
					ReferenceEquals(ReturnTale, Archive.ReturnChronicle) &&
					ReferenceEquals(Regard, Archive.ReturnReputation) &&
					ReferenceEquals(Feelings, Archive.ReturnFeelings) &&
					ReferenceEquals(Seat, Archive.ReturnSeat) &&
					ReferenceEquals(ReturnCharter, Archive.ReturnAbility);
			}

			internal bool GraphHash(int Version, out string Hash, out string Failure)
			{ return KingdomRealmArchive.TryCurrentGraphHash(Realm, Version, out Hash, out Failure); }

			internal bool AuthorityHash(int Version, out string Hash, out string Failure)
			{ return Archive.TryAuthorityHash(Receipt, Scope, Version, out Hash, out Failure); }

			internal bool ResolveIntentBasis(out int Basis, out string Failure)
			{ return TryResolveIntentBasis(Receipt, AuthorityHash, out Basis, out Failure); }

			internal bool CaptureIntent(string BeforeEffect, string AfterEffect, int BeforeStamp,
				int AfterStamp, out string Failure)
			{
				return TryCaptureIntent(Receipt, Intact, Scope, BeforeEffect, AfterEffect,
					BeforeStamp, AfterStamp, GraphHash, AuthorityHash, out Failure);
			}

			internal bool ProveAttempting(out string Failure)
			{ return TryProveAttempting(Receipt, Intact, GraphHash, AuthorityHash, out Failure); }

			internal bool Settle(KingdomRealmCallbackDisposition Disposition, string ObservedEffect,
				out string Failure)
			{
				return TrySettle(Receipt, Intact, Disposition, ObservedEffect, GraphHash,
					AuthorityHash, out Failure);
			}

			internal bool VerifySettled(out string Failure)
			{ return TryVerifySettled(Receipt, Intact, GraphHash, AuthorityHash, out Failure); }

			internal bool ProveIntentGraph(out string Failure)
			{ return TryProveIntentGraph(Receipt, Intact, GraphHash, AuthorityHash, out Failure); }
		}
#endif
	}
}
