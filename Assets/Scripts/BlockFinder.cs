﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockFinder {
   public static List<Block> FindBlocks(Dictionary<Vector2, HashSet<Vector2>> localGraph, List<Vector2> intersections, float primaryDir, float secondaryDir) {
      List<Block> blocks = new List<Block>();
      List<Vector2> searchSrc = new List<Vector2>(intersections);

      // TODO sometimes no intersections, also need to add general srcs (every third)
      HashSet<Vector2> intersectionSet = new HashSet<Vector2>(intersections); // to not double add
      int i = 1;
      foreach (KeyValuePair<Vector2, HashSet<Vector2>> pair in localGraph) {
         if (i % 3 == 0) {
            if (!intersectionSet.Contains(pair.Key)) searchSrc.Add(pair.Key);
         }
         i++;
      }

      // (intersection center, intersection child edge)
      HashSet<(Vector2, Vector2)> explored = new HashSet<(Vector2, Vector2)>();
      // for each intersection
      foreach (Vector2 intV in searchSrc) {
         foreach (Vector2 childV in localGraph[intV]) {
            (Vector2, Vector2) srcTup = (intV, childV);
            if (!explored.Contains(srcTup)) {
               explored.Add(srcTup);

               Block block = SearchBlockBidirectional(localGraph, intV, childV, primaryDir, secondaryDir);
               bool alreadyFound = false;
               foreach (Block b in blocks) {
                  if (block.IsSame(b)) {
                     alreadyFound = true;
                  }
               }
               if (!alreadyFound) {
                  blocks.Add(block);
               }
            }
         }
      }

      return blocks;
   }

   // Spawns two search instances in opposite directions and joins results
   private static Block SearchBlockBidirectional(
      Dictionary<Vector2, HashSet<Vector2>> localGraph, 
      Vector2 source, Vector2 curr, 
      float primaryDir, float secondaryDir) {
      // search first direction
      ArrayList first = SearchBlock(localGraph, true, new ArrayList() { source }, curr);

      // search second direction
      Vector2 dirFrom = curr - source;
      (Vector2, float) tup = ChooseNext(localGraph, false, source, dirFrom);
      //Debug.Log("sec next " + tup.Item1 + " " + source);
      if (tup.Item2 != 360) {
         ArrayList second = SearchBlock(localGraph, false, new ArrayList() { source }, tup.Item1);
         if (second.Count > 1) {
            for (int i = 1; i < second.Count; i++) {
               Vector2 v = (Vector2)second[i];
               first.Insert(0, v);
            }
         }
      }
      Block block = new Block(first, primaryDir, secondaryDir);
      return block;
   }

   private static ArrayList SearchBlock(Dictionary<Vector2, HashSet<Vector2>> localGraph, bool dir, ArrayList history, Vector2 curr) {
      //Debug.Log("Starting area search: " + history.Count + " " + curr);
      history.Add(curr);

      // END if (back at source vert) or (too far from source)
      if ((Vector2)history[0] == curr || /*((Vector2)history[0] - curr).magnitude > 2000 ||*/ history.Count > 70) {
         return history;
      }

      Vector2 dirFrom = (Vector2)history[history.Count - 2] - curr;
      (Vector2, float) tup = ChooseNext(localGraph, dir, curr, dirFrom);
      Vector2 nextVec = tup.Item1;
      float minAngle = tup.Item2;

      //Debug.Log("dir: " + dir + " minAngle: " + minAngle + " from" + curr + " to" + nextVec);

      if (minAngle < 360f) {
         history = SearchBlock(localGraph, dir, history, nextVec); //recurse
      }
      return history;
   }

   private static (Vector2, float) ChooseNext(Dictionary<Vector2, HashSet<Vector2>> localGraph, bool dir, Vector2 v, Vector2 dirFrom) {
      float minAngle = 360f;
      Vector2 nextVec = Vector2.zero;
      //Vector2 dirFrom = (Vector2)history[history.Count - 2] - curr;

      int dirSignMultiplier = dir ? 1 : -1;

      // pick leftmost neighbor
      foreach (Vector2 vecNeighbor in localGraph[v]) {
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
