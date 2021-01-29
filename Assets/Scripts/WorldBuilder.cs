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

   void Start() {

   }

   public void BuildHighway(HighwayGenerator hwg, ArrayList edges, ArrayList vertices, Vector2Int regionIdx) {
      ArrayList vert = new ArrayList();
      foreach (Vertex v in vertices) {
         vert.Add(v);
      }

      if (true) {
         foreach (Edge e in edges) {
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

                  float dist = (P0 - P1).magnitude;
                  //Debug.Log(P0 + " -> " + P1 + " " + dist);

                  float x = (float)(P0.x + P1.x) / 2;
                  float y = (float)(P0.y + P1.y) / 2;

                  Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

                  float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

                  GameObject segment = Instantiate(purpleCube, new Vector3(x, 10, y), Quaternion.AngleAxis(angle, Vector3.up));
                  segment.name = "HighwaySeg " + PStart + " " + PEnd;
                  Transform trans = segment.GetComponent<Transform>();
                  trans.localScale = new Vector3(vec.magnitude, 1, 4);

               }

               //Debug.Log("Segments: " + P0 + " " + P1);

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

   
}
