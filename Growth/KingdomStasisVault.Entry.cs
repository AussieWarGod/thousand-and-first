using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomStasisVault
	{
		internal static bool TryEnter(r_KingdomStasisVault Vault, GameObject Subject,
			out string Failure)
		{
			Failure = "";
			GameObject root = Vault?.ParentObject;
			Zone zone = root?.CurrentZone;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string realm = "";
			string settlement = "";
			bool identity = system != null
				&& system.TryGetCurrentIdentity(out realm, out settlement);
			GameObject body = CurrentDominator(Subject);
			Dominated dominated = Subject?.GetEffect<Dominated>();
			string lot = LotOf(root);
			List<GameObject> cradles = Cradles(root, true);
			GameObject cradle = null;
			int slot = -1;
			for (int i = 0; i < cradles.Count; i++)
			{
				if (body?.CurrentCell != cradles[i].CurrentCell) continue;
				cradle = cradles[i];
				slot = cradle.GetIntProperty(BayIndexProperty) - 1;
				break;
			}
			KingdomStasisCustodyReceipt existing = Slot(Vault, slot);
			bool empty = existing == null
				|| existing.Phase == KingdomStasisCustodyPhase.Released;
			bool held = body?.GetPart<r_KingdomStasisCustody>() != null
				|| BodyNamedInOpenSlot(Vault, body?.IDIfAssigned);
			bool foreign = cradle != null && (cradle.GetPart<Stasisfield>() != null
				|| cradle.GetPart<r_KingdomStasisProjection>() != null
				|| cradle.HasEffect<Phased>());
			bool inPhase = body != null && body.GetPhase() == 1
				&& !body.HasEffect<Phased>() && !body.HasEffect<Omniphase>();
			KingdomStasisVaultVerdict verdict = KingdomStasisVaultRules.JudgeEntry(
				system != null && system.Founded,
				zone != null && system?.ClaimedZones != null
					&& system.ClaimedZones.Contains(zone.ZoneID),
				root != null && root.GetPart<r_KingdomStasisVault>() == Vault
					&& root.GetIntProperty("KingdomBuilt") == 1
					&& !string.IsNullOrEmpty(lot),
				Subject != null && Subject.IsPlayer() && dominated != null,
				body != null && body.IsCreature && body.CurrentZone == zone,
				cradle != null && cradles.Count == KingdomStasisVaultRules.MaxSlots
					&& slot >= 0 && slot < KingdomStasisVaultRules.MaxSlots,
				empty, held, body != null && body.IsInStasis(), inPhase,
				CradleCellClear(cradle, body), foreign,
				identity && BoundedRuntimeIdentity(root, cradle, body, Subject, realm,
					settlement, zone, lot));
			if (verdict != KingdomStasisVaultVerdict.Allowed)
			{
				Failure = Refusal(verdict);
				return false;
			}
			if (Vault.NextGeneration <= 0 || Vault.NextGeneration == int.MaxValue)
			{
				Failure = "The vault's custody generation is exhausted; no body was touched.";
				return false;
			}
			int generation = Vault.NextGeneration++;
			KingdomStasisCustodyReceipt receipt;
			if (!KingdomStasisVaultRules.TryPrepare(slot, generation, realm, settlement,
				zone.ZoneID, root.IDIfAssigned, lot, cradle.IDIfAssigned, body.IDIfAssigned, Subject.IDIfAssigned,
				body.Blueprint, body.ShortDisplayNameStripped,
				InventoryFingerprint(body), EquipmentFingerprint(body),
				EffectFingerprint(body), Math.Max(0L, The.Game?.TimeTicks ?? 0L),
				out receipt, out Failure)) return false;
			Put(Vault, receipt);
			try
			{
				r_KingdomStasisCustody marker = new r_KingdomStasisCustody();
				marker.Stamp(receipt);
				body.AddPart(marker);
				r_KingdomStasisProjection projection = new r_KingdomStasisProjection();
				projection.Stamp(receipt);
				cradle.AddPart(projection);
			}
			catch (Exception ex)
			{
				Failure = "Stasis receipt projection threw " + ex.GetType().Name + ".";
				AbortEntry(Vault, receipt, body, cradle, Failure);
				return false;
			}
			if (!ExactMarkers(receipt, body, cradle))
			{
				Failure = "The exact body and cradle receipts did not both persist.";
				AbortEntry(Vault, receipt, body, cradle, Failure);
				return false;
			}
			if (!ProjectNativeField(Vault, receipt, body, cradle, out Failure))
			{
				AbortEntry(Vault, receipt, body, cradle, Failure);
				return false;
			}
			return true;
		}

		private static bool BodyNamedInOpenSlot(r_KingdomStasisVault Vault,
			string BodyId)
		{
			if (string.IsNullOrEmpty(BodyId) || Vault?.Slots == null) return false;
			for (int i = 0; i < Vault.Slots.Count; i++)
				if (Vault.Slots[i].BodyObjectId == BodyId
					&& Vault.Slots[i].Phase != KingdomStasisCustodyPhase.Released) return true;
			return false;
		}

		private static bool BoundedRuntimeIdentity(GameObject Vault, GameObject Cradle,
			GameObject Body, GameObject Subject, string Realm, string Settlement,
			Zone Zone, string Lot)
		{
			return !string.IsNullOrEmpty(Realm) && !string.IsNullOrEmpty(Settlement)
				&& !string.IsNullOrEmpty(Zone?.ZoneID) && !string.IsNullOrEmpty(Lot)
				&& !string.IsNullOrEmpty(Vault?.IDIfAssigned) && !string.IsNullOrEmpty(Cradle?.IDIfAssigned)
				&& !string.IsNullOrEmpty(Body?.IDIfAssigned) && !string.IsNullOrEmpty(Subject?.IDIfAssigned)
				&& !string.IsNullOrEmpty(Body?.Blueprint)
				&& !string.IsNullOrEmpty(Body?.ShortDisplayNameStripped);
		}

		private static string Refusal(KingdomStasisVaultVerdict Verdict)
		{
			switch (Verdict)
			{
			case KingdomStasisVaultVerdict.NotDominating:
				return "Dominate another body first; your dormant body must already lie on one cradle.";
			case KingdomStasisVaultVerdict.DominatorMissing:
				return "The exact dormant body and its domination link cannot be proved in this vault.";
			case KingdomStasisVaultVerdict.WrongCradle:
				return "Lay the dormant body on one of this vault's four exact cradles.";
			case KingdomStasisVaultVerdict.NoEmptyBay:
				return "That physical bay still owns an open custody receipt.";
			case KingdomStasisVaultVerdict.BodyAlreadyHeld:
				return "That body already belongs to an open stasis custody.";
			case KingdomStasisVaultVerdict.BodyAlreadyStilled:
				return "That body is already in a stasis field this vault does not own.";
			case KingdomStasisVaultVerdict.BodyOutOfPhase:
				return "A phased, omniphase, or nullphase body cannot enter the isolated bay.";
			case KingdomStasisVaultVerdict.CradleOccupied:
				return "Another phase-matching object occupies that cradle cell.";
			case KingdomStasisVaultVerdict.ForeignProjection:
				return "That cradle already carries a field, phase, or receipt the vault does not own.";
			case KingdomStasisVaultVerdict.WrongGround:
				return "Stasis custody operates only on the seated city's currently held ground.";
			default:
				return "The exact built vault, realm, or body identity cannot be proved; nothing changed.";
			}
		}
	}
}
