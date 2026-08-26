using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritEngine
	{
#if !TAF_TESTS
		internal static KingdomInheritApplyResult Apply(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, string TargetZoneId, Zone Zone)
		{
			return Apply(Legacy, Receipt, TargetZoneId, Zone == null ? null : new ZoneHost(Zone));
		}

		private sealed partial class ZoneHost : IKingdomInheritEngineHost
		{
			private readonly Zone Zone;

			private readonly bool[,] Connections;

			internal ZoneHost(Zone Zone)
			{
				this.Zone = Zone;
				Connections = new bool[Zone.Width, Zone.Height];
				foreach (ZoneConnection connection in Zone.EnumerateConnections())
				{
					MarkConnection(connection);
				}
				// EnumerateConnections only includes pending local ("-") cache entries. Every pending
				// boundary entry also reserves a live cell, so inspect the cache whole before mutation.
				if (Zone.ZoneConnectionCache != null)
				{
					for (int i = 0; i < Zone.ZoneConnectionCache.Count; i++)
					{
						MarkConnection(Zone.ZoneConnectionCache[i]);
					}
				}
			}

			public int Width { get { return Zone.Width; } }

			public int Height { get { return Zone.Height; } }

			public string ZoneId { get { return Zone.ZoneID ?? ""; } }

			public string TargetGameId { get { return The.Game == null ? "" : (The.Game.GameID ?? ""); } }

			public string ReadApplicationMarker()
			{
				return Zone.GetZoneProperty(ZoneMarkerProperty, "") ?? "";
			}

			public int CountApplicationObjects(string Marker)
			{
				int count = 0;
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					if (objects[i].GetStringProperty(ObjectMarkerProperty, "") == Marker)
					{
						count++;
					}
				}
				return count;
			}

			public bool HasAnyApplicationObjects()
			{
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					if (!string.IsNullOrEmpty(objects[i].GetStringProperty(ObjectMarkerProperty, "")))
					{
						return true;
					}
				}
				return false;
			}

			public bool HasExactApplicationObject(string Marker, KingdomInheritBuildSpec Spec,
				string CairnText)
			{
				Cell cell = Zone.GetCell(Spec.X, Spec.Y);
				if (cell == null)
				{
					return false;
				}
				for (int i = 0; i < cell.Objects.Count; i++)
				{
					GameObject obj = cell.Objects[i];
					bool degraded = Spec.IsArchitecture && obj.Blueprint == "r_KingdomCairn"
						&& obj.GetStringProperty(ObjectDegradedHashProperty, "")
							== Spec.ArchitectureHash;
					bool authorityMemory = KingdomInheritRules.IsFoundingHeartKey(Spec.Key)
						&& obj.Blueprint == "r_KingdomCairn"
						&& obj.GetIntProperty(ObjectAuthorityMemoryProperty) == 1;
					if ((obj.Blueprint == Spec.Blueprint || degraded || authorityMemory)
						&& obj.GetStringProperty(ObjectMarkerProperty, "") == Marker
						&& obj.GetStringProperty(ObjectKeyProperty, "") == Spec.Key
						&& obj.GetIntProperty(ObjectIndexProperty, -1) == Spec.Index
						&& obj.GetIntProperty(ObjectStateProperty, -1) == (int)Spec.State
						&& obj.GetIntProperty(ObjectConditionProperty, -1) == Spec.Condition
						&& obj.GetIntProperty(ObjectFreshEmptyProperty, 0) == 1
						&& (!Spec.IsStreet || obj.GetIntProperty(KingdomRoads.PathStateProperty)
							== (int)KingdomRoadRules.WearState.Path)
						&& (!Spec.IsArchitecture || degraded || ExactArchitecture(obj, Spec))
						&& (Spec.Key != KingdomInheritRules.FounderCairnKey
							|| (obj.GetPart<Description>() != null
								&& obj.GetPart<Description>()._Short == CairnText)))
					{
						return true;
					}
				}
				return false;
			}

			public bool HasBlueprint(string Blueprint)
			{
				return GameObjectFactory.Factory.HasBlueprint(Blueprint);
			}

			public bool TryReadCell(int X, int Y, out KingdomInheritCellFacts Facts)
			{
				Facts = new KingdomInheritCellFacts();
				Cell cell = Zone.GetCell(X, Y);
				if (cell == null)
				{
					return false;
				}
				Facts.Exists = true;
				Facts.Occupied = IsOccupied(cell);
				Facts.Terrain = cell.HasOpenLiquidVolume();
				Facts.Stairs = cell.HasObjectWithPart("StairsUp")
					|| cell.HasObjectWithPart("StairsDown") || cell.HasStairs();
				Facts.Connection = Connections[X, Y];
				Facts.Walkable = cell.IsPassable(null, false);
				return true;
			}

			public bool TryCreateFresh(KingdomInheritBuildSpec Spec, string Marker, string CairnText,
				out object Handle, out string Failure)
			{
				Handle = null;
				Failure = "";
				string resolved;
				bool degradedArchitecture = false;
				bool authorityMemory = false;
				if (Spec == null)
				{
					Failure = "the inherited build row is absent";
					return false;
				}
				if (Spec.IsStreet)
					resolved = "DirtPath";
				else if (!Spec.IsArchitecture && KingdomInheritRules.IsFoundingHeartKey(Spec.Key))
				{
					resolved = "r_KingdomCairn";
					authorityMemory = true;
				}
				else if (Spec.IsArchitecture && !CanStampArchitecture(Spec))
				{
					resolved = "r_KingdomCairn";
					degradedArchitecture = true;
				}
				else if (!KingdomInheritRules.TryResolveBlueprint(Spec.Key, out resolved))
					resolved = null;
				if ((!degradedArchitecture && !authorityMemory && resolved != Spec.Blueprint)
					|| !GameObjectFactory.Factory.HasBlueprint(resolved))
				{
					Failure = "the inherited semantic key is not allowlisted by this build";
					return false;
				}
				GameObject obj = GameObject.CreateUnmodified(resolved);
				if (obj == null)
				{
					Failure = "the allowlisted inherited object factory returned nothing";
					return false;
				}
				Handle = obj;
				Scrub(obj);

				obj.SetStringProperty(ObjectMarkerProperty, Marker);
				obj.SetStringProperty(ObjectKeyProperty, Spec.Key);
				obj.SetIntProperty(ObjectIndexProperty, Spec.Index);
					obj.SetIntProperty(ObjectStateProperty, (int)Spec.State);
					obj.SetIntProperty(ObjectConditionProperty, Spec.Condition);
					obj.SetIntProperty(ObjectFreshEmptyProperty, 1);
					int inheritedWear = KingdomInheritanceFabricRules.WearFor(
						Spec.State, Spec.Condition);
					if (inheritedWear > 0) obj.RequirePart<r_KingdomInheritedFabric>();
				if (degradedArchitecture)
					obj.SetStringProperty(ObjectDegradedHashProperty, Spec.ArchitectureHash);
				if (authorityMemory) obj.SetIntProperty(ObjectAuthorityMemoryProperty, 1);
				if (Spec.IsStreet)
					obj.SetIntProperty(KingdomRoads.PathStateProperty,
						(int)KingdomRoadRules.WearState.Path);
				if (Spec.IsArchitecture && !degradedArchitecture)
				{
					KingdomArchitectureIntent intent;
					if (!TryArchitectureIntent(Spec, out intent, out Failure)
						|| !KingdomArchitectureRuntime.TryFreeze(obj, intent, out Failure)
						|| !KingdomArchitectureStamper.TryInitializeOwner(obj, intent,
							LotFor(Spec), out Failure)) return false;
				}
				if (Spec.State == KingdomInheritWorkState.Standing
					|| Spec.State == KingdomInheritWorkState.Derelict)
				{
					int baseHp = obj.baseHitpoints;
					if (baseHp > 0)
					{
						obj.hitpoints = Math.Max(1, baseHp * Spec.Condition / 100);
					}
				}
				if (authorityMemory)
				{
					Description description = obj.RequirePart<Description>();
					description._Short = "A bounded marker remembers the old settlement's founding heart without claiming its immutable basin in this world.";
				}
				else if (degradedArchitecture)
				{
					Description description = obj.RequirePart<Description>();
					description._Short = "A bounded marker remembers an authored work whose frozen fabric is not installed in this world.";
				}
				else if (Spec.Key == KingdomInheritRules.FounderCairnKey)
				{
					Description description = obj.RequirePart<Description>();
					description._Short = CairnText;
				}
				else if (Spec.State == KingdomInheritWorkState.Derelict)
				{
					Description description = obj.RequirePart<Description>();
					description._Short = (description._Short ?? "").TrimEnd()
						+ " It stands intact but derelict, with no stores or household left inside.";
				}
				if (!IsEmptyObject(obj))
				{
					Failure = "the fresh inherited object was not empty after stripping";
					return false;
				}
				return true;
			}

		}
#endif

	}
}
