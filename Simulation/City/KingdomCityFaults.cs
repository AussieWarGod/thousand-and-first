using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static class KingdomCityFaults
	{
		/// <summary>
		/// A kernel refusal in the city's vocabulary. Anything the kernel can raise that the city
		/// has no narrower word for arrives as <see cref="KingdomCityFault.ArithmeticOverflow"/>
		/// rather than as <see cref="KingdomCityFault.None"/>: a fault must never translate into a
		/// success.
		/// </summary>
		internal static KingdomCityFault FromKernel(KernelFaultCode fault)
		{
			switch (fault)
			{
			case KernelFaultCode.None:
				return KingdomCityFault.None;
			case KernelFaultCode.InvalidTick:
				return KingdomCityFault.InvalidTick;
			case KernelFaultCode.InvalidInterval:
				return KingdomCityFault.InvalidInterval;
			case KernelFaultCode.ClockRegression:
				return KingdomCityFault.ClockRegression;
			default:
				return KingdomCityFault.ArithmeticOverflow;
			}
		}
	}
}
