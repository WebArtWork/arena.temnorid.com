using ITKamianets.Engine.Input;
using UnityEngine;
using UnityEngine.InputSystem;

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
		InputAction[] _skills;
		InputAction _nextClass;

		void Awake()
		{
			_fighter = GetComponent<Fighter>();
		}

		void Update()
		{
			if (Game == null || Game.Phase != ArenaPhase.Playing || _fighter.IsDown) return;
			if (EngineInput.Default.Player.Primary.IsPressed()) _fighter.TryAttack(AimDirection());
			FindActions();
			if (_skills == null) return;
			for (var i = 0; i < _skills.Length; i++)
			{
				if (_skills[i] == null || !_skills[i].WasPressedThisFrame()) continue;
				var result = _fighter.TryCastSkill(i, AimDirection());
				if (result == Fighter.CastResult.NoMana) Game.ShowToast("Not enough mana", 1.2f);
				else if (result == Fighter.CastResult.Cooling) Game.ShowToast("Not ready yet", 1.2f);
			}
			if (_nextClass != null && _nextClass.WasPressedThisFrame()) Game.NextPracticeClass();
		}

		/// <summary>The skill keys are actions of this game's own map (added by the HUD), so they can be rebound like the rest.</summary>
		void FindActions()
		{
			if (_skills != null) return;
			var map = EngineInput.Default.Asset.FindActionMap("Arena");
			if (map == null) return;
			_skills = new InputAction[Classes.SkillSlots];
			for (var i = 0; i < _skills.Length; i++) _skills[i] = map.FindAction("Skill" + (i + 1));
			_nextClass = map.FindAction("NextClass");
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
