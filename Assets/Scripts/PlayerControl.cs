using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>
	/// The player's trigger finger: the main action (left mouse) attacks towards where the camera is looking.
	/// Moving and looking are done by the Character View controls; this only aims and fires.
	/// </summary>
	[RequireComponent(typeof(Fighter))]
	public sealed class PlayerControl : MonoBehaviour
	{
		public ArenaGame Game;

		Fighter _fighter;

		void Awake()
		{
			_fighter = GetComponent<Fighter>();
		}

		void Update()
		{
			if (Game == null || Game.Phase != ArenaPhase.Playing || _fighter.IsDown) return;
			if (EngineInput.Default.Player.Primary.IsPressed()) _fighter.TryAttack(AimDirection());
		}

		/// <summary>From the fighter's chest to the point in the middle of the screen.</summary>
		Vector3 AimDirection()
		{
			var camera = Camera.main;
			if (camera == null) return transform.forward;

			var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
			var target = ray.GetPoint(40f);
			var hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore);
			var nearest = float.MaxValue;
			foreach (var hit in hits)
			{
				if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
				nearest = hit.distance;
				target = hit.point;
			}
			return (target - _fighter.Chest).normalized;
		}
	}
}
