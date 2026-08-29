using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Bounded founder-facing inspection. It never claims a clean uninstall.</summary>
	public sealed class KingdomRealmRetirementReport
	{
		public bool CanBegin;
		public bool KnownProjectionsClosed;
		public bool CleanRemovalProvable;
		public string Summary = "";
		public List<string> Blockers = new List<string>();
		public List<string> OutstandingGround = new List<string>();
		public List<string> Disclosures = new List<string>();

		public string Render()
		{
			StringBuilder text = new StringBuilder();
			text.Append(Summary ?? "Realm-removal inspection.");
			Append(text, "Must be resolved first", Blockers);
			Append(text, "Ground requiring an ordinary visit", OutstandingGround);
			Append(text, "Limits and retained evidence", Disclosures);
			return text.ToString();
		}

		private static void Append(StringBuilder Text, string Title, IList<string> Rows)
		{
			if (Rows == null || Rows.Count == 0) return;
			Text.Append("\n\n{{W|").Append(Title).Append(":}}");
			for (int i = 0; i < Rows.Count; i++)
				Text.Append("\n{{rules|--}} ").Append(Rows[i] ?? "unknown evidence");
		}
	}
}
