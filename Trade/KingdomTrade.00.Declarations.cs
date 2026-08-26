using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Engine shell around the realm's durable, one-operation trade book.</summary>
	public static partial class KingdomTrade
	{
		private const string ProjectionProperty = "KingdomTradeProjectionId";
		private const string MaterialProperty = "KingdomTradeMaterialId";
		private const int MaxProjectionCellProbes = 512;
		private static readonly object InFlightSync = new object();
		private static TradeLease InFlight;

		private sealed class TradeLease : IDisposable
		{
			internal KingdomSystem System;
			internal Zone Zone;
			internal KingdomSurvey Survey;

			public void Dispose()
			{
				lock (InFlightSync)
				{
					if (!ReferenceEquals(InFlight, this)) return;
					// Keep old serialized API field coherent before another caller can observe
					// or mutate Trade authority. Properties are not safe here: KingdomSystem writes
					// engine named fields explicitly and must retain old field wire name.
					System?.SynchronizeLegacyManifestProjection();
					InFlight = null;
				}
			}
		}

		private sealed class TradeExileCoreSeal
		{
			internal KingdomSystem System;
			internal Simulation.City.KingdomCityBook City;
			internal KingdomSettlement Away;
			internal string GraphHash;
			internal KingdomTradeReferenceSeal References;
		}

		private sealed class TradeLiveFrame
		{
			internal KingdomSystem System;
			internal KingdomTradeBook Book;
			internal KingdomTradeOperation Operation;
			internal List<KingdomTradeCharter> Charters;
			internal List<KingdomTradeWaterLeg> WaterLegs;
			internal List<KingdomTradeMaterialOutput> MaterialOutputs;
			internal KingdomTradeWaterLeg[] WaterRows;
			internal KingdomTradeMaterialOutput[] MaterialRows;
			internal List<KingdomTradeProjectionRow> ProjectionRows;
			internal ProjectionRowWitness[] ProjectionRowValues;
			internal ManifestWitness Manifest;
			internal long RetainedEscrow;
			internal string LegacyProjectionId;
			internal string LegacyProjectionObjectId;
			internal string RealmId;
			internal Zone Zone;
			internal string SettlementId;
			internal string SettlementName;
			internal string KeepersRoster;
			internal Simulation.City.KingdomCityBook City;
			internal KingdomLedger Ledger;
			internal List<string> LedgerNotes;
			internal string[] LedgerNoteRows;
			internal int LedgerDelivered;
			internal Dictionary<string, int> Standings;
			internal Dictionary<string, int> StandingRows;
			internal List<string> ClaimedZones;
			internal string[] ClaimedZoneRows;
			internal List<string> CityZones;
			internal string[] CityZoneRows;
			internal List<string> SettlementIds;
			internal string[] SettlementIdRows;
			internal TradePhysicalFrame Physical;
			internal GameObject ProjectionObject;
			internal CellWitness ProjectionCell;
		}

		private sealed class TradePhysicalFrame
		{
			internal KingdomSurvey Survey;
			internal List<LiquidVolume> StoreList;
			internal LiquidVolume[] StoreRows;
			internal readonly List<WaterWitness> Water = new List<WaterWitness>();
			internal readonly List<MaterialWitness> Materials = new List<MaterialWitness>();
			internal readonly List<InventoryWitness> Inventories = new List<InventoryWitness>();
		}

		private sealed class WaterWitness
		{
			internal KingdomTradeWaterLeg Leg;
			internal GameObject Owner;
			internal LiquidVolume Vessel;
			internal Cell Cell;
			internal Dictionary<string, int> Dictionary;
			internal Dictionary<string, int> BeforeComponents;
			internal string OwnerId;
			internal string ZoneId;
			internal int Capacity;
			internal int Before;
			internal int Delta;
			internal int After;
			internal string BeforeComposition;
			internal string AfterComposition;
			internal LoadedTopologyWitness Topology;
		}

		private sealed class InventoryWitness
		{
			internal GameObject Owner;
			internal Inventory Inventory;
			internal List<GameObject> Objects;
			internal GameObject[] Rows;
		}

		private sealed class MaterialWitness
		{
			internal KingdomTradeMaterialOutput Output;
			internal GameObject Item;
			internal GameObject Destination;
			internal InventoryWitness Inventory;
			internal string OutputId;
			internal string Marker;
			internal string Blueprint;
			internal int Count;
			internal string DestinationOwnerId;
			internal string ZoneId;
			internal LoadedTopologyWitness Topology;
		}

		private enum LoadedObjectResolution : byte
		{
			Incomplete = 0,
			Missing = 1,
			ExactUnique = 2,
			Ambiguous = 3
		}

		private sealed class LoadedTopologyWitness
		{
			internal ZoneManager Manager;
			internal KingdomSurvey Survey;
			internal Zone Active;
			internal List<GameObject> RootList;
			internal readonly List<LoadedZoneWitness> Zones = new List<LoadedZoneWitness>();
			internal readonly List<LoadedObjectWitness> Objects = new List<LoadedObjectWitness>();
		}

		private sealed class LoadedZoneWitness
		{
			internal Zone Zone;
			internal GameObject[] Roots;
		}

		private sealed class LoadedObjectWitness
		{
			internal GameObject Object;
			internal GameObject Root;
			internal Zone Zone;
			internal Inventory Inventory;
			internal List<GameObject> InventoryObjects;
			internal GameObject[] InventoryRows;
			internal GameObject[] Contents;
		}

		private sealed class CellWitness
		{
			internal Cell Cell;
			internal Cell.ObjectRack Objects;
			internal GameObject[] Rows;
		}

		private sealed class ProjectionRowWitness
		{
			internal KingdomTradeProjectionRow Row;
			internal long OperationSequence;
			internal string SettlementId;
			internal string ZoneId;
			internal string ProjectionId;
			internal string ObjectId;
			internal bool Quarantined;
			internal string Fault;
		}

		private sealed class ManifestWitness
		{
			internal KingdomTradeManifestState Row;
			internal long OperationSequence;
			internal string OperationId;
			internal string Id;
			internal string OriginId;
			internal string OriginName;
			internal string DestinationId;
			internal string DestinationName;
			internal int OriginalDrams;
			internal int EscrowDrams;
			internal long LoadedTick;
			internal long DeadlineTick;
			internal bool TurnedBack;
			internal KingdomTradeManifestStatus Status;
			internal string Fault;
		}

		private sealed class CallbackWitness
		{
			internal KingdomTradeAuthoritySeal Seal;
			internal byte[] AuthorityBytes;
			internal List<string> ClaimedZones;
			internal string[] ClaimedRows;
			internal List<string> CityZones;
			internal string[] CityZoneRows;
			internal List<string> SettlementIds;
			internal string[] SettlementRows;
			internal List<KingdomTradeCharter> Charters;
			internal KingdomTradeCharter[] CharterRows;
			internal List<KingdomTradeProjectionRow> Projections;
			internal KingdomTradeProjectionRow[] ProjectionRows;
			internal List<KingdomTradeProof> Proofs;
			internal KingdomTradeProof[] ProofRows;
			internal List<KingdomTradeProofCompaction> CompactedProofs;
			internal KingdomTradeProofCompaction[] CompactedProofRows;
			internal List<KingdomTradeArchive> Archives;
			internal KingdomTradeArchive[] ArchiveRows;
			internal List<KingdomTradeIncident> Incidents;
			internal KingdomTradeIncident[] IncidentRows;
			internal KingdomTradeManifestState Manifest;
			internal KingdomTradeOperation Operation;
			internal KingdomTradeStandingCas Standing;
			internal KingdomTradeOutbox Outbox;
		}

	}
}
