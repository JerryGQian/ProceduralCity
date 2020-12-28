using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChunkState {
   UNLOADED, // not set up
   PRELOADED, // base data loaded for neighbors to fully load
   LOADED, // fully loaded
}

public class Chunk {
   public static ArrayList dirs = new ArrayList() {
      Vector2Int.up,
      Vector2Int.down,
      Vector2Int.left,
      Vector2Int.right,
      new Vector2Int(-1,-1),
      new Vector2Int(1,-1),
      new Vector2Int(1,1),
      new Vector2Int(-1,1),
   };

   public Bounds bounds;
   public ChunkState state;

   public float[,] terrainHeightMap;
   public float[,] waterDistanceMap; // actual map for this chunk
   public float[,] densityMap; // population and activity density

   private ArrayList waterList; // list of shore coordinates
   private float[,] patchWaterDistanceMap; // for water distance gen only
   private Bounds patchBounds; // for patchWaterDistanceMap

   

   public Chunk(Bounds bounds) {
      this.bounds = bounds;
      state = ChunkState.UNLOADED;
      // We take dim+1 because mesh requires beginning of next chunk to render continuously
      terrainHeightMap = new float[(int)bounds.dim+1, (int)bounds.dim+1];
      waterDistanceMap = new float[(int)bounds.dim+1, (int)bounds.dim+1];
      densityMap = new float[(int)bounds.dim + 1, (int)bounds.dim + 1];

      waterList = new ArrayList();
      patchWaterDistanceMap = new float[(int)bounds.dim*3, (int)bounds.dim*3];
      patchBounds = new Bounds(bounds.dim * 3, bounds.xMin - bounds.dim, bounds.zMin - bounds.dim);
   }

   public void Preload(MonoBehaviour mono) {
      GenerateTerrain();
      PrepareWaterList();
      //mono.StartCoroutine("PrepareWaterList", this);//PrepareWaterList();
   }

   public void Load(ArrayList patchChunks) {
      CalcShoreDistance(patchChunks);
      CalcDensity();
      state = ChunkState.LOADED;
   }

   public void CalcShoreDistance(ArrayList patchChunks) {
      for (int x = 0; x < (int)patchBounds.dim; x++) {
         for (int z = 0; z < (int)patchBounds.dim; z++) {
            patchWaterDistanceMap[x, z] = WorldManager.waterDistanceLimit;
         }
      }

      Queue<Vector2Int> queue = new Queue<Vector2Int>();
      HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
      // Enqueue initial shore points
      foreach (Chunk c in patchChunks) {
         foreach (Vector2Int waterPoint in c.waterList) {
            Vector2Int patchLocalPoint = GetLocalPatchCoord(waterPoint);
            queue.Enqueue(waterPoint);
            visited.Add(waterPoint);
            patchWaterDistanceMap[patchLocalPoint.x, patchLocalPoint.y] = 0;
         }
      }
      
      // Process queued point
      while (queue.Count > 0) {
         Vector2Int currPoint = queue.Dequeue();
         if (!InPatch(currPoint)) {
            continue;
         }
         foreach (Vector2Int dir in dirs) { // in all 8 directions, finds min water distance + diff
            if (!InPatch(currPoint + dir)) {
               continue;
            }
            Vector2Int localCurrPoint = GetLocalPatchCoord(currPoint);
            Vector2Int altPoint = currPoint + dir;
            Vector2Int localAltPoint = localCurrPoint + dir;

            float dist = patchWaterDistanceMap[localAltPoint.x, localAltPoint.y] + dir.magnitude;

            if (!visited.Contains(altPoint) && InPatch(altPoint)) { //does calc for water because no access to patch terrainHeightMaps, but water doesnt really matter
               queue.Enqueue(altPoint);
               visited.Add(altPoint);
            }

            if (dist < patchWaterDistanceMap[localCurrPoint.x, localCurrPoint.y]) {
               patchWaterDistanceMap[localCurrPoint.x, localCurrPoint.y] = dist;
            }
         }
      }
      // Copy over center of patch calcs as waterDistanceMap for this chunk
      for (int x = 0; x < (int)bounds.dim+1; x++) {
         for (int z = 0; z < (int)bounds.dim+1; z++) {
            waterDistanceMap[x, z] = patchWaterDistanceMap[x + (int)bounds.dim, z + (int)bounds.dim];
         }
      }
   }

   public void CalcDensity() {
      float mountainPenalty(float d, Vector2Int p) {
         float limit = 12;
         if (terrainHeightMap[p.x, p.y] > limit) {
            d /= (terrainHeightMap[p.x, p.y] - limit) / 15 + 1;
         }
         return d;
      }

      float nearbyWater(float d, Vector2Int p) {
         d += ((WorldManager.waterDistanceLimit / waterDistanceMap[p.x, p.y])-1)/10;
         return d;
      }

      for (int i = 0, x = (int)bounds.xMin; x < bounds.xMax + 1; x++) {
         for (int z = (int)bounds.zMin; z < bounds.zMax + 1; z++) {
            Vector2Int localPoint = GetLocalCoord(new Vector2Int(x, z));

            float density = (Mathf.PerlinNoise(x * .01f + 500000, z * .01f + 500000)
                           + Mathf.PerlinNoise(x * .004f + 500000, z * .004f + 500000))/2;
            density = nearbyWater(density, localPoint);
            density = mountainPenalty(density, localPoint);            

            densityMap[localPoint.x, localPoint.y] = density;
         }
      }
   }

   void GenerateTerrain() {
      for (int i = 0, x = (int)bounds.xMin; x < bounds.xMax+1; x++) {
         for (int z = (int)bounds.zMin; z < bounds.zMax+1; z++) {
            float y = Mathf.PerlinNoise(x * .05f + 10000, z * .05f + 10000) * 12f - 6f
                     + Mathf.PerlinNoise(x * .01f + 10000, z * .01f + 10000) * 60f - 30f
                     + Mathf.PerlinNoise(x * .004f + 10000, z * .004f + 10000) * 120f - 60f
                     + 40;
            y = Mathf.Pow(0.04f * y, 3) + 0.2f * y;

            Vector2Int localPoint = GetLocalCoord(new Vector2Int(x, z));
            terrainHeightMap[localPoint.x, localPoint.y] = y;
            if (localPoint.x == WorldManager.dim && localPoint.y == WorldManager.dim)
               Debug.Log("Corner ->:" + y);
            i++;
         }
      }
      state = ChunkState.PRELOADED;
   }

   void PrepareWaterList() {
      for (int x = (int)bounds.xMin; x < bounds.xMax; x += 2) {
         for (int z = (int)bounds.zMin; z < bounds.zMax; z += 2) {
            Vector2Int localPoint = GetLocalCoord(new Vector2Int(x, z));
            if (IsShore(localPoint)) {
               waterList.Add(new Vector2Int(x, z));
            }
         }
      }
      state = ChunkState.PRELOADED;
   }

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

   public bool InPatch(Vector2Int point) {
      Vector2Int local = GetLocalPatchCoord(point);
      return local.x >= 0 && local.y >= 0 && local.x < patchBounds.dim && local.y < patchBounds.dim;
   }

   public Vector2Int GetLocalCoord(Vector2Int global) {
      return global - bounds.GetCornerVecInt();
   }
   public Vector2Int GetGlobalCoord(Vector2Int local) {
      return local + bounds.GetCornerVecInt();
   }
   public Vector2Int GetGlobalCoord(Chunk chunk, Vector2Int local) {
      return local + chunk.bounds.GetCornerVecInt();
   }

   public Vector2Int GetLocalPatchCoord(Vector2Int global) {
      return global - patchBounds.GetCornerVecInt();
   }
   public Vector2Int GetGlobalPatchCoord(Vector2Int local) {
      return local + patchBounds.GetCornerVecInt();
   }
   public static float Sigmoid(float value) {
      return 1.0f / (1.0f + (float)Mathf.Exp(-value));
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
