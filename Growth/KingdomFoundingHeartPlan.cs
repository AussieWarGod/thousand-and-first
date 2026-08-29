using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Frozen authority for one founding heart. Geometry, authored plot payload, labour, and
	/// deterministic identities never change; only the six monotone placement checkpoints do.
	/// </summary>
	public sealed class KingdomFoundingHeartPlan
	{
		public string TransactionId;
		public string ZoneId;
		public int RiteX;
		public int RiteY;
		public int SurveyX1;
		public int SurveyY1;
		public int SurveyX2;
		public int SurveyY2;
		public int RectX1;
		public int RectY1;
		public int RectX2;
		public int RectY2;
		public long StartedTick;
		public long TotalTicks;
		public string PlotId;
		public string Payload;
		public string StakeTruth;
		public int[] States;

		public KingdomFoundingHeartPlan Copy()
		{
			return new KingdomFoundingHeartPlan
			{
				TransactionId = TransactionId,
				ZoneId = ZoneId,
				RiteX = RiteX,
				RiteY = RiteY,
				SurveyX1 = SurveyX1,
				SurveyY1 = SurveyY1,
				SurveyX2 = SurveyX2,
				SurveyY2 = SurveyY2,
				RectX1 = RectX1,
				RectY1 = RectY1,
				RectX2 = RectX2,
				RectY2 = RectY2,
				StartedTick = StartedTick,
				TotalTicks = TotalTicks,
				PlotId = PlotId,
				Payload = Payload,
				StakeTruth = StakeTruth,
				States = States == null ? null : (int[])States.Clone()
			};
		}
	}

	/// <summary>Exact plot-works fields frozen before founding receipt publication.</summary>
	public sealed class KingdomFoundingHeartStakeTruth
	{
		public string BuildKey;
		public string DisplayName;
		public string Blueprint;
		public int FootprintX1;
		public int FootprintY1;
		public int FootprintX2;
		public int FootprintY2;
		public int Roof;
		public bool Open;
		public bool Carved;
		public string WallBlueprint;
		public string Contents;
		public int Staff;
		public bool ThresholdManning;
		public int Defence;
		public bool HasDoor;
		public int DoorX;
		public int DoorY;
		public bool PurposeLegacy;
	}
}
