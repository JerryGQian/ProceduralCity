using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using TriangleNet.Tools;

public static class Util {

   private static Vector2 angleBase = new Vector2(1, 0);

   public static string List2String(ArrayList list) {
      string s = "";
      for (int i = 0; i < list.Count; i++) {
         s += list[i] + ", ";
      }
      return s;
   }

   // World coordinate to Region index
   public static Vector2Int W2R(Vector2 worldCoord) {
      int regionSize = WorldManager.regionDim * WorldManager.chunkSize;

      if (worldCoord.x < 0) {
         worldCoord.x -= regionSize;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= regionSize;
      }
      return new Vector2Int((int)(worldCoord.x / regionSize), (int)(worldCoord.y / regionSize));
   }

   public static Vector2Int RegionIdx2ChunkCenter(Vector2Int regionIdx) {
      int regionOffsetMag = (WorldManager.regionDim - 1) / 2;

      return new Vector2Int((regionIdx.x * WorldManager.regionDim) + regionOffsetMag, (regionIdx.y * WorldManager.regionDim) + regionOffsetMag);
   }

   // World coordinate to Chunk index
   public static Vector2Int W2C(Vector2 worldCoord) {

      if (worldCoord.x < 0) {
         worldCoord.x -= WorldManager.chunkSize;
      }
      if (worldCoord.y < 0) {
         worldCoord.y -= WorldManager.chunkSize;
      }
      return new Vector2Int((int)(worldCoord.x / WorldManager.chunkSize), (int)(worldCoord.y / WorldManager.chunkSize));
   }
   public static Vector2Int W2C(Vector3 worldCoord) {
      return W2C(new Vector2(worldCoord.x, worldCoord.z));
   }

   // Chunk index to World coordinate of chunk center
   public static Vector2 C2W(Vector2Int worldCoord) {
      return new Vector2Int((int)(worldCoord.x * WorldManager.chunkSize) + WorldManager.chunkSize / 2, (int)(worldCoord.y * WorldManager.chunkSize) + WorldManager.chunkSize / 2);
   }

   public static bool SameVec2(Vector2 a, Vector2 b) {
      return (a.x == b.x && a.y == b.y) || (a.x == b.y && a.y == b.x);
   }

   public static bool SameEdge((Vector2Int, Vector2Int) e1, (Vector2Int, Vector2Int) e2) {
      return (SameVec2(e1.Item1, e2.Item1) && SameVec2(e1.Item2, e2.Item2)) || (SameVec2(e1.Item1, e2.Item2) && SameVec2(e1.Item2, e2.Item1));
   }

   public static Vector2Int VecToVecInt(Vector2 v) {
      return new Vector2Int((int)v.x, (int)v.y);
   }



   // Delaunay utils
   

   public static bool IsSameVertex(Vertex v0, Vertex v1) {
      return v0.X == v1.X && v0.Y == v1.Y;
   }

   public static float VertexDistance(Vertex v0, Vertex v1) {
      return (new Vector2((float)v0.X, (float)v0.Y) - new Vector2((float)v1.X, (float)v1.Y)).magnitude;
   }

   public static float VertexAngle(Vertex v0, Vertex v1) {
      Vector2 vec0 = new Vector2((float)v0.X, (float)v0.Y);
      Vector2 vec1 = new Vector2((float)v1.X, (float)v1.Y);

      return Vector2.Angle(vec0, vec1);
   }

   public static Vertex Vector2ToVertex(Vector2 vec) {
      return new Vertex(vec.x, vec.y);
   }

   public static Vector2 VertexToVector2(Vertex vert) {
      return new Vector2((float)vert.X, (float)vert.Y);
   }

   public static Vector2Int VertexToVector2Int(Vertex vert) {
      return new Vector2Int((int)vert.X, (int)vert.Y);
   }
}
