using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings {
   // Static Settings class
   /////////////////////////////////////////////////
   // Generation params ////////////////////////////
   /////////////////////////////////////////////////
   public static int chunkLoadingIncrement = 8;

   /////////////////////////////////////////////////
   // Render settings //////////////////////////////
   /////////////////////////////////////////////////

   // End Product Visualization, only final results (overrides manual markings)
   public static bool production = false;

   // Region Markers
   public static bool renderRegionMarkers = false && !production;

   // Highways Network
   public static bool renderDensityCenters = false && !production;
   public static bool renderHighwayLayout = false && !production;
   public static bool renderHighwayPaths = true || production;

   // Arterial Network
   public static bool renderArterialLayout = true && !production;
   public static bool renderArterialPoints = true && !production;
   public static bool renderArterialEdges = false && !production;
   public static bool renderArterialPaths = false || production;

   // Areas/Local Network
   public static bool renderAreaDebug = false && !production;
   public static bool renderAreaSeeds = false && !production;
   public static bool renderAreaIntersections = false && !production;
   public static bool renderAreaLocal = false || production;

   // Blocks
   public static bool renderLotEdges = false && !production;
   public static bool renderBuildings = false && !production;

}
