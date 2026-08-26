using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Reads vanilla <c>GetCulture()</c>/<c>GetSpecies()</c> from real citizen bodies and keeps
	/// the seated settlement's live tallies exact. A body remembers the last fact counted for it,
	/// so witnessing it again, retrying a pass, or moving it between claimed zones cannot mint a
	/// second resident fact.
	/// </summary>
	public static class KingdomResidentIdentity
	{
		public const string CultureProperty = "r_TAF_CountedCulture";
		public const string SpeciesProperty = "r_TAF_CountedSpecies";
		public const string IdentityKeysProperty = "r_TAF_CountedIdentityKeys";

		/// <summary>Reconcile every citizen body this survey honestly witnessed.</summary>
		public static bool Reconcile(KingdomSystem System, IList<GameObject> Settlers)
		{
			if (System == null)
			{
				return false;
			}
			Dictionary<string, int> cultures = KingdomResidentIdentityRules.CanonicalTallies(
				System.CultureCounts, KingdomZoningRules.KindCulture);
			Dictionary<string, int> species = KingdomResidentIdentityRules.CanonicalTallies(
				System.SpeciesCounts, KingdomZoningRules.KindSpecies);
			Dictionary<string, int> identityKeys =
				KingdomResidentIdentityRules.CanonicalIdentityTallies(System.IdentityCounts);
			bool changed = !Same(System.CultureCounts, cultures)
				|| !Same(System.SpeciesCounts, species)
				|| !Same(System.IdentityCounts, identityKeys);
			System.CultureCounts = cultures;
			System.SpeciesCounts = species;
			System.IdentityCounts = identityKeys;
			if (Settlers == null)
			{
				return changed;
			}
			for (int i = 0; i < Settlers.Count; i++)
			{
				GameObject settler = Settlers[i];
				if (!GameObject.Validate(settler))
				{
					continue;
				}
				string culture = KingdomResidentIdentityRules.CanonicalName(
					KingdomZoningRules.KindCulture, settler.GetCulture());
				string body = KingdomResidentIdentityRules.CanonicalName(
					KingdomZoningRules.KindSpecies, settler.GetSpecies());
				string formerCulture = settler.GetStringProperty(CultureProperty);
				string formerSpecies = settler.GetStringProperty(SpeciesProperty);
				List<string> formerKeys = KingdomResidentIdentityRules.DecodeIdentityKeys(
					settler.GetStringProperty(IdentityKeysProperty));
				KingdomIdentityReading reading = KingdomIdentity.Read(settler);
				ResidentTruth truth = KingdomQol.TruthOf(settler);
				List<string> currentKeys = KingdomResidentIdentityRules.BuiltInIdentityKeys(
					reading.Genotype, truth.Robot, truth.Aquatic && !truth.Flying,
					truth.BroadBodied);
				currentKeys.AddRange(KingdomExtensions.IdentityKeys(reading));
				currentKeys = KingdomResidentIdentityRules.CanonicalIdentityKeys(currentKeys);
				changed |= KingdomResidentIdentityRules.Transition(cultures,
					KingdomZoningRules.KindCulture, formerCulture, culture);
				changed |= KingdomResidentIdentityRules.Transition(species,
					KingdomZoningRules.KindSpecies, formerSpecies, body);
				changed |= KingdomResidentIdentityRules.TransitionIdentityKeys(identityKeys,
					formerKeys, currentKeys);
				Write(settler, CultureProperty, culture);
				Write(settler, SpeciesProperty, body);
				Write(settler, IdentityKeysProperty,
					KingdomResidentIdentityRules.EncodeIdentityKeys(currentKeys));
			}
			return changed;
		}

		/// <summary>
		/// Strikes one departing/dead body from both live tallies. Safe for a legacy body that was
		/// never witnessed: it removes nothing rather than guessing that some other resident's row
		/// belonged to this one.
		/// </summary>
		public static bool Forget(KingdomSystem System, GameObject Settler)
		{
			if (System == null || Settler == null)
			{
				return false;
			}
			if (System.CultureCounts == null)
			{
				System.CultureCounts = new Dictionary<string, int>();
			}
			if (System.SpeciesCounts == null)
			{
				System.SpeciesCounts = new Dictionary<string, int>();
			}
			if (System.IdentityCounts == null)
			{
				System.IdentityCounts = new Dictionary<string, int>();
			}
			bool changed = KingdomResidentIdentityRules.Transition(System.CultureCounts,
				KingdomZoningRules.KindCulture, Settler.GetStringProperty(CultureProperty), null);
			changed |= KingdomResidentIdentityRules.Transition(System.SpeciesCounts,
				KingdomZoningRules.KindSpecies, Settler.GetStringProperty(SpeciesProperty), null);
			changed |= KingdomResidentIdentityRules.TransitionIdentityKeys(System.IdentityCounts,
				KingdomResidentIdentityRules.DecodeIdentityKeys(
					Settler.GetStringProperty(IdentityKeysProperty)), null);
			Write(Settler, CultureProperty, null);
			Write(Settler, SpeciesProperty, null);
			Write(Settler, IdentityKeysProperty, null);
			return changed;
		}

		private static void Write(GameObject Object, string Property, string Value)
		{
			Object.SetStringProperty(Property, Value, RemoveIfNull: true);
		}

		private static bool Same(IDictionary<string, int> Left, IDictionary<string, int> Right)
		{
			if (ReferenceEquals(Left, Right)) return true;
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			foreach (KeyValuePair<string, int> row in Left)
			{
				if (!Right.TryGetValue(row.Key, out int value) || value != row.Value) return false;
			}
			return true;
		}
	}
}
