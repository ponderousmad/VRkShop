using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Lathe : MonoBehaviour
{
	public int lengthSegments = 20;
	public int radialSegments = 32;
	public float length = 1.0f;
	public float radius = 0.1f;
	public float spinRate = 100.0f;

	private Mesh mMesh = null;
	private bool mShapeChanged = false;
	private const int TRI_VERTS = 3;

	[ContextMenu("Clear Geometry")]
	void ClearGeometry()
	{
		mMesh = null;
		var mf = GetComponent<MeshFilter>();
		mf.mesh = null;
	}

	private int VertexCount
	{
		get { return (3 + lengthSegments) * (radialSegments + 1) + 2; }
	}

	private int TriangleCount
	{
		get { return ((lengthSegments + 2) * radialSegments * 2); }
	}

	void Start()
	{
		CreateMesh();
	}

	void Update()
	{
		if (mShapeChanged)
		{
			ShapeMesh(false);
		}
		transform.Rotate(Vector3.right, spinRate * Time.deltaTime, Space.Self);
	}

	void ShapeMesh(bool initialize)
	{
		var vertices = mMesh.vertices;
		var tri = mMesh.triangles;
		var normals = mMesh.normals;
		var uv = mMesh.uv;

		var index = 0;
		var triIndex = 0;
		var uStep = 1.0f / radialSegments;
		var vStep = 1.0f / (lengthSegments + 2);
		var angleStep = (2 * Mathf.PI) / radialSegments;
		var lengthStep = length / lengthSegments;
		var ringCount = radialSegments + 1;
		var maxIndex = vertices.Length - 2 * (ringCount + 1); // Remove the caps.
		var topCenter = vertices.Length - 1;
		var bottomCenter = topCenter - 1;
		for (int i = 0; i <= radialSegments; ++i)
		{
			var angle = angleStep * i;
			var cos = Mathf.Cos(angle);
			var sin = Mathf.Sin(angle);
			var z = radius * cos;
			var y = radius * sin;
			vertices[maxIndex + i] = new Vector3(0, y, z);
			vertices[maxIndex + ringCount + i] = new Vector3(length, y, z);
			if (initialize)
			{
				uv[maxIndex + i] = new Vector2(i * uStep, vStep);
				uv[maxIndex + ringCount + i] = new Vector2(i * uStep, 1 - vStep);
				normals[maxIndex + i] = new Vector3(-1, 0, 0);
				normals[maxIndex + ringCount + i] = new Vector3(1, 0, 0);
				if (i < radialSegments)
				{
					tri[triIndex + 0] = bottomCenter;
					tri[triIndex + 1] = maxIndex + i;
					tri[triIndex + 2] = maxIndex + (i + 1) % ringCount;
					triIndex += TRI_VERTS;
					tri[triIndex + 0] = topCenter;
					tri[triIndex + 1] = maxIndex + ringCount + (i + 1) % ringCount;
					tri[triIndex + 2] = maxIndex + ringCount + i;
					triIndex += TRI_VERTS;
				}
			}
			for (int j = 0; j <= lengthSegments; ++j)
			{
				vertices[index] = new Vector3(lengthStep * j, y, z);
				if (initialize)
				{
					normals[index] = new Vector3(0, cos, sin);
					uv[index] = new Vector2(i * uStep, (j + 1) * vStep);
					if (i < radialSegments && j < lengthSegments)
					{
						tri[triIndex + 0] = index;
						tri[triIndex + 1] = (index + 1) % maxIndex;
						tri[triIndex + 2] = (index + lengthSegments + 1) % maxIndex;
						triIndex += TRI_VERTS;
						tri[triIndex + 0] = (index + 1) % maxIndex;
						tri[triIndex + 1] = (index + lengthSegments + 2) % maxIndex;
						tri[triIndex + 2] = (index + lengthSegments + 1) % maxIndex;
						triIndex += TRI_VERTS;
					}
				}
				++index;
			}
		}
		vertices[bottomCenter] = new Vector3(0, 0, 0);
		vertices[topCenter] = new Vector3(length, 0, 0);
		if (initialize)
		{
			normals[bottomCenter] = new Vector3(-1, 0, 0);
			normals[topCenter] = new Vector3(1, 0, 0);
			uv[bottomCenter] = new Vector2(0.5f, 0);
			uv[topCenter] = new Vector2(0.5f, 1);
		}

		mMesh.vertices = vertices;
		if (initialize)
		{
			mMesh.normals = normals;
			mMesh.uv = uv;
			mMesh.triangles = tri;
		}

		mMesh.RecalculateBounds();
		mMesh.RecalculateNormals();
	}

	void CreateMesh()
	{
		var mf = GetComponent<MeshFilter>();
		mMesh = new Mesh();
		mf.mesh = mMesh;
		mMesh.vertices = new Vector3[VertexCount];
		mMesh.normals = new Vector3[VertexCount];
		mMesh.uv = new Vector2[VertexCount];
		mMesh.triangles = new int[TriangleCount * TRI_VERTS];

		ShapeMesh(true);
	}
}
