using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChunkState {
   UNLOADED, // not set up
   PRELOADED, // base data loaded for neighbors to fully load
   LOADED, // fully loaded
}

public class Chunk {
   public Bounds bounds;
   public ChunkState state;

   public float[,] terrainHeightMap;
   public ArrayList waterList;
   public float[,] waterDistanceMap;

   public Chunk(Bounds bounds) {
      this.bounds = bounds;
      state = ChunkState.UNLOADED;
      terrainHeightMap = new float[(int)bounds.dim, (int)bounds.dim];
      waterList = new ArrayList();
      waterDistanceMap = new float[(int)bounds.dim, (int)bounds.dim];
   }

   public void Preload(MonoBehaviour mono) {
      GenerateTerrain();
      mono.StartCoroutine("PrepareWaterList", this);//PrepareWaterList();
   }

   public void Load(ArrayList patchChunks) {
      
      // Water distance
      for (int x = 0; x < (int)bounds.dim; x++) {
         for (int z = 0; z < (int)bounds.dim; z++) {
            waterDistanceMap[x, z] = WorldManager.waterDistanceLimit;
         }
      }
      for (int x = (int)bounds.xMin; x < bounds.xMax; x++) {
         for (int z = (int)bounds.zMin; z < bounds.zMax; z++) {
            foreach (Chunk c in patchChunks) {
               foreach (Vector2Int waterPoint in c.waterList) {
                  Vector2Int curPoint = new Vector2Int(x, z);
                  Vector2Int curPointLocal = GetLocalCoord(new Vector2Int(x, z));
                  float curDist = waterDistanceMap[curPointLocal.x, curPointLocal.y];
                  float thisDist = Vector2Int.Distance(curPoint, waterPoint);
                  if (thisDist < curDist) {
                     waterDistanceMap[curPointLocal.x, curPointLocal.y] = thisDist;
                  }
               }
            }
         }
      }
      state = ChunkState.LOADED;
   }

   void GenerateTerrain() {
      Vector3[] vertices = new Vector3[((int)bounds.dim + 1) * ((int)bounds.dim + 1)];
      Vector2[] UV;
      int[] triangles;
      Color[] colors;

      for (int i = 0, x = (int)bounds.xMin; x < bounds.xMax; x++) {
         for (int z = (int)bounds.zMin; z < bounds.zMax; z++) {
            float y = Mathf.PerlinNoise(x * .03f, z * .03f) * 3f - 1.5f
                     + Mathf.PerlinNoise(x * .01f, z * .01f) * 40f - 20f
                     + Mathf.PerlinNoise(x * .002f, z * .002f) * 150f - 75f
                     + Mathf.PerlinNoise(x * .0008f, z * .0008f) * 300f - 150f
                     + 50;
            y = Mathf.Pow(0.04f * y, 3) + 0.2f * y;

            
            Vector2Int localPoint = GetLocalCoord(new Vector2Int(x, z));
            terrainHeightMap[localPoint.x, localPoint.y] = y;

            //vertices[i] = new Vector3(x, WorldManager.terrainHeightMap[x, z], z);

            i++;
         }
      }

      state = ChunkState.PRELOADED;
   }

   /*IEnumerator PrepareWaterList() {
      for (int i = 0, x = (int)bounds.xMin; x < bounds.xMax; x += 2) {
         for (int z = (int)bounds.zMin; z < bounds.zMax; z += 2) {
            Vector2Int localPoint = GetLocalCoord(new Vector2Int(x, z));
            if (IsShore(localPoint)) {
               waterList.Add(new Vector2Int(x, z));
            }
            i++;
            if (i > 30) {
               i = 0;
               yield return null;
            }
         }
      }
      Debug.Log("water:" + waterList.Count);
      state = ChunkState.PRELOADED;
   }*/

   public bool IsShore(Vector2Int point) {
      if (!IsWater(point))
         return false;

      //Debug.Log(point);
      if ((point + Vector2Int.up).y < bounds.dim && !IsWater(point + Vector2Int.up)) 
         return true;
      if ((point + Vector2Int.down).y >= 0 && !IsWater(point + Vector2Int.down))
         return true;
      if ((point + Vector2Int.left).x >= 0 && !IsWater(point + Vector2Int.left))
         return true;
      if ((point + Vector2Int.right).x < bounds.dim && !IsWater(point + Vector2Int.right))
         return true;

      return false;
   }

   bool IsWater(Vector2Int point) {
      return terrainHeightMap[point.x, point.y] <= 0;
   }

   public Vector2Int GetLocalCoord(Vector2Int global) {
      //Debug.Log(global + " " + bounds.GetCornerVecInt());
      /*if (bounds.GetCornerVecInt().x < 0) {
         return global + bounds.GetCornerVecInt();
      }
      else {
         return global - bounds.GetCornerVecInt();
      }*/
      return global - bounds.GetCornerVecInt();
   }

   public Vector2Int GetGlobalCoord(Vector2Int local) {
      /*if (bounds.GetCornerVecInt().x < 0) {
         return local - bounds.GetCornerVecInt();
      }
      else {
         return local + bounds.GetCornerVecInt();
      }*/
      return local + bounds.GetCornerVecInt();
   }
}

public class Bounds {
   public float dim, xMin, xMax, zMin, zMax;

   public Bounds(float dim, float xMin, float zMin) {
      this.dim = dim;
      this.xMin = xMin;
      this.xMax = xMin + dim;
      this.zMin = zMin;
      this.zMax = zMin + dim;
   }

   public Vector2Int GetCornerVecInt() {
      return new Vector2Int((int)xMin, (int)zMin);
   }
}
