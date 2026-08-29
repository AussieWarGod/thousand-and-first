using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		public static int CountStoredWater(Zone Z)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null) return active.StoredWater;
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && KingdomLiquids.HasFreshWater(part))
				{
					total += part.Volume;
				}
			}
			return total;
		}

		public static int CountOpenWater(Zone Z)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null) return active.OpenWater;
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume < 0 && KingdomLiquids.HasFreshWater(part))
				{
					total += part.Volume;
				}
			}
			return total;
		}

		public static int CountStorageSpace(Zone Z)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null) return active.StorageSpace;
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && part.Volume < part.MaxVolume && KingdomLiquids.CanReceiveFreshWater(part))
				{
					total += part.MaxVolume - part.Volume;
				}
			}
			return total;
		}

		/// <summary>Counts vessels currently dedicated to the settlement's stores in a zone.</summary>
		public static int CountDedicatedVessels(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("KingdomStores") == 1)
				{
					total++;
				}
			}
			return total;
		}

		/// <summary>Counts larders currently dedicated to the settlement's food stores in a zone.</summary>
		public static int CountDedicatedLarders(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("KingdomLarder") == 1)
				{
					total++;
				}
			}
			return total;
		}

		/// <summary>Counts beds the settlement built. These are the population ceiling.</summary>
		public static int CountBeds(Zone Z)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null) return active.Beds * KingdomRules.BedsPerBunk;
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && item.HasPart("Bed"))
				{
					total += KingdomRules.BedsPerBunk;
				}
			}
			return total;
		}

		public static int CountStorageCapacity(Zone Z)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null) return active.StorageCapacity;
			int total = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1)
				{
					total += part.MaxVolume;
				}
			}
			return total;
		}
	}
}
