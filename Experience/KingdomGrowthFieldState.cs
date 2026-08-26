using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthFieldState
	{
		public string FieldId;
		public string WorkObjectId;
		public string WorkPartId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public int X = -1;
		public int Y = -1;
		public string CropBlueprint;
		public int Stage;
		public long NextStageTick;
		public long SownTick;
		public int Cycles;
		public int SaidWant;
		public int DeclaredRows;
		public int EffectivenessPercent;
		public int MethodPercent;
		public bool NoLarderAnnounced;
		public string SeedBlueprint;
		public string PartGraphHash;
		public string ObjectGraphHash;
		public string TopologyHash;
	}
}
