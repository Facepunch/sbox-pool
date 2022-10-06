using Sandbox;

namespace Facepunch.Pool
{
	public class Powerup : BaseNetworkable
	{
		public virtual string Name => "Powerup";
		public virtual float Duration => 60f;
		public virtual string Icon => string.Empty;
		public virtual bool IsStatic => false;

		public virtual void OnStart( Player player )
		{
			
		}

		public virtual void OnFinish( Player player )
		{

		}

		public virtual void OnSpawn( PowerupEntity entity )
		{

		}
	}
}
