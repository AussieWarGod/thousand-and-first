using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal static class KingdomExperienceTelemetryExport
	{
		internal const int MaxExportBytes = 24 * 1024;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool TryCompose(KingdomExperienceTelemetryBuffer Buffer, out string Text)
		{
			Text = null; if (Buffer == null) return false;
			StringBuilder b = new StringBuilder(256 + Buffer.Count * 32);
			b.Append("taf-experience-v1\ncapacity\t")
				.Append(KingdomExperienceTelemetryBuffer.Capacity.ToString(CultureInfo.InvariantCulture))
				.Append("\ncount\t").Append(Buffer.Count.ToString(CultureInfo.InvariantCulture))
				.Append("\ndropped\t").Append(Buffer.Dropped.ToString(CultureInfo.InvariantCulture))
				.Append("\nsequence\texperiment\tarm\tfixture\tobservation\tmeasure\n");
			for (int i = 0; i < Buffer.Count; i++)
			{
				if (!Buffer.TryGet(i, out KingdomExperienceTelemetryReceipt r)) return false;
				b.Append(r.Sequence.ToString(CultureInfo.InvariantCulture)).Append('\t')
					.Append(((byte)r.Experiment).ToString(CultureInfo.InvariantCulture)).Append('\t')
					.Append(((byte)r.Arm).ToString(CultureInfo.InvariantCulture)).Append('\t')
					.Append(((byte)r.Fixture).ToString(CultureInfo.InvariantCulture)).Append('\t')
					.Append(((byte)r.Observation).ToString(CultureInfo.InvariantCulture)).Append('\t')
					.Append(r.Measure.ToString(CultureInfo.InvariantCulture)).Append('\n');
			}
			string result = b.ToString();
			if (Utf8.GetByteCount(result) > MaxExportBytes) return false;
			Text = result; return true;
		}
	}
}
