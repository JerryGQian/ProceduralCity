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
   int dim = WorldManager.dim;

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

      /*triangles = new int[xSize * zSize * 6];
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
      }*/

      List<int> tris = new List<int>();
      //Bottom left section of the map, other sections are similar
      for (int i = 0; i < dim; i++) {
         for (int j = 0; j < dim; j++) {            
            //Skip if a new square on the plane hasn't been formed
            if (i == 0 || j == 0) continue;
            //Adds the index of the three vertices in order to make up each of the two tris
            tris.Add(dim * i + j); //Top right
            tris.Add(dim * i + j - 1); //Bottom right
            tris.Add(dim * (i - 1) + j - 1); //Bottom left - First triangle
            tris.Add(dim * (i - 1) + j - 1); //Bottom left 
            tris.Add(dim * (i - 1) + j); //Top left
            tris.Add(dim * i + j); //Top right - Second triangle
         }
      }
      triangles = tris.ToArray();
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

      mesh.RecalculateNormals();
   }
   Color ColorFromGradient(Gradient gradient, float value) {
      return gradient.Evaluate(value);
   }
}
