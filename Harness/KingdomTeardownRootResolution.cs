namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free choice of WHICH object a teardown case reads after commissioning: the works
	/// root it captured at commission time, or the plot's current root. Proved by value in
	/// DevTests/KingdomTeardownRootResolutionTests.cs (both public projects).
	/// <para>
	/// RUN 43 (6fba8b5). Production declared <c>plot complete: communal fire</c>, the job reached
	/// Complete and the crew was released, yet the fixture journaled
	/// <c>stage-applied=unread; built=False; completed-tick=-1</c> from checkpoint 2 on: it froze
	/// <c>WorksId = job.OutputId</c> at commission time and kept reading that retired works root.
	/// Production re-roots the paid output on the FINAL building and keeps
	/// <c>Job.OutputId</c> naming it (Growth/KingdomPlot2.33.FinishEffects.cs:30-40
	/// ExactPlotFinalRootCustody(construction.OutputId, building); the #172/#174 fix asks the
	/// final building for its id before rooting), exactly what the lifecycle harness reads in
	/// lifecycle-grown (Harness/KingdomQuickstartLifecycleFinish.cs Standing: Job.OutputId on the
	/// job's own cell). So: a found, Complete row's OutputId wins; a compacted row falls back to
	/// the built object carrying this job's construction receipt; otherwise the works root.
	/// </para>
	/// </summary>
	internal static class KingdomTeardownRootResolution
	{
		internal const string SourceWorksRoot = "works-root";
		internal const string SourceFinalOutput = "final-output";
		internal const string SourceBuiltReceipt = "built-receipt";

		/// <summary>The id to read now, and which source named it.</summary>
		internal static string Choose(string WorksId, bool RowFound, bool RowComplete,
			string RowOutputId, string ReceiptHolderId, out string Source)
		{
			if (RowFound && RowComplete && !string.IsNullOrEmpty(RowOutputId))
			{
				Source = SourceFinalOutput;
				return RowOutputId;
			}
			if (!RowFound && !string.IsNullOrEmpty(ReceiptHolderId))
			{
				Source = SourceBuiltReceipt;
				return ReceiptHolderId;
			}
			Source = SourceWorksRoot;
			return WorksId;
		}
	}
}
