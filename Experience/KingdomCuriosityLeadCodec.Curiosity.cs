using System;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>
	/// The curiosity book on the wire.
	/// <para>
	/// A row declares its own revision, so a book may hold rows of both. Revision 1 rows are the
	/// exact fourteen fields the first build wrote and are read back byte-compatibly forever;
	/// revision 2 rows add the journal category the curation actually matched, which the first
	/// build never recorded and this build will not invent for it.
	/// </para>
	/// <para>
	/// The book is written at revision 1 when every row it holds is a revision 1 row, so a save
	/// that never gains a category never changes shape. It becomes revision 2 the moment one row
	/// has something to say that revision 1 has no room for &mdash; which is the only thing
	/// "only where needed" can honestly mean.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityLeadCodec
	{
		public static KingdomCuriosityBook DecodeCuriosity(byte[] bytes)
		{
			// One private copy, taken before anything is judged. Everything below reads it.
			byte[] snapshot = Ingress(bytes, MaxCuriosityBookBytes, out string ingress);
			if (snapshot == null) return QuarantineCuriosity(null, ingress);
			KingdomCuriosityFrame frame = Inspect(snapshot, CuriosityMagic,
				CuriosityHighestKnownVersion);
			if (frame.Kind == KingdomCuriosityFrameKind.Unreadable)
				return QuarantineCuriosity(snapshot, frame.Fault);
			if (frame.Kind == KingdomCuriosityFrameKind.Future)
				return new KingdomCuriosityBook
				{
					State = KingdomCuriosityBookState.FutureOpaque,
					OpaqueVersion = frame.WireVersion,
					OpaquePayload = snapshot,
					Fault = "the curiosity book was written at wire revision " + frame.WireVersion
						+ ", which this build can keep but not read"
				};
			try
			{
				KingdomCuriosityBook book = new KingdomCuriosityBook();
				using (BinaryReader r = Reader(snapshot, frame.BodyEnd))
				{
					r.ReadInt32(); r.ReadInt32();
					book.Revision = r.ReadInt64();
					int count = GetRowCount(r, KingdomCuriosityBook.MaxRows);
					for (int i = 0; i < count; i++) book.Rows.Add(ReadRow(r, frame.WireVersion));
					RequireEnd(r);
				}
				if (!KingdomCuriosityRules.ValidBook(book))
					throw Bad("the rows do not satisfy the curiosity book's own rules");
				return book;
			}
			catch (Exception e) when (WireFault(e))
			{
				return QuarantineCuriosity(snapshot, e.Message);
			}
		}

		internal static bool TryWriteCuriosity(KingdomCuriosityBook book, out byte[] bytes,
			out string failure)
		{
			bytes = null; failure = null;
			if (!KingdomCuriosityRules.ValidBook(book))
				return Refuse("the curiosity book does not satisfy its own rules", out failure);
			int wireVersion = WireVersionFor(book);
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter w = Writer(stream))
					{
						WriteHeader(w, CuriosityMagic, wireVersion, book.Revision,
							book.Rows.Count);
						for (int i = 0; i < book.Rows.Count; i++) WriteRow(w, book.Rows[i]);
						w.Flush();
					}
					byte[] written = wireVersion >= FirstDigestVersion
						? Seal(stream) : stream.ToArray();
					// What this build writes is held to the exact arithmetic, which is tighter
					// than the accepted cap. Authoring right up to what we merely tolerate would
					// leave no room to tell our own overrun from a stranger's.
					if (written.Length > ExactCuriosityBookBytes)
						return Refuse("the curiosity book is " + written.Length
							+ " bytes, past the " + ExactCuriosityBookBytes
							+ " bytes this build can lawfully write", out failure);
					bytes = written;
					return true;
				}
			}
			catch (Exception e) when (WireFault(e))
			{
				return Refuse("the curiosity book would not encode: " + e.Message, out failure);
			}
		}

		/// <summary>Revision 1 while nothing needs more; revision 2 as soon as one row does.</summary>
		internal static int WireVersionFor(KingdomCuriosityBook book)
		{
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].Version >= KingdomCuriosityReceipt.CategoryVersion)
					return CuriosityHighestKnownVersion;
			return FirstWireVersion;
		}

		private static void WriteRow(BinaryWriter w, KingdomCuriosityReceipt x)
		{
			w.Write(x.Version); w.Write((byte)x.State);
			PutString(w, x.SourceId); w.Write(x.SourceVersion);
			PutString(w, x.SettlementId); w.Write(x.CuratorResidentId);
			PutString(w, x.CuratorName); PutString(w, x.CuratorObjectId);
			PutString(w, x.NoteId); PutString(w, x.Locator); PutString(w, x.NoteText);
			PutString(w, x.Reason);
			w.Write(x.PreparedTick); w.Write(x.ClosedTick);
			if (x.Version >= KingdomCuriosityReceipt.CategoryVersion)
				PutString(w, x.NoteCategory);
		}

		private static KingdomCuriosityReceipt ReadRow(BinaryReader r, int wireVersion)
		{
			int rowVersion = r.ReadInt32();
			if (rowVersion < KingdomCuriosityReceipt.FirstVersion
				|| rowVersion > KingdomCuriosityReceipt.CurrentVersion)
				throw Bad("a curiosity row declares revision " + rowVersion);
			if (rowVersion > wireVersion)
				throw Bad("a revision " + rowVersion + " row sits inside a revision "
					+ wireVersion + " book, which has no room for it");
			KingdomCuriosityReceipt row = new KingdomCuriosityReceipt
			{
				Version = rowVersion,
				State = (KingdomCuriosityState)r.ReadByte(),
				SourceId = GetString(r, KingdomCuriosityRules.MaxIdChars),
				SourceVersion = r.ReadInt32(),
				SettlementId = GetString(r, KingdomCuriosityRules.MaxIdChars),
				CuratorResidentId = r.ReadInt32(),
				CuratorName = GetString(r, KingdomCuriosityRules.MaxText),
				CuratorObjectId = GetString(r, KingdomCuriosityRules.MaxIdChars),
				NoteId = GetString(r, KingdomCuriosityRules.MaxIdChars),
				Locator = GetString(r, KingdomCuriosityRules.MaxLegacyLocatorChars),
				NoteText = GetString(r, KingdomCuriosityRules.MaxText),
				Reason = GetString(r, KingdomCuriosityRules.MaxText),
				PreparedTick = r.ReadInt64(),
				ClosedTick = r.ReadInt64()
			};
			if (rowVersion >= KingdomCuriosityReceipt.CategoryVersion)
				row.NoteCategory = GetString(r, KingdomCuriosityRules.MaxCategoryChars);
			return row;
		}

		private static KingdomCuriosityBook QuarantineCuriosity(byte[] bytes, string fault)
		{
			return new KingdomCuriosityBook
			{
				State = KingdomCuriosityBookState.Quarantined,
				Fault = "the curiosity book would not read: " + fault,
				OpaquePayload = bytes
			};
		}
	}
}
