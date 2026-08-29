using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure validation, rendering and bounded-CAS rules for D5.</summary>
	public static partial class KingdomBodyHistoryRules
	{
		public const int MaxRows = 8;
		public const int MaxAnatomyParts = 64;
		public const int MaxIdBytes = 128;
		public const int MaxTextBytes = 512;
		public const int LabContractVersion = 1;
		public const string CompletedLabProcedureKind = "completed-lab-procedure";

		private const string BodyObjectPrefix = "taf:object:";
		private const string LabOwnerPrefix = "taf:lab-procedure:v1:";
		private const string HistoryPrefix = "taf:body-history:";

		public static string AnatomyDigest(string ResidentIdentity, string BodyObjectId,
			IList<KingdomLiveAnatomyPart> OrderedParts)
		{
			List<string> fields = new List<string>
			{
				"TAF-LIVE-ANATOMY-V1", ResidentIdentity, BodyObjectId,
				Number(OrderedParts == null ? -1 : OrderedParts.Count)
			};
			for (int i = 0; OrderedParts != null && i < OrderedParts.Count; i++)
			{
				KingdomLiveAnatomyPart part = OrderedParts[i];
				fields.Add(Number(part == null ? -1 : part.NativeOrderIndex));
				fields.Add(part == null ? null : part.NativePath);
				fields.Add(part == null ? null : part.Type);
				fields.Add(part == null ? null : part.OrdinalName);
				fields.Add(Number(part == null ? -1 : part.Category));
				fields.Add(part != null && part.Extrinsic ? "1" : "0");
				fields.Add(part == null ? null : part.CyberneticsBlueprint);
			}
			return Hash(fields.ToArray());
		}

		public static bool TryView(KingdomLiveAnatomySnapshot Snapshot,
			out string View, out string Failure)
		{
			View = null;
			Failure = null;
			if (!ValidAnatomy(Snapshot))
				return Fail("exact loaded anatomy is unavailable", out Failure);
			StringBuilder text = new StringBuilder("Current anatomy: ");
			for (int i = 0; i < Snapshot.OrderedParts.Count; i++)
			{
				if (i > 0) text.Append("; ");
				KingdomLiveAnatomyPart part = Snapshot.OrderedParts[i];
				text.Append(part.OrdinalName);
				if (part.Extrinsic) text.Append(" [extrinsic]");
				if (!string.IsNullOrEmpty(part.CyberneticsBlueprint))
					text.Append(" [cybernetics: ").Append(part.CyberneticsBlueprint).Append(']');
			}
			View = text.ToString();
			if (Utf8(View, 4096)) return true;
			View = null;
			return Fail("anatomy view exceeds its cap", out Failure);
		}

		internal static bool TryRecordWitnessedProcedure(KingdomBodyHistoryBook Book,
			long ExpectedRevision, KingdomWitnessedBodyEventEvidence Evidence,
			out KingdomBodyHistoryReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = null;
			if (!TryValidate(Book, out Failure)) return false;
			if (!ValidEvidence(Evidence))
				return Fail("completed lab-procedure evidence is invalid", out Failure);
			string digest = ReceiptDigest(Evidence.ResidentIdentity, Evidence.BodyObjectId,
				Evidence.ProcedureKey, Evidence.OwnerReceiptId, Evidence.BodyPartFact,
				Evidence.WitnessedTick);
			string receiptId = HistoryPrefix + digest;
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomBodyHistoryReceipt row = Book.Rows[i];
				if (row.ReceiptId == receiptId)
				{
					Receipt = row.Copy();
					return true;
				}
				if (row.ProcedureReceiptId == Evidence.OwnerReceiptId)
					return Fail("procedure owner is already recorded with different facts",
						out Failure);
			}
			if (Book.Revision != ExpectedRevision)
				return Fail("stale body history revision", out Failure);
			if (Book.Rows.Count >= MaxRows)
				return Fail("body history capacity is full", out Failure);

			KingdomBodyHistoryBook candidate = Book.Copy();
			candidate.Rows.Add(new KingdomBodyHistoryReceipt
			{
				ReceiptId = receiptId,
				ResidentIdentity = Evidence.ResidentIdentity,
				BodyObjectId = Evidence.BodyObjectId,
				ProcedureKey = Evidence.ProcedureKey,
				ProcedureReceiptId = Evidence.OwnerReceiptId,
				BodyPartFact = Evidence.BodyPartFact,
				WitnessedTick = Evidence.WitnessedTick,
				Description = Description(Evidence.BodyPartFact),
				Digest = digest
			});
			candidate.Rows.Sort(delegate(KingdomBodyHistoryReceipt left,
				KingdomBodyHistoryReceipt right)
			{
				return string.CompareOrdinal(left.ReceiptId, right.ReceiptId);
			});
			candidate.Revision++;
			if (!TryValidate(candidate, out Failure)) return false;
			Book.Revision = candidate.Revision;
			Book.Rows = candidate.Rows;
			for (int i = 0; i < Book.Rows.Count; i++)
				if (Book.Rows[i].ReceiptId == receiptId) Receipt = Book.Rows[i].Copy();
			return Receipt != null;
		}

		public static bool TryValidate(KingdomBodyHistoryBook Book, out string Failure)
		{
			Failure = null;
			if (Book == null || Book.Revision < 0 || Book.Rows == null
				|| Book.Rows.Count > MaxRows)
				return Fail("body history book is invalid", out Failure);
			string previous = null;
			HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomBodyHistoryReceipt row = Book.Rows[i];
				string digest = row == null ? null : ReceiptDigest(row.ResidentIdentity,
					row.BodyObjectId, row.ProcedureKey, row.ProcedureReceiptId,
					row.BodyPartFact, row.WitnessedTick);
				if (row == null || row.Version != 1 || !Identifier(row.ResidentIdentity)
					|| !PrefixedIdentifier(row.BodyObjectId, BodyObjectPrefix)
					|| !PrefixedIdentifier(row.ProcedureReceiptId, LabOwnerPrefix)
					|| !Text(row.ProcedureKey) || !Text(row.BodyPartFact)
					|| row.WitnessedTick < 0 || row.Digest != digest
					|| row.ReceiptId != HistoryPrefix + digest
					|| row.Description != Description(row.BodyPartFact)
					|| !Utf8(row.Description, 1024)
					|| previous != null && string.CompareOrdinal(previous, row.ReceiptId) >= 0
					|| !owners.Add(row.ProcedureReceiptId))
					return Fail("body history row is invalid", out Failure);
				previous = row.ReceiptId;
			}
			return true;
		}

		internal static string CompletedLabProcedureReceiptId(params string[] OwnerFields)
		{
			if (OwnerFields == null || OwnerFields.Length < 1 || OwnerFields.Length > 16)
				return null;
			string[] fields = new string[OwnerFields.Length + 1];
			fields[0] = "TAF-COMPLETED-LAB-PROCEDURE-V1";
			for (int i = 0; i < OwnerFields.Length; i++)
			{
				if (!OwnerField(OwnerFields[i])) return null;
				fields[i + 1] = OwnerFields[i];
			}
			string digest = Hash(fields);
			return digest == null ? null : LabOwnerPrefix + digest;
		}

		internal static bool ValidEvidence(KingdomWitnessedBodyEventEvidence Evidence)
		{
			return Evidence != null && Evidence.OwnerKind == CompletedLabProcedureKind
				&& PrefixedIdentifier(Evidence.OwnerReceiptId, LabOwnerPrefix)
				&& Identifier(Evidence.ResidentIdentity)
				&& PrefixedIdentifier(Evidence.BodyObjectId, BodyObjectPrefix)
				&& Text(Evidence.ProcedureKey) && Text(Evidence.BodyPartFact)
				&& Evidence.WitnessedTick >= 0;
		}

		private static bool ValidAnatomy(KingdomLiveAnatomySnapshot Snapshot)
		{
			if (Snapshot == null || !Identifier(Snapshot.ResidentIdentity)
				|| !PrefixedIdentifier(Snapshot.BodyObjectId, BodyObjectPrefix)
				|| !Digest(Snapshot.BodyIdentityDigest) || Snapshot.ObservedTick < 0
				|| Snapshot.OrderedParts == null || Snapshot.OrderedParts.Count < 1
				|| Snapshot.OrderedParts.Count > MaxAnatomyParts) return false;
			HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Snapshot.OrderedParts.Count; i++)
			{
				KingdomLiveAnatomyPart part = Snapshot.OrderedParts[i];
				if (part == null || part.NativeOrderIndex != i
					|| !NativePath(part.NativePath) || !paths.Add(part.NativePath)
					|| !Text(part.Type) || !Text(part.OrdinalName) || part.Category < 0
					|| !OptionalText(part.CyberneticsBlueprint)) return false;
			}
			return Snapshot.BodyIdentityDigest == AnatomyDigest(Snapshot.ResidentIdentity,
				Snapshot.BodyObjectId, Snapshot.OrderedParts);
		}

		private static string ReceiptDigest(string Resident, string BodyId,
			string ProcedureKey, string ProcedureReceipt, string PartFact, long Tick)
		{
			return Hash("TAF-BODY-HISTORY-ROW-V1", Resident, BodyId, ProcedureKey,
				ProcedureReceipt, PartFact, Number(Tick));
		}

		private static string Description(string PartFact)
		{
			return "Witnessed procedure: " + PartFact + ".";
		}

		private static string Number(long Value)
		{
			return Value.ToString(CultureInfo.InvariantCulture);
		}

		private static string Hash(params string[] Fields)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream,
					new UTF8Encoding(false, true), true))
				{
					for (int i = 0; i < Fields.Length; i++) writer.Write(Fields[i] ?? "");
					writer.Flush();
					using (SHA256 sha = SHA256.Create())
					{
						byte[] bytes = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(bytes.Length * 2);
						for (int i = 0; i < bytes.Length; i++)
							text.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch (EncoderFallbackException) { return null; }
		}

		private static bool Identifier(string Value)
		{
			return Value != null && Value.StartsWith("taf:", StringComparison.Ordinal)
				&& Utf8(Value, MaxIdBytes);
		}

		private static bool PrefixedIdentifier(string Value, string Prefix)
		{
			return Value != null && Value.StartsWith(Prefix, StringComparison.Ordinal)
				&& Utf8(Value, MaxIdBytes);
		}

		private static bool Text(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Utf8(Value, MaxTextBytes);
		}

		private static bool OptionalText(string Value)
		{
			return string.IsNullOrEmpty(Value) || Utf8(Value, MaxTextBytes);
		}

		private static bool OwnerField(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Utf8(Value, MaxTextBytes);
		}

		private static bool Digest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool Utf8(string Value, int Maximum)
		{
			try
			{
				return Value != null && Value.IndexOf('\0') < 0
					&& new UTF8Encoding(false, true).GetByteCount(Value) <= Maximum;
			}
			catch (EncoderFallbackException) { return false; }
		}

		private static bool Fail(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
