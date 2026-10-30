using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>
	/// Drives the fighter's model: how fast it moves forwards and sideways (worked out from where it was a moment
	/// ago, so it is the same for the player and for bots), plus the attack, hit and death animations. The animator
	/// is the mage's, with a blend tree for walking in any direction.
	/// </summary>
	public sealed class FighterAnimation : MonoBehaviour
	{
		static readonly int Forward = Animator.StringToHash("Forward");
		static readonly int Right = Animator.StringToHash("Right");
		static readonly int Speed = Animator.StringToHash("Speed");
		static readonly int Grounded = Animator.StringToHash("Grounded");
		static readonly int Dead = Animator.StringToHash("Dead");
		static readonly int CastProjectile = Animator.StringToHash("CastProjectile");
		static readonly int CastNova = Animator.StringToHash("CastNova");
		static readonly int Hit = Animator.StringToHash("Hit");

		public Animator Animator;

		Vector3 _last;
		bool _hasLast;

		/// <summary>The fighter's speed in metres per second on the ground, as last measured.</summary>
		public Vector3 Velocity { get; private set; }

		void Update()
		{
			var dt = Time.deltaTime;
			if (dt <= 0f) return;
			var position = transform.position;
			if (_hasLast)
			{
				var velocity = (position - _last) / dt;
				velocity.y = 0f;
				if (velocity.magnitude > 20f) velocity = Vector3.zero; // a respawn jump, not running
				Velocity = Vector3.Lerp(Velocity, velocity, 0.5f);
			}
			_last = position;
			_hasLast = true;

			if (Animator == null) return;
			var local = transform.InverseTransformDirection(Velocity);
			Animator.SetFloat(Forward, local.z, 0.1f, dt);
			Animator.SetFloat(Right, local.x, 0.1f, dt);
			Animator.SetFloat(Speed, local.magnitude, 0.1f, dt);
			Animator.SetBool(Grounded, true);
		}

		/// <summary>Forget where the fighter was: it has just been put somewhere else.</summary>
		public void Snap()
		{
			_hasLast = false;
			Velocity = Vector3.zero;
		}

		public void PlayAttack(AttackKind kind)
		{
			if (Animator == null) return;
			Animator.SetTrigger(kind == AttackKind.Melee ? CastNova : CastProjectile);
		}

		public void PlayHit()
		{
			if (Animator != null) Animator.SetTrigger(Hit);
		}

		public void SetDead(bool dead)
		{
			if (Animator != null) Animator.SetBool(Dead, dead);
		}
	}
}
