using System.Collections;
using System.Collections.Generic;

public class SpatialObject {
   public float x;
   public float z;
}

public abstract class QuadTree {
   public Bounds bounds;
   public int bucketLimit;
   public int count;
   public QuadTreeNode root;

   public QuadTree(Bounds bounds, int bucketLimit) {
      this.bounds = bounds;
      this.bucketLimit = bucketLimit;
      this.count = 0;
      this.root = new QuadTreeBucket(bounds, bucketLimit);
   }

   public abstract class QuadTreeNode {
      public Bounds bounds;
      public int bucketLimit;
      public int count;

      public QuadTreeNode(Bounds bounds, int bucketLimit) {
         this.bounds = bounds;
         this.bucketLimit = bucketLimit;
         this.count = 0;
      }

      public abstract void Add(SpatialObject obj);
   }

   public class QuadTreeSplit : QuadTreeNode {
      public QuadTreeNode[] children;

      public QuadTreeSplit(Bounds bounds, int bucketLimit) : base(bounds, bucketLimit) {
         children = new QuadTreeNode[4];
         for (int i = 0; i < 4; i++) {
            children[i] = new QuadTreeBucket(bounds, bucketLimit);
         }
      }

      public override void Add(SpatialObject obj) {
         LocateChild(obj).Add(obj);
      }

      private QuadTreeNode LocateChild(SpatialObject obj) {
         float xMid = (bounds.xMin + bounds.xMax) / 2;
         float zMid = (bounds.zMin + bounds.zMax) / 2;
         QuadTreeNode node;
         if (obj.x < xMid) {
            if (obj.z < zMid) {
               node = children[2];
            }
            else {
               node = children[0];
            }
         }
         else {
            if (obj.z < zMid) {
               node = children[3];
            }
            else {
               node = children[1];
            }
         }
         return node;
      }
   }

   public class QuadTreeBucket : QuadTreeNode {
      public ArrayList list;

      public QuadTreeBucket(Bounds bounds, int bucketLimit) : base(bounds, bucketLimit) {
         list = new ArrayList();
      }

      public override void Add(SpatialObject obj) {
         if (count < bucketLimit) {
            list.Add(obj);
         }
         else {
            
         }
      }
   }

   public void Add(SpatialObject obj) {
      root.Add(obj);
   }
}
