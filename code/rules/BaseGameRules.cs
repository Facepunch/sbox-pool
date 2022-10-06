using Sandbox;

namespace Facepunch.Pool
{
	public abstract partial class BaseGameRules : BaseNetworkable
	{
		[Net] public bool IsPlaying { get; set; }

		public virtual bool IsRanked => false;
		public virtual PoolCue CreatePoolCue() => new PoolCue();
		public virtual PoolBall CreatePoolBall() => new PoolBall();

		public BaseGameRules()
		{
			Event.Register( this );
		}

		~BaseGameRules()
		{
			Event.Unregister( this );
		}

		public void Start()
		{
			IsPlaying = true;
			OnStart();
		}

		public void Finish()
		{
			IsPlaying = false;
			OnFinish();
		}

		protected virtual void OnStart()
		{

		}

		protected virtual void OnFinish()
		{

		}
	}
}
