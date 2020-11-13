using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour {
   public Gradient waterGradient;
   Mesh mesh;

   Vector3[] vertices;
   Vector2[] UV;
   int[] triangles;
   Color[] colors;

   int xSize = WorldManager.dim;
   int zSize = WorldManager.dim;

   // Start is called before the first frame update
   void Start() {
      mesh = new Mesh();
      mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      GetComponent<MeshFilter>().mesh = mesh;

      CreateShape();
      UpdateMesh();
      GetComponent<MeshCollider>().sharedMesh = mesh;
   }


   void CreateShape() {
      vertices = new Vector3[(xSize + 1) * (zSize + 1)];

      for (int i = 0, z = 0; z < zSize; z++) {
         for (int x = 0; x < xSize; x++) {
            if (x < xSize && z < zSize) {
               vertices[i] = new Vector3(x, WorldManager.terrainHeightMap[x, z], z);
            }
            i++;
         }
      }

      triangles = new int[xSize * zSize * 6];
      int vert = 0;
      int tris = 0;
      for (int z = 0; z < zSize - 1; z++) {
         for (int x = 0; x < xSize - 1; x++) {
            triangles[tris + 0] = vert + 0;
            triangles[tris + 1] = vert + xSize + 1;
            triangles[tris + 2] = vert + 1;
            triangles[tris + 3] = vert + 1;
            triangles[tris + 4] = vert + xSize + 1;
            triangles[tris + 5] = vert + xSize + 2;

            vert++;
            tris += 6;
         }
         vert++;
      }
   }

   public void UpdateMesh() {
      mesh.Clear();

      mesh.vertices = vertices;
      mesh.triangles = triangles;

      colors = new Color[vertices.Length];
      for (int i = 0, z = 0; z < zSize; z++) {
         for (int x = 0; x < xSize; x++) {
            float val = WorldManager.waterDistanceArr[i] / WorldManager.waterDistanceLimit;
            colors[i] = Color.Lerp(Color.red, Color.green, val);
            i++;
         }
      }
      mesh.colors = colors;
      for (int i = 0; i < 1000; i+=50) {
         Debug.Log(WorldManager.waterDistanceArr[i] + " " + WorldManager.waterDistanceLimit);
      }

      mesh.RecalculateNormals();
   }
   Color ColorFromGradient(Gradient gradient, float value) {
      return gradient.Evaluate(value);
   }
}
