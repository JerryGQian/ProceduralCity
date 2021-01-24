using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;

public class WorldBuilder : MonoBehaviour {
   public WorldManager wm;
   public GameObject indicator; // for visualization
   public GameObject cube;

   void Start() {

   }

   public void BuildHighway(ArrayList edges, ArrayList vertices, Vector2Int regionIdx) {
      ArrayList vert = new ArrayList();
      foreach (Vertex v in vertices) {
         vert.Add(v);
      }

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

            GameObject segment = Instantiate(cube, new Vector3(x, 15, y), Quaternion.AngleAxis(angle, Vector3.up));
            Transform trans = segment.GetComponent<Transform>();
            //trans.position = new Vector3(x, 0, y);
            //trans.rotation = Quaternion.AngleAxis(angle, Vector3.up);
            trans.localScale = new Vector3(vec.magnitude, 1, 2);
         }
      }
   }

   private bool InPatchBounds(Vector2Int regionIdx, Vector2 P0, Vector2 P1) {
      for (int i = -1; i <= 1; i++) {
         for (int j = -1; j <= 1; j++) {
            if (WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P0) || WorldManager.regions[regionIdx + new Vector2Int(i, j)].bounds.InBounds(P1)) {
               return true;
            }
         }
      }
      return false;
   }

   /*public void BuildHighway(IMesh mesh, Vector2Int regionIdx) {
      ArrayList vert = new ArrayList();
      foreach (var v in mesh.Vertices) {
         vert.Add(v);
      }

      foreach (var e in mesh.Edges) {
         Vector2 P0 = new Vector2((float)((Vertex)vert[e.P0]).X, (float)((Vertex)vert[e.P0]).Y);
         Vector2 P1 = new Vector2((float)((Vertex)vert[e.P1]).X, (float)((Vertex)vert[e.P1]).Y);

         if (WorldManager.regions[regionIdx].bounds.InBounds(P0) || WorldManager.regions[regionIdx].bounds.InBounds(P1)) {
            float dist = (P0 - P1).magnitude;
            //Debug.Log(P0 + " -> " + P1 + " " + dist);

            float x = (float)(P0.x + P1.x) / 2;
            float y = (float)(P0.y + P1.y) / 2;

            Vector2 vec = new Vector2(P1.x - P0.x, P1.y - P0.y);

            float angle = Mathf.Atan2(P1.x - P0.x, P1.y - P0.y) * Mathf.Rad2Deg + 90;

            GameObject segment = Instantiate(cube, new Vector3(x, 15, y), Quaternion.AngleAxis(angle, Vector3.up));
            Transform trans = segment.GetComponent<Transform>();
            //trans.position = new Vector3(x, 0, y);
            //trans.rotation = Quaternion.AngleAxis(angle, Vector3.up);
            trans.localScale = new Vector3(vec.magnitude, 1, 2);
         }
      }
   }*/
}
