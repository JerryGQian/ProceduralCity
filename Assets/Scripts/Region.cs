using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RegionState {
   UNEXECUTED, // not set up
   EXECUTED, // fully loaded
   PATCH_EXECUTED,
   ARTERIAL_LAYOUT_PREPARED
}

public class Region {
   public Bounds bounds;
   public RegionState state = RegionState.UNEXECUTED;
   public Vector2Int regionIdx;

   public bool generated = false; // for debugging vis
   public bool highwayCentersGenerated = false;
   public bool highwayPatchExecuted = false;
   public bool highwaysBuilt = false;
   public bool arterialLayoutGenerated = false;
   public bool arterialLayoutBuilt = false; // for debugging vis
   //public bool arterialRoadsBuilt = false;

   public CoordRandom rand;

   public ArrayList densityCenters;
   ArrayList densityChunks;
   ArrayList densityRanking;

   public float[,] densitySnapshots = new float[WorldManager.regionDim, WorldManager.regionDim];
   public Dictionary<Vector2Int, float> densitySnapshotsMap;
   float distanceThreshold = 9f;

   //highway vars
   public HighwayGenerator hwg;

   //arterial road vars
   public ArterialGenerator atg;
   

   public Region(Vector2Int regionIdx, float[,] densitySnapshots, Dictionary<Vector2Int, float> densitySnapshotsMap) {
      float regionSize = WorldManager.regionDim * WorldManager.chunkSize;
      bounds = new Bounds(regionSize, regionIdx.x * regionSize, regionIdx.y * regionSize);
      this.regionIdx = regionIdx;
      this.densitySnapshots = densitySnapshots;
      this.densitySnapshotsMap = densitySnapshotsMap;
      rand = new CoordRandom(regionIdx);
      densityCenters = new ArrayList();
      densityChunks = new ArrayList();
      densityRanking = new ArrayList();
   }

   public void CalcDensityCenters() {
      if (!highwayCentersGenerated) {
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
         if (densitySnapshots[(int)top.x, (int)top.y] == 0.9f) {
            centers = 3;
         }
         else if (densitySnapshots[(int)top.x, (int)top.y] > 0.7f) {
            centers = 2;
         }
         else if (densitySnapshots[(int)top.x, (int)top.y] > 0.2f) {
            centers = 1;
         }

         // choose n coordinates from the top rankings as centers
         for (int i = 0; i < centers; i++) {
            Vector2Int v = (Vector2Int)densityRanking[i];
            Vector2 worldV = C2W(v) + regionIdx * WorldManager.regionDim * WorldManager.chunkSize;
            Vector2Int worldVInt = Util.VecToVecInt(worldV);
            if (!TooClose(v, densityChunks) && TerrainGen.GenerateTerrainAt(worldVInt.x, worldVInt.y) > 0) {
               //Debug.Log(TerrainGen.GenerateTerrainAt(v.x, v.y) + " " + v);
               densityChunks.Add(v);
            }
            else {
               centers++;
            }
         }

         // converting density chunk indices to world coordinates
         for (int i = 0; i < densityChunks.Count; i++) {
            Vector2Int chunk = (Vector2Int)densityChunks[i];
            Vector2 center = C2W(chunk) + regionIdx * WorldManager.regionDim * WorldManager.chunkSize;
            //center += rand.NextVector2(0, 10);

            densityCenters.Add((center, densitySnapshots[chunk.x, chunk.y]));
         }

         //state = RegionState.EXECUTED;
         highwayCentersGenerated = true;
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
