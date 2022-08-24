using Sandbox;

namespace Facepunch.Pool
{
	public abstract class BaseGameRules : BaseNetworkable
	{
		public virtual bool IsRanked => false;
		public virtual PoolCue CreatePoolCue() => new PoolCue();
		public virtual PoolBall CreatePoolBall() => new PoolBall();
	}
}
