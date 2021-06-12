using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoolGame
{
	[Library( "pool_cue" )]
	public partial class PoolCue : ModelEntity
	{
		[Net] public Vector3 AimDir { get; set; }
		[Net] public float ShotPower { get; set; }
		public bool IsMakingShot { get; set; }
		public float CuePitch { get; set; }
		public float CueYaw { get; set; }
		public ShotPowerLine ShotPowerLine { get; set; }
		public ModelEntity GhostBall { get; private set; }

		private float _cuePullBackOffset;
		private float _lastPowerDistance;
		private float _maxCuePitch = 17f;
		private float _minCuePitch = 5f;

		public void Reset()
		{
			IsMakingShot = false;
		}

		public Vector3 DirectionTo( PoolBall ball )
		{
			return (ball.Position - Position.WithZ( ball.Position.z )).Normal;
		}

		public override void Spawn()
		{
			base.Spawn();

			SetModel( "models/pool/pool_cue_b.vmdl" );

			EnableDrawing = false;
			Transmit = TransmitType.Always;
		}

		public override void Simulate( Client client )
		{
			var whiteBall = Game.Instance.WhiteBall;
			
			EnableDrawing = false;

			if ( !IsOwnerInPlay( whiteBall, out var controller ) )
				return;

			if ( controller.IsPlacingWhiteBall )
			{
				HandleWhiteBallPlacement( controller, whiteBall );
				return;
			}

			if ( !Input.Down( InputButton.Attack1 ) )
			{
				UpdateAimDir( controller, whiteBall.Position );

				if ( !IsMakingShot )
				{
					RotateCue( whiteBall );
				}
				else
				{
					if ( Host.IsServer )
						TakeShot( controller, whiteBall );

					_cuePullBackOffset = 0f;
					IsMakingShot = false;
					ShotPower = 0f;
				}
			}
			else
			{
				HandlePowerSelection( controller );
			}

			EnableDrawing = true;
			Position = whiteBall.Position - Rotation.Forward * (1f + _cuePullBackOffset + (CuePitch * 0.04f));

			// Never interpolate just update its position immediately for everybody.
			ResetInterpolation();

			base.Simulate( client );
		}

		[Event( EventType.ClientTick )]
		private void Tick()
		{
			var whiteBall = Game.Instance.WhiteBall;

			if ( Host.IsClient )
			{
				if ( ShotPowerLine != null )
					ShotPowerLine.IsEnabled = false;

				if ( GhostBall != null )
					GhostBall.EnableDrawing = false;
			}

			if ( !IsOwnerInPlay( whiteBall, out var controller ) )
				return;

			if ( controller.IsPlacingWhiteBall )
			{
				ShowWhiteArea( true );
				return;
			}	
			else
			{
				ShowWhiteArea( false );
			}

			if ( ShotPowerLine == null )
				ShotPowerLine = new ShotPowerLine();

			var trace = Trace.Ray( whiteBall.Position, whiteBall.Position + AimDir * 1000f )
				.Ignore( whiteBall )
				.Ignore( this )
				.Run();

			ShotPowerLine.IsEnabled = true;
			ShotPowerLine.Position = trace.StartPos;
			ShotPowerLine.ShotPower = ShotPower;
			ShotPowerLine.EndPos = trace.EndPos;
			ShotPowerLine.Color = Color.Green;
			ShotPowerLine.Width = 0.1f + ((0.15f / 100f) * ShotPower);

			var fromTransform = whiteBall.PhysicsBody.Transform;
			var toTransform = whiteBall.PhysicsBody.Transform;
			toTransform.Position = trace.EndPos;

			var sweep = Trace.Sweep( whiteBall.PhysicsBody, fromTransform, toTransform )
				.Ignore( whiteBall )
				.Run();

			if ( sweep.Hit )
			{
				if ( GhostBall == null )
				{
					GhostBall = new ModelEntity();
					GhostBall.SetModel( "models/pool/pool_ball.vmdl" );
					GhostBall.RenderAlpha = 0.4f;
				}

				if ( sweep.Entity is PoolBall other && !other.CanPlayerHit( controller ) )
				{
					ShotPowerLine.Color = Color.Red;
					GhostBall.RenderColor = Color.Red;
				}
				else
				{
					GhostBall.RenderColor = Color.White;
				}

				GhostBall.EnableDrawing = true;
				GhostBall.Position = sweep.EndPos;
			}
		}

		private bool IsOwnerInPlay( PoolBall whiteBall, out Player controller )
		{
			controller = Owner as Player;

			if ( controller == null )
				return false;

			if ( !controller.IsTurn || controller.HasStruckWhiteBall )
				return false;

			if ( whiteBall == null || !whiteBall.IsValid() )
				return false;

			return true;
		}

		private void ShowWhiteArea( bool shouldShow )
		{
			if ( Host.IsServer ) return;

			var whiteArea = Game.Instance.WhiteArea;

			if ( whiteArea != null && whiteArea.IsValid() )
				whiteArea.Quad.IsEnabled = shouldShow;
		}

		private void TakeShot( Player controller, PoolBall whiteBall )
		{
			Host.AssertServer();

			if ( ShotPower >= 5f )
			{
				using ( Prediction.Off() )
				{
					controller.StrikeWhiteBall( this, whiteBall, ShotPower * 6f );

					var soundFileId = Convert.ToInt32( MathF.Round( (2f / 100f) * ShotPower ) );
					whiteBall.PlaySound( $"shot-power-{soundFileId}" );
				}
			}
		}

		private void HandleWhiteBallPlacement( Player controller, PoolBall whiteBall )
		{
			var cursorTrace = Trace.Ray( controller.EyePos, controller.EyePos + Input.CursorAim * 1000f )
				.WorldOnly()
				.Run();

			var whiteArea = Game.Instance.WhiteArea;
			var whiteAreaWorldOBB = whiteArea.CollisionBounds.ToWorldSpace( whiteArea );

			whiteBall.TryMoveTo( cursorTrace.EndPos, whiteAreaWorldOBB );

			if ( Input.Released( InputButton.Attack1 ) )
				controller.StopPlacingWhiteBall();
		}

		private void HandlePowerSelection( Player controller )
		{
			var cursorPlaneEndPos = controller.EyePos + Input.CursorAim * 350f;
			var distanceToCue = cursorPlaneEndPos.Distance( Position - Rotation.Forward * 100f );
			var cuePullBackDelta = (_lastPowerDistance - distanceToCue) * Time.Delta * 20f;

			if ( !IsMakingShot )
			{
				_lastPowerDistance = 0f;
				cuePullBackDelta = 0f;
			}

			_cuePullBackOffset = Math.Clamp( _cuePullBackOffset + cuePullBackDelta, 0f, 8f );
			_lastPowerDistance = distanceToCue;

			IsMakingShot = true;
			ShotPower = _cuePullBackOffset.AsPercentMinMax( 0f, 8f );
		}

		private bool UpdateAimDir( Player controller, Vector3 ballCenter )
		{
			if ( IsMakingShot ) return true;

			var tablePlane = new Plane( ballCenter, Vector3.Up );
			var hitPos = tablePlane.Trace( new Ray( controller.EyePos, Input.CursorAim ), true );

			if ( !hitPos.HasValue ) return false;

			AimDir = (hitPos.Value - ballCenter).WithZ( 0 ).Normal;

			return true;
		}

		private void RotateCue( PoolBall whiteBall )
		{
			var rollTrace = Trace.Ray( whiteBall.Position, whiteBall.Position - AimDir * 100f )
				.Ignore( this )
				.Ignore( whiteBall )
				.Run();

			var aimRotation = Rotation.LookAt( AimDir, Vector3.Forward );

			_cuePullBackOffset = _cuePullBackOffset.LerpTo( 0f, Time.Delta * 10f );

			CuePitch = CuePitch.LerpTo( _minCuePitch + ((_maxCuePitch - _minCuePitch) * (1f - rollTrace.Fraction)), Time.Delta * 10f );
			CueYaw = aimRotation.Yaw().Normalize( 0f, 360f );

			if ( IsAuthority )
			{
				Rotation = Rotation.From(
					Rotation.Angles()
						.WithYaw( CueYaw )
						.WithPitch( CuePitch )
				);
			}
		}
	}
}
