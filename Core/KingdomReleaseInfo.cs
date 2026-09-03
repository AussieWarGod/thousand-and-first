namespace ThousandAndFirst
{
	/// <summary>
	/// Single runtime-facing release identity. Keep this byte-for-byte equal to manifest.json;
	/// receipts use it so native evidence can never be mislabeled by a forgotten debug constant.
	/// </summary>
	internal static class KingdomReleaseInfo
	{
		internal const string Version = "0.3.0";
	}
}
