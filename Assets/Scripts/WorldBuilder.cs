using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;

public class WorldBuilder : MonoBehaviour {
   public WorldManager wm;
   public GameObject indicator; // for visualization
   public GameObject yellowCube;
   public GameObject purpleCube;
   public static Dictionary<(Vector2, Vector2), bool> builtHighways = new Dictionary<(Vector2, Vector2), bool>();
   public static Vector2 SignalVector = new Vector2(999999, 999999); // Unique vector that signals something, eg a jump in highways
   // Spatial hashing of highway vertices for intersection management, <chunkIdx, List<vertex,(parent vertices)>>
   public static Dictionary<Vector2Int, List<(Vector2, (Vector2Int, Vector2Int))>> highwayVertChunkHash = new Dictionary<Vector2Int, List<(Vector2, (Vector2Int, Vector2Int))>>();
   private static int hashChunkWidth = 2;

   void Start() {

   }

   public void BuildHighway(HighwayGenerator hwg, Vector2Int regionIdx) {
      ArrayList vert = new ArrayList();
      foreach (Vertex v in hwg.vertices) {
         vert.Add(v);
      }

      //visualize regions
      Region region = WorldManager.regions[regionIdx];
      Bounds regionBounds = region.bounds;
      GameObject boundMarker = Instantiate(purpleCube, new Vector3(regionBounds.xMin, 20, regionBounds.zMin), Quaternion.AngleAxis(0, Vector3.up));
      boundMarker.name = "BoundMarker " + regionIdx;
      Transform bmtrans = boundMarker.GetComponent<Transform>();
      bmtrans.localScale = new Vector3(20, 10, 20);

      boundMarker = Instantiate(purpleCube, new Vector3(regionBounds.xMin, 20, regionBounds.zMax), Quaternion.AngleAxis(0, Vector3.up));
      boundMarker.name = "BoundMarker " + regionIdx;
      bmtrans = boundMarker.GetComponent<Transform>();
      bmtrans.localScale = new Vector3(20, 10, 20);

      boundMarker = Instantiate(purpleCube, new Vector3(regionBounds.xMax, 20, regionBounds.zMax), Quaternion.AngleAxis(0, Vector3.up));
      boundMarker.name = "BoundMarker " + regionIdx;
      bmtrans = boundMarker.GetComponent<Transform>();
      bmtrans.localScale = new Vector3(20, 10, 20);

      boundMarker = Instantiate(purpleCube, new Vector3(regionBounds.xMax, 20, regionBounds.zMin), Quaternion.AngleAxis(0, Vector3.up));
      boundMarker.name = "BoundMarker " + regionIdx;
      bmtrans = boundMarker.GetComponent<Transform>();
      bmtrans.localScale = new Vector3(20, 10, 20);



      if (false) {
         foreach (Edge e in hwg.edges) {
            Vector2 P0 = new Vector2((float)((Vertex)vert[e.P0]).X, (float)((Vertex)vert[e.P0]).Y);
            Vector2 P1 = new Vector2((float)((Vertex)vert[e.P1]).X, (float)((Vertex)vert[e.P1]).Y);

            //if (wm.regions[regionIdx].bounds.InBounds(P0) || wm.regions[regionIdx].bounds.InBounds(P1)) {
            if (InPatchBounds(regionIdx, P0, P1)) {
               float dist = (P0 - P1).magnitude;
               //Debug.Log(P0 + " -> " + P1 + " " + dist);

               float x = (float)(P0.x + P1.x) / 2;
               float y = (float)(P0.y + P1.y) / 2;

               Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

               float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

               GameObject segment = Instantiate(yellowCube, new Vector3(x, 20, y), Quaternion.AngleAxis(angle, Vector3.up));
               segment.name = "Edge " + P0 + " " + P1;
               Transform trans = segment.GetComponent<Transform>();
               //trans.position = new Vector3(x, 0, y);
               //trans.rotation = Quaternion.AngleAxis(angle, Vector3.up);
               trans.localScale = new Vector3(vec.magnitude, 1, 2);
            }
         }
      }

      if (hwg.highways != null && hwg.highways.Count > 0) {
         //Debug.Log("Segments Count: " + hwg.segmentVertices.Count);
         foreach (ArrayList segments in hwg.highways) {
            Vector2 PStart = (Vector2)segments[0];
            Vector2 PEnd = (Vector2)segments[segments.Count - 1];
            Vector2Int PStartInt = new Vector2Int((int)PStart.x, (int)PStart.y);
            Vector2Int PEndInt = new Vector2Int((int)PEnd.x, (int)PEnd.y);
            //Debug.Log(segments);
            if (!builtHighways.ContainsKey((PStartInt, PEndInt)) && !builtHighways.ContainsKey((PEndInt, PStartInt))) {
               builtHighways[(PStartInt, PEndInt)] = true;
               builtHighways[(PEndInt, PStartInt)] = true;
               //Debug.Log("Generating: " + PStartInt + " " + PEndInt);

               for (int i = 0; i < segments.Count - 1; i++) {
                  Vector2 P0 = (Vector2)segments[i];
                  Vector2 P1 = (Vector2)segments[i + 1];

                  // Path disconnect if signal, skip 2 segments!
                  if (P0 == SignalVector || P1 == SignalVector) {
                     continue;
                  }

                  Vector2 topDownVec = new Vector2(P1.x - P0.x, P1.y - P0.y);
                  // 3D version of points with terrain height
                  Vector3 P0_3 = new Vector3(P0.x, TerrainGen.GenerateTerrainAt((int)P0.x, (int)P0.y), P0.y);
                  Vector3 P1_3 = new Vector3(P1.x, TerrainGen.GenerateTerrainAt((int)P1.x, (int)P1.y), P1.y);
                  float dist = (P0_3 - P1_3).magnitude;
                  float incline = Mathf.Atan2(topDownVec.magnitude, P0_3.y - P1_3.y) * Mathf.Rad2Deg + 90;



                  float x = (float)(P0.x + P1.x) / 2;
                  float y = (float)(P0.y + P1.y) / 2;


                  float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

                  float objHeight = (TerrainGen.GenerateTerrainAt((int)P0_3.x, (int)P0_3.z) + TerrainGen.GenerateTerrainAt((int)P1_3.x, (int)P1_3.z)) / 2;
                  if (objHeight < 0) {
                     objHeight = 1f;
                  }
                  GameObject segment = Instantiate(purpleCube, new Vector3(x, objHeight, y), Quaternion.AngleAxis(angle, Vector3.up));
                  segment.transform.eulerAngles = new Vector3(
                      segment.transform.eulerAngles.x,
                      angle + 180,
                      incline
                  );
                  segment.name = "HighwaySeg " + PStart + " " + PEnd;
                  Transform trans = segment.GetComponent<Transform>();
                  trans.localScale = new Vector3(dist, 1, 4); //topDownVec.magnitude

               }

            }
         }
      }
   }

   public void BuildArterial(ArterialGenerator atg, Vector2Int regionIdx) {
      Region region = WorldManager.regions[regionIdx];
      foreach (KeyValuePair<Vector2Int, ArrayList> regionPair in region.atg.arterialPointsByRegion) {
         Region curRegion = WorldManager.regions[regionPair.Key];
         if (!curRegion.arterialLayoutBuilt) {
            curRegion.arterialLayoutBuilt = true;
            foreach (Vector2 point in atg.arterialPointsByRegion[regionPair.Key]) {
               GameObject obj = Instantiate(purpleCube, new Vector3(point.x, 18, point.y), Quaternion.identity);
               obj.name = "ArterialPoint " + point;
               Transform trans = obj.GetComponent<Transform>();
               trans.localScale = new Vector3(6, 4, 6);
            }
         }
      }

      foreach (Vector2 point in atg.initialArterialPoints) {
         GameObject obj = Instantiate(yellowCube, new Vector3(point.x, 20, point.y), Quaternion.identity);
         obj.name = "InitialArterialPoint " + point;
         Transform trans = obj.GetComponent<Transform>();
         trans.localScale = new Vector3(2, 2, 2);
      }

      //
      ArrayList vert = new ArrayList();
      foreach (Vertex v in atg.vertices) {
         vert.Add(v);
      }

      if (true) {
         foreach (Edge e in atg.edges) {
            Vector2 P0 = new Vector2((float)((Vertex)vert[e.P0]).X, (float)((Vertex)vert[e.P0]).Y);
            Vector2 P1 = new Vector2((float)((Vertex)vert[e.P1]).X, (float)((Vertex)vert[e.P1]).Y);

            //if (wm.regions[regionIdx].bounds.InBounds(P0) || wm.regions[regionIdx].bounds.InBounds(P1)) {
            if (InPatchBounds(regionIdx, P0, P1)) {
               //float dist = (P0 - P1).magnitude;
               //Debug.Log(P0 + " -> " + P1 + " " + dist);

               float x = (float)(P0.x + P1.x) / 2;
               float y = (float)(P0.y + P1.y) / 2;

               Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

               float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

               GameObject segment = Instantiate(yellowCube, new Vector3(x, 20, y), Quaternion.AngleAxis(angle, Vector3.up));
               segment.name = "ArtEdge " + P0 + " " + P1;
               Transform trans = segment.GetComponent<Transform>();
               //trans.position = new Vector3(x, 0, y);
               //trans.rotation = Quaternion.AngleAxis(angle, Vector3.up);
               trans.localScale = new Vector3(vec.magnitude, 1, 2);
            }
         }
      }
   }

   public static bool InPatchBounds(Vector2Int regionIdx, Vector2 P0, Vector2 P1) {
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            if (WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P0) || WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P1)) {
               return true;
            }
         }
      }
      return false;
   }

   //Highway collision
   public static void AddHighwayVertToChunkHash(Vector2 vert, (Vector2Int, Vector2Int) edge) {
      Vector2Int chunk = HashChunkGrouping(Util.W2C(vert));
      if (!highwayVertChunkHash.ContainsKey(chunk)) {
         highwayVertChunkHash[chunk] = new List<(Vector2, (Vector2Int, Vector2Int))>();
      }

      highwayVertChunkHash[chunk].Add((vert, edge));
   }

   public static List<(Vector2, (Vector2Int, Vector2Int))> GetHighwayVertList(Vector2 vert) {
      Vector2Int chunk = HashChunkGrouping(Util.W2C(vert));
      if (!highwayVertChunkHash.ContainsKey(chunk)) {
         return null;
      }

      return highwayVertChunkHash[chunk];
   }

   public static bool DoesChunkContainHighway(Vector2 vert) {
      Vector2Int chunk = HashChunkGrouping(Util.W2C(vert));
      return highwayVertChunkHash.ContainsKey(chunk);
   }
   public static Vector2Int HashChunkGrouping(Vector2Int chunk) {
      if (chunk.x < 0) {
         chunk.x -= 1;
      }
      if (chunk.y < 0) {
         chunk.y -= 1;
      }
      return chunk / hashChunkWidth;
   }


}
