namespace ThousandAndFirst
{
	/// <summary>Durable replay authority for completed plots without a construction job.</summary>
	public sealed class KingdomPlotLegacyEffectsPlan
	{
		public string FinalId;
		public string PredecessorId;
		public string Blueprint;
		public string BuildKey;
		public string PlotId;
		public string ZoneId;
		public int X;
		public int Y;
		public bool Founded;
		public bool Heart;
		public bool Delve;
		public KingdomFoundingHeartSinkDisposition Raising;
		public KingdomFoundingHeartSinkDisposition HeartSink;
		public KingdomFoundingHeartSinkDisposition DelveSink;

		public KingdomPlotLegacyEffectsPlan Copy()
		{
			return new KingdomPlotLegacyEffectsPlan
			{
				FinalId = FinalId, PredecessorId = PredecessorId, Blueprint = Blueprint,
				BuildKey = BuildKey, PlotId = PlotId, ZoneId = ZoneId, X = X, Y = Y,
				Founded = Founded, Heart = Heart, Delve = Delve, Raising = Raising,
				HeartSink = HeartSink, DelveSink = DelveSink
			};
		}
	}
}
