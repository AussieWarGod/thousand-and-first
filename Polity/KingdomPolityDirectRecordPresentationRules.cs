using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Player prose for a validated direct record. Internal proof strings never cross
	/// this boundary.</summary>
	internal static class KingdomPolityDirectRecordPresentationRules
	{
		public static bool TryBuild(KingdomPolityDirectRecord Record, string Dated,
			out KingdomPolityDirectRecordView View)
		{
			View = null;
			if (Record == null || string.IsNullOrWhiteSpace(Dated)) return false;
			bool detailed = KingdomPolityDispatchRules.IsKind(Record,
				KingdomPolityDispatchRules.DirectPrefix);
			bool aggregate = KingdomPolityDispatchRules.IsKind(Record,
				KingdomPolityDispatchRules.AggregatePrefix);
			if (!detailed && !aggregate) return false;
			KingdomPolityAmbientTransaction transaction = Record.AmbientTransaction;
			if (detailed && !KingdomPolityAmbientTransactionRules.Valid(transaction,
				Record.SourceRef, out _)) return false;

			string subject = Subject(Record.Purpose);
			if (subject == null) return false;
			string prefix = Record.AcknowledgedTick == 0L ? "{{W|Unread}}" : "{{K|Read}}";
			if (aggregate)
			{
				if (Record.WindowOrdinal < 1UL) return false;
				string count = Record.WindowOrdinal.ToString(CultureInfo.InvariantCulture);
				View = new KingdomPolityDirectRecordView(
					prefix + " — older traffic (" + count + ")",
					"Older traffic bound in the Charter",
					"The clerk bound " + count + " older traffic "
						+ (Record.WindowOrdinal == 1UL ? "notice" : "notices")
						+ " beneath this leaf. The latest was dated " + Dated
						+ " and concerned " + subject + ".",
					Record.AcknowledgedTick != 0L);
				return true;
			}

			View = new KingdomPolityDirectRecordView(
				prefix + " — " + Label(Record.Purpose) + " — " + Dated,
				"Traffic record: " + Label(Record.Purpose),
				"On the " + Dated + ", " + ExactClause(transaction) +
					" Every civic audience and floor-place was already promised, so the city " +
					"bound the frozen matter into its Charter instead. Reading this leaf " +
					"acknowledges the record; it does not fabricate a visit, journey, trade, " +
					"or resident decision.",
				Record.AcknowledgedTick != 0L);
			return true;
		}

		private static string ExactClause(KingdomPolityAmbientTransaction T)
		{
			string from = KingdomPresentation.Rich(T.SourceSettlementName);
			string to = KingdomPresentation.Rich(T.DestinationSettlementName);
			string detail = KingdomPresentation.Rich(T.SafeDetail);
			switch (T.Purpose)
			{
			case KingdomPolityCohortPurpose.Guard:
				return "a witnessed local watch matter at " + to + " was recorded: " + detail + ".";
			case KingdomPolityCohortPurpose.Patrol:
				return "a caused local condition report at " + to + " was recorded: " + detail + ".";
			case KingdomPolityCohortPurpose.Courier:
				return "a message from " + from + " to " + to + " was recorded: " + detail + ".";
			case KingdomPolityCohortPurpose.Trader:
				return "a market notice from " + from + " to " + to + " was recorded: " + detail + ".";
			default:
				return "a petition from " + from + " to " + to + " was recorded: " + detail + ".";
			}
		}

		private static string Label(KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "gate watch";
			case KingdomPolityCohortPurpose.Patrol: return "road patrol";
			case KingdomPolityCohortPurpose.Courier: return "courier's word";
			case KingdomPolityCohortPurpose.Trader: return "travelling trader";
			case KingdomPolityCohortPurpose.Migrant: return "migrant's petition";
			default: return null;
			}
		}

		private static string Subject(KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "the gate watch";
			case KingdomPolityCohortPurpose.Patrol: return "a road patrol";
			case KingdomPolityCohortPurpose.Courier: return "a courier's word";
			case KingdomPolityCohortPurpose.Trader: return "a travelling trader";
			case KingdomPolityCohortPurpose.Migrant: return "a migrant's petition";
			default: return null;
			}
		}

	}

	internal sealed class KingdomPolityDirectRecordView
	{
		public readonly string Label;
		public readonly string Title;
		public readonly string Body;
		public readonly bool WasAcknowledged;

		public KingdomPolityDirectRecordView(string Label, string Title, string Body,
			bool WasAcknowledged)
		{
			this.Label = Label;
			this.Title = Title;
			this.Body = Body;
			this.WasAcknowledged = WasAcknowledged;
		}
	}
}
