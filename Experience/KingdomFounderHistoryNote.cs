using System;
using Qud.API;

namespace ThousandAndFirst
{
	/// <summary>
	/// Reconstructed read-only view over the founder receipt. It owns no static/global collection;
	/// save/load persistence belongs solely to <see cref="KingdomFounderHistoryReceipt"/>.
	/// </summary>
	[Serializable]
	public sealed class KingdomFounderHistoryProjection
	{
		public readonly string Id;
		public readonly string ProofId;
		public readonly string Title;
		public readonly string Text;
		public readonly string LearnedFrom;
		public readonly long HistoricYear;

		internal KingdomFounderHistoryProjection(string Id, string ProofId, string Title,
			string Text, string LearnedFrom, long HistoricYear)
		{
			this.Id = Id;
			this.ProofId = ProofId;
			this.Title = Title;
			this.Text = Text;
			this.LearnedFrom = LearnedFrom;
			this.HistoricYear = HistoricYear;
		}

		public string Render()
		{
			return Title + "\n" + Text + "\nRecorded from " + LearnedFrom + ".";
		}
	}

	/// <summary>
	/// Schema-1 deserialization carrier only. New code never creates or registers this type. It
	/// remains loadable so exact old notes can be identified and atomically removed from Qud's
	/// Sultan-journal pools; changing or deleting the type would strand old save payloads.
	/// </summary>
	[Serializable]
	[Obsolete("Schema-1 founder-history save compatibility only; never create or register.")]
	public sealed class r_KingdomFounderHistoryNote : JournalSultanNote
	{
		public override bool Forgettable()
		{
			return false;
		}
	}
}
