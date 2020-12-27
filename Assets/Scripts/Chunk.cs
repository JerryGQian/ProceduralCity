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
   public ArrayList waterList;
   public float[,] waterDistanceMap; // actual map for this chunk
   public float[,] patchWaterDistanceMap; // for water distance gen only
   public Bounds patchBounds; // for patchWaterDistanceMap

   public Chunk(Bounds bounds) {
      this.bounds = bounds;
      state = ChunkState.UNLOADED;
      terrainHeightMap = new float[(int)bounds.dim, (int)bounds.dim];
      waterList = new ArrayList();
      waterDistanceMap = new float[(int)bounds.dim, (int)bounds.dim];

      patchWaterDistanceMap = new float[(int)bounds.dim*3, (int)bounds.dim*3];
      patchBounds = new Bounds(bounds.dim * 3, bounds.xMin - bounds.dim, bounds.zMin - bounds.dim);
   }

   public void Preload(MonoBehaviour mono) {
      GenerateTerrain();
      mono.StartCoroutine("PrepareWaterList", this);//PrepareWaterList();
   }

   public void Load(ArrayList patchChunks) {
      //mono.StartCoroutine(LoadCoroutine(patchChunks));
      int count = 0;

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
            count++;
            Vector2Int localCurrPoint = GetLocalPatchCoord(currPoint);
            Vector2Int altPoint = currPoint + dir;
            Vector2Int localAltPoint = localCurrPoint + dir;

            float dist = patchWaterDistanceMap[localAltPoint.x, localAltPoint.y] + dir.magnitude;

            if (!visited.Contains(altPoint) && InPatch(altPoint)) { //does calc for water because no access to patch terrainHeightMaps, but water doesnt really matter
               //Debug.Log("adding!" + localAltPoint);
               queue.Enqueue(altPoint);
               visited.Add(altPoint);
            }

            if (dist < patchWaterDistanceMap[localCurrPoint.x, localCurrPoint.y]) {
               patchWaterDistanceMap[localCurrPoint.x, localCurrPoint.y] = dist;
            }


         }
      }
      // Copy over center of patch calcs as waterDistanceMap for this chunk
      for (int x = 0; x < (int)bounds.dim; x++) {
         for (int z = 0; z < (int)bounds.dim; z++) {
            count++;
            //Debug.Log("END: " + x + " " + z + " : " + (x + (int)bounds.dim) + " " + (z + (int)bounds.dim));
            waterDistanceMap[x, z] = patchWaterDistanceMap[x + (int)bounds.dim, z + (int)bounds.dim];
         }
      }


      //
      /*
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
                  count++;
               }
            }
         }
      }*/
      state = ChunkState.LOADED;
      Debug.Log(count);
   }

   IEnumerator LoadCoroutine(ArrayList patchChunks) {
      int count = 0;

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
            //Vector2Int globalPoint = GetGlobalCoord(c, waterPoint);
            //Debug.Log("globalPoint: " + waterPoint + " " + c.bounds.GetCornerVecInt());
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
            count++;
            Vector2Int localCurrPoint = GetLocalPatchCoord(currPoint);
            Vector2Int altPoint = currPoint + dir;
            Vector2Int localAltPoint = localCurrPoint + dir;

            float dist = patchWaterDistanceMap[localAltPoint.x, localAltPoint.y] + dir.magnitude;

            if (!visited.Contains(altPoint) && InPatch(altPoint)) { //does calc for water because no access to patch terrainHeightMaps, but water doesnt really matter
               //Debug.Log("adding!" + localAltPoint);
               queue.Enqueue(altPoint);
               visited.Add(altPoint);
            }

            if (dist < patchWaterDistanceMap[localCurrPoint.x, localCurrPoint.y]) {
               patchWaterDistanceMap[localCurrPoint.x, localCurrPoint.y] = dist;
            }


         }
      }
      yield return null;
      // Copy over center of patch calcs as waterDistanceMap for this chunk
      for (int x = 0; x < (int)bounds.dim; x++) {
         for (int z = 0; z < (int)bounds.dim; z++) {
            count++;
            //Debug.Log("END: " + x + " " + z + " : " + (x + (int)bounds.dim) + " " + (z + (int)bounds.dim));
            waterDistanceMap[x, z] = patchWaterDistanceMap[x + (int)bounds.dim, z + (int)bounds.dim];
         }
      }
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

   public bool InPatch(Vector2Int point) {
      Vector2Int local = GetLocalPatchCoord(point);
      return local.x >= 0 && local.y >= 0 && local.x < patchBounds.dim && local.y < patchBounds.dim;
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
