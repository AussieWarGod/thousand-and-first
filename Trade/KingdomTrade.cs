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
	public static class KingdomTrade
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

		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionTrade") != "No";

		private static bool TryEnter(KingdomSystem System, out TradeLease Lease)
		{
			lock (InFlightSync)
			{
				if (System == null || InFlight != null)
				{
					Lease = null;
					return false;
				}
				Lease = new TradeLease { System = System };
				InFlight = Lease;
				return true;
			}
		}

		private static bool BindOperationSettlement(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeOperation Operation, Zone Z)
		{
			if (System == null || Book == null || Operation == null || Z == null
				|| !System.Founded || System.Ledger == null
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)
				|| System.City == null || System.City.ZoneIds == null
				|| !System.City.ZoneIds.Contains(Z.ZoneID)
				|| !KingdomTradeRules.IdentityContainsSettlement(Book, System.City.SettlementId)
				|| !KingdomTradeRules.ValidName(System.SeatName)) return false;
			Operation.ZoneId = Z.ZoneID;
			Operation.SettlementName = System.SeatName;
			Operation.SettlementId = System.City.SettlementId;
			return KingdomTradeRules.ValidId(Operation.SettlementId);
		}

		private static bool TryBindFrame(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeOperation Operation, Zone Z, out TradeLiveFrame Frame)
		{
			Frame = null;
			if (System == null || Book == null || !KingdomTradeRules.BookUsable(Book)
				|| !ReferenceEquals(System.TradeBook, Book) || Book.Charters == null
				|| System.Ledger == null || System.Ledger.Notes == null
				|| System.ClaimedZones == null || System.City == null
				|| System.Standings == null) return false;
			TradeLiveFrame frame = new TradeLiveFrame
			{
				System = System,
				Book = Book,
				Operation = Operation,
				Charters = Book.Charters,
				WaterLegs = Operation?.WaterLegs,
				MaterialOutputs = Operation?.MaterialOutputs,
				WaterRows = Operation?.WaterLegs?.ToArray(),
				MaterialRows = Operation?.MaterialOutputs?.ToArray(),
				ProjectionRows = Book.Projections,
				ProjectionRowValues = CaptureProjectionRows(Book.Projections),
				Manifest = CaptureManifest(Book.Manifest),
				RetainedEscrow = Book.RetainedEscrowDrams,
				LegacyProjectionId = Book.ActiveProjectionId,
				LegacyProjectionObjectId = Book.ActiveProjectionObjectId,
				RealmId = Book.RealmId,
				Zone = Z,
				SettlementId = Operation == null ? System.City.SettlementId
					: Operation.SettlementId,
					SettlementName = Operation == null ? System.SeatName
						: Operation.SettlementName,
					KeepersRoster = System.KeepersRoster ?? "",
				City = System.City,
				Ledger = System.Ledger,
				LedgerNotes = System.Ledger.Notes,
				LedgerNoteRows = System.Ledger.Notes.ToArray(),
				LedgerDelivered = System.Ledger.Delivered,
				Standings = System.Standings,
				StandingRows = new Dictionary<string, int>(System.Standings),
				ClaimedZones = System.ClaimedZones,
				ClaimedZoneRows = System.ClaimedZones.ToArray(),
				CityZones = System.City.ZoneIds,
				CityZoneRows = System.City.ZoneIds?.ToArray(),
				SettlementIds = Book.SettlementIds,
				SettlementIdRows = Book.SettlementIds?.ToArray()
			};
			if (Operation != null)
			{
				if (!ReferenceEquals(Book.OpenOperation, Operation) || Z == null
					|| System.Ledger == null || System.ClaimedZones == null) return false;
				if (!ExactSettlement(frame)) return false;
			}
			Frame = frame;
			return true;
		}

		private static bool ExactSettlement(TradeLiveFrame Frame)
		{
			if (Frame == null) return false;
			KingdomSystem system = Frame.System;
			bool common = system != null && system.Founded
				&& KingdomTradeRules.ValidId(Frame.SettlementId)
				&& KingdomTradeRules.ValidName(Frame.SettlementName)
				&& ReferenceEquals(system.Ledger, Frame.Ledger)
				&& ReferenceEquals(system.ClaimedZones, Frame.ClaimedZones)
				&& ExactStrings(Frame.ClaimedZones, Frame.ClaimedZoneRows)
				&& ReferenceEquals(system.City, Frame.City)
				&& ReferenceEquals(Frame.City.ZoneIds, Frame.CityZones)
				&& ExactStrings(Frame.CityZones, Frame.CityZoneRows)
				&& ReferenceEquals(Frame.Book.SettlementIds, Frame.SettlementIds)
				&& ExactStrings(Frame.SettlementIds, Frame.SettlementIdRows)
				&& ReferenceEquals(system.Standings, Frame.Standings)
				&& ExactDictionary(Frame.Standings, Frame.StandingRows)
				&& ExactLedger(Frame)
					&& string.Equals(system.SeatName, Frame.SettlementName,
						StringComparison.Ordinal)
					&& string.Equals(system.KeepersRoster ?? "", Frame.KeepersRoster,
						StringComparison.Ordinal)
				&& string.Equals(Frame.City.SettlementId, Frame.SettlementId,
					StringComparison.Ordinal);
			if (!common || Frame.Operation == null) return common;
			return Frame.Zone != null && Frame.ClaimedZones.Contains(Frame.Zone.ZoneID)
				&& Frame.City.ZoneIds != null && Frame.City.ZoneIds.Contains(Frame.Zone.ZoneID)
				&& string.Equals(Frame.Zone.ZoneID, Frame.Operation.ZoneId,
					StringComparison.Ordinal)
				&& string.Equals(Frame.Operation.SettlementName, Frame.SettlementName,
					StringComparison.Ordinal)
				&& string.Equals(Frame.Operation.SettlementId, Frame.SettlementId,
					StringComparison.Ordinal);
		}

		private static bool ExactLedger(TradeLiveFrame Frame)
		{
			if (Frame == null || Frame.Ledger == null
				|| !ReferenceEquals(Frame.System?.Ledger, Frame.Ledger)
				|| !ReferenceEquals(Frame.Ledger.Notes, Frame.LedgerNotes)
				|| Frame.LedgerNotes == null || Frame.LedgerNoteRows == null
				|| Frame.Ledger.Delivered != Frame.LedgerDelivered
				|| Frame.LedgerNotes.Count != Frame.LedgerNoteRows.Length) return false;
			for (int i = 0; i < Frame.LedgerNoteRows.Length; i++)
				if (!string.Equals(Frame.LedgerNotes[i], Frame.LedgerNoteRows[i],
					StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ExactDictionary(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count)
				return false;
			foreach (KeyValuePair<string, int> pair in Expected)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		private static bool ExactStandingWithOverride(TradeLiveFrame Frame,
			string Faction, int Value)
		{
			if (Frame?.Standings == null || Frame.StandingRows == null
				|| !ReferenceEquals(Frame.System.Standings, Frame.Standings)) return false;
			int expectedCount = Frame.StandingRows.ContainsKey(Faction)
				? Frame.StandingRows.Count : Frame.StandingRows.Count + 1;
			if (Frame.Standings.Count != expectedCount) return false;
			foreach (KeyValuePair<string, int> pair in Frame.StandingRows)
			{
				int current;
				int expected = string.Equals(pair.Key, Faction, StringComparison.Ordinal)
					? Value : pair.Value;
				if (!Frame.Standings.TryGetValue(pair.Key, out current)
					|| current != expected) return false;
			}
			int after;
			return Frame.Standings.TryGetValue(Faction, out after) && after == Value;
		}

		private static bool ExactAuthority(TradeLiveFrame Frame,
			KingdomTradePhase ExpectedPhase)
		{
			if (Frame == null || Frame.System == null || Frame.Book == null
				|| !ReferenceEquals(Frame.System.TradeBook, Frame.Book)
				|| !ReferenceEquals(Frame.Book.Charters, Frame.Charters)
				|| !string.Equals(Frame.Book.RealmId, Frame.RealmId,
					StringComparison.Ordinal) || !KingdomTradeRules.BookUsable(Frame.Book)
				|| !ExactProjectionRows(Frame) || !ExactBookDomain(Frame)) return false;
			if (!ExactSettlement(Frame)) return false;
			if (Frame.Operation == null) return true;
			return ReferenceEquals(Frame.Book.OpenOperation, Frame.Operation)
				&& ReferenceEquals(Frame.Operation.WaterLegs, Frame.WaterLegs)
				&& ReferenceEquals(Frame.Operation.MaterialOutputs, Frame.MaterialOutputs)
				&& ExactReceiptRows(Frame)
				&& Frame.Operation.Phase == ExpectedPhase && ExactSettlement(Frame);
		}

		private static CallbackWitness CaptureCallbackWitness(TradeLiveFrame Frame)
		{
			try
			{
				KingdomTradeBook book = Frame?.Book;
				if (book == null || Frame.System == null || Frame.City?.ZoneIds == null
					|| Frame.System.ClaimedZones == null || book.SettlementIds == null
					|| book.Charters == null || book.Projections == null || book.RecentProofs == null
					|| book.CompactedProofs == null
					|| book.Archives == null || book.Incidents == null) return null;
				return new CallbackWitness
				{
					Seal = KingdomTradeRules.CaptureAuthoritySeal(book,
						Frame.System.ClaimedZones, Frame.City.ZoneIds),
					AuthorityBytes = KingdomTradeCodec.EncodePayload(book),
					ClaimedZones = Frame.System.ClaimedZones,
					ClaimedRows = Frame.System.ClaimedZones.ToArray(),
					CityZones = Frame.City.ZoneIds,
					CityZoneRows = Frame.City.ZoneIds.ToArray(),
					SettlementIds = book.SettlementIds,
					SettlementRows = book.SettlementIds.ToArray(),
					Charters = book.Charters, CharterRows = book.Charters.ToArray(),
					Projections = book.Projections, ProjectionRows = book.Projections.ToArray(),
					Proofs = book.RecentProofs, ProofRows = book.RecentProofs.ToArray(),
					CompactedProofs = book.CompactedProofs,
					CompactedProofRows = book.CompactedProofs.ToArray(),
					Archives = book.Archives, ArchiveRows = book.Archives.ToArray(),
					Incidents = book.Incidents, IncidentRows = book.Incidents.ToArray(),
					Manifest = book.Manifest, Operation = book.OpenOperation,
					Standing = book.OpenOperation?.Standing, Outbox = book.OpenOperation?.Outbox
				};
			}
			catch { return null; }
		}

		private static bool ExactCallbackWitness(TradeLiveFrame Frame, CallbackWitness Witness)
		{
			if (Frame == null || Witness == null || Frame.Book == null
				|| !KingdomTradeRules.ExactAuthoritySeal(Frame.Book,
					Frame.System?.ClaimedZones, Frame.City?.ZoneIds, Witness.Seal)
				|| !ReferenceEquals(Frame.System?.ClaimedZones, Witness.ClaimedZones)
				|| !ReferenceEquals(Frame.City?.ZoneIds, Witness.CityZones)
				|| !ReferenceEquals(Frame.Book.SettlementIds, Witness.SettlementIds)
				|| !ReferenceEquals(Frame.Book.Charters, Witness.Charters)
				|| !ReferenceEquals(Frame.Book.Projections, Witness.Projections)
				|| !ReferenceEquals(Frame.Book.RecentProofs, Witness.Proofs)
				|| !ReferenceEquals(Frame.Book.CompactedProofs, Witness.CompactedProofs)
				|| !ReferenceEquals(Frame.Book.Archives, Witness.Archives)
				|| !ReferenceEquals(Frame.Book.Incidents, Witness.Incidents)
				|| !ReferenceEquals(Frame.Book.Manifest, Witness.Manifest)
				|| !ReferenceEquals(Frame.Book.OpenOperation, Witness.Operation)
				|| !ReferenceEquals(Frame.Book.OpenOperation?.Standing, Witness.Standing)
				|| !ReferenceEquals(Frame.Book.OpenOperation?.Outbox, Witness.Outbox)
				|| !ExactStrings(Witness.ClaimedZones, Witness.ClaimedRows)
				|| !ExactStrings(Witness.CityZones, Witness.CityZoneRows)
				|| !ExactStrings(Witness.SettlementIds, Witness.SettlementRows)
				|| !ExactReferences(Witness.Charters, Witness.CharterRows)
				|| !ExactReferences(Witness.Projections, Witness.ProjectionRows)
				|| !ExactReferences(Witness.Proofs, Witness.ProofRows)
				|| !ExactReferences(Witness.CompactedProofs, Witness.CompactedProofRows)
				|| !ExactReferences(Witness.Archives, Witness.ArchiveRows)
				|| !ExactReferences(Witness.Incidents, Witness.IncidentRows)) return false;
			byte[] current;
			try { current = KingdomTradeCodec.EncodePayload(Frame.Book); }
			catch { return false; }
			if (current.Length != Witness.AuthorityBytes.Length) return false;
			for (int i = 0; i < current.Length; i++)
				if (current[i] != Witness.AuthorityBytes[i]) return false;
			return true;
		}

		private static bool ExactStrings(List<string> Current, string[] Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (!string.Equals(Current[i], Expected[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ExactBytes(byte[] Current, byte[] Expected)
		{
			if (Current == null || Expected == null || Current.Length != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (Current[i] != Expected[i]) return false;
			return true;
		}

		private static bool ExactSettlementTopology(List<string> Current,
			List<string> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count)
				return false;
			for (int i = 0; i < Expected.Count; i++)
				if (!string.Equals(Current[i], Expected[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool TryCaptureExileCoreSeal(KingdomSystem System,
			out TradeExileCoreSeal Seal, out string Failure)
		{
			Seal = null;
			Failure = null;
			try
			{
				if (System?.City == null || !KingdomRealmArchive.TryCurrentGraphHash(System,
					out string graphHash, out Failure)
					|| !TryExileReferenceRoots(System, out List<object> roots)
					|| !KingdomTradeRules.TryCaptureExactReferenceSeal(roots,
						out KingdomTradeReferenceSeal references))
				{
					Failure = Failure ?? "exact Core reference graph could not be frozen";
					return false;
				}
				Seal = new TradeExileCoreSeal
				{
					System = System,
					City = System.City,
					Away = System.Away,
					GraphHash = graphHash,
					References = references
				};
				return true;
			}
			catch (Exception error)
			{
				Failure = error.Message;
				Seal = null;
				return false;
			}
		}

		private static bool ExactExileCoreSeal(KingdomSystem System, TradeExileCoreSeal Seal)
		{
			try
			{
				return Seal != null && ReferenceEquals(System, Seal.System)
					&& ReferenceEquals(System.City, Seal.City)
					&& ReferenceEquals(System.Away, Seal.Away)
					&& KingdomRealmArchive.TryCurrentGraphHash(System, out string graphHash,
						out string _)
					&& string.Equals(graphHash, Seal.GraphHash, StringComparison.Ordinal)
					&& TryExileReferenceRoots(System, out List<object> roots)
					&& KingdomTradeRules.ExactReferenceSeal(roots, Seal.References);
			}
			catch { return false; }
		}

		private static bool TryExileReferenceRoots(KingdomSystem System,
			out List<object> Roots)
		{
			Roots = new List<object>();
			if (System == null) return false;
			try
			{
				// Capture every mutable seated-settlement field by the same field-name contract
				// KingdomSystem.Capture uses, plus every realm-level mutable root in TAG1.
				FieldInfo[] settlement = typeof(KingdomSettlement).GetFields(
					BindingFlags.Instance | BindingFlags.Public);
				Array.Sort(settlement,
					(left, right) => string.CompareOrdinal(left.Name, right.Name));
				for (int i = 0; i < settlement.Length; i++)
				{
					FieldInfo archived = settlement[i];
					if (archived.IsStatic || archived.FieldType.IsValueType
						|| archived.FieldType == typeof(string)
						|| archived.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
					FieldInfo live = typeof(KingdomSystem).GetField(archived.Name,
						BindingFlags.Instance | BindingFlags.Public);
					if (live == null || live.FieldType != archived.FieldType) return false;
					Roots.Add(live.GetValue(System));
				}
				Roots.Add(System.City);
				Roots.Add(System.Away);
				Roots.Add(System.Seceded);
				Roots.Add(System.CarryBook);
				Roots.Add(System.Bindings);
				Roots.Add(System.Jobs);
				Roots.Add(System.Standings);
				Roots.Add(System.ChronicleEntries);
				Roots.Add(System.OutsiderEntries);
				Roots.Add(System.Haul);
				return Roots.Count <= 256;
			}
			catch { Roots = null; return false; }
		}

		/// <summary>
		/// Freezes only the exact active settlement ground already indexed for this trade lease.
		/// Cached zones are not transaction participants: scanning them made one local delivery
		/// proportional to every zone the player had visited and silently created foreign surveys
		/// inside the bound semantic pass. A caller standing on unavailable or non-active ground
		/// receives no witness and therefore defers without touching physical authority.
		/// </summary>
		private static LoadedTopologyWitness CaptureLoadedTopology()
		{
			try
			{
				ZoneManager manager;
				Zone zone;
				KingdomSurvey survey;
				if (!TryBoundTopologyGround(out manager, out zone, out survey)) return null;
				IList<GameObject> indexed;
				if (!survey.TryLoaded(out indexed) || indexed == null) return null;
				LoadedTopologyWitness witness = new LoadedTopologyWitness
				{
					Manager = manager,
					Survey = survey,
					Active = zone,
					RootList = survey.Objects
				};
				LoadedZoneWitness zoneWitness = new LoadedZoneWitness
				{
					Zone = zone,
					Roots = survey.Objects.ToArray()
				};
				witness.Zones.Add(zoneWitness);
				HashSet<GameObject> visited = new HashSet<GameObject>();
				for (int i = 0; i < zoneWitness.Roots.Length; i++)
					if (!CaptureLoadedObject(witness, zoneWitness.Roots[i],
						zoneWitness.Roots[i], zone, visited)) return null;
				return witness;
			}
			catch { return null; }
		}

		private static bool TryBindTopologyGround(KingdomSystem System, Zone Z,
			KingdomSurvey Survey)
		{
			try
			{
				ZoneManager manager = The.ZoneManager;
				KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
				if (System == null || Z == null || Survey == null || manager == null
					|| !ReferenceEquals(manager.ActiveZone, Z)
					|| !ReferenceEquals(Survey.Ground, Z)
					|| (active != null && !ReferenceEquals(active, Survey))
					|| !Survey.TryLoaded(out IList<GameObject> loaded) || loaded == null)
					return false;
				lock (InFlightSync)
				{
					if (InFlight == null || !ReferenceEquals(InFlight.System, System)) return false;
					InFlight.Zone = Z;
					InFlight.Survey = Survey;
				}
				return true;
			}
			catch { return false; }
		}

		private static bool TryBoundTopologyGround(out ZoneManager Manager,
			out Zone Z, out KingdomSurvey Survey)
		{
			Manager = null;
			Z = null;
			Survey = null;
			lock (InFlightSync)
			{
				if (InFlight == null) return false;
				Z = InFlight.Zone;
				Survey = InFlight.Survey;
			}
			Manager = The.ZoneManager;
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			return Manager != null && Z != null && Survey != null
				&& ReferenceEquals(Manager.ActiveZone, Z)
				&& ReferenceEquals(Survey.Ground, Z)
				&& (active == null || ReferenceEquals(active, Survey));
		}

		private static KingdomSurvey BoundTradeSurvey(Zone Z)
		{
			ZoneManager manager;
			Zone ground;
			KingdomSurvey survey;
			return TryBoundTopologyGround(out manager, out ground, out survey)
				&& ReferenceEquals(ground, Z) ? survey : null;
		}

		private static bool CaptureLoadedObject(LoadedTopologyWitness Topology,
			GameObject Object, GameObject Root, Zone Zone, HashSet<GameObject> Visited)
		{
			if (Topology == null || !GameObject.Validate(Object) || Root == null || Zone == null
				|| !Visited.Add(Object) || Topology.Objects.Count >= 200000) return false;
			Inventory inventory = Object.Inventory;
			List<GameObject> inventoryObjects = inventory?.Objects;
			if (inventory != null && (inventoryObjects == null
				|| !ReferenceEquals(Object.GetPart<Inventory>(), inventory)
				|| inventory.ParentObject != Object)) return false;
			List<GameObject> contents = Object.GetInventoryAndEquipmentAndDefaultEquipment();
			if (contents == null) return false;
			List<GameObject> installed = Object.Body?.GetInstalledCybernetics();
			if (installed != null)
				for (int i = 0; i < installed.Count; i++)
					if (!ContainsObjectReference(contents, installed[i])) contents.Add(installed[i]);
			if (inventoryObjects != null)
				for (int i = 0; i < inventoryObjects.Count; i++)
					if (!ContainsObjectReference(contents, inventoryObjects[i]))
						contents.Add(inventoryObjects[i]);
			LoadedObjectWitness row = new LoadedObjectWitness
			{
				Object = Object,
				Root = Root,
				Zone = Zone,
				Inventory = inventory,
				InventoryObjects = inventoryObjects,
				InventoryRows = inventoryObjects?.ToArray(),
				Contents = contents.ToArray()
			};
			Topology.Objects.Add(row);
			for (int i = 0; i < row.Contents.Length; i++)
				if (!CaptureLoadedObject(Topology, row.Contents[i], Root, Zone, Visited)) return false;
			return true;
		}

		private static bool ContainsObjectReference(IList<GameObject> Values, GameObject Value)
		{
			if (Values == null) return false;
			for (int i = 0; i < Values.Count; i++)
				if (ReferenceEquals(Values[i], Value)) return true;
			return false;
		}

		private static bool ExactLoadedInfrastructure(LoadedTopologyWitness Expected,
			LoadedTopologyWitness Current)
		{
			if (Expected == null || Current == null
				|| !ReferenceEquals(Expected.Manager, Current.Manager)
				|| !ReferenceEquals(Expected.Survey, Current.Survey)
				|| !ReferenceEquals(Expected.Active, Current.Active)
				|| !ReferenceEquals(Expected.RootList, Current.RootList)
				|| Expected.Zones.Count != Current.Zones.Count) return false;
			for (int i = 0; i < Expected.Zones.Count; i++)
				if (!ReferenceEquals(Expected.Zones[i].Zone, Current.Zones[i].Zone)) return false;
			return true;
		}

		private static LoadedObjectWitness FindLoadedRow(LoadedTopologyWitness Topology,
			GameObject Object)
		{
			if (Topology == null) return null;
			for (int i = 0; i < Topology.Objects.Count; i++)
				if (ReferenceEquals(Topology.Objects[i].Object, Object)) return Topology.Objects[i];
			return null;
		}

		private static bool ExactLoadedRow(LoadedObjectWitness Expected,
			LoadedObjectWitness Current)
		{
			if (Expected == null || Current == null
				|| !ReferenceEquals(Expected.Object, Current.Object)
				|| !ReferenceEquals(Expected.Root, Current.Root)
				|| !ReferenceEquals(Expected.Zone, Current.Zone)
				|| !ReferenceEquals(Expected.Inventory, Current.Inventory)
				|| !ReferenceEquals(Expected.InventoryObjects, Current.InventoryObjects)
				|| !ExactObjectRows(Expected.InventoryRows, Current.InventoryRows)
				|| !ExactObjectRows(Expected.Contents, Current.Contents)) return false;
			return true;
		}

		private static bool ExactObjectRows(GameObject[] Left, GameObject[] Right)
		{
			if (Left == null || Right == null) return Left == null && Right == null;
			if (Left.Length != Right.Length) return false;
			for (int i = 0; i < Left.Length; i++)
				if (!ReferenceEquals(Left[i], Right[i])) return false;
			return true;
		}

		private static bool ExactLoadedTopology(LoadedTopologyWitness Expected)
		{
			LoadedTopologyWitness current = CaptureLoadedTopology();
			if (!ExactLoadedInfrastructure(Expected, current)
				|| Expected.Objects.Count != current.Objects.Count) return false;
			for (int i = 0; i < Expected.Zones.Count; i++)
				if (!ExactObjectRows(Expected.Zones[i].Roots, current.Zones[i].Roots)) return false;
			for (int i = 0; i < Expected.Objects.Count; i++)
				if (!ExactLoadedRow(Expected.Objects[i], current.Objects[i])) return false;
			return true;
		}

		private static LoadedObjectResolution ResolveLoadedObject(string Id, Zone ExpectedZone,
			out GameObject Object, out LoadedTopologyWitness Topology)
		{
			Object = null;
			Topology = CaptureLoadedTopology();
			if (!KingdomTradeRules.ValidId(Id) || ExpectedZone == null || Topology == null)
				return LoadedObjectResolution.Incomplete;
			bool zoneLoaded = false;
			LoadedObjectWitness exact;
			for (int i = 0; i < Topology.Zones.Count; i++)
				if (ReferenceEquals(Topology.Zones[i].Zone, ExpectedZone)) zoneLoaded = true;
			if (!zoneLoaded) return LoadedObjectResolution.Incomplete;
			KingdomTradeExactLookup result = KingdomTradeRules.ResolveExactUnique(
				Topology.Objects, Id, row => row.Object.ID, out exact);
			if (result == KingdomTradeExactLookup.Incomplete) return LoadedObjectResolution.Incomplete;
			if (result == KingdomTradeExactLookup.Missing) return LoadedObjectResolution.Missing;
			if (result == KingdomTradeExactLookup.Ambiguous) return LoadedObjectResolution.Ambiguous;
			if (!ReferenceEquals(exact.Zone, ExpectedZone)) return LoadedObjectResolution.Ambiguous;
			Object = exact.Object;
			return LoadedObjectResolution.ExactUnique;
		}

		private static bool ExactLoadedTopologyWithDelta(LoadedTopologyWitness Expected,
			GameObject Added, GameObject Removed, GameObject ChangedInventoryOwner,
			bool RootDelta)
		{
			LoadedTopologyWitness current = CaptureLoadedTopology();
			if (!ExactLoadedInfrastructure(Expected, current)) return false;
			List<GameObject> addedTree = Added == null ? new List<GameObject>()
				: LoadedSubtree(Current: current, Root: Added, RootDelta: RootDelta);
			int removedCount = 0;
			for (int i = 0; i < Expected.Objects.Count; i++)
				if (Removed != null && ReferenceEquals(Expected.Objects[i].Root, Removed)) removedCount++;
			if (current.Objects.Count != Expected.Objects.Count - removedCount + addedTree.Count)
				return false;
			for (int i = 0; i < Expected.Objects.Count; i++)
			{
				LoadedObjectWitness prior = Expected.Objects[i];
				if (Removed != null && ReferenceEquals(prior.Root, Removed))
				{
					if (FindLoadedRow(current, prior.Object) != null) return false;
					continue;
				}
				LoadedObjectWitness now = FindLoadedRow(current, prior.Object);
				if (now == null) return false;
				if (ReferenceEquals(prior.Object, ChangedInventoryOwner))
				{
					if (!ReferenceEquals(prior.Object, now.Object)
						|| !ReferenceEquals(prior.Root, now.Root)
						|| !ReferenceEquals(prior.Zone, now.Zone)
						|| !ReferenceEquals(prior.Inventory, now.Inventory)
						|| !ReferenceEquals(prior.InventoryObjects, now.InventoryObjects)
						|| prior.InventoryRows == null || now.InventoryRows == null
						|| now.InventoryRows.Length != prior.InventoryRows.Length + 1)
						return false;
					for (int j = 0; j < prior.InventoryRows.Length; j++)
						if (!ReferenceEquals(prior.InventoryRows[j], now.InventoryRows[j])) return false;
					if (!ReferenceEquals(now.InventoryRows[now.InventoryRows.Length - 1], Added))
						return false;
				}
				else if (!ExactLoadedRow(prior, now)) return false;
			}
			for (int i = 0; i < current.Objects.Count; i++)
			{
				GameObject item = current.Objects[i].Object;
				if (FindLoadedRow(Expected, item) == null
					&& !ContainsObjectReference(addedTree, item)) return false;
			}
			for (int i = 0; i < Expected.Zones.Count; i++)
			{
				GameObject[] prior = Expected.Zones[i].Roots;
				GameObject[] now = current.Zones[i].Roots;
				int delta = 0;
				if (RootDelta && Added != null
					&& Added.CurrentZone == current.Zones[i].Zone) delta++;
				if (RootDelta && Removed != null
					&& ReferenceEquals(Expected.Zones[i].Zone,
						FindLoadedRow(Expected, Removed)?.Zone)) delta--;
				if (now.Length != prior.Length + delta) return false;
				for (int j = 0; j < prior.Length; j++)
					if (!ReferenceEquals(prior[j], Removed)
						&& !ContainsObjectReference(now, prior[j])) return false;
				if (delta > 0 && !ContainsObjectReference(now, Added)) return false;
			}
			return true;
		}

		private static List<GameObject> LoadedSubtree(LoadedTopologyWitness Current,
			GameObject Root, bool RootDelta)
		{
			List<GameObject> result = new List<GameObject>();
			if (Current == null || Root == null) return result;
			for (int i = 0; i < Current.Objects.Count; i++)
			{
				LoadedObjectWitness row = Current.Objects[i];
				if ((RootDelta && ReferenceEquals(row.Root, Root))
					|| (!RootDelta && (ReferenceEquals(row.Object, Root)
						|| IsContentDescendant(Current, row.Object, Root)))) result.Add(row.Object);
			}
			return result;
		}

		private static bool IsContentDescendant(LoadedTopologyWitness Topology,
			GameObject Candidate, GameObject Ancestor)
		{
			HashSet<GameObject> frontier = new HashSet<GameObject> { Ancestor };
			for (int pass = 0; pass < Topology.Objects.Count; pass++)
			{
				bool changed = false;
				for (int i = 0; i < Topology.Objects.Count; i++)
				{
					LoadedObjectWitness row = Topology.Objects[i];
					if (!frontier.Contains(row.Object)) continue;
					for (int j = 0; j < row.Contents.Length; j++)
						if (frontier.Add(row.Contents[j])) changed = true;
				}
				if (frontier.Contains(Candidate)) return true;
				if (!changed) return false;
			}
			return frontier.Contains(Candidate);
		}

		private static void RefreshPhysicalTopologies(TradePhysicalFrame Physical)
		{
			if (Physical == null) return;
			LoadedTopologyWitness current = CaptureLoadedTopology();
			for (int i = 0; i < Physical.Water.Count; i++) Physical.Water[i].Topology = current;
			for (int i = 0; i < Physical.Materials.Count; i++) Physical.Materials[i].Topology = current;
		}

		private static bool ExactReferences<T>(List<T> Current, T[] Expected) where T : class
		{
			if (Current == null || Expected == null || Current.Count != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (!ReferenceEquals(Current[i], Expected[i])) return false;
			return true;
		}

		private static bool ExactReceiptRows(TradeLiveFrame Frame)
		{
			if (Frame?.WaterLegs == null || Frame.MaterialOutputs == null
				|| Frame.WaterRows == null || Frame.MaterialRows == null
				|| Frame.WaterLegs.Count != Frame.WaterRows.Length
				|| Frame.MaterialOutputs.Count != Frame.MaterialRows.Length) return false;
			for (int i = 0; i < Frame.WaterRows.Length; i++)
				if (!ReferenceEquals(Frame.WaterLegs[i], Frame.WaterRows[i])) return false;
			for (int i = 0; i < Frame.MaterialRows.Length; i++)
				if (!ReferenceEquals(Frame.MaterialOutputs[i], Frame.MaterialRows[i])) return false;
			return true;
		}

		private static void RefreshReceiptRows(TradeLiveFrame Frame)
		{
			if (Frame == null) return;
			Frame.WaterRows = Frame.WaterLegs?.ToArray();
			Frame.MaterialRows = Frame.MaterialOutputs?.ToArray();
		}

		private static WaterWitness CaptureWaterWitness(KingdomTradeWaterLeg Leg,
			GameObject Owner, LiquidVolume Vessel)
		{
			if (Leg == null || Owner == null || Vessel == null
				|| Vessel.ComponentLiquids == null) return null;
			return new WaterWitness
			{
				Leg = Leg,
				Owner = Owner,
				Vessel = Vessel,
				Cell = Owner.CurrentCell,
				Dictionary = Vessel.ComponentLiquids,
				BeforeComponents = new Dictionary<string, int>(Vessel.ComponentLiquids),
				OwnerId = Leg.OwnerId,
				ZoneId = Leg.ZoneId,
				Capacity = Leg.Capacity,
				Before = Leg.Before,
				Delta = Leg.Delta,
				After = Leg.After,
				BeforeComposition = Leg.BeforeComposition,
				AfterComposition = Leg.AfterComposition,
				Topology = CaptureLoadedTopology()
			};
		}

		private static bool ExactWaterReceipt(WaterWitness Witness)
		{
			return Witness != null && Witness.Leg != null
				&& string.Equals(Witness.Leg.OwnerId, Witness.OwnerId,
					StringComparison.Ordinal)
				&& string.Equals(Witness.Leg.ZoneId, Witness.ZoneId,
					StringComparison.Ordinal)
				&& Witness.Leg.Capacity == Witness.Capacity
				&& Witness.Leg.Before == Witness.Before
				&& Witness.Leg.Delta == Witness.Delta
				&& Witness.Leg.After == Witness.After
				&& string.Equals(Witness.Leg.BeforeComposition,
					Witness.BeforeComposition, StringComparison.Ordinal)
				&& string.Equals(Witness.Leg.AfterComposition,
					Witness.AfterComposition, StringComparison.Ordinal);
		}

		private static MaterialWitness CaptureMaterialWitness(
			KingdomTradeMaterialOutput Output, GameObject Item,
			GameObject Destination, InventoryWitness Inventory)
		{
			if (Output == null) return null;
			return new MaterialWitness
			{
				Output = Output,
				Item = Item,
				Destination = Destination,
				Inventory = Inventory,
				OutputId = Output.OutputId,
				Marker = Output.Marker,
				Blueprint = Output.Blueprint,
				Count = Output.Count,
				DestinationOwnerId = Output.DestinationOwnerId,
				ZoneId = Output.ZoneId,
				Topology = CaptureLoadedTopology()
			};
		}

		private static bool ExactMaterialReceipt(MaterialWitness Witness)
		{
			return Witness != null && Witness.Output != null
				&& string.Equals(Witness.Output.OutputId, Witness.OutputId,
					StringComparison.Ordinal)
				&& string.Equals(Witness.Output.Marker, Witness.Marker,
					StringComparison.Ordinal)
				&& string.Equals(Witness.Output.Blueprint, Witness.Blueprint,
					StringComparison.Ordinal)
				&& Witness.Output.Count == Witness.Count
				&& string.Equals(Witness.Output.DestinationOwnerId,
					Witness.DestinationOwnerId, StringComparison.Ordinal)
				&& string.Equals(Witness.Output.ZoneId, Witness.ZoneId,
					StringComparison.Ordinal);
		}

		private static ProjectionRowWitness[] CaptureProjectionRows(
			List<KingdomTradeProjectionRow> Rows)
		{
			if (Rows == null) return null;
			ProjectionRowWitness[] values = new ProjectionRowWitness[Rows.Count];
			for (int i = 0; i < Rows.Count; i++)
			{
				KingdomTradeProjectionRow row = Rows[i];
				if (row == null) return null;
				values[i] = new ProjectionRowWitness
				{
					Row = row, OperationSequence = row.OperationSequence,
					SettlementId = row.SettlementId, ZoneId = row.ZoneId,
					ProjectionId = row.ProjectionId, ObjectId = row.ObjectId,
					Quarantined = row.Quarantined, Fault = row.Fault
				};
			}
			return values;
		}

		private static bool ExactProjectionRows(TradeLiveFrame Frame)
		{
			if (Frame == null || !ReferenceEquals(Frame.Book?.Projections,
					Frame.ProjectionRows) || Frame.ProjectionRows == null
				|| Frame.ProjectionRowValues == null
				|| Frame.ProjectionRows.Count != Frame.ProjectionRowValues.Length) return false;
			for (int i = 0; i < Frame.ProjectionRowValues.Length; i++)
			{
				ProjectionRowWitness expected = Frame.ProjectionRowValues[i];
				KingdomTradeProjectionRow row = Frame.ProjectionRows[i];
				if (expected == null || !ReferenceEquals(row, expected.Row)
					|| row.OperationSequence != expected.OperationSequence
					|| !string.Equals(row.SettlementId, expected.SettlementId,
						StringComparison.Ordinal)
					|| !string.Equals(row.ZoneId, expected.ZoneId, StringComparison.Ordinal)
					|| !string.Equals(row.ProjectionId, expected.ProjectionId,
						StringComparison.Ordinal)
					|| !string.Equals(row.ObjectId, expected.ObjectId, StringComparison.Ordinal)
					|| row.Quarantined != expected.Quarantined
					|| !string.Equals(row.Fault, expected.Fault, StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private static void RefreshProjectionRows(TradeLiveFrame Frame)
		{
			if (Frame == null) return;
			Frame.ProjectionRows = Frame.Book?.Projections;
			Frame.ProjectionRowValues = CaptureProjectionRows(Frame.ProjectionRows);
		}

		private static ManifestWitness CaptureManifest(KingdomTradeManifestState Row)
		{
			return Row == null ? null : new ManifestWitness
			{
				Row = Row, OperationSequence = Row.OperationSequence,
				OperationId = Row.OperationId, Id = Row.Id,
				OriginId = Row.OriginId, OriginName = Row.OriginName,
				DestinationId = Row.DestinationId, DestinationName = Row.DestinationName,
				OriginalDrams = Row.OriginalDrams, EscrowDrams = Row.EscrowDrams,
				LoadedTick = Row.LoadedTick, DeadlineTick = Row.DeadlineTick,
				TurnedBack = Row.TurnedBack, Status = Row.Status, Fault = Row.Fault
			};
		}

		private static bool ExactBookDomain(TradeLiveFrame Frame)
		{
			if (Frame == null || Frame.Book == null
				|| Frame.Book.RetainedEscrowDrams != Frame.RetainedEscrow
				|| !string.Equals(Frame.Book.ActiveProjectionId,
					Frame.LegacyProjectionId, StringComparison.Ordinal)
				|| !string.Equals(Frame.Book.ActiveProjectionObjectId,
					Frame.LegacyProjectionObjectId, StringComparison.Ordinal)) return false;
			KingdomTradeManifestState row = Frame.Book.Manifest;
			ManifestWitness expected = Frame.Manifest;
			if (row == null || expected == null) return row == null && expected == null;
			return ReferenceEquals(row, expected.Row)
				&& row.OperationSequence == expected.OperationSequence
				&& string.Equals(row.OperationId, expected.OperationId, StringComparison.Ordinal)
				&& string.Equals(row.Id, expected.Id, StringComparison.Ordinal)
				&& string.Equals(row.OriginId, expected.OriginId, StringComparison.Ordinal)
				&& string.Equals(row.OriginName, expected.OriginName, StringComparison.Ordinal)
				&& string.Equals(row.DestinationId, expected.DestinationId, StringComparison.Ordinal)
				&& string.Equals(row.DestinationName, expected.DestinationName, StringComparison.Ordinal)
				&& row.OriginalDrams == expected.OriginalDrams
				&& row.EscrowDrams == expected.EscrowDrams
				&& row.LoadedTick == expected.LoadedTick
				&& row.DeadlineTick == expected.DeadlineTick
				&& row.TurnedBack == expected.TurnedBack && row.Status == expected.Status
				&& string.Equals(row.Fault, expected.Fault, StringComparison.Ordinal);
		}

		private static void RefreshBookDomain(TradeLiveFrame Frame)
		{
			if (Frame == null || Frame.Book == null) return;
			Frame.Manifest = CaptureManifest(Frame.Book.Manifest);
			Frame.RetainedEscrow = Frame.Book.RetainedEscrowDrams;
			Frame.LegacyProjectionId = Frame.Book.ActiveProjectionId;
			Frame.LegacyProjectionObjectId = Frame.Book.ActiveProjectionObjectId;
		}

		private static bool FailDetachedAuthority(TradeLiveFrame Frame, string Fault)
		{
			KingdomSystem system = Frame?.System;
			KingdomTradeBook original = Frame?.Book;
			KingdomTradeBook official = system?.TradeBook;
			long now = 0L;
			try { now = The.Game.TimeTicks; } catch { }
			if (official == null && system != null)
			{
				official = original;
				system.TradeBook = official;
			}
			if (original != null)
			{
				KingdomTradeRules.RecordIncident(original, now, Fault, original);
				KingdomTradeRules.QuarantineBook(original, Fault);
			}
			if (official != null && !ReferenceEquals(official, original))
				KingdomTradeRules.RecordIncident(official, now, Fault, original);
			KingdomTradeRules.QuarantineBook(official, Fault);
			system?.SynchronizeLegacyManifestProjection();
			return false;
		}

		public static KingdomTradeManifestState CurrentManifest(KingdomSystem System)
		{
			TradeLease lease;
			if (!TryEnter(System, out lease)) return null;
			using (lease)
			{
				return KingdomTradeRules.SnapshotManifest(EnsureBook(System)?.Manifest);
			}
		}

		public static bool ResetAuthority(KingdomSystem System, out string Failure)
		{
			Failure = null;
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Trade authority is busy; reset changed nothing.";
				return false;
			}
			using (lease)
			{
				System.TradeBook = new KingdomTradeBook();
				return true;
			}
		}

		public static bool StrikeDeal(KingdomSystem System, string DealKey,
			string FactionName, out string Failure)
		{
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Failure = "Settlement simulation is paused; no new trade charter was struck.";
				return false;
			}
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Another trade callback is already in flight; no charter was changed.";
				return false;
			}
			using (lease)
			{
				return StrikeDealCore(System, DealKey, FactionName, out Failure);
			}
		}

		private static bool StrikeDealCore(KingdomSystem System, string DealKey,
			string FactionName, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Trade is disabled. Existing receipts remain recorded, but no new charter is struck.";
				return false;
			}
			if (!KingdomData.TryGetDeal(DealKey, out KingdomRules.DealEntry deal))
			{
				Failure = "No such charter.";
				return false;
			}
			Faction faction = Factions.GetIfExists(FactionName);
			if (faction == null)
			{
				Failure = "No such faction.";
				return false;
			}
			if (System.GetStanding(FactionName) < deal.MinStanding)
			{
				Failure = faction.DisplayName + " will not treat with the kingdom yet (standing "
					+ System.GetStanding(FactionName) + " of " + deal.MinStanding + " needed).";
				return false;
			}
			long now = The.Game.TimeTicks;
			KingdomTradeBook book = EnsureBook(System);
			if (book == null)
			{
				Failure = "The trade book uses an unknown or quarantined schema.";
				return false;
			}
			if (System.City == null
				|| !KingdomTradeRules.ValidId(System.City.SettlementId))
			{
				Failure = "The seated city has no exact identity; no charter was changed.";
				return false;
			}
			if (book.OpenOperation != null)
			{
				Failure = "Another trade receipt is still being reconciled.";
				return false;
			}
			ApplyOption(book, true, now);
			if (!KingdomTradeRules.BookUsable(book))
			{
				Failure = book.SchemaFault ?? "Trade option evidence is not authoritative.";
				return false;
			}
			int active = 0;
			for (int i = 0; i < book.Charters.Count; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null || row.Quarantined) continue;
				active++;
			}
			if (active >= KingdomTradeRules.MaxCharters
				|| book.Charters.Count >= KingdomTradeRules.MaxCharters
				|| book.NextCharterSequence == long.MaxValue)
			{
				Failure = "The kingdom already keeps as many charters as it can honor.";
				return false;
			}
			long sequence = book.NextCharterSequence;
			string nextCharterId = KingdomTradeRules.CharterId(book.RealmId, sequence);
			if (!KingdomTradeRules.ValidId(nextCharterId))
			{
				Failure = "The charter identity could not be encoded exactly.";
				return false;
			}
			bool collision = false;
			for (int i = 0; i < book.Charters.Count; i++)
			{
				KingdomTradeCharter row = book.Charters[i];
				if (row == null) continue;
				if (!string.Equals(row.Id, nextCharterId, StringComparison.Ordinal)
					&& !(string.Equals(row.DealKey, DealKey, StringComparison.Ordinal)
						&& string.Equals(row.Faction, FactionName, StringComparison.Ordinal))) continue;
				collision = true;
				row.Quarantined = true;
				row.Fault = AppendFault(row.Fault, "new charter identity or schedule pair collided with preserved evidence");
			}
			if (collision)
			{
				Failure = "Charter evidence collides; every matching row was quarantined before mutation.";
				return false;
			}
			book.NextCharterSequence++;
			KingdomTradeCharter charter = new KingdomTradeCharter
			{
				Sequence = sequence,
				Id = nextCharterId,
				DealKey = DealKey,
				Faction = FactionName,
				CreatedTick = now,
				NextTick = KingdomTradeRules.SaturatingAdd(now, deal.IntervalTicks)
			};
			book.Charters.Add(charter);
			string charterId = charter.Id;
			long charterNext = charter.NextTick;
			TradeLiveFrame frame;
			if (!TryBindFrame(System, book, null, null, out frame)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				charter.Quarantined = true;
				charter.Fault = "The struck charter lost its exact authority before publication.";
				Failure = charter.Fault;
				return false;
			}
			CallbackWitness callback = CaptureCallbackWitness(frame);
			if (callback == null)
			{
				KingdomTradeRules.QuarantineBook(book, "Charter commit frame could not be frozen.");
				Failure = book.SchemaFault;
				return false;
			}
			KingdomGovernanceScope.Commit("strike trade charter");
			if (!ExactCallbackWitness(frame, callback)
				|| !ExactAuthority(frame, KingdomTradePhase.Invalid)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				KingdomTradeRules.QuarantineBook(System.TradeBook,
					"The charter commit callback changed its exact authority.");
				Failure = "The charter commit changed its authority and was quarantined.";
				return false;
			}
			string eventId = charter.Id + ":struck";
			callback = CaptureCallbackWitness(frame);
			if (callback == null)
			{
				KingdomTradeRules.QuarantineBook(book, "Charter chronicle frame could not be frozen.");
				Failure = book.SchemaFault;
				return false;
			}
			bool recorded = KingdomChronicle.RecordOnce(System, eventId,
				KingdomPresentation.Rich(System.KingdomDisplayName) + " struck "
				+ XRL.Language.Grammar.A(KingdomRules.StripParenthetical(deal.DisplayName))
				+ " with " + Faction.GetFormattedName(FactionName), Accomplishment: true);
			if (!recorded || !ExactCallbackWitness(frame, callback)
				|| !ExactAuthority(frame, KingdomTradePhase.Invalid)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				charter.Quarantined = true;
				charter.Fault = AppendFault(charter.Fault,
					"The charter chronicle callback was lost or changed exact authority.");
				Failure = charter.Fault;
				return false;
			}
			callback = CaptureCallbackWitness(frame);
			if (callback == null)
			{
				KingdomTradeRules.QuarantineBook(book, "Charter message frame could not be frozen.");
				Failure = book.SchemaFault;
				return false;
			}
			MessageQueue.AddPlayerMessage("{{G|The charter is struck. Caravans of "
				+ Faction.GetFormattedName(FactionName) + " will come.}}");
			if (!ExactCallbackWitness(frame, callback)
				|| !ExactAuthority(frame, KingdomTradePhase.Invalid)
				|| !ExactCharter(frame, charter, charterId, DealKey, FactionName, now,
					charterNext))
			{
				KingdomTradeRules.QuarantineBook(System.TradeBook,
					"The charter message callback changed its exact authority.");
				Failure = "The charter telling changed its authority and was quarantined.";
				return false;
			}
			KingdomLog.Log("trade: struck id=" + charter.Id + " next=" + charter.NextTick);
			return true;
		}

		private static bool ExactCharter(TradeLiveFrame Frame,
			KingdomTradeCharter Charter, string Id, string Deal, string Faction, long Created,
			long Next)
		{
			if (Frame == null || Charter == null || Frame.Charters == null
				|| Charter.Quarantined || !string.Equals(Charter.Id, Id, StringComparison.Ordinal)
				|| !string.Equals(Charter.DealKey, Deal, StringComparison.Ordinal)
				|| !string.Equals(Charter.Faction, Faction, StringComparison.Ordinal)
				|| Charter.CreatedTick != Created || Charter.NextTick != Next) return false;
			int identity = 0;
			int pair = 0;
			for (int i = 0; i < Frame.Charters.Count; i++)
			{
				KingdomTradeCharter row = Frame.Charters[i];
				if (ReferenceEquals(row, Charter)) identity++;
				if (row != null && string.Equals(row.Id, Id, StringComparison.Ordinal))
					identity += ReferenceEquals(row, Charter) ? 0 : 1;
				if (row != null && string.Equals(row.DealKey, Deal, StringComparison.Ordinal)
					&& string.Equals(row.Faction, Faction, StringComparison.Ordinal)) pair++;
			}
			return identity == 1 && pair == 1;
		}

		public static void OnZoneActivated(KingdomSystem System, Zone Z,
			KingdomSurvey Shared = null)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
			TradeLease lease;
			if (!TryEnter(System, out lease)) return;
			using (lease)
			{
				OnZoneActivatedCore(System, Z, Shared);
			}
		}

		private static void OnZoneActivatedCore(KingdomSystem System, Zone Z,
			KingdomSurvey Shared)
		{
			if (System == null || !System.Founded || Z == null
				|| !System.ClaimedZones.Contains(Z.ZoneID)) return;
			long now = The.Game.TimeTicks;
			KingdomTradeBook book = EnsureBook(System);
			if (book == null) return;
			KingdomTradeOptionAction option = ApplyOption(book, Enabled, now);
			if (!KingdomTradeRules.BookUsable(book)) return;
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z, System);

			if (book.OpenOperation != null)
			{
				if (!Enabled && book.OpenOperation.Phase == KingdomTradePhase.Prepared)
					return;
				ContinueOperation(System, book, Z, survey, now);
				if (book.OpenOperation != null || !Enabled) return;
			}
			if (!Enabled) return;
			if (book.RestampPending)
			{
				RestampTradeClocks(book, now);
				return;
			}
			if (option == KingdomTradeOptionAction.EnableAndRestamp) return;

			KingdomTradeManifestState manifest = book.Manifest;
			if (manifest != null && manifest.Status == KingdomTradeManifestStatus.InFlight)
			{
				if (KingdomManifestRules.ManifestExpired(now, manifest.DeadlineTick))
				{
					PrepareManifestClockOperation(System, book, manifest, Z, now);
					ContinueOperation(System, book, Z, survey, now);
					return;
				}
				string seatId = System.City?.SettlementId;
				if (string.Equals(manifest.DestinationId, seatId, StringComparison.Ordinal)
					&& string.Equals(manifest.DestinationName, System.SeatName,
						StringComparison.Ordinal))
				{
					PrepareManifestDelivery(System, book, manifest, Z, now);
					ContinueOperation(System, book, Z, survey, now);
					return;
				}
			}

			int due = KingdomTradeRules.DueCharterIndex(book, now);
			if (due >= 0 && PrepareCharterDelivery(System, book, book.Charters[due], Z,
				survey, now))
			{
				ContinueOperation(System, book, Z, survey, now);
			}
		}

		/// <summary>Publishes the exact route and debit intent before touching any source vessel.</summary>
		public static bool TryLoadManifest(KingdomSystem System, Zone Z, int Amount,
			string OriginName, string DestinationName, out string Failure)
		{
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Failure = "Settlement simulation is paused; no new manifest was loaded.";
				return false;
			}
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Another trade callback is already in flight; no manifest was changed.";
				return false;
			}
			using (lease)
			{
				return TryLoadManifestCore(System, Z, Amount, OriginName, DestinationName,
					out Failure);
			}
		}

		private static bool TryLoadManifestCore(KingdomSystem System, Zone Z, int Amount,
			string OriginName, string DestinationName, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded || Z == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "Manifests are loaded standing on the kingdom's own ground.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Trade is disabled. No new manifest is loaded.";
				return false;
			}
			if (Amount <= 0 || Amount > KingdomManifestRules.MaximumManifestDrams
				|| string.IsNullOrEmpty(OriginName) || string.IsNullOrEmpty(DestinationName))
			{
				Failure = "The manifest amount or route cannot be recorded exactly.";
				return false;
			}
			long now = The.Game.TimeTicks;
			KingdomTradeBook book = EnsureBook(System);
			if (book == null)
			{
				Failure = "The trade book uses an unknown or quarantined schema.";
				return false;
			}
			if (book.OpenOperation != null)
			{
				Failure = "Another trade receipt is still being reconciled.";
				return false;
			}
			ApplyOption(book, true, now);
			if (!KingdomTradeRules.BookUsable(book))
			{
				Failure = book.SchemaFault ?? "Trade option evidence is not authoritative.";
				return false;
			}
			if (book.Manifest != null)
			{
				Failure = "Another manifest is already on the road or held for inspection.";
				return false;
			}
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(book,
				KingdomTradeOperationKind.ManifestLoad, now);
			if (operation == null)
			{
				Failure = "The trade ledger cannot open another durable receipt.";
				return false;
			}
			if (!BindOperationSettlement(System, book, operation, Z))
			{
				Quarantine(operation, "The manifest could not bind its exact settlement and zone.");
				FinalizeQuarantine(System, book, operation, now, null);
				Failure = operation.Fault;
				return false;
			}
			operation.ManifestId = KingdomTradeRules.ManifestId(operation.Id);
			if (System.City == null || System.Away?.City == null
				|| !string.Equals(OriginName, System.SeatName, StringComparison.Ordinal)
				|| !string.Equals(DestinationName, System.Away.SettlementName,
					StringComparison.Ordinal)
				|| !KingdomTradeRules.IdentityContainsSettlement(book, System.City.SettlementId)
				|| !KingdomTradeRules.IdentityContainsSettlement(book, System.Away.City.SettlementId))
			{
				Quarantine(operation,
					"The manifest route could not bind both exact city identities.");
				FinalizeQuarantine(System, book, operation, now, null);
				Failure = operation.Fault;
				return false;
			}
			operation.OriginId = System.City.SettlementId;
			operation.OriginName = OriginName;
			operation.DestinationId = System.Away.City.SettlementId;
			operation.DestinationName = DestinationName;
			operation.ManifestLoadedTick = now;
			operation.ManifestDeadlineTick = KingdomTradeRules.SaturatingAdd(now,
				KingdomManifestRules.ManifestWindowTicks);
			operation.WaterDirection = KingdomTradeWaterDirection.Debit;
			operation.RequestedWater = Amount;
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			ContinueOperation(System, book, Z, survey, now);
			KingdomTradeManifestState manifest = book.Manifest;
			bool success = manifest != null
				&& string.Equals(manifest.Id, operation.ManifestId, StringComparison.Ordinal)
				&& manifest.Status == KingdomTradeManifestStatus.InFlight
				&& manifest.EscrowDrams == Amount;
			if (!success)
			{
				Failure = operation.Fault ?? "The exact source-vessel debit could not be proved; it was not retried.";
			}
			return success;
		}

		/// <summary>Compatibility facade for the Charter. A lapsed load is retained, not destroyed.</summary>
		public static KingdomManifest ExpireManifestIfStale(KingdomSystem System,
			Zone Here, long Now)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return null;
			TradeLease lease;
			if (!TryEnter(System, out lease)) return null;
			using (lease)
			{
				return ExpireManifestIfStaleCore(System, Here, Now);
			}
		}

		private static KingdomManifest ExpireManifestIfStaleCore(KingdomSystem System,
			Zone Here, long Now)
		{
			KingdomTradeBook book = EnsureBook(System);
			KingdomTradeManifestState manifest = book?.Manifest;
			if (manifest == null || manifest.Status != KingdomTradeManifestStatus.InFlight
				|| !KingdomManifestRules.ManifestExpired(Now, manifest.DeadlineTick)) return null;
			if (book.OpenOperation != null) return null;
			bool lapse = manifest.TurnedBack;
			KingdomManifest answer = lapse ? LegacyManifestSnapshot(manifest) : null;
			PrepareManifestClockOperation(System, book, manifest, Here, Now);
			ContinueOperation(System, book, Here, Here == null ? null : KingdomSurvey.Take(Here, System), Now);
			return answer;
		}

		/// <summary>
		/// Atomically replaces live Trade authority with a durable exact exile receipt. Core must
		/// call this before changing realm identity, settlement topology, legacy rows, or chronicles.
		/// False leaves the current TradeBook graph and bytes untouched.
		/// </summary>
		public static bool TryOnExile(KingdomSystem System, long Now, string ExactRealmId,
			List<string> ExactSettlementIds, out long SettledTick, out string Failure)
		{
			SettledTick = -1L;
			Failure = null;
			TradeLease lease;
			if (!TryEnter(System, out lease))
			{
				Failure = "Trade exile deferred because another synchronous trade lease is active.";
				return false;
			}
			using (lease)
			{
				if (System == null || System.TradeBook == null || Now < 0L)
				{
					Failure = "Trade exile requires a live system, book, and nonnegative tick.";
					return false;
				}
				KingdomTradeBook original = System.TradeBook;
				byte[] before;
				try { before = KingdomTradeCodec.EncodePayload(original); }
				catch
				{
					Failure = "Trade exile could not freeze the current bounded authority graph.";
					return false;
				}
				List<string> exact = new List<string>();
				if (ExactSettlementIds == null || ExactSettlementIds.Count < 1
					|| ExactSettlementIds.Count > KingdomTradeRules.MaxSettlementIds)
				{
					Failure = "Trade exile requires complete exact settlement topology within product cap.";
					return false;
				}
				for (int i = 0; i < ExactSettlementIds.Count; i++)
				{
					string id = ExactSettlementIds[i];
					if (!KingdomTradeRules.ValidId(id) || exact.Contains(id))
					{
						Failure = "Trade exile requires distinct exact settlement ids.";
						return false;
					}
					exact.Add(id);
				}
				exact.Sort(StringComparer.Ordinal);
				List<string> liveTopology;
				string topologyFailure;
				if (!System.TryRetainedSettlementIds(true, false,
					out liveTopology, out topologyFailure)
					|| !ExactSettlementTopology(liveTopology, exact))
				{
					Failure = "Trade exile could not reprove the complete exact settlement topology: "
						+ (topologyFailure ?? "topology differs");
					return false;
				}
				string currentRealm = System.CurrentRealmId;
				string currentSettlement = System.CurrentSettlementId;
				if (!string.Equals(currentRealm, ExactRealmId, StringComparison.Ordinal)
					|| !KingdomTradeRules.ValidId(currentSettlement) || !exact.Contains(currentSettlement))
				{
					Failure = "Trade exile could not prove the exact current realm and seated city topology.";
					return false;
				}
				KingdomTradeAuthoritySeal seal = KingdomTradeRules.CaptureAuthoritySeal(original,
					System.ClaimedZones, System.City?.ZoneIds);
				if (seal == null)
				{
					Failure = "Trade exile could not seal exact Core and Trade authority.";
					return false;
				}
				if (!TryCaptureExileCoreSeal(System, out TradeExileCoreSeal coreSeal,
					out string coreFailure))
				{
					Failure = "Trade exile could not freeze the complete Core topology: "
						+ (coreFailure ?? "unknown Core graph failure");
					return false;
				}
				KingdomTradeBook replacement;
				long closedTick;
				if (!KingdomTradeRules.TryPrepareExile(original, Now, ExactRealmId, exact,
					out replacement, out closedTick, out Failure)) return false;
				byte[] after;
				try { after = KingdomTradeCodec.EncodePayload(original); }
				catch
				{
					Failure = "Trade exile authority became unencodable during preflight.";
					return false;
				}
				List<string> finalTopology;
				if (!System.TryRetainedSettlementIds(true, false,
					out finalTopology, out topologyFailure)
					|| !ExactSettlementTopology(finalTopology, exact)
					|| !ReferenceEquals(System.TradeBook, original) || !ExactBytes(before, after)
					|| !string.Equals(System.CurrentRealmId, currentRealm, StringComparison.Ordinal)
					|| !string.Equals(System.CurrentSettlementId, currentSettlement,
						StringComparison.Ordinal)
					|| !ExactExileCoreSeal(System, coreSeal)
					|| !KingdomTradeRules.ExactAuthoritySeal(original,
						System.ClaimedZones, System.City?.ZoneIds, seal))
				{
					Failure = "Trade exile exact authority or topology changed during preflight.";
					return false;
				}
				if (!ReferenceEquals(replacement, original)) System.TradeBook = replacement;
				SettledTick = closedTick;
				return true;
			}
		}

		private static KingdomTradeBook EnsureBook(KingdomSystem System)
		{
			if (System == null) return null;
			if (System.TradeBook == null) System.TradeBook = new KingdomTradeBook();
			KingdomTradeBook book = System.TradeBook;
			KingdomTradeRules.Normalize(book);
			return KingdomTradeRules.BookUsable(book) ? book : null;
		}

		private static KingdomTradeOptionAction ApplyOption(KingdomTradeBook Book,
			bool IsEnabled, long Now)
		{
			KingdomTradeOptionAction action = KingdomTradeRules.ObserveOption(
				Book.OptionState, IsEnabled);
			KingdomTradeOptionState next = IsEnabled ? KingdomTradeOptionState.Enabled
				: KingdomTradeOptionState.Disabled;
			if (Book.OptionState != next)
			{
				if (Book.OptionEpoch == long.MaxValue)
				{
					KingdomTradeRules.QuarantineBook(Book, "trade option epoch overflow");
					return KingdomTradeOptionAction.None;
				}
				Book.OptionEpoch++;
			}
			Book.OptionState = next;
			Book.OptionObservedTick = Now < 0L ? 0L : Now;
			if (action == KingdomTradeOptionAction.EnableAndRestamp)
				Book.RestampPending = true;
			return action;
		}

		private static void RestampTradeClocks(KingdomTradeBook Book, long Now)
		{
			if (Book == null) return;
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter charter = Book.Charters[i];
				if (charter == null || charter.Quarantined) continue;
				if (!KingdomData.TryGetDeal(charter.DealKey,
					out KingdomRules.DealEntry deal) || deal.IntervalTicks <= 0L)
				{
					charter.Quarantined = true;
					charter.Fault = "Charter content no longer resolves during enable restamp.";
					continue;
				}
				charter.NextTick = KingdomTradeRules.SaturatingAdd(Now, deal.IntervalTicks);
			}
			if (Book.Manifest != null
				&& Book.Manifest.Status == KingdomTradeManifestStatus.InFlight)
			{
				Book.Manifest.LoadedTick = Now;
				Book.Manifest.DeadlineTick = KingdomTradeRules.SaturatingAdd(Now,
					KingdomManifestRules.ManifestWindowTicks);
			}
			Book.RestampPending = false;
		}

		private static bool PrepareCharterDelivery(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeCharter Charter, Zone Z,
			KingdomSurvey Survey, long Now)
		{
			if (Charter == null || Charter.Quarantined || Book.OpenOperation != null) return false;
			if (!KingdomData.TryGetDeal(Charter.DealKey, out KingdomRules.DealEntry deal)
				|| deal.IntervalTicks <= 0L || deal.IncomeDrams < 0)
			{
				Charter.Quarantined = true;
				Charter.Fault = "Charter content cannot be frozen for delivery.";
				return false;
			}
			int cycles = KingdomRules.BankedCycles(Now, Charter.NextTick, deal.IntervalTicks);
			if (cycles <= 0) return false;
			int goodsHouseholds = KingdomYardGoods.ExactStandingHouseholds(Survey);
			int incomePerCycle = KingdomYardGoodsRules.IncomePerCycle(
				deal.IncomeDrams, goodsHouseholds);
			int water = KingdomTradeRules.SaturatingMultiply(incomePerCycle, cycles);
			if (water > KingdomTradeRules.MaxOperationWater) return false;
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				KingdomTradeOperationKind.CharterDelivery, Now);
			if (operation == null) return false;
			if (!BindOperationSettlement(System, Book, operation, Z))
			{
				Quarantine(operation, "The charter delivery could not bind its exact settlement and zone.");
				return true;
			}
			operation.CharterId = Charter.Id;
			operation.DealKey = Charter.DealKey;
			operation.DealDisplayName = deal.DisplayName;
			operation.Faction = Charter.Faction;
			operation.Cycles = cycles;
			// Frozen adjusted income is durable authority. Later release, registry merge, or reload
			// cannot change this already-open exchange or apply its household goods twice.
			operation.IncomePerCycle = incomePerCycle;
			operation.IntervalTicks = deal.IntervalTicks;
			operation.DueBefore = Charter.NextTick;
			operation.DueAfter = KingdomTradeRules.SaturatingAdd(Now, deal.IntervalTicks);
			operation.CaravanBlueprint = deal.CaravanBlueprint;
			operation.WaterDirection = KingdomTradeWaterDirection.Credit;
			operation.RequestedWater = water;
			operation.ProjectionId = KingdomTradeRules.ProjectionId(operation.Id);
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow collision = Book.Projections[i];
				if (collision == null || !string.Equals(collision.ProjectionId,
					operation.ProjectionId, StringComparison.Ordinal)) continue;
				collision.Quarantined = true;
				collision.Fault = AppendFault(collision.Fault,
					"new projection identity collided with preserved authority");
				Quarantine(operation, "Projection identity collided before physical mutation.");
				return true;
			}
			KingdomTradeProjectionRow prior;
			if (!TryProjectionRow(Book, operation.SettlementId, out prior))
			{
				Quarantine(operation,
					"Per-city caravan projection authority collided before delivery.");
				return true;
			}
			if (prior != null)
			{
				operation.PriorProjectionId = prior.ProjectionId;
				operation.PriorProjectionObjectId = prior.ObjectId;
				operation.PriorProjectionZoneId = prior.ZoneId;
			}
			FreezeMaterials(operation, KingdomMaterials.DealMaterialsFor(deal.Key).Scaled(cycles * 100));
			int before = System.GetStanding(Charter.Faction);
			long after = (long)before + KingdomRules.DealTrickleStanding;
			if (after < int.MinValue || after > int.MaxValue)
			{
				Quarantine(operation, "Standing CAS would overflow.");
				return true;
			}
			operation.Standing = new KingdomTradeStandingCas
			{
				Faction = Charter.Faction,
				Before = before,
				Delta = KingdomRules.DealTrickleStanding,
				After = (int)after,
				State = KingdomTradePhysicalState.Prepared
			};
			operation.Pattern = KingdomCeremony.FreezePatternBook(System,
				operation.SettlementId, operation.Sequence);
			if (!KingdomTradePatternRules.Valid(operation.Pattern))
			{
				Quarantine(operation,
					"The charter's pattern-book offer could not be frozen within its bounds.");
			}
			return true;
		}

		private static bool TryProjectionRow(KingdomTradeBook Book, string SettlementId,
			out KingdomTradeProjectionRow Projection)
		{
			Projection = null;
			if (Book?.Projections == null || !KingdomTradeRules.ValidId(SettlementId))
				return false;
			for (int i = 0; i < Book.Projections.Count; i++)
			{
				KingdomTradeProjectionRow row = Book.Projections[i];
				if (row == null) return false;
				if (!string.Equals(row.SettlementId, SettlementId, StringComparison.Ordinal)) continue;
				if (row.Quarantined) return false;
				if (Projection != null) return false;
				Projection = row;
			}
			return true;
		}

		private static void PrepareManifestDelivery(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeManifestState Manifest, Zone Z, long Now)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				KingdomTradeOperationKind.ManifestDelivery, Now);
			if (operation == null) return;
			if (!BindOperationSettlement(System, Book, operation, Z))
			{
				Quarantine(operation, "The manifest arrival could not bind its exact settlement and zone.");
				return;
			}
			CopyManifest(operation, Manifest);
			operation.WaterDirection = KingdomTradeWaterDirection.Credit;
			operation.RequestedWater = Manifest.EscrowDrams;
			operation.ManifestEscrowBefore = Manifest.EscrowDrams;
			operation.ManifestEscrowDebit = 0;
			operation.ManifestEscrowAfter = Manifest.EscrowDrams;
			operation.ManifestEscrowState = KingdomTradePhysicalState.Prepared;
		}

		private static void PrepareManifestClockOperation(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeManifestState Manifest, Zone Z, long Now)
		{
			KingdomTradeOperation operation = KingdomTradeRules.NewOperation(Book,
				Manifest.TurnedBack ? KingdomTradeOperationKind.ManifestLapse
					: KingdomTradeOperationKind.ManifestTurnback, Now);
			if (operation == null) return;
			if (!BindOperationSettlement(System, Book, operation, Z))
			{
				Quarantine(operation, "The manifest clock could not bind its exact settlement and zone.");
				return;
			}
			CopyManifest(operation, Manifest);
			if (operation.Kind == KingdomTradeOperationKind.ManifestLapse)
			{
				operation.RetainedBefore = Book.RetainedEscrowDrams;
				operation.RetainedDelta = Manifest.EscrowDrams;
				operation.RetainedAfter = KingdomTradeRules.SaturatingAdd(
					operation.RetainedBefore, operation.RetainedDelta);
				operation.RetainedState = KingdomTradePhysicalState.Prepared;
			}
			operation.ManifestLoadedTick = Now;
			operation.ManifestDeadlineTick = KingdomTradeRules.SaturatingAdd(Now,
				KingdomManifestRules.ManifestWindowTicks);
		}

		private static void CopyManifest(KingdomTradeOperation Operation,
			KingdomTradeManifestState Manifest)
		{
			Operation.ManifestId = Manifest.Id;
			Operation.OriginId = Manifest.OriginId;
			Operation.OriginName = Manifest.OriginName;
			Operation.DestinationId = Manifest.DestinationId;
			Operation.DestinationName = Manifest.DestinationName;
			Operation.ManifestLoadedTick = Manifest.LoadedTick;
			Operation.ManifestDeadlineTick = Manifest.DeadlineTick;
			Operation.RequestedWater = Manifest.EscrowDrams;
		}

		private static void ContinueOperation(KingdomSystem System, KingdomTradeBook Book,
			Zone Z, KingdomSurvey Survey, long Now)
		{
			KingdomTradeOperation operation = Book.OpenOperation;
			if (operation == null) return;
			if (!TryBindTopologyGround(System, Z, Survey))
			{
				KingdomLog.Log("trade: open receipt " + (operation.Id ?? "?")
					+ " deferred; exact active settlement ground is unavailable");
				return;
			}
			TradeLiveFrame frame;
			if (!TryBindFrame(System, Book, operation, Z, out frame))
			{
				if (!ReferenceEquals(System.TradeBook, Book)
					|| !ReferenceEquals(Book.OpenOperation, operation))
				{
					FailDetachedAuthority(new TradeLiveFrame
					{
						System = System, Book = Book, Operation = operation,
						Charters = Book.Charters, RealmId = Book.RealmId, Zone = Z
					}, "A trade callback detached the official operation before resume.");
				}
				else
				{
					KingdomLog.Log("trade: open receipt " + (operation.Id ?? "?")
						+ " remains bound to " + (operation.SettlementName ?? "?")
						+ "/" + (operation.ZoneId ?? "?") + "; refused resume here");
				}
				return;
			}
			if (operation.Phase == KingdomTradePhase.Quarantined)
			{
				FinalizeQuarantine(System, Book, operation, Now, frame);
				return;
			}
			if (operation.Phase >= KingdomTradePhase.ResourceSettled
				&& (!TryBindPersistedPhysicalFrame(frame, operation, Z, Survey)
					|| !TryBindProjectionFrame(frame, operation, Z)))
			{
				ReconcilePhysicalFailure(frame, operation, Z,
					"A resumed trade receipt could not bind its exact physical frame.");
				FinalizeQuarantine(System, Book, operation, Now, frame);
				return;
			}
			if (operation.Phase == KingdomTradePhase.Prepared
				|| operation.Phase == KingdomTradePhase.ResourceIntent)
			{
				if (!SettleResources(operation, Z, Survey, frame))
				{
					if (operation.Phase == KingdomTradePhase.Quarantined)
						FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
			}
			if (operation.Phase == KingdomTradePhase.ResourceSettled
				|| operation.Phase == KingdomTradePhase.ProjectionIntent)
			{
				SettleProjection(operation, Z, frame);
				if (operation.Phase == KingdomTradePhase.Quarantined)
				{
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				if (operation.Phase == KingdomTradePhase.ProjectionIntent) return;
			}
			if (operation.Phase == KingdomTradePhase.ProjectionSettled
				|| operation.Phase == KingdomTradePhase.DomainIntent)
			{
				if (!SettleDomain(System, Book, operation, frame))
				{
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
			}
			if (operation.Phase == KingdomTradePhase.DomainSettled)
			{
				BuildOutbox(System, operation);
				if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& !KingdomTradeRules.CharterOutboxReadyForDispatch(operation))
				{
					Quarantine(operation,
						"The mandatory Charter outbox was malformed before external dispatch.");
					return;
				}
				operation.Phase = KingdomTradePhase.Sinks;
			}
			if (operation.Phase == KingdomTradePhase.Sinks)
			{
				if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& !KingdomTradeRules.CharterOutboxReadyForDispatch(operation))
				{
					Quarantine(operation,
						"The mandatory Charter outbox changed before external dispatch.");
					return;
				}
				if (!DispatchOutbox(System, operation, frame))
				{
					if (operation.Phase == KingdomTradePhase.Quarantined)
						FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				if (!OutboxSettled(operation.Outbox)) return;
				operation.Phase = KingdomTradePhase.ScheduleIntent;
			}
			if (operation.Phase == KingdomTradePhase.ScheduleIntent)
			{
				if (operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& !ContinuePatternBook(System, operation, frame))
				{
					if (operation.Phase == KingdomTradePhase.Quarantined)
						FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				if (!ExactPhysicalFrame(frame, operation, Z))
				{
					ReconcilePhysicalFailure(frame, operation, Z,
						"The final physical checkpoint no longer matched its exact witnesses.");
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				RefreshSurveyWater(frame.Physical);
				if (!SettleSchedule(Book, operation, frame))
				{
					FinalizeQuarantine(System, Book, operation, Now, frame);
					return;
				}
				KingdomTradePhase disposition = string.IsNullOrEmpty(operation.Fault)
					? KingdomTradePhase.Terminal : KingdomTradePhase.Quarantined;
				operation.Phase = KingdomTradePhase.RetirementReady;
				KingdomTradeRules.Retire(Book, operation, disposition, Now, operation.Fault);
				System.SynchronizeLegacyManifestProjection();
			}
		}

		private static bool SettleResources(KingdomTradeOperation Operation,
			Zone Z, KingdomSurvey Survey, TradeLiveFrame Frame)
		{
			if (Operation.Kind == KingdomTradeOperationKind.ManifestTurnback
				|| Operation.Kind == KingdomTradeOperationKind.ManifestLapse)
			{
				Operation.Phase = KingdomTradePhase.ResourceSettled;
				return true;
			}
			if (Operation.Phase == KingdomTradePhase.ResourceIntent)
			{
				if (!TryBindPersistedPhysicalFrame(Frame, Operation, Z, Survey))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"The persisted resource frame could not bind its exact live owners and parts.");
					return false;
				}
				if (!ResumePreparedWater(Operation, Z, Frame)) return false;
				if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
					&& Operation.MaterialRequested > 0)
				{
					if (Operation.MaterialOutputs.Count == 0)
					{
						// Water was proved before the material sub-lane published an Add intent.
						// Starting that still-unstarted lane is safe; no output ID existed to replay.
						if (!ApplyMaterials(Operation, Z, Frame)) return false;
					}
					else if (!ReconcileMaterialOutputs(Operation, Z, Frame)) return false;
				}
				Operation.Phase = KingdomTradePhase.ResourceSettled;
				return true;
			}
			if (Survey == null || Z == null || !string.Equals(Operation.ZoneId,
				Z.ZoneID, StringComparison.Ordinal))
			{
				Quarantine(Operation, "The prepared resource zone is not loaded exactly.");
				return false;
			}
			if (!ApplyWater(Operation, Z, Survey, Frame)) return false;
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& Operation.MaterialRequested > 0)
			{
				if (!ApplyMaterials(Operation, Z, Frame)) return false;
			}
			Operation.Phase = KingdomTradePhase.ResourceSettled;
			return true;
		}

		private static bool ApplyWater(KingdomTradeOperation Operation, Zone Z,
			KingdomSurvey Survey, TradeLiveFrame Frame)
		{
			if (Operation.RequestedWater <= 0) return true;
			if (Frame == null || Survey == null || Survey.Stores == null
				|| Operation.WaterLegs.Count != 0) return false;
			TradePhysicalFrame physical = new TradePhysicalFrame
			{
				Survey = Survey,
				StoreList = Survey.Stores,
				StoreRows = Survey.Stores.ToArray()
			};
			Frame.Physical = physical;
			int planned = 0;
			for (int i = 0; i < Survey.Stores.Count && planned < Operation.RequestedWater
				&& physical.Water.Count < KingdomTradeRules.MaxWaterLegs; i++)
			{
				LiquidVolume vessel = Survey.Stores[i];
				GameObject owner = vessel?.ParentObject;
				bool duplicate = false;
				for (int j = 0; j < physical.Water.Count; j++)
					if (ReferenceEquals(physical.Water[j].Vessel, vessel)) duplicate = true;
				if (duplicate || !ExactDedicated(owner, vessel, Z)
					|| vessel.ComponentLiquids == null) continue;
				int available;
				if (Operation.WaterDirection == KingdomTradeWaterDirection.Debit)
				{
					if (!KingdomLiquids.HasFreshWater(vessel)) continue;
					available = vessel.Volume;
				}
				else
				{
					if (!KingdomLiquids.CanReceiveFreshWater(vessel)) continue;
					available = vessel.MaxVolume - vessel.Volume;
				}
				if (available <= 0) continue;
				int delta = Math.Min(available, Operation.RequestedWater - planned);
				int after = Operation.WaterDirection == KingdomTradeWaterDirection.Debit
					? vessel.Volume - delta : vessel.Volume + delta;
				KingdomTradeWaterLeg leg = new KingdomTradeWaterLeg
				{
					OwnerId = owner.ID,
					ZoneId = Z.ZoneID,
					Capacity = vessel.MaxVolume,
					Before = vessel.Volume,
					Delta = delta,
					After = after,
					BeforeComposition = ComponentFingerprint(vessel),
					AfterComposition = after == 0 ? "empty" : "water=1000",
					State = KingdomTradePhysicalState.Prepared
				};
					Operation.WaterLegs.Add(leg);
					GameObject resolvedOwner;
					LoadedTopologyWitness ownerTopology;
					if (ResolveLoadedObject(owner.ID, Z, out resolvedOwner, out ownerTopology)
						!= LoadedObjectResolution.ExactUnique
						|| !ReferenceEquals(resolvedOwner, owner))
					{
						Quarantine(Operation, "A source vessel owner id was not exact-unique on active settlement ground.");
						return false;
					}
					WaterWitness witness = CaptureWaterWitness(leg, owner, vessel);
				if (witness == null)
				{
					Quarantine(Operation,
						"A source vessel could not be frozen exactly before intent.");
						return false;
					}
					witness.Topology = ownerTopology;
				physical.Water.Add(witness);
				planned += delta;
			}
			RefreshReceiptRows(Frame);
			if (Operation.WaterDirection == KingdomTradeWaterDirection.Debit
				&& planned != Operation.RequestedWater)
			{
				Quarantine(Operation, "The exact source vessels cannot cover the published manifest.");
				return false;
			}
			if (planned == 0) return true;
			Operation.Phase = KingdomTradePhase.ResourceIntent;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				KingdomTradeWaterLeg leg = witness.Leg;
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A water frame changed before its exact mutation.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				LoadedTopologyWitness createTopology = CaptureLoadedTopology();
				if (callback == null || createTopology == null
					|| !ExactLoadedTopology(createTopology))
				{
					leg.State = KingdomTradePhysicalState.Prepared;
					Quarantine(Operation, "A water callback frame could not be frozen before mutation.");
					return false;
				}
				int changed;
				try
				{
					changed = Operation.WaterDirection == KingdomTradeWaterDirection.Debit
						? KingdomLiquids.Drain(witness.Vessel, witness.Delta)
						: KingdomLiquids.Fill(witness.Vessel, "water", witness.Delta);
				}
				finally
				{
					// Liquid callbacks may commit before throwing. Reclassify the exact owner
					// while this attended survey remains the later-pass authority.
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Owner);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent))
				{
					return FailDetachedAuthority(Frame,
						"A water callback detached or rewrote its official trade authority.");
				}
				if (leg.State != KingdomTradePhysicalState.Intent
					|| changed != witness.Delta || !ExactPhysicalWithWaterOverride(Frame,
						Operation, Z, witness, true))
				{
					leg.State = KingdomTradePhysicalState.Lost;
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A water callback changed an exact owner, part, dedication, capacity, composition, or intended delta.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Proved;
				Operation.ProvedWater = KingdomTradeRules.SaturatingAdd(
					Operation.ProvedWater, witness.Delta);
			}
			Operation.AmbiguousWater = 0;
			return true;
		}

		private static void RefreshSurveyWater(TradePhysicalFrame Physical)
		{
			if (Physical == null || Physical.Survey == null || Physical.StoreList == null) return;
			int stored = 0;
			int room = 0;
			HashSet<GameObject> owners = new HashSet<GameObject>();
			for (int i = 0; i < Physical.StoreList.Count; i++)
			{
				LiquidVolume vessel = Physical.StoreList[i];
				if (vessel == null) continue;
				if (GameObject.Validate(vessel.ParentObject)) owners.Add(vessel.ParentObject);
				if (KingdomLiquids.HasFreshWater(vessel))
					stored = KingdomTradeRules.SaturatingAdd(stored, vessel.Volume);
				if (KingdomLiquids.CanReceiveFreshWater(vessel) && vessel.MaxVolume >= vessel.Volume)
					room = KingdomTradeRules.SaturatingAdd(room, vessel.MaxVolume - vessel.Volume);
			}
			Physical.Survey.StoredWater = stored;
			Physical.Survey.StorageSpace = room;
			// Aggregate was published from the frozen store list; align every cached row
			// without applying those same deltas twice.
			foreach (GameObject owner in owners)
				Physical.Survey.SynchronizeReceiptObject(owner);
		}

		private static bool ExactWaterWitness(WaterWitness Witness, Zone Z, bool After)
		{
			if (!ExactWaterReceipt(Witness) || !ExactLoadedTopology(Witness.Topology)
				|| Witness.Dictionary == null
				|| !ExactDedicated(Witness.Owner, Witness.Vessel, Z)
				|| Witness.Owner.CurrentCell != Witness.Cell || Witness.Cell == null
				|| Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Vessel.ComponentLiquids, Witness.Dictionary)
				|| !string.Equals(Witness.Owner.ID, Witness.OwnerId,
					StringComparison.Ordinal)
				|| !string.Equals(Z.ZoneID, Witness.ZoneId, StringComparison.Ordinal)
				|| Witness.Vessel.MaxVolume != Witness.Capacity) return false;
			if (After)
				return Witness.Vessel.Volume == Witness.After
					&& string.Equals(ComponentFingerprint(Witness.Vessel),
						Witness.AfterComposition, StringComparison.Ordinal);
			return Witness.Vessel.Volume == Witness.Before
				&& string.Equals(ComponentFingerprint(Witness.Vessel),
					Witness.BeforeComposition, StringComparison.Ordinal)
				&& ComponentsExact(Witness.Dictionary, Witness.BeforeComponents);
		}

		private static bool ExactDedicated(GameObject Owner, LiquidVolume Vessel, Zone Z)
		{
			return GameObject.Validate(Owner) && Vessel != null && Z != null
				&& Owner.CurrentZone == Z && Vessel.ParentObject == Owner
				&& Owner.CurrentCell != null && Owner.CurrentCell.ParentZone == Z
				&& ReferenceEquals(Owner.GetPart<LiquidVolume>(), Vessel)
				&& Owner.GetIntProperty("KingdomStores") == 1
				&& Vessel.MaxVolume >= 0 && !string.IsNullOrEmpty(Owner.ID);
		}

		private static bool ExactWaterFrame(KingdomSurvey Survey,
			List<LiquidVolume> StoreList, LiquidVolume[] Rows)
		{
			if (Survey == null || !ReferenceEquals(Survey.Stores, StoreList)
				|| StoreList == null || Rows == null || StoreList.Count != Rows.Length) return false;
			for (int i = 0; i < Rows.Length; i++)
				if (!ReferenceEquals(StoreList[i], Rows[i])) return false;
			return true;
		}

		private static bool ComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count) return false;
			foreach (KeyValuePair<string, int> pair in Expected)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		private static string ComponentFingerprint(LiquidVolume Vessel)
		{
			if (Vessel == null || Vessel.Volume == 0) return "empty";
			if (Vessel.ComponentLiquids == null) return "missing";
			List<string> keys = new List<string>(Vessel.ComponentLiquids.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < keys.Count; i++)
			{
				if (i > 0) text.Append('|');
				text.Append(keys[i]).Append('=').Append(Vessel.ComponentLiquids[keys[i]]);
			}
			return text.ToString();
		}

		private static bool TryBindPersistedPhysicalFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, KingdomSurvey Survey)
		{
			if (Frame == null || Operation == null || Z == null) return false;
			if (Frame.Physical != null) return ExactPhysicalFrame(Frame, Operation, Z);
			TradePhysicalFrame physical = new TradePhysicalFrame();
			if (Operation.WaterLegs != null && Operation.WaterLegs.Count > 0)
			{
				if (Survey == null || Survey.Stores == null) return false;
				physical.Survey = Survey;
				physical.StoreList = Survey.Stores;
				physical.StoreRows = Survey.Stores.ToArray();
				for (int i = 0; i < Operation.WaterLegs.Count; i++)
				{
					KingdomTradeWaterLeg leg = Operation.WaterLegs[i];
					if (leg == null || (leg.State != KingdomTradePhysicalState.Prepared
							&& leg.State != KingdomTradePhysicalState.Proved)) return false;
					GameObject owner;
					LoadedTopologyWitness topology;
					if (ResolveLoadedObject(leg.OwnerId, Z, out owner, out topology)
						!= LoadedObjectResolution.ExactUnique) return false;
					LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
					if (!ExactDedicated(owner, vessel, Z) || vessel.ComponentLiquids == null
						|| !ContainsReference(physical.StoreRows, vessel)) return false;
					WaterWitness witness = CaptureWaterWitness(leg, owner, vessel);
					if (witness == null) return false;
					witness.Topology = topology;
					if (!ExactWaterWitness(witness, Z,
						leg.State == KingdomTradePhysicalState.Proved)) return false;
					physical.Water.Add(witness);
				}
			}
			Frame.Physical = physical;
			if (Operation.MaterialOutputs != null)
			{
				int proved = 0;
				for (int i = 0; i < Operation.MaterialOutputs.Count; i++)
				{
					KingdomTradeMaterialOutput output = Operation.MaterialOutputs[i];
					if (output == null) return false;
					if (output.State == KingdomTradePhysicalState.Proved
						|| output.State == KingdomTradePhysicalState.Intent)
					{
						GameObject destination;
						LoadedTopologyWitness destinationTopology;
						if (ResolveLoadedObject(output.DestinationOwnerId, Z, out destination,
							out destinationTopology) != LoadedObjectResolution.ExactUnique) return false;
						GameObject item;
						LoadedTopologyWitness itemTopology;
						if (ResolveLoadedObject(output.OutputId, Z, out item, out itemTopology)
							!= LoadedObjectResolution.ExactUnique
							|| !ExactLoadedTopology(destinationTopology)) return false;
						InventoryWitness inventory;
						if (!TryCaptureInventory(physical, destination, Z, out inventory)) return false;
						MaterialWitness witness = CaptureMaterialWitness(output, item,
							destination, inventory);
						if (witness == null) return false;
						witness.Topology = itemTopology;
						physical.Materials.Add(witness);
						if (!ExactMaterialWitness(witness, Z)
							|| CountMarker(Z, witness.Marker) != 1) return false;
						output.State = KingdomTradePhysicalState.Proved;
						proved = KingdomTradeRules.SaturatingAdd(proved, witness.Count);
					}
					else if (output.State == KingdomTradePhysicalState.Prepared
						|| output.State == KingdomTradePhysicalState.CreateIntent
						|| output.State == KingdomTradePhysicalState.CleanupIntent) return false;
				}
				Operation.MaterialProved = proved;
			}
			return ExactPhysicalFrame(Frame, Operation, Z);
		}

		private static bool ResumePreparedWater(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null) return Operation.WaterLegs == null
				|| Operation.WaterLegs.Count == 0;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				KingdomTradeWaterLeg leg = witness.Leg;
				if (leg.State == KingdomTradePhysicalState.Proved) continue;
				if (leg.State != KingdomTradePhysicalState.Prepared
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A resumed water frame changed before its exact mutation.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null)
				{
					leg.State = KingdomTradePhysicalState.Prepared;
					Quarantine(Operation, "A resumed water callback frame could not be frozen before mutation.");
					return false;
				}
				int changed;
				try
				{
					changed = Operation.WaterDirection == KingdomTradeWaterDirection.Debit
						? KingdomLiquids.Drain(witness.Vessel, witness.Delta)
						: KingdomLiquids.Fill(witness.Vessel, "water", witness.Delta);
				}
				finally
				{
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Owner);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent))
					return FailDetachedAuthority(Frame,
						"A resumed water callback detached its official trade authority.");
				if (leg.State != KingdomTradePhysicalState.Intent
					|| changed != witness.Delta || !ExactPhysicalWithWaterOverride(Frame,
						Operation, Z, witness, true))
				{
					leg.State = KingdomTradePhysicalState.Lost;
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A resumed water callback lost its exact physical proof.");
					return false;
				}
				leg.State = KingdomTradePhysicalState.Proved;
			}
			int proved = 0;
			for (int i = 0; i < physical.Water.Count; i++)
				if (physical.Water[i].Leg.State == KingdomTradePhysicalState.Proved)
					proved = KingdomTradeRules.SaturatingAdd(proved,
						physical.Water[i].Delta);
			Operation.ProvedWater = proved;
			Operation.AmbiguousWater = Math.Max(0, Operation.RequestedWater - proved);
			return true;
		}

		private static bool ContainsReference(LiquidVolume[] Rows, LiquidVolume Value)
		{
			if (Rows == null) return false;
			for (int i = 0; i < Rows.Length; i++)
				if (ReferenceEquals(Rows[i], Value)) return true;
			return false;
		}

		private static bool ExactPhysicalFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null)
			{
				bool empty = Operation != null && (Operation.WaterLegs == null
					|| Operation.WaterLegs.Count == 0) && (Operation.MaterialOutputs == null
					|| Operation.MaterialOutputs.Count == 0);
				return empty && (Operation.ProjectionState != KingdomTradePhysicalState.Proved
					|| ExactProjectionWitness(Frame, Operation, Z));
			}
			if (physical.StoreList != null
				&& !ExactWaterFrame(physical.Survey, physical.StoreList, physical.StoreRows))
				return false;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				if (witness?.Leg == null) return false;
				bool after = witness.Leg.State == KingdomTradePhysicalState.Proved;
				if (witness.Leg.State != KingdomTradePhysicalState.Prepared
					&& witness.Leg.State != KingdomTradePhysicalState.Proved) return false;
				if (!ExactWaterWitness(witness, Z, after)) return false;
			}
			for (int i = 0; i < physical.Inventories.Count; i++)
				if (!ExactInventory(physical.Inventories[i], Z)) return false;
			for (int i = 0; i < physical.Materials.Count; i++)
				if (!ExactMaterialWitness(physical.Materials[i], Z)
					|| CountMarker(Z, physical.Materials[i].Marker) != 1) return false;
			if (Operation != null && Operation.ProjectionState == KingdomTradePhysicalState.Proved
				&& !ExactProjectionWitness(Frame, Operation, Z)) return false;
			return true;
		}

		private static bool ExactPhysicalWithWaterOverride(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, WaterWitness Override, bool After)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null || (physical.StoreList != null
				&& !ExactWaterFrame(physical.Survey, physical.StoreList, physical.StoreRows))) return false;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness witness = physical.Water[i];
				bool expectedAfter = ReferenceEquals(witness, Override) ? After
					: witness.Leg.State == KingdomTradePhysicalState.Proved;
				if (!ReferenceEquals(witness, Override)
					&& witness.Leg.State != KingdomTradePhysicalState.Prepared
					&& witness.Leg.State != KingdomTradePhysicalState.Proved) return false;
				if (!ExactWaterWitness(witness, Z, expectedAfter)) return false;
			}
			for (int i = 0; i < physical.Inventories.Count; i++)
				if (!ExactInventory(physical.Inventories[i], Z)) return false;
			for (int i = 0; i < physical.Materials.Count; i++)
				if (!ExactMaterialWitness(physical.Materials[i], Z)
					|| CountMarker(Z, physical.Materials[i].Marker) != 1) return false;
			if (Operation != null && Operation.ProjectionState == KingdomTradePhysicalState.Proved
				&& !ExactProjectionWitness(Frame, Operation, Z)) return false;
			return true;
		}

		private static void ReconcilePhysicalFailure(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, string Fault)
		{
			if (Operation == null) return;
			int water = 0;
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical != null)
			{
				for (int i = 0; i < physical.Water.Count; i++)
				{
					WaterWitness witness = physical.Water[i];
					if (ExactWaterWitness(witness, Z, true))
					{
						witness.Leg.State = KingdomTradePhysicalState.Proved;
						water = KingdomTradeRules.SaturatingAdd(water, witness.Delta);
					}
					else witness.Leg.State = KingdomTradePhysicalState.Lost;
				}
				int material = 0;
				for (int i = 0; i < physical.Materials.Count; i++)
				{
					MaterialWitness witness = physical.Materials[i];
					if (ExactMaterialWitness(witness, Z)
						&& CountMarker(Z, witness.Marker) == 1)
					{
						witness.Output.State = KingdomTradePhysicalState.Proved;
						material = KingdomTradeRules.SaturatingAdd(material,
							witness.Count);
					}
					else witness.Output.State = KingdomTradePhysicalState.Lost;
				}
				Operation.MaterialProved = material;
				RefreshSurveyWater(physical);
			}
			Operation.ProvedWater = water;
			Operation.AmbiguousWater = Math.Max(Operation.AmbiguousWater,
				Math.Max(0, Operation.RequestedWater - water));
			Quarantine(Operation, Fault);
		}

		private static void FreezeMaterials(KingdomTradeOperation Operation,
			KingdomMaterialTally Tally)
		{
			string[] rows = new string[KingdomMaterialRules.MaterialCount];
			int total = 0;
			for (int i = 0; i < rows.Length; i++)
			{
				int amount = Tally == null ? 0 : Tally.Get((KingdomMaterial)i);
				rows[i] = amount.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
				total = KingdomTradeRules.SaturatingAdd(total, amount);
			}
			Operation.MaterialClaim = string.Join("|", rows);
			Operation.MaterialRequested = total;
		}

		private static bool ApplyMaterials(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			int[] amounts;
			if (!TryMaterialClaim(Operation.MaterialClaim, out amounts))
			{
				Quarantine(Operation,
					"The frozen material load is malformed and was not minted.");
				return false;
			}
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			GameObject destination = null;
			for (int i = 0; stock != null && stock.Stockpiles != null
				&& i < stock.Stockpiles.Count; i++)
			{
				GameObject candidate = stock.Stockpiles[i];
				if (GameObject.Validate(candidate) && candidate.CurrentZone == Z
					&& candidate.Inventory != null
					&& candidate.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				{
					destination = candidate;
					break;
				}
			}
			if (destination == null || string.IsNullOrEmpty(destination.ID))
			{
				Quarantine(Operation,
					"The material load had no exact stockpile owner and remains quarantined on the caravan.");
				return false;
			}
			GameObject resolvedDestination;
			LoadedTopologyWitness destinationTopology;
			if (ResolveLoadedObject(destination.ID, Z, out resolvedDestination,
				out destinationTopology) != LoadedObjectResolution.ExactUnique
				|| !ReferenceEquals(resolvedDestination, destination))
				return QuarantineFalse(Operation,
					"The material destination id was not exact-unique on active settlement ground.");
			if (Frame.Physical == null) Frame.Physical = new TradePhysicalFrame();
			InventoryWitness inventory;
			if (!TryCaptureInventory(Frame.Physical, destination, Z, out inventory))
				return QuarantineFalse(Operation,
					"The material destination inventory could not be captured exactly.");
			List<GameObject> made = new List<GameObject>();
			List<KingdomTradeMaterialOutput> receipts =
				new List<KingdomTradeMaterialOutput>();
			List<MaterialWitness> candidates = new List<MaterialWitness>();
			Operation.Phase = KingdomTradePhase.ResourceIntent;
			for (int i = 0; i < amounts.Length; i++)
			{
				if (amounts[i] <= 0) continue;
				string blueprint = KingdomMaterials.MaterialBlueprints[i];
				KingdomTradeMaterialOutput output = new KingdomTradeMaterialOutput
				{
					Marker = KingdomTradeRules.MaterialMarker(Operation.Id, i),
					Blueprint = blueprint,
					Count = amounts[i],
					DestinationOwnerId = destination.ID,
					ZoneId = Z.ZoneID,
					State = KingdomTradePhysicalState.CreateIntent,
					CleanupState = KingdomTradePhysicalState.None
				};
				Operation.MaterialOutputs.Add(output);
				RefreshReceiptRows(Frame);
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterials(candidates, 0))
				{
					output.State = KingdomTradePhysicalState.Lost;
					CleanupCreatedMaterials(Operation, Z, Frame, candidates);
					return QuarantineFalse(Operation,
						"The material frame changed before its creation callback.");
					}
					CallbackWitness callback = CaptureCallbackWitness(Frame);
					LoadedTopologyWitness createTopology = CaptureLoadedTopology();
					if (callback == null || createTopology == null
						|| !ExactLoadedTopology(createTopology))
				{
					output.State = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation, "Material creation frame could not be frozen.");
				}
				GameObject item = GameObject.Create(blueprint);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactLoadedTopology(createTopology))
					return FailDetachedAuthority(Frame,
						"A material creation callback detached its official trade authority.");
				if (!ReferenceEquals(Operation.MaterialOutputs[
						Operation.MaterialOutputs.Count - 1], output)
					|| output.State != KingdomTradePhysicalState.CreateIntent
					|| !string.Equals(output.Marker,
						KingdomTradeRules.MaterialMarker(Operation.Id, i), StringComparison.Ordinal)
					|| !string.Equals(output.Blueprint, blueprint, StringComparison.Ordinal)
					|| output.Count != amounts[i]
					|| !string.Equals(output.DestinationOwnerId, destination.ID,
						StringComparison.Ordinal)
					|| !string.Equals(output.ZoneId, Z.ZoneID, StringComparison.Ordinal)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterials(candidates, 0)
					|| !GameObject.Validate(item) || string.IsNullOrEmpty(item.ID)
					|| !string.Equals(item.Blueprint, blueprint, StringComparison.Ordinal))
				{
					output.State = KingdomTradePhysicalState.Lost;
					CleanupCreatedMaterials(Operation, Z, Frame, candidates);
					return QuarantineFalse(Operation,
						"A material output blueprint could not be bound before placement.");
				}
				item.Count = amounts[i];
				item.SetStringProperty(MaterialProperty, output.Marker);
				output.OutputId = item.ID;
				output.State = KingdomTradePhysicalState.Prepared;
				MaterialWitness witness = CaptureMaterialWitness(output, item,
					destination, inventory);
				if (!ExactCreatedMaterial(witness))
				{
					output.State = KingdomTradePhysicalState.Lost;
					if (witness != null) candidates.Add(witness);
					CleanupCreatedMaterials(Operation, Z, Frame, candidates);
					return QuarantineFalse(Operation,
						"A created material output changed before its placement intent.");
				}
				made.Add(item);
				receipts.Add(output);
				candidates.Add(witness);
			}
			for (int i = 0; i < made.Count; i++)
			{
				KingdomTradeMaterialOutput output = receipts[i];
				GameObject item = made[i];
				MaterialWitness witness = candidates[i];
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterials(candidates, i)
					|| !ExactInventory(inventory, Z))
				{
					output.State = KingdomTradePhysicalState.Lost;
					MarkUnplacedCleanupLost(candidates, i);
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"The material frame changed before its AddObject callback.");
					return false;
				}
				output.State = KingdomTradePhysicalState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				LoadedTopologyWitness addTopology = CaptureLoadedTopology();
				if (callback == null || addTopology == null
					|| !ExactLoadedTopology(addTopology))
				{
					output.State = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation, "Material AddObject frame could not be frozen.");
				}
				GameObject added = null;
				try
				{
					added = inventory.Inventory.AddObject(item, null, Silent: true);
				}
				finally
				{
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(inventory.Owner);
					KingdomSurvey.ObserveAddResultInActive(Z, item, added);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactLoadedTopologyWithDelta(addTopology, item, null,
						inventory.Owner, false))
					return FailDetachedAuthority(Frame,
						"A material AddObject callback detached its official trade authority.");
				RefreshPhysicalTopologies(Frame.Physical);
				witness.Topology = CaptureLoadedTopology();
				if (!ReferenceEquals(added, item) || output.State != KingdomTradePhysicalState.Intent
					|| !ExactPhysicalWithInventoryAppend(Frame, Operation, Z,
						inventory, witness)
					|| !ExactCreatedMaterials(candidates, i + 1))
				{
					output.State = KingdomTradePhysicalState.Lost;
					MarkUnplacedCleanupLost(candidates, i + 1);
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"A material AddObject callback did not leave the exact output at its bound owner.");
					return false;
				}
				inventory.Rows = AppendRow(inventory.Rows, item);
				Frame.Physical.Materials.Add(witness);
				output.State = KingdomTradePhysicalState.Proved;
				Operation.MaterialProved = KingdomTradeRules.SaturatingAdd(
					Operation.MaterialProved, witness.Count);
				if (!ExactPhysicalFrame(Frame, Operation, Z))
				{
					ReconcilePhysicalFailure(Frame, Operation, Z,
						"The material frame changed before its durable checkpoint.");
					return false;
				}
			}
			return true;
		}

		private static bool ReconcileMaterialOutputs(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			int proved = 0;
			for (int i = 0; i < Operation.MaterialOutputs.Count; i++)
			{
				KingdomTradeMaterialOutput output = Operation.MaterialOutputs[i];
				if (output.State == KingdomTradePhysicalState.Prepared
					|| output.State == KingdomTradePhysicalState.CreateIntent
					|| output.CleanupState == KingdomTradePhysicalState.CleanupIntent)
				{
					output.State = KingdomTradePhysicalState.Lost;
					if (output.CleanupState == KingdomTradePhysicalState.CleanupIntent)
						output.CleanupState = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation,
						"A reloaded material creation or cleanup frame was uninspectable and was not replayed.");
				}
				if (output.State == KingdomTradePhysicalState.Proved)
					proved = KingdomTradeRules.SaturatingAdd(proved, output.Count);
				else if (output.State == KingdomTradePhysicalState.Intent)
					return QuarantineFalse(Operation,
						"A reloaded material Add intent lacked one exact live topology and was not replayed.");
			}
			Operation.MaterialProved = proved;
			return ExactPhysicalFrame(Frame, Operation, Z);
		}

		private static bool ExactMaterial(MaterialWitness Witness, Zone Z)
		{
			if (!ExactMaterialReceipt(Witness)) return false;
			GameObject item = Witness.Item;
			GameObject destination = Witness.Destination;
			return GameObject.Validate(item) && GameObject.Validate(destination) && Z != null
				&& destination.CurrentZone == Z && destination.Inventory != null
				&& destination.GetIntProperty(KingdomMaterials.StockpileProperty) == 1
				&& string.Equals(destination.ID, Witness.DestinationOwnerId,
					StringComparison.Ordinal)
				&& string.Equals(Z.ZoneID, Witness.ZoneId, StringComparison.Ordinal)
				&& string.Equals(item.ID, Witness.OutputId, StringComparison.Ordinal)
				&& string.Equals(item.Blueprint, Witness.Blueprint, StringComparison.Ordinal)
				&& item.Count == Witness.Count && item.InInventory == destination
				&& destination.Inventory.Objects.Contains(item)
				&& string.Equals(item.GetStringProperty(MaterialProperty), Witness.Marker,
					StringComparison.Ordinal);
		}

		private static bool ExactCreatedMaterial(MaterialWitness Witness)
		{
			if (!ExactMaterialReceipt(Witness)) return false;
			GameObject item = Witness.Item;
			return GameObject.Validate(item) && item.InInventory == null
				&& item.CurrentCell == null && string.Equals(item.ID, Witness.OutputId,
					StringComparison.Ordinal)
				&& string.Equals(item.Blueprint, Witness.Blueprint, StringComparison.Ordinal)
				&& item.Count == Witness.Count && string.Equals(item.GetStringProperty(
					MaterialProperty), Witness.Marker, StringComparison.Ordinal);
		}

		private static bool ExactCreatedMaterials(List<MaterialWitness> Witnesses,
			int Start)
		{
			if (Witnesses == null || Start < 0 || Start > Witnesses.Count) return false;
			for (int i = Start; i < Witnesses.Count; i++)
				if (!ExactCreatedMaterial(Witnesses[i])) return false;
			return true;
		}

		private static bool TryCaptureInventory(TradePhysicalFrame Physical,
			GameObject Owner, Zone Z, out InventoryWitness Witness)
		{
			Witness = null;
			if (Physical == null || !GameObject.Validate(Owner) || Owner.CurrentZone != Z
				|| Owner.CurrentCell == null || Owner.CurrentCell.ParentZone != Z
				|| Owner.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| Owner.Inventory == null || Owner.Inventory.Objects == null
				|| !ReferenceEquals(Owner.GetPart<Inventory>(), Owner.Inventory)) return false;
			for (int i = 0; i < Physical.Inventories.Count; i++)
			{
				InventoryWitness existing = Physical.Inventories[i];
				if (!ReferenceEquals(existing.Owner, Owner)) continue;
				if (!ExactInventory(existing, Z)) return false;
				Witness = existing;
				return true;
			}
			Witness = new InventoryWitness
			{
				Owner = Owner,
				Inventory = Owner.Inventory,
				Objects = Owner.Inventory.Objects,
				Rows = Owner.Inventory.Objects.ToArray()
			};
			Physical.Inventories.Add(Witness);
			return ExactInventory(Witness, Z);
		}

		private static bool ExactInventory(InventoryWitness Witness, Zone Z)
		{
			if (Witness == null || !GameObject.Validate(Witness.Owner)
				|| Witness.Owner.CurrentZone != Z || Witness.Owner.CurrentCell == null
				|| Witness.Owner.CurrentCell.ParentZone != Z
				|| Witness.Owner.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| !ReferenceEquals(Witness.Owner.Inventory, Witness.Inventory)
				|| !ReferenceEquals(Witness.Owner.GetPart<Inventory>(), Witness.Inventory)
				|| Witness.Inventory == null || Witness.Inventory.ParentObject != Witness.Owner
				|| !ReferenceEquals(Witness.Inventory.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length) return false;
			for (int i = 0; i < Witness.Rows.Length; i++)
				if (!ReferenceEquals(Witness.Objects[i], Witness.Rows[i])) return false;
			return true;
		}

		private static bool ExactMaterialWitness(MaterialWitness Witness, Zone Z)
		{
			return ExactMaterialReceipt(Witness)
				&& ExactLoadedTopology(Witness.Topology)
				&& ExactInventory(Witness.Inventory, Z)
				&& ReferenceEquals(Witness.Destination, Witness.Inventory.Owner)
				&& ExactMaterial(Witness, Z);
		}

		private static bool ExactPhysicalWithInventoryAppend(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z, InventoryWitness Target,
			MaterialWitness Added)
		{
			TradePhysicalFrame physical = Frame?.Physical;
			if (physical == null || Target == null || Added == null
				|| !ReferenceEquals(Added.Inventory, Target) || Target.Objects == null
				|| Target.Rows == null || Target.Objects.Count != Target.Rows.Length + 1)
				return false;
			if (!GameObject.Validate(Target.Owner) || Target.Owner.CurrentZone != Z
				|| Target.Owner.CurrentCell == null || Target.Owner.CurrentCell.ParentZone != Z
				|| Target.Owner.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| !ReferenceEquals(Target.Owner.Inventory, Target.Inventory)
				|| !ReferenceEquals(Target.Owner.GetPart<Inventory>(), Target.Inventory)
				|| !ReferenceEquals(Target.Inventory.Objects, Target.Objects)) return false;
			for (int i = 0; i < Target.Rows.Length; i++)
				if (!ReferenceEquals(Target.Objects[i], Target.Rows[i])) return false;
			if (!ReferenceEquals(Target.Objects[Target.Rows.Length], Added.Item)
				|| !ReferenceEquals(Added.Destination, Target.Owner)
				|| !ExactMaterial(Added, Z)
				|| CountMarker(Z, Added.Marker) != 1) return false;
			if (physical.StoreList != null
				&& !ExactWaterFrame(physical.Survey, physical.StoreList, physical.StoreRows)) return false;
			for (int i = 0; i < physical.Water.Count; i++)
			{
				WaterWitness water = physical.Water[i];
				if (water.Leg.State != KingdomTradePhysicalState.Prepared
					&& water.Leg.State != KingdomTradePhysicalState.Proved) return false;
				if (!ExactWaterWitness(water, Z,
					water.Leg.State == KingdomTradePhysicalState.Proved)) return false;
			}
			for (int i = 0; i < physical.Inventories.Count; i++)
				if (!ReferenceEquals(physical.Inventories[i], Target)
					&& !ExactInventory(physical.Inventories[i], Z)) return false;
			for (int i = 0; i < physical.Materials.Count; i++)
				if (!ExactMaterialWitness(physical.Materials[i], Z)
					|| CountMarker(Z, physical.Materials[i].Marker) != 1) return false;
			return true;
		}

		private static GameObject[] AppendRow(GameObject[] Rows, GameObject Item)
		{
			GameObject[] next = new GameObject[Rows.Length + 1];
			Array.Copy(Rows, next, Rows.Length);
			next[Rows.Length] = Item;
			return next;
		}

		private static bool CleanupCreatedMaterials(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame, List<MaterialWitness> Witnesses)
		{
			bool exact = true;
			for (int i = 0; Witnesses != null && i < Witnesses.Count; i++)
			{
				MaterialWitness witness = Witnesses[i];
				GameObject item = witness?.Item;
				KingdomTradeMaterialOutput output = witness?.Output;
				if (output == null)
				{
					exact = false;
					continue;
				}
				output.State = KingdomTradePhysicalState.Lost;
				if (!ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Z)
					|| !ExactCreatedMaterial(witness))
				{
					output.CleanupState = KingdomTradePhysicalState.Lost;
					exact = false;
					continue;
				}
				output.CleanupState = KingdomTradePhysicalState.CleanupIntent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				LoadedTopologyWitness cleanupTopology = CaptureLoadedTopology();
				if (callback == null || cleanupTopology == null
					|| !ExactLoadedTopology(cleanupTopology))
				{
					output.CleanupState = KingdomTradePhysicalState.Lost;
					exact = false;
					continue;
				}
				try
				{
					item.Obliterate();
				}
				finally
				{
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(witness.Inventory?.Owner);
					BoundTradeSurvey(Z)?.ObserveCurrentTopology(item);
				}
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ResourceIntent)
					|| !ExactLoadedTopology(cleanupTopology))
				{
					FailDetachedAuthority(Frame,
						"A material cleanup callback detached its official trade authority.");
					output.CleanupState = KingdomTradePhysicalState.Lost;
					return false;
				}
				output.CleanupState = output.CleanupState == KingdomTradePhysicalState.CleanupIntent
					&& ExactMaterialReceipt(witness) && !GameObject.Validate(item)
					&& ExactPhysicalFrame(Frame, Operation, Z)
					? KingdomTradePhysicalState.Proved : KingdomTradePhysicalState.Lost;
				if (output.CleanupState != KingdomTradePhysicalState.Proved) exact = false;
			}
			return exact;
		}

		private static void MarkUnplacedCleanupLost(List<MaterialWitness> Witnesses, int Start)
		{
			if (Witnesses == null) return;
			for (int i = Start; i < Witnesses.Count; i++)
			{
				KingdomTradeMaterialOutput output = Witnesses[i]?.Output;
				if (output == null) continue;
				output.State = KingdomTradePhysicalState.Lost;
				output.CleanupState = KingdomTradePhysicalState.Lost;
			}
		}

		private static int CountMarker(Zone Z, string Marker)
		{
			if (Z == null || string.IsNullOrEmpty(Marker)) return 0;
			KingdomSurvey survey = BoundTradeSurvey(Z);
			IList<GameObject> objects;
			if (survey == null || !survey.TryLoaded(out objects) || objects == null)
				return int.MaxValue;
			int count = 0;
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i]) && string.Equals(
					objects[i].GetStringProperty(MaterialProperty),
					Marker, StringComparison.Ordinal)) count++;
			return count;
		}

		private static bool TryMaterialClaim(string Claim, out int[] Amounts)
		{
			Amounts = null;
			if (string.IsNullOrEmpty(Claim)
				|| Claim.Length > KingdomTradeRules.MaxClaimChars) return false;
			int separators = 0;
			for (int i = 0; i < Claim.Length; i++)
				if (Claim[i] == '|') separators++;
			if (separators != KingdomMaterialRules.MaterialCount - 1) return false;
			string[] rows = Claim.Split('|');
			if (rows.Length != KingdomMaterialRules.MaterialCount) return false;
			Amounts = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].Length == 0 || rows[i].Length > 10
					|| !int.TryParse(rows[i], global::System.Globalization.NumberStyles.None,
					global::System.Globalization.CultureInfo.InvariantCulture, out Amounts[i])
					|| Amounts[i] < 0 || Amounts[i].ToString(
						global::System.Globalization.CultureInfo.InvariantCulture) != rows[i]) return false;
			}
			return true;
		}

		private static void SettleProjection(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			if (Operation.Kind != KingdomTradeOperationKind.CharterDelivery)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Skipped;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
				return;
			}
			if (!string.IsNullOrEmpty(Operation.PriorProjectionId)
				&& !string.Equals(Operation.PriorProjectionZoneId, Z?.ZoneID,
					StringComparison.Ordinal))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Skipped;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Operation.Fault = AppendFault(Operation.Fault,
					"This city's existing caravan is bound to another loaded zone; no second projection was created.");
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
				return;
			}
			if (Operation.Phase == KingdomTradePhase.ProjectionIntent)
			{
				ReconcileProjection(Operation, Z, Frame);
				return;
			}
			Cell cell;
			if (!TryChooseProjectionCell(Z, out cell)
				|| string.IsNullOrEmpty(Operation.CaravanBlueprint))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Skipped;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Operation.Fault = AppendFault(Operation.Fault,
					"The caravan projection had no exact cell; delivery authority was not replayed.");
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
				return;
			}
			Operation.ProjectionState = KingdomTradePhysicalState.CreateIntent;
			Operation.Phase = KingdomTradePhase.ProjectionIntent;
			if (!ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Z))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"The caravan frame changed before its creation callback.");
				return;
			}
			CallbackWitness callback = CaptureCallbackWitness(Frame);
			LoadedTopologyWitness createTopology = CaptureLoadedTopology();
			if (callback == null || createTopology == null
				|| !ExactLoadedTopology(createTopology))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Caravan creation frame could not be frozen.");
				return;
			}
			GameObject caravan = GameObject.Create(Operation.CaravanBlueprint);
			if (!ExactCallbackWitness(Frame, callback)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactLoadedTopology(createTopology))
			{
				FailDetachedAuthority(Frame,
					"A caravan creation callback detached its official trade authority.");
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				return;
			}
			if (Operation.ProjectionState != KingdomTradePhysicalState.CreateIntent
				|| !ExactPhysicalFrame(Frame, Operation, Z)
				|| !GameObject.Validate(caravan) || string.IsNullOrEmpty(caravan.ID)
				|| !string.Equals(caravan.Blueprint, Operation.CaravanBlueprint,
					StringComparison.Ordinal))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				Quarantine(Operation,
					"The frozen caravan blueprint did not create an exact projection.");
				return;
			}
			CellWitness cellWitness;
			if (!TryCaptureCell(cell, Z, out cellWitness)
				|| cellWitness.Rows.Length != 0)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"The chosen caravan cell changed before placement.");
				return;
			}
			Operation.ProjectionObjectId = caravan.ID;
			Operation.ProjectionX = cell.X;
			Operation.ProjectionY = cell.Y;
			caravan.SetStringProperty(ProjectionProperty, Operation.ProjectionId);
			caravan.SetIntProperty("KingdomCaravan", 1);
			if (caravan.Brain != null) caravan.Brain.Allegiance.Calm = true;
			Operation.ProjectionState = KingdomTradePhysicalState.Intent;
			if (!ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Z)
				|| !ExactCell(cellWitness, Z))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "The caravan frame changed before AddObject.");
				return;
			}
			callback = CaptureCallbackWitness(Frame);
			LoadedTopologyWitness addTopology = CaptureLoadedTopology();
			if (callback == null || addTopology == null
				|| !ExactLoadedTopology(addTopology))
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Caravan AddObject frame could not be frozen.");
				return;
			}
			GameObject added = null;
			try
			{
				added = cell.AddObject(caravan);
			}
			finally
			{
				KingdomSurvey.ObserveAddResultInActive(Z, caravan, added);
			}
			if (!ExactCallbackWitness(Frame, callback)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactLoadedTopologyWithDelta(addTopology, caravan, null, null, true))
			{
				FailDetachedAuthority(Frame,
					"A caravan AddObject callback detached its official trade authority.");
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				return;
			}
			RefreshPhysicalTopologies(Frame.Physical);
			if (!ReferenceEquals(added, caravan)
				|| Operation.ProjectionState != KingdomTradePhysicalState.Intent
				|| !ExactPhysicalFrame(Frame, Operation, Z)
				|| !ExactCellAfterAppend(cellWitness, caravan, Z)
				|| !ExactProjection(caravan, cell, Operation, Z)
				|| CountProjection(Z, Operation.ProjectionId) != 1)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"The caravan AddObject callback did not leave one exact projection.");
				return;
			}
			cellWitness.Rows = AppendRow(cellWitness.Rows, caravan);
			Frame.ProjectionObject = caravan;
			Frame.ProjectionCell = cellWitness;
			Operation.ProjectionState = KingdomTradePhysicalState.Proved;
			SettlePriorProjection(Operation, Z, Frame);
			if (Operation.Phase != KingdomTradePhase.Quarantined)
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
		}

		/// <summary>Finds one exact object-rack-empty caravan berth without allocating or
		/// traversing Qud's full empty-cell list. Boundary cells retain first priority; a bounded
		/// row-major interior probe is deterministic across save/resume and fails closed when the
		/// settlement is too crowded.</summary>
		private static bool TryChooseProjectionCell(Zone Z, out Cell Cell)
		{
			Cell = null;
			if (Z == null || Z.Width <= 0 || Z.Height <= 0) return false;
			int probes = 0;
			for (int y = 0; y < Z.Height && probes < MaxProjectionCellProbes; y++)
			{
				for (int x = 0; x < Z.Width && probes < MaxProjectionCellProbes; x++)
				{
					if (x != 0 && x != Z.Width - 1 && y != 0 && y != Z.Height - 1)
						continue;
					probes++;
					Cell candidate = Z.GetCell(x, y);
					if (ExactEmptyProjectionCell(candidate, Z))
					{
						Cell = candidate;
						return true;
					}
				}
			}
			for (int y = 1; y < Z.Height - 1 && probes < MaxProjectionCellProbes; y++)
			{
				for (int x = 1; x < Z.Width - 1 && probes < MaxProjectionCellProbes; x++)
				{
					probes++;
					Cell candidate = Z.GetCell(x, y);
					if (ExactEmptyProjectionCell(candidate, Z))
					{
						Cell = candidate;
						return true;
					}
				}
			}
			return false;
		}

		private static bool ExactEmptyProjectionCell(Cell Cell, Zone Z)
		{
			return Cell != null && ReferenceEquals(Cell.ParentZone, Z)
				&& Cell.Objects != null && Cell.Objects.Count == 0 && Cell.IsEmpty();
		}

		private static void ReconcileProjection(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			if (Operation.ProjectionState == KingdomTradePhysicalState.Intent
				|| Operation.ProjectionState == KingdomTradePhysicalState.CreateIntent)
			{
				Operation.ProjectionState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"A reloaded caravan Create/Add intent lacked its live list witness and was not replayed.");
				return;
			}
			SettlePriorProjection(Operation, Z, Frame);
			if (Operation.Phase != KingdomTradePhase.Quarantined)
				Operation.Phase = KingdomTradePhase.ProjectionSettled;
		}

		private static void SettlePriorProjection(KingdomTradeOperation Operation, Zone Z,
			TradeLiveFrame Frame)
		{
			if (string.IsNullOrEmpty(Operation.PriorProjectionId))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				return;
			}
			GameObject old;
			LoadedTopologyWitness oldTopology;
			LoadedObjectResolution oldResolution = ResolveLoadedObject(
				Operation.PriorProjectionObjectId, Z, out old, out oldTopology);
			if (Operation.PriorCleanupState == KingdomTradePhysicalState.Intent
				|| Operation.PriorCleanupState == KingdomTradePhysicalState.CleanupIntent)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation,
					"Old projection cleanup resumed without its live list witness and was not repeated.");
				return;
			}
			if (oldResolution == LoadedObjectResolution.Missing)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Skipped;
				return;
			}
			if (oldResolution != LoadedObjectResolution.ExactUnique)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection object identity was ambiguous or topology-incomplete.");
				return;
			}
			if (!GameObject.Validate(old) || old.CurrentZone != Z
				|| !string.Equals(old.GetStringProperty(ProjectionProperty),
					Operation.PriorProjectionId, StringComparison.Ordinal))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection exact object did not match its persisted marker.");
				return;
			}
			if (CountProjection(Z, Operation.PriorProjectionId) != 1)
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection identity was not unique on active settlement ground.");
				return;
			}
			CellWitness oldCell;
			if (!TryCaptureCell(old.CurrentCell, Z, out oldCell)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Z))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection cleanup lost its exact live frame.");
				return;
			}
			Operation.PriorCleanupState = KingdomTradePhysicalState.CleanupIntent;
			CallbackWitness callback = CaptureCallbackWitness(Frame);
			if (callback == null || oldTopology == null || !ExactLoadedTopology(oldTopology))
			{
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				Quarantine(Operation, "Old projection cleanup frame could not be frozen.");
				return;
			}
			try
			{
				old.Obliterate();
			}
			finally
			{
				BoundTradeSurvey(Z)?.ObserveCurrentTopology(old);
			}
			if (!ExactCallbackWitness(Frame, callback)
				|| !ExactAuthority(Frame, KingdomTradePhase.ProjectionIntent)
				|| !ExactLoadedTopologyWithDelta(oldTopology, null, old, null, true))
			{
				FailDetachedAuthority(Frame,
					"An old-projection cleanup callback detached official trade authority.");
				Operation.PriorCleanupState = KingdomTradePhysicalState.Lost;
				return;
			}
			RefreshPhysicalTopologies(Frame.Physical);
			Operation.PriorCleanupState = !GameObject.Validate(old)
				&& ExactCellAfterRemoval(oldCell, old, Z)
				&& CountProjection(Z, Operation.PriorProjectionId) == 0
				&& ExactPhysicalFrame(Frame, Operation, Z)
				? KingdomTradePhysicalState.Proved : KingdomTradePhysicalState.Lost;
			if (Operation.PriorCleanupState == KingdomTradePhysicalState.Lost)
				Quarantine(Operation,
					"Old caravan destruction was vetoed or changed topology; it was not attempted twice.");
		}

		private static bool TryBindProjectionFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z)
		{
			if (Operation == null || Operation.Kind != KingdomTradeOperationKind.CharterDelivery
				|| Operation.ProjectionState != KingdomTradePhysicalState.Proved) return true;
			if (Frame.ProjectionObject != null || Frame.ProjectionCell != null)
				return ExactProjectionWitness(Frame, Operation, Z);
			GameObject body;
			LoadedTopologyWitness topology;
			if (ResolveLoadedObject(Operation.ProjectionObjectId, Z, out body, out topology)
				!= LoadedObjectResolution.ExactUnique) return false;
			CellWitness cell;
			if (!GameObject.Validate(body) || !TryCaptureCell(body.CurrentCell, Z, out cell))
				return false;
			Frame.ProjectionObject = body;
			Frame.ProjectionCell = cell;
			return ExactLoadedTopology(topology) && ExactProjectionWitness(Frame, Operation, Z);
		}

		private static bool ExactProjectionWitness(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, Zone Z)
		{
			return Frame != null && ExactCell(Frame.ProjectionCell, Z)
				&& ExactProjection(Frame.ProjectionObject, Frame.ProjectionCell.Cell,
					Operation, Z)
				&& CountProjection(Z, Operation.ProjectionId) == 1;
		}

		private static bool ExactProjection(GameObject Body, Cell Cell,
			KingdomTradeOperation Operation, Zone Z)
		{
			return GameObject.Validate(Body) && Cell != null && Cell.ParentZone == Z
				&& Body.CurrentZone == Z && Body.CurrentCell == Cell
				&& Cell.X == Operation.ProjectionX && Cell.Y == Operation.ProjectionY
				&& string.Equals(Body.ID, Operation.ProjectionObjectId,
					StringComparison.Ordinal)
				&& string.Equals(Body.Blueprint, Operation.CaravanBlueprint,
					StringComparison.Ordinal)
				&& string.Equals(Body.GetStringProperty(ProjectionProperty),
					Operation.ProjectionId, StringComparison.Ordinal)
				&& Body.GetIntProperty("KingdomCaravan") == 1;
		}

		private static bool TryCaptureCell(Cell Cell, Zone Z, out CellWitness Witness)
		{
			Witness = null;
			if (Cell == null || Cell.ParentZone != Z || Cell.Objects == null) return false;
			Witness = new CellWitness
			{
				Cell = Cell, Objects = Cell.Objects, Rows = Cell.Objects.ToArray()
			};
			return ExactCell(Witness, Z);
		}

		private static bool ExactCell(CellWitness Witness, Zone Z)
		{
			if (Witness == null || Witness.Cell == null || Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Cell.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length) return false;
			for (int i = 0; i < Witness.Rows.Length; i++)
				if (!ReferenceEquals(Witness.Objects[i], Witness.Rows[i])) return false;
			return true;
		}

		private static bool ExactCellAfterAppend(CellWitness Witness, GameObject Added, Zone Z)
		{
			if (Witness == null || Witness.Cell == null || Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Cell.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length + 1) return false;
			for (int i = 0; i < Witness.Rows.Length; i++)
				if (!ReferenceEquals(Witness.Objects[i], Witness.Rows[i])) return false;
			return ReferenceEquals(Witness.Objects[Witness.Rows.Length], Added);
		}

		private static bool ExactCellAfterRemoval(CellWitness Witness, GameObject Removed, Zone Z)
		{
			if (Witness == null || Witness.Cell == null || Witness.Cell.ParentZone != Z
				|| !ReferenceEquals(Witness.Cell.Objects, Witness.Objects)
				|| Witness.Objects == null || Witness.Rows == null
				|| Witness.Objects.Count != Witness.Rows.Length - 1) return false;
			int at = 0;
			bool found = false;
			for (int i = 0; i < Witness.Rows.Length; i++)
			{
				if (ReferenceEquals(Witness.Rows[i], Removed))
				{
					if (found) return false;
					found = true;
					continue;
				}
				if (at >= Witness.Objects.Count
					|| !ReferenceEquals(Witness.Objects[at++], Witness.Rows[i])) return false;
			}
			return found && at == Witness.Objects.Count;
		}

		private static int CountProjection(Zone Z, string ProjectionId)
		{
			if (Z == null || string.IsNullOrEmpty(ProjectionId)) return 0;
			KingdomSurvey survey = BoundTradeSurvey(Z);
			IList<GameObject> objects;
			if (survey == null || !survey.TryLoaded(out objects) || objects == null)
				return int.MaxValue;
			int count = 0;
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i]) && string.Equals(
					objects[i].GetStringProperty(ProjectionProperty), ProjectionId,
					StringComparison.Ordinal)) count++;
			return count;
		}

		private static bool SettleDomain(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			Operation.Phase = KingdomTradePhase.DomainIntent;
			if (!ExactAuthority(Frame, KingdomTradePhase.DomainIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The domain frame changed before its exact settlement CAS.");
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.CharterDelivery:
				if (!SettleStanding(System, Operation, Frame)) return false;
				if (Operation.ProjectionState == KingdomTradePhysicalState.Proved)
				{
					if (!PublishProjectionRow(Book, Operation))
						return QuarantineFalse(Operation,
							"The per-city caravan projection lost its exact before/after CAS.");
					RefreshProjectionRows(Frame);
				}
				break;
			case KingdomTradeOperationKind.ManifestLoad:
				if (Book.Manifest != null || Operation.ProvedWater != Operation.RequestedWater)
				{
					Quarantine(Operation, "Manifest publication lost its exact empty-slot or debit proof.");
					return false;
				}
					Book.Manifest = new KingdomTradeManifestState
					{
						OperationSequence = Operation.Sequence,
						OperationId = Operation.Id,
						Id = Operation.ManifestId,
					OriginId = Operation.OriginId,
					OriginName = Operation.OriginName,
					DestinationId = Operation.DestinationId,
					DestinationName = Operation.DestinationName,
					OriginalDrams = Operation.RequestedWater,
					EscrowDrams = Operation.ProvedWater,
					LoadedTick = Operation.ManifestLoadedTick,
					DeadlineTick = Operation.ManifestDeadlineTick,
					Status = KingdomTradeManifestStatus.InFlight
				};
				break;
			case KingdomTradeOperationKind.ManifestDelivery:
				if (!ExactManifestIdentity(Book.Manifest, Operation)
					|| (Operation.ManifestEscrowState == KingdomTradePhysicalState.Prepared
						&& Book.Manifest.EscrowDrams != Operation.ManifestEscrowBefore))
					return QuarantineFalse(Operation,
					"Manifest delivery no longer owns the exact escrow row.");
				if (!SettleManifestCreditAccounting(Book, Operation)) return false;
				break;
			case KingdomTradeOperationKind.ManifestTurnback:
				if (!ExactManifest(Book.Manifest, Operation) || Book.Manifest.TurnedBack)
					return QuarantineFalse(Operation, "Manifest turnback lost its exact route CAS.");
				string originId = Book.Manifest.OriginId;
				string originName = Book.Manifest.OriginName;
				Book.Manifest.OriginId = Book.Manifest.DestinationId;
				Book.Manifest.OriginName = Book.Manifest.DestinationName;
				Book.Manifest.DestinationId = originId;
				Book.Manifest.DestinationName = originName;
				Book.Manifest.TurnedBack = true;
				Book.Manifest.LoadedTick = Operation.ManifestLoadedTick;
				Book.Manifest.DeadlineTick = Operation.ManifestDeadlineTick;
				break;
			case KingdomTradeOperationKind.ManifestLapse:
				if (!ExactManifest(Book.Manifest, Operation) || !Book.Manifest.TurnedBack)
					return QuarantineFalse(Operation, "Manifest lapse lost its exact escrow CAS.");
				if (!SettleRetainedAccounting(Book, Operation)) return false;
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = "Both road windows closed; escrow remains retained under its permanent receipt.";
				break;
			}
			RefreshBookDomain(Frame);
			// Domain state is now externally visible to outbox callbacks. Publish compatibility
			// projection before any callback can read documented legacy API.
			System.SynchronizeLegacyManifestProjection();
			if (!ExactAuthority(Frame, KingdomTradePhase.DomainIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The domain settlement CAS changed its exact authority or physical frame.");
			Operation.Phase = KingdomTradePhase.DomainSettled;
			return true;
		}

		private static bool SettleManifestCreditAccounting(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			KingdomTradeManifestState manifest = Book?.Manifest;
			if (manifest == null || Operation == null || Operation.ProvedWater < 0
				|| Operation.ProvedWater > Operation.ManifestEscrowBefore)
				return QuarantineFalse(Operation, "Manifest credit accounting lacks exact escrow evidence.");
			if (Operation.ManifestEscrowState == KingdomTradePhysicalState.Prepared)
			{
				Operation.ManifestEscrowDebit = Operation.ProvedWater;
				Operation.ManifestEscrowAfter = Operation.ManifestEscrowBefore - Operation.ProvedWater;
				Operation.ManifestEscrowState = KingdomTradePhysicalState.Intent;
			}
			if (Operation.ManifestEscrowState == KingdomTradePhysicalState.Intent)
			{
				int after;
				bool apply;
				if (!KingdomTradeRules.TryReconcileEscrow(Operation.ManifestEscrowBefore,
					Operation.ManifestEscrowDebit, manifest.EscrowDrams, out after, out apply)
					|| after != Operation.ManifestEscrowAfter)
				{
					Operation.ManifestEscrowState = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation,
						"Manifest escrow is neither exact before nor exact after; credit remains unresolved.");
				}
				if (apply) manifest.EscrowDrams = after;
				Operation.ManifestEscrowState = KingdomTradePhysicalState.Proved;
			}
			if (Operation.ManifestEscrowState != KingdomTradePhysicalState.Proved
				|| manifest.EscrowDrams != Operation.ManifestEscrowAfter)
			{
				Operation.ManifestEscrowState = KingdomTradePhysicalState.Lost;
				return QuarantineFalse(Operation, "Manifest escrow debit proof was lost.");
			}
			if (manifest.EscrowDrams == 0) manifest.Status = KingdomTradeManifestStatus.Delivered;
			return true;
		}

		private static bool SettleRetainedAccounting(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			if (Book == null || Operation == null) return false;
			if (Operation.RetainedState == KingdomTradePhysicalState.Prepared)
				Operation.RetainedState = KingdomTradePhysicalState.Intent;
			if (Operation.RetainedState == KingdomTradePhysicalState.Intent)
			{
				long after;
				bool apply;
				if (!KingdomTradeRules.TryReconcileRetained(Operation.RetainedBefore,
					Operation.RetainedDelta, Book.RetainedEscrowDrams, out after, out apply)
					|| after != Operation.RetainedAfter)
				{
					Operation.RetainedState = KingdomTradePhysicalState.Lost;
					return QuarantineFalse(Operation,
						"Retained escrow is neither exact before nor exact after; value remains unresolved.");
				}
				if (apply) Book.RetainedEscrowDrams = after;
				Operation.RetainedState = KingdomTradePhysicalState.Proved;
			}
			if (Operation.RetainedState != KingdomTradePhysicalState.Proved
				|| Book.RetainedEscrowDrams != Operation.RetainedAfter)
			{
				Operation.RetainedState = KingdomTradePhysicalState.Lost;
				return QuarantineFalse(Operation, "Retained escrow proof was lost.");
			}
			return true;
		}

		private static bool PublishProjectionRow(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			KingdomTradeProjectionRow current;
			if (!TryProjectionRow(Book, Operation.SettlementId, out current)) return false;
			if (string.IsNullOrEmpty(Operation.PriorProjectionId))
			{
				if (current != null || Book.Projections.Count >= KingdomTradeRules.MaxProjectionRows)
					return false;
				Book.Projections.Add(new KingdomTradeProjectionRow
					{
						OperationSequence = Operation.Sequence,
						OperationId = Operation.Id,
					SettlementId = Operation.SettlementId,
					ZoneId = Operation.ZoneId,
					ProjectionId = Operation.ProjectionId,
					ObjectId = Operation.ProjectionObjectId
				});
				return true;
			}
			if (current == null || current.Quarantined
				|| !string.Equals(current.ZoneId, Operation.PriorProjectionZoneId,
					StringComparison.Ordinal)
				|| !string.Equals(current.ProjectionId, Operation.PriorProjectionId,
					StringComparison.Ordinal)
				|| !string.Equals(current.ObjectId, Operation.PriorProjectionObjectId,
					StringComparison.Ordinal)) return false;
			current.ZoneId = Operation.ZoneId;
			current.OperationSequence = Operation.Sequence;
			current.OperationId = Operation.Id;
			current.ProjectionId = Operation.ProjectionId;
			current.ObjectId = Operation.ProjectionObjectId;
			return true;
		}

		private static bool SettleStanding(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			KingdomTradeStandingCas standing = Operation.Standing;
			if (standing == null) return true;
			if (!ExactAuthority(Frame, KingdomTradePhase.DomainIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The standing frame changed before its exact callback.");
			int current = System.GetStanding(standing.Faction);
			if (current == standing.After)
			{
				standing.State = KingdomTradePhysicalState.Proved;
				return true;
			}
			if (current != standing.Before)
			{
				return QuarantineFalse(Operation,
					"Standing changed outside the frozen before/delta/after CAS; it was not overwritten.");
			}
			standing.State = KingdomTradePhysicalState.Intent;
			CallbackWitness callback = CaptureCallbackWitness(Frame);
			if (callback == null)
			{
				standing.State = KingdomTradePhysicalState.Lost;
				return QuarantineFalse(Operation, "Standing callback frame could not be frozen.");
			}
			System.SetStanding(standing.Faction, standing.After);
			if (!ExactCallbackWitness(Frame, callback)
				|| !ReferenceEquals(Frame.System.TradeBook, Frame.Book)
				|| !ReferenceEquals(Frame.Book.OpenOperation, Operation)
				|| Operation.Phase != KingdomTradePhase.DomainIntent)
				return FailDetachedAuthority(Frame,
					"A standing callback detached its official trade authority.");
			if (standing.State != KingdomTradePhysicalState.Intent
				|| System.GetStanding(standing.Faction) != standing.After
				|| !ExactStandingWithOverride(Frame, standing.Faction, standing.After)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone)
				|| !ExactSettlement(Frame))
				return QuarantineFalse(Operation, "Standing CAS did not leave its exact after value.");
			Frame.StandingRows[standing.Faction] = standing.After;
			standing.State = KingdomTradePhysicalState.Proved;
			return ExactAuthority(Frame, KingdomTradePhase.DomainIntent);
		}

		private static bool ExactManifest(KingdomTradeManifestState Manifest,
			KingdomTradeOperation Operation)
		{
			return ExactManifestIdentity(Manifest, Operation)
				&& string.Equals(Manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
				&& Manifest.EscrowDrams == Operation.RequestedWater;
		}

		private static bool ExactManifestIdentity(KingdomTradeManifestState Manifest,
			KingdomTradeOperation Operation)
		{
			return Manifest != null && Operation != null
				&& (Manifest.Status == KingdomTradeManifestStatus.InFlight
					|| Manifest.Status == KingdomTradeManifestStatus.Delivered)
				&& string.Equals(Manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
				&& string.Equals(Manifest.OriginId, Operation.OriginId, StringComparison.Ordinal)
				&& string.Equals(Manifest.OriginName, Operation.OriginName, StringComparison.Ordinal)
				&& string.Equals(Manifest.DestinationId, Operation.DestinationId, StringComparison.Ordinal)
				&& string.Equals(Manifest.DestinationName, Operation.DestinationName, StringComparison.Ordinal);
		}

		private static void BuildOutbox(KingdomSystem System, KingdomTradeOperation Operation)
		{
			if (Operation.Outbox != null) return;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string origin = KingdomPresentation.Rich(Operation.OriginName);
			string destination = KingdomPresentation.Rich(Operation.DestinationName);
			string chronicle = null;
			string ledger = null;
			string message = null;
			string deed = null;
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.CharterDelivery:
				string faction = FactionDisplay(Operation.Faction);
				chronicle = ((Operation.Cycles > 1) ? Operation.Cycles + " caravans of " : "a caravan of ")
					+ faction + " came to " + realm + " and delivered "
					+ Operation.ProvedWater + " drams under charter";
				ledger = "{{G|" + ((Operation.Cycles > 1) ? Operation.Cycles + " caravans of " : "A caravan of ")
					+ faction + " came under charter: " + Operation.ProvedWater + " drams"
					+ (Operation.ProvedWater < Operation.RequestedWater ? ", with the unplaced water retained by the caravan" : "")
					+ (Operation.MaterialRequested > Operation.MaterialProved ? "; some material remained quarantined" : "") + ".}}";
				message = "{{G|A chartered caravan of " + faction + " arrived.}}";
				deed = "the caravans that come to " + realm;
				break;
			case KingdomTradeOperationKind.ManifestLoad:
				chronicle = "the water-keepers of " + origin + " sent "
					+ Operation.ProvedWater + " drams toward " + destination;
				ledger = "{{G|" + Operation.ProvedWater + " drams left " + origin
					+ " under exact manifest " + Operation.ManifestId + ".}}";
				message = "{{G|" + Operation.ProvedWater + " drams leave the stores of "
					+ origin + ", bound for " + destination
					+ ".}} The road is given " + KingdomManifestRules.ManifestWindowDays
					+ " days; only exact proved placement can reduce its escrow.";
				break;
			case KingdomTradeOperationKind.ManifestDelivery:
				chronicle = Operation.ProvedWater > 0 ? "water sent from " + origin
					+ " reached " + destination + ": " + Operation.ProvedWater
					+ " drams entered its exact stores" : null;
				ledger = Operation.ProvedWater > 0 ? "{{G|A manifest from " + origin
					+ " delivered " + Operation.ProvedWater + " drams; "
					+ (Operation.RequestedWater - Operation.ProvedWater) + " remain in escrow.}}" : null;
				message = Operation.ProvedWater > 0 ? "{{G|The manifest carters have arrived.}}" : null;
				deed = Operation.ProvedWater > 0 ? "the water that reached "
					+ destination + " from " + origin : null;
				break;
			case KingdomTradeOperationKind.ManifestTurnback:
				chronicle = KingdomManifestRules.ManifestTurnedBackDeed(origin,
					destination, Operation.RequestedWater);
				ledger = "{{y|" + chronicle + ".}}";
				message = "{{y|The manifest turns back with all " + Operation.RequestedWater
					+ " escrowed drams still on its carts.}}";
				break;
			case KingdomTradeOperationKind.ManifestLapse:
				chronicle = "the twice-spent manifest road closed, and " + Operation.RequestedWater
					+ " drams remained retained under " + Operation.ManifestId;
				ledger = "{{y|The manifest road closed. Its " + Operation.RequestedWater
					+ " drams remain retained under permanent receipt; none were destroyed or reissued.}}";
				message = "{{y|The manifest road has closed; its escrow is retained for inspection.}}";
				break;
			}
			Operation.Outbox = new KingdomTradeOutbox
			{
				EventId = Operation.Id,
				Chronicle = chronicle,
				ChronicleState = chronicle == null ? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending,
				LedgerNote = ledger,
				LedgerDeliveredDelta = Operation.Kind == KingdomTradeOperationKind.CharterDelivery
					|| Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
					? Operation.ProvedWater : 0,
				LedgerState = ledger == null && Operation.ProvedWater == 0
					? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending,
				Message = message,
				MessageState = message == null ? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending,
				Deed = deed,
				DeedState = deed == null ? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending
			};
		}

		private static bool DispatchOutbox(KingdomSystem System, KingdomTradeOperation Operation,
			TradeLiveFrame Frame)
		{
			KingdomTradeOutbox box = Operation.Outbox;
			if (box == null || Frame == null) return false;
			KingdomTradePhase expectedPhase = Operation.Phase;
			if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
			if (box.ChronicleState == KingdomTradeSinkState.Pending)
			{
				string eventId = box.EventId;
				string chronicle = box.Chronicle;
				box.ChronicleState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Chronicle callback frame could not be frozen.");
				bool delivered = KingdomChronicle.RecordOnce(System,
					eventId + ":chronicle", chronicle);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactSinkFrame(Frame, Operation, box, expectedPhase)
					|| box.ChronicleState != KingdomTradeSinkState.Intent
					|| !string.Equals(box.EventId, eventId, StringComparison.Ordinal)
					|| !string.Equals(box.Chronicle, chronicle, StringComparison.Ordinal))
					return SinkFrameFailed(Frame, Operation,
						"The chronicle callback changed its exact trade sink frame.");
				box.ChronicleState = delivered
					? KingdomTradeSinkState.Delivered : KingdomTradeSinkState.Lost;
			}
			if (box.LedgerState == KingdomTradeSinkState.Pending)
			{
				if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
				int deliveredBefore = Frame.LedgerDelivered;
				string note = box.LedgerNote;
				int delta = box.LedgerDeliveredDelta;
				box.LedgerState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Ledger callback frame could not be frozen.");
				Frame.Ledger.Delivered = KingdomTradeRules.SaturatingAdd(
					deliveredBefore, delta);
				if (!string.IsNullOrEmpty(note)) Frame.Ledger.Note(note);
				if (!ExactCallbackWitness(Frame, callback)
					|| box.LedgerState != KingdomTradeSinkState.Intent
					|| !ExactLedgerAfter(Frame, deliveredBefore, delta, note)
					|| !ReferenceEquals(Operation.Outbox, box)
					|| !ExactSettlement(Frame) || !ExactPhysicalFrame(Frame,
						Operation, Frame.Zone))
					return SinkFrameFailed(Frame, Operation,
						"The exact settlement ledger CAS did not match its frozen delta and note.");
				Frame.LedgerDelivered = Frame.Ledger.Delivered;
				Frame.LedgerNoteRows = Frame.LedgerNotes.ToArray();
				box.LedgerState = KingdomTradeSinkState.Delivered;
			}
			if (box.MessageState == KingdomTradeSinkState.Pending)
			{
				if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
				string message = box.Message;
				box.MessageState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Message callback frame could not be frozen.");
				MessageQueue.AddPlayerMessage(message);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactSinkFrame(Frame, Operation, box, expectedPhase)
					|| box.MessageState != KingdomTradeSinkState.Intent
					|| !string.Equals(box.Message, message, StringComparison.Ordinal))
					return SinkFrameFailed(Frame, Operation,
						"The player-message callback changed its exact trade sink frame.");
				box.MessageState = KingdomTradeSinkState.Delivered;
			}
			if (box.DeedState == KingdomTradeSinkState.Pending)
			{
				if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
				string deed = box.Deed;
				box.DeedState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Deed callback frame could not be frozen.");
				System.RecordDeed(deed);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactSinkFrame(Frame, Operation, box, expectedPhase)
					|| box.DeedState != KingdomTradeSinkState.Intent
					|| !string.Equals(box.Deed, deed, StringComparison.Ordinal)
					|| !string.Equals(System.LastDeed, deed, StringComparison.Ordinal))
					return SinkFrameFailed(Frame, Operation,
						"The deed sink changed its exact trade frame.");
				box.DeedState = KingdomTradeSinkState.Delivered;
			}
			return true;
		}

		private static bool ExactSinkFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, KingdomTradeOutbox Outbox,
			KingdomTradePhase ExpectedPhase)
		{
			return ReferenceEquals(Operation?.Outbox, Outbox)
				&& ExactAuthority(Frame, ExpectedPhase)
				&& ExactPhysicalFrame(Frame, Operation, Frame.Zone);
		}

		private static bool SinkFrameFailed(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, string Fault)
		{
			if (Frame == null || !ReferenceEquals(Frame.System?.TradeBook, Frame.Book)
				|| !ReferenceEquals(Frame.Book?.OpenOperation, Operation))
				return FailDetachedAuthority(Frame, Fault);
			Quarantine(Operation, Fault);
			return false;
		}

		private static bool ExactLedgerAfter(TradeLiveFrame Frame, int Before,
			int Delta, string Note)
		{
			if (Frame == null || Frame.Ledger == null
				|| !ReferenceEquals(Frame.System.Ledger, Frame.Ledger)
				|| !ReferenceEquals(Frame.Ledger.Notes, Frame.LedgerNotes)
				|| Frame.LedgerNotes == null || Frame.LedgerNoteRows == null
				|| Frame.Ledger.Delivered != KingdomTradeRules.SaturatingAdd(Before, Delta))
				return false;
			bool append = !string.IsNullOrEmpty(Note) && Frame.LedgerNoteRows.Length < 12;
			int expected = Frame.LedgerNoteRows.Length + (append ? 1 : 0);
			if (Frame.LedgerNotes.Count != expected) return false;
			for (int i = 0; i < Frame.LedgerNoteRows.Length; i++)
				if (!string.Equals(Frame.LedgerNotes[i], Frame.LedgerNoteRows[i],
					StringComparison.Ordinal)) return false;
			return !append || string.Equals(Frame.LedgerNotes[expected - 1], Note,
				StringComparison.Ordinal);
		}

		private static bool OutboxSettled(KingdomTradeOutbox Outbox)
		{
			return Outbox != null && KingdomTradeRules.SinkSettled(Outbox.ChronicleState)
				&& KingdomTradeRules.SinkSettled(Outbox.LedgerState)
				&& KingdomTradeRules.SinkSettled(Outbox.MessageState)
				&& KingdomTradeRules.SinkSettled(Outbox.DeedState);
		}

		private static bool ContinuePatternBook(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			KingdomTradePatternReceipt receipt = Operation?.Pattern;
			if (Operation == null || Operation.Kind != KingdomTradeOperationKind.CharterDelivery
				|| receipt == null || !KingdomTradePatternRules.Valid(receipt))
				return QuarantineFalse(Operation,
					"The CharterDelivery pattern receipt was missing or malformed before retirement.");
			if (KingdomTradePatternRules.Terminal(receipt)) return true;

			if (receipt.State == KingdomTradePatternState.Offered
				|| receipt.State == KingdomTradePatternState.ChoiceIntent)
			{
				if (!KingdomTradePatternRules.BeginChoice(receipt)
					|| !ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
					return QuarantineFalse(Operation,
						"The pattern-book choice lost its exact settlement frame.");
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return QuarantineFalse(Operation,
					"The pattern-book choice callback could not be frozen.");
				int pick = KingdomCeremony.PickPatternBook(receipt);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
					return FailDetachedAuthority(Frame,
						"The pattern-book UI callback changed its exact trade authority or city.");
				if (pick < 0 || pick >= receipt.Offers.Count)
				{
					if (!KingdomTradePatternRules.Decline(receipt))
						return QuarantineFalse(Operation,
							"The pattern-book decline did not match its choice intent.");
					return true;
				}
				string failure;
				if (!KingdomTradePatternRules.TrySelect(receipt, pick,
					Frame.KeepersRoster, KingdomPresentation.Rich(Operation.SettlementName), out failure))
				{
					KingdomTradePatternRules.MarkConflict(receipt, failure);
					KingdomLog.Log("trade: pattern-book selection refused: " + failure);
					return true;
				}
			}

			if (receipt.State == KingdomTradePatternState.Selected
				|| receipt.State == KingdomTradePatternState.RosterIntent)
			{
				KingdomTradePatternCasVerdict verdict =
					KingdomTradePatternRules.InspectRoster(receipt, System.KeepersRoster);
				if (verdict == KingdomTradePatternCasVerdict.ThirdValue)
				{
					KingdomTradePatternRules.MarkConflict(receipt,
						"The seated city's stored roster was neither the frozen before nor after value; it was not overwritten.");
					return true;
				}
				if (verdict == KingdomTradePatternCasVerdict.Invalid)
					return QuarantineFalse(Operation,
						"The pattern-book roster CAS evidence was malformed.");
				if (verdict == KingdomTradePatternCasVerdict.Apply)
				{
					if (!KingdomTradePatternRules.MarkRosterIntent(receipt)
						|| !ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
						|| !string.Equals(System.KeepersRoster ?? "", receipt.RosterBefore,
							StringComparison.Ordinal))
						return QuarantineFalse(Operation,
							"The pattern-book roster changed before its exact CAS.");
					System.KeepersRoster = receipt.RosterAfter;
					Frame.KeepersRoster = receipt.RosterAfter;
					if (!ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
						|| !string.Equals(System.City?.SettlementId, Operation.SettlementId,
							StringComparison.Ordinal)
						|| KingdomTradePatternRules.InspectRoster(receipt,
							System.KeepersRoster) != KingdomTradePatternCasVerdict.AlreadyApplied)
						return FailDetachedAuthority(Frame,
							"The exact city-roster CAS did not publish its frozen after value.");
				}
				if (!KingdomTradePatternRules.MarkLearned(receipt))
					return QuarantineFalse(Operation,
						"The pattern-book roster proof could not settle as learned.");
			}

			if (receipt.State == KingdomTradePatternState.Learned
				&& !DispatchPatternSinks(System, Operation, Frame,
					KingdomTradePhase.ScheduleIntent)) return false;
			return KingdomTradePatternRules.Terminal(receipt);
		}

		private static bool DispatchPatternSinks(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame,
			KingdomTradePhase ExpectedPhase)
		{
			KingdomTradePatternReceipt receipt = Operation?.Pattern;
			if (receipt == null || receipt.State != KingdomTradePatternState.Learned
				|| !KingdomTradePatternRules.Valid(receipt)) return false;
			if (receipt.ChronicleState == KingdomTradeSinkState.Pending
				|| receipt.ChronicleState == KingdomTradeSinkState.Intent)
			{
				receipt.ChronicleState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return false;
				bool settled = KingdomChronicle.RecordOnce(System,
					Operation.Id + ":pattern:chronicle", receipt.Chronicle);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, ExpectedPhase)
					|| receipt.ChronicleState != KingdomTradeSinkState.Intent)
					return FailDetachedAuthority(Frame,
						"The pattern-book chronicle callback changed its exact receipt.");
				if (!settled) return false;
				receipt.ChronicleState = KingdomTradeSinkState.Delivered;
			}
			// MessageQueue has no receipt lookup. Intent on re-entry is conservatively lost,
			// while Pending gets exactly one callback attempt in this process.
			if (receipt.MessageState == KingdomTradeSinkState.Intent)
				receipt.MessageState = KingdomTradeSinkState.Lost;
			if (receipt.MessageState == KingdomTradeSinkState.Pending)
			{
				receipt.MessageState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return false;
				MessageQueue.AddPlayerMessage(receipt.Message);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, ExpectedPhase)
					|| receipt.MessageState != KingdomTradeSinkState.Intent)
					return FailDetachedAuthority(Frame,
						"The pattern-book message callback changed its exact receipt.");
				receipt.MessageState = KingdomTradeSinkState.Delivered;
			}
			return KingdomTradePatternRules.Terminal(receipt);
		}

		private static bool SettlePatternForQuarantine(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			if (Operation.Kind != KingdomTradeOperationKind.CharterDelivery) return true;
			KingdomTradePatternReceipt receipt = Operation.Pattern;
			if (receipt == null || !KingdomTradePatternRules.Valid(receipt))
			{
				Operation.Pattern = KingdomTradePatternRules.Conflict(
					"The quarantined charter had no valid frozen pattern-book receipt.");
				return true;
			}
			if (KingdomTradePatternRules.Terminal(receipt)) return true;
			if (receipt.State == KingdomTradePatternState.Offered
				|| receipt.State == KingdomTradePatternState.ChoiceIntent)
			{
				KingdomTradePatternRules.MarkConflict(receipt,
					"The charter was quarantined before its frozen pattern-book choice settled.");
				return true;
			}
			if (receipt.State == KingdomTradePatternState.Selected
				|| receipt.State == KingdomTradePatternState.RosterIntent)
			{
				KingdomTradePatternCasVerdict verdict =
					KingdomTradePatternRules.InspectRoster(receipt, System.KeepersRoster);
				bool exactCity = string.Equals(System.City?.SettlementId,
					Operation.SettlementId, StringComparison.Ordinal);
				if (exactCity && verdict == KingdomTradePatternCasVerdict.AlreadyApplied)
				{
					if (!KingdomTradePatternRules.MarkLearned(receipt)) return false;
				}
				else
				{
					KingdomTradePatternRules.MarkConflict(receipt,
						"The charter was quarantined before its exact city-roster CAS could be proved applied.");
					return true;
				}
			}
			if (receipt.State == KingdomTradePatternState.Learned)
				return DispatchPatternSinks(System, Operation, Frame,
					KingdomTradePhase.Quarantined);
			Operation.Pattern = KingdomTradePatternRules.Conflict(
				"The quarantined charter had an unrecognized pattern-book continuation state.");
			return true;
		}

		private static bool SettleSchedule(KingdomTradeBook Book,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			if (Operation.Kind != KingdomTradeOperationKind.CharterDelivery) return true;
			KingdomTradeCharter charter = null;
			int matches = 0;
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter row = Book.Charters[i];
				if (row == null || !(string.Equals(row.Id, Operation.CharterId,
						StringComparison.Ordinal)
					|| (string.Equals(row.DealKey, Operation.DealKey, StringComparison.Ordinal)
						&& string.Equals(row.Faction, Operation.Faction,
							StringComparison.Ordinal)))) continue;
				matches++;
				charter = row;
			}
			if (matches != 1 || charter == null || charter.Quarantined
				|| !string.Equals(charter.Id, Operation.CharterId, StringComparison.Ordinal)
				|| !string.Equals(charter.DealKey, Operation.DealKey, StringComparison.Ordinal)
				|| !string.Equals(charter.Faction, Operation.Faction, StringComparison.Ordinal))
			{
				QuarantineScheduleAuthority(Book, Operation,
					"The exact charter schedule row disappeared or collided.");
				return false;
			}
			if (!ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The charter schedule frame changed before its exact CAS.");
			if (charter.NextTick == Operation.DueAfter) return true;
			if (charter.NextTick != Operation.DueBefore)
				return QuarantineFalse(Operation,
					"Charter schedule changed outside its frozen before/after CAS; it was not overwritten.");
			charter.NextTick = Operation.DueAfter;
			return ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
				&& ExactPhysicalFrame(Frame, Operation, Frame.Zone)
				&& charter.NextTick == Operation.DueAfter;
		}

		private static void QuarantineScheduleAuthority(KingdomTradeBook Book,
			KingdomTradeOperation Operation, string Fault)
		{
			if (Book?.Charters != null)
			{
				for (int i = 0; i < Book.Charters.Count; i++)
				{
					KingdomTradeCharter row = Book.Charters[i];
					if (row == null || !(string.Equals(row.Id, Operation.CharterId,
							StringComparison.Ordinal)
						|| (string.Equals(row.DealKey, Operation.DealKey,
								StringComparison.Ordinal)
							&& string.Equals(row.Faction, Operation.Faction,
								StringComparison.Ordinal)))) continue;
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, Fault);
				}
			}
			Quarantine(Operation, Fault);
		}

		private static void FinalizeQuarantine(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeOperation Operation, long Now, TradeLiveFrame Frame)
		{
			if (System == null || Book == null || Operation == null
				|| !ReferenceEquals(System.TradeBook, Book)
				|| !ReferenceEquals(Book.OpenOperation, Operation))
			{
				FailDetachedAuthority(Frame, "Detached trade quarantine could not finalize authority.");
				return;
			}
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLoad
				&& Operation.ProvedWater > 0 && Book.Manifest == null)
			{
					Book.Manifest = new KingdomTradeManifestState
					{
						OperationSequence = Operation.Sequence,
						OperationId = Operation.Id,
						Id = Operation.ManifestId,
					OriginId = Operation.OriginId,
					OriginName = Operation.OriginName,
					DestinationId = Operation.DestinationId,
					DestinationName = Operation.DestinationName,
					OriginalDrams = Operation.RequestedWater,
					EscrowDrams = Operation.ProvedWater,
					LoadedTick = Operation.ManifestLoadedTick,
					DeadlineTick = Operation.ManifestDeadlineTick,
					Status = KingdomTradeManifestStatus.Quarantined,
					Fault = Operation.Fault
				};
			}
			if (Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
				&& Book.Manifest != null && string.Equals(Book.Manifest.Id,
					Operation.ManifestId, StringComparison.Ordinal))
			{
				SettleManifestCreditAccounting(Book, Operation);
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = Operation.Fault;
			}
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLapse
				&& Book.Manifest != null && string.Equals(Book.Manifest.Id,
					Operation.ManifestId, StringComparison.Ordinal))
			{
				SettleRetainedAccounting(Book, Operation);
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = Operation.Fault;
			}
			RefreshBookDomain(Frame);
			System.SynchronizeLegacyManifestProjection();
			if (Operation.Outbox == null)
			{
				Operation.Outbox = new KingdomTradeOutbox
				{
					EventId = Operation.Id,
					Chronicle = "trade receipt " + Operation.Id + " was quarantined after proving "
						+ Operation.ProvedWater + " drams; " + (Operation.Fault ?? "physical state is uncertain"),
					ChronicleState = KingdomTradeSinkState.Pending,
					LedgerNote = "{{r|Trade receipt " + Operation.Id + " is quarantined: "
						+ (Operation.Fault ?? "physical state is uncertain") + ". It will not replay.}}",
					LedgerState = KingdomTradeSinkState.Pending,
					Message = "{{r|A trade receipt was quarantined and will not be repeated. Inspect the ledger.}}",
					MessageState = KingdomTradeSinkState.Pending,
					DeedState = KingdomTradeSinkState.Skipped
				};
			}
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& !KingdomTradeRules.CharterOutboxSafeForQuarantineDispatch(Operation))
			{
				QuarantineScheduleAuthority(Book, Operation,
					"The malformed Charter outbox was retained and no external sink was called.");
				return;
			}
			if (!SettlePatternForQuarantine(System, Operation, Frame)) return;
			DispatchOutbox(System, Operation, Frame);
			if (!OutboxSettled(Operation.Outbox)) SettleOutboxAsLost(Operation);
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery)
			{
				QuarantineScheduleAuthority(Book, Operation,
					"The charter receipt was quarantined; its schedule authority was frozen.");
			}
			KingdomTradeRules.Retire(Book, Operation, KingdomTradePhase.Quarantined,
				Now, Operation.Fault);
			System.SynchronizeLegacyManifestProjection();
		}

		private static void SettleOutboxAsLost(KingdomTradeOperation Operation)
		{
			if (Operation.Outbox == null) return;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.ChronicleState))
				Operation.Outbox.ChronicleState = KingdomTradeSinkState.Lost;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.LedgerState))
				Operation.Outbox.LedgerState = KingdomTradeSinkState.Lost;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.MessageState))
				Operation.Outbox.MessageState = KingdomTradeSinkState.Lost;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.DeedState))
				Operation.Outbox.DeedState = KingdomTradeSinkState.Lost;
		}

		private static void Quarantine(KingdomTradeOperation Operation, string Fault)
		{
			Operation.Fault = AppendFault(Operation.Fault, Fault);
			Operation.Phase = KingdomTradePhase.Quarantined;
		}

		private static bool QuarantineFalse(KingdomTradeOperation Operation, string Fault)
		{
			Quarantine(Operation, Fault);
			return false;
		}

		private static string AppendFault(string Existing, string Added)
		{
			if (!string.IsNullOrEmpty(Existing))
			{
				if (Existing.Length > KingdomTradeRules.MaxTextChars || string.IsNullOrEmpty(Added)
					|| Added.Length > KingdomTradeRules.MaxTextChars - Existing.Length - 2)
					return Existing;
				return Existing + "; " + Added;
			}
			if (Added == null || Added.Length <= KingdomTradeRules.MaxTextChars) return Added;
			return Added.Substring(0, KingdomTradeRules.MaxTextChars);
		}

		private static string FactionDisplay(string FactionName)
		{
			Faction faction = Factions.GetIfExists(FactionName);
			return faction == null ? (FactionName ?? "an unknown faction")
				: Faction.GetFormattedName(FactionName);
		}

		internal static KingdomManifest LegacyManifestSnapshot(
			KingdomTradeManifestState Manifest)
		{
			if (Manifest == null) return null;
			return new KingdomManifest
			{
				OriginName = Manifest.OriginName,
				DestinationName = Manifest.DestinationName,
				Drams = Manifest.EscrowDrams,
				LoadedTick = Manifest.LoadedTick,
				DeadlineTick = Manifest.DeadlineTick,
				TurnedBack = Manifest.TurnedBack
			};
		}

		internal static KingdomManifest LegacyManifestSnapshot(KingdomManifest Manifest)
		{
			if (Manifest == null) return null;
			return new KingdomManifest
			{
				OriginName = Manifest.OriginName,
				DestinationName = Manifest.DestinationName,
				Drams = Manifest.Drams,
				LoadedTick = Manifest.LoadedTick,
				DeadlineTick = Manifest.DeadlineTick,
				TurnedBack = Manifest.TurnedBack
			};
		}

		internal static bool LegacyManifestMatches(KingdomManifest Legacy,
			KingdomTradeManifestState Authoritative)
		{
			if (Legacy == null || Authoritative == null)
				return Legacy == null && Authoritative == null;
			return string.Equals(Legacy.OriginName, Authoritative.OriginName,
				StringComparison.Ordinal)
				&& string.Equals(Legacy.DestinationName, Authoritative.DestinationName,
					StringComparison.Ordinal)
				&& Legacy.Drams == Authoritative.EscrowDrams
				&& Legacy.LoadedTick == Authoritative.LoadedTick
				&& Legacy.DeadlineTick == Authoritative.DeadlineTick
				&& Legacy.TurnedBack == Authoritative.TurnedBack;
		}

	}
}
