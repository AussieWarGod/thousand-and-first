using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		/// <summary>Runtime-only §3.10 geometry/holder cache. Ground-derived, deliberately absent
		/// from saves, and rebuilt per dirty zone on render. A cold load starts with no trusted
		/// distances rather than serializing a stale answer.</summary>
		[NonSerialized]
		internal KingdomDistanceCache DistanceCache = null;

		public int SchemaVersion = KingdomCityRules.SchemaVersion;

		public int RulesVersion = KingdomCityRules.RulesVersion;

		/// <summary>Which city this book is. The settlement's own name at founding; never read for
		/// display, only for telling two books apart in a log line.</summary>
		public string SettlementId = "";

		/// <summary>How far the model has been advanced. LIVING-CITY-ARCHITECTURE &sect;2.2.</summary>
		public long ProcessedThroughTick;

		public long WaterLevel;

		public long WaterCapacity;

		public long FoodLevel;

		public long FoodCapacity;

		public long MaterialsLevel;

		public long MaterialsCapacity;

		// ---- Zone rows -----------------------------------------------------------------------

		public List<string> ZoneIds = new List<string>();

		public List<int> ZoneDistrictCodes = new List<int>();

		public List<long> ZoneLastReadTicks = new List<long>();

		public List<long> ZoneWaterLevels = new List<long>();

		public List<long> ZoneWaterCapacities = new List<long>();

		public List<long> ZoneFoodLevels = new List<long>();

		public List<long> ZoneFoodCapacities = new List<long>();

		public List<long> ZoneMaterialsLevels = new List<long>();

		public List<long> ZoneMaterialsCapacities = new List<long>();

		public List<int> ZoneRoofs = new List<int>();

		public List<int> ZoneDefences = new List<int>();

		public List<int> ZoneWaterCarries = new List<int>();

		public List<int> ZoneFoodCarries = new List<int>();

		public List<int> ZoneOwedWater = new List<int>();

		public List<int> ZoneOwedFood = new List<int>();

		public List<int> ZoneOwedMaterials = new List<int>();

		// ---- Work rows -----------------------------------------------------------------------

		public List<int> WorkIds = new List<int>();

		public List<string> WorkZoneIds = new List<string>();

		public List<int> WorkAnchorsX = new List<int>();

		public List<int> WorkAnchorsY = new List<int>();

		public List<string> WorkDesignKeys = new List<string>();

		public List<int> WorkConditions = new List<int>();

		public List<int> WorkCrews = new List<int>();

		public List<long> WorkRanThroughTicks = new List<long>();

		public List<int> WorkKinds = new List<int>();

		public List<int> WorkStages = new List<int>();

		public List<int> WorkProgress = new List<int>();

		public List<long> WorkNextTicks = new List<long>();

	}
}
