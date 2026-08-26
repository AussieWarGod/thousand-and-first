using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		private static void NormalizeProjections(KingdomTradeBook Book)
		{
			if (Book.Projections == null)
			{
				QuarantineBook(Book, "missing projection evidence list");
				return;
			}
			if (Book.Projections.Count > MaxProjectionRows)
			{
				Book.SchemaState = KingdomTradeSchemaState.Quarantined;
				Book.SchemaFault = AppendFault(Book.SchemaFault,
					"active per-city projection row cap exceeded; no authority rows were discarded");
				return;
			}
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow row = Book.Projections[i];
				if (row == null)
				{
					Book.SchemaState = KingdomTradeSchemaState.Quarantined;
					Book.SchemaFault = AppendFault(Book.SchemaFault,
						"null active projection authority row");
					return;
				}
				bool oversized = TooLong(row.OperationId, MaxIdChars)
					|| TooLong(row.SettlementId, MaxIdChars)
					|| TooLong(row.ZoneId, MaxNameChars)
					|| TooLong(row.ProjectionId, MaxIdChars)
					|| TooLong(row.ObjectId, MaxIdChars);
				if (oversized || row.OperationSequence <= 0L
					|| !string.Equals(row.OperationId,
						OperationId(Book.RealmId, row.OperationSequence), StringComparison.Ordinal)
					|| !IdentityContainsSettlement(Book, row.SettlementId) || !ValidName(row.ZoneId)
					|| !string.Equals(row.ProjectionId, ProjectionId(row.OperationId), StringComparison.Ordinal)
					|| !ValidId(row.ObjectId) || TooLong(row.Fault, MaxTextChars))
				{
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, "malformed projection authority row");
				}
			}
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow left = Book.Projections[i];
				for (int j = i + 1; j < Book.Projections.Count; j++)
				{
					KingdomTradeProjectionRow right = Book.Projections[j];
					if (!(string.Equals(left.SettlementId, right.SettlementId,
							StringComparison.Ordinal)
						|| string.Equals(left.ProjectionId, right.ProjectionId,
							StringComparison.Ordinal)
						|| string.Equals(left.ObjectId, right.ObjectId,
							StringComparison.Ordinal))) continue;
					left.Quarantined = true;
					right.Quarantined = true;
					left.Fault = AppendFault(left.Fault, "duplicate projection authority");
					right.Fault = AppendFault(right.Fault, "duplicate projection authority");
				}
			}
		}

		private static bool NormalizeWaterLeg(KingdomTradeWaterLeg Leg,
			KingdomTradeWaterDirection Direction)
		{
			bool oversized = TooLong(Leg.OwnerId, MaxIdChars)
				|| TooLong(Leg.ZoneId, MaxNameChars)
				|| TooLong(Leg.BeforeComposition, 64) || TooLong(Leg.AfterComposition, 64);
			return !oversized && ValidId(Leg.OwnerId) && ValidName(Leg.ZoneId) && Leg.Capacity >= 0
				&& Leg.Before >= 0 && Leg.Before <= Leg.Capacity && Leg.Delta > 0
				&& Leg.After >= 0 && Leg.After <= Leg.Capacity
				&& ((Direction == KingdomTradeWaterDirection.Debit && Leg.After == Leg.Before - Leg.Delta)
					|| (Direction == KingdomTradeWaterDirection.Credit && Leg.After == Leg.Before + Leg.Delta))
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Leg.State);
		}

		private static bool NormalizeMaterial(KingdomTradeMaterialOutput Output)
		{
			bool oversized = TooLong(Output.OutputId, MaxIdChars)
				|| TooLong(Output.Marker, MaxIdChars) || TooLong(Output.Blueprint, MaxNameChars)
				|| TooLong(Output.DestinationOwnerId, MaxIdChars)
				|| TooLong(Output.ZoneId, MaxNameChars);
			bool creating = Output.State == KingdomTradePhysicalState.CreateIntent;
			if (creating)
			{
				Output.State = KingdomTradePhysicalState.Lost;
			}
			if (Output.CleanupState == KingdomTradePhysicalState.CleanupIntent)
			{
				Output.CleanupState = KingdomTradePhysicalState.Lost;
			}
			return !oversized && (creating ? string.IsNullOrEmpty(Output.OutputId) : ValidId(Output.OutputId))
				&& ValidId(Output.Marker)
				&& ValidName(Output.Blueprint) && ValidId(Output.DestinationOwnerId)
				&& ValidName(Output.ZoneId) && Output.Count > 0
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Output.State)
				&& Enum.IsDefined(typeof(KingdomTradePhysicalState), Output.CleanupState);
		}

		private static void NormalizeStanding(KingdomTradeStandingCas Standing, ref bool Malformed)
		{
			if (Standing == null) return;
			if (TooLong(Standing.Faction, MaxNameChars)) Malformed = true;
			long expected = (long)Standing.Before + Standing.Delta;
			if (!ValidName(Standing.Faction) || expected < int.MinValue || expected > int.MaxValue
				|| Standing.After != (int)expected
				|| !Enum.IsDefined(typeof(KingdomTradePhysicalState), Standing.State)) Malformed = true;
		}

		private static void NormalizeOutbox(KingdomTradeOutbox Outbox, ref bool Malformed)
		{
			if (Outbox == null) return;
			if (TooLong(Outbox.EventId, MaxIdChars) || TooLong(Outbox.Chronicle, MaxTextChars)
				|| TooLong(Outbox.LedgerNote, MaxTextChars) || TooLong(Outbox.Message, MaxTextChars)
				|| TooLong(Outbox.Deed, MaxTextChars)) Malformed = true;
			if (Outbox.LedgerDeliveredDelta < 0) Malformed = true;
			Outbox.ChronicleState = ResumeSink(Outbox.ChronicleState);
			Outbox.LedgerState = ResumeSink(Outbox.LedgerState);
			Outbox.MessageState = ResumeSink(Outbox.MessageState);
			Outbox.DeedState = ResumeSink(Outbox.DeedState);
			NormalizeSink(ref Outbox.ChronicleState,
				!string.IsNullOrEmpty(Outbox.Chronicle), ref Malformed);
			NormalizeSink(ref Outbox.LedgerState,
				!string.IsNullOrEmpty(Outbox.LedgerNote)
					|| Outbox.LedgerDeliveredDelta > 0, ref Malformed);
			NormalizeSink(ref Outbox.MessageState,
				!string.IsNullOrEmpty(Outbox.Message), ref Malformed);
			NormalizeSink(ref Outbox.DeedState,
				!string.IsNullOrEmpty(Outbox.Deed), ref Malformed);
			if (!ValidId(Outbox.EventId)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.ChronicleState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.LedgerState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.MessageState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Outbox.DeedState)) Malformed = true;
		}

	}
}
