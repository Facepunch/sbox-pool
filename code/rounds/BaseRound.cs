using Sandbox;
using System.Collections.Generic;

namespace Facepunch.Pool
{
    public abstract partial class BaseRound : BaseNetworkable
	{
		public virtual int RoundDuration => 0;
		public virtual string RoundName => "";
		public virtual bool CanPlayerSuicide => false;
		public virtual bool ShowTimeLeft => false;
		public virtual bool ShowRoundInfo => false;

		public List<Player> Players = new();

		public float RoundEndTime { get; set; }

		public float TimeLeft
		{
			get
			{
				return RoundEndTime - Time.Now;
			}
		}

		[Net] public int TimeLeftSeconds { get; set; }

		public void Start()
		{
			if ( Game.IsServer && RoundDuration > 0 )
				RoundEndTime = Time.Now + RoundDuration;
			
			OnStart();
		}

		public void Finish()
		{
			if ( Game.IsServer )
			{
				RoundEndTime = 0f;
				Players.Clear();
			}

			OnFinish();
		}

		public void AddPlayer( Player player )
		{
			Game.AssertServer();

			if ( !Players.Contains(player) )
				Players.Add( player );
		}

		public virtual void OnBallEnterPocket( PoolBall ball, TriggerBallPocket pocket ) { }

		public virtual void OnBallHitOtherBall( PoolBall ball, PoolBall other ) { }

		public virtual void OnPlayerJoin( Player player ) { }

		public virtual void OnPlayerKilled( Player player ) { }

		public virtual void OnPlayerLeave( Player player )
		{
			Players.Remove( player );
		}

		public virtual void UpdatePlayerPosition( Player player )
		{
			var zoomOutDistance = 350f;
			player.Position = new Vector3( 0f, 0f, zoomOutDistance );
			player.Rotation = Rotation.LookAt( Vector3.Down );
		}

		public virtual void OnTick() { }

		public virtual void OnSecond()
		{
			if ( Game.IsServer )
			{
				if ( RoundEndTime > 0 && Sandbox.Time.Now >= RoundEndTime )
				{
					RoundEndTime = 0f;
					OnTimeUp();
				}
				else
				{
					TimeLeftSeconds = TimeLeft.CeilToInt();
				}
			}
		}

		protected virtual void OnStart() { }

		protected virtual void OnFinish() { }

		protected virtual void OnTimeUp() { }
	}
}
