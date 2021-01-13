using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RegionState {
   UNEXECUTED, // not set up
   EXECUTED, // fully loaded
   PATCH_EXECUTED,
}

public class Region {
   public Bounds bounds;
   public RegionState state = RegionState.UNEXECUTED;
   public Vector2Int regionIdx;
   public bool generated = false; // for debugging vis
   public CoordRandom rand;

   ArrayList densityClusters;
   public ArrayList densityCenters;
   ArrayList densityChunks;
   ArrayList densityRanking;

   float[,] densitySnapshots = new float[WorldManager.regionDim, WorldManager.regionDim];
   float distanceThreshold = 5f;

   public Region(Vector2Int regionIdx, float[,] densitySnapshots) {
      float regionSize = WorldManager.regionDim * WorldManager.chunkSize;
      bounds = new Bounds(regionSize, regionIdx.x * regionSize, regionIdx.y * regionSize);
      this.regionIdx = regionIdx;
      this.densitySnapshots = densitySnapshots;
      rand = new CoordRandom(regionIdx);
      densityClusters = new ArrayList();
      densityCenters = new ArrayList();
      densityChunks = new ArrayList();
      densityRanking = new ArrayList();
   }

   public void CalcDensityCenters() {
      if (state != RegionState.EXECUTED) {
         for (int i = 0; i < WorldManager.regionDim; i++) {
            for (int j = 0; j < WorldManager.regionDim; j++) {
               if (densityRanking.Count == 0) {
                  densityRanking.Add(new Vector2Int(i, j));
               }
               else {
                  for (int idx = 0; idx < densityRanking.Count; idx++) {
                     Vector2Int currIdx = (Vector2Int)densityRanking[idx];
                     float currDensity = densitySnapshots[currIdx.x, currIdx.y];
                     if (currDensity < densitySnapshots[i, j]) {
                        densityRanking.Insert(idx, new Vector2Int(i, j));
                        break;
                     }
                     else if (idx == densityRanking.Count-1) { //Add to end
                        densityRanking.Add(new Vector2Int(i, j));
                        break;
                     }
                  }
               }
            }
         }

         // determine # centers based on highest density
         int centers = 0;
         Vector2Int top = (Vector2Int)densityRanking[0];
         if (densitySnapshots[(int)top.x, (int)top.y] == 1f) {
            centers = 3;
         }
         else if (densitySnapshots[(int)top.x, (int)top.y] > 0.85f) {
            centers = 2;
         }
         else if (densitySnapshots[(int)top.x, (int)top.y] > 0.5f) {
            centers = 1;
         }

         // choose n coordinates from the top rankings as centers
         for (int i = 0; i < centers; i++) {
            Vector2Int v = (Vector2Int)densityRanking[i];
            if (!TooClose(v, densityChunks)) {
               densityChunks.Add(v);
               /*if (i == 0) {
                  densityClusters.Add(v);
               }*/
            }
            else {
               centers++;
            }
         }

         // converting density chunk indices to world coordinates
         for (int i = 0; i < densityChunks.Count; i++) {
            Vector2Int chunk = (Vector2Int)densityChunks[i];
            Vector2 center = C2W(chunk) + regionIdx * WorldManager.regionDim * WorldManager.chunkSize;
            center += rand.NextVector2(0, 10);

            densityCenters.Add(center);
         }
         //Debug.Log("Total centers: " + densityCenters.Count);

         

         state = RegionState.EXECUTED;
      }
   }

   private bool TooClose(Vector2Int v, ArrayList list) {
      foreach (Vector2Int existingV in list) {
         if ((v-existingV).magnitude < distanceThreshold) {
            return true;
         }
      }
      return false;
   }

   // chunk idx to world distance
   private Vector2 C2W(Vector2Int chunkCoord) {
      float halfChunksize = WorldManager.chunkSize / 2;
      return new Vector2(chunkCoord.x * WorldManager.chunkSize, chunkCoord.y * WorldManager.chunkSize);// + new Vector2(halfChunksize, halfChunksize);
   }
}
