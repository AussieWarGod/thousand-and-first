using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>How one public XML registry root relates to this build's schema.</summary>
	public enum KingdomXmlSchemaVerdict
	{
		/// <summary>The root declares the schema this build writes.</summary>
		Compatible = 0,

		/// <summary>The root predates explicit versioning. Read for backward compatibility.</summary>
		LegacyUnversioned = 1,

		/// <summary>The root declares a canonical integer schema this build does not read.</summary>
		Unsupported = 2,

		/// <summary>The root's schema attribute is present but is not a canonical integer.</summary>
		Malformed = 3
	}

	/// <summary>Version boundary shared by every mergeable public XML registry.</summary>
	public static class KingdomXmlSchemaRules
	{
		/// <summary>Schema written by this build's public registry files.</summary>
		public const int CurrentVersion = 1;

		/// <summary>
		/// Judges one root's <c>Schema</c> attribute. An absent attribute remains readable as the
		/// pre-versioning format; a present attribute must be the canonical decimal spelling.
		/// </summary>
		/// <param name="Declared">Raw attribute value, or null when absent.</param>
		/// <param name="Version">Parsed version for compatible and unsupported declarations;
		/// zero otherwise.</param>
		/// <returns>The compatibility verdict. This method never throws.</returns>
		public static KingdomXmlSchemaVerdict Judge(string Declared, out int Version)
		{
			Version = 0;
			if (Declared == null)
			{
				return KingdomXmlSchemaVerdict.LegacyUnversioned;
			}
			int parsed;
			if (Declared.Length == 0
				|| !int.TryParse(Declared, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)
				|| parsed.ToString(CultureInfo.InvariantCulture) != Declared)
			{
				return KingdomXmlSchemaVerdict.Malformed;
			}
			Version = parsed;
			return parsed == CurrentVersion
				? KingdomXmlSchemaVerdict.Compatible
				: KingdomXmlSchemaVerdict.Unsupported;
		}

		/// <summary>Whether a verdict may contribute entries to the merged registry.</summary>
		public static bool IsReadable(KingdomXmlSchemaVerdict Verdict)
		{
			return Verdict == KingdomXmlSchemaVerdict.Compatible
				|| Verdict == KingdomXmlSchemaVerdict.LegacyUnversioned;
		}
	}
}
