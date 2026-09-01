using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Open provider interface. XML parts and code mods both resolve into one typed,
	/// bounded declaration; custom operation is the only case delegated back to provider code.
	/// Every callback is an observation-only deterministic function for one benefit epoch: it must
	/// not mutate the provider, root, survey, zone, another provider, designation source, or hidden
	/// state, and repeated description calls in that epoch must normalize identically. The evaluator
	/// may call descriptions more than once and refuses detectable contract violations.</summary>
	public interface IKingdomBenefitProvider
	{
		bool TryDescribeKingdomBenefits(out KingdomBenefitProviderDeclaration Declaration,
			out string Failure);
		bool IsKingdomBenefitOperational(GameObject Provider, GameObject DesignationRoot,
			KingdomSurvey Survey);
	}

	/// <summary>Optional richer custom-operation seam. Existing providers keep the boolean
	/// callback; implementations here may report a bounded partial 0-100 operating state. This
	/// callback has the same deterministic, observation-only contract as
	/// <see cref="IKingdomBenefitProvider"/>. Arbitrary mutable hidden state is not authority.</summary>
	public interface IKingdomQuantitativeBenefitProvider
	{
		bool TryKingdomBenefitOperationPercent(GameObject Provider,
			GameObject DesignationRoot, KingdomSurvey Survey, out int Percent,
			out string Failure);
	}
}

namespace XRL.World.Parts
{
	/// <summary>XML surface for ordinary physical providers. ProviderKey is stable semantic
	/// identity; the object ID supplies instance identity.</summary>
	[Serializable]
	public sealed class r_KingdomBenefitProvider : IPart,
		ThousandAndFirst.IKingdomBenefitProvider
	{
		public string ProviderKey;
		public string Carries;
		public string Provides;
		public string NetworkKey;
		public string Scope = "building";
		public string Operation = "present";

		public bool TryDescribeKingdomBenefits(
			out ThousandAndFirst.KingdomBenefitProviderDeclaration Declaration,
			out string Failure)
		{
			return ThousandAndFirst.KingdomBenefitProviderRules.TryDescribe(ProviderKey,
				Carries, Provides, Scope, Operation, NetworkKey, out Declaration, out Failure);
		}

		public bool IsKingdomBenefitOperational(GameObject Provider,
			GameObject DesignationRoot, ThousandAndFirst.KingdomSurvey Survey)
		{
			// XML can declare host-owned predicates, but contains no executable custom predicate.
			// The evaluator calls this interface method only for Operation=custom, which therefore
			// fails closed for the declarative part. Code providers may implement their own callback.
			return false;
		}
	}
}
