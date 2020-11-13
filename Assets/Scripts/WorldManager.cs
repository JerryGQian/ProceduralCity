using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour {

   public static int dim = 500;
   public static int dimArea = dim*dim;
   public static float[,] landValueMap = new float[dim, dim];
   public static float[,] terrainHeightMap = new float[dim, dim];
   public static float[,] waterDistanceMap = new float[dim, dim];
   public static float[] waterDistanceArr = new float[dimArea];

   public static float waterDistanceLimit = 60.5f;

   public GameObject MeshGO;

   private GameObject[] chunks;

   // Start is called before the first frame update
   void Start() {
      
   }

   // Update is called once per frame
   void Update() {

   }


}
