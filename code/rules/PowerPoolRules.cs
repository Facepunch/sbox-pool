using Sandbox;

namespace Facepunch.Pool
{
	[ClassName( "rules_power_pool")]
	public class PowerPoolRules : BaseGameRules
	{
		public override PoolBall CreatePoolBall() => new PowerPoolBall();
	}
}
