using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One thing wrong &mdash; or merely worth saying &mdash; about one catalogue entry,
	/// or about the catalogue as a whole when <see cref="Key"/> is null.</summary>
	public class CatalogueFinding
	{
		/// <summary>The entry this is about, or null for a finding about the whole file.</summary>
		public string Key;

		/// <summary>The attribute at fault, for an author reading the log. Null when the finding
		/// is about the entry rather than one of its attributes.</summary>
		public string Attribute;

		public CatalogueSeverity Severity;

		/// <summary>One sentence, log-facing. Nothing depends on the wording.</summary>
		public string Message;

		public CatalogueFinding(string Key, string Attribute, CatalogueSeverity Severity, string Message)
		{
			this.Key = Key;
			this.Attribute = Attribute;
			this.Severity = Severity;
			this.Message = Message;
		}
	}
}
