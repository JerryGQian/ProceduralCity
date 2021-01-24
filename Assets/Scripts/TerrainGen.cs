using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGen {


   public static float GenerateTerrainAt(int x, int z) {
      float y = Mathf.PerlinNoise(x * .030f + 15000, z * .030f + 15000) * 10f - 5f
                     + Mathf.PerlinNoise(x * .030f + 10000, z * .030f + 10000) * 10f - 5f
                     + Mathf.PerlinNoise(x * .004f + 10000, z * .004f + 10000) * 25f - 5f
                     + 4.5f;
      y = Mathf.Pow(0.12f * y, 3) + 0.2f * y;
      return y;
   }
}
