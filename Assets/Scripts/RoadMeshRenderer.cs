using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoadMeshRenderer : MonoBehaviour {
   public Gradient waterGradient;
   Mesh mesh;

   Vector3[] vertices;
   Vector2[] UV;
   int[] triangles;
   Color[] colors;

   // Start is called before the first frame update
   void Start() {
      //mesh = new Mesh();
      //mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      //GetComponent<MeshFilter>().mesh = mesh;
      //Debug.Log("Start Mesh " + mesh + " " + GetComponent<MeshFilter>().mesh);
   }

   // Builds mesh out of chunk data
   public void BuildMesh(List<(Vector2, Vector2)> segments, float width) {
      Debug.Log("BuildMesh: " + segments.Count);
      CreateShape(segments, width);
      //StartCoroutine(CreateShape(chunk));
      UpdateMesh(segments);
      GetComponent<MeshCollider>().sharedMesh = mesh;
   }

   void CreateShape(List<(Vector2, Vector2)> segments, float width) {
      vertices = new Vector3[segments.Count * 4];
      triangles = new int[segments.Count * 6];

      int i = 0;
      foreach ((Vector2, Vector2) seg in segments) {
         Vector2 diff = seg.Item2 - seg.Item1;
         Vector2 orth = new Vector2(-diff.y, diff.x).normalized;

         Vector2 v0 = seg.Item1 - width/2 * orth;
         Vector2 v1 = seg.Item1 + width/2 * orth;
         Vector2 v2 = seg.Item2 - width/2 * orth;
         Vector2 v3 = seg.Item2 + width/2 * orth;
         //Debug.Log(v0 + " " + v1 + " " + v2 + " " + v3);

         float height1 = TerrainGen.GenerateTerrainAt((int)seg.Item1.x, (int)seg.Item1.y) + 0.2f;
         float height2 = TerrainGen.GenerateTerrainAt((int)seg.Item2.x, (int)seg.Item2.y) + 0.2f;

         vertices[i]     = new Vector3(v0.x, height1, v0.y);
         vertices[i + 1] = new Vector3(v1.x, height1, v1.y);
         vertices[i + 2] = new Vector3(v2.x, height2, v2.y);
         vertices[i + 3] = new Vector3(v3.x, height2, v3.y);

         i += 4;
      }
      int segquad = 0;
      int tris = 0;
      foreach ((Vector2, Vector2) seg in segments) {
         triangles[tris + 0] = segquad + 0;
         triangles[tris + 1] = segquad + 1;
         triangles[tris + 2] = segquad + 2;
         triangles[tris + 3] = segquad + 1;
         triangles[tris + 4] = segquad + 3;
         triangles[tris + 5] = segquad + 2;

         segquad += 4;
         tris += 6;
      }
   }

   void UpdateMesh(List<(Vector2, Vector2)> segments) {
      mesh = new Mesh();
      GetComponent<MeshFilter>().mesh = mesh;
      //mesh.Clear();

      mesh.vertices = vertices;
      mesh.triangles = triangles;

      colors = new Color[vertices.Length];
      for (int i = 0; i < colors.Length; i++) {
         colors[i] = Color.yellow;
      }

      mesh.colors = colors;

      mesh.RecalculateNormals();
   }
}
