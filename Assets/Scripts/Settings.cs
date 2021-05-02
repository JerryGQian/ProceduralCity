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
   public static bool production = true;

   // Region Markers
   public static bool renderRegionMarkers = false && !production;

   // Highways Network
   public static bool renderDensityCenters = false && !production;
   public static bool renderHighwayLayout = false && !production;
   public static bool renderHighwayPaths = true || production;

   // Arterial Network
   public static bool renderArterialLayout = false && !production;
   public static bool renderArterialPoints = false && !production;
   public static bool renderArterialEdges = false && !production;
   public static bool renderArterialPaths = true || production;

   // Areas/Local Network
   public static bool renderAreaDebug = true && !production;
   public static bool renderAreaSeeds = true && !production;
   public static bool renderAreaIntersections = true && !production;
   public static bool renderAreaLocal = true || production;
   public static bool optimizeLocalRoads = true || production;

   // Blocks
   public static bool renderLotEdges = false && !production;
   public static bool renderBuildings = false && !production;

}
