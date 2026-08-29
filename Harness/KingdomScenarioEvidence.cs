using System;
using System.IO;
using System.Text;

using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Durable scenario evidence rows.
	/// <para>
	/// The gallery harness logs its rows through <c>KingdomLog</c>, which is an option-gated
	/// Unity log line and survives nothing. A scenario verdict has to outlive the session that
	/// produced it, so rows are appended to a file under the save path using the one file-writing
	/// path this mod already owns.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioEvidence
	{
		internal const string FileName = "scenario-evidence.tsv";

		/// <summary>
		/// Appends one row. Never throws into a caller mid-transaction: a failed write is logged
		/// and reported, because losing evidence must not also lose the run.
		/// </summary>
		internal static void Record(KingdomScenarioPlan Plan, string KeySetDigest, bool Eligible,
			string Detail)
		{
			try
			{
				string directory = DataManager.SavePath("ThousandAndFirst");
				if (string.IsNullOrEmpty(directory)) return;
				Directory.CreateDirectory(directory);
				string path = Path.Combine(directory, FileName);
				File.AppendAllText(path, Row(Plan, KeySetDigest, Eligible, Detail) + "\n",
					new UTF8Encoding(false, true));
			}
			catch (Exception exception)
			{
				KingdomLog.Log("[TAF scenario] evidence row could not be written: "
					+ KingdomScenarioRules.Bounded(exception.Message));
			}
		}

		/// <summary>
		/// One tab-separated row in the shipped evidence grammar. Verdict eligibility is recorded
		/// separately from any human pass/fail, because an ineligible run is not a failed one.
		/// </summary>
		internal static string Row(KingdomScenarioPlan Plan, string KeySetDigest, bool Eligible,
			string Detail)
		{
			if (Plan == null) return null;
			StringBuilder sb = new StringBuilder("[TAF scenario-evidence]");
			sb.Append("\tschema=1")
				.Append("\tsuite=").Append(KingdomScenarioHarness.Suite)
				.Append("\tscenario=").Append(Plan.Key)
				.Append("\tauthority=").Append(Plan.AuthorityClass)
				.Append("\tverbs=").Append(Plan.Verbs)
				.Append("\tseed=").Append(Plan.Seed ?? "-")
				.Append("\tplan=").Append(Plan.PlanDigest ?? "-")
				.Append("\tdefinition=").Append(Plan.DefinitionDigest ?? "-")
				.Append("\tanchor=").Append(Plan.AnchorId ?? "-")
				.Append("\tkeyset=").Append(KeySetDigest ?? "-")
				.Append("\tsynthetic=").Append(Plan.Synthetic ? "1" : "0")
				.Append("\teligible=").Append(Eligible ? "1" : "0")
				.Append("\tdetail64=").Append(Base64(Detail))
				.Append("\tmod=").Append(KingdomReleaseInfo.Version)
				.Append("\tqud=").Append(XRLGame.CoreVersion)
				.Append("\tcapture=harness-measured");
			return sb.ToString();
		}

		private static string Base64(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}
	}
}
