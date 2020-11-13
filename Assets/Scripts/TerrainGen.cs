using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGen : MonoBehaviour {
   int xSize = WorldManager.dim;
   int zSize = WorldManager.dim;

   private void Start() {
      GenHeightmap();
   }

   void GenHeightmap() {
      for (int i = 0, z = 0; z < zSize; z++) {
         for (int x = 0; x < xSize; x++) {
            float y = Mathf.PerlinNoise(x * .03f, z * .03f) * 3f - 1.5f
                     + Mathf.PerlinNoise(x * .01f, z * .01f) * 40f - 20f
                     + Mathf.PerlinNoise(x * .002f, z * .002f) * 150f - 75f
                     + Mathf.PerlinNoise(x * .0008f, z * .0008f) * 300f - 150f
                     + 50;
            y = Mathf.Pow(0.04f * y, 3) + 0.2f * y;
            WorldManager.terrainHeightMap[x, z] = y;

            i++;
         }
      }

   }

   private void Update() {
      //UpdateMesh();
   }
}
