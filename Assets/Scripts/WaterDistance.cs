using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterDistance : MonoBehaviour {
   public float maxDistance = WorldManager.waterDistanceLimit;
   public Vector2Int[] octDeltas = new Vector2Int[]{
      new Vector2Int(0,1),
      new Vector2Int(1,1),
      new Vector2Int(1,0),
      new Vector2Int(1,-1),
      new Vector2Int(0,-1),
      new Vector2Int(-1,-1),
      new Vector2Int(-1,0),
      new Vector2Int(-1,1),
   };
   public Vector2Int[] octState = new Vector2Int[8];

   // Start is called before the first frame update
   void Start() {
      CalcShoreDistances();
   }

   void CalcShoreDistances() {
      for (int z = 0; z < WorldManager.dim; z++) {
         for (int x = 0; x < WorldManager.dim; x++) {
            float dist = CalcShoreDistance(new Vector2Int(x, z));
            WorldManager.waterDistanceMap[x, z] = dist;
            WorldManager.waterDistanceArr[x + z * WorldManager.dim] = dist;
         }
      }
   }

   float CalcShoreDistance(Vector2Int center) {
      if (WorldManager.terrainHeightMap[center.x, center.y] <= 0f) {
         return 0f;
      }

      HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
      Queue<Vector2Int> toVisit = new Queue<Vector2Int>();

      toVisit.Enqueue(center);

      void AddIfUnvisited(Vector2Int node) {
         if (!visited.Contains(node)) {
            toVisit.Enqueue(node);
         }
      }
      bool printed = false;

      while (toVisit.Count > 0) {
         Vector2Int curr = toVisit.Dequeue();
         if (Vector2.Distance(center, curr) > WorldManager.waterDistanceLimit) {
            continue;
         }
         visited.Add(curr);
         //if (!printed) Debug.Log(curr);
         if (WorldManager.terrainHeightMap[curr.x, curr.y] <= 0f) {
            return Vector2.Distance(center, curr);
         }
         if (curr.x > 0) AddIfUnvisited(curr + Vector2Int.left);
         if (curr.x < WorldManager.dim - 1) AddIfUnvisited(curr + Vector2Int.right);
         if (curr.y < WorldManager.dim - 1) AddIfUnvisited(curr + Vector2Int.up);
         if (curr.y > 0) AddIfUnvisited(curr + Vector2Int.down);
      }

      /*float currStraightDist = 0;
      float currDiagDist = 0;

      // Init in 8 directions
      for (int d = 0; d < octDeltas.Length; d++) {
         octState[d] = center;
      }

      while (currStraightDist < maxDistance || currDiagDist < maxDistance) {
         // Expand ring in 8 directions
         for (int d = 0; d < octDeltas.Length; d++) {
            if ((d % 2 == 0 && currStraightDist < maxDistance) || // even = straight
                (d % 2 != 0 && currDiagDist < maxDistance)) {     // odd = diag
               octState[d] += octDeltas[d];
               if (octState[d].x < 0) {
                  octState[d].x = 0;
               }
               if (octState[d].x >= WorldManager.dim) {
                  octState[d].x = WorldManager.dim - 1;
               }
               if (octState[d].y < 0) {
                  octState[d].y = 0;
               }
               if (octState[d].y >= WorldManager.dim) {
                  octState[d].y = WorldManager.dim - 1;
               }

               if (WorldManager.terrainHeightMap[octState[d].x, octState[d].y] <= 0f) {
                  return Vector2.Distance(center, octState[d]);
               }
            }
         }

         // Update curr distances
         //if (currStraightDist < maxDistance) {
         currStraightDist++;
         //}
         //if (currDiagDist < maxDistance) {
         currDiagDist += Mathf.Sqrt(2);
         //}
      }*/

      return maxDistance;
   }
}
