using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hp {
   // Static Hyperparameters class

   // Randomness also decreases griddiness
   public static float arterialGridding = 1f; // point shifting from highways proximity
   public static float arterialRandomness = 1f; // added randomness to points

   public static float localGridding = 1f;
   public static float localRandomness = 1f;
}
