using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityTest : MonoBehaviour {
   public static int dim = WorldManager.dim;
   public float neighborPower = 0.4f; //Proportion of density from avg neighbor values vs land value 
   public Gradient gradient;
   private float[,] landValueMap = new float[dim, dim];
   private float[,] densityMap = new float[dim, dim];
   private float max = 0;
   private float min = 9999f;

   public class Point {
      public int x;
      public int z;
      public Point(int x, int z) {
         this.x = x;
         this.z = z;
      }
   }

   // Start is called before the first frame update
   void Start() {
      UnityEngine.Random.InitState(42);
      GenLandValues();
      GenDensity();
      RenderDensityMap();
   }

   void RenderDensityMap() {
      for (int z = 0; z < dim; z++) {
         for (int x = 0; x < dim; x++) {
            if (densityMap[x, z] < 0) {
               densityMap[x, z] = 0f;
            }
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(x, densityMap[x, z] / 2f, z);
            cube.transform.localScale = new Vector3(1f, densityMap[x, z], 1f);
            var cubeRenderer = cube.GetComponent<Renderer>();
            cubeRenderer.material.SetColor("_Color", ColorFromGradient((densityMap[x, z]-min)  / max));
         }
      }
   }

   Color ColorFromGradient(float value) {
      return gradient.Evaluate(value);
   }

   void GenLandValues() {
      for (int z = 0; z < dim; z++) {
         for (int x = 0; x < dim; x++) {
            landValueMap[x, z] = Mathf.PerlinNoise(x * .008f, z * .008f);
            //Debug.Log(x + " " + z + " " + landValueMap[x, z]);
         }
      }
   }

   void GenDensity() {
      ArrayList coordsToProcess = new ArrayList();
      ArrayList nextCoordsToProcess = new ArrayList();
      /*for (int i = 0; i < dim - 1; i++) {
         nextCoordsToProcess.Add(new Point(i, 0));
         nextCoordsToProcess.Add(new Point(dim - 1, i));
         nextCoordsToProcess.Add(new Point(i, dim - 1));
         nextCoordsToProcess.Add(new Point(0, i));
      }*/
      coordsToProcess.Add(new Point(0, 0));
      coordsToProcess.Add(new Point(dim - 1, 0));
      coordsToProcess.Add(new Point(dim - 1, dim - 1));
      coordsToProcess.Add(new Point(0, dim - 1));

      InitDensity(-1f);

      while (coordsToProcess.Count > 0) {
         int idx = UnityEngine.Random.Range(0, coordsToProcess.Count);
         Point p = (Point)coordsToProcess[idx];
         densityMap[p.x, p.z] = CalcDensity(p.x, p.z);
         coordsToProcess.RemoveAt(idx);
         foreach (Point candidate in GetQueueableAdjacents(p.x, p.z)) {
            coordsToProcess.Add(candidate);
         }
      }

      /*while (nextCoordsToProcess.Count > 0) {
         coordsToProcess = nextCoordsToProcess;
         nextCoordsToProcess = new ArrayList();

         while (coordsToProcess.Count > 0) {
            int idx = UnityEngine.Random.Range(0, coordsToProcess.Count);
            Point p = (Point)coordsToProcess[idx];
            densityMap[p.x, p.z] = CalcDensity(p.x, p.z);
            foreach (Point candidate in GetQueueableAdjacents(p.x, p.z)) {
               nextCoordsToProcess.Add(candidate);
            }
            coordsToProcess.RemoveAt(idx);
         }
      }*/
   }

   void InitDensity(float val) {
      for (int z = 0; z < dim; z++) {
         for (int x = 0; x < dim; x++) {
            densityMap[x, z] = val;
         }
      }
   }

   float CalcDensity(int x, int z) {
      float sum = 0f;
      int count = 0;

      void AddIf(int X, int Z) {
         float val = GetDensity(X, Z);
         if (val >= 0) {
            sum += val;
            count++;
         }
      }

      AddIf(x - 1, z - 1);
      AddIf(x, z - 1);
      AddIf(x + 1, z - 1);
      AddIf(x + 1, z);
      AddIf(x + 1, z + 1);
      AddIf(x, z + 1);
      AddIf(x - 1, z + 1);
      AddIf(x - 1, z);

      float avg = 1f;
      if (count > 0) {
         avg = sum / (float)count;
      }
      //Debug.Log(x + " " + z + " : " + neighborPower * avg + " + " + (1f - neighborPower) * landValueMap[x, z]);

      float density = (1.2f*avg + avg*(2f*landValueMap[x, z] - 1f));

      if (density > max) {
         max = density;
      }
      if (density < min) {
         min = density;
      }

      return density;
   }

   float GetDensity(int x, int z) {
      if (x < 0 || x >= dim || z < 0 || z >= dim) {
         return 1f;
      }

      return densityMap[x, z];
   }

   bool IsQueueable(int x, int z) {
      if (x < 0 || x >= dim || z < 0 || z >= dim) {
         return false;
      }
      return densityMap[x, z] < 0f;
   }

   ArrayList GetQueueableAdjacents(int x, int z) {
      ArrayList queueList = new ArrayList();

      if (x + 1 < dim - 1 && IsQueueable(x + 1, z)) {
         queueList.Add(new Point(x + 1, z));
      }
      if (x - 1 >= 0 && IsQueueable(x - 1, z)) {
         queueList.Add(new Point(x - 1, z));
      }
      if (z + 1 < dim - 1 && IsQueueable(x, z + 1)) {
         queueList.Add(new Point(x, z + 1));
      }
      if (z - 1 >= 0 && IsQueueable(x, z - 1)) {
         queueList.Add(new Point(x, z - 1));
      }

      return queueList;
   }
}
