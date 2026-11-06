using UnityEngine;

namespace Temnorid.Arena
{
	/// <summary>
	/// What a fighter carries for its class: a sword, a bow, a staff... built from simple shapes, and rebuilt when the fighter
	/// trains as another class. The scene builder only adds this and gives it a material to copy.
	/// </summary>
	public sealed class ClassGear : MonoBehaviour
	{
		/// <summary>Any lit material of the scene: the gear's pieces are copies of it in other colours.</summary>
		public Material Template;

		Transform _root;

		public void Apply(ClassDefinition definition)
		{
			if (Template == null) return;
			if (_root != null)
			{
				_root.gameObject.SetActive(false);
				Destroy(_root.gameObject);
			}
			_root = new GameObject("ClassGear").transform;
			_root.SetParent(transform, false);

			var tint = Mat(definition.Tint);
			var steel = Mat(new Color(0.78f, 0.8f, 0.86f));
			var wood = Mat(new Color(0.45f, 0.3f, 0.18f));
			var gold = Mat(new Color(0.9f, 0.75f, 0.3f));
			Piece(PrimitiveType.Cube, "Sash", new Vector3(0f, 1.15f, 0.2f), new Vector3(0.62f, 0.14f, 0.38f), tint);

			switch (definition.Id)
			{
				case ClassId.Warrior:
					Piece(PrimitiveType.Cube, "Blade", new Vector3(0.5f, 1.05f, 0.45f), new Vector3(0.08f, 0.08f, 1.2f), steel);
					Piece(PrimitiveType.Cube, "Guard", new Vector3(0.5f, 1.05f, -0.1f), new Vector3(0.28f, 0.08f, 0.1f), wood);
					Piece(PrimitiveType.Cube, "ShoulderL", new Vector3(-0.36f, 1.5f, 0f), new Vector3(0.28f, 0.16f, 0.34f), tint);
					Piece(PrimitiveType.Cube, "ShoulderR", new Vector3(0.36f, 1.5f, 0f), new Vector3(0.28f, 0.16f, 0.34f), tint);
					break;
				case ClassId.Knight:
					Piece(PrimitiveType.Cube, "Blade", new Vector3(0.5f, 1.05f, 0.4f), new Vector3(0.08f, 0.08f, 1.0f), steel);
					Piece(PrimitiveType.Cube, "Guard", new Vector3(0.5f, 1.05f, -0.05f), new Vector3(0.26f, 0.08f, 0.1f), gold);
					Piece(PrimitiveType.Cube, "Shield", new Vector3(-0.5f, 1.15f, 0.25f), new Vector3(0.1f, 0.8f, 0.55f), tint);
					Piece(PrimitiveType.Cube, "ShieldRim", new Vector3(-0.55f, 1.15f, 0.25f), new Vector3(0.05f, 0.5f, 0.3f), gold);
					Piece(PrimitiveType.Cube, "ShoulderL", new Vector3(-0.36f, 1.5f, 0f), new Vector3(0.3f, 0.18f, 0.36f), steel);
					Piece(PrimitiveType.Cube, "ShoulderR", new Vector3(0.36f, 1.5f, 0f), new Vector3(0.3f, 0.18f, 0.36f), steel);
					Piece(PrimitiveType.Cube, "Plume", new Vector3(0f, 1.95f, -0.05f), new Vector3(0.08f, 0.3f, 0.3f), tint);
					break;
				case ClassId.Archer:
					Piece(PrimitiveType.Cube, "BowGrip", new Vector3(-0.5f, 1.1f, 0.35f), new Vector3(0.05f, 0.9f, 0.05f), wood);
					Piece(PrimitiveType.Cube, "BowTop", new Vector3(-0.5f, 1.6f, 0.3f), new Vector3(0.05f, 0.5f, 0.05f), wood).localRotation = Quaternion.Euler(-25f, 0f, 0f);
					Piece(PrimitiveType.Cube, "BowBottom", new Vector3(-0.5f, 0.6f, 0.3f), new Vector3(0.05f, 0.5f, 0.05f), wood).localRotation = Quaternion.Euler(25f, 0f, 0f);
					Piece(PrimitiveType.Cube, "Quiver", new Vector3(0.1f, 1.2f, -0.28f), new Vector3(0.14f, 0.5f, 0.14f), tint);
					break;
				case ClassId.Rogue:
					Piece(PrimitiveType.Cube, "DaggerR", new Vector3(0.5f, 1.0f, 0.35f), new Vector3(0.05f, 0.05f, 0.55f), steel);
					Piece(PrimitiveType.Cube, "DaggerL", new Vector3(-0.5f, 1.0f, 0.35f), new Vector3(0.05f, 0.05f, 0.55f), steel);
					Piece(PrimitiveType.Cube, "Cloak", new Vector3(0f, 1.1f, -0.28f), new Vector3(0.55f, 0.95f, 0.08f), tint);
					Piece(PrimitiveType.Cube, "Hood", new Vector3(0f, 1.78f, -0.05f), new Vector3(0.34f, 0.12f, 0.36f), tint);
					break;
				case ClassId.Healer:
					Piece(PrimitiveType.Cylinder, "Staff", new Vector3(0.5f, 0.95f, 0.35f), new Vector3(0.05f, 0.95f, 0.05f), wood);
					Piece(PrimitiveType.Sphere, "Orb", new Vector3(0.5f, 1.95f, 0.35f), Vector3.one * 0.24f, gold);
					Piece(PrimitiveType.Cylinder, "Ring", new Vector3(0.5f, 1.8f, 0.35f), new Vector3(0.3f, 0.02f, 0.3f), tint);
					Piece(PrimitiveType.Cube, "Stole", new Vector3(0f, 1.2f, 0.22f), new Vector3(0.18f, 0.7f, 0.06f), gold);
					break;
				default:
					Piece(PrimitiveType.Cylinder, "Staff", new Vector3(0.5f, 0.95f, 0.35f), new Vector3(0.05f, 0.95f, 0.05f), wood);
					Piece(PrimitiveType.Sphere, "Orb", new Vector3(0.5f, 1.95f, 0.35f), Vector3.one * 0.22f, tint);
					break;
			}
		}

		Material Mat(Color colour)
		{
			var material = new Material(Template) { color = colour };
			return material;
		}

		Transform Piece(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			var collider = piece.GetComponent<Collider>();
			if (collider != null) Destroy(collider);
			piece.transform.SetParent(_root, false);
			piece.transform.localPosition = position;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece.transform;
		}
	}
}
