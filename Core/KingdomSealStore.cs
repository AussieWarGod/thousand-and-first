using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomSealReceiptState
	{
		Reserved = 0,
		Committed = 1,
		Declined = 2
	}

	internal sealed class KingdomSealReceipt
	{
		private const string Kind = "receipt";

		private static readonly string[] StateNames = new string[3] { "reserved", "committed", "declined" };

		public string LineageId = "";

		public string LegacyId = "";

		public string TargetGameId = "";

		public KingdomSealReceiptState State = KingdomSealReceiptState.Reserved;

		public long WrittenTick;

		internal string Compose()
		{
			KingdomSealBody body = new KingdomSealBody();
			body.Put("kind", Kind);
			body.Put("lineage", LineageId);
			body.Put("legacy", LegacyId);
			body.Put("target", TargetGameId);
			body.Put("state", StateName(State));
			body.Put("written", WrittenTick);
			return KingdomSealFormat.Compose(KingdomSealRecord.CurrentSchema, body);
		}

		internal static bool TryParse(string FileText, out KingdomSealReceipt Receipt)
		{
			Receipt = null;
			try
			{
				int schema;
				KingdomSealBody body;
				KingdomSealFault fault;
				string detail;
				if (!KingdomSealFormat.TryParse(FileText, KingdomSealRecord.CurrentSchema,
					KingdomSealRecord.CurrentSchema, out schema, out body, out fault, out detail))
				{
					return false;
				}
				if (body.Count != 6 || !body.Has("kind") || !body.Has("lineage") || !body.Has("legacy")
					|| !body.Has("target") || !body.Has("state") || !body.Has("written"))
				{
					return false;
				}
				if (body.KindOf("kind") != KingdomSealKind.Text || body.Text("kind") != Kind
					|| body.KindOf("lineage") != KingdomSealKind.Text
					|| body.KindOf("legacy") != KingdomSealKind.Text
					|| body.KindOf("target") != KingdomSealKind.Text
					|| body.KindOf("state") != KingdomSealKind.Text
					|| body.KindOf("written") != KingdomSealKind.Number)
				{
					return false;
				}
				string lineage = body.Text("lineage");
				string legacy = body.Text("legacy");
				string target = body.Text("target");
				if (!ValidId(lineage) || !ValidId(legacy) || !ValidId(target))
				{
					return false;
				}
				int state = StateIndex(body.Text("state"));
				long written = body.Number("written", -1L);
				if (state < 0 || written < 0L)
				{
					return false;
				}
				Receipt = new KingdomSealReceipt
				{
					LineageId = lineage,
					LegacyId = legacy,
					TargetGameId = target,
					State = (KingdomSealReceiptState)state,
					WrittenTick = written
				};
				return true;
			}
			catch (Exception)
			{
				Receipt = null;
				return false;
			}
		}

		internal static bool ValidId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > KingdomSealRecord.MaxIdChars)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z')
					&& (c < '0' || c > '9') && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}

		private static string StateName(KingdomSealReceiptState State)
		{
			int index = (int)State;
			if (index < 0 || index >= StateNames.Length)
			{
				throw new InvalidOperationException("The receipt state is not known.");
			}
			return StateNames[index];
		}

		private static int StateIndex(string Value)
		{
			for (int i = 0; i < StateNames.Length; i++)
			{
				if (StateNames[i] == Value)
				{
					return i;
				}
			}
			return -1;
		}
	}

	internal interface IKingdomSealFileOps
	{
		bool Exists(string Path);

		FileAttributes Attributes(string Path);

		long Length(string Path);

		string ReadAllText(string Path);

		void WriteAllTextDurable(string Path, string Text);

		void MoveNew(string Source, string Destination);

		void ReplaceAtomic(string Source, string Destination, string Backup);

		void DeleteIfExists(string Path);
	}

	internal sealed class SystemKingdomSealFileOps : IKingdomSealFileOps
	{
		internal static readonly SystemKingdomSealFileOps Instance = new SystemKingdomSealFileOps();

		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		private SystemKingdomSealFileOps()
		{
		}

		public bool Exists(string Path)
		{
			return File.Exists(Path);
		}

		public FileAttributes Attributes(string Path)
		{
			return File.GetAttributes(Path);
		}

		public long Length(string Path)
		{
			return new FileInfo(Path).Length;
		}

		public string ReadAllText(string Path)
		{
			return File.ReadAllText(Path, Utf8);
		}

		public void WriteAllTextDurable(string Path, string Text)
		{
			byte[] bytes = Utf8.GetBytes(Text ?? "");
			using (FileStream stream = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				stream.Write(bytes, 0, bytes.Length);
				stream.Flush(true);
			}
		}

		public void MoveNew(string Source, string Destination)
		{
			File.Move(Source, Destination);
		}

		public void ReplaceAtomic(string Source, string Destination, string Backup)
		{
			File.Replace(Source, Destination, Backup, true);
		}

		public void DeleteIfExists(string Path)
		{
			if (File.Exists(Path))
			{
				File.Delete(Path);
			}
		}
	}

	/// <summary>An OS-held, process-scoped proof that one exact target is still creating a world
	/// for a reserved legacy. The empty lock file is not proof; only this open exclusive handle is.</summary>
	internal sealed class KingdomSealReservationLease : IDisposable
	{
		private readonly object _sync = new object();

		private FileStream _gate;

		internal readonly string LineageId;

		internal readonly string LegacyId;

		internal readonly string TargetGameId;

		internal KingdomSealReservationLease(KingdomSealReceipt Receipt, FileStream Gate)
		{
			LineageId = Receipt.LineageId;
			LegacyId = Receipt.LegacyId;
			TargetGameId = Receipt.TargetGameId;
			_gate = Gate;
		}

		internal bool IsHeld
		{
			get
			{
				lock (_sync)
				{
					return _gate != null;
				}
			}
		}

		internal bool Matches(KingdomSealReceipt Receipt)
		{
			return Receipt != null && Receipt.LineageId == LineageId
				&& Receipt.LegacyId == LegacyId && Receipt.TargetGameId == TargetGameId;
		}

		public void Dispose()
		{
			FileStream gate;
			lock (_sync)
			{
				gate = _gate;
				_gate = null;
			}
			gate?.Dispose();
		}
	}

	internal sealed class KingdomSealStore
	{
		internal const string StagesFolder = "Stages";

		internal const string LegaciesFolder = "Legacies";

		internal const string ReceiptsFolder = "Receipts";

		internal const string ClaimsFolder = "Claims";

		internal const string SealExtension = ".seal";

		internal const string ReceiptExtension = ".receipt";

		internal const int MaxFilesScanned = 256;

		internal const int MaxStageFilesScanned = MaxFilesScanned * 2;

		private readonly string _root;

		private readonly string _rootPrefix;

		private readonly StringComparison _pathComparison;

		private readonly IKingdomSealFileOps _files;

		internal KingdomSealStore(string Root)
			: this(Root, SystemKingdomSealFileOps.Instance)
		{
		}

		internal KingdomSealStore(string Root, IKingdomSealFileOps Files)
		{
			if (string.IsNullOrEmpty(Root))
			{
				throw new ArgumentException("A seal store needs a root folder.");
			}
			if (Files == null)
			{
				throw new ArgumentNullException("Files");
			}
			_root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar,
				Path.AltDirectorySeparatorChar);
			if (_root.Length == 0 || string.Equals(_root, Path.GetPathRoot(_root),
				StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException("A seal store root must be a bounded profile subfolder.");
			}
			_rootPrefix = _root + Path.DirectorySeparatorChar;
			_pathComparison = Path.DirectorySeparatorChar == '\\'
				? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			_files = Files;
		}

		internal string Root => _root;

		internal bool TryStage(KingdomSealRecord Record, out string Failure)
		{
			Failure = "";
			if (Record == null || !KingdomSealReceipt.ValidId(Record.OriginGameId))
			{
				Failure = "the record names no valid origin";
				return false;
			}
			FileStream gate;
			if (!TryLockStage(Record.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				return TryStageLocked(Record, out Failure);
			}
		}

		private bool TryStageLocked(KingdomSealRecord Record, out string Failure)
		{
			Failure = "";
			if (Record == null || Record.Status == KingdomSealStatus.Promoted)
			{
				Failure = "only a living, terminal, or retired record is a stage";
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Record.OriginGameId))
			{
				Failure = "the record names no valid origin";
				return false;
			}
			string slotA = StagePath(Record.OriginGameId, 'a');
			string slotB = StagePath(Record.OriginGameId, 'b');
			KingdomSealRecord a = ReadSlot(slotA);
			KingdomSealRecord b = ReadSlot(slotB);
			if ((a != null && a.OriginGameId != Record.OriginGameId)
				|| (b != null && b.OriginGameId != Record.OriginGameId)
				|| !SameStageIdentity(a, b))
			{
				Failure = "the origin journal contains a record that does not match its filename or other slot";
				return false;
			}
			KingdomSealRecord best = Best(a, b);
			if (best != null)
			{
				if (best.LineageId != Record.LineageId || best.LegacyId != Record.LegacyId
					|| best.Generation != Record.Generation)
				{
					Failure = "an origin journal cannot change its lineage, legacy, or generation";
					return false;
				}
				if (best.Revision > Record.Revision)
				{
					Failure = "the stage revision would go backwards";
					return false;
				}
				if (best.Revision == Record.Revision)
				{
					if (SameRecord(best, Record))
					{
						return true;
					}
					Failure = "the stage revision already names different facts";
					return false;
				}
				if (best.Status == KingdomSealStatus.Retired)
				{
					Failure = "an explicitly retired generation cannot be rewritten";
					return false;
				}
			}
			string target = (best != null && object.ReferenceEquals(best, a)) ? slotB : slotA;
			return TryWriteSeal(target, Record, true, out Failure);
		}

		internal bool TryAdvanceGeneration(KingdomSealRecord Previous, KingdomSealRecord Successor, out string Failure)
		{
			Failure = "";
			if (!ValidGenerationHandoff(Previous, Successor, out Failure))
			{
				return false;
			}
			FileStream gate;
			if (!TryLockStage(Previous.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(Previous.OriginGameId, 'a');
				string slotB = StagePath(Previous.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b))
				{
					Failure = "the origin journal contains an unreadable slot";
					return false;
				}
				if (SameRecord(a, Successor) && SameRecord(b, Successor))
				{
					return true;
				}
				if (SameRecord(a, Previous) && SameRecord(b, Successor))
				{
					return TryWriteSeal(slotA, Successor, true, out Failure);
				}
				if (SameRecord(b, Previous) && SameRecord(a, Successor))
				{
					return TryWriteSeal(slotB, Successor, true, out Failure);
				}
				if (!SameStageIdentity(a, b))
				{
					Failure = "the origin journal is not one coherent generation";
					return false;
				}
				KingdomSealRecord current = Best(a, b);
				if (!SameRecord(current, Previous))
				{
					Failure = "the previous generation is not the exact current stage";
					return false;
				}
				string first = object.ReferenceEquals(current, a) ? slotB : slotA;
				string second = object.ReferenceEquals(current, a) ? slotA : slotB;
				if (!TryWriteSeal(first, Successor, true, out Failure))
				{
					return false;
				}
				return TryWriteSeal(second, Successor, true, out Failure);
			}
		}

		internal bool TryCompleteGenerationAdvance(KingdomSealRecord Successor, out string Failure)
		{
			Failure = "";
			if (!ValidStageRecord(Successor) || Successor.Status != KingdomSealStatus.Living
				|| Successor.IsResolved)
			{
				Failure = "only an exact complete living successor can finish a generation handoff";
				return false;
			}
			FileStream gate;
			if (!TryLockStage(Successor.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(Successor.OriginGameId, 'a');
				string slotB = StagePath(Successor.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b))
				{
					Failure = "the origin journal contains an unreadable slot";
					return false;
				}
				if (SameRecord(a, Successor) && SameRecord(b, Successor))
				{
					return true;
				}
				KingdomSealRecord durableNewer;
				if (!TryRecoverableGenerationPair(a, b, out durableNewer)
					|| !SameRecord(durableNewer, Successor))
				{
					Failure = "the origin journal is not the exact adjacent handoff for that successor";
					return false;
				}
				string olderSlot = object.ReferenceEquals(durableNewer, a) ? slotB : slotA;
				return TryWriteSeal(olderSlot, durableNewer, true, out Failure);
			}
		}

		internal bool TryRestoreLivingGeneration(KingdomSealRecord SavedLiving, out string Failure)
		{
			Failure = "";
			if (!ValidStageRecord(SavedLiving) || SavedLiving.Status != KingdomSealStatus.Living
				|| SavedLiving.IsResolved)
			{
				Failure = "only a complete living primary-save generation can restore a journal";
				return false;
			}
			FileStream gate;
			if (!TryLockStage(SavedLiving.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(SavedLiving.OriginGameId, 'a');
				string slotB = StagePath(SavedLiving.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b)
					|| !MayRestoreOver(a, SavedLiving) || !MayRestoreOver(b, SavedLiving)
					|| !RecoverableRestoreJournal(a, b, SavedLiving))
				{
					Failure = "the journal is not a recoverable living generation for that primary save";
					return false;
				}
				if (!SameRecord(a, SavedLiving)
					&& !TryWriteSeal(slotA, SavedLiving, true, out Failure))
				{
					return false;
				}
				if (!SameRecord(b, SavedLiving)
					&& !TryWriteSeal(slotB, SavedLiving, true, out Failure))
				{
					return false;
				}
				return true;
			}
		}

		internal bool TryRestoreRetiredGeneration(KingdomSealLineage SavedRetirement,
			out string Failure)
		{
			Failure = "";
			if (SavedRetirement == null
				|| !KingdomSealReceipt.ValidId(SavedRetirement.LineageId)
				|| !KingdomSealReceipt.ValidId(SavedRetirement.LegacyId)
				|| !KingdomSealReceipt.ValidId(SavedRetirement.OriginGameId)
				|| SavedRetirement.Generation < 0 || SavedRetirement.Generation > 1024
				|| SavedRetirement.Revision < 0)
			{
				Failure = "the saved retirement identity is incomplete";
				return false;
			}
			KingdomSealRecord proof = ReadSlot(LegacyPath(SavedRetirement.LegacyId));
			if (proof == null || proof.Status != KingdomSealStatus.Promoted || !proof.IsResolved
				|| proof.LineageId != SavedRetirement.LineageId
				|| proof.LegacyId != SavedRetirement.LegacyId
				|| proof.OriginGameId != SavedRetirement.OriginGameId
				|| proof.Generation != SavedRetirement.Generation
				|| proof.Revision != SavedRetirement.Revision)
			{
				Failure = "the saved retirement has no exact immutable legacy proof";
				return false;
			}
			KingdomSealRecord retired = KingdomSealRules.Copy(proof);
			retired.Status = KingdomSealStatus.Retired;
			retired.InterregnumRoll = -1;
			retired.InheritedState = -1;
			KingdomSealRecord promotedEcho;
			try
			{
				promotedEcho = KingdomSealRules.PromoteRetirement(retired);
			}
			catch (Exception ex)
			{
				Failure = "the immutable retirement proof could not be reconstructed: " + ex.Message;
				return false;
			}
			if (!ValidStageRecord(retired) || !SameRecord(promotedEcho, proof))
			{
				Failure = "the immutable legacy is not the exact promotion of one retired stage";
				return false;
			}

			FileStream gate;
			if (!TryLockStage(retired.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(retired.OriginGameId, 'a');
				string slotB = StagePath(retired.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b)
					|| !MayRestoreRetirementOver(a, retired)
					|| !MayRestoreRetirementOver(b, retired)
					|| !RecoverableRetirementJournal(a, b))
				{
					Failure = "the journal is not recoverable from that exact saved retirement";
					return false;
				}
				if (!SameRecord(a, retired)
					&& !TryWriteSeal(slotA, retired, true, out Failure))
				{
					return false;
				}
				if (!SameRecord(b, retired)
					&& !TryWriteSeal(slotB, retired, true, out Failure))
				{
					return false;
				}
				return true;
			}
		}

		internal KingdomSealRecord ReadStage(string OriginGameId)
		{
			if (!KingdomSealReceipt.ValidId(OriginGameId))
			{
				return null;
			}
			KingdomSealRecord a = ReadSlot(StagePath(OriginGameId, 'a'));
			KingdomSealRecord b = ReadSlot(StagePath(OriginGameId, 'b'));
			if (a != null && a.OriginGameId != OriginGameId)
			{
				a = null;
			}
			if (b != null && b.OriginGameId != OriginGameId)
			{
				b = null;
			}
			if (SameStageIdentity(a, b))
			{
				return Best(a, b);
			}
			KingdomSealRecord newer;
			if (!TryRecoverableGenerationPair(a, b, out newer))
			{
				return null;
			}
			return newer;
		}

		internal List<string> StagedOrigins(out int Refused)
		{
			Refused = 0;
			List<string> origins = new List<string>();
			HashSet<string> seen = new HashSet<string>();
			bool overflow;
			int refusedJunk;
			foreach (string path in Files(StagesFolder, SealExtension,
				MaxStageFilesScanned, out overflow, out refusedJunk))
			{
				string name = Path.GetFileName(path);
				if (!name.EndsWith(SealExtension, StringComparison.Ordinal))
				{
					Refused++;
					continue;
				}
				string stem = name.Substring(0, name.Length - SealExtension.Length);
				int slotCut = stem.LastIndexOf(".", StringComparison.Ordinal);
				if (slotCut <= 0 || stem.Length - slotCut != 2
					|| (stem[slotCut + 1] != 'a' && stem[slotCut + 1] != 'b'))
				{
					Refused++;
					continue;
				}
				string origin = stem.Substring(0, slotCut);
				if (!KingdomSealReceipt.ValidId(origin))
				{
					Refused++;
					continue;
				}
				if (seen.Add(origin))
				{
					origins.Add(origin);
				}
			}
			if (overflow)
			{
				Refused++;
			}
			Refused += refusedJunk;
			origins.Sort(StringComparer.Ordinal);
			return origins;
		}

		internal bool TryWriteLegacy(KingdomSealRecord Record, out string Failure)
		{
			Failure = "";
			if (Record == null || Record.Status != KingdomSealStatus.Promoted || !Record.IsResolved)
			{
				Failure = "only a promoted legacy with its fate drawn is written";
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Record.LegacyId) || !KingdomSealReceipt.ValidId(Record.LineageId))
			{
				Failure = "the legacy or lineage is not an identifier this build accepts";
				return false;
			}
			string path = LegacyPath(Record.LegacyId);
			if (TryWriteSeal(path, Record, false, out Failure))
			{
				return true;
			}
			KingdomSealRecord existing = ReadSlot(path);
			if (existing != null && existing.LegacyId == Record.LegacyId && SameRecord(existing, Record))
			{
				Failure = "";
				return true;
			}
			return false;
		}

		internal List<KingdomSealRecord> ReadLegacies(out int Refused)
		{
			Refused = 0;
			List<KingdomSealRecord> legacies = new List<KingdomSealRecord>();
			bool overflow;
			int refusedJunk;
			foreach (string path in Files(LegaciesFolder, SealExtension,
				MaxFilesScanned, out overflow, out refusedJunk))
			{
				if (!path.EndsWith(SealExtension, StringComparison.Ordinal))
				{
					continue;
				}
				string name = Path.GetFileName(path);
				string legacy = name.Substring(0, name.Length - SealExtension.Length);
				KingdomSealRecord record = ReadSlot(path);
				if (record == null || record.LegacyId != legacy || record.Status != KingdomSealStatus.Promoted || !record.IsResolved)
				{
					Refused++;
					continue;
				}
				legacies.Add(record);
			}
			if (overflow)
			{
				Refused++;
			}
			Refused += refusedJunk;
			legacies.Sort(delegate(KingdomSealRecord a, KingdomSealRecord b)
			{
				return string.CompareOrdinal(a.LegacyId, b.LegacyId);
			});
			return legacies;
		}

		internal bool TryClaimReservation(KingdomSealRecord Legacy, string TargetGameId, long WrittenTick,
			out KingdomSealReceipt Receipt, out KingdomSealReservationLease Lease, out string Failure)
		{
			Receipt = null;
			Lease = null;
			Failure = "";
			if (Legacy == null || Legacy.Status != KingdomSealStatus.Promoted || !Legacy.IsResolved
				|| !KingdomSealReceipt.ValidId(Legacy.LineageId) || !KingdomSealReceipt.ValidId(Legacy.LegacyId)
				|| !KingdomSealReceipt.ValidId(TargetGameId) || WrittenTick < 0L)
			{
				Failure = "the reservation does not name one valid promoted legacy and target";
				return false;
			}
			KingdomSealRecord stored = ReadSlot(LegacyPath(Legacy.LegacyId));
			if (stored == null || !SameRecord(stored, Legacy))
			{
				Failure = "the legacy is not the immutable record on disk";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Legacy.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing != null)
				{
					if (existing.LineageId == Legacy.LineageId && existing.TargetGameId == TargetGameId
						&& (existing.State == KingdomSealReceiptState.Reserved
							|| existing.State == KingdomSealReceiptState.Committed))
					{
						if (existing.State == KingdomSealReceiptState.Reserved
							&& !TryAcquireLiveClaim(existing, out Lease, out Failure))
						{
							return false;
						}
						Receipt = existing;
						return true;
					}
					Failure = "that legacy already has a claim";
					return false;
				}
				KingdomSealReceipt created = new KingdomSealReceipt
				{
					LineageId = Legacy.LineageId,
					LegacyId = Legacy.LegacyId,
					TargetGameId = TargetGameId,
					State = KingdomSealReceiptState.Reserved,
					WrittenTick = WrittenTick
				};
				if (!TryAcquireLiveClaim(created, out Lease, out Failure))
				{
					return false;
				}
				if (!TryWriteReceiptFile(created, false, out Failure))
				{
					Lease.Dispose();
					Lease = null;
					return false;
				}
				Receipt = created;
				return true;
			}
		}

		internal bool TryAcquireReservationLease(KingdomSealReceipt Receipt,
			out KingdomSealReservationLease Lease, out string Failure)
		{
			Lease = null;
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reserved receipt can hold a live claim";
				return false;
			}
			FileStream receiptsGate;
			if (!TryLockReceipts(out receiptsGate, out Failure))
			{
				return false;
			}
			using (receiptsGate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (!SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed before its live claim was acquired";
					return false;
				}
				return TryAcquireLiveClaim(existing, out Lease, out Failure);
			}
		}

		internal bool TryInspectReceipt(KingdomSealReceipt Expected,
			out KingdomSealReceipt Current, out string Failure)
		{
			Current = null;
			Failure = "";
			if (!ValidReceipt(Expected))
			{
				Failure = "the expected receipt is malformed";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Expected.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null || existing.LineageId != Expected.LineageId
					|| existing.LegacyId != Expected.LegacyId
					|| existing.TargetGameId != Expected.TargetGameId
					|| existing.WrittenTick < Expected.WrittenTick
					|| (existing.State != Expected.State
						&& Expected.State != KingdomSealReceiptState.Reserved))
				{
					Failure = "the current receipt is not a monotone state of the expected tuple";
					return false;
				}
				Current = existing;
				return true;
			}
		}

		internal bool TryCommitReservation(KingdomSealReceipt Reserved,
			KingdomSealReservationLease Lease, long WrittenTick,
			out KingdomSealReceipt Committed, out string Failure)
		{
			Committed = null;
			Failure = "";
			if (!ValidReceipt(Reserved) || Reserved.State != KingdomSealReceiptState.Reserved
				|| Lease == null || !Lease.IsHeld || !Lease.Matches(Reserved)
				|| WrittenTick < Reserved.WrittenTick)
			{
				Failure = "only the live holder of an exact reservation can commit it monotonically";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Reserved.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null || existing.LineageId != Reserved.LineageId
					|| existing.LegacyId != Reserved.LegacyId
					|| existing.TargetGameId != Reserved.TargetGameId)
				{
					Failure = "the reservation tuple changed before it could be committed";
					return false;
				}
				if (existing.State == KingdomSealReceiptState.Committed
					&& existing.WrittenTick >= Reserved.WrittenTick)
				{
					Committed = existing;
					Lease.Dispose();
					return true;
				}
				if (!SameReceipt(existing, Reserved))
				{
					Failure = "the exact reservation changed before it could be committed";
					return false;
				}
				KingdomSealReceipt committed = new KingdomSealReceipt
				{
					LineageId = Reserved.LineageId,
					LegacyId = Reserved.LegacyId,
					TargetGameId = Reserved.TargetGameId,
					State = KingdomSealReceiptState.Committed,
					WrittenTick = WrittenTick
				};
				if (!TryWriteReceiptFile(committed, true, out Failure))
				{
					return false;
				}
				Committed = committed;
				Lease.Dispose();
				return true;
			}
		}

		internal bool TryWriteReceipt(KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!ValidReceipt(Receipt))
			{
				Failure = "the receipt is malformed";
				return false;
			}
			if (Receipt.State == KingdomSealReceiptState.Committed)
			{
				Failure = "a committed receipt requires its exact live reservation claim";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					Failure = "a reservation must be claimed atomically before it can change";
					return false;
				}
				if (existing.LineageId != Receipt.LineageId || existing.TargetGameId != Receipt.TargetGameId)
				{
					Failure = "the receipt tuple does not match the existing claim";
					return false;
				}
				if (existing.State == Receipt.State)
				{
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved
					|| (Receipt.State != KingdomSealReceiptState.Committed
						&& Receipt.State != KingdomSealReceiptState.Declined))
				{
					Failure = "a receipt cannot move backwards or leave a final state";
					return false;
				}
				if (Receipt.WrittenTick < existing.WrittenTick)
				{
					Failure = "a receipt's written tick cannot go backwards";
					return false;
				}
				return TryWriteReceiptFile(Receipt, true, out Failure);
			}
		}

		internal bool TryReleaseReservation(KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reserved receipt can be released";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved || !SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed and cannot be released by this receipt";
					return false;
				}
				KingdomSealReservationLease lease;
				if (!TryAcquireLiveClaim(existing, out lease, out Failure))
				{
					return false;
				}
				using (lease)
				{
					return TryRemoveReservationLocked(existing, out Failure);
				}
			}
		}

		internal bool TryReleaseReservation(KingdomSealReceipt Receipt,
			KingdomSealReservationLease Lease, out string Failure)
		{
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved
				|| Lease == null || !Lease.IsHeld || !Lease.Matches(Receipt))
			{
				Failure = "only the live holder of an exact reserved receipt can release it";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					Lease.Dispose();
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved || !SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed and cannot be released by this live claim";
					return false;
				}
				if (!TryRemoveReservationLocked(existing, out Failure))
				{
					return false;
				}
				Lease.Dispose();
				return true;
			}
		}

		internal bool TryReleaseAbandonedReservation(KingdomSealReceipt Receipt,
			out bool Released, out string Failure)
		{
			Released = false;
			Failure = "";
			if (!ValidReceipt(Receipt) || Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = "only an exact reserved receipt can be reconciled";
				return false;
			}
			FileStream gate;
			if (!TryLockReceipts(out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				KingdomSealReceipt existing;
				if (!TryFindReceipt(Receipt.LegacyId, out existing, out Failure))
				{
					return false;
				}
				if (existing == null)
				{
					Released = true;
					return true;
				}
				if (existing.State != KingdomSealReceiptState.Reserved || !SameReceipt(existing, Receipt))
				{
					Failure = "the reservation changed while reconciliation was examining it";
					return false;
				}
				KingdomSealReservationLease lease;
				bool contended;
				if (!TryAcquireLiveClaim(existing, out lease, out contended, out Failure))
				{
					if (contended)
					{
						Failure = "";
						return true;
					}
					return false;
				}
				using (lease)
				{
					if (!TryRemoveReservationLocked(existing, out Failure))
					{
						return false;
					}
					Released = true;
					return true;
				}
			}
		}

		internal List<KingdomSealReceipt> ReadReceipts()
		{
			int refused;
			return ReadReceipts(out refused);
		}

		internal List<KingdomSealReceipt> ReadReceipts(out int Refused)
		{
			Refused = 0;
			List<KingdomSealReceipt> receipts = new List<KingdomSealReceipt>();
			bool overflow;
			int refusedJunk;
			foreach (string path in Files(ReceiptsFolder, ReceiptExtension,
				MaxFilesScanned, out overflow, out refusedJunk))
			{
				if (!path.EndsWith(ReceiptExtension, StringComparison.Ordinal))
				{
					continue;
				}
				string legacy;
				string target;
				KingdomSealReceipt receipt;
				string text = ReadText(path);
				if (TryParseReceiptTuple(Path.GetFileName(path), out legacy, out target)
					&& text != null && KingdomSealReceipt.TryParse(text, out receipt)
					&& receipt.LegacyId == legacy && receipt.TargetGameId == target)
				{
					receipts.Add(receipt);
				}
				else
				{
					Refused++;
				}
			}
			if (overflow)
			{
				Refused++;
			}
			Refused += refusedJunk;
			return receipts;
		}

		internal HashSet<string> SpentLegacyIds()
		{
			HashSet<string> spent = new HashSet<string>();
			List<KingdomSealReceipt> receipts = ReadReceipts();
			for (int i = 0; i < receipts.Count; i++)
			{
				if (receipts[i].State != KingdomSealReceiptState.Reserved)
				{
					spent.Add(receipts[i].LegacyId);
				}
			}
			return spent;
		}

		private bool TryFindReceipt(string LegacyId, out KingdomSealReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = "";
			bool overflow;
			int refusedJunk;
			IEnumerable<string> paths = Files(ReceiptsFolder, ReceiptExtension,
				MaxFilesScanned, out overflow, out refusedJunk);
			if (overflow || refusedJunk > 0)
			{
				Failure = overflow
					? "the receipt folder holds too many files to claim safely"
					: "the receipt folder contains unrecognized files";
				return false;
			}
			foreach (string path in paths)
			{
				if (!path.EndsWith(ReceiptExtension, StringComparison.Ordinal))
				{
					continue;
				}
				string namedLegacy;
				string namedTarget;
				if (!TryParseReceiptTuple(Path.GetFileName(path), out namedLegacy, out namedTarget))
				{
					Failure = "the receipt folder contains an invalid filename tuple";
					return false;
				}
				string text = ReadText(path);
				KingdomSealReceipt parsed;
				if (text == null || !KingdomSealReceipt.TryParse(text, out parsed)
					|| parsed.LegacyId != namedLegacy || parsed.TargetGameId != namedTarget)
				{
					Failure = "an existing receipt does not match its filename tuple";
					return false;
				}
				if (namedLegacy != LegacyId)
				{
					continue;
				}
				if (Receipt != null)
				{
					Failure = "that legacy has more than one receipt";
					return false;
				}
				Receipt = parsed;
			}
			return true;
		}

		private bool TryWriteSeal(string PathValue, KingdomSealRecord Record, bool ReplaceExisting, out string Failure)
		{
			Failure = "";
			string text;
			try
			{
				text = Record.Compose();
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			string temp = TempPath(PathValue);
			try
			{
				if (!TryEnsureFolderOf(PathValue, out Failure))
				{
					return false;
				}
				bool tempExists;
				if (!TrySafeLeaf(temp, out tempExists, out Failure) || tempExists)
				{
					if (Failure.Length == 0) Failure = "the random seal staging leaf already exists";
					return false;
				}
				_files.WriteAllTextDurable(temp, text);
				KingdomSealRecord echo = ReadSlot(temp);
				if (echo == null || !SameRecord(echo, Record))
				{
					Failure = "the seal did not read back the same";
					return false;
				}
				return Install(temp, PathValue, ReplaceExisting, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			finally
			{
				TryDelete(temp);
			}
		}

		private bool TryWriteReceiptFile(KingdomSealReceipt Receipt, bool ReplaceExisting, out string Failure)
		{
			Failure = "";
			string path = ReceiptPath(Receipt.LegacyId, Receipt.TargetGameId);
			string temp = TempPath(path);
			try
			{
				if (!TryEnsureFolderOf(path, out Failure))
				{
					return false;
				}
				bool tempExists;
				if (!TrySafeLeaf(temp, out tempExists, out Failure) || tempExists)
				{
					if (Failure.Length == 0) Failure = "the random receipt staging leaf already exists";
					return false;
				}
				string text = Receipt.Compose();
				_files.WriteAllTextDurable(temp, text);
				KingdomSealReceipt echo;
				string echoText = ReadText(temp);
				if (echoText == null || !KingdomSealReceipt.TryParse(echoText, out echo)
					|| !SameReceipt(echo, Receipt))
				{
					Failure = "the receipt did not read back the same";
					return false;
				}
				return Install(temp, path, ReplaceExisting, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			finally
			{
				TryDelete(temp);
			}
		}

		private bool Install(string Temp, string PathValue, bool ReplaceExisting, out string Failure)
		{
			Failure = "";
			try
			{
				bool tempExists;
				bool destinationExists;
				if (!TrySafeLeaf(Temp, out tempExists, out Failure) || !tempExists
					|| !TrySafeLeaf(PathValue, out destinationExists, out Failure))
				{
					if (Failure.Length == 0) Failure = "the atomic staging leaf disappeared";
					return false;
				}
				if (!destinationExists)
				{
					_files.MoveNew(Temp, PathValue);
					bool installed;
					return TrySafeLeaf(PathValue, out installed, out Failure) && installed;
				}
				if (!ReplaceExisting)
				{
					Failure = "the destination already exists";
					return false;
				}
				string backup = PathValue + ".backup." + Guid.NewGuid().ToString("N");
				bool backupExists;
				if (!TrySafeLeaf(backup, out backupExists, out Failure) || backupExists)
				{
					if (Failure.Length == 0) Failure = "the random backup leaf already exists";
					return false;
				}
				try
				{
					_files.ReplaceAtomic(Temp, PathValue, backup);
					bool installed;
					if (!TrySafeLeaf(PathValue, out installed, out Failure) || !installed)
					{
						if (Failure.Length == 0) Failure = "the atomic replacement did not leave a regular destination";
						return false;
					}
					TryDelete(backup);
					return true;
				}
				catch (Exception ex)
				{
					// No delete-then-move fallback: after a failed replacement only the
					// platform can still know whether the old durable leaf survived.
					Failure = ex.Message;
					return false;
				}
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		private KingdomSealRecord ReadSlot(string PathValue)
		{
			string text = ReadText(PathValue);
			if (text == null)
			{
				return null;
			}
			KingdomSealRecord record;
			KingdomSealFault fault;
			string detail;
			return KingdomSealRecord.TryParse(text, out record, out fault, out detail) ? record : null;
		}

		private string ReadText(string PathValue)
		{
			try
			{
				bool exists;
				string failure;
				if (!TrySafeLeaf(PathValue, out exists, out failure) || !exists)
				{
					return null;
				}
				long length = _files.Length(PathValue);
				if (length < 0L || length > KingdomSealFormat.MaxFileChars)
				{
					return null;
				}
				string text = _files.ReadAllText(PathValue);
				bool stillExists;
				return TrySafeLeaf(PathValue, out stillExists, out failure) && stillExists ? text : null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private IEnumerable<string> Files(string Folder, string Extension, int RecognizedLimit,
			out bool Overflow, out int RefusedJunk)
		{
			Overflow = false;
			RefusedJunk = 0;
			try
			{
				string folder;
				bool folderExists;
				string folderFailure;
				if (!TrySafeFolder(Folder, false, out folder, out folderExists, out folderFailure))
				{
					Overflow = true;
					return new string[0];
				}
				if (!folderExists)
				{
					return new string[0];
				}
				List<string> found = new List<string>();
				int inspected = 0;
				int totalLimit = RecognizedLimit + MaxFilesScanned;
				foreach (string path in Directory.EnumerateFiles(folder, "*",
					SearchOption.TopDirectoryOnly))
				{
					if (++inspected > totalLimit)
					{
						Overflow = true;
						break;
					}
					string name = Path.GetFileName(path);
					bool leafExists;
					string leafFailure;
					if (!TrySafeLeaf(path, out leafExists, out leafFailure) || !leafExists)
					{
						RefusedJunk++;
						continue;
					}
					if (name.EndsWith(Extension, StringComparison.Ordinal))
					{
						if (found.Count >= RecognizedLimit)
						{
							Overflow = true;
							break;
						}
						found.Add(path);
						continue;
					}
					if (KnownOperationalJunk(Folder, name, Extension))
					{
						continue;
					}
					if (RefusedJunk < MaxFilesScanned + 1)
					{
						RefusedJunk++;
					}
				}
				found.Sort(StringComparer.Ordinal);
				return found;
			}
			catch (Exception)
			{
				Overflow = true;
				return new string[0];
			}
		}

		private static bool KnownOperationalJunk(string Folder, string Name, string Extension)
		{
			if (Folder == ReceiptsFolder && Name == ".claims.lock")
			{
				return true;
			}
			if (Folder == StagesFolder && Name.StartsWith(".journal-", StringComparison.Ordinal)
				&& Name.EndsWith(".lock", StringComparison.Ordinal))
			{
				return true;
			}
			return Name.IndexOf(Extension + ".writing.", StringComparison.Ordinal) >= 0
				|| Name.IndexOf(Extension + ".backup.", StringComparison.Ordinal) >= 0
				|| Name.IndexOf(Extension + ".released.", StringComparison.Ordinal) >= 0;
		}

		private bool TryAcquireLiveClaim(KingdomSealReceipt Receipt,
			out KingdomSealReservationLease Lease, out string Failure)
		{
			bool contended;
			if (TryAcquireLiveClaim(Receipt, out Lease, out contended, out Failure))
			{
				return true;
			}
			if (contended)
			{
				Failure = "the reservation is held by a live target world";
			}
			return false;
		}

		private bool TryAcquireLiveClaim(KingdomSealReceipt Receipt,
			out KingdomSealReservationLease Lease, out bool Contended, out string Failure)
		{
			Lease = null;
			Contended = false;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(ClaimsFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string claim = ClaimPath(Receipt.LegacyId, Receipt.TargetGameId);
				bool claimExists;
				if (!TrySafeLeaf(claim, out claimExists, out Failure))
				{
					return false;
				}
				FileStream gate = new FileStream(claim,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(claim, out openedExists, out Failure) || !openedExists)
				{
					gate.Dispose();
					return false;
				}
				Lease = new KingdomSealReservationLease(Receipt, gate);
				return true;
			}
			catch (IOException)
			{
				// A sharing violation is the positive live-claim signal. Other I/O failures are
				// deliberately treated the same way: without exclusive proof, release is unsafe.
				Contended = true;
				return false;
			}
			catch (Exception ex)
			{
				Failure = "the reservation live-claim lock is unavailable: " + ex.Message;
				return false;
			}
		}

		private bool TryRemoveReservationLocked(KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			string path = ReceiptPath(Receipt.LegacyId, Receipt.TargetGameId);
			string released = path + ".released." + Guid.NewGuid().ToString("N");
			try
			{
				bool sourceExists;
				bool releasedExists;
				if (!TrySafeLeaf(path, out sourceExists, out Failure) || !sourceExists
					|| !TrySafeLeaf(released, out releasedExists, out Failure) || releasedExists)
				{
					if (Failure.Length == 0) Failure = "the exact reserved receipt leaf is unavailable";
					return false;
				}
				_files.MoveNew(path, released);
				if (!TrySafeLeaf(released, out releasedExists, out Failure) || !releasedExists)
				{
					if (Failure.Length == 0) Failure = "the released receipt is not a regular leaf";
					return false;
				}
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			TryDelete(released);
			return true;
		}

		private bool TryLockReceipts(out FileStream Gate, out string Failure)
		{
			Gate = null;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(ReceiptsFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string path = Path.Combine(_root, ReceiptsFolder, ".claims.lock");
				bool exists;
				if (!TrySafeLeaf(path, out exists, out Failure))
				{
					return false;
				}
				Gate = new FileStream(path,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(path, out openedExists, out Failure) || !openedExists)
				{
					Gate.Dispose();
					Gate = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the receipt claim lock is unavailable: " + ex.Message;
				return false;
			}
		}

		private bool TryLockStage(string OriginGameId, out FileStream Gate, out string Failure)
		{
			Gate = null;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(StagesFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string path = Path.Combine(_root, StagesFolder,
					".journal-" + OriginGameId + ".lock");
				bool exists;
				if (!TrySafeLeaf(path, out exists, out Failure))
				{
					return false;
				}
				Gate = new FileStream(path,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(path, out openedExists, out Failure) || !openedExists)
				{
					Gate.Dispose();
					Gate = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the origin journal lock is unavailable: " + ex.Message;
				return false;
			}
		}

		private bool TrySafeFolder(string Folder, bool Create, out string PathValue,
			out bool Exists, out string Failure)
		{
			PathValue = "";
			Exists = false;
			Failure = "";
			if (!IsFixedFolder(Folder))
			{
				Failure = "the store path does not name a fixed folder";
				return false;
			}
			bool rootExists;
			if (!TrySafeDirectory(_root, Create, out rootExists, out Failure))
			{
				return false;
			}
			if (!rootExists)
			{
				return true;
			}
			PathValue = Path.GetFullPath(Path.Combine(_root, Folder));
			if (!Contained(PathValue))
			{
				Failure = "the store folder escapes its root";
				return false;
			}
			return TrySafeDirectory(PathValue, Create, out Exists, out Failure);
		}

		private bool TrySafeDirectory(string PathValue, bool Create, out bool Exists,
			out string Failure)
		{
			Exists = false;
			Failure = "";
			FileAttributes attributes;
			if (!TryDirectoryAttributes(PathValue, out attributes, out Exists, out Failure))
			{
				return false;
			}
			if (!Exists && Create)
			{
				try
				{
					Directory.CreateDirectory(PathValue);
				}
				catch (Exception ex)
				{
					Failure = "the store folder could not be created: " + ex.Message;
					return false;
				}
				if (!TryDirectoryAttributes(PathValue, out attributes, out Exists, out Failure))
				{
					return false;
				}
			}
			if (Exists && ((attributes & FileAttributes.Directory) == 0
				|| (attributes & FileAttributes.ReparsePoint) != 0))
			{
				Failure = "the store folder is not a direct regular directory";
				return false;
			}
			return true;
		}

		private static bool TryDirectoryAttributes(string PathValue, out FileAttributes Attributes,
			out bool Exists, out string Failure)
		{
			Attributes = 0;
			Exists = false;
			Failure = "";
			try
			{
				Attributes = File.GetAttributes(PathValue);
				Exists = true;
				return true;
			}
			catch (FileNotFoundException)
			{
				return true;
			}
			catch (DirectoryNotFoundException)
			{
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the store path could not be inspected: " + ex.Message;
				return false;
			}
		}

		private bool TrySafeLeaf(string PathValue, out bool Exists, out string Failure)
		{
			Exists = false;
			Failure = "";
			string full;
			try
			{
				full = Path.GetFullPath(PathValue);
			}
			catch (Exception ex)
			{
				Failure = "the store leaf path is invalid: " + ex.Message;
				return false;
			}
			if (!Contained(full) || !string.Equals(full, PathValue, _pathComparison))
			{
				Failure = "the store leaf escapes its root";
				return false;
			}
			string folderName;
			if (!TryFixedFolderOf(full, out folderName))
			{
				Failure = "the store leaf is outside a fixed folder";
				return false;
			}
			string folder;
			bool folderExists;
			if (!TrySafeFolder(folderName, false, out folder, out folderExists, out Failure))
			{
				return false;
			}
			if (!folderExists)
			{
				return true;
			}
			FileAttributes attributes;
			try
			{
				attributes = _files.Attributes(full);
				Exists = true;
			}
			catch (FileNotFoundException)
			{
				return true;
			}
			catch (DirectoryNotFoundException)
			{
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the store leaf could not be inspected: " + ex.Message;
				return false;
			}
			if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
			{
				Failure = "the store leaf is not a direct regular file";
				return false;
			}
			return true;
		}

		private bool TryEnsureFolderOf(string PathValue, out string Failure)
		{
			Failure = "";
			string folderName;
			if (!TryFixedFolderOf(Path.GetFullPath(PathValue), out folderName))
			{
				Failure = "the store write is outside a fixed folder";
				return false;
			}
			string folder;
			bool exists;
			return TrySafeFolder(folderName, true, out folder, out exists, out Failure) && exists;
		}

		private bool Contained(string PathValue)
		{
			return PathValue != null && PathValue.StartsWith(_rootPrefix, _pathComparison);
		}

		private bool TryFixedFolderOf(string PathValue, out string Folder)
		{
			Folder = "";
			string parent = Path.GetDirectoryName(PathValue);
			string[] fixedFolders = new string[]
			{
				StagesFolder, LegaciesFolder, ReceiptsFolder, ClaimsFolder
			};
			for (int i = 0; i < fixedFolders.Length; i++)
			{
				string expected = Path.GetFullPath(Path.Combine(_root, fixedFolders[i]));
				if (string.Equals(parent, expected, _pathComparison))
				{
					Folder = fixedFolders[i];
					return true;
				}
			}
			return false;
		}

		private static bool IsFixedFolder(string Folder)
		{
			return Folder == StagesFolder || Folder == LegaciesFolder
				|| Folder == ReceiptsFolder || Folder == ClaimsFolder;
		}

		private void TryDelete(string PathValue)
		{
			try
			{
				bool exists;
				string failure;
				if (TrySafeLeaf(PathValue, out exists, out failure) && exists)
				{
					_files.DeleteIfExists(PathValue);
				}
			}
			catch (Exception)
			{
			}
		}

		private static KingdomSealRecord Best(KingdomSealRecord A, KingdomSealRecord B)
		{
			if (A == null)
			{
				return B;
			}
			if (B == null)
			{
				return A;
			}
			return KingdomSealRules.Later(A, B) ? A : B;
		}

		private static bool SameRecord(KingdomSealRecord A, KingdomSealRecord B)
		{
			try
			{
				return A != null && B != null && A.Compose() == B.Compose();
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool ValidStageRecord(KingdomSealRecord Record)
		{
			if (Record == null)
			{
				return false;
			}
			try
			{
				KingdomSealRecord parsed;
				KingdomSealFault fault;
				string detail;
				return KingdomSealRecord.TryParse(Record.Compose(), out parsed, out fault, out detail)
					&& SameRecord(parsed, Record);
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static bool ValidGenerationHandoff(KingdomSealRecord Previous, KingdomSealRecord Successor, out string Failure)
		{
			Failure = "";
			if (!ValidStageRecord(Previous) || !ValidStageRecord(Successor)
				|| (Previous.Status != KingdomSealStatus.Living && Previous.Status != KingdomSealStatus.Retired)
				|| Successor.Status != KingdomSealStatus.Living || Successor.IsResolved)
			{
				Failure = "the handoff requires one complete living or retired stage and one complete living successor";
				return false;
			}
			if (Previous.LineageId != Successor.LineageId || Previous.OriginGameId != Successor.OriginGameId)
			{
				Failure = "a generation handoff cannot change lineage or origin game";
				return false;
			}
			if (Previous.Generation == int.MaxValue || Successor.Generation != Previous.Generation + 1)
			{
				Failure = "a generation handoff must advance exactly one generation";
				return false;
			}
			if (Previous.LegacyId == Successor.LegacyId)
			{
				Failure = "every generation must mint a distinct legacy id";
				return false;
			}
			if (Previous.Revision == int.MaxValue || Successor.Revision != Previous.Revision + 1)
			{
				Failure = "a generation handoff must advance the origin revision exactly once";
				return false;
			}
			if (Successor.WrittenTick < Previous.WrittenTick)
			{
				Failure = "a generation handoff cannot move its diagnostic tick backwards";
				return false;
			}
			return true;
		}

		private bool SlotIsBroken(string PathValue, KingdomSealRecord Record)
		{
			try
			{
				bool exists;
				string failure;
				return !TrySafeLeaf(PathValue, out exists, out failure) || (exists && Record == null);
			}
			catch (Exception)
			{
				return true;
			}
		}

		private static bool MayRestoreOver(KingdomSealRecord Existing, KingdomSealRecord Saved)
		{
			if (Existing == null)
			{
				return true;
			}
			if (Existing.Status != KingdomSealStatus.Living || Existing.OriginGameId != Saved.OriginGameId
				|| Existing.LineageId != Saved.LineageId)
			{
				return false;
			}
			if (Existing.Generation == Saved.Generation)
			{
				return Existing.LegacyId == Saved.LegacyId;
			}
			return Existing.Generation > Saved.Generation && Existing.LegacyId != Saved.LegacyId
				&& Existing.Revision > Saved.Revision
				&& Existing.WrittenTick >= Saved.WrittenTick;
		}

		private static bool MayRestoreRetirementOver(KingdomSealRecord Existing,
			KingdomSealRecord Retired)
		{
			if (Existing == null || SameRecord(Existing, Retired))
			{
				return true;
			}
			if (Existing.Status != KingdomSealStatus.Living
				|| Existing.OriginGameId != Retired.OriginGameId
				|| Existing.LineageId != Retired.LineageId)
			{
				return false;
			}
			if (Existing.Generation == Retired.Generation)
			{
				return Existing.LegacyId == Retired.LegacyId
					&& Existing.Revision < Retired.Revision;
			}
			return Existing.Generation > Retired.Generation
				&& Existing.LegacyId != Retired.LegacyId
				&& Existing.Revision > Retired.Revision
				&& Existing.WrittenTick >= Retired.WrittenTick;
		}

		private static bool RecoverableRetirementJournal(KingdomSealRecord A,
			KingdomSealRecord B)
		{
			if (A == null || B == null || SameStageIdentity(A, B))
			{
				return true;
			}
			KingdomSealRecord newer;
			return TryRecoverableGenerationPair(A, B, out newer);
		}

		private static bool RecoverableRestoreJournal(KingdomSealRecord A, KingdomSealRecord B,
			KingdomSealRecord Saved)
		{
			if (A == null || B == null || SameStageIdentity(A, B))
			{
				return true;
			}
			if (SameStageIdentity(A, Saved) || SameStageIdentity(B, Saved))
			{
				return true;
			}
			KingdomSealRecord newer;
			return TryRecoverableGenerationPair(A, B, out newer);
		}

		private static bool TryRecoverableGenerationPair(KingdomSealRecord A, KingdomSealRecord B, out KingdomSealRecord Newer)
		{
			Newer = null;
			if (A == null || B == null || A.OriginGameId != B.OriginGameId || A.LineageId != B.LineageId
				|| A.Generation == B.Generation)
			{
				return false;
			}
			KingdomSealRecord older = (A.Generation < B.Generation) ? A : B;
			KingdomSealRecord newer = object.ReferenceEquals(older, A) ? B : A;
			if (older.Generation == int.MaxValue || newer.Generation != older.Generation + 1
				|| older.LegacyId == newer.LegacyId
				|| (older.Status != KingdomSealStatus.Living && older.Status != KingdomSealStatus.Retired)
				|| newer.Status != KingdomSealStatus.Living
				|| older.Revision == int.MaxValue || newer.Revision != older.Revision + 1
				|| newer.WrittenTick < older.WrittenTick)
			{
				return false;
			}
			Newer = newer;
			return true;
		}

		private static bool SameStageIdentity(KingdomSealRecord A, KingdomSealRecord B)
		{
			return A == null || B == null || (A.OriginGameId == B.OriginGameId
				&& A.LineageId == B.LineageId && A.LegacyId == B.LegacyId && A.Generation == B.Generation);
		}

		private static bool SameReceipt(KingdomSealReceipt A, KingdomSealReceipt B)
		{
			return A != null && B != null && A.LineageId == B.LineageId && A.LegacyId == B.LegacyId
				&& A.TargetGameId == B.TargetGameId && A.State == B.State && A.WrittenTick == B.WrittenTick;
		}

		private static bool ValidReceipt(KingdomSealReceipt Receipt)
		{
			return Receipt != null && KingdomSealReceipt.ValidId(Receipt.LineageId)
				&& KingdomSealReceipt.ValidId(Receipt.LegacyId) && KingdomSealReceipt.ValidId(Receipt.TargetGameId)
				&& Receipt.WrittenTick >= 0L && (int)Receipt.State >= 0 && (int)Receipt.State <= 2;
		}

		private static string TempPath(string PathValue)
		{
			return PathValue + ".writing." + Guid.NewGuid().ToString("N");
		}

		internal string StagePath(string Origin, char Slot)
		{
			if (!KingdomSealReceipt.ValidId(Origin) || (Slot != 'a' && Slot != 'b'))
			{
				throw new ArgumentException("A stage path requires one safe origin and slot.");
			}
			return Path.Combine(_root, StagesFolder, Origin + "." + Slot + SealExtension);
		}

		internal string LegacyPath(string Legacy)
		{
			if (!KingdomSealReceipt.ValidId(Legacy))
			{
				throw new ArgumentException("A legacy path requires one safe legacy id.");
			}
			return Path.Combine(_root, LegaciesFolder, Legacy + SealExtension);
		}

		internal string ReceiptPath(string Legacy, string Target)
		{
			if (!KingdomSealReceipt.ValidId(Legacy) || !KingdomSealReceipt.ValidId(Target))
			{
				throw new ArgumentException("A receipt path requires one safe tuple.");
			}
			return Path.Combine(_root, ReceiptsFolder, ReceiptFileName(Legacy, Target));
		}

		private string ClaimPath(string Legacy, string Target)
		{
			if (!KingdomSealReceipt.ValidId(Legacy) || !KingdomSealReceipt.ValidId(Target))
			{
				throw new ArgumentException("A live-claim path requires one safe tuple.");
			}
			return Path.Combine(_root, ClaimsFolder, ReceiptFileName(Legacy, Target) + ".live");
		}

		private static string ReceiptFileName(string Legacy, string Target)
		{
			return Legacy.Length.ToString(CultureInfo.InvariantCulture) + "_" + Legacy
				+ Target.Length.ToString(CultureInfo.InvariantCulture) + "_" + Target + ReceiptExtension;
		}

		private static bool TryParseReceiptTuple(string FileName, out string Legacy, out string Target)
		{
			Legacy = "";
			Target = "";
			if (FileName == null || !FileName.EndsWith(ReceiptExtension, StringComparison.Ordinal))
			{
				return false;
			}
			string stem = FileName.Substring(0, FileName.Length - ReceiptExtension.Length);
			int at = 0;
			int legacyLength;
			if (!ReadLength(stem, ref at, out legacyLength) || legacyLength <= 0
				|| at + legacyLength > stem.Length)
			{
				return false;
			}
			Legacy = stem.Substring(at, legacyLength);
			at += legacyLength;
			int targetLength;
			if (!ReadLength(stem, ref at, out targetLength) || targetLength <= 0
				|| at + targetLength != stem.Length)
			{
				Legacy = "";
				return false;
			}
			Target = stem.Substring(at, targetLength);
			return KingdomSealReceipt.ValidId(Legacy) && KingdomSealReceipt.ValidId(Target);
		}

		private static bool ReadLength(string Value, ref int At, out int Length)
		{
			Length = 0;
			int start = At;
			while (At < Value.Length && Value[At] >= '0' && Value[At] <= '9')
			{
				if (At - start >= 3)
				{
					return false;
				}
				Length = Length * 10 + (Value[At] - '0');
				At++;
			}
			if (At == start || At >= Value.Length || Value[At] != '_')
			{
				return false;
			}
			At++;
			return Length <= KingdomSealRecord.MaxIdChars;
		}
	}
}
