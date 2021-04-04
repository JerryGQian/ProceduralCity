using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaFinder {
   /*List<Vector2> sourcePoints;
   public AreaFinder(List<Vector2> sourcePoints) {
      this.sourcePoints = sourcePoints;

   }*/

   public static List<Area> FindAreas(List<Vector2> sourcePoints) {
      List<Area> areas = new List<Area>();
      foreach (Vector2 v in sourcePoints) {
         foreach (Vector2 vn in WorldManager.roadGraph[v]) {
            // start search
            //Debug.Log("Starting... " + v + " " + vn);
            Area area = SearchAreaBidirectional(v, vn);
            bool alreadyFound = false;
            foreach (Area a in areas) {
               if (area.IsSame(a)) {
                  alreadyFound = true;
                  //Debug.Log("Duplicate found " + area.verts.Count);
               }
            }
            if (!alreadyFound) {
               //Debug.Log("Area: " + area.verts.Count + " " + Util.List2String(area.verts));
               areas.Add(area);
            }
         }
      }

      return areas;
   }

   // Spawns two search instances in opposite directions and joins results
   private static Area SearchAreaBidirectional(Vector2 source, Vector2 curr) {
      // search first direction
      ArrayList first = SearchArea(true, new ArrayList() { source }, curr);

      // search second direction
      Vector2 dirFrom = curr - source;
      (Vector2, float) tup = ChooseNext(false, source, dirFrom);
      //Debug.Log("sec next " + tup.Item1 + " " + source);
      if (tup.Item2 != 360) {
         ArrayList second = SearchArea(false, new ArrayList() { source }, tup.Item1);
         if (second.Count > 1) {
            for (int i = 1; i < second.Count; i++) {
               Vector2 v = (Vector2)second[i];
               first.Insert(0, v);
            }
         }
      }
      //Debug.Log("first " + Util.List2String(first));
      //Debug.Log("second " + Util.List2String(second));
      
      //Debug.Log("Area: " + first.Count + " " + Util.List2String(first));
      Area area = new Area(first);
      return area;
   }

   private static ArrayList SearchArea(bool dir, ArrayList history, Vector2 curr) {
      //Debug.Log("Starting area search: " + history.Count + " " + curr);
      history.Add(curr);

      // END if (back at source vert) or (too far from source)
      if ((Vector2)history[0] == curr || /*((Vector2)history[0] - curr).magnitude > 2000 ||*/ history.Count > 70) {
         return history;
      }

      Vector2 dirFrom = (Vector2)history[history.Count - 2] - curr;
      (Vector2, float) tup = ChooseNext(dir, curr, dirFrom);
      Vector2 nextVec = tup.Item1;
      float minAngle = tup.Item2;

      //Debug.Log("dir: " + dir + " minAngle: " + minAngle + " from" + curr + " to" + nextVec);

      if (minAngle < 360f) {
         history = SearchArea(dir, history, nextVec); //recurse
      }
      return history;
   }

   private static (Vector2, float) ChooseNext(bool dir, Vector2 v, Vector2 dirFrom) {
      float minAngle = 360f;
      Vector2 nextVec = Vector2.zero;
      //Vector2 dirFrom = (Vector2)history[history.Count - 2] - curr;

      int dirSignMultiplier = dir ? 1 : -1;

      // pick leftmost neighbor
      foreach (Vector2 vecNeighbor in WorldManager.roadGraph[v]) {
         Vector2 dirNext = vecNeighbor - v;
         var sign = dirSignMultiplier * Mathf.Sign(dirNext.x * dirFrom.y - dirNext.y * dirFrom.x);
         float angleDiff = sign * Vector2.Angle(dirFrom, dirNext);
         if (angleDiff < 0) {
            angleDiff += 360;
         }
         //Debug.Log(v + " " + vecNeighbor + " : dirs from" + dirFrom + " next" + dirNext + " " + (angleDiff));
         if (angleDiff != 0 && angleDiff < minAngle && v != vecNeighbor) {
            minAngle = angleDiff;
            nextVec = vecNeighbor;
         }
      }

      return (nextVec, minAngle);
   }

}
