using System;
using System.Security.Cryptography;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		/// <summary>Exact wire-v4/format-5 adoption; old consignment landing was not provable.</summary>
		internal static void MigrateWireV4(KingdomTradeBook Book)
		{
			if (Book == null || Book.FormatVersion != 5) return;
			KingdomTradeOperation operation = Book.OpenOperation;
			KingdomTradeProof pending = Book.PendingRetirement;
			bool migratePending = operation != null && pending != null;
			if (migratePending)
			{
				string oldDigest = OperationEvidenceDigestV4(operation);
				if (!ValidId(oldDigest) || !string.Equals(pending.OperationEvidenceHash,
					oldDigest, StringComparison.Ordinal))
				{
					Book.FormatVersion = CurrentFormatVersion;
					QuarantineBook(Book,
						"wire-v4 pending retirement did not authenticate its exact prior operation");
					return;
				}
			}
			Book.FormatVersion = CurrentFormatVersion;
			QuarantineLegacyConsignmentWithoutWitness(Book, "wire-v4");
			if (migratePending)
				pending.OperationEvidenceHash = OperationEvidenceDigest(operation);
		}

		private static string OperationEvidenceDigestV4(KingdomTradeOperation Operation)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook
				{
					FormatVersion = 5, OpenOperation = Operation
				};
				byte[] bytes = KingdomTradeCodec.EncodePayloadV4ForMigration(evidence);
				string inner;
				using (SHA256 sha = SHA256.Create()) inner = Hex(sha.ComputeHash(bytes));
				return CanonicalId("operation-proof", Operation.Sequence, Operation.Id, inner);
			}
			catch { return null; }
		}

		private static void QuarantineLegacyConsignmentWithoutWitness(
			KingdomTradeBook Book, string Wire)
		{
			KingdomTradeOperation operation = Book?.OpenOperation;
			if (operation == null || operation.Kind !=
				KingdomTradeOperationKind.PolityConsignmentDelivery ||
				operation.PolityRecipient != null) return;
			SealUnstartedPolityConsignmentLegs(operation);
			operation.Phase = KingdomTradePhase.Quarantined;
			operation.Fault = AppendFault(operation.Fault, Wire +
				" polity consignment lacks an exact recipient body/projection witness; " +
				"no debit or success may resume");
		}
	}
}
