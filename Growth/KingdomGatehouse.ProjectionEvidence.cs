using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private const int MaxProjectionScanObjects = 16384;

		private static bool TryDriveProjectionSlot(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, out string Failure)
		{
			Failure = null;
			if (Z == null || Plan == null
				|| (Plan.ReceiptVersion != 1 && Part == null)
				|| !KingdomGatehouseRules.TrySatellite(Plan, Index, out KingdomGatehouseCell spec))
				return FailSlot("The frozen gatehouse topology is incomplete.", out Failure);
			if (Plan.ReceiptVersion == 1)
				return TryDriveLegacyProjectionSlot(Root, RootCell, Job, Scaffold, Z,
					Plan, PlanReceipt, Part, Index, spec, out Failure);
			string expectedId = KingdomGatehouseProjectionRules.StableSatelliteId(
				Root.IDIfAssigned, PlanReceipt, Index);
			string idKey = SatelliteIdProperty(Index);
			string stateKey = SatelliteStateProperty(Index);
			if (string.IsNullOrEmpty(expectedId) || Root.HasIntProperty(idKey)
				|| Root.HasStringProperty(stateKey))
				return FailSlot("A gatehouse satellite receipt has the wrong value type.", out Failure);

			for (int transition = 0; transition < 5; transition++)
			{
				string id = Root.GetStringProperty(idKey);
				int rawState = Root.GetIntProperty(stateKey);
				if (rawState < 0 || rawState > (int)KingdomGatehouseSlotState.Contested
					|| (!string.IsNullOrEmpty(id) && id != expectedId))
					return ContestSlot(Root, Index,
						"A gatehouse satellite identity or phase is noncanonical.", out Failure);
				KingdomGatehouseSlotState state = (KingdomGatehouseSlotState)rawState;
				if (!TryProjectionEvidence(Root, Z, Plan, Part, Index, expectedId,
					out KingdomGatehouseSlotEvidence evidence, out GameObject item,
					out Failure)) return ContestSlot(Root, Index, Failure, out Failure);
				KingdomGatehouseSlotAction action = KingdomGatehouseProjectionRules.Resolve(
					Index, state, !string.IsNullOrEmpty(id), evidence);
				switch (action)
				{
				case KingdomGatehouseSlotAction.PublishIdentity:
					Root.SetStringProperty(idKey, expectedId);
					if (Root.GetStringProperty(idKey) != expectedId)
						return FailSlot("A gatehouse satellite identity did not persist.", out Failure);
					continue;
				case KingdomGatehouseSlotAction.PublishPending:
					Root.SetIntProperty(stateKey, (int)KingdomGatehouseSlotState.Pending);
					if (Root.GetIntProperty(stateKey) != (int)KingdomGatehouseSlotState.Pending)
						return FailSlot("A gatehouse satellite pending phase did not persist.", out Failure);
					continue;
				case KingdomGatehouseSlotAction.Create:
					if (!TryCreateStagedSatellite(Root, RootCell, Job, Scaffold, Z,
						Plan, PlanReceipt, Part, Index, spec, expectedId, out Failure))
						return false;
					continue;
				case KingdomGatehouseSlotAction.Place:
					return TryPlaceStagedSatellite(Root, RootCell, Job, Scaffold, Z,
						Plan, PlanReceipt, Part, Index, spec, expectedId, item, out Failure);
				case KingdomGatehouseSlotAction.Settle:
					return SettleProjectionSlot(Root, Part, Index, item, out Failure);
				case KingdomGatehouseSlotAction.Verify:
					if (ProjectionCustody(Part, Index) != null
						&& !ReferenceEquals(ProjectionCustody(Part, Index), item))
						return ContestSlot(Root, Index,
							"A settled gatehouse slot retains foreign callback custody.", out Failure);
					return SetProjectionCustody(Part, Index, null)
						|| FailSlot("Settled gatehouse callback custody did not clear.", out Failure);
				default:
					return ContestSlot(Root, Index,
						"A gatehouse satellite is absent, duplicated, foreign, or contested.", out Failure);
				}
			}
			return FailSlot("A gatehouse satellite transition did not converge.", out Failure);
		}

		private static bool TryDriveLegacyProjectionSlot(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part, int Index,
			KingdomGatehouseCell Spec, out string Failure)
		{
			Failure = null;
			string idKey = SatelliteIdProperty(Index);
			string stateKey = SatelliteStateProperty(Index);
			if (Plan?.ReceiptVersion != 1 || Root.HasIntProperty(idKey)
				|| Root.HasStringProperty(stateKey))
				return FailSlot("A legacy gatehouse satellite receipt has the wrong value type.",
					out Failure);
			for (int transition = 0; transition < 4; transition++)
			{
				string id = Root.GetStringProperty(idKey);
				int rawState = Root.GetIntProperty(stateKey);
				if (rawState < 0 || rawState > (int)KingdomGatehouseSlotState.Contested
					|| (!string.IsNullOrEmpty(id)
						&& !KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
							Root.IDIfAssigned, PlanReceipt, Index, id)))
					return ContestSlot(Root, Index,
						"A legacy gatehouse satellite identity or phase is malformed.", out Failure);
				KingdomGatehouseSlotState state = (KingdomGatehouseSlotState)rawState;
				if (state == KingdomGatehouseSlotState.Empty && string.IsNullOrEmpty(id))
				{
					GameObject staged = ProjectionCustody(Part, Index);
					if (staged != null)
					{
						if (!TryAdoptUnpublishedLegacyCustody(Root, RootCell, Job,
							Scaffold, Z, Plan, PlanReceipt, Part, Index, Spec, staged,
							out Failure)) return false;
						continue;
					}
					if (Part == null || KingdomGatehouseProjectionRules.
						ResolveLegacyPublicationCut(Index, state, false, false,
							false, false, false, false, false,
							KingdomGatehouseSlotEvidence.Absent)
							!= KingdomGatehouseLegacyPublicationAction.Create
						|| !TryCreateLegacyStagedSatellite(Root, RootCell,
						Job, Scaffold, Z, Plan, PlanReceipt, Part, Index, Spec, out Failure))
						return false;
					continue;
				}
				if (string.IsNullOrEmpty(id)
					|| !TryProjectionEvidence(Root, Z, Plan, Part, Index, id,
						out KingdomGatehouseSlotEvidence evidence, out GameObject item,
						out Failure))
					return ContestSlot(Root, Index, Failure
						?? "A legacy gatehouse satellite lacks exact stored identity.", out Failure);
				switch (state)
				{
				case KingdomGatehouseSlotState.Empty:
					if (Part == null || KingdomGatehouseProjectionRules.
						ResolveLegacyPublicationCut(Index, state, true, true,
							true, true, true, true, true,
							evidence) != KingdomGatehouseLegacyPublicationAction.PublishPending)
						return ContestSlot(Root, Index,
							"Legacy identity publication lacks its staged body.", out Failure);
					Root.SetIntProperty(stateKey, (int)KingdomGatehouseSlotState.Pending);
					if (Root.GetIntProperty(stateKey)
						!= (int)KingdomGatehouseSlotState.Pending)
						return FailSlot("A legacy pending phase did not persist.", out Failure);
					continue;
				case KingdomGatehouseSlotState.Pending:
					if (evidence == KingdomGatehouseSlotEvidence.Staged && Part != null)
						return TryPlaceStagedSatellite(Root, RootCell, Job, Scaffold, Z,
							Plan, PlanReceipt, Part, Index, Spec, id, item, out Failure);
					if (evidence == KingdomGatehouseSlotEvidence.ExactPlacement && Part != null)
						return SettleProjectionSlot(Root, Part, Index, item, out Failure);
					return ContestSlot(Root, Index,
						"A legacy pending satellite is absent, foreign, or duplicated.", out Failure);
				case KingdomGatehouseSlotState.Settled:
					if (evidence != KingdomGatehouseSlotEvidence.ExactPlacement)
						return ContestSlot(Root, Index,
							"A settled legacy satellite lost exact physical evidence.", out Failure);
					GameObject custody = ProjectionCustody(Part, Index);
					if (custody != null && !ReferenceEquals(custody, item))
						return ContestSlot(Root, Index,
							"A settled legacy slot retains foreign callback custody.", out Failure);
					return Part == null || SetProjectionCustody(Part, Index, null)
						|| FailSlot("Settled legacy callback custody did not clear.", out Failure);
				default:
					return ContestSlot(Root, Index,
						"A legacy gatehouse satellite is contested.", out Failure);
				}
			}
			return FailSlot("A legacy gatehouse satellite transition did not converge.",
				out Failure);
		}

		private static bool TryAdoptUnpublishedLegacyCustody(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part, int Index,
			KingdomGatehouseCell Spec, GameObject Staged, out string Failure)
		{
			Failure = null;
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			string stagedId = GameObject.Validate(Staged) ? Staged.IDIfAssigned : null;
			bool blueprintExact = GameObject.Validate(Staged)
				&& Staged.Blueprint == Spec.Blueprint;
			bool unplaced = GameObject.Validate(Staged) && Staged.CurrentCell == null
				&& Staged.InInventory == null && Staged.Equipped == null;
			bool boundedId = KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
				Root.IDIfAssigned, PlanReceipt, Index, stagedId);
			bool uniqueId = boundedId && LegacyUnpublishedIdentityUnique(Z, Part, Index,
				Staged, stagedId);
			bool compatibleMarks = CompatibleUnpublishedLegacyMarks(Staged, Root,
				Plan, Index, Spec);
			if (KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(Index,
				KingdomGatehouseSlotState.Empty, false,
				ReferenceEquals(ProjectionCustody(Part, Index), Staged), blueprintExact,
				unplaced, boundedId, uniqueId, compatibleMarks,
				KingdomGatehouseSlotEvidence.Foreign)
					!= KingdomGatehouseLegacyPublicationAction.AdoptCustody)
				return ContestSlot(Root, Index,
					"Unpublished legacy callback custody is foreign, landed, duplicated, or malformed.",
					out Failure);
			if (!TryApplySatellitePalette(Staged, Plan, Index))
				return ContestSlot(Root, Index,
					"Unpublished legacy callback custody rejected its frozen palette.", out Failure);
			StampProjectionSatellite(Staged, Root, Plan, Index, Spec);
			if (!ExactProjectionMarks(Staged, Root, Plan, Index, Spec, stagedId)
				|| !LegacyUnpublishedIdentityUnique(Z, Part, Index, Staged, stagedId))
				return ContestSlot(Root, Index,
					"Recovered legacy callback custody did not become one exact staged body.",
					out Failure);
			Root.SetStringProperty(SatelliteIdProperty(Index), stagedId);
			if (Root.GetStringProperty(SatelliteIdProperty(Index)) != stagedId)
				return FailSlot("Recovered legacy identity did not persist.", out Failure);
			Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Pending);
			return Root.GetIntProperty(SatelliteStateProperty(Index))
				== (int)KingdomGatehouseSlotState.Pending
				|| FailSlot("Recovered legacy phase did not persist.", out Failure);
		}

		private static bool LegacyUnpublishedIdentityUnique(Zone Z, IPart Part, int Index,
			GameObject Staged, string StagedId)
		{
			if (!GameObject.Validate(Staged) || string.IsNullOrEmpty(StagedId)
				|| !ReferenceEquals(ProjectionCustody(Part, Index), Staged)
				|| CountLoadedIdentity(Z, StagedId, out _) != 0) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				if (i == Index) continue;
				GameObject other = ProjectionCustody(Part, i);
				if (ReferenceEquals(other, Staged)
					|| (GameObject.Validate(other) && other.IDIfAssigned == StagedId)) return false;
			}
			return true;
		}

		private static bool CompatibleUnpublishedLegacyMarks(GameObject Item,
			GameObject Root, KingdomGatehousePlan Plan, int Index, KingdomGatehouseCell Spec)
		{
			if (!GameObject.Validate(Item) || !GameObject.Validate(Root) || Plan == null
				|| Item.Blueprint != Spec.Blueprint
				|| !CompatibleIntMark(Item, SatelliteProperty, 1)
				|| !CompatibleStringMark(Item, OwnerProperty, Root.IDIfAssigned)
				|| !CompatibleIntMark(Item, IndexProperty, Index)
				|| !CompatibleStringMark(Item, SlotProperty, Spec.Slot)
				|| !CompatibleIntMark(Item, KingdomPlots.PlotPartProperty, 0)) return false;
			if (Index == 0)
				return CompatibleIntMark(Item, ReservationProperty, Schema)
					&& CompatibleIntMark(Item, KingdomPlots.PlotX1Property, Plan.X1)
					&& CompatibleIntMark(Item, KingdomPlots.PlotY1Property, Plan.Y1)
					&& CompatibleIntMark(Item, KingdomPlots.PlotX2Property, Plan.X2)
					&& CompatibleIntMark(Item, KingdomPlots.PlotY2Property, Plan.Y2);
			return !Item.HasIntProperty(ReservationProperty)
				&& !Item.HasStringProperty(ReservationProperty) && !HasPlotRectMark(Item);
		}

		private static bool CompatibleIntMark(GameObject Item, string Key, int Expected)
		{
			return !Item.HasStringProperty(Key)
				&& (!Item.HasIntProperty(Key) || Item.GetIntProperty(Key) == Expected);
		}

		private static bool CompatibleStringMark(GameObject Item, string Key, string Expected)
		{
			return !Item.HasIntProperty(Key)
				&& (!Item.HasStringProperty(Key)
					|| Item.GetStringProperty(Key) == Expected);
		}

		private static bool TryCreateLegacyStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part, int Index,
			KingdomGatehouseCell Spec, out string Failure)
		{
			Failure = null; GameObject item;
			try { item = GameObject.Create(Spec.Blueprint); }
			catch (Exception ex)
			{
				return FailSlot("A legacy gatehouse satellite blueprint threw before staging: "
					+ ex.Message, out Failure);
			}
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			if (!GameObject.Validate(item))
				return FailSlot("A legacy gatehouse satellite blueprint created no object.",
					out Failure);
			string generatedId = item.ID;
			if (!KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
				Root.IDIfAssigned, PlanReceipt, Index, generatedId)
				|| !TryApplySatellitePalette(item, Plan, Index))
				return FailSlot("A legacy gatehouse satellite produced no bounded exact identity.",
					out Failure);
			if (!SetProjectionCustody(Part, Index, item))
				return FailSlot("A legacy gatehouse satellite could not enter serialized custody.",
					out Failure);
			StampProjectionSatellite(item, Root, Plan, Index, Spec);
			if (!ExactProjectionMarks(item, Root, Plan, Index, Spec, generatedId)
				|| !LegacyUnpublishedIdentityUnique(Z, Part, Index, item, generatedId))
				return ContestSlot(Root, Index,
					"A legacy gatehouse satellite could not prove one staged identity.", out Failure);
			Root.SetStringProperty(SatelliteIdProperty(Index), generatedId);
			if (Root.GetStringProperty(SatelliteIdProperty(Index)) != generatedId)
				return FailSlot("A legacy gatehouse satellite identity did not persist.", out Failure);
			Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Pending);
			if (Root.GetIntProperty(SatelliteStateProperty(Index))
				!= (int)KingdomGatehouseSlotState.Pending
				|| !TryProjectionEvidence(Root, Z, Plan, Part, Index, generatedId,
					out KingdomGatehouseSlotEvidence evidence, out GameObject exact,
					out Failure) || evidence != KingdomGatehouseSlotEvidence.Staged
				|| !ReferenceEquals(exact, item))
				return ContestSlot(Root, Index, Failure
					?? "A legacy gatehouse satellite did not retain exact staged custody.",
					out Failure);
			return true;
		}

		private static bool TryCreateStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, KingdomGatehouseCell Spec, string ExpectedId, out string Failure)
		{
			Failure = null; GameObject item;
			try { item = GameObject.Create(Spec.Blueprint); }
			catch (Exception ex)
			{
				return FailSlot("A gatehouse satellite blueprint threw before staging: "
					+ ex.Message, out Failure);
			}
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			if (!GameObject.Validate(item))
				return FailSlot("A gatehouse satellite blueprint created no object.", out Failure);
			if (!TryApplySatellitePalette(item, Plan, Index))
				return FailSlot("A gatehouse satellite could not retain its frozen render palette.",
					out Failure);
			item.ID = ExpectedId;
			StampProjectionSatellite(item, Root, Plan, Index, Spec);
			if (!KingdomGatehouseProjectionRules.CanSerializeDeterministicCustody(
				ExactSatellitePalette(item, Plan, Index), item.IDIfAssigned == ExpectedId,
				ExactProjectionMarks(item, Root, Plan, Index, Spec, ExpectedId)))
				return FailSlot("A gatehouse satellite could not freeze its body before custody.",
					out Failure);
			if (!SetProjectionCustody(Part, Index, item))
				return FailSlot("A gatehouse satellite could not enter serialized custody.", out Failure);
			if (!TryProjectionEvidence(Root, Z, Plan, Part, Index, ExpectedId,
				out KingdomGatehouseSlotEvidence evidence, out GameObject exact,
				out Failure) || evidence != KingdomGatehouseSlotEvidence.Staged
				|| !ReferenceEquals(exact, item))
				return ContestSlot(Root, Index, Failure
					?? "A created gatehouse satellite did not retain exact staged custody.", out Failure);
			return true;
		}

		private static bool TryPlaceStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, KingdomGatehouseCell Spec, string ExpectedId, GameObject Item,
			out string Failure)
		{
			Failure = null; GameObject accepted = null; Exception callback = null;
			try { accepted = Z.GetCell(Spec.X, Spec.Y).AddObject(Item, NoStack: true); }
			catch (Exception ex) { callback = ex; }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, Item, accepted); }
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure)) return false;
			if (!TryProjectionEvidence(Root, Z, Plan, Part, Index, ExpectedId,
				out KingdomGatehouseSlotEvidence evidence, out GameObject exact,
				out Failure)) return ContestSlot(Root, Index, Failure, out Failure);
			if (evidence == KingdomGatehouseSlotEvidence.ExactPlacement
				&& (callback != null || ReferenceEquals(accepted, Item))
				&& ReferenceEquals(exact, Item))
				return SettleProjectionSlot(Root, Part, Index, Item, out Failure);
			if (callback == null && !ReferenceEquals(accepted, Item))
				return ContestSlot(Root, Index,
					"Gatehouse AddObject returned a different physical identity.", out Failure);
			if (evidence == KingdomGatehouseSlotEvidence.Staged)
			{
				if (Plan.ReceiptVersion == 1)
					return FailSlot((callback == null
						? "Legacy gatehouse AddObject refused exact placement."
						: "Legacy gatehouse AddObject threw before exact placement: "
							+ callback.Message)
						+ " Exact serialized custody was retained for retry.", out Failure);
				if (!TryRetireStagedSatellite(Root, RootCell, Job, Scaffold, Z, Plan,
					PlanReceipt, Part, Index, ExpectedId, Item,
					out KingdomGatehouseSlotEvidence after, out Failure)) return false;
				return FailSlot((callback == null ? "Gatehouse AddObject refused exact placement."
					: "Gatehouse AddObject threw before exact placement: " + callback.Message)
					+ (after == KingdomGatehouseSlotEvidence.Staged
						? " Exact serialized custody was retained after cleanup veto." : ""),
					out Failure);
			}
			if (evidence == KingdomGatehouseSlotEvidence.Absent)
			{
				SetProjectionCustody(Part, Index, null);
				return FailSlot("Gatehouse AddObject left the published output exactly absent.",
					out Failure);
			}
			return ContestSlot(Root, Index,
				"Gatehouse AddObject left duplicate or foreign physical evidence.", out Failure);
		}

		private static bool TryRetireStagedSatellite(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, string ExpectedId, GameObject Item,
			out KingdomGatehouseSlotEvidence Evidence, out string Failure)
		{
			Failure = null;
			try { Item.Obliterate(null, Silent: true); }
			catch (Exception) { }
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, Item);
			if (!ProjectionCallbackAuthorityStillExact(Root, RootCell, Job, Scaffold,
				Plan, PlanReceipt, out Failure))
			{
				Evidence = KingdomGatehouseSlotEvidence.Foreign;
				return false;
			}
			TryProjectionEvidence(Root, Z, Plan, Part, Index, ExpectedId,
				out Evidence, out _, out _);
			if (KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true, Evidence))
				SetProjectionCustody(Part, Index, null);
			return true;
		}

		private static bool SettleProjectionSlot(GameObject Root,
			IPart Part,
			int Index, GameObject Item, out string Failure)
		{
			Failure = null;
			Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Settled);
			if (Root.GetIntProperty(SatelliteStateProperty(Index))
				!= (int)KingdomGatehouseSlotState.Settled)
				return FailSlot("A gatehouse satellite settled phase did not persist.", out Failure);
			GameObject custody = ProjectionCustody(Part, Index);
			if (custody != null && !ReferenceEquals(custody, Item))
				return ContestSlot(Root, Index,
					"A gatehouse satellite settled beside foreign callback custody.", out Failure);
			return SetProjectionCustody(Part, Index, null)
				|| FailSlot("A settled gatehouse custody reference did not clear.", out Failure);
		}

		private static void StampProjectionSatellite(GameObject Item, GameObject Root,
			KingdomGatehousePlan Plan, int Index, KingdomGatehouseCell Spec)
		{
			Item.SetIntProperty(SatelliteProperty, 1);
			Item.SetStringProperty(OwnerProperty, Root.IDIfAssigned);
			Item.SetIntProperty(IndexProperty, Index);
			Item.SetStringProperty(SlotProperty, Spec.Slot);
			if (Index != 0) return;
			KingdomPlots.StampRect(Item, new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2));
			Item.SetIntProperty(ReservationProperty, Schema);
		}

		private static bool TryApplySatellitePalette(GameObject Item,
			KingdomGatehousePlan Plan, int Index)
		{
			if (!GameObject.Validate(Item) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			if (!KingdomGatehouseRules.TrySatelliteRender(Plan, Index,
				out string glyph, out string color, out string tileColor,
				out string detail, out string tile)) return false;
			Render render = Item.GetPart<Render>();
			if (render == null) return false;
			render.RenderString = glyph;
			render.ColorString = color;
			render.TileColor = tileColor;
			render.DetailColor = detail;
			if (!string.IsNullOrEmpty(tile)) render.Tile = tile;
			return ExactSatellitePalette(Item, Plan, Index);
		}

		private static bool ExactSatellitePalette(GameObject Item,
			KingdomGatehousePlan Plan, int Index)
		{
			if (!GameObject.Validate(Item) || Plan == null) return false;
			if (Plan.ReceiptVersion != 2) return true;
			Render render = Item.GetPart<Render>();
			return render != null
				&& KingdomGatehouseRules.TrySatelliteRender(Plan, Index,
					out string glyph, out string color, out string tileColor,
					out string detail, out string tile)
				&& render.RenderString == glyph && render.ColorString == color
				&& render.TileColor == tileColor && render.DetailColor == detail
				&& (string.IsNullOrEmpty(tile) || render.Tile == tile);
		}

		private static bool AllProjectionSlotsSettled(GameObject Root,
			IPart Part)
		{
			if (!GameObject.Validate(Root)) return false;
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				if (Root.HasStringProperty(SatelliteStateProperty(i))
					|| !Root.HasIntProperty(SatelliteStateProperty(i))
					|| Root.GetIntProperty(SatelliteStateProperty(i))
						!= (int)KingdomGatehouseSlotState.Settled
					|| ProjectionCustody(Part, i) != null) return false;
			return true;
		}

		private static bool ContestSlot(GameObject Root, int Index, string Reason,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Reason)
				? "A gatehouse satellite carries contested evidence." : Reason;
			if (GameObject.Validate(Root)) Root.SetIntProperty(SatelliteStateProperty(Index),
				(int)KingdomGatehouseSlotState.Contested);
			return false;
		}

		private static bool FailSlot(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}
	}
}
