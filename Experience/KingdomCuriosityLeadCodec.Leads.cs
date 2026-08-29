using System;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>
	/// The civic-lead book on the wire.
	/// <para>
	/// This book gained no field in wire revision 2, and it is written at revision 1 for exactly
	/// that reason: nothing here needs the extra shape, so nothing here pays for it and every
	/// existing save keeps its exact bytes. What revision 2 gives this book is the ability to
	/// <i>recognise</i> a later one &mdash; a revision 3 lead book will close with the same digest
	/// the curiosity book promises, and will be held rather than mourned.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityLeadCodec
	{
		public static KingdomCivicLeadBook DecodeLeads(byte[] bytes)
		{
			// One private copy, taken before anything is judged. Everything below reads it.
			byte[] snapshot = Ingress(bytes, MaxLeadBookBytes, out string ingress);
			if (snapshot == null) return QuarantineLeads(null, ingress);
			KingdomCuriosityFrame frame = Inspect(snapshot, LeadMagic, LeadHighestKnownVersion);
			if (frame.Kind == KingdomCuriosityFrameKind.Unreadable)
				return QuarantineLeads(snapshot, frame.Fault);
			if (frame.Kind == KingdomCuriosityFrameKind.Future)
				return new KingdomCivicLeadBook
				{
					State = KingdomCuriosityBookState.FutureOpaque,
					OpaqueVersion = frame.WireVersion,
					OpaquePayload = snapshot,
					Fault = "the civic-lead book was written at wire revision " + frame.WireVersion
						+ ", which this build can keep but not read"
				};
			try
			{
				KingdomCivicLeadBook book = new KingdomCivicLeadBook();
				using (BinaryReader r = Reader(snapshot, frame.BodyEnd))
				{
					r.ReadInt32(); r.ReadInt32();
					book.Revision = r.ReadInt64();
					int count = GetRowCount(r, KingdomCivicLeadBook.MaxRows);
					for (int i = 0; i < count; i++) book.Rows.Add(ReadLeadRow(r));
					RequireEnd(r);
				}
				if (!KingdomCivicLeadRules.ValidBook(book))
					throw Bad("the rows do not satisfy the civic-lead book's own rules");
				return book;
			}
			catch (Exception e) when (WireFault(e))
			{
				return QuarantineLeads(snapshot, e.Message);
			}
		}

		internal static bool TryWriteLeads(KingdomCivicLeadBook book, out byte[] bytes,
			out string failure)
		{
			bytes = null; failure = null;
			if (!KingdomCivicLeadRules.ValidBook(book))
				return Refuse("the civic-lead book does not satisfy its own rules", out failure);
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter w = Writer(stream))
					{
						WriteHeader(w, LeadMagic, LeadHighestKnownVersion,
							book.Revision, book.Rows.Count);
						for (int i = 0; i < book.Rows.Count; i++) WriteLeadRow(w, book.Rows[i]);
						w.Flush();
					}
					byte[] written = stream.ToArray();
					if (written.Length > ExactLeadBookBytes)
						return Refuse("the civic-lead book is " + written.Length
							+ " bytes, past the " + ExactLeadBookBytes
							+ " bytes this build can lawfully write", out failure);
					bytes = written;
					return true;
				}
			}
			catch (Exception e) when (WireFault(e))
			{
				return Refuse("the civic-lead book would not encode: " + e.Message, out failure);
			}
		}

		private static void WriteLeadRow(BinaryWriter w, KingdomCivicLeadReceipt x)
		{
			w.Write(x.Version); w.Write((byte)x.Phase);
			PutString(w, x.SourceId); w.Write(x.SourceVersion);
			PutString(w, x.SettlementId); PutString(w, x.LeadId); PutString(w, x.Locator);
			PutString(w, x.Title); PutString(w, x.AuthoredReason);
			w.Write(x.CompletedTick);
			if (x.Fault == null) PutAbsent(w); else PutString(w, x.Fault);
		}

		private static KingdomCivicLeadReceipt ReadLeadRow(BinaryReader r)
		{
			int rowVersion = r.ReadInt32();
			if (rowVersion != KingdomCivicLeadReceipt.CurrentVersion)
				throw Bad("a civic-lead row declares revision " + rowVersion);
			return new KingdomCivicLeadReceipt
			{
				Version = rowVersion,
				Phase = (KingdomCivicLeadPhase)r.ReadByte(),
				SourceId = GetString(r, KingdomCuriosityRules.MaxIdChars),
				SourceVersion = r.ReadInt32(),
				SettlementId = GetString(r, KingdomCuriosityRules.MaxIdChars),
				LeadId = GetString(r, KingdomCivicLeadRules.LeadIdChars),
				Locator = GetString(r, KingdomCuriosityRules.MaxLegacyLocatorChars),
				Title = GetString(r, KingdomCuriosityRules.MaxText),
				AuthoredReason = GetString(r, KingdomCuriosityRules.MaxText),
				CompletedTick = r.ReadInt64(),
				Fault = GetOptionalString(r, KingdomCuriosityRules.MaxText)
			};
		}

		private static KingdomCivicLeadBook QuarantineLeads(byte[] bytes, string fault)
		{
			return new KingdomCivicLeadBook
			{
				State = KingdomCuriosityBookState.Quarantined,
				Fault = "the civic-lead book would not read: " + fault,
				OpaquePayload = bytes
			};
		}
	}
}
