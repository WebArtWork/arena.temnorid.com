using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>A small gold marker and the word HERO above a fighter who won their class last period.</summary>
	public sealed class HeroBadge : MonoBehaviour
	{
		public float Height = 3.0f;

		GameObject _badge;

		void Awake()
		{
			_badge = new GameObject("HeroBadge");
			_badge.transform.SetParent(transform, false);
			_badge.transform.localPosition = new Vector3(0f, Height, 0f);

			var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gem.name = "Gem";
			Destroy(gem.GetComponent<Collider>());
			gem.transform.SetParent(_badge.transform, false);
			gem.transform.localPosition = new Vector3(0f, 0.28f, 0f);
			gem.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
			gem.transform.localScale = Vector3.one * 0.22f;
			gem.GetComponent<Renderer>().sharedMaterial = ITKamianets.Engine.Feedback.Fx.Material(new Color(1f, 0.85f, 0.2f));

			var text = new GameObject("Text");
			text.transform.SetParent(_badge.transform, false);
			var mesh = text.AddComponent<TextMesh>();
			mesh.text = "HERO";
			mesh.anchor = TextAnchor.LowerCenter;
			mesh.alignment = TextAlignment.Center;
			mesh.characterSize = 0.12f;
			mesh.fontSize = 48;
			mesh.color = new Color(1f, 0.85f, 0.2f);
			var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			mesh.font = font;
			text.GetComponent<MeshRenderer>().sharedMaterial = font.material;

			_badge.SetActive(false);
		}

		public void SetHero(bool hero)
		{
			if (_badge == null) Awake();
			_badge.SetActive(hero);
		}

		void LateUpdate()
		{
			if (_badge == null || !_badge.activeSelf) return;
			var camera = Camera.main;
			if (camera != null) _badge.transform.rotation = camera.transform.rotation; // always faces the camera
			var gem = _badge.transform.GetChild(0);
			gem.localRotation = Quaternion.Euler(45f, Time.time * 90f, 0f);
		}
	}
}
