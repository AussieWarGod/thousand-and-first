using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ThousandAndFirst
{
	/// <summary>The ledger lane of one report entry. Its own enum, deliberately: the shared
	/// <see cref="KingdomSubsidenceEffectPhase"/> has no way to say "this line was never owed to
	/// the ledger at all", and a skipped line is settled rather than unfinished. Nothing serialises
	/// this as a step part field; it lives only inside a report plan and its own wire.
	/// <para><see cref="Lost"/> is the terminal failure: settled with the ledger, so the frontier
	/// moves on, but never delivered and never provable afterwards.</para></summary>
	internal enum ReportLedgerPhase : byte
	{
		Prepared = 0, Intent = 1, Proved = 2, Skipped = 3, Lost = 4
	}

	/// <summary>Why a ledger line was lost, recorded at the observation that lost it and never
	/// recomputed. A third state is not monotone - clearing the note list returns it to count
	/// zero, which would re-authorize the very append already lost - so the fact is latched here
	/// rather than derived from a later look at the world.</summary>
	internal enum LedgerLossKind : byte { None = 0, ThirdState = 1, HomecomingReset = 2 }
	internal enum ReportChronicleRefusal : byte { None = 0, CapacityRefused = 1 }

	/// <summary>One thing the settlement owes once. The Chronicle must be proved or its exact
	/// terminal non-delivery retained; the
	/// ledger half is optional, and when it fires it is one exact append with a frozen before.
	/// Every field is captured when the entry is prepared and never re-read from the live world.
	/// </summary>
	internal sealed class KingdomSubsidenceReportEntry
	{
		/// <summary>The chronicle line. Never empty: an entry that tells nothing is not an entry.
		/// </summary>
		internal readonly string Text;

		/// <summary>The ledger line, or the empty string for an explicit no-ledger entry. Empty is
		/// a decision recorded at preparation, not an absence to be repaired later.</summary>
		internal readonly string LedgerText;

		internal readonly long AtTick;
		internal readonly ReportLedgerPhase LedgerPhase;
		internal readonly bool ChronicleProved;

		/// <summary>The exact note count and canonical hash frozen when the ledger was armed, and
		/// the exact pair the append is proved against. A skipped entry carries the same pair
		/// twice: nothing moved, and that is what was proved. A lost entry keeps the frozen before
		/// untouched as its witness and carries as its after exactly the state observed when the
		/// line was lost - never a guessed append.</summary>
		internal readonly int BeforeCount;
		internal readonly string BeforeHash;
		internal readonly int AfterCount;
		internal readonly string AfterHash;

		/// <summary>Why the ledger half was lost, or <see cref="LedgerLossKind.None"/> for a line
		/// that never was. Written once, with the phase, and never revised.</summary>
		internal readonly LedgerLossKind LedgerLoss;

		/// <summary>The chronicle half is not proved delivered to both registers; at least one
		/// sink was lost, and no further attempt will be made. Mutually exclusive with
		/// <see cref="ChronicleProved"/>: a telling is delivered or lost, never both.</summary>
		internal readonly bool ChronicleLost;
		internal readonly ReportChronicleRefusal ChronicleRefusal;
		internal readonly int CapacityCount;
		internal readonly string CapacityHash, CapacityFingerprint;
		internal bool CapacityRefused { get { return ChronicleRefusal == ReportChronicleRefusal.CapacityRefused; } }

		internal KingdomSubsidenceReportEntry(string text, string ledgerText, long atTick,
			ReportLedgerPhase ledgerPhase, bool chronicleProved, int beforeCount, string beforeHash,
			int afterCount, string afterHash, LedgerLossKind ledgerLoss = LedgerLossKind.None,
			bool chronicleLost = false, ReportChronicleRefusal chronicleRefusal = ReportChronicleRefusal.None,
			int capacityCount = 0, string capacityHash = "", string capacityFingerprint = "")
		{
			Text = text; LedgerText = ledgerText; AtTick = atTick; LedgerPhase = ledgerPhase;
			ChronicleProved = chronicleProved; BeforeCount = beforeCount; BeforeHash = beforeHash;
			AfterCount = afterCount; AfterHash = afterHash; LedgerLoss = ledgerLoss;
			ChronicleLost = chronicleLost;
			ChronicleRefusal = chronicleRefusal; CapacityCount = capacityCount;
			CapacityHash = capacityHash; CapacityFingerprint = capacityFingerprint;
		}

		/// <summary>A fresh entry: nothing armed, nothing counted, nothing told, nothing lost.
		/// </summary>
		internal KingdomSubsidenceReportEntry(string text, string ledgerText, long atTick)
			: this(text, ledgerText, atTick, ReportLedgerPhase.Prepared, false, 0, "", 0, "")
		{
		}

		internal KingdomSubsidenceReportEntry WithLedger(ReportLedgerPhase phase, int beforeCount,
			string beforeHash, int afterCount, string afterHash)
		{
			return new KingdomSubsidenceReportEntry(Text, LedgerText, AtTick, phase, ChronicleProved,
				beforeCount, beforeHash, afterCount, afterHash, LedgerLoss, ChronicleLost,
				ChronicleRefusal, CapacityCount, CapacityHash, CapacityFingerprint);
		}

		internal KingdomSubsidenceReportEntry WithChronicle(bool proved)
		{
			return new KingdomSubsidenceReportEntry(Text, LedgerText, AtTick, LedgerPhase, proved,
				BeforeCount, BeforeHash, AfterCount, AfterHash, LedgerLoss, ChronicleLost,
				ChronicleRefusal, CapacityCount, CapacityHash, CapacityFingerprint);
		}

		/// <summary>Latches the ledger half as lost. The frozen before is carried through exactly
		/// as the intent left it, and the after is the state actually seen; the reason is stamped
		/// once so no later look at the world can revise or undo it.</summary>
		internal KingdomSubsidenceReportEntry WithLoss(LedgerLossKind kind, int afterCount,
			string afterHash)
		{
			return new KingdomSubsidenceReportEntry(Text, LedgerText, AtTick,
				ReportLedgerPhase.Lost, ChronicleProved, BeforeCount, BeforeHash, afterCount,
				afterHash, kind, ChronicleLost, ChronicleRefusal, CapacityCount, CapacityHash, CapacityFingerprint);
		}

		/// <summary>Latches the chronicle half as lost, leaving every ledger field alone.</summary>
		internal KingdomSubsidenceReportEntry WithChronicleLost()
		{
			return new KingdomSubsidenceReportEntry(Text, LedgerText, AtTick, LedgerPhase,
				ChronicleProved, BeforeCount, BeforeHash, AfterCount, AfterHash, LedgerLoss, true,
				ChronicleRefusal, CapacityCount, CapacityHash, CapacityFingerprint);
		}

		internal KingdomSubsidenceReportEntry WithCapacityRefusal(KingdomChronicleCapacityWitness witness)
		{
			return new KingdomSubsidenceReportEntry(Text, LedgerText, AtTick, LedgerPhase,
				ChronicleProved, BeforeCount, BeforeHash, AfterCount, AfterHash, LedgerLoss, ChronicleLost,
				ReportChronicleRefusal.CapacityRefused, witness.RegistryCount, witness.RegistryHash, witness.Fingerprint);
		}
	}

	/// <summary>One owner's whole telling, ordered. The owner is a subsidence step or a subsidence
	/// batch; either way the report is retired entry by entry in order, so a cut resumes at the
	/// first unfinished line and never re-tells a finished one.</summary>
	internal sealed class KingdomSubsidenceReportPlan
	{
		internal readonly string OwnerId, RealmId, SettlementId;
		internal readonly ReadOnlyCollection<KingdomSubsidenceReportEntry> Entries;

		internal KingdomSubsidenceReportPlan(string ownerId, string realmId, string settlementId,
			IEnumerable<KingdomSubsidenceReportEntry> entries)
		{
			OwnerId = ownerId; RealmId = realmId; SettlementId = settlementId;
			Entries = entries == null
				? null : new List<KingdomSubsidenceReportEntry>(entries).AsReadOnly();
		}

		internal KingdomSubsidenceReportPlan With(int index, KingdomSubsidenceReportEntry entry)
		{
			List<KingdomSubsidenceReportEntry> rows
				= new List<KingdomSubsidenceReportEntry>(Entries);
			rows[index] = entry;
			return new KingdomSubsidenceReportPlan(OwnerId, RealmId, SettlementId, rows);
		}
	}
}
