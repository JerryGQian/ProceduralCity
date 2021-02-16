using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ChunkState {
   UNLOADED, // not set up
   SNAPSHOTTED, // snapshot 
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
   public float avgDensity;
   public float terrainSnapshot;
   public float densitySnapshot;

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
      avgDensity = 0;
      terrainSnapshot = 0;
      densitySnapshot = 0;

      waterList = new ArrayList();
      patchWaterDistanceMap = new float[(int)bounds.dim*5, (int)bounds.dim*5];
      patchBounds = new Bounds(bounds.dim * 5, bounds.xMin - bounds.dim*2, bounds.zMin - bounds.dim*2);

      //Debug.Log(Mathf.PerlinNoise(100 * .003f + 30000, 100 * .003f + 30000) + " " + Mathf.PerlinNoise(101 * .003f + 30000, 100 * .003f + 30000));
   }

   // Loads cheap snapshot of chunk
   public void Snapshot() {
      if (state >= ChunkState.SNAPSHOTTED)
         return;
      
      Vector2Int center = bounds.GetCenter();
      Vector2Int localCenter = GetLocalCoord(center);

      terrainSnapshot = TerrainGen.GenerateTerrainAt(center.x, center.y);
      terrainHeightMap[localCenter.x, localCenter.y] = terrainSnapshot;

      densitySnapshot = CalculateDensityAt(center.x, center.y);
      densityMap[localCenter.x, localCenter.y] = densitySnapshot;

      state = ChunkState.SNAPSHOTTED;
   }

   // Preloads some data
   public void Preload(MonoBehaviour mono) {
      if (state >= ChunkState.PRELOADED)
         return;

      GenerateTerrain();
      PrepareWaterList();
      //mono.StartCoroutine("PrepareWaterList", this);//PrepareWaterList();
      state = ChunkState.PRELOADED;
   }

   // Loads actual structure
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
      int i = 0;
      // Enqueue initial shore points
      foreach (Chunk c in patchChunks) {
         foreach (Vector2Int waterPoint in c.waterList) {
            Vector2Int patchLocalPoint = GetLocalPatchCoord(waterPoint);
            queue.Enqueue(waterPoint);
            visited.Add(waterPoint);
            /*if (i % 20 == 0) {
               Debug.Log(patchLocalPoint.x + " " + patchLocalPoint.y);
            }*/
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
      float sum = 0;
      int count = 0;
      for (int i = 0, x = (int)bounds.xMin; x < bounds.xMax + 1; x++) {
         for (int z = (int)bounds.zMin; z < bounds.zMax + 1; z++) {
            Vector2Int globalPoint = new Vector2Int(x, z);
            Vector2Int localPoint = GetLocalCoord(globalPoint);

            float density = CalculateDensityAt(x, z);

            if (terrainHeightMap[localPoint.x, localPoint.y] > 0) {
               sum += density;
               count++;
            }

            densityMap[localPoint.x, localPoint.y] = density;
         }
      }
      avgDensity = sum / count;
   }
   float CalculateDensityAt(int x, int z) {
      Vector2Int globalPoint = new Vector2Int(x, z);
      Vector2Int localPoint = GetLocalCoord(globalPoint);
      
      float density = 0.3f + 0.2f * Mathf.PerlinNoise(x * .02f + 500000, z * .03f + 500000)
                     + 0.8f * Mathf.PerlinNoise(x * .01f + 500000, z * .015f + 500000);
      /*if (state >= ChunkState.PRELOADED) 
         density = nearbyWater(density, localPoint, globalPoint);*/
      density = mountainPenalty(density, localPoint);
      density = wildernessNoisePenalty(density, globalPoint);
      density = distancePenalty(density, globalPoint);

      if (density > 1) {
         density = 1;
      }

      return density;
   }
   float mountainPenalty(float d, Vector2Int p) {
      float limit = 5;
      if (terrainHeightMap[p.x, p.y] > limit) {
         d /= (terrainHeightMap[p.x, p.y] - limit) / 5 + 1;
      }
      return d;
   }

   float nearbyWater(float d, Vector2Int lp, Vector2Int gp) {
      float mask = Mathf.PerlinNoise(gp.x * .008f + 800000, gp.y * .008f + 800000);
      float temp = d * 2 * WorldManager.waterDistanceLimit / (WorldManager.waterDistanceLimit + waterDistanceMap[lp.x, lp.y]);
      temp -= d;
      temp *= mask;
      d += temp;
      if (d > 1) {
         d = 1;
      }
      return d;
   }

   float distancePenalty(float d, Vector2Int p) {
      if (p.magnitude > 500) {
         d /= ((500 - 300) / 200) + 1;
      }
      else if (p.magnitude > 300) {
         d /= ((p.magnitude - 300) / 200) + 1; // d /= ((p.magnitude - 300) / 200) + 1;
      }

      return d;
   }

   float wildernessNoisePenalty(float d, Vector2Int gp) {
      float mask = Mathf.PerlinNoise(gp.x * .003f + 30000, gp.y * .003f + 30000);
      mask *= 2.2f;
      if (mask > 1f) {
         mask = 1f;
      }

      if (gp.magnitude < 400) {
         float rem = 1f - mask;
         mask += (1-(gp.magnitude/400)) * rem;
      }

      d *= mask;

      return d;
   }

   void GenerateTerrain() {
      for (int x = (int)bounds.xMin; x < bounds.xMax+1; x++) {
         for (int z = (int)bounds.zMin; z < bounds.zMax+1; z++) {
            Vector2Int localPoint = GetLocalCoord(new Vector2Int(x, z));
            terrainHeightMap[localPoint.x, localPoint.y] = TerrainGen.GenerateTerrainAt(x,z);
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

   public Vector2Int GetCenter() {
      return new Vector2Int((int)(xMin + xMax)/2, (int)(zMin + zMax)/2);
   }

   public bool InBounds(Vector2 v) {
      return v.x >= xMin && v.x < xMax && v.y >= zMin && v.y < zMax;
   }
}
