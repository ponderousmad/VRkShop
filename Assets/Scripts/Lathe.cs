using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap.Unity;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Lathe : MonoBehaviour
{
	public int lengthSegments = 20;
	public int radialSegments = 32;
	public float length = 1.0f;
	public float radius = 0.1f;
	public float maxSpinRate = 500.0f;

	public Transform tool = null;
	public float toolWidth = 0.01f;

	public Transform speedControlHandle = null;
	private Transform SpeedControl { get { return speedControlHandle.parent; } }

	public LeapHandController leap = null;

	private Mesh mMesh = null;
	private bool mShapeChanged = false;
	private const int TRI_VERTS = 3;

	private const float MAX_SPEED_ANGLE = Mathf.PI / 2;

	private List<float[]> mShell = new List<float[]>();

	[ContextMenu("Clear Geometry")]
	void ClearGeometry()
	{
		mMesh = null;
		var mf = GetComponent<MeshFilter>();
		mf.mesh = null;
	}

	[ContextMenu("Generate Geometry")]
	void Generate()
	{
		CreateMesh();
	}

	private int VertexCount
	{
		get { return (3 + lengthSegments) * (radialSegments + 1) + 2; }
	}

	private int TriangleCount
	{
		get { return ((lengthSegments + 2) * radialSegments * 2); }
	}

	private float AngleStep
	{
		get { return (2 * Mathf.PI) / radialSegments; }
	}

	private float LengthStep
	{
		get { return length / lengthSegments; }
	}

	void Start()
	{
		for (var i = 0; i < radialSegments; ++i)
		{
			var angle = i * AngleStep;
			var sideB = Mathf.Tan(angle); // Calculate a square cross section.
			sideB = Mathf.Abs(sideB) <= 1 ? sideB : 1 / sideB;
			var r = radius * Mathf.Sqrt(1 + sideB * sideB);
			var spoke = new float[lengthSegments + 1];
			mShell.Add(spoke);
			for (var j = 0; j <= lengthSegments; ++j)
			{
				spoke[j] = r;
			}
		}
		if (speedControlHandle)
		{
			SpeedControl.localRotation = Quaternion.Euler(MAX_SPEED_ANGLE * Mathf.Rad2Deg / 2, 0, 0);
		}

		CreateMesh();
	}

	void Update()
	{
		if (leap)
		{
			var provider = leap.GetComponent<LeapProvider>();
			if (provider)
			{
				var hands = provider.CurrentFrame.Hands;
				foreach (var hand in hands)
				{
					if (hand.IsRight)
					{

						if (tool)
						{
							var indexFinger = hand.Fingers[1];
							tool.localPosition = tool.parent.InverseTransformPoint(indexFinger.TipPosition.ToVector3());
							Debug.Log(tool.position);
						}
					}
					else if (hand.IsLeft)
					{
						if (speedControlHandle)
						{
							var speedPos = hand.PalmPosition.ToVector3() - SpeedControl.position;
							var distance = Vector3.Distance(speedPos, speedControlHandle.position - SpeedControl.position);
							if (distance < 0.1f && hand.GrabStrength > 0.6f)
							{
								Debug.Log("Distance: " + distance);
								var angle = Mathf.Clamp(Mathf.Atan2(speedPos.z, speedPos.y), -MAX_SPEED_ANGLE, 0);
								angle += Mathf.PI / 2;
								SpeedControl.localRotation = Quaternion.Euler(angle * Mathf.Rad2Deg, 0, 0);
							}
						}
					}
				}
			}
		}

		var spinRate = maxSpinRate;
		if (speedControlHandle)
		{
			var angle = SpeedControl.localRotation.eulerAngles.x * Mathf.Deg2Rad;
			spinRate = maxSpinRate * (angle / MAX_SPEED_ANGLE);
		}
		var spinStep = spinRate * Time.deltaTime;

		if (tool)
		{
			var tip = transform.InverseTransformPoint(tool.TransformPoint(Vector3.zero));
			var onAxis = new Vector3(tip.x, 0, 0);
			var spoke = tip - onAxis;
			var direction = spoke.normalized;
			var angle = -Mathf.Atan2(direction.z, direction.y) + (Mathf.PI / 2);
			if (angle < 0)
			{
				angle += 2 * Mathf.PI;
			}
			var angleIndex = Mathf.RoundToInt(angle / AngleStep) % radialSegments;
			var offset = Mathf.RoundToInt(tip.x / LengthStep);
			var steps = Mathf.CeilToInt(toolWidth / LengthStep);
			var angleSweep = Mathf.CeilToInt(spinStep * Mathf.Deg2Rad / AngleStep);
			for (var j = offset - steps; j <= offset + steps; ++j)
			{
				if (0 <= j && j <= lengthSegments)
				{
					for (var index = angleIndex - angleSweep; index <= angleIndex + angleSweep; ++index)
					{
						var i = (index + radialSegments) % radialSegments;
						var distance = Mathf.Max(1e-6f, spoke.magnitude);
						if (mShell[i][j] > distance)
						{
							mShell[i][j] = distance;
							mShapeChanged = true;
						}
					}
				}
			}
		}
		if (mShapeChanged)
		{
			ShapeMesh(false);
			mShapeChanged = false;
		}
		transform.Rotate(Vector3.right, spinStep, Space.Self);
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
		var angleStep = AngleStep;
		var lengthStep = LengthStep;
		var ringCount = radialSegments + 1;
		var maxIndex = vertices.Length - 2 * (ringCount + 1); // Excludes the caps.
		var topCenter = vertices.Length - 1;
		var bottomCenter = topCenter - 1;
		for (int i = 0; i <= radialSegments; ++i)
		{
			var angle = i < radialSegments ? angleStep * i : 0.0f;
			var cos = Mathf.Cos(angle);
			var sin = Mathf.Sin(angle);
			var radiusBottom = mShell[i % radialSegments][0];
			var radiusTop = mShell[i % radialSegments][lengthSegments];
			vertices[maxIndex + i] = new Vector3(0, sin * radiusBottom, cos * radiusBottom);
			vertices[maxIndex + ringCount + i] = new Vector3(length, sin * radiusTop, cos * radiusTop);
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
				var segmentRadius = mShell[i % radialSegments][j];
				vertices[index] = new Vector3(lengthStep * j, sin * segmentRadius, cos * segmentRadius);
				if (initialize)
				{
					normals[index] = new Vector3(0, sin, cos);
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
