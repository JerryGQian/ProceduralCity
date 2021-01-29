using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGen {


   public static float GenerateTerrainAt(int x, int z) {
      // small to big
      float y = Mathf.PerlinNoise(x * .030f + 15000, z * .030f + 15000) * 4f - 2f
                     + Mathf.PerlinNoise(x * .030f + 10000, z * .030f + 10000) * 4f - 2f
                     + Mathf.PerlinNoise(x * .004f + 10000, z * .004f + 10000) * 50f - 25f
                     + 8f;
      y = Mathf.Pow(0.12f * y, 3) + 0.2f * y;
      return y;
   }
}
