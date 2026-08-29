using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// The reading half of D6: frozen object facts, and the bounded register of every recognition
	/// the realm has actually kept.
	/// <para>
	/// Everything here is plain text. No caller in this file emits display markup, because the
	/// strings being rendered came from object display names and settler names that this mod did
	/// not author; the engine-side opener escapes the whole block once, at the boundary that owns
	/// escaping, rather than each fragment guessing.
	/// </para>
	/// <para>
	/// This is also the only inspection D6 offers, and it is deliberately a readback: it renders
	/// the rows a fresh read of the current realm's authority actually returned, never a caller's
	/// memory of what it wrote, and never anything inferred from what the founder is carrying or
	/// what happens to be standing nearby.
	/// </para>
	/// </summary>
	public static class KingdomArtifactRecognitionRegister
	{
		public static string KindName(KingdomArtifactRecognitionKind Kind)
		{
			switch (Kind)
			{
			case KingdomArtifactRecognitionKind.Remark: return "a spoken remark";
			case KingdomArtifactRecognitionKind.Inscription: return "an inscription";
			case KingdomArtifactRecognitionKind.Representation:
				return "a fixed representation of no commerce value";
			default: return "nothing this build can name";
			}
		}

		/// <summary>The exact frozen facts of one reading, in the order a person would ask them.</summary>
		public static string Facts(KingdomArtifactSnapshot Source)
		{
			if (Source == null) return "The object facts are absent.";
			return "Object: " + Plain(Source.DisplayName)
				+ "\nMade as: " + Plain(Source.Blueprint)
				+ "\nIdentity: " + Plain(Source.ObjectId)
				+ "\nWhere it stood: " + Plain(Source.LocationId)
				+ "\nWhose it was: " + (string.IsNullOrEmpty(Source.OwnerId)
					? "no owner was recorded on it" : Plain(Source.OwnerId))
				+ "\nRemembered for: " + (Source.DeedText == null
					? "nothing beyond the object itself was recorded" : Plain(Source.DeedText))
				+ "\nRead at tick: " + Source.ObservedTick.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Every retained row, or an explicit statement that there are none.
		/// <para>
		/// Absence is written out rather than implied by a blank page. A founder who is told
		/// nothing cannot tell an empty register from a register that failed to open, and those
		/// two need very different next moves.
		/// </para>
		/// </summary>
		public static string Register(KingdomArtifactRecognitionBook Book)
		{
			if (Book == null || Book.Rows == null)
				return "This realm's recognition authority could not be read.";
			if (Book.Rows.Count == 0)
				return "This realm has recognized nothing yet. "
					+ KingdomArtifactRecognitionRules.MaxRows
					+ " recognitions may ever be kept, and none has been spent.";
			StringBuilder text = new StringBuilder();
			text.Append("Kept recognitions: ")
				.Append(Book.Rows.Count.ToString(CultureInfo.InvariantCulture))
				.Append(" of ")
				.Append(KingdomArtifactRecognitionRules.MaxRows
					.ToString(CultureInfo.InvariantCulture))
				.Append(".");
			for (int i = 0; i < Book.Rows.Count; i++)
			{
				KingdomArtifactRecognitionReceipt row = Book.Rows[i];
				text.Append("\n\n")
					.Append((i + 1).ToString(CultureInfo.InvariantCulture))
					.Append(". ")
					.Append(row == null ? "an unreadable row" : Plain(row.Text));
				if (row == null) continue;
				text.Append("\n   Form: ").Append(KindName(row.Kind))
					.Append("\n   Object: ").Append(Plain(row.Source.ObjectId))
					.Append("\n   Where it stood: ").Append(Plain(row.Source.LocationId))
					.Append("\n   Value: ")
					.Append(row.CommerceValue.ToString(CultureInfo.InvariantCulture))
					.Append(row.CustodyClaimed ? "; custody claimed" : "; no custody claimed")
					.Append("\n   Recorded at tick: ")
					.Append(row.RecognizedTick.ToString(CultureInfo.InvariantCulture));
			}
			return text.ToString();
		}

		/// <summary>
		/// The rows a fresh read of one realm's authority holds, in their canonical order.
		/// A copy, so nothing a caller does to the returned list reaches the authority.
		/// </summary>
		public static List<KingdomArtifactRecognitionReceipt> Rows(
			KingdomCivicArtifactsEnvelope Held)
		{
			List<KingdomArtifactRecognitionReceipt> rows =
				new List<KingdomArtifactRecognitionReceipt>();
			if (Held == null || Held.Recognitions == null || Held.Recognitions.Rows == null)
				return rows;
			for (int i = 0; i < Held.Recognitions.Rows.Count; i++)
				rows.Add(Held.Recognitions.Rows[i]);
			return rows;
		}

		/// <summary>
		/// Text safe to place inside a plain block: no line may be broken by content, because a
		/// forged newline inside a display name would let an object appear to be a second row.
		/// </summary>
		public static string Plain(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "";
			StringBuilder text = new StringBuilder(Value.Length);
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				text.Append(c < ' ' || c == '\u007f' || (c >= '\u0080' && c <= '\u009f')
					? ' ' : c);
			}
			return text.ToString();
		}
	}
}
