using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthCropRow
	{
		public string FieldId;
		public string RowId;
		public string ObjectId;
		public string Marker;
		public string Blueprint;
		public string ZoneId;
		public string OwnerId;
		public int X = -1;
		public int Y = -1;
		public int Count;
		public bool HasHarvestable;
		public bool Ripe;
		public int RegenTimer;
		public string RegenTime;
		public int TileIndex;
		public string RenderTile;
		public string RenderColor;
		public string RenderDetail;
		public string RenderString;
		public string TileColor;
		public string PartGraphHash;
		public string ObjectGraphHash;
		public string TopologyHash;
		public long Revision;
		public string LastOperationId;
	}
}
