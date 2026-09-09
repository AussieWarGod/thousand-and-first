using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// How the bootstrap recovers the gap between the grant scope committing four placed bodies and
	/// the receipt publishing their identities, and what it says when the cohort cannot be
	/// completed.
	/// <para>
	/// The gap is real and unavoidable: the scope commits when its verification passes, and the
	/// publish happens after it returns. A failed write, or a save cut across that instant, leaves
	/// four live, marked, unnamed bodies. The recovery is the shipped grant idiom rather than a
	/// second durable fence: a founder's reservation is minted from the receipt's own frozen ground
	/// (profile, zone, food, index) and needs no publication at all, so the ground itself is the
	/// witness. A wake that owes a cohort READS THE GROUND FIRST and adopts what it finds; it
	/// stages a new cohort only when it finds nothing at all.
	/// </para>
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		/// <summary>
		/// The founders' announce-once flag, and the block it is keyed to: the exact receipt wire
		/// the refusal was said about.
		/// <para>
		/// STANDARDS 251-272 wants an <c>…Announced</c> flag cleared when the block lifts. The wire
		/// IS that condition: every publish the cohort makes changes it, so any measured progress
		/// clears the flag by itself, while a refusal that republishes the same bytes (a Stage A
		/// unwind restating Pending) keeps it and stays silent. It is deliberately session-scoped
		/// and NOT durable state: the quickstart's one durable authority is the receipt, and a
		/// refusal that survives a reload is worth saying again on the next load.
		/// </para>
		/// </summary>
		private static string FoundersAnnouncedWire = "";

		private static void AnnounceFoundersOnce(XRLGame Game,
			KingdomQuickstartReceipt Receipt, string Message)
		{
			string wire = KingdomQuickstartRules.Encode(Receipt) ?? "";
			if (string.Equals(wire, FoundersAnnouncedWire, StringComparison.Ordinal)) return;
			FoundersAnnouncedWire = wire;
			MetricsManager.LogError("ThousandAndFirst quickstart founders: " + Message);
			Popup.Show("{{W|Your founding party is not whole.}} " + Message
				+ "\n\nEvery store the quickstart grants is standing and unaffected; only the "
				+ "founders were refused, and nobody is invented to replace them.");
		}

		/// <summary>
		/// Reads the ground for the founder bodies this receipt's own reservations name. Counts
		/// occurrences, not distinct references: a duplicated reservation is not a second proof.
		/// <para>
		/// Returns false only when the scan itself cannot be trusted &#8212; a malformed
		/// reservation, or one index worn by two bodies. A clean scan reports 0 (nothing was ever
		/// committed), four (a committed cohort whose publication was lost), or a number between,
		/// which the caller refuses rather than completing or replacing.
		/// </para>
		/// </summary>
		private static bool TryObserveFounders(Zone Zone, KingdomQuickstartReceipt Receipt,
			out GameObject[] Standing, out int Found, out string Failure)
		{
			Standing = new GameObject[KingdomQuickstartRules.FounderCount];
			Found = 0;
			Failure = "";
			string[] marks = new string[KingdomQuickstartRules.FounderCount];
			for (int i = 0; i < marks.Length; i++)
			{
				marks[i] = KingdomQuickstartRules.FounderMarker(Receipt, i);
				if (string.IsNullOrEmpty(marks[i]))
				{
					Failure = "The founder reservations were malformed.";
					return false;
				}
			}
			if (Zone == null)
			{
				Failure = "The founder scan had no ground to read.";
				return false;
			}
			List<GameObject> objects = Zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)
					|| !item.HasStringProperty(KingdomQuickstartRules.GrantMarkerProperty))
					continue;
				string marker = item.GetStringProperty(
					KingdomQuickstartRules.GrantMarkerProperty, "");
				for (int index = 0; index < marks.Length; index++)
				{
					if (!string.Equals(marker, marks[index], StringComparison.Ordinal)) continue;
					if (Standing[index] != null)
					{
						Failure = "A founder reservation was worn by more than one body.";
						return false;
					}
					Standing[index] = item;
				}
			}
			for (int i = 0; i < Standing.Length; i++) if (Standing[i] != null) Found++;
			return true;
		}

		/// <summary>
		/// Four exact bodies, each wearing its own indexed reservation, each with an identity, and
		/// no two the same. Deliberately does NOT pin the cell: an adopted cohort may have been
		/// standing through turns before its publication was recovered, and a founder walks.
		/// </summary>
		private static bool VerifyFounderCohort(Zone Zone, GameObject[] Cohort,
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
				if (!FounderIsExact(Zone, Cohort[i], Receipt, i))
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
