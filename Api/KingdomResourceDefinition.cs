using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One extension-owned civic good. The host files <see cref="Key"/> under the owning
	/// mod, freezes the remaining metadata, and keeps its level in the settlement's durable
	/// behaviour sidecar. A malformed definition disables only this row.</summary>
	public readonly struct KingdomResourceDefinition
	{
		/// <summary>Canonical lowercase owner-local key. The host owner-qualifies it; malformed keys
		/// are refused rather than silently rewritten.</summary>
		public readonly string Key;

		/// <summary>Founder-facing singular unit, such as <c>brick</c> or <c>spore</c>.</summary>
		public readonly string Unit;

		/// <summary>Integer property which marks a physical container dedicated to this good. Empty
		/// means the good has no physical container projection.</summary>
		public readonly string ContainerProperty;

		/// <summary>Owner-local <see cref="INetworkKind"/> key when the good flows, or empty.</summary>
		public readonly string NetworkKey;

		/// <summary>Exact Qud liquid id when the good flows as liquid, or empty for discrete goods.</summary>
		public readonly string LiquidId;

		/// <summary>Level used only when this owner/key is first admitted to this settlement.</summary>
		public readonly long InitialLevel;

		/// <summary>Current civic capacity. Existing levels clamp down, never wrap, if it shrinks.</summary>
		public readonly long Capacity;

		/// <summary>Builds one proposed resource kind. Host validation and owner qualification occur
		/// after the extension call returns.</summary>
		public KingdomResourceDefinition(string Key, string Unit, string ContainerProperty,
			string NetworkKey, string LiquidId, long InitialLevel, long Capacity)
		{
			this.Key = Key;
			this.Unit = Unit;
			this.ContainerProperty = ContainerProperty;
			this.NetworkKey = NetworkKey;
			this.LiquidId = LiquidId;
			this.InitialLevel = InitialLevel;
			this.Capacity = Capacity;
		}
	}
}
