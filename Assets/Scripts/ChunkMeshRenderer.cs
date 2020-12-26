using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkMeshRenderer : MonoBehaviour {
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
   public void BuildMesh(Chunk chunk) {
      //Debug.Log("BuildMesh: " + mesh);
      CreateShape(chunk);
      UpdateMesh(chunk);
      GetComponent<MeshCollider>().sharedMesh = mesh;
   }

   void CreateShape(Chunk chunk) {
      int dim = (int)chunk.bounds.dim;
      vertices = new Vector3[(dim + 1) * (dim + 1)];

      for (int i = 0, z = 0; z < dim; z++) {
         for (int x = 0; x < dim; x++) {
            if (x < dim && z < dim) {
               Vector2Int globalCoord = chunk.GetGlobalCoord(new Vector2Int(x, z));
               vertices[i] = new Vector3(globalCoord.x, chunk.terrainHeightMap[x, z], globalCoord.y);
            }
            i++;
         }
      }

      triangles = new int[dim * dim * 6];
      int vert = 0;
      int tris = 0;
      for (int z = 0; z < dim - 1; z++) {
         for (int x = 0; x < dim - 1; x++) {
            triangles[tris + 0] = vert + 0;
            triangles[tris + 1] = vert + dim + 1;
            triangles[tris + 2] = vert + 1;
            triangles[tris + 3] = vert + 1;
            triangles[tris + 4] = vert + dim + 1;
            triangles[tris + 5] = vert + dim + 2;

            vert++;
            tris += 6;
         }
         vert++;
      }
   }

   void UpdateMesh(Chunk chunk) {
      int dim = (int)chunk.bounds.dim;
      mesh = new Mesh();
      GetComponent<MeshFilter>().mesh = mesh;
      //mesh.Clear();

      mesh.vertices = vertices;
      mesh.triangles = triangles;

      colors = new Color[vertices.Length];
      for (int i = 0, z = 0; z < dim; z++) {
         for (int x = 0; x < dim; x++) {
            float val = chunk.waterDistanceMap[x,z] / WorldManager.waterDistanceLimit;
            colors[i] = Color.Lerp(Color.red, Color.green, val);
            i++;
         }
      }
      mesh.colors = colors;

      mesh.RecalculateNormals();
      
   }
}
