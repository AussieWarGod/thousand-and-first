using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The founding cohort, in two stages split at the reversibility boundary.
	/// <para>
	/// Stage A is physical and reversible: four bodies and every piece of gear they arrived with
	/// are allocated inside ONE grant scope, so a failure anywhere unwinds all four and their gear
	/// in reverse order and leaves the world byte-identical to before it ran. Stage B is model-only
	/// and irreversible &#8212; citizenship, the roll, the origin tally &#8212; so it runs only
	/// after the four are published by identity, and every step of it is idempotent per founder.
	/// </para>
	/// <para>
	/// The publish between the stages is the durable fence. It names the exact four, so a wake
	/// after a crash finds them without discovering ownership by walking the zone, and it changes
	/// the wire, so the lifecycle's attempted-receipt guard cannot suppress the next wake.
	/// </para>
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		/// <summary>
		/// The authored cast, in index order: a hand, a drifter, a tinker and a physicker. Drawn
		/// from nothing &#8212; these are the shipped settler bodies, not minted replacements.
		/// </summary>
		private static readonly string[] FounderBlueprints =
		{
			"r_KingdomSettlerHand",
			"r_KingdomSettlerDrifter",
			"r_KingdomSettlerTinker",
			"r_KingdomSettlerPhysicker"
		};

		/// <summary>One culture per profile, four names each, in the same index order.</summary>
		private static readonly string[][] FounderNames =
		{
			new string[4] { "Sedge-at-Furrow", "Rushlight-of-Roads", "Gutter-at-Anvil",
				"Willow-at-Bedside" },
			new string[4] { "Terrace-at-Furrow", "Scree-of-Roads", "Flint-at-Anvil",
				"Cairn-at-Bedside" },
			new string[4] { "Chaff-at-Furrow", "Dunelee-of-Roads", "Slag-at-Anvil",
				"Brinewort-at-Bedside" }
		};

		/// <summary>
		/// The one entry point. Reads the receipt, not the branch that called it, so the boot pass
		/// and every later wake run exactly the same code.
		/// </summary>
		private static bool TryRunFounders(XRLGame Game, KingdomSystem System, Zone Zone,
			ref KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			switch (Receipt.FoundersDisposition)
			{
			case KingdomQuickstartFoundersDisposition.Omitted:
			case KingdomQuickstartFoundersDisposition.Faulted:
				// Terminal. Omitted never owed a cohort; Faulted said so once and is done.
				return true;
			case KingdomQuickstartFoundersDisposition.Seeded:
				return VerifyFounders(Zone, Receipt, out Failure);
			case KingdomQuickstartFoundersDisposition.Pending:
				GameObject[] cohort;
				string staged;
				if (!TryStageFounderBodies(Game, Zone, Receipt, out cohort, out staged))
				{
					// Stage A unwound whole. Restating Pending with the ids cleared is a no-op on
					// an already-Pending receipt, and that is the point: the world is exactly as it
					// was, so the next load or zone activation retries from a clean slate.
					string cleared;
					if (!Restate(Game, ref Receipt,
						KingdomQuickstartFoundersDisposition.Pending, null, out cleared))
					{
						Failure = cleared;
						return false;
					}
					MetricsManager.LogError("ThousandAndFirst quickstart founders: " + staged);
					Failure = "The founding cohort could not be raised whole: " + staged;
					return false;
				}
				string[] ids = new string[KingdomQuickstartRules.FounderCount];
				for (int i = 0; i < ids.Length; i++) ids[i] = cohort[i].IDIfAssigned;
				if (!Restate(Game, ref Receipt,
					KingdomQuickstartFoundersDisposition.Seeding, ids, out Failure)) return false;
				break;
			case KingdomQuickstartFoundersDisposition.Seeding:
				break;
			default:
				Failure = "The quickstart receipt carried no lawful founders disposition.";
				return false;
			}
			return TryEnrolFounders(Game, System, Zone, ref Receipt, out Failure);
		}

		/// <summary>
		/// Stage A. ONE scope for the whole cohort, because the scope's rollback walks its
		/// allocations in reverse: each body is allocated before its own gear, so reverse order
		/// takes a body's gear off before the body, which exact-custody removal requires. Any
		/// failure anywhere therefore unwinds all four bodies and all their gear, behind the
		/// quarantine fence, which is exact zero-or-four for the physical half.
		/// </summary>
		private static bool TryStageFounderBodies(XRLGame Game, Zone Zone,
			KingdomQuickstartReceipt Receipt, out GameObject[] Cohort, out string Failure)
		{
			string failure = "";
			GameObject[] cohort = new GameObject[KingdomQuickstartRules.FounderCount];
			bool created = TryCreateFreshGrant(Game, scope =>
			{
				for (int index = 0; index < cohort.Length; index++)
				{
					int slot = index;
					GameObject body = scope.Create(() => GameObject.Create(
						FounderBlueprints[slot]));
					if (!GameObject.Validate(body) || !body.IsCreature || body.IsPlayer()
						|| body.CurrentCell != null)
					{
						failure = "a founding body was not a fresh, unplaced creature";
						return null;
					}
					// Their own gear, registered before the next body is allocated, so the unwind
					// order stays gear-then-body for every founder in the cohort.
					if (!TryRegisterFounderGear(scope, body))
					{
						failure = "a founding body's gear could not be taken into custody";
						return null;
					}
					int x, y;
					if (!KingdomQuickstartRules.TryFounderCell(slot, out x, out y)
						|| !TryPrepareMarked(body, KingdomQuickstartRules.FounderMarker(
							Receipt, slot), out failure)
						|| !TryPlaceGrant(Zone, body, x, y, out failure)) return null;
					cohort[slot] = body;
				}
				return cohort[0];
			}, candidate => ReferenceEquals(candidate, cohort[0])
				&& VerifyFounderPlacement(Zone, cohort, Receipt, out failure),
				out GameObject grant);
			Cohort = created ? cohort : null;
			Failure = created ? "" : FreshGrantFailure(failure);
			return created;
		}

		/// <summary>
		/// Takes every object the body arrived carrying or wearing into the same scope. Nothing is
		/// created here: the factory hands back what the blueprint already made, which is what
		/// makes the reverse unwind able to prove exact custody of each piece.
		/// </summary>
		private static bool TryRegisterFounderGear(
			KingdomQuickstartGrantScope<GameObject> Scope, GameObject Body)
		{
			if (Body.Inventory != null)
			{
				GameObject[] carried = Body.Inventory.Objects.ToArray();
				for (int i = 0; i < carried.Length; i++)
				{
					GameObject item = carried[i];
					if (!GameObject.Validate(item)) return false;
					if (!ReferenceEquals(Scope.Create(() => item), item)) return false;
				}
			}
			if (Body.Body == null) return true;
			foreach (GameObject worn in Body.Body.GetEquippedObjects())
			{
				GameObject item = worn;
				if (!GameObject.Validate(item)) return false;
				if (!ReferenceEquals(Scope.Create(() => item), item)) return false;
			}
			return true;
		}

		/// <summary>
		/// Stage A's own verification: four exact bodies, each on its own reserved cell, each
		/// wearing its own indexed reservation, each with an identity, and no two the same.
		/// </summary>
		private static bool VerifyFounderPlacement(Zone Zone, GameObject[] Cohort,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (Cohort == null || Cohort.Length != KingdomQuickstartRules.FounderCount)
			{
				Failure = "the founding cohort was not four bodies";
				return false;
			}
			for (int i = 0; i < Cohort.Length; i++)
			{
				int x, y;
				if (!KingdomQuickstartRules.TryFounderCell(i, out x, out y)
					|| !ExactRole(Zone, Cohort[i], FounderBlueprints[i], x, y)
					|| !FounderIsExact(Zone, Cohort[i], Receipt, i))
				{
					Failure = "founder " + i + " was not an exact placed body";
					return false;
				}
				for (int j = 0; j < i; j++)
					if (ReferenceEquals(Cohort[i], Cohort[j])
						|| string.Equals(Cohort[i].IDIfAssigned, Cohort[j].IDIfAssigned,
							StringComparison.Ordinal))
					{
						Failure = "two founders were the same body";
						return false;
					}
			}
			return true;
		}

		/// <summary>
		/// What a founder must be at every later boundary. Deliberately does NOT pin the cell: a
		/// founder is a citizen, and a citizen walks. Identity, blueprint, zone and reservation are
		/// what prove they are still the exact four the receipt named.
		/// </summary>
		private static bool FounderIsExact(Zone Zone, GameObject Body,
			KingdomQuickstartReceipt Receipt, int Index)
		{
			return GameObject.Validate(Body) && Zone != null && Body.CurrentZone == Zone
				&& Body.IsCreature && !Body.IsPlayer() && !Body.IsPlayerLed()
				&& !string.IsNullOrEmpty(Body.IDIfAssigned)
				&& string.Equals(Body.Blueprint, FounderBlueprints[Index],
					StringComparison.Ordinal)
				&& ExactMarker(Body, KingdomQuickstartRules.FounderMarker(Receipt, Index));
		}

		/// <summary>Every founder the receipt names still stands, is enrolled, and is on the roll.</summary>
		private static bool VerifyFounders(Zone Zone, KingdomQuickstartReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			for (int i = 0; i < KingdomQuickstartRules.FounderCount; i++)
			{
				GameObject body = Zone?.FindObjectByID(Receipt.FounderObjectIds[i]);
				if (!FounderIsExact(Zone, body, Receipt, i)
					|| !ReceiptOwns(body, Receipt.FounderObjectIds[i])
					|| body.GetIntProperty("KingdomCitizen") != 1
					|| body.GetIntProperty("KingdomBorn") != 1
					|| string.IsNullOrEmpty(body.GetStringProperty("KingdomName"))
					|| !string.Equals(body.GetStringProperty("KingdomOrigin"),
						Receipt.ProfileKey, StringComparison.Ordinal))
				{
					Failure = "A seeded founder was missing, foreign, or off the roll.";
					return false;
				}
			}
			return true;
		}

		/// <summary>The authored name of founder <paramref name="Index"/> in this profile's culture.</summary>
		private static string FounderName(string ProfileKey, int Index)
		{
			int culture = string.Equals(ProfileKey, "marsh", StringComparison.Ordinal) ? 0
				: string.Equals(ProfileKey, "canyon", StringComparison.Ordinal) ? 1 : 2;
			return FounderNames[culture][Index];
		}
	}
}
