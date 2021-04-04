using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGen {

   public static float GenerateTerrainAt(Vector2Int v) {
      return GenerateTerrainAt(v.x, v.y);
   }
   public static float GenerateTerrainAt(int x, int z) {
      // small to big
      float y = Mathf.PerlinNoise(x * .030f + 15000, z * .030f + 15000) * 4f - 2f
                     + Mathf.PerlinNoise(x * .030f + 10000, z * .030f + 10000) * 4f - 2f
                     + Mathf.PerlinNoise(x * .004f + 10000, z * .004f + 10000) * 40f - 20f
                     + 10.5f;
      y = Mathf.Pow(0.12f * y, 3) + 0.2f * y;
      return y;
   }

   public static bool IsWaterAt(int x, int z) {
      return GenerateTerrainAt(x, z) <= 0;
   }

   public static bool IsWaterAt(Vector2 p) {
      return IsWaterAt((int)p.x, (int)p.y);
   }


   // DENSITY CALCS
   public static float CalculateDensityAtChunk(Vector2Int chunkIdx) {
      Vector2 w = Util.C2W(chunkIdx);
      return CalculateDensityAt(new Vector2Int((int)w.x, (int)w.y));
   }
   public static float CalculateDensityAt(Vector2Int v) {
      //Vector2Int localPoint = GetLocalCoord(globalPoint);

      float density = 0.35f + 0.2f * Mathf.PerlinNoise(v.x * .02f + 500000, v.y * .03f + 500000)
                     + 0.8f * Mathf.PerlinNoise(v.x * .01f + 500000, v.y * .015f + 500000);
      /*if (state >= ChunkState.PRELOADED) 
         density = nearbyWater(density, localPoint, globalPoint);*/
      density = mountainPenalty(density, v);
      //density = wildernessNoisePenalty(density, globalPoint);
      density = distancePenalty(density, v);

      if (density > 1) {
         density = 1;
      }

      return density;
   }
   public static float mountainPenalty(float d, Vector2Int p) {
      float limit = 5;
      if (GenerateTerrainAt(p) > limit) {
         d /= (GenerateTerrainAt(p) - limit) / 2 + 1;
      }
      return d;
   }

   /*public static float nearbyWater(float d, Vector2Int lp, Vector2Int gp) {
      float mask = Mathf.PerlinNoise(gp.x * .008f + 800000, gp.y * .008f + 800000);
      float temp = d * 2 * WorldManager.waterDistanceLimit / (WorldManager.waterDistanceLimit + waterDistanceMap[lp.x, lp.y]);
      temp -= d;
      temp *= mask;
      d += temp;
      if (d > 1) {
         d = 1;
      }
      return d;
   }*/

   public static float distancePenalty(float d, Vector2Int p) {
      if (p.magnitude > 600) {
         d /= ((600 - 400) / 200) + 1;
      }
      else if (p.magnitude > 400) {
         d /= ((p.magnitude - 400) / 200) + 1; // d /= ((p.magnitude - 300) / 200) + 1;
      }

      return d;
   }
}
