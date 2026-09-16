using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomQuickstartCaskFault : IDisposable
	{
		private const string BlueprintName = "r_KingdomCaskRack", ProbeName = "r_TAF_QuickstartCaskFault";
		internal static KingdomQuickstartCaskFault Active { get; private set; }
		private readonly KingdomNativeRegressionContext Context;
		private readonly KingdomQuickstartReceipt Receipt;
		private readonly string ReceiptWire, GameId;
		private readonly string Provenance, Request, Seed;
		private readonly GameObject Player;
		private readonly KingdomSystem System;
		private readonly GameObjectFactory Factory;
		private readonly Dictionary<string, GameObjectBlueprint> Blueprints;
		private readonly GameObjectBlueprint Blueprint;
		private readonly Dictionary<string, GamePartBlueprint> Parts, Original;
		private readonly GamePartBlueprint Probe;
		private readonly GamePartBlueprint.PartReflectionCache Reflector;
		private readonly List<Witness> Originals = new List<Witness>();
		private int Entries, Faults;
		private bool Closed;
		private string Error;
		internal GameObject First { get { return Originals.Count > 0 ? Originals[0].Body : null; } }
		internal GameObject Second { get { return Originals.Count > 1 ? Originals[1].Body : null; } }

		internal KingdomQuickstartCaskFault(KingdomNativeRegressionContext context, KingdomQuickstartReceipt receipt)
		{
			Context = context; Receipt = receipt; ReceiptWire = KingdomQuickstartRules.Encode(receipt);
			GameId = context.Game.GameID; Player = The.Player; System = context.Game.GetSystem<KingdomSystem>();
			Provenance = context.Game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
			Request = context.Game.GetStringGameState(KingdomScenarioNewGameGate.RequestState, null);
			Seed = context.Game.GetStringGameState(KingdomScenarioRealizer.EngineSeedState, null);
			Factory = GameObjectFactory.Factory; Blueprints = Factory?.Blueprints;
			Require(Active == null && receipt.Phase == KingdomQuickstartPhase.Founded
				&& KingdomQuickstartRules.Valid(receipt) && Blueprints != null
				&& Blueprints.TryGetValue(BlueprintName, out Blueprint), "fresh exact cask blueprint unavailable");
			Parts = Blueprint.Parts;
			Require(Parts != null && ReferenceEquals(Blueprint.allparts, Parts) && Parts.Count <= 512
				&& !Parts.ContainsKey(ProbeName), "cask part table is unavailable or already instrumented");
			Original = new Dictionary<string, GamePartBlueprint>(Parts, StringComparer.Ordinal);
			foreach (var row in Original)
				Require(!string.IsNullOrEmpty(row.Key) && row.Value != null && row.Value.T != null
					&& typeof(IPart).IsAssignableFrom(row.Value.T) && row.Value.Name != ProbeName
					&& row.Value.T != typeof(r_TAF_QuickstartCaskFault), "original cask contains malformed or aliased probe part");
			Probe = new GamePartBlueprint(ProbeName); Reflector = Probe.Reflector;
			Owner(); BlueprintExact(false);
			Active = this;
			try { Parts.Add(ProbeName, Probe); BlueprintExact(true); }
			catch { Dispose(); throw; }
		}

		private void Owner()
		{
			Require(!Closed && ReferenceEquals(The.Game, Context.Game) && Context.Game.GameID == GameId
				&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Context.Zone)
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, Context.Zone)
				&& ReferenceEquals(Context.Game.GetSystem<KingdomSystem>(), System) && !(System?.Founded ?? false)
				&& KingdomQuickstartRules.Encode(Receipt) == ReceiptWire
				&& !KingdomNativeRegressionContext.HasQuickstartState(Context.Game)
				&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance)
				&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioNewGameGate.RequestState, Request)
				&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioRealizer.EngineSeedState, Seed)
				&& KingdomScenarioDurableState.ProvesExactInt(KingdomScenarioRealizer.StampedState, KingdomScenarioStateShape.MarkerValue)
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None
				&& KingdomScenarioDurableState.ProvesExactText(KingdomQuickstartNativeProvider.Receipt, "intent"),
				"native cask owner, receipt or fresh authority changed");
		}

		private void BlueprintExact(bool installed)
		{
			Require(ReferenceEquals(GameObjectFactory.Factory, Factory) && ReferenceEquals(Factory.Blueprints, Blueprints)
				&& Blueprints.TryGetValue(BlueprintName, out var current) && ReferenceEquals(current, Blueprint)
				&& Blueprint.Name == BlueprintName && ReferenceEquals(Blueprint.Parts, Parts)
				&& ReferenceEquals(Blueprint.allparts, Parts) && Parts.Count == Original.Count + (installed ? 1 : 0),
				"original cask blueprint identity or part count changed");
			foreach (var row in Original)
				Require(Parts.TryGetValue(row.Key, out var part) && ReferenceEquals(part, row.Value), "original cask part changed");
			Require(installed ? Parts.TryGetValue(ProbeName, out var added) && ReferenceEquals(added, Probe)
				: !Parts.ContainsKey(ProbeName), "native cask probe entry changed");
			Require(Probe.Name == ProbeName && Probe.Namespace == "XRL.World.Parts" && Probe.ChanceOneIn == 1
				&& Probe.T == typeof(r_TAF_QuickstartCaskFault) && Probe.Reflector != null
				&& ReferenceEquals(Probe.Reflector, Reflector) && Reflector.T == typeof(r_TAF_QuickstartCaskFault),
				"native cask probe type changed");
			using (var parameters = Probe.GetParameterStrings().GetEnumerator())
				Require(!parameters.MoveNext(), "native cask probe acquired parameters");
		}

		internal void Mint(r_TAF_QuickstartCaskFault part, BeforeObjectCreatedEvent creation)
		{
			try
			{
				Require(ReferenceEquals(Active, this) && Error == null, "native cask observer lost authority");
				Owner(); BlueprintExact(true);
				GameObject body = part.ParentObject;
				Require(creation != null && ReferenceEquals(creation.Object, body) && Originals.Count < 2,
					"native cask mint count or original reference differs");
				var witness = new Witness(body, part);
				foreach (Witness prior in Originals) Require(!ReferenceEquals(prior.Body, body), "native cask original repeated");
				Require(body.Physics._CurrentCell == null && body.Physics._InInventory == null
					&& body.Physics._Equipped == null && body.Implantee == null
					&& !body.HasStringProperty(KingdomQuickstartRules.GrantMarkerProperty)
					&& !body.HasIntProperty(KingdomQuickstartRules.GrantMarkerProperty), "factory original is not private and unmarked");
				Originals.Add(witness); Context.Track(body);
			}
			catch (Exception error) { Record(error); }
		}

		internal void Enter(r_TAF_QuickstartCaskFault part, EnteredCellEvent entered)
		{
			try
			{
				Require(ReferenceEquals(Active, this) && Error == null, "native cask observer lost authority");
				Owner(); BlueprintExact(true);
				Require(Originals.Count == Entries + 1 && Entries < 2, "native cask entered event repeated or lacked mint");
				Witness witness = Originals[Entries]; GameObject body = witness.Body;
				Cell cell = Context.Zone.GetCell(KingdomQuickstartRules.WaterCellX, KingdomQuickstartRules.WaterCellY);
				Require(entered != null && ReferenceEquals(entered.Object, body) && ReferenceEquals(part.ParentObject, body)
					&& ReferenceEquals(entered.Cell, cell), "entered event does not name original cask and role cell");
				witness.Exact();
				Require(ReferenceEquals(body.Physics._CurrentCell, cell) && body.Physics._InInventory == null
					&& body.Physics._Equipped == null && body.Implantee == null && Count(cell.Objects, body) == 1
					&& Count(Context.Zone.GetObjects(), body) == 1 && !string.IsNullOrEmpty(body.IDIfAssigned)
					&& body.GetStringProperty(KingdomQuickstartRules.GrantMarkerProperty)
						== KingdomQuickstartRules.GrantMarker(Receipt, KingdomQuickstartPhase.WaterStocked)
					&& !body.HasIntProperty(KingdomQuickstartRules.GrantMarkerProperty)
					&& body.GetIntProperty("KingdomStores") == 1 && body.GetIntProperty("KingdomBuilt") == 0
					&& !body.HasPart("LiquidProducer") && witness.Volume.MaxVolume == 64
					&& witness.Volume.Volume == KingdomQuickstartRules.StarterWaterDrams && KingdomLiquids.HasFreshWater(witness.Volume),
					"entered cask was not the exact healthy placed production grant");
				Owner(); BlueprintExact(true); witness.Exact();
				witness.PlacedId = body.IDIfAssigned;
				Entries++;
				if (Entries == 1)
				{
					Faults++; witness.Volume.MaxVolume = 32;
					Require(witness.Volume.MaxVolume == 32 && witness.Volume.Volume == KingdomQuickstartRules.StarterWaterDrams,
						"native cask capacity injection did not preserve physical stock");
				}
			}
			catch (Exception error) { Record(error); }
		}

		internal void Check(int mints, int entries)
		{
			Owner(); BlueprintExact(true);
			Require(Error == null && ReferenceEquals(Active, this) && Originals.Count == mints
				&& Entries == entries && Faults == 1, "native cask callback counts or fault evidence differ: " + Error);
			if (mints == 2)
			{
				Originals[1].Exact();
				Require(Originals[0].PlacedId != Originals[1].PlacedId
					&& Second.IDIfAssigned == Originals[1].PlacedId && Originals[1].Volume.MaxVolume == 64,
					"retry lost its distinct identity or original capacity");
			}
		}

		public void Dispose()
		{
			if (Closed) return;
			Exception failure = null;
			try { Owner(); BlueprintExact(true); Require(ReferenceEquals(Active, this), "native cask observer was replaced"); }
			catch (Exception error) { failure = error; }
			try
			{
				// Remove only our exact entry from the retained original table; never repair foreign definitions.
				if (Parts.TryGetValue(ProbeName, out var added) && ReferenceEquals(added, Probe)) Parts.Remove(ProbeName);
				BlueprintExact(false);
			}
			catch (Exception error) { if (failure == null) failure = error; }
			finally { Closed = true; if (ReferenceEquals(Active, this)) Active = null; }
			if (failure != null) throw new InvalidOperationException("Native cask blueprint restoration unproved.", failure);
		}

		private void Record(Exception error) { if (Error == null) Error = error.GetType().Name; }
		private static int Count(IEnumerable<GameObject> rows, GameObject body)
		{
			int count = 0; foreach (GameObject row in rows) if (ReferenceEquals(row, body)) count++; return count;
		}
		private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }

		private sealed class Witness
		{
			internal readonly GameObject Body;
			internal readonly LiquidVolume Volume;
			internal string PlacedId;
			private readonly Physics Physics;
			private readonly IPart[] Parts;
			private readonly string InitialId;
			internal Witness(GameObject body, r_TAF_QuickstartCaskFault probe)
			{
				Require(GameObject.Validate(body) && body.Blueprint == BlueprintName && body.PartsList != null
					&& body.PartsList.Count <= 256, "factory original body is malformed");
				Body = body; Physics = body.Physics; Volume = body.GetPart<LiquidVolume>(); InitialId = body.IDIfAssigned;
				Parts = new IPart[body.PartsList.Count]; int probes = 0, physics = 0, volumes = 0;
				for (int i = 0; i < Parts.Length; i++)
				{
					Parts[i] = body.PartsList[i];
					for (int j = 0; j < i; j++) Require(!ReferenceEquals(Parts[i], Parts[j]), "factory part repeated");
					if (ReferenceEquals(Parts[i], probe)) probes++;
					if (Parts[i] is Physics) physics++;
					if (Parts[i] is LiquidVolume) volumes++;
				}
				Require(probes == 1 && physics == 1 && volumes == 1 && Physics != null && Volume != null,
					"factory cask parts are not exact and unique");
				Exact();
			}
			internal void Exact()
			{
				Require(GameObject.Validate(Body) && Body.Blueprint == BlueprintName && Body.PartsList.Count == Parts.Length
					&& ReferenceEquals(Body.Physics, Physics) && ReferenceEquals(Body.GetPart<LiquidVolume>(), Volume)
					&& (InitialId == null || InitialId == Body.IDIfAssigned), "original cask identity or parts changed");
				for (int i = 0; i < Parts.Length; i++)
					Require(ReferenceEquals(Body.PartsList[i], Parts[i]) && ReferenceEquals(Parts[i]?.ParentObject, Body),
						"original cask part reference or parent changed");
			}
		}
	}
}

namespace XRL.World.Parts
{
	[Serializable]
	public sealed class r_TAF_QuickstartCaskFault : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeObjectCreatedEvent.ID || ID == EnteredCellEvent.ID;
		}
		public override bool HandleEvent(BeforeObjectCreatedEvent E)
		{
			ThousandAndFirst.Harness.KingdomQuickstartCaskFault.Active?.Mint(this, E);
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(EnteredCellEvent E)
		{
			ThousandAndFirst.Harness.KingdomQuickstartCaskFault.Active?.Enter(this, E);
			return base.HandleEvent(E);
		}
	}
}
